//-----------------------------------------------------------------------
// <copyright file="E2EAnalysisTests.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;


namespace SonarMSBuild.Tasks.IntegrationTests.E2E
{

    /* Tests:

        * clashing project names -> separate folders
        * Project-level FxCop settings overridden
        * Project types: web, class, 
        * Project languages: C#, VB, C++???
        * Handling of missing Guids
        * Output dir is not set
        
    */

    [TestClass]
    [DeploymentItem("LinkedFiles\\Sonar.Integration.v0.1.targets")]
    public class E2EAnalysisTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void E2E_OutputFolderStructure()
        {
            // Checks the output folder structure is correct for a simple solution
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor project1 = new ProjectDescriptor()
            {
                ProjectName = "nonTestProject",
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = false
            };
            project1.ProjectPath = Path.Combine(rootInputFolder, project1.ProjectName);

            ProjectRootElement project1Root = BuildUtilities.CreateProjectFromDescriptor(this.TestContext, project1);
            project1Root.AddProperty(TargetProperties.SonarOutputPath, rootOutputFolder);
            project1Root.Save(project1.ProjectPath);

            this.TestContext.AddResultFile(project1.ProjectPath);

            ProjectInstance project1Instance = new ProjectInstance(project1Root);
            BuildResult result = BuildUtilities.BuildTarget(project1Instance, TargetConstants.WriteSonarProjectDataTargetName);

            BuildUtilities.AssertTargetSucceeded(result, TargetConstants.WriteSonarProjectDataTargetName);

            // Check expected folder structure exists
            CheckRootOutputFolder(rootOutputFolder);

            // Check expected project outputs
            // TODO: check only one
            string projectDir = Directory.EnumerateDirectories(rootOutputFolder).FirstOrDefault();
            Assert.IsFalse(string.IsNullOrEmpty(projectDir), "No project directories were created");

            CheckProjectOutputFolder(project1, projectDir);
        }

        #endregion

        #region Private methods

        private void CheckRootOutputFolder(string rootOutputFolder)
        {
            Assert.IsTrue(Directory.Exists(rootOutputFolder), "Expected root output folder does not exist");

            int fileCount = Directory.GetFiles(rootOutputFolder, "*.*", SearchOption.TopDirectoryOnly).Count();
            Assert.AreEqual(0, fileCount, "Not expecting the top-level output folder to contain any files");
        }

        private void CheckProjectOutputFolder(ProjectDescriptor expected, string projectOutputFolder)
        {
            Assert.IsFalse(string.IsNullOrEmpty(projectOutputFolder), "Test error: projectOutputFolder should not be null/empty");
            Assert.IsTrue(Directory.Exists(projectOutputFolder), "Expected project folder does not exist: {0}", projectOutputFolder);

            // Check folder naming
            string folderName = Path.GetFileName(projectOutputFolder);
            Assert.IsTrue(folderName.StartsWith(expected.ProjectName), "Project output folder does not start with the project name. Expected: {0}, actual: {1}",
                expected.ProjectName, folderName);

            // Check specific files
            CheckProjectInfo(expected, projectOutputFolder);
            CheckCompileList(expected, projectOutputFolder);

            // Check there are no other files
            AssertNoAdditionalFilesInFolder(projectOutputFolder, FileConstants.CompileListFileName, FileConstants.ProjectInfoFileName);
        }

        private void CheckCompileList(ProjectDescriptor expected, string projectOutputFolder)
        {
            AssertFileExists(projectOutputFolder, FileConstants.CompileListFileName);

            string fullName = Path.Combine(projectOutputFolder, FileConstants.CompileListFileName);
            string[] actualFileNames = File.ReadAllLines(fullName);

            CollectionAssert.AreEquivalent(expected.CompileInputs ?? new string[] { }, actualFileNames, "Compile list file does not contain the expected entries");
        }

        private void CheckProjectInfo(ProjectDescriptor expected, string projectOutputFolder)
        {
            AssertFileExists(projectOutputFolder, FileConstants.ProjectInfoFileName); // should always exist

            string fullName = Path.Combine(projectOutputFolder, FileConstants.ProjectInfoFileName);
            ProjectInfo actualProjectInfo = ProjectInfo.Load(fullName);

            ProjectInfo expectedProjectInfo = expected.CreateProjectInfo();
            TestUtilities.ProjectInfoAssertions.AssertExpectedValues(expectedProjectInfo, actualProjectInfo);
        }

        private void AssertFileExists(string projectOutputFolder, string fileName)
        {
            string fullPath = Path.Combine(projectOutputFolder, fileName);
            bool exists = this.CheckExistenceAndAddToResults(fullPath);

            Assert.IsTrue(exists, "Expected file does not exist: {0}", fullPath);
        }

        private void AssertFileDoesNotExist(string projectOutputFolder, string fileName)
        {
            string fullPath = Path.Combine(projectOutputFolder, fileName);
            bool exists = this.CheckExistenceAndAddToResults(fullPath);

            Assert.IsFalse(exists, "Not expecting file to exist: {0}", fullPath);
        }

        private bool CheckExistenceAndAddToResults(string fullPath)
        {
            bool exists = File.Exists(fullPath);
            if (exists)
            {
                this.TestContext.AddResultFile(fullPath);
            }
            return exists;
        }

        private static void AssertNoAdditionalFilesInFolder(string folderPath, params string[] allowedFileNames)
        {
            string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
            IEnumerable<string> additionalFiles = files.Select(f => Path.GetFileName(f)).
                Except(new string[] { FileConstants.CompileListFileName, FileConstants.ProjectInfoFileName });

            if (additionalFiles.Any())
            {
                Console.WriteLine("Additional file(s) in folder: {0}", folderPath);
                foreach (string additionalFile in additionalFiles)
                {
                    Console.WriteLine("\t{0}", additionalFile);
                }
                Assert.Fail("Additional files exist in the project output folder: {0}", folderPath);
            }

        }

        #endregion

    }
}
