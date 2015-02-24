//-----------------------------------------------------------------------
// <copyright file="ProjectInfoAssertions.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sonar.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestUtilities
{
    public static class ProjectInfoAssertions
    {
        #region Public methods

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
