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
using System.IO;
using SonarQube.Common;

namespace SonarQube.TeamBuild.PostProcessor
{
    /// <summary>
    /// Handles removing targets from well known locations
    /// </summary>
    public class TargetsUninstaller : ITargetsUninstaller
    {
        private readonly IMsBuildPathsSettings msBuildPathsSettings = new MsBuildPathSettings();

        public void UninstallTargets(ILogger logger)
        {
            foreach (var directoryPath in msBuildPathsSettings.GetImportBeforePaths())
            {
                var destinationPath = Path.Combine(directoryPath, FileConstants.ImportBeforeTargetsName);

                if (!File.Exists(destinationPath))
                {
                    logger.LogDebug(Resources.MSG_UninstallTargets_NotExists, FileConstants.ImportBeforeTargetsName, directoryPath);
                    continue;
                }

                try
                {
                    File.Delete(destinationPath);
                }
                catch (IOException)
                {
                    logger.LogDebug(Resources.MSG_UninstallTargets_CouldNotDelete, FileConstants.ImportBeforeTargetsName, directoryPath);
                }
                catch (UnauthorizedAccessException)
                {
                    logger.LogDebug(Resources.MSG_UninstallTargets_CouldNotDelete, FileConstants.ImportBeforeTargetsName, directoryPath);
                }
            }
        }
    }
}
