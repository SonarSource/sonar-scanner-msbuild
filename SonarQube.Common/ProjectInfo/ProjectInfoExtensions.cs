//-----------------------------------------------------------------------
// <copyright file="ProjectInfoExtensions.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SonarQube.Common
{
    /// <summary>
    /// Extension methods for <see cref="ProjectInfo"/>
    /// </summary>
    public static class ProjectInfoExtensions
    {
        #region Public methods

        /// <summary>
        /// Attempts to find and return the analyzer result with the specified id
        /// </summary>
        /// <returns>True if the analyzer result was found, otherwise false</returns>
        public static bool TryGetAnalyzerResult(this ProjectInfo projectInfo, AnalysisType analyzerType, out AnalysisResult result)
        {
            return TryGetAnalyzerResult(projectInfo, analyzerType.ToString(), out result);
        }

        /// <summary>
        /// Attempts to find and return the analyzer result with the specified id
        /// </summary>
        /// <returns>True if the analyzer result was found, otherwise false</returns>
        public static bool TryGetAnalyzerResult(this ProjectInfo projectInfo, string id, out AnalysisResult result)
        {
            if (projectInfo == null)
            {
                throw new ArgumentNullException("projectInfo");
            }

            result = null;

            if (projectInfo.AnalysisResults != null)
            {
                result = projectInfo.AnalysisResults.FirstOrDefault(ar => AnalysisResult.ResultKeyComparer.Equals(id, ar.Id));
            }
            return result != null;
        }

        /// <summary>
        /// Attempts to find and return the analysis setting with the specified id
        /// </summary>
        /// <returns>True if the setting was found, otherwise false</returns>
        public static bool TryGetAnalysisSetting(this ProjectInfo projectInfo, string id, out ConfigSetting result)
        {
            if (projectInfo == null)
            {
                throw new ArgumentNullException("projectInfo");
            }

            result = null;

            if (projectInfo.AnalysisSettings != null)
            {
                result = projectInfo.AnalysisSettings.FirstOrDefault(ar => ConfigSetting.SettingKeyComparer.Equals(id, ar.Id));
            }
            return result != null;
        }

        /// <summary>
        /// Adds an analysis result of the specified type
        /// </summary>
        /// <remarks>The method does not check whether an analysis result with the same id already exists i.e. duplicate results are allowed</remarks>
        public static void AddAnalyzerResult(this ProjectInfo projectInfo, AnalysisType analyzerType, string location)
        {
            AddAnalyzerResult(projectInfo, analyzerType.ToString(), location);
        }

        /// <summary>
        /// Adds an analysis result of the specified kind
        /// </summary>
        /// <remarks>The method does not check whether an analysis result with the same id already exists i.e. duplicate results are allowed</remarks>
        public static void AddAnalyzerResult(this ProjectInfo projectInfo, string id, string location)
        {
            if (projectInfo == null)
            {
                throw new ArgumentNullException("projectInfo");
            }
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException("id");
            }
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentNullException("location");
            }
            

            if (projectInfo.AnalysisResults == null)
            {
                projectInfo.AnalysisResults = new System.Collections.Generic.List<AnalysisResult>();
            }

            AnalysisResult result = new AnalysisResult() { Id = id, Location = location };
            projectInfo.AnalysisResults.Add(result);
        }

        /// <summary>
        /// Returns the full path of the directory containing the project file
        /// </summary>
        public static string GetProjectDirectory(this ProjectInfo projectInfo)
        {
            if (projectInfo == null)
            {
                throw new ArgumentNullException("projectInfo");
            }

            string dir = null;
            if (projectInfo.FullPath != null)
            {
                dir = Path.GetDirectoryName(Path.GetFullPath(projectInfo.FullPath));
            }
            return dir;
        }

        /// <summary>
        /// Returns the ProjectGuid formatted as a string
        /// </summary>
        public static string GetProjectGuidAsString(this ProjectInfo projectInfo)
        {
            if (projectInfo == null)
            {
                throw new ArgumentNullException("projectInfo");
            }

            return projectInfo.ProjectGuid.ToString("D", CultureInfo.InvariantCulture).ToUpperInvariant();
        }

        /// <summary>
        /// Attempts to return the file location for the specified type of analysis result.
        /// Returns null if there is not a result for the specified type, or if the
        /// file does not exist.
        /// </summary>
        public static string TryGetAnalysisFileLocation(this ProjectInfo projectInfo, AnalysisType analysisType)
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

        #endregion

        #region Private methods

        /// <summary>
        /// Returns the list of files to be analyzed. If there are no files to be analyzed
        /// then an empty list will be returned.
        /// </summary>
        public static IList<string> GetAllAnalysisFiles(this ProjectInfo projectInfo)
        {
            List<String> files = new List<string>();
            var compiledFilesPath = projectInfo.TryGetAnalysisFileLocation(AnalysisType.FilesToAnalyze);
            if (compiledFilesPath != null)
            {
                files.AddRange(File.ReadAllLines(compiledFilesPath));
            }
            return files;
        }

        #endregion

    }
}
