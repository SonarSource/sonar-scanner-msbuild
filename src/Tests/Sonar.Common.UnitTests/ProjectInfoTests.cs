//-----------------------------------------------------------------------
// <copyright file="ProjectInfoTests.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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

            // 1. Save
            Assert.IsFalse(File.Exists(fileName), "Test error: file should not exist at the start of the test. File: {0}", fileName);
            originalProjectInfo.Save(fileName);
            Assert.IsTrue(File.Exists(fileName), "Failed to create the output file. File: {0}", fileName);
            this.TestContext.AddResultFile(fileName);

            // 2. Re-load
            ProjectInfo reloadedProjectInfo = ProjectInfo.Load(fileName);
            Assert.IsNotNull(reloadedProjectInfo, "Reloaded project info should not be null");

            ProjectInfoAssertions.AssertExpectedValues(
                "c:\\fullPath\\project.proj",
                ProjectType.Product,
                projectGuid,
                "MyProject",
                reloadedProjectInfo);
        }

        #endregion
    }
}
