//-----------------------------------------------------------------------
// <copyright file="ProjectInfoTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using TestUtilities;

namespace Sonar.Common.UnitTests
{
    [TestClass]
    public class ProjectInfoTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void ProjectInfo_Serialization_InvalidFileName()
        {
            // 0. Setup
            ProjectInfo pi = new ProjectInfo();

            // 1a. Missing file name - save
            AssertException.Expects<ArgumentNullException>(() => pi.Save(null));
            AssertException.Expects<ArgumentNullException>(() => pi.Save(string.Empty));
            AssertException.Expects<ArgumentNullException>(() => pi.Save("\r\t "));

            // 1b. Missing file name - load
            AssertException.Expects<ArgumentNullException>(() => ProjectInfo.Load(null));
            AssertException.Expects<ArgumentNullException>(() => ProjectInfo.Load(string.Empty));
            AssertException.Expects<ArgumentNullException>(() => ProjectInfo.Load("\r\t "));
        }

        [TestMethod]
        [Description("Checks ProjectInfo can be serialized and deserialized")]
        public void ProjectInfo_Serialization_SaveAndReload()
        {
            // 0. Setup
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid projectGuid = Guid.NewGuid();

            ProjectInfo originalProjectInfo = new ProjectInfo();
            originalProjectInfo.FullPath = "c:\\fullPath\\project.proj";
            originalProjectInfo.ProjectType = ProjectType.Product;
            originalProjectInfo.ProjectGuid = projectGuid;
            originalProjectInfo.ProjectName = "MyProject";

            string fileName = Path.Combine(testFolder, "ProjectInfo1.xml");

            SaveAndReloadProjectInfo(originalProjectInfo, fileName);
        }

        [TestMethod]
        [Description("Checks analysis results can be serialized and deserialized")]
        public void ProjectInfo_Serialization_AnalysisResults()
        {

            // 0. Setup
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid projectGuid = Guid.NewGuid();

            ProjectInfo originalProjectInfo = new ProjectInfo();
            originalProjectInfo.ProjectGuid = projectGuid;

            // 1. Null list
            SaveAndReloadProjectInfo(originalProjectInfo, Path.Combine(testFolder, "ProjectInfo_AnalysisResults1.xml"));

            // 2. Empty list
            originalProjectInfo.AnalysisResults = new List<AnalysisResult>();
            SaveAndReloadProjectInfo(originalProjectInfo, Path.Combine(testFolder, "ProjectInfo_AnalysisResults2.xml"));

            // 3. Non-empty list
            originalProjectInfo.AnalysisResults.Add(new AnalysisResult() { Id = string.Empty, Location = string.Empty }); // empty item
            originalProjectInfo.AnalysisResults.Add(new AnalysisResult() { Id = "Id1", Location = "location1" });
            originalProjectInfo.AnalysisResults.Add(new AnalysisResult() { Id = "Id2", Location = "location2" });
            SaveAndReloadProjectInfo(originalProjectInfo, Path.Combine(testFolder, "ProjectInfo_AnalysisResults3.xml"));
        }

        #endregion

        #region Helper methods

        private ProjectInfo SaveAndReloadProjectInfo(ProjectInfo original, string outputFileName)
        {
            Assert.IsFalse(File.Exists(outputFileName), "Test error: file should not exist at the start of the test. File: {0}", outputFileName);
            original.Save(outputFileName);
            Assert.IsTrue(File.Exists(outputFileName), "Failed to create the output file. File: {0}", outputFileName);
            this.TestContext.AddResultFile(outputFileName);

            ProjectInfo reloadedProjectInfo = ProjectInfo.Load(outputFileName);
            Assert.IsNotNull(reloadedProjectInfo, "Reloaded project info should not be null");

            ProjectInfoAssertions.AssertExpectedValues(original, reloadedProjectInfo);
            return reloadedProjectInfo;
        }

        #endregion
    }
}
