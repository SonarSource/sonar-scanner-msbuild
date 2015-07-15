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