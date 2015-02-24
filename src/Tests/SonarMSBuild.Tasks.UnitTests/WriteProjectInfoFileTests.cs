//-----------------------------------------------------------------------
// <copyright file="WriteProjectInfoFileTests.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sonar.Common;
using System;
using System.IO;
using TestUtilities;

namespace SonarMSBuild.Tasks.UnitTests
{
    [TestClass]
    public class WriteProjectInfoFileTests
    {
        public TestContext TestContext { get; set; }
        
        #region Tests

        [TestMethod]
        [Description("Tests that the project info file is created when the task is executed")]
        public void WriteProjectInfoFile_FileCreated()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid projectGuid = Guid.NewGuid();

            WriteProjectInfoFile task = new WriteProjectInfoFile();
            task.FullProjectPath = "c:\\fullPath\\project.proj";
            task.IsTest = true;
            task.OutputFolder = testFolder;
            task.ProjectGuid = projectGuid.ToString("B");
            task.ProjectName = "MyProject";

            string expectedOutputFile = Path.Combine(testFolder, "ProjectInfo.xml");
            Assert.IsFalse(File.Exists(expectedOutputFile), "Test error: output file should not exist before the task is executed");

            // Act
            bool result = task.Execute();

            // Assert
            Assert.IsTrue(result, "Expecting the task execution to succeed");
            Assert.IsTrue(File.Exists(expectedOutputFile), "Expected output file was not created by the task. Expected: {0}", expectedOutputFile);

            ProjectInfo reloadedProjectInfo = ProjectInfo.Load(expectedOutputFile);

            ProjectInfoAssertions.AssertExpectedValues(
                "c:\\fullPath\\project.proj",
                ProjectType.Test,
                projectGuid,
                "MyProject",
                reloadedProjectInfo);
        }

        #endregion

    }
}
