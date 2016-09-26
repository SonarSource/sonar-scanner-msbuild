//-----------------------------------------------------------------------
// <copyright file="TargetsInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.IO;

namespace SonarQube.TeamBuild.PostProcessor
{
    /// <summary>
    /// Handles removing targets from well known locations
    /// </summary>
    public class TargetsUninstaller : ITargetsUninstaller
    {
        public void UninstallTargets(ILogger logger)
        {
            foreach (string directoryPath in FileConstants.ImportBeforeDestinationDirectoryPaths)
            {
                string destinationPath = Path.Combine(directoryPath, FileConstants.ImportBeforeTargetsName);

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