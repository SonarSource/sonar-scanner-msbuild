//-----------------------------------------------------------------------
// <copyright file="TargetsInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Handlers copying targets to well known locations and warning the user about existing targets file
    /// </summary>
    public class TargetsInstaller : ITargetsInstaller
    {
        /// <summary>
        /// Controls the default value for installing the loader targets.
        /// </summary>
        /// <remarks> Can be overridden from the command line</remarks>
        public const bool DefaultInstallSetting = true;

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

        public void InstallLoaderTargets(ILogger logger)
        {
            WarnOnGlobalTargetsFile(logger);
            InternalCopyTargetsFile(logger);
        }

        #region Private Methods

        private static void InternalCopyTargetsFile(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            logger.LogInfo(Resources.MSG_UpdatingMSBuildTargets);

            string sourceTargetsPath = Path.Combine(Path.GetDirectoryName(typeof(TeamBuildPreProcessor).Assembly.Location), "Targets", LoaderTargetsName);
            Debug.Assert(File.Exists(sourceTargetsPath),
                String.Format("Could not find the loader .targets file at {0}", sourceTargetsPath));

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
                    logger.LogDebug(Resources.MSG_InstallTargets_Copy, fileName, destinationDir);
                }
                else
                {
                    string destinationContent = GetReadOnlyFileContent(destinationPath);

                    if (!String.Equals(sourceContent, destinationContent, StringComparison.Ordinal))
                    {
                        File.Copy(sourcePath, destinationPath, overwrite: true);
                        logger.LogDebug(Resources.MSG_InstallTargets_Overwrite, fileName, destinationDir);
                    }
                    else
                    {
                        logger.LogDebug(Resources.MSG_InstallTargets_UpToDate, fileName, destinationDir);
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

        #endregion Private Methods
    }
}