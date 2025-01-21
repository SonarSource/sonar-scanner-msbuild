/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PostProcessor;

/// <summary>
/// Handles removing targets from .sonarqube/bin directory
/// </summary>
public class TargetsUninstaller : ITargetsUninstaller
{
    private readonly ILogger logger;

    public TargetsUninstaller(ILogger logger) =>
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public void UninstallTargets(string binDirectory)
    {
        var path = Path.Combine(binDirectory, "targets", FileConstants.IntegrationTargetsName);
        if (File.Exists(path))
        {
            try
            {
                logger.LogDebug(Resources.MSG_UninstallTargets_Uninstalling, path);
                File.Delete(path);
            }
            catch (IOException)
            {
                logger.LogDebug(Resources.MSG_UninstallTargets_CouldNotDelete, path);
            }
            catch (UnauthorizedAccessException)
            {
                logger.LogDebug(Resources.MSG_UninstallTargets_CouldNotDelete, path);
            }
        }
        else
        {
            logger.LogDebug(Resources.MSG_UninstallTargets_NotExists, path);
        }
    }
}
