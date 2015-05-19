//-----------------------------------------------------------------------
// <copyright file="E2EAnalysisTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.IntegrationTests.E2E
{

    /* Tests:

        * clashing project names -> separate folders
        * Project-level FxCop settings overridden
        * Project types: web, class, 
        * Project languages: C#, VB, C++???
        
    */

    [TestClass]
    public class E2EAnalysisTests
    {
        private const string ExpectedAnalysisFilesListFileName = "FilesToAnalyze.txt";

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_OutputFolderStructure()
        {
            // Checks the output folder structure is correct for a simple solution

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);

            AddEmptyCodeFile(descriptor, rootInputFolder);

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            string projectSpecificOutputDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            // Assert
            CheckProjectOutputFolder(descriptor, projectSpecificOutputDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        [Description("Tests that projects with missing project guids are handled correctly")]
        public void E2E_MissingProjectGuid()
        {
            // Projects with missing guids should have a warning emitted. The project info
            // should not be generated.

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            preImportProperties["SonarConfigPath"] = rootInputFolder;

            ProjectDescriptor descriptor = new ProjectDescriptor()
            {
                // No guid property
                IsTestProject = false,
                ParentDirectoryPath = rootInputFolder,
                ProjectFolderName = "MyProjectDir",
                ProjectFileName = "MissingProjectGuidProject.proj"
            };
            AddEmptyCodeFile(descriptor, rootInputFolder);

            ProjectRootElement projectRoot = BuildUtilities.CreateInitializedProjectRoot(this.TestContext, descriptor, preImportProperties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget); // Build should succeed with warnings
            ProjectInfoAssertions.AssertNoProjectInfoFilesExists(rootOutputFolder);

            logger.AssertExpectedErrorCount(0);
            logger.AssertExpectedWarningCount(1);

            BuildWarningEventArgs warning = logger.Warnings[0];
            Assert.IsTrue(warning.Message.Contains(descriptor.FullFilePath),
                "Expecting the warning to contain the full path to the bad project file");
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        [Description("Tests that projects with invalid project guids are handled correctly")]
        public void E2E_InvalidGuid()
        {
            // Projects with invalid guids should have a warning emitted. The project info
            // should not be generated.

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            ProjectDescriptor descriptor = new ProjectDescriptor()
            {
                // No guid property
                IsTestProject = false,
                ParentDirectoryPath = rootInputFolder,
                ProjectFolderName = "MyProjectDir",
                ProjectFileName = "MissingProjectGuidProject.proj"
            };
            AddEmptyCodeFile(descriptor, rootInputFolder);

            ProjectRootElement projectRoot = BuildUtilities.CreateInitializedProjectRoot(this.TestContext, descriptor, preImportProperties);
            projectRoot.AddProperty("ProjectGuid", "Invalid guid");

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget); // Build should succeed with warnings
            ProjectInfoAssertions.AssertNoProjectInfoFilesExists(rootOutputFolder);

            logger.AssertExpectedErrorCount(0);
            logger.AssertExpectedWarningCount(1);

            BuildWarningEventArgs warning = logger.Warnings[0];
            Assert.IsTrue(warning.Message.Contains(descriptor.FullFilePath),
                "Expecting the warning to contain the full path to the bad project file");
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_NoManagedFiles()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);

            AddEmptyContentFile(descriptor, rootInputFolder);
            AddEmptyContentFile(descriptor, rootInputFolder);
            AddEmptyContentFile(descriptor, rootInputFolder);

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            string projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            //AssertFileDoesNotExist(projectDir, ExpectedManagedInputsListFileName);
            //AssertFileExists(projectDir, ExpectedContentsListFileName);
            AssertFileExists(projectDir, ExpectedAnalysisFilesListFileName);
            
            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_NoContentFiles()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);

            AddEmptyCodeFile(descriptor, rootInputFolder);
            AddEmptyCodeFile(descriptor, rootInputFolder);
            AddEmptyCodeFile(descriptor, rootInputFolder);

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            string projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            AssertFileExists(projectDir, ExpectedAnalysisFilesListFileName);

            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_NoContentOrManagedFiles()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            string projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            AssertFileDoesNotExist(projectDir, ExpectedAnalysisFilesListFileName);

            // Specify the expected analysis results
            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_HasManagedAndContentFiles()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);

            AddEmptyCodeFile(descriptor, rootInputFolder);
            AddEmptyCodeFile(descriptor, rootInputFolder);

            AddEmptyContentFile(descriptor, rootInputFolder);
            AddEmptyContentFile(descriptor, rootInputFolder);

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            string projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            AssertFileExists(projectDir, ExpectedAnalysisFilesListFileName);

            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_ExcludedProjects()
        {
            // Project info should still be written for files with $(SonarQubeExclude) set to true

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);
            descriptor.IsExcluded = true;

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.SonarQubeExclude = "tRUe";

            // Act
            string projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            CheckProjectOutputFolder(descriptor, projectDir);
        }

        #endregion

        #region Private methods

        private void AddEmptyCodeFile(ProjectDescriptor descriptor, string projectFolder)
        {
            string emptyCodeFilePath = Path.Combine(projectFolder, "empty_" + Guid.NewGuid().ToString() + ".cs");
            File.WriteAllText(emptyCodeFilePath, string.Empty);
            
            if (descriptor.ManagedSourceFiles == null)
            {
                descriptor.ManagedSourceFiles = new List<string>();
            }
            descriptor.ManagedSourceFiles.Add(emptyCodeFilePath);
        }

        private void AddEmptyContentFile(ProjectDescriptor descriptor, string projectFolder)
        {
            string emptyFilePath = Path.Combine(projectFolder, "emptyContent_" + Guid.NewGuid().ToString() + ".txt");
            File.WriteAllText(emptyFilePath, string.Empty);

            if (descriptor.ContentFiles == null)
            {
                descriptor.ContentFiles = new List<string>();
            }
            descriptor.ContentFiles.Add(emptyFilePath);
        }

        /// <summary>
        /// Creates a default set of properties sufficient to trigger test analysis
        /// </summary>
        /// <param name="inputPath">The analysis config directory</param>
        /// <param name="outputPath">The output path into which results should be written</param>
        /// <returns></returns>
        private static WellKnownProjectProperties CreateDefaultAnalysisProperties(string configPath, string outputPath)
        {
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarQubeAnalysis = "true";
            
            preImportProperties.SonarQubeConfigPath = configPath;
            preImportProperties.SonarQubeOutputPath = outputPath;
            
            // Ensure the project is isolated from environment variables
            // that could be picked up when running on a TeamBuild build agent
            preImportProperties.TeamBuildLegacyBuildDirectory = "";
            preImportProperties.TeamBuild2105BuildDirectory = "";
            return preImportProperties;
        }

        /// <summary>
        /// Creates and builds a new Sonar-enabled project using the supplied descriptor.
        /// The method will check the build succeeded and that a single project output file was created.
        /// </summary>
        /// <returns>The full path of the project-specsific directory that was created during the build</returns>
        private string CreateAndBuildSonarProject(ProjectDescriptor descriptor, string rootOutputFolder, WellKnownProjectProperties preImportProperties)
        {
            ProjectRootElement projectRoot = BuildUtilities.CreateInitializedProjectRoot(this.TestContext, descriptor, preImportProperties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            // We expect the compiler to warn if there are no compiler inputs
            int expectedWarnings = (descriptor.ManagedSourceFiles == null || !descriptor.ManagedSourceFiles.Any()) ? 1 : 0;
            logger.AssertExpectedErrorCount(0);
            logger.AssertExpectedWarningCount(expectedWarnings);

            logger.AssertExpectedTargetOrdering(
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.CoreCompileTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.DefaultBuildTarget,
                TargetConstants.WriteProjectDataTarget);

            // Check expected folder structure exists
            CheckRootOutputFolder(rootOutputFolder);

            // Check expected project outputs
            Assert.AreEqual(1, Directory.EnumerateDirectories(rootOutputFolder).Count(), "Only expecting one child directory to exist under the root analysis output folder");
            ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);

            return Directory.EnumerateDirectories(rootOutputFolder).Single();
        }

        #endregion

        #region Assertions methods

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
            ProjectInfo expectedProjectInfo = CreateExpectedProjectInfo(expected, projectOutputFolder);
            CheckProjectInfo(expectedProjectInfo, projectOutputFolder);
            CheckAnalysisFileList(expected, projectOutputFolder);

            // Check there are no other files
            List<string> allowedFiles = new List<string>(expectedProjectInfo.AnalysisResults.Select(ar => ar.Location));
            allowedFiles.Add(Path.Combine(projectOutputFolder, FileConstants.ProjectInfoFileName));
            AssertNoAdditionalFilesInFolder(projectOutputFolder, allowedFiles.ToArray());
        }

        private static IList<string> GetAnalysisFiles(ProjectDescriptor descriptor)
        {
            List<string> allFiles = new List<string>();
            if (descriptor.ManagedSourceFiles != null && descriptor.ManagedSourceFiles.Any())
            {
                allFiles.AddRange(descriptor.ManagedSourceFiles);
            }
            if (descriptor.ContentFiles != null && descriptor.ContentFiles.Any())
            {
                allFiles.AddRange(descriptor.ContentFiles);
            }
            return allFiles;
        }

        private void CheckAnalysisFileList(ProjectDescriptor expected, string projectOutputFolder)
        {

            IList<string> analysisFiles = GetAnalysisFiles(expected);

            if (!analysisFiles.Any())
            {
                AssertFileDoesNotExist(projectOutputFolder, ExpectedAnalysisFilesListFileName);
            }
            else
            {
                string fullName = AssertFileExists(projectOutputFolder, ExpectedAnalysisFilesListFileName);

                string[] actualFileNames = File.ReadAllLines(fullName);

                // The actual files might contain extra compiler generated files, so check the expected files
                // we know about is a subset of the actual
                CollectionAssert.IsSubsetOf(analysisFiles.ToArray(), actualFileNames, "Analysis file does not contain the expected entries");
            }
        }


        private void CheckProjectInfo(ProjectInfo expected, string projectOutputFolder)
        {
            string fullName = AssertFileExists(projectOutputFolder, FileConstants.ProjectInfoFileName); // should always exist

            ProjectInfo actualProjectInfo = ProjectInfo.Load(fullName);

            TestUtilities.ProjectInfoAssertions.AssertExpectedValues(expected, actualProjectInfo);
        }

        private static ProjectInfo CreateExpectedProjectInfo (ProjectDescriptor expected, string projectOutputFolder)
        {
            ProjectInfo expectedProjectInfo = expected.CreateProjectInfo();

            // Work out what the expected analysis results are

            if (
                (expected.ManagedSourceFiles != null && expected.ManagedSourceFiles.Any())
                ||
                (expected.ContentFiles != null && expected.ContentFiles.Any())
                )
            {
                expectedProjectInfo.AnalysisResults.Add(
                    new AnalysisResult()
                    {
                        Id = AnalysisType.FilesToAnalyze.ToString(),
                        Location = Path.Combine(projectOutputFolder, ExpectedAnalysisFilesListFileName)
                    });
            }

            return expectedProjectInfo;
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
