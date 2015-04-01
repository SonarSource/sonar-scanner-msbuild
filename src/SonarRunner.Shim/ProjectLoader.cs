//-----------------------------------------------------------------------
// <copyright file="ProjectLoader.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SonarRunner.Shim
{
    public static class ProjectLoader
    {
        public static List<ProjectInfo> LoadFrom(string dumpFolderPath)
        {
            List<ProjectInfo> result = new List<ProjectInfo>();

            foreach (string projectFolderPath in Directory.GetDirectories(dumpFolderPath))
            {
                var projectInfo = TryGetProjectInfo(projectFolderPath);
                if (projectInfo != null)
                {
                    result.Add(projectInfo);
                }
            }

            return result;
        }

        private static ProjectInfo TryGetProjectInfo(string projectFolderPath)
        {
            ProjectInfo projectInfo = null;

            string projectInfoPath = Path.Combine(projectFolderPath, FileConstants.ProjectInfoFileName);

            if (File.Exists(projectInfoPath))
            {
                projectInfo = ProjectInfo.Load(projectInfoPath);
            }

            return projectInfo;
        }

        /// <summary>
        /// Attempts to return the file location for the specified type of analysis result.
        /// Returns null if the there is not a result for the specified type, or if the
        /// file does not exist.
        /// </summary>
        private static string TryGetAnalysisFileLocation(ProjectInfo projectInfo, AnalysisType analysisType)
        {
            string location = null;

            AnalysisResult result = null;
            if (projectInfo.TryGetAnalyzerResult(analysisType, out result))
            {
                if (File.Exists(result.Location))
                {
                    location = result.Location;
                }
            }
            return location;
        }
    }
}
