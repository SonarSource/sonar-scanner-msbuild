//-----------------------------------------------------------------------
// <copyright file="WriteProjectInfoFileTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.UnitTests
{
    [TestClass]
    public class WriteProjectInfoFileTests
    {
        private const string ExpectedProjectInfoFileName = "ProjectInfo.xml";

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
            // No analysis results are supplied

            // Act
            ProjectInfo reloadedProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);

            // Addition assertions
            ProjectInfoAssertions.AssertExpectedValues(
                "c:\\fullPath\\project.proj",
                ProjectType.Test,
                projectGuid,
                "MyProject",
                reloadedProjectInfo);
        }

        [TestMethod]
        [Description("Tests that analysis results are correctly handled")]
        public void WriteProjectInfoFile_AnalysisResults()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid projectGuid = Guid.NewGuid();

            WriteProjectInfoFile task = new WriteProjectInfoFile();
            task.FullProjectPath = "x:\\a.csproj";
            task.IsTest = false;
            task.OutputFolder = testFolder;
            task.ProjectGuid = projectGuid.ToString("B");
            task.ProjectName = "MyProject";

            List<ITaskItem> resultInputs = new List<ITaskItem>();

            // Add invalid task items
            // Note: the TaskItem class won't allow the item spec or metadata values to be null,
            // so we aren't testing those
            resultInputs.Add(CreateMetadataItem("itemSpec1", "abc", "def")); // Id field is missing
            resultInputs.Add(CreateAnalysisTaskItem("\r", "should be ignored - whitespace")); // whitespace id
            resultInputs.Add(CreateAnalysisTaskItem("should be ignored - whitespace", " ")); // whitespace location

            // Add valid task items
            resultInputs.Add(CreateAnalysisTaskItem("id1", "location1"));
            resultInputs.Add(CreateAnalysisTaskItem("id2", "location2", "md1", "md1 value", "md2", "md2 value")); // valid but with extra metadata

            task.AnalysisResults = resultInputs.ToArray();

            // Act
            ProjectInfo createdProjectInfo;
            using (new AssertIgnoreScope()) // We've deliberately created task items with unexpected item names that will cause assertions
            {
                createdProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);
            }

            // Assert
            AssertAnalysisResultExists(createdProjectInfo, "id1", "location1");
            AssertAnalysisResultExists(createdProjectInfo, "id2", "location2");
            AssertExpectedAnalysisResultCount(2, createdProjectInfo);
        }

        [TestMethod]
        [Description("Tests that the project info file is not created if a project guid is not supplied")]
        [WorkItem(50)] // Regression test for Bug 50:MSBuild projects with missing ProjectGuids cause the build to fail
        public void WriteProjectInfoFile_MissingProjectGuid()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            WriteProjectInfoFile task = new WriteProjectInfoFile();
            task.FullProjectPath = "c:\\fullPath\\project.proj";
            task.IsTest = true;
            task.OutputFolder = testFolder;
            task.ProjectName = "ProjectWithoutProjectGuid";
            // No analysis results are supplied

            // Act
            DummyBuildEngine engine = new DummyBuildEngine();
            task.BuildEngine = engine;
            bool success = task.Execute();

            Assert.IsTrue(success, "Not expecting the task to fail as this would fail the build");
            engine.AssertNoErrors();
            Assert.AreEqual(1, engine.Warnings.Count, "Expecting a build warning as the ProjectGuid is missing");

            BuildWarningEventArgs firstWarning = engine.Warnings[0];
            Assert.IsNotNull(firstWarning.Message, "Warning message should not be null");   
        }

        #endregion

        #region Helper methods

        private static ITaskItem CreateAnalysisTaskItem(string id, string location, params string[] idAndValuePairs)
        {
            ITaskItem item = CreateMetadataItem(location, idAndValuePairs);
            item.SetMetadata(BuildTaskConstants.ResultMetadataIdProperty, id);
            return item;
        }

        private static ITaskItem CreateMetadataItem(string itemSpec, params string[] idAndValuePairs)
        {
            ITaskItem item = new TaskItem(itemSpec);
            int remainder;
            int dummy = Math.DivRem(idAndValuePairs.Length, 2, out remainder);
            Assert.AreEqual(0, remainder, "Test setup error: the supplied list should contain id-location pairs");

            for(int index = 0; index < idAndValuePairs.Length; index += 2)
            {
                item.SetMetadata(idAndValuePairs[index], idAndValuePairs[index + 1]);
            }
            return item;
        }

        #endregion

        #region Checks

        private ProjectInfo ExecuteAndCheckSucceeds(Task task, string testFolder)
        {
            string expectedOutputFile = Path.Combine(testFolder, ExpectedProjectInfoFileName);
            Assert.IsFalse(File.Exists(expectedOutputFile), "Test error: output file should not exist before the task is executed");

            bool result = task.Execute();

            Assert.IsTrue(result, "Expecting the task execution to succeed");
            Assert.IsTrue(File.Exists(expectedOutputFile), "Expected output file was not created by the task. Expected: {0}", expectedOutputFile);
            this.TestContext.AddResultFile(expectedOutputFile);

            ProjectInfo reloadedProjectInfo = ProjectInfo.Load(expectedOutputFile);
            Assert.IsNotNull(reloadedProjectInfo, "Not expecting the reloaded project info file to be null");
            return reloadedProjectInfo;
        }

        private static void AssertAnalysisResultExists(ProjectInfo actual, string expectedId, string expectedLocation)
        {
            Assert.IsNotNull(actual, "Supplied project info should not be null");
            Assert.IsNotNull(actual.AnalysisResults, "AnalysisResults array should not be null");

            AnalysisResult result = actual.AnalysisResults.FirstOrDefault(ar => expectedId.Equals(ar.Id, StringComparison.InvariantCulture));
            Assert.IsNotNull(result, "AnalysisResult with the expected id does not exist. Id: {0}", expectedId);

            Assert.AreEqual(result.Location, expectedLocation, "Analysis result does not have the expected location");
        }

        private static void AssertExpectedAnalysisResultCount(int count, ProjectInfo actual)
        {
            Assert.IsNotNull(actual, "Supplied project info should not be null");
            Assert.IsNotNull(actual.AnalysisResults, "AnalysisResults array should not be null");

            Assert.AreEqual(count, actual.AnalysisResults.Count, "Unexpected number of AnalysisResult items");
        }

        #endregion
    }
}
