/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SonarQube.Common;

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

        public void InstallLoaderTargets(ILogger logger, string workDirectory)
        {
            WarnOnGlobalTargetsFile(logger);
            InternalCopyTargetsFile(logger);
            InternalCopyTargetFileToProject(logger, workDirectory);
        }

        #region Private Methods

        private static void InternalCopyTargetFileToProject(ILogger logger, string workDirectory)
        {
            var sourceTargetsPath = Path.Combine(Path.GetDirectoryName(typeof(TeamBuildPreProcessor).Assembly.Location), "Targets",
                FileConstants.IntegrationTargetsName);
            var dstTargetsPath = new string[] { Path.Combine(workDirectory, "bin", "targets") };

            // For old bootstrappers, the payload and targets are already installed at the destination
            if(string.Equals(sourceTargetsPath, dstTargetsPath))
            {
                return;
            }

            Debug.Assert(File.Exists(sourceTargetsPath),
    string.Format(System.Globalization.CultureInfo.InvariantCulture, "Could not find the loader .targets file at {0}", sourceTargetsPath));

            CopyIfDifferent(sourceTargetsPath, dstTargetsPath, logger);
        }

        private static void InternalCopyTargetsFile(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            logger.LogInfo(Resources.MSG_UpdatingMSBuildTargets);

            var sourceTargetsPath = Path.Combine(Path.GetDirectoryName(typeof(TeamBuildPreProcessor).Assembly.Location), "Targets",
                FileConstants.ImportBeforeTargetsName);
            Debug.Assert(File.Exists(sourceTargetsPath),
                string.Format(System.Globalization.CultureInfo.InvariantCulture, "Could not find the loader .targets file at {0}",
                sourceTargetsPath));

            CopyIfDifferent(sourceTargetsPath, FileConstants.ImportBeforeDestinationDirectoryPaths, logger);
        }

        private static void CopyIfDifferent(string sourcePath, IEnumerable<string> destinationDirs, ILogger logger)
        {
            var sourceContent = GetReadOnlyFileContent(sourcePath);
            var fileName = Path.GetFileName(sourcePath);

            foreach (var destinationDir in destinationDirs)
            {
                var destinationPath = Path.Combine(destinationDir, fileName);

                if (!File.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationDir); // creates all the directories in the path if needed
                    File.Copy(sourcePath, destinationPath, overwrite: false);
                    logger.LogDebug(Resources.MSG_InstallTargets_Copy, fileName, destinationDir);
                }
                else
                {
                    var destinationContent = GetReadOnlyFileContent(destinationPath);

                    if (!string.Equals(sourceContent, destinationContent, StringComparison.Ordinal))
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
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var sr = new StreamReader(fs))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private static void WarnOnGlobalTargetsFile(ILogger logger)
        {
            // Giving a warning is best effort - if the user has installed MSBUILD in a non-standard location then this will not work
            var globalMsbuildTargetsDirs = new string[]
            {
                Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MSBuild", "14.0",
                    "Microsoft.Common.Targets", "ImportBefore"),
                Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MSBuild", "12.0",
                    "Microsoft.Common.Targets", "ImportBefore")
            };

            foreach (var globalMsbuildTargetDir in globalMsbuildTargetsDirs)
            {
                var existingFile = Path.Combine(globalMsbuildTargetDir, FileConstants.ImportBeforeTargetsName);

                if (File.Exists(existingFile))
                {
                    logger.LogWarning(Resources.WARN_ExistingGlobalTargets, FileConstants.ImportBeforeTargetsName, globalMsbuildTargetDir);
                }
            }
        }

        #endregion Private Methods
    }
}
