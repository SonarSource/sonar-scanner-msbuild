/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.Shim;

public class SonarProjectPropertiesValidator : ISonarProjectPropertiesValidator
{
    /// <summary>
    /// Verifies that no sonar-project.properties conflicting with the generated one exists within the project
    /// </summary>
    /// <param name="sonarScannerCwd">Solution folder to check</param>
    /// <param name="projects">MSBuild projects to check, only valid ones will be verified</param>
    public bool AreExistingSonarPropertiesFilesPresent(string sonarScannerCwd, ICollection<ProjectData> projects, out IEnumerable<string> invalidFolders)
    {
        invalidFolders = projects
            .Where(p => p.Status == ProjectInfoValidity.Valid)
            .Select(p => p.Project.GetDirectory().FullName)
            .Union(new[] { sonarScannerCwd })
            .Where(SonarProjectPropertiesExists)
            .ToList();

        return invalidFolders.Any();
    }

    private bool SonarProjectPropertiesExists(string folder)
    {
        return File.Exists(Path.Combine(folder, "sonar-project.properties"));
    }
}
