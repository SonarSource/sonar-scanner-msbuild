//-----------------------------------------------------------------------
// <copyright file="ProjectInfoAssertions.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sonar.Common;
using System;

namespace TestUtilities
{
    public static class ProjectInfoAssertions
    {
        /// <summary>
        /// Checks that the project info contains the expected values
        /// </summary>
        public static void AssertExpectedValues(ProjectInfo expected, ProjectInfo actual)
        {
            AssertExpectedValues(expected.FullPath, expected.ProjectType, expected.ProjectGuid, expected.ProjectName, actual);
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
    }
}
