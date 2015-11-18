//-----------------------------------------------------------------------
// <copyright file="SonarProjectPropertiesValidator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SonarQube.Common;

namespace SonarRunner.Shim
{
    public static class SonarProjectPropertiesValidator
    {
        /// <summary>
        /// Verifies that no sonar-project.properties conflicting with the generated one exists within the project
        /// </summary>
        /// <param name="sonarRunnerCwd">Solution folder to check</param>
        /// <param name="projects">MSBuild projects to check, only valid ones will be verified</param>
        /// <param name="onValid">Called when validation succeeded</param>
        /// <param name="onInvalid">Called when validation fails, with the list of folders containing a sonar-project.properties file</param>
        public static void Validate(string sonarRunnerCwd, IDictionary<ProjectInfo, ProjectInfoValidity> projects, Action onValid, Action<IList<string>> onInvalid)
        {
            var folders = new List<string>();
            folders.Add(sonarRunnerCwd);
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
