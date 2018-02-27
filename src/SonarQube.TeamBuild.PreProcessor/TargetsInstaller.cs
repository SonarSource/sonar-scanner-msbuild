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
using System.Globalization;
using System.IO;
using System.Linq;
using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Handlers copying targets to well known locations and warning the user about existing targets file
    /// </summary>
    public class TargetsInstaller : ITargetsInstaller
    {
        private static readonly string AssemblyLocation = Path.GetDirectoryName(typeof(TeamBuildPreProcessor).Assembly.Location);

        /// <summary>
        /// Controls the default value for installing the loader targets.
        /// </summary>
        /// <remarks> Can be overridden from the command line</remarks>
        public const bool DefaultInstallSetting = true;

        private readonly ILogger logger;
        private readonly IMsBuildPathsSettings msBuildPathsSettings;
        private readonly IFileWrapper fileWrapper;
        private readonly IDirectoryWrapper directoryWrapper;

        public TargetsInstaller(ILogger logger)
            : this(logger, new MsBuildPathSettings(), new FileWrapper(), new DirectoryWrapper())
        {
        }

        public /*for testing*/ TargetsInstaller(ILogger logger, IMsBuildPathsSettings msBuildPathsSettings,
            IFileWrapper fileWrapper, IDirectoryWrapper directoryWrapper)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.msBuildPathsSettings = msBuildPathsSettings ?? throw new ArgumentNullException(nameof(msBuildPathsSettings));
            this.fileWrapper = fileWrapper ?? throw new ArgumentNullException(nameof(fileWrapper));
            this.directoryWrapper = directoryWrapper ?? throw new ArgumentNullException(nameof(directoryWrapper));
        }

        public void InstallLoaderTargets(string workDirectory)
        {
            WarnOnGlobalTargetsFile();
            WarnOnAppDataInsideSystem32();
            InternalCopyTargetsFile();
            InternalCopyTargetFileToProject(workDirectory);
        }

        private void InternalCopyTargetFileToProject(string workDirectory) =>
            CopyIfDifferent(
                GetTargetSourcePath(FileConstants.IntegrationTargetsName),
                new string[] { Path.Combine(workDirectory, "bin", "targets") });

        private void InternalCopyTargetsFile()
        {
            logger.LogInfo(Resources.MSG_UpdatingMSBuildTargets);

            CopyIfDifferent(
                GetTargetSourcePath(FileConstants.ImportBeforeTargetsName),
                msBuildPathsSettings.GetImportBeforePaths());
        }

        private string GetTargetSourcePath(string targetFileName) =>
            Path.Combine(AssemblyLocation, "Targets", targetFileName);

        private void CopyIfDifferent(string sourcePath, IEnumerable<string> destinationDirs)
        {
            Debug.Assert(fileWrapper.Exists(sourcePath),
                string.Format(CultureInfo.InvariantCulture, "Could not find the loader .targets file at {0}", sourcePath));

            var sourceContent = fileWrapper.ReadAllText(sourcePath);
            var fileName = Path.GetFileName(sourcePath);

            foreach (var destinationDir in destinationDirs)
            {
                var destinationPath = Path.Combine(destinationDir, fileName);

                if (!fileWrapper.Exists(destinationPath))
                {
                    directoryWrapper.CreateDirectory(destinationDir); // creates all the directories in the path if needed
                    fileWrapper.Copy(sourcePath, destinationPath, overwrite: false);
                    logger.LogDebug(Resources.MSG_InstallTargets_Copy, fileName, destinationDir);
                }
                else
                {
                    var destinationContent = fileWrapper.ReadAllText(destinationPath);

                    if (!string.Equals(sourceContent, destinationContent, StringComparison.Ordinal))
                    {
                        fileWrapper.Copy(sourcePath, destinationPath, overwrite: true);
                        logger.LogDebug(Resources.MSG_InstallTargets_Overwrite, fileName, destinationDir);
                    }
                    else
                    {
                        logger.LogDebug(Resources.MSG_InstallTargets_UpToDate, fileName, destinationDir);
                    }
                }
            }
        }

        private void WarnOnAppDataInsideSystem32()
        {
            // When the scanner is running under Local System or Network Service the AppData folder is under
            // C:\Windows\System32\... and is ignored by MSBuild and dotnet.
            if (msBuildPathsSettings.GetImportBeforePaths().Any(IsInsideSystem32))
            {
                logger.LogWarning(Resources.MSG_InstallTargetsLocalSystem);
            }

            bool IsInsideSystem32(string path) =>
                path.IndexOf("windows\\system32", StringComparison.InvariantCultureIgnoreCase) >= 0;
        }

        private void WarnOnGlobalTargetsFile()
        {
            // Giving a warning is best effort - if the user has installed MSBUILD in a non-standard location then this will not work
            msBuildPathsSettings.GetGlobalTargetsPaths()
                .Where(ImportBeforeTargetExists)
                .ToList()
                .ForEach(LogWarning);

            bool ImportBeforeTargetExists(string globalTargetPath) =>
                fileWrapper.Exists(Path.Combine(globalTargetPath, FileConstants.ImportBeforeTargetsName));

            void LogWarning(string globalTargetPath) =>
                logger.LogWarning(Resources.WARN_ExistingGlobalTargets, FileConstants.ImportBeforeTargetsName, globalTargetPath);
        }
    }
}
