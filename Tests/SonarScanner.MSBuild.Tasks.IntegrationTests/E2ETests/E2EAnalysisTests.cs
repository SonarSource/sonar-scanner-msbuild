/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests.E2E
{
    [TestClass]
    public class E2EAnalysisTests
    {
        private const string ExpectedAnalysisFilesListFileName = "FilesToAnalyze.txt";

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
        {
            HackForVs2017Update3.Enable();
        }

        #region Tests

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_OutputFolderStructure()
        {
            // Checks the output folder structure is correct for a simple solution

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);

            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            var projectSpecificOutputDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

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
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            preImportProperties["SonarConfigPath"] = rootInputFolder;

            var descriptor = new ProjectDescriptor()
            {
                // No guid property
                IsTestProject = false,
                ParentDirectoryPath = rootInputFolder,
                ProjectFolderName = "MyProjectDir",
                ProjectFileName = "MissingProjectGuidProject.proj"
            };
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);

            var projectRoot = BuildUtilities.CreateInitializedProjectRoot(TestContext, descriptor, preImportProperties);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, descriptor.FullFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings
            ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, Path.Combine(rootInputFolder,
                descriptor.ProjectFolderName, descriptor.ProjectFileName));

            result.AssertExpectedErrorCount(0);
            result.AssertExpectedWarningCount(1);

            var warning = result.Warnings[0];
            Assert.IsTrue(warning.Contains(descriptor.FullFilePath),
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
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            var descriptor = new ProjectDescriptor()
            {
                // No guid property
                IsTestProject = false,
                ParentDirectoryPath = rootInputFolder,
                ProjectFolderName = "MyProjectDir",
                ProjectFileName = "MissingProjectGuidProject.proj"
            };
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);

            var projectRoot = BuildUtilities.CreateInitializedProjectRoot(TestContext, descriptor, preImportProperties);
            projectRoot.AddProperty("ProjectGuid", "Invalid guid");

            string projectFilePath = Path.Combine(rootInputFolder,
                descriptor.ProjectFolderName, descriptor.ProjectFileName);

            projectRoot.Save(projectFilePath);
            TestContext.AddResultFile(projectFilePath);

            // Act
            var buildLog = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            buildLog.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings
            ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectFilePath);

            buildLog.AssertExpectedErrorCount(0);
            buildLog.AssertExpectedWarningCount(1);

            var warning = buildLog.Warnings[0];
            Assert.IsTrue(warning.Contains(descriptor.FullFilePath),
                "Expecting the warning to contain the full path to the bad project file");
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_NoManagedFiles()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);

            AddEmptyContentFile(descriptor, rootInputFolder);
            AddEmptyContentFile(descriptor, rootInputFolder);
            AddEmptyContentFile(descriptor, rootInputFolder);

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            var projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            AssertFileExists(projectDir, ExpectedAnalysisFilesListFileName);

            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_NoContentFiles()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);

            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            var projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            AssertFileExists(projectDir, ExpectedAnalysisFilesListFileName);

            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_NoContentOrManagedFiles()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            var projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            AssertFileDoesNotExist(projectDir, ExpectedAnalysisFilesListFileName);

            // Specify the expected analysis results
            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_HasManagedAndContentFiles()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);

            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);

            AddEmptyContentFile(descriptor, rootInputFolder);
            AddEmptyContentFile(descriptor, rootInputFolder);

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            var projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            AssertFileExists(projectDir, ExpectedAnalysisFilesListFileName);

            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets"), TestCategory("VB")]
        public void E2E_HasManagedAndContentFiles_VB()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);
            descriptor.ProjectLanguage = ProjectLanguages.VisualBasic;

            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder, ".vb");
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder, ".vb");

            AddEmptyContentFile(descriptor, rootInputFolder);
            AddEmptyContentFile(descriptor, rootInputFolder);

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            var projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            AssertFileExists(projectDir, ExpectedAnalysisFilesListFileName);

            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")] // SONARMSBRU-104: files under the obj folder should be excluded from analysis
        public void E2E_IntermediateOutputFilesAreExcluded()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            // Create a new project
            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);
            var projectFolder = descriptor.FullDirectoryPath;

            // Add files that should be analyzed
            var nonObjFolder = Path.Combine(projectFolder, "foo");
            Directory.CreateDirectory(nonObjFolder);
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);
            AddEmptyAnalysedCodeFile(descriptor, nonObjFolder);

            // Add files under the obj folder that should not be analyzed
            var objFolder = Path.Combine(projectFolder, "obj");
            var objSubFolder1 = Path.Combine(objFolder, "debug");
            var objSubFolder2 = Path.Combine(objFolder, "xxx"); // any folder under obj should be ignored
            Directory.CreateDirectory(objSubFolder1);
            Directory.CreateDirectory(objSubFolder2);

            // File in obj
            var filePath = CreateEmptyFile(objFolder, "cs");
            descriptor.AddCompileInputFile(filePath, false);

            // File in obj\debug
            filePath = CreateEmptyFile(objSubFolder1, "cs");
            descriptor.AddCompileInputFile(filePath, false);

            // File in obj\xxx
            filePath = CreateEmptyFile(objSubFolder2, "vb");
            descriptor.AddCompileInputFile("obj\\xxx\\" + Path.GetFileName(filePath), false); // reference the file using a relative path

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Act
            var projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            AssertFileExists(projectDir, ExpectedAnalysisFilesListFileName);

            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestCategory("E2E"), TestCategory("Targets")] // SONARMSBRU-12: Analysis build fails if the build definition name contains brackets
        public void E2E_UsingTaskHandlesBracketsInName()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "folder with brackets in name (SONARMSBRU-12)");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);
            AddEmptyAnalysedCodeFile(descriptor, rootInputFolder);

            // Copy the task assembly to a folder with brackets in the name
            var taskAssemblyFilePath = typeof(WriteProjectInfoFile).Assembly.Location;
            var asmName = Path.GetFileName(taskAssemblyFilePath);
            var copiedTaskAssemblyFilePath = Path.Combine(rootInputFolder, Path.GetFileName(asmName));
            File.Copy(taskAssemblyFilePath, copiedTaskAssemblyFilePath);

            // Set the project property to use that file. To reproduce the bug, we need to have MSBuild search for
            // the assembly using "GetDirectoryNameOfFileAbove".
            var val = @"$([MSBuild]::GetDirectoryNameOfFileAbove('{0}', '{1}'))\{1}";
            val = string.Format(System.Globalization.CultureInfo.InvariantCulture, val, rootInputFolder, asmName);

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.Add(TargetProperties.SonarBuildTasksAssemblyFile, val);

            // Act
            var projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            AssertFileExists(projectDir, ExpectedAnalysisFilesListFileName);

            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_ExcludedProjects()
        {
            // Project info should still be written for files with $(SonarQubeExclude) set to true

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);
            descriptor.IsExcluded = true;

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.SonarQubeExclude = "tRUe";

            // Act
            var projectDir = CreateAndBuildSonarProject(descriptor, rootOutputFolder, preImportProperties);

            CheckProjectOutputFolder(descriptor, projectDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_BareProject_FilesToAnalyse()
        {
            // Checks the integration targets handle non-VB/C# project types
            // that don't import the standard targets or set the expected properties
            // The project info should be created as normal and the correct files to analyze detected.

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            var projectFilePath = Path.Combine(rootInputFolder, "project.txt");
            var projectGuid = Guid.NewGuid();

            var codeFile = CreateEmptyFile(rootInputFolder, "cpp");
            var contentFile = CreateEmptyFile(rootInputFolder, ".js");
            var unanalysedFile = CreateEmptyFile(rootInputFolder, ".shouldnotbeanalysed");
            var excludedFile = CreateEmptyFile(rootInputFolder, "excluded.cpp");

            var projectXml = @"<?xml version='1.0' encoding='utf-8'?>
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
            var projectRoot = BuildUtilities.CreateProjectFromTemplate(projectFilePath, TestContext, projectXml,
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
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath,
                TargetConstants.DefaultBuildTarget);

            // Assert
            result.BuildSucceeded.Should().BeTrue();

            result.AssertExpectedTargetOrdering(
                TargetConstants.DefaultBuildTarget,
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.WriteProjectDataTarget);

            // Check the content of the project info xml
            var projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);

            Assert.AreEqual(projectGuid, projectInfo.ProjectGuid, "Unexpected project guid");
            Assert.IsNull(projectInfo.ProjectLanguage, "Expecting the project language to be null");
            Assert.IsFalse(projectInfo.IsExcluded, "Project should not be marked as excluded");
            Assert.AreEqual(ProjectType.Product, projectInfo.ProjectType, "Project should be marked as a product project");
            Assert.AreEqual(1, projectInfo.AnalysisResults.Count, "Unexpected number of analysis results created");

            // Check the correct list of files to analyze were returned
            var filesToAnalyse = ProjectInfoAssertions.AssertAnalysisResultExists(projectInfo, AnalysisType.FilesToAnalyze.ToString());
            var actualFilesToAnalyse = File.ReadAllLines(filesToAnalyse.Location);
            CollectionAssert.AreEquivalent(new string[] { codeFile, contentFile }, actualFilesToAnalyse, "Unexpected list of files to analyze");
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_BareProject_CorrectlyCategorised()
        {
            // Checks that projects that don't include the standard managed targets are still
            // processed correctly e.g. can be excluded, marked as test projects etc

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            var projectFilePath = Path.Combine(rootInputFolder, "project.txt");
            var projectGuid = Guid.NewGuid();

            var projectXml = @"<?xml version='1.0' encoding='utf-8'?>
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
    <!-- no recognized content -->
  </ItemGroup>

  <Import Project='{3}' />

  <Target Name='Build'>
    <Message Importance='high' Text='In dummy build target' />
  </Target>

</Project>
";
            var projectRoot = BuildUtilities.CreateProjectFromTemplate(projectFilePath, TestContext, projectXml,
                projectGuid.ToString(),
                rootOutputFolder,
                typeof(WriteProjectInfoFile).Assembly.Location,
                sqTargetFile,
                TargetConstants.MsTestProjectTypeGuid
                );

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath,
                TargetConstants.DefaultBuildTarget);

            // Assert
            result.BuildSucceeded.Should().BeTrue();

            result.AssertExpectedTargetOrdering(
                TargetConstants.DefaultBuildTarget,
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.WriteProjectDataTarget);

            // Check the project info
            var projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);

            Assert.IsTrue(projectInfo.IsExcluded, "Expecting the project to be marked as excluded");
            Assert.AreEqual("my.language", projectInfo.ProjectLanguage, "Unexpected project language");
            Assert.AreEqual(ProjectType.Test, projectInfo.ProjectType, "Project should be marked as a test project");
            Assert.AreEqual(0, projectInfo.AnalysisResults.Count, "Unexpected number of analysis results created");
        }

        #endregion Tests

        #region Private methods

        private string AddEmptyAnalysedCodeFile(ProjectDescriptor descriptor, string projectFolder, string extension = "cs")
        {
            var filePath = CreateEmptyFile(projectFolder, extension);
            descriptor.AddCompileInputFile(filePath, true);
            return filePath;
        }

        private static void AddEmptyContentFile(ProjectDescriptor descriptor, string projectFolder)
        {
            var filePath = CreateEmptyFile(projectFolder, "txt");
            descriptor.AddContentFile(filePath, true);
        }

        private static string CreateEmptyFile(string folder, string extension)
        {
            var emptyFilePath = Path.Combine(folder, "empty_" + Guid.NewGuid().ToString() + ".xxx");
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
            var preImportProperties = new WellKnownProjectProperties
            {
                SonarQubeTempPath = outputPath, // FIXME
                SonarQubeConfigPath = configPath,
                SonarQubeOutputPath = outputPath,

                // Ensure the project is isolated from environment variables
                // that could be picked up when running on a TeamBuild build agent
                TeamBuildLegacyBuildDirectory = "",
                TeamBuild2105BuildDirectory = ""
            };
            return preImportProperties;
        }

        /// <summary>
        /// Creates and builds a new Sonar-enabled project using the supplied descriptor.
        /// The method will check the build succeeded and that a single project output file was created.
        /// </summary>
        /// <returns>The full path of the project-specific directory that was created during the build</returns>
        private string CreateAndBuildSonarProject(ProjectDescriptor descriptor, string rootOutputFolder, WellKnownProjectProperties preImportProperties)
        {
            var projectRoot = BuildUtilities.CreateInitializedProjectRoot(TestContext, descriptor, preImportProperties);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, descriptor.FullFilePath);
            TestContext.AddResultFile(result.FilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget);

            // We expect the compiler to warn if there are no compiler inputs
            var expectedWarnings = (descriptor.ManagedSourceFiles.Any()) ? 0 : 1;
            result.AssertExpectedErrorCount(0);
            result.AssertExpectedWarningCount(expectedWarnings);

            result.AssertExpectedTargetOrdering(
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.DefaultBuildTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.WriteProjectDataTarget);

            // Check expected folder structure exists
            CheckRootOutputFolder(rootOutputFolder);

            // Check expected project outputs
            Assert.AreEqual(1, Directory.EnumerateDirectories(rootOutputFolder).Count(), "Only expecting one child directory to exist under the root analysis output folder");
            ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, descriptor.FullFilePath);

            return Directory.EnumerateDirectories(rootOutputFolder).Single();
        }

        #endregion Private methods

        #region Assertions methods

        private static void CheckRootOutputFolder(string rootOutputFolder)
        {
            Assert.IsTrue(Directory.Exists(rootOutputFolder), "Expected root output folder does not exist");

            var fileCount = Directory.GetFiles(rootOutputFolder, "*.*", SearchOption.TopDirectoryOnly).Count();
            Assert.AreEqual(0, fileCount, "Not expecting the top-level output folder to contain any files");
        }

        private void CheckProjectOutputFolder(ProjectDescriptor expected, string projectOutputFolder)
        {
            Assert.IsFalse(string.IsNullOrEmpty(projectOutputFolder), "Test error: projectOutputFolder should not be null/empty");
            Assert.IsTrue(Directory.Exists(projectOutputFolder), "Expected project folder does not exist: {0}", projectOutputFolder);

            // Check folder naming
            var folderName = Path.GetFileName(projectOutputFolder);
            Assert.IsFalse(folderName.StartsWith(expected.ProjectName), "Project output folder starts with the project name. Expected: {0}, actual: {1}",
                expected.ProjectFolderName, folderName);

            // Check specific files
            var expectedProjectInfo = CreateExpectedProjectInfo(expected, projectOutputFolder);
            CheckProjectInfo(expectedProjectInfo, projectOutputFolder);
            CheckAnalysisFileList(expected, projectOutputFolder);

            // Check there are no other files
            var allowedFiles = new List<string>(expectedProjectInfo.AnalysisResults.Select(ar => ar.Location))
            {
                Path.Combine(projectOutputFolder, FileConstants.ProjectInfoFileName)
            };
            AssertNoAdditionalFilesInFolder(projectOutputFolder, allowedFiles.ToArray());
        }

        private static IList<string> GetExpectedAnalysisFiles(ProjectDescriptor descriptor)
        {
            return descriptor.Files.Where(f => f.ShouldBeAnalysed).Select(f => f.FilePath).ToList();
        }

        private void CheckAnalysisFileList(ProjectDescriptor expected, string projectOutputFolder)
        {
            var expectedFiles = GetExpectedAnalysisFiles(expected).ToArray();

            if (!expectedFiles.Any())
            {
                AssertFileDoesNotExist(projectOutputFolder, ExpectedAnalysisFilesListFileName);
            }
            else
            {
                var fullName = AssertFileExists(projectOutputFolder, ExpectedAnalysisFilesListFileName);

                var actualFiles = File.ReadAllLines(fullName);

                // The actual files might contain extra compiler generated files, so check the expected files
                // we expected is a subset of the actual
                CollectionAssert.IsSubsetOf(expectedFiles, actualFiles, "Analysis file does not contain the expected entries");

                // Check that any files that should not be analyzed are not included
                if (expected.FilesNotToAnalyse != null && expected.FilesNotToAnalyse.Any())
                {
                    foreach (var unanalysedFile in expected.FilesNotToAnalyse)
                    {
                        var filePathToCheck = unanalysedFile;
                        if (!Path.IsPathRooted(filePathToCheck))
                        {
                            // Assume paths are relative to the project directory
                            filePathToCheck = Path.Combine(expected.FullDirectoryPath, filePathToCheck);
                        }
                        CollectionAssert.DoesNotContain(actualFiles, filePathToCheck, "Not expecting file to be included for analysis: {0}", filePathToCheck);
                    }
                }
            }
        }

        private static void AssertFileIsNotAnalysed(string analysisFileListPath, string unanalysedPath)
        {
            var actualFiles = GetAnalysedFiles(analysisFileListPath); CollectionAssert.DoesNotContain(actualFiles, unanalysedPath, "File should not be analyzed: {0}", unanalysedPath);
        }

        private static void AssertFileIsAnalysed(string analysisFileListPath, string unanalysedPath)
        {
            var actualFiles = GetAnalysedFiles(analysisFileListPath);
            CollectionAssert.Contains(actualFiles, unanalysedPath, "File should not be analyzed: {0}", unanalysedPath);
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
            var fullName = AssertFileExists(projectOutputFolder, FileConstants.ProjectInfoFileName); // should always exist

            var actualProjectInfo = ProjectInfo.Load(fullName);

            TestUtilities.ProjectInfoAssertions.AssertExpectedValues(expected, actualProjectInfo);
        }

        private static ProjectInfo CreateExpectedProjectInfo(ProjectDescriptor expected, string projectOutputFolder)
        {
            var expectedProjectInfo = expected.CreateProjectInfo();

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
            var fullPath = Path.Combine(projectOutputFolder, fileName);
            var exists = CheckExistenceAndAddToResults(fullPath);

            Assert.IsTrue(exists, "Expected file does not exist: {0}", fullPath);
            return fullPath;
        }

        private void AssertFileDoesNotExist(string projectOutputFolder, string fileName)
        {
            var fullPath = Path.Combine(projectOutputFolder, fileName);
            var exists = CheckExistenceAndAddToResults(fullPath);

            Assert.IsFalse(exists, "Not expecting file to exist: {0}", fullPath);
        }

        private bool CheckExistenceAndAddToResults(string fullPath)
        {
            var exists = File.Exists(fullPath);
            if (exists)
            {
                TestContext.AddResultFile(fullPath);
            }
            return exists;
        }

        private static void AssertNoAdditionalFilesInFolder(string folderPath, params string[] allowedFileNames)
        {
            var files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
            var additionalFiles = files.Except(allowedFileNames);

            if (additionalFiles.Any())
            {
                Console.WriteLine("Additional file(s) in folder: {0}", folderPath);
                foreach (var additionalFile in additionalFiles)
                {
                    Console.WriteLine("\t{0}", additionalFile);
                }
                Assert.Fail("Additional files exist in the project output folder: {0}", folderPath);
            }
        }

        #endregion Assertions methods
    }
}
