//-----------------------------------------------------------------------
// <copyright file="E2EAnalysisTests.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sonar.Common;
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
        private const string ExpectedCompileListFileName = "CompileList.txt";

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
                ProjectName= "nonTestProject",
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = false,
                ParentDirectoryPath = rootInputFolder,
                ProjectFolderName ="nonTestProjectDir",
                ProjectFileName = "nonTestProject.csproj"
            };

            ProjectRootElement project1Root = BuildUtilities.CreateProjectFromDescriptor(this.TestContext, project1);
            project1Root.AddProperty(TargetProperties.SonarOutputPath, rootOutputFolder);
            project1Root.Save(project1.FullFilePath);

            this.TestContext.AddResultFile(project1.FullFilePath);

            ProjectInstance project1Instance = new ProjectInstance(project1Root);
            BuildResult result = BuildUtilities.BuildTarget(project1Instance, TargetConstants.WriteSonarProjectDataTargetName);

            BuildUtilities.AssertTargetSucceeded(result, TargetConstants.WriteSonarProjectDataTargetName);

            // Check expected folder structure exists
            CheckRootOutputFolder(rootOutputFolder);

            // Check expected project outputs
            // TODO: check only one
            string projectDir = Directory.EnumerateDirectories(rootOutputFolder).FirstOrDefault();
            Assert.IsFalse(string.IsNullOrEmpty(projectDir), "No project directories were created");

            // Specify the expected analysis results
            project1.AddAnalysisResult(AnalysisType.ManagedCompilerInputs.ToString(), Path.Combine(projectDir, ExpectedCompileListFileName));

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
                expected.ProjectFolderName, folderName);

            // Check specific files
            CheckProjectInfo(expected, projectOutputFolder);
            CheckCompileList(expected, projectOutputFolder);

            // Check there are no other files
            List<string> allowedFiles = new List<string>(expected.AnalysisResults.Select(ar => ar.Location));
            allowedFiles.Add(Path.Combine(projectOutputFolder, FileConstants.ProjectInfoFileName));
            AssertNoAdditionalFilesInFolder(projectOutputFolder, allowedFiles.ToArray());
        }

        private void CheckCompileList(ProjectDescriptor expected, string projectOutputFolder)
        {
            string fullName = AssertFileExists(projectOutputFolder, ExpectedCompileListFileName);

            string[] actualFileNames = File.ReadAllLines(fullName);

            CollectionAssert.AreEquivalent(expected.ManagedSourceFiles ?? new string[] { }, actualFileNames, "Compile list file does not contain the expected entries");
        }

        private void CheckProjectInfo(ProjectDescriptor expected, string projectOutputFolder)
        {
            string fullName = AssertFileExists(projectOutputFolder, FileConstants.ProjectInfoFileName); // should always exist

            ProjectInfo actualProjectInfo = ProjectInfo.Load(fullName);

            ProjectInfo expectedProjectInfo = expected.CreateProjectInfo();
            TestUtilities.ProjectInfoAssertions.AssertExpectedValues(expectedProjectInfo, actualProjectInfo);
        }

        private string AssertFileExists(string projectOutputFolder, string fileName)
        {
            string fullPath = Path.Combine(projectOutputFolder, fileName);
            bool exists = this.CheckExistenceAndAddToResults(fullPath);

            Assert.IsTrue(exists, "Expected file does not exist: {0}", fullPath);
            return fullPath;
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
            IEnumerable<string> additionalFiles = files.Except(allowedFileNames);

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
