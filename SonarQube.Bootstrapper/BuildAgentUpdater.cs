//-----------------------------------------------------------------------
// <copyright file="BuildAgentUpdater.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;

namespace SonarQube.Bootstrapper
{
    public class BuildAgentUpdater : IBuildAgentUpdater
    {
        public /* for test purposes */ const string LoaderTargetsName = "SonarQube.Integration.ImportBefore.targets";

        public /* for test purposes */ static IReadOnlyList<string> DestinationDirs
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return new string[]
                    {
                        Path.Combine(appData, "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
                        Path.Combine(appData, "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
                        Path.Combine(appData, "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore")
                    };
            }
        }

        /// <summary>
        /// Gets a zip file containing the pre/post processors from the server
        /// </summary>
        /// <param name="hostUrl">The server Url</param>
        public bool TryUpdate(string hostUrl, string targetDir, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(hostUrl))
            {
                throw new ArgumentNullException("hostUrl");
            }
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                throw new ArgumentNullException("targetDir");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            string integrationUrl = GetDownloadZipUrl(hostUrl);
            string downloadedZipFilePath = Path.Combine(targetDir, BootstrapperSettings.SonarQubeIntegrationFilename);

            using (WebClient client = new WebClient())
            {
                try
                {
                    logger.LogMessage(Resources.INFO_Downloading, BootstrapperSettings.SonarQubeIntegrationFilename, integrationUrl, downloadedZipFilePath);
                    client.DownloadFile(integrationUrl, downloadedZipFilePath);
                }
                catch (WebException ex)
                {
                    if (Utilities.HandleHostUrlWebException(ex, hostUrl, logger))
                    {
                        return false;
                    }

                    throw;
                }

                ZipFile.ExtractToDirectory(downloadedZipFilePath, targetDir);
                return true;
            }
        }

        /// <summary>
        /// Verifies that the pre/post-processors are compatible with this version of the bootstrapper
        /// </summary>
        /// <remarks>Older C# plugins will not have the file contain the supported versions -
        /// in this case we fail because we are not backwards compatible with those versions</remarks>
        /// <param name="versionFilePath">path to the XML file containing the supported versions</param>
        /// <param name="bootstrapperVersion">current version</param>
        public bool CheckBootstrapperApiVersion(string versionFilePath, Version bootstrapperVersion)
        {
            if (string.IsNullOrWhiteSpace(versionFilePath))
            {
                throw new ArgumentNullException("versionFilePath");
            }
            if (bootstrapperVersion == null)
            {
                throw new ArgumentNullException("bootstrapperVersion");
            }

            // The Bootstrapper 1.0+ does not support versions of the C# plugin that don't have this file
            if (!File.Exists(versionFilePath))
            {
                return false;
            }

            BootstrapperSupportedVersions supportedVersions = BootstrapperSupportedVersions.Load(versionFilePath);

            foreach (string stringVersion in supportedVersions.Versions)
            {
                // parse to version to make sure they are in the right format and to ease with comparisons
                Version version = null;
                if (Version.TryParse(stringVersion, out version) && version.Equals(bootstrapperVersion))
                {
                    return true;
                }
            }

            return false;
        }


        public void InstallLoaderTargets(ILogger logger)
        {
            WarnOnGlobalTargetsFile(logger);
            InternalCopyTargetsFile(logger);
        }

        private static void InternalCopyTargetsFile(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            string sourceTargetsPath = Path.Combine(Path.GetDirectoryName(typeof(BuildAgentUpdater).Assembly.Location), LoaderTargetsName);
            Debug.Assert(File.Exists(sourceTargetsPath), 
                String.Format("Could not find the {0} file in the directory of the executing assembly", LoaderTargetsName));

            CopyIfDifferent(sourceTargetsPath, DestinationDirs, logger);
        }

        private static void CopyIfDifferent(string sourcePath, IEnumerable<string> destinationDirs, ILogger logger)
        {
            string sourceContent = GetReadOnlyFileContent(sourcePath);
            string fileName = Path.GetFileName(sourcePath);

            foreach (string destinationDir in destinationDirs)
            {
                string destinationPath = Path.Combine(destinationDir, fileName);

                if (!File.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationDir); // creates all the directories in the path if needed
                    File.Copy(sourcePath, destinationPath, overwrite: false);
                    logger.LogMessage(Resources.INFO_InstallTargets_Copy, fileName, destinationDir);
                }
                else
                {
                    string destinationContent = GetReadOnlyFileContent(destinationPath);

                    if (!String.Equals(sourceContent, destinationContent, StringComparison.Ordinal))
                    {
                        File.Copy(sourcePath, destinationPath, overwrite: true);
                        logger.LogMessage(Resources.INFO_InstallTargets_Overwrite, fileName, destinationDir);
                    }
                    else
                    {
                        logger.LogMessage(Resources.INFO_InstallTargets_UpToDate, fileName, destinationDir);
                    }
                }
            }
        }

        private static string GetReadOnlyFileContent(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private static void WarnOnGlobalTargetsFile(ILogger logger)
        {
            // Giving a warning is best effort - if the user has installed MSBUILD in a non-standard location then this will not work
            string[] globalMsbuildTargetsDirs = new string[]
            {
                Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MSBuild", "14.0", "Microsoft.Common.Targets", "ImportBefore"),
                Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MSBuild", "12.0", "Microsoft.Common.Targets", "ImportBefore"),
                Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MSBuild", "4.0", "Microsoft.Common.Targets", "ImportBefore"),
            };

            foreach (string globalMsbuildTargetDir in globalMsbuildTargetsDirs)
            {
                string existingFile = Path.Combine(globalMsbuildTargetDir, LoaderTargetsName);

                if (File.Exists(existingFile))
                {
                    logger.LogWarning(Resources.WARN_ExistingGlobalTargets, LoaderTargetsName, globalMsbuildTargetDir);
                }
            }
        }

        private static string GetDownloadZipUrl(string url)
        {
            string downloadZipUrl = url;
            if (downloadZipUrl.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                downloadZipUrl = downloadZipUrl.Substring(0, downloadZipUrl.Length - 1);
            }

            downloadZipUrl = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}{1}", downloadZipUrl, BootstrapperSettings.IntegrationUrlSuffix);

            return downloadZipUrl;
        }
    }
}