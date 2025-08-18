﻿/*
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

namespace SonarScanner.MSBuild.Shim;

public class SonarProjectPropertiesValidator
{
    /// <summary>
    /// Verifies that no sonar-project.properties conflicting with the generated one exists within the project.
    /// </summary>
    /// <param name="sonarScannerCwd">Solution folder to check.</param>
    /// <param name="projects">MSBuild projects to check, only valid ones will be verified.</param>
    public virtual bool AreExistingSonarPropertiesFilesPresent(string sonarScannerCwd, ICollection<ProjectData> projects, out IEnumerable<string> invalidFolders)
    {
        invalidFolders = projects
            .Where(x => x.Status == ProjectInfoValidity.Valid)
            .Select(x => x.Project.GetDirectory().FullName)
            .Union([sonarScannerCwd])
            .Where(SonarProjectPropertiesExists)
            .ToList();

        return invalidFolders.Any();
    }

    private static bool SonarProjectPropertiesExists(string folder) =>
        File.Exists(Path.Combine(folder, "sonar-project.properties"));
}
