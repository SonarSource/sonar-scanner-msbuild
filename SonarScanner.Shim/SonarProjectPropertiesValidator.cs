/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
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
using System.Linq;
using System.Collections.Generic;
using SonarQube.Common;

namespace SonarScanner.Shim
{
    public static class SonarProjectPropertiesValidator
    {
        /// <summary>
        /// Verifies that no sonar-project.properties conflicting with the generated one exists within the project
        /// </summary>
        /// <param name="sonarScannerCwd">Solution folder to check</param>
        /// <param name="projects">MSBuild projects to check, only valid ones will be verified</param>
        /// <param name="onValid">Called when validation succeeded</param>
        /// <param name="onInvalid">Called when validation fails, with the list of folders containing a sonar-project.properties file</param>
        public static void Validate(string sonarScannerCwd, IDictionary<ProjectInfo, ProjectInfoValidity> projects, Action onValid, Action<IList<string>> onInvalid)
        {
            var folders = new List<string>
            {
                sonarScannerCwd
            };
            folders.AddRange(projects.Where(p => p.Value == ProjectInfoValidity.Valid).Select(p => Path.GetDirectoryName(p.Key.FullPath)));

            var invalidFolders = folders.Where(f => !Validate(f)).ToList();

            if (!invalidFolders.Any())
            {
                onValid();
            }
            else
            {
                onInvalid(invalidFolders);
            }
        }

        private static bool Validate(string folder)
        {
            return !File.Exists(Path.Combine(folder, "sonar-project.properties"));
        }
    }
}
