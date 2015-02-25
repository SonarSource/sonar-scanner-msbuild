using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using Microsoft.Build.Utilities;

namespace SonarMSBuild.Tasks.IntegrationTests
{
    [TestClass]
    public class WriteProjectInfoTaskTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void WriteProjectInfoFile_RequiredProperties()
        {
            Assert.Inconclusive("TODO: this test requires a real build engine to check the required properties");
            // TODO: this requires a real build engine to do the checking
            // 0. Setup
            WriteProjectInfoFile task;

            // 1. Missing project path
            task = this.CreateValidTask();
            task.FullProjectPath = null;
            AssertTaskExecutionThrows(task);

            // 2. Missing guid
            task = this.CreateValidTask();
            task.ProjectGuid = "";
            AssertTaskExecutionThrows(task);

            // 3. Missing project name
            task = this.CreateValidTask();
            task.ProjectName = null;
            AssertTaskExecutionThrows(task);

            // 4. Missing output folder
            task = this.CreateValidTask();
            task.OutputFolder = null;
            AssertTaskExecutionThrows(task);
        }


        #endregion


        #region Private methods

        /// <summary>
        /// Creates and returns a new task with valid properties (i.e. can be executed successfully)
        /// </summary>
        private WriteProjectInfoFile CreateValidTask()
        {

            string folderPath = TestUtils.EnsureTestSpecificFolder(this.TestContext);

            WriteProjectInfoFile task = new WriteProjectInfoFile();
            task.FullProjectPath = "d:\\project.csproj";
            task.ProjectGuid = Guid.NewGuid().ToString("B");
            task.ProjectName = "AProject";
            task.OutputFolder = folderPath;

            return task;
        }

        private static void AssertTaskExecutionThrows(Task buildTask)
        {
            AssertException.Expects<ArgumentNullException>(() => buildTask.Execute());
        }

        #endregion

    }
}
