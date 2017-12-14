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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;

namespace TestUtilities
{
    public static class ProjectInfoAssertions
    {
        #region Public methods

        /// <summary>
        /// Returns the list of project info objects beneath the specified root output folder
        /// </summary>
        /// <param name="rootOutputFolder">The root Sonary analysis ouptut folder. Project info files will be searched for in
        /// immediate sub-directories of this folder only.</param>
        public static IList<ProjectInfo> GetProjectInfosFromOutputFolder(string rootOutputFolder)
        {
            var items = new List<ProjectInfo>();

            foreach (var directory in Directory.EnumerateDirectories(rootOutputFolder, "*.*", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.Combine(directory, FileConstants.ProjectInfoFileName);
                if (File.Exists(fileName))
                {
                    var item = ProjectInfo.Load(fileName);
                    items.Add(item);
                }
            }
            return items;
        }

        #endregion Public methods

        #region Assertions

        /// <summary>
        /// Checks that the project info contains the expected values
        /// </summary>
        public static void AssertExpectedValues(ProjectInfo expected, ProjectInfo actual)
        {
            Assert.IsNotNull(actual, "Supplied ProjectInfo should not be null");

            Assert.AreEqual(expected.FullPath, actual.FullPath, "Unexpected FullPath");
            Assert.AreEqual(expected.ProjectLanguage, actual.ProjectLanguage, "Unexpected ProjectLanguage");
            Assert.AreEqual(expected.ProjectType, actual.ProjectType, "Unexpected ProjectType");
            Assert.AreEqual(expected.ProjectGuid, actual.ProjectGuid, "Unexpected ProjectGuid");
            Assert.AreEqual(expected.ProjectName, actual.ProjectName, "Unexpected ProjectName");
            Assert.AreEqual(expected.IsExcluded, actual.IsExcluded, "Unexpected IsExcluded");

            CompareAnalysisResults(expected, actual);
        }

        /// <summary>
        /// Checks that not project info files exist under the output folder
        /// </summary>
        /// <param name="rootOutputFolder">The root SonarQube analysis output folder i.e. the folder that contains the per-project folders</param>
        public static void AssertNoProjectInfoFilesExists(string rootOutputFolder)
        {
            var items = GetProjectInfosFromOutputFolder(rootOutputFolder);
            Assert.AreEqual(0, items.Count, "Not expecting any project info files to exist");
        }

        /// <summary>
        /// Checks that a project info file exists for the specified project
        /// </summary>
        /// <param name="rootOutputFolder">The root SonarQube analysis output folder i.e. the folder that contains the per-project folders</param>
        /// <param name="fullProjectFileName">The full path and file name of the project file to which the project info file relates</param>
        public static ProjectInfo AssertProjectInfoExists(string rootOutputFolder, string fullProjectFileName)
        {
            var items = GetProjectInfosFromOutputFolder(rootOutputFolder);
            Assert.AreNotEqual(0, items.Count, "Failed to locate any project info files under the specified root folder");

            var match = GetProjectInfosFromOutputFolder(rootOutputFolder).FirstOrDefault(pi => fullProjectFileName.Equals(pi.FullPath, StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(match, "Failed to retrieve a project info file for the specified project: {0}", fullProjectFileName);
            return match;
        }

        public static void AssertNoAnalysisResultsExist(ProjectInfo projectInfo)
        {
            Assert.IsTrue(projectInfo.AnalysisResults == null || projectInfo.AnalysisResults.Count == 0,
                "Not expecting analysis results to exist. Count: {0}", projectInfo.AnalysisResults.Count);
        }

        public static void AssertAnalysisResultDoesNotExists(ProjectInfo projectInfo, string resultId)
        {
            Assert.IsNotNull(projectInfo.AnalysisResults, "AnalysisResults should not be null");
            var found = SonarQube.Common.ProjectInfoExtensions.TryGetAnalyzerResult(projectInfo, resultId, out AnalysisResult result);
            Assert.IsFalse(found, "Not expecting to find an analysis result for id. Id: {0}", resultId);
        }

        public static AnalysisResult AssertAnalysisResultExists(ProjectInfo projectInfo, string resultId)
        {
            Assert.IsNotNull(projectInfo.AnalysisResults, "AnalysisResults should not be null");
            var found = SonarQube.Common.ProjectInfoExtensions.TryGetAnalyzerResult(projectInfo, resultId, out AnalysisResult result);
            Assert.IsTrue(found, "Failed to find an analysis result with the expected id. Id: {0}", resultId);
            Assert.IsNotNull(result, "Returned analysis result should not be null. Id: {0}", resultId);
            return result;
        }

        public static AnalysisResult AssertAnalysisResultExists(ProjectInfo projectInfo, string resultId, string expectedLocation)
        {
            var result = AssertAnalysisResultExists(projectInfo, resultId);
            Assert.AreEqual(expectedLocation, result.Location,
                "Analysis result exists but does not have the expected location. Id: {0}, expected: {1}, actual: {2}",
                    resultId, expectedLocation, result.Location);
            return result;
        }

        #endregion Assertions

        #region Private methods

        private static void CompareAnalysisResults(ProjectInfo expected, ProjectInfo actual)
        {
            // We're assuming the actual analysis results have been reloaded by the serializer
            // so they should never be null
            Assert.IsNotNull(actual.AnalysisResults, "actual AnalysisResults should not be null");

            if (expected.AnalysisResults == null || !expected.AnalysisResults.Any())
            {
                Assert.AreEqual(0, actual.AnalysisResults.Count, "actual AnalysisResults should be empty");
            }
            else
            {
                foreach(var expectedResult in expected.AnalysisResults)
                {
                    AssertAnalysisResultExists(actual, expectedResult.Id, expectedResult.Location);
                }
                Assert.AreEqual(expected.AnalysisResults.Count, actual.AnalysisResults.Count, "Unexpected additional analysis results found");
            }
        }

        #endregion Private methods
    }
}
