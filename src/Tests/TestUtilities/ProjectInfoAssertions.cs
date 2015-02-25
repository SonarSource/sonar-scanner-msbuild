//-----------------------------------------------------------------------
// <copyright file="ProjectInfoAssertions.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sonar.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            List<ProjectInfo> items = new List<ProjectInfo>();

            foreach (string directory in Directory.EnumerateDirectories(rootOutputFolder, "*.*", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.Combine(directory, FileConstants.ProjectInfoFileName);
                if (File.Exists(fileName))
                {
                    ProjectInfo item = ProjectInfo.Load(fileName);
                    items.Add(item);
                }
            }
            return items;
        }

        #endregion

        #region Assertions

        /// <summary>
        /// Checks that the project info contains the expected values
        /// </summary>
        public static void AssertExpectedValues(ProjectInfo expected, ProjectInfo actual)
        {
            AssertExpectedValues(expected.FullPath, expected.ProjectType, expected.ProjectGuid, expected.ProjectName, actual);

            CompareAnalysisResults(expected.AnalysisResults, actual.AnalysisResults);
        }

        /// <summary>
        /// Checks that the project info contains the expected values
        /// </summary>
        public static void AssertExpectedValues(
            string expectedFullProjectPath,
            ProjectType expectedProjectType,
            Guid expectedProjectGuid,
            string expectedProjectName,
            ProjectInfo actualProjectInfo)
        {
            Assert.IsNotNull(actualProjectInfo, "Supplied ProjectInfo should not be null");

            Assert.AreEqual(expectedFullProjectPath, actualProjectInfo.FullPath, "Unexpected FullPath");
            Assert.AreEqual(expectedProjectType, actualProjectInfo.ProjectType, "Unexpected ProjectType");
            Assert.AreEqual(expectedProjectGuid, actualProjectInfo.ProjectGuid, "Unexpected ProjectGuid");
            Assert.AreEqual(expectedProjectName, actualProjectInfo.ProjectName, "Unexpected ProjectName");

        }

        /// <summary>
        /// Checks that a project info file exists for the specified project
        /// </summary>
        /// <param name="rootOutputFolder">The root Sonar analysis output folder i.e. the folder that contains the per-project folders</param>
        /// <param name="fullProjectFileName">The full path and file name of the project file to which the project info file relates</param>
        public static ProjectInfo AssertProjectInfoExists(string rootOutputFolder, string fullProjectFileName)
        {
            IList<ProjectInfo> items = GetProjectInfosFromOutputFolder(rootOutputFolder);
            Assert.AreNotEqual(0, items.Count, "Failed to locate any project info files under the specified root folder");

            ProjectInfo match = GetProjectInfosFromOutputFolder(rootOutputFolder).FirstOrDefault(pi => fullProjectFileName.Equals(pi.FullPath, StringComparison.InvariantCultureIgnoreCase));
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
            AnalysisResult result;
            bool found = Sonar.Common.ProjectInfoExtensions.TryGetAnalyzerResult(projectInfo, resultId, out result);
            Assert.IsFalse(found, "Not expecting to find an analysis result for id. Id: {0}", resultId);
        }

        public static AnalysisResult AssertAnalysisResultExists(ProjectInfo projectInfo, string resultId)
        {
            Assert.IsNotNull(projectInfo.AnalysisResults, "AnalysisResults should not be null");
            AnalysisResult result;
            bool found = Sonar.Common.ProjectInfoExtensions.TryGetAnalyzerResult(projectInfo, resultId, out result);
            Assert.IsTrue(found, "Failed to find an analysis result with the expected id. Id: {0}", resultId);
            Assert.IsNotNull(result, "Returned analysis result should not be null. Id: {0}", resultId);
            return result;
        }

        public static AnalysisResult AssertAnalysisResultExists(ProjectInfo projectInfo, string resultId, string expectedLocation)
        {
            AnalysisResult result = AssertAnalysisResultExists(projectInfo, resultId);
            Assert.AreEqual(expectedLocation, result.Location,
                "Analysis result exists but does not have the expected location. Id: {0}, expected: {1}, actual: {2}",
                    resultId, expectedLocation, result.Location);
            return result;
        }

        #endregion

        #region Private methods

        private static void CompareAnalysisResults(List<AnalysisResult> expected, List<AnalysisResult> actual)
        {
            // We're assuming the actual analysis results have been reloaded by the serializer
            // so they should never be null
            Assert.IsNotNull(actual, "actual AnalysisResults should not be null");

            if (expected == null || !expected.Any())
            {
                Assert.AreEqual(0, actual.Count, "actual AnalysisResults should be empty");
            }
            else
            {
                for (int index = 0; index < expected.Count; index++)
                {
                    AnalysisResult expectedResult = expected[index];
                    AnalysisResult actualResult = actual[index];

                    Assert.AreEqual(expectedResult.Id, actualResult.Id, "Analysis result ids do not match. Index: {0}", index);
                    Assert.AreEqual(expectedResult.Location, actualResult.Location, "Analysis result locations do not match. Index: {0}", index);
                }
            }
        }
        #endregion

    }
}
