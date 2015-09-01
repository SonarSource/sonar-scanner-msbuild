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

            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);

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
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);

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
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);

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

            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);

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

            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);

            AddEmptyContentFile(descriptor, rootInputFolder);
            AddEmptyContentFile(descriptor, rootInputFolder);

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            string projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            AssertFileExists(projectDir, ExpectedAnalysisFilesListFileName);

            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets"), TestCategory("VB")]
        public void E2E_HasManagedAndContentFiles_VB()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);
            descriptor.ProjectLanguage = ProjectLanguages.VisualBasic;

            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder, ".vb");
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder, ".vb");

            AddEmptyContentFile(descriptor, rootInputFolder);
            AddEmptyContentFile(descriptor, rootInputFolder);

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            string projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties, isVBProject:true);

            AssertFileExists(projectDir, ExpectedAnalysisFilesListFileName);

            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestCategory("E2E"), TestCategory("Targets")] // SONARMSBRU-12: Analysis build fails if the build definition name contains brackets
        public void E2E_UsingTaskHandlesBracketsInName()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "folder with brackets in name (SONARMSBRU-12)");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);

            // Copy the task assembly to a folder with brackets in the name
            string taskAssemblyFilePath = typeof(WriteProjectInfoFile).Assembly.Location;
            string asmName = Path.GetFileName(taskAssemblyFilePath);
            string copiedTaskAssemblyFilePath = Path.Combine(rootInputFolder, Path.GetFileName(asmName));
            File.Copy(taskAssemblyFilePath, copiedTaskAssemblyFilePath);

            // Set the project property to use that file. To reproduce the bug, we need to have MSBuild search for
            // the assembly using "GetDirectoryNameOfFileAbove".
            string val = @"$([MSBuild]::GetDirectoryNameOfFileAbove('{0}', '{1}'))\{1}";
            val = string.Format(System.Globalization.CultureInfo.InvariantCulture, val, rootInputFolder, asmName);

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.Add(TargetProperties.SonarBuildTasksAssemblyFile, val);

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

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_BareProject_FilesToAnalyse()
        {
            // Checks the integration targets handle non-VB/C# project types
            // that don't import the standard targets or set the expected properties
            // The project info should be created as normal and the correct files to analyse detected.

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            BuildLogger logger = new BuildLogger();

            string sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(this.TestContext);
            string projectFilePath = Path.Combine(rootInputFolder, "project.txt");
            Guid projectGuid = Guid.NewGuid();

            string codeFile = CreateEmptyFile(rootInputFolder, "cpp");
            string contentFile = CreateEmptyFile(rootInputFolder, ".js");
            string unanalysedFile = CreateEmptyFile(rootInputFolder, ".shouldnotbeanalysed");
            string excludedFile = CreateEmptyFile(rootInputFolder, "excluded.cpp");

            string projectXml = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='12.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>

  <PropertyGroup>
    <ProjectGuid>{0}</ProjectGuid>

    <SonarQubeTempPath>{1}</SonarQubeTempPath>
    <SonarQubeOutputPath>{1}</SonarQubeOutputPath>
    <SonarQubeBuildTasksAssemblyFile>{2}</SonarQubeBuildTasksAssemblyFile>
  </PropertyGroup>

  <ItemGroup>
    <ClCompile Include='{4}' />
    <Content Include='{5}' />
    <ShouldBeIgnored Include='{6}' />
    <ClCompile Include='{7}'>
      <SonarQubeExclude>true</SonarQubeExclude>
    </ClCompile>
  </ItemGroup>

  <Import Project='{3}' />

  <Target Name='Build'>
    <Message Importance='high' Text='In dummy build target' />
  </Target>

</Project>
";
            ProjectRootElement projectRoot = BuildUtilities.CreateProjectFromTemplate(projectFilePath, this.TestContext, projectXml,
                projectGuid.ToString(),
                rootOutputFolder,
                typeof(WriteProjectInfoFile).Assembly.Location,
                sqTargetFile,
                codeFile,
                contentFile,
                unanalysedFile,
                excludedFile
                );

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger,
                TargetConstants.DefaultBuildTarget);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertExpectedTargetOrdering(
                TargetConstants.DefaultBuildTarget,
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.WriteProjectDataTarget);

            // Not expecting the FxCop targets to execute as they should not be imported
            logger.AssertTargetNotExecuted(TargetConstants.OverrideFxCopSettingsTarget);
            logger.AssertTargetNotExecuted(TargetConstants.FxCopTarget);
            logger.AssertTargetNotExecuted(TargetConstants.SetFxCopResultsTarget);

            // Check the content of the project info xml
            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);

            Assert.AreEqual(projectGuid, projectInfo.ProjectGuid, "Unexpected project guid");
            Assert.IsNull(projectInfo.ProjectLanguage, "Expecting the project language to be null");
            Assert.IsFalse(projectInfo.IsExcluded, "Project should not be marked as excluded");
            Assert.AreEqual(ProjectType.Product, projectInfo.ProjectType, "Project should be marked as a product project");
            Assert.AreEqual(1, projectInfo.AnalysisResults.Count, "Unexpected number of analysis results created");

            // Check the correct list of files to analyse were returned
            AnalysisResult filesToAnalyse = ProjectInfoAssertions.AssertAnalysisResultExists(projectInfo, AnalysisType.FilesToAnalyze.ToString());
            string[] actualFilesToAnalyse = File.ReadAllLines(filesToAnalyse.Location);
            CollectionAssert.AreEquivalent(new string[] { codeFile, contentFile }, actualFilesToAnalyse, "Unexpected list of files to analyse");
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_BareProject_CorrectlyCategorised()
        {
            // Checks that projects that don't include the standard managed targets are still
            // processed correctly e.g. can be excluded, marked as test projects etc
            
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            BuildLogger logger = new BuildLogger();

            string sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(this.TestContext);
            string projectFilePath = Path.Combine(rootInputFolder, "project.txt");
            Guid projectGuid = Guid.NewGuid();

            string projectXml = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='12.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>

  <PropertyGroup>
    <SonarQubeExclude>true</SonarQubeExclude>
    <Language>my.language</Language>
    <ProjectTypeGuids>{4}</ProjectTypeGuids>

    <ProjectGuid>{0}</ProjectGuid>

    <SonarQubeTempPath>{1}</SonarQubeTempPath>
    <SonarQubeOutputPath>{1}</SonarQubeOutputPath>
    <SonarQubeBuildTasksAssemblyFile>{2}</SonarQubeBuildTasksAssemblyFile>
  </PropertyGroup>

  <ItemGroup>
    <!-- no recognised content -->
  </ItemGroup>

  <Import Project='{3}' />

  <Target Name='Build'>
    <Message Importance='high' Text='In dummy build target' />
  </Target>

</Project>
";
            ProjectRootElement projectRoot = BuildUtilities.CreateProjectFromTemplate(projectFilePath, this.TestContext, projectXml,
                projectGuid.ToString(),
                rootOutputFolder,
                typeof(WriteProjectInfoFile).Assembly.Location,
                sqTargetFile,
                TargetConstants.MsTestProjectTypeGuid
                );

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger,
                TargetConstants.DefaultBuildTarget);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertExpectedTargetOrdering(
                TargetConstants.DefaultBuildTarget,
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.WriteProjectDataTarget);

            // Check the project info
            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);

            Assert.IsTrue(projectInfo.IsExcluded, "Expecting the project to be marked as excluded");
            Assert.AreEqual("my.language", projectInfo.ProjectLanguage, "Unexpected project language");
            Assert.AreEqual(ProjectType.Test, projectInfo.ProjectType, "Project should be marked as a test project");
            Assert.AreEqual(0, projectInfo.AnalysisResults.Count, "Unexpected number of analysis results created");
        }

        #endregion

        #region Private methods

        private string AddEmptyAnalysedCodeFile(ProjectDescriptor descriptor, string projectFolder, string extension = "cs")
        {
            string filePath = CreateEmptyFile(projectFolder, extension);
            descriptor.AddCompileInputFile(filePath, true);
            return filePath;
        }
        
        private static void AddEmptyContentFile(ProjectDescriptor descriptor, string projectFolder)
        {
            string filePath = CreateEmptyFile(projectFolder, "txt");
            descriptor.AddContentFile(filePath, true);
        }

        private static string CreateEmptyFile(string folder, string extension)
        {
            string emptyFilePath = Path.Combine(folder, "empty_" + Guid.NewGuid().ToString() + ".xxx");
            emptyFilePath = Path.ChangeExtension(emptyFilePath, extension);
            File.WriteAllText(emptyFilePath, string.Empty);

            return emptyFilePath;
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

            preImportProperties.SonarQubeTempPath = outputPath; // FIXME
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
        private string CreateAndBuildSonarProject(ProjectDescriptor descriptor, string rootOutputFolder, WellKnownProjectProperties preImportProperties, bool isVBProject = false)
        {
            ProjectRootElement projectRoot = BuildUtilities.CreateInitializedProjectRoot(this.TestContext, descriptor, preImportProperties, isVBProject);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            // We expect the compiler to warn if there are no compiler inputs
            int expectedWarnings = (descriptor.ManagedSourceFiles.Any()) ? 0 : 1;
            logger.AssertExpectedErrorCount(0);
            logger.AssertExpectedWarningCount(expectedWarnings);

            logger.AssertExpectedTargetOrdering(
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.DefaultBuildTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
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

        private static void CheckRootOutputFolder(string rootOutputFolder)
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

        private static IList<string> GetExpectedAnalysisFiles(ProjectDescriptor descriptor)
        {
            return descriptor.Files.Where(f => f.ShouldBeAnalysed).Select(f => f.FilePath).ToList();
        }

        private void CheckAnalysisFileList(ProjectDescriptor expected, string projectOutputFolder)
        {
            string[] expectedFiles = GetExpectedAnalysisFiles(expected).ToArray();

            if (!expectedFiles.Any())
            {
                AssertFileDoesNotExist(projectOutputFolder, ExpectedAnalysisFilesListFileName);
            }
            else
            {
                string fullName = AssertFileExists(projectOutputFolder, ExpectedAnalysisFilesListFileName);

                string[] actualFiles = File.ReadAllLines(fullName);

                // The actual files might contain extra compiler generated files, so check the expected files
                // we expected is a subset of the actual
                CollectionAssert.IsSubsetOf(expectedFiles, actualFiles, "Analysis file does not contain the expected entries");

                // Check that any files that should not be analysed are not included
                if (expected.FilesNotToAnalyse != null && expected.FilesNotToAnalyse.Any())
                {
                    foreach(string unanalysedFile in expected.FilesNotToAnalyse)
                    {
                        CollectionAssert.DoesNotContain(expectedFiles, unanalysedFile, "Not expecting file to be included for analysis: {0}", unanalysedFile);
                    }
                }
            }
        }

        private static void AssertFileIsNotAnalysed(string analysisFileListPath, string unanalysedPath)
        {
            string[] actualFiles = GetAnalysedFiles(analysisFileListPath); CollectionAssert.DoesNotContain(actualFiles, unanalysedPath, "File should not be analysed: {0}", unanalysedPath);
        }

        private static void AssertFileIsAnalysed(string analysisFileListPath, string unanalysedPath)
        {
            string[] actualFiles = GetAnalysedFiles(analysisFileListPath);
            CollectionAssert.Contains(actualFiles, unanalysedPath, "File should not be analysed: {0}", unanalysedPath);
        }

        private static string[] GetAnalysedFiles(string analysisFileListPath)
        {
            if (!File.Exists(analysisFileListPath))
            {
                Assert.Inconclusive("Test error: the specified analysis file list does not exist: {0}", analysisFileListPath);
            }

            return File.ReadAllLines(analysisFileListPath);
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

            if (expected.Files.Any(f => f.ShouldBeAnalysed))
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
