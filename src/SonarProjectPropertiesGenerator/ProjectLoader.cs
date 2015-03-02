//-----------------------------------------------------------------------
// <copyright file="ProjectLoader.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SonarProjectPropertiesGenerator
{
    public static class ProjectLoader
    {
        public static List<Project> LoadFrom(string dumpFolderPath)
        {
            List<Project> result = new List<Project>();

            foreach (string projectFolderPath in Directory.GetDirectories(dumpFolderPath))
            {
                var projectInfo = TryGetProjectInfo(projectFolderPath);
                if (projectInfo == null)
                {
                    continue;
                }

                string compileListPath = TryGetAnalysisFileLocation(projectInfo, AnalysisType.ManagedCompilerInputs);
                if (compileListPath == null)
                {
                    continue;
                }

                bool isTest = projectInfo.ProjectType == ProjectType.Test;
                
                List<String> files = File.ReadAllLines(compileListPath, Encoding.UTF8).ToList();

                result.Add(new Project(projectInfo.ProjectName, projectInfo.ProjectGuid, projectInfo.FullPath, isTest, files));
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
