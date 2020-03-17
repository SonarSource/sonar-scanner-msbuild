/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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

        /// <summary>
        /// File names of all of the protobuf files created by the utility analyzers
        /// </summary>
        private readonly string[] ProtobufFileNames = { "encoding.pb", "file-metadata.pb", "metrics.pb", "symrefs.pb", "token-cpd.pb", "token-type.pb" };

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_OutputFolderStructure()
        {
            // Checks the output folder structure is correct for a simple solution

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            var codeFilePath = CreateEmptyFile(rootInputFolder, "codeFile1.txt");
            var projectXml = $@"
<ItemGroup>
  <Compile Include='{codeFilePath}' />
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(projectXml, rootOutputFolder);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget);

            result.AssertExpectedErrorCount(0);
            result.AssertExpectedWarningCount(0);

            result.AssertExpectedTargetOrdering(
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.DefaultBuildTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.WriteProjectDataTarget);

            var projectSpecificOutputDir = CheckProjectSpecificOutputStructure(rootOutputFolder);
            var actualProjectInfo = CheckProjectInfoExists(projectSpecificOutputDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        [Description("Tests that projects with missing project guids are handled correctly")]
        public void E2E_MissingProjectGuid()
        {
            // Projects with missing guids should have a warning emitted. The project info
            // should still be generated.

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // Include a compilable file to avoid warnings about no files
            var codeFilePath = CreateEmptyFile(rootInputFolder, "codeFile1.txt");
            var projectXml = $@"
<PropertyGroup>
  <ProjectGuid />
</PropertyGroup>

<ItemGroup>
  <Compile Include='{codeFilePath}' />
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(projectXml, rootOutputFolder);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings

            var projectSpecificOutputDir = CheckProjectSpecificOutputStructure(rootOutputFolder);
            var actualProjectInfo = CheckProjectInfoExists(projectSpecificOutputDir);
            actualProjectInfo.ProjectGuid.Should().Be(Guid.Empty);

            result.AssertExpectedErrorCount(0);
            result.AssertExpectedWarningCount(1);

            var warning = result.Warnings[0];
            warning.Should().Contain(projectFilePath, "Expecting the warning to contain the full path to the bad project file");
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        [Description("Tests that projects with invalid project guids are handled correctly")]
        public void E2E_InvalidGuid()
        {
            // Projects with invalid guids should have a warning emitted. The project info
            // should not be generated.

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // Include a compilable file to avoid warnings about no files
            var codeFilePath = CreateEmptyFile(rootInputFolder, "codeFile1.txt");
            var projectXml = $@"
<PropertyGroup>
  <ProjectGuid>bad guid</ProjectGuid>
</PropertyGroup>

<ItemGroup>
  <Compile Include='{codeFilePath}' />
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(projectXml, rootOutputFolder);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings

            var projectSpecificOutputDir = CheckProjectSpecificOutputStructure(rootOutputFolder);
            var actualProjectInfo = CheckProjectInfoExists(projectSpecificOutputDir);
            actualProjectInfo.ProjectGuid.Should().Be(Guid.Empty);

            result.AssertExpectedErrorCount(0);
            result.AssertExpectedWarningCount(1);

            var warning = result.Warnings[0];
            warning.Should().Contain(projectFilePath, "Expecting the warning to contain the full path to the bad project file");
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_HasAnalyzableFiles()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // Mix of analyzable and non-analyzable files
            var none1 = CreateEmptyFile(rootInputFolder, "none1.txt");
            var foo1 = CreateEmptyFile(rootInputFolder, "foo1.txt");
            var foo2 = CreateEmptyFile(rootInputFolder, "foo2.txt");
            var content1 = CreateEmptyFile(rootInputFolder, "content1.txt");
            var code1 = CreateEmptyFile(rootInputFolder, "code1.txt");
            var bar1 = CreateEmptyFile(rootInputFolder, "bar1.txt");
            var junk1 = CreateEmptyFile(rootInputFolder, "junk1.txt");
            var content2 = CreateEmptyFile(rootInputFolder, "content2.txt");

            var projectXml = $@"
<PropertyGroup>
  <ProjectGuid>4077C120-AF29-422F-8360-8D7192FA03F3</ProjectGuid>
</PropertyGroup>
<ItemGroup>
  <None Include='{none1}' />
  <Foo Include='{foo1}' />
  <Foo Include='{foo2}' />
  <Content Include='{content1}' />
  <Compile Include='{code1}' />
  <Bar Include='{bar1}' />
  <Junk Include='{junk1}' />
  <Content Include='{content2}' />
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(projectXml, rootOutputFolder);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings
            var projectSpecificOutputDir = CheckProjectSpecificOutputStructure(rootOutputFolder);

            // Check the list of files to be analyzed
            var expectedFilesToAnalyzeFilePath = AssertFileExists(projectSpecificOutputDir, ExpectedAnalysisFilesListFileName);
            var fileList = File.ReadLines(expectedFilesToAnalyzeFilePath);
            fileList.Should().BeEquivalentTo(new string []
            {
                rootInputFolder + "\\none1.txt",
                rootInputFolder + "\\content1.txt",
                rootInputFolder + "\\code1.txt",
                rootInputFolder + "\\content2.txt"
            });

            // Check the projectInfo.xml file points to the file containing the list of files to analyze
            var actualProjectInfo = CheckProjectInfoExists(projectSpecificOutputDir);

            var actualFilesToAnalyze = actualProjectInfo.AssertAnalysisResultExists("FilesToAnalyze");
            actualFilesToAnalyze.Location.Should().Be(expectedFilesToAnalyzeFilePath);

            actualProjectInfo.GetProjectGuidAsString().Should().Be("4077C120-AF29-422F-8360-8D7192FA03F3");

            AssertNoAdditionalFilesInFolder(projectSpecificOutputDir,
                ExpectedAnalysisFilesListFileName, FileConstants.ProjectInfoFileName);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_NoAnalyzableFiles()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // Only non-analyzable files
            var foo1 = CreateEmptyFile(rootInputFolder, "foo1.txt");
            var foo2 = CreateEmptyFile(rootInputFolder, "foo2.txt");
            var bar1 = CreateEmptyFile(rootInputFolder, "bar1.txt");

            var projectXml = $@"
<ItemGroup>
  <Compile Include='*.cs' />
  <Foo Include='{foo1}' />
  <Foo Include='{foo2}' />
  <Bar Include='{bar1}' />
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(projectXml, rootOutputFolder);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings
            var projectSpecificOutputDir = CheckProjectSpecificOutputStructure(rootOutputFolder);
            AssertFileDoesNotExist(projectSpecificOutputDir, ExpectedAnalysisFilesListFileName);

            // Check the projectInfo.xml does not have an analysis result
            var actualProjectInfo = CheckProjectInfoExists(projectSpecificOutputDir);
            actualProjectInfo.AssertAnalysisResultDoesNotExists("FilesToAnalyze");
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets"), TestCategory("VB")]
        public void E2E_HasManagedAndContentFiles_VB()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // Mix of analyzable and non-analyzable files
            var none1 = CreateEmptyFile(rootInputFolder, "none1.txt");
            var foo1 = CreateEmptyFile(rootInputFolder, "foo1.txt");
            var code1 = CreateEmptyFile(rootInputFolder, "code1.vb");
            var code2 = CreateEmptyFile(rootInputFolder, "code2.vb");

            var projectXml = $@"
<ItemGroup>
  <None Include='{none1}' />
  <Foo Include='{foo1}' />
  <Compile Include='{code1}' />
  <Compile Include='{code2}' />
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(projectXml, rootOutputFolder, isVB: true);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings
            var projectSpecificOutputDir = CheckProjectSpecificOutputStructure(rootOutputFolder);

            // Check the list of files to be analyzed
            var expectedFilesToAnalyzeFilePath = AssertFileExists(projectSpecificOutputDir, ExpectedAnalysisFilesListFileName);
            var fileList = File.ReadLines(expectedFilesToAnalyzeFilePath);
            fileList.Should().BeEquivalentTo(new string[]
            {
                rootInputFolder + "\\none1.txt",
                rootInputFolder + "\\code1.vb",
                rootInputFolder + "\\code2.vb"
            });

            // Check the projectInfo.xml file points to the file containing the list of files to analyze
            var actualProjectInfo = CheckProjectInfoExists(projectSpecificOutputDir);

            var actualFilesToAnalyze = actualProjectInfo.AssertAnalysisResultExists("FilesToAnalyze");
            actualFilesToAnalyze.Location.Should().Be(expectedFilesToAnalyzeFilePath);
        }
        
        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")] // SONARMSBRU-104: files under the obj folder should be excluded from analysis
        public void E2E_IntermediateOutputFilesAreExcluded()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // Add files that should be analyzed
            var nonObjFolder = Path.Combine(rootInputFolder, "foo");
            Directory.CreateDirectory(nonObjFolder);
            var compile1 = CreateEmptyFile(rootInputFolder,  "compile1.cs");
            var foo_compile2 = CreateEmptyFile(nonObjFolder, "compile2.cs");

            // Add files under the obj folder that should not be analyzed
            var objFolder = Path.Combine(rootInputFolder, "obj");
            var objSubFolder1 = Path.Combine(objFolder, "debug");
            var objSubFolder2 = Path.Combine(objFolder, "xxx"); // any folder under obj should be ignored
            Directory.CreateDirectory(objSubFolder1);
            Directory.CreateDirectory(objSubFolder2);

            // File in obj
            var objFile = CreateEmptyFile(objFolder, "objFile1.cs");

            // File in obj\debug
            var objDebugFile = CreateEmptyFile(objSubFolder1, "objDebugFile1.cs");

            // File in obj\xxx
            var objFooFile = CreateEmptyFile(objSubFolder2, "objFooFile.cs");

            var projectXml = $@"
<ItemGroup>
  <Compile Include='{compile1}' />
  <Compile Include='foo\compile2.cs' />
  <Compile Include='{objFile}' />
  <Compile Include='obj\debug\objDebugFile1.cs' />
  <Compile Include='{objFooFile}' />
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(projectXml, rootOutputFolder, isVB: true);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings
            var projectSpecificOutputDir = CheckProjectSpecificOutputStructure(rootOutputFolder);

            // Check the list of files to be analyzed
            var expectedFilesToAnalyzeFilePath = AssertFileExists(projectSpecificOutputDir, ExpectedAnalysisFilesListFileName);
            var fileList = File.ReadLines(expectedFilesToAnalyzeFilePath);
            fileList.Should().BeEquivalentTo(new string[]
            {
                rootInputFolder + "\\compile1.cs",
                rootInputFolder + "\\foo\\compile2.cs"
            });
        }

        [TestCategory("E2E"), TestCategory("Targets")] // SONARMSBRU-12: Analysis build fails if the build definition name contains brackets
        [TestMethod]
        public void E2E_UsingTaskHandlesBracketsInName()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "folder with brackets in name (SONARMSBRU-12)");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // Copy the task assembly and supporting assemblies to a folder with brackets in the name
            var taskAssemblyFilePath = typeof(WriteProjectInfoFile).Assembly.Location;
            var asmName = Path.GetFileName(taskAssemblyFilePath);
            foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(taskAssemblyFilePath), "*sonar*.dll"))
            {
                File.Copy(file, Path.Combine(rootInputFolder, Path.GetFileName(file)));
            }

            // Set the project property to use that file. To reproduce the bug, we need to have MSBuild search for
            // the assembly using "GetDirectoryNameOfFileAbove".
            var val = @"$([MSBuild]::GetDirectoryNameOfFileAbove('{0}', '{1}'))\{1}";
            val = string.Format(System.Globalization.CultureInfo.InvariantCulture, val, rootInputFolder, asmName);

            // Arrange
            var code1 = CreateEmptyFile(rootInputFolder, "code1.vb");
            var projectXml = $@"
<PropertyGroup>
  <SonarQubeBuildTasksAssemblyFile>{val}</SonarQubeBuildTasksAssemblyFile>
</PropertyGroup>

<ItemGroup>
  <Compile Include='{code1}' />
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(projectXml, rootOutputFolder, isVB: true);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings
            var projectSpecificOutputDir = CheckProjectSpecificOutputStructure(rootOutputFolder);

            // Check the list of files to be analyzed
            var expectedFilesToAnalyzeFilePath = AssertFileExists(projectSpecificOutputDir, ExpectedAnalysisFilesListFileName);
            var fileList = File.ReadLines(expectedFilesToAnalyzeFilePath);
            fileList.Should().BeEquivalentTo(rootInputFolder + "\\code1.vb");

            // Check the projectInfo.xml file points to the file containing the list of files to analyze
            var actualProjectInfo = CheckProjectInfoExists(projectSpecificOutputDir);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_ExcludedProjects()
        {
            // Project info should still be written for files with $(SonarQubeExclude) set to true
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // Mix of analyzable and non-analyzable files
            var foo1 = CreateEmptyFile(rootInputFolder, "foo1.txt");
            var code1 = CreateEmptyFile(rootInputFolder, "code1.txt");

            var projectXml = $@"
<PropertyGroup>
  <SonarQubeExclude>true</SonarQubeExclude>
</PropertyGroup>

<ItemGroup>
  <Foo Include='{foo1}' />
  <Compile Include='{code1}' />
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(projectXml, rootOutputFolder);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings
            var projectSpecificOutputDir = CheckProjectSpecificOutputStructure(rootOutputFolder);

            var actualProjectInfo = CheckProjectInfoExists(projectSpecificOutputDir);
            actualProjectInfo.IsExcluded.Should().BeTrue();

            // This list of files should also be written
            var expectedFilesToAnalyzeFilePath = AssertFileExists(projectSpecificOutputDir, ExpectedAnalysisFilesListFileName);
            var fileList = File.ReadLines(expectedFilesToAnalyzeFilePath);
            fileList.Should().BeEquivalentTo(new string[]
            {
                rootInputFolder + "\\code1.txt"
            });

            var actualFilesToAnalyze = actualProjectInfo.AssertAnalysisResultExists("FilesToAnalyze");
            actualFilesToAnalyze.Location.Should().Be(expectedFilesToAnalyzeFilePath);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_BareProject_FilesToAnalyse()
        {
            // Checks the integration targets handle non-VB/C# project types
            // that don't import the standard targets or set the expected properties
            // The project info should be created as normal and the correct files to analyze detected.

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            var projectFilePath = Path.Combine(rootInputFolder, "project.txt");
            var projectGuid = Guid.NewGuid();

            var codeFile = CreateEmptyFile(rootInputFolder, "code.cpp");
            var contentFile = CreateEmptyFile(rootInputFolder, "code.js");
            var unanalysedFile = CreateEmptyFile(rootInputFolder, "text.shouldnotbeanalysed");
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

            projectInfo.ProjectGuid.Should().Be(projectGuid, "Unexpected project guid");
            projectInfo.ProjectLanguage.Should().BeNull("Expecting the project language to be null");
            projectInfo.IsExcluded.Should().BeFalse("Project should not be marked as excluded");
            projectInfo.ProjectType.Should().Be(ProjectType.Product, "Project should be marked as a product project");
            projectInfo.AnalysisResults.Should().HaveCount(1, "Unexpected number of analysis results created");

            // Check the correct list of files to analyze were returned
            var filesToAnalyse = ProjectInfoAssertions.AssertAnalysisResultExists(projectInfo, AnalysisType.FilesToAnalyze.ToString());
            var actualFilesToAnalyse = File.ReadAllLines(filesToAnalyse.Location);
            actualFilesToAnalyse.Should().BeEquivalentTo(new string[] { codeFile, contentFile }, "Unexpected list of files to analyze");
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_BareProject_CorrectlyCategorised()
        {
            // Checks that projects that don't include the standard managed targets are still
            // processed correctly e.g. can be excluded, marked as test projects etc

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

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

            projectInfo.IsExcluded.Should().BeTrue("Expecting the project to be marked as excluded");
            projectInfo.ProjectLanguage.Should().Be("my.language", "Unexpected project language");
            projectInfo.ProjectType.Should().Be(ProjectType.Test, "Project should be marked as a test project");
            projectInfo.AnalysisResults.Should().BeEmpty("Unexpected number of analysis results created");
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_TestProjects_ProtobufsUpdated()
        {
            // Arrange and Act
            var result = Execute_E2E_TestProjects_ProtobufsUpdated(true, "subdir1");

            // Assert
            result.AssertTargetExecuted("FixUpTestProjectOutputs");

            var protobufDir = Path.Combine(result.GetCapturedPropertyValue("ProjectSpecificOutDir"), "subdir1");

            AssertFilesExistsAndAreNotEmpty(protobufDir, "encoding.pb", "file-metadata.pb", "symrefs.pb", "token-type.pb");
            AssertFilesExistsAndAreEmpty(protobufDir, "metrics.pb", "token-cpd.pb");
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_NonTestProjects_ProtobufsNotUpdated()
        {
            // Arrange and Act
            var result = Execute_E2E_TestProjects_ProtobufsUpdated(false, "subdir2");

            // Assert
            result.AssertTargetNotExecuted("FixUpTestProjectOutputs");

            var protobufDir = Path.Combine(result.GetCapturedPropertyValue("ProjectSpecificOutDir"), "subdir2");

            // Protobufs should not changed for non-test project
            AssertFilesExistsAndAreNotEmpty(protobufDir, ProtobufFileNames);
        }

        private BuildLog Execute_E2E_TestProjects_ProtobufsUpdated(bool isTestProject, string projectSpecificSubDir)
        {
            // Protobuf files containing metrics information should be created for test projects.
            // However, some of the metrics files should be empty, as should the issues report.
            // See [MMF-485] : https://jira.sonarsource.com/browse/MMF-486
            // This method creates some non-empty dummy protobuf files during a build.
            // The caller can should check that the protobufs have been updated/not-updated,
            // as expected, depending on the type of the project being built.

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            var code1 = CreateEmptyFile(rootInputFolder, "code1.cs");

            var projectXml = $@"
<PropertyGroup>
  <SonarQubeTestProject>{isTestProject.ToString()}</SonarQubeTestProject>
</PropertyGroup>
<ItemGroup>
  <Compile Include='{code1}' />
</ItemGroup>

<!-- Target to create dummy, non-empty protobuf files. We can't do this from code since the targets create
     a unique folder. We have to insert this target into the build after the unique folder has been created,
     but before the targets that modify the protobufs are executed -->
<Target Name='CreateDummyProtobufFiles' DependsOnTargets='CreateProjectSpecificDirs' BeforeTargets='OverrideRoslynCodeAnalysisProperties'>

  <Error Condition=""$(ProjectSpecificOutDir)==''"" Text='Test error: ProjectSpecificOutDir is not set' />
  <Message Text='CAPTURE___PROPERTY___ProjectSpecificOutDir___$(ProjectSpecificOutDir)' Importance='high' />

  <!-- Write the protobufs to an arbitrary subdirectory under the project-specific folder. -->
  <MakeDir Directories='$(ProjectSpecificOutDir)\{projectSpecificSubDir}' />

  <WriteLinesToFile File='$(ProjectSpecificOutDir)\{projectSpecificSubDir}\encoding.pb' Lines='XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX' />
  <WriteLinesToFile File='$(ProjectSpecificOutDir)\{projectSpecificSubDir}\file-metadata.pb' Lines='XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX' />
  <WriteLinesToFile File='$(ProjectSpecificOutDir)\{projectSpecificSubDir}\metrics.pb' Lines='XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX' />
  <WriteLinesToFile File='$(ProjectSpecificOutDir)\{projectSpecificSubDir}\symrefs.pb' Lines='XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX' />
  <WriteLinesToFile File='$(ProjectSpecificOutDir)\{projectSpecificSubDir}\token-cpd.pb' Lines='XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX' />
  <WriteLinesToFile File='$(ProjectSpecificOutDir)\{projectSpecificSubDir}\token-type.pb' Lines='XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX' />
  
</Target>
";
            var projectFilePath = CreateProjectFile(projectXml, rootOutputFolder);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings
            var projectSpecificOutputDir = CheckProjectSpecificOutputStructure(rootOutputFolder);

            // Sanity check that the above target was executed
            result.AssertTargetExecuted("CreateDummyProtobufFiles");

            var projectSpecificOutputDir2 = result.GetCapturedPropertyValue("ProjectSpecificOutDir");
            projectSpecificOutputDir2.Should().Be(projectSpecificOutputDir);

            AssertNoAdditionalFilesInFolder(projectSpecificOutputDir,
                ProtobufFileNames.Concat(new string[] { ExpectedAnalysisFilesListFileName, FileConstants.ProjectInfoFileName })
                .ToArray());

            return result;
        }

        #endregion Tests

        #region Private methods

        private static string CreateEmptyFile(string folder, string fileName)
        {
            return CreateFile(folder, fileName, string.Empty);
        }

        private static string CreateFile(string folder, string fileName, string content)
        {
            var filePath = Path.Combine(folder, fileName);
            File.WriteAllText(filePath, content);

            return filePath;
        }

        private string CreateProjectFile(string testSpecificProjectXml, string sonarQubeOutputPath, bool isVB = false)
        {
            var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var language = isVB ? "VB" : "C#";

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            File.Exists(sqTargetFile).Should().BeTrue("Test error: the SonarQube analysis targets file could not be found. Full path: {0}", sqTargetFile);
            TestContext.AddResultFile(sqTargetFile);

            var template = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='Current' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>

  <!-- Boilerplate -->
  <!-- All of these boilerplate properties can be overridden by setting the value again in the test-specific XML snippet -->
  <PropertyGroup>

    <ImportByWildcardBeforeMicrosoftCommonTargets>false</ImportByWildcardBeforeMicrosoftCommonTargets>
    <ImportByWildcardAfterMicrosoftCommonTargets>false</ImportByWildcardAfterMicrosoftCommonTargets>
    <ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets>false</ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets>
    <ImportUserLocationsByWildcardAfterMicrosoftCommonTargets>false</ImportUserLocationsByWildcardAfterMicrosoftCommonTargets>
    <OutputPath>bin\</OutputPath>
    <OutputType>library</OutputType>
    <ProjectGuid>ffdb93c0-2880-44c7-89a6-bbd4ddab034a</ProjectGuid>
    <Language>LANGUAGE</Language>
    <CodePage>65001</CodePage>
  </PropertyGroup>

  <PropertyGroup>
    <!-- Standard values that need to be set for each/most tests -->
    <SonarQubeBuildTasksAssemblyFile>SONARSCANNER_MSBUILD_TASKS_DLL</SonarQubeBuildTasksAssemblyFile>
    <SonarQubeConfigPath>PROJECT_DIRECTORY_PATH</SonarQubeConfigPath>
    <SonarQubeTempPath>PROJECT_DIRECTORY_PATH</SonarQubeTempPath>
    <SonarQubeOutputPath>SQ_OUTPUT_PATH</SonarQubeOutputPath>

    <!-- Ensure the project is isolated from environment variables that could be picked up when running on a TeamBuild build agent-->
    <TF_BUILD_BUILDDIRECTORY />
    <AGENT_BUILDDIRECTORY />
  </PropertyGroup>

  <!-- Test-specific data -->
  TEST_SPECIFIC_XML

  <!-- Standard boilerplate closing imports -->
  <Import Project='$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), SonarQube.Integration.targets))SonarQube.Integration.targets' />
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>
";
            var projectData = template.Replace("PROJECT_DIRECTORY_PATH", projectDirectory)
                .Replace("SONARSCANNER_MSBUILD_TASKS_DLL", typeof(WriteProjectInfoFile).Assembly.Location)
                .Replace("TEST_SPECIFIC_XML", testSpecificProjectXml ?? "<!-- none -->")
                .Replace("SQ_OUTPUT_PATH", sonarQubeOutputPath)
                .Replace("LANGUAGE", language);

            var projectFilePath = Path.Combine(projectDirectory, TestContext.TestName + ".proj.txt");
            File.WriteAllText(projectFilePath, projectData);
            TestContext.AddResultFile(projectFilePath);

            return projectFilePath;
        }

        #endregion Private methods

        #region Assertions methods

        private string CheckProjectSpecificOutputStructure(string rootOutputFolder)
        {
            Directory.Exists(rootOutputFolder).Should().BeTrue("Expected root output folder does not exist");

            // We've only built one project, so we only expect one directory under the root output
            Directory.EnumerateDirectories(rootOutputFolder).Should().HaveCount(1, "Only expecting one child directory to exist under the root analysis output folder");

            var fileCount = Directory.GetFiles(rootOutputFolder, "*.*", SearchOption.TopDirectoryOnly).Count();
            fileCount.Should().Be(0, "Not expecting the top-level output folder to contain any files");


            var projectSpecificOutputPath = Directory.EnumerateDirectories(rootOutputFolder).Single();

            // Check folder naming
            var folderName = Path.GetFileName(projectSpecificOutputPath);
            int.TryParse(folderName, out _).Should().BeTrue($"Expecting the folder name to be numeric: {folderName}");

            return projectSpecificOutputPath;
        }

        private ProjectInfo CheckProjectInfoExists(string projectOutputFolder)
        {
            var fullName = AssertFileExists(projectOutputFolder, FileConstants.ProjectInfoFileName); // should always exist

            return ProjectInfo.Load(fullName);
        }

        private string AssertFileExists(string projectOutputFolder, string fileName)
        {
            var fullPath = Path.Combine(projectOutputFolder, fileName);
            var exists = CheckExistenceAndAddToResults(fullPath);

            exists.Should().BeTrue("Expected file does not exist: {0}", fullPath);
            return fullPath;
        }

        private void AssertFileDoesNotExist(string projectOutputFolder, string fileName)
        {
            var fullPath = Path.Combine(projectOutputFolder, fileName);
            var exists = CheckExistenceAndAddToResults(fullPath);

            exists.Should().BeFalse("Not expecting file to exist: {0}", fullPath);
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
            var files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                .Select(f => Path.GetFileName(f));
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

        private void AssertFilesExistsAndAreNotEmpty(string directory, params string[] fileNames)
        {
            CheckFilesExistenceAndSize(false, directory, fileNames);
        }

        private void AssertFilesExistsAndAreEmpty(string directory, params string[] fileNames)
        {
            CheckFilesExistenceAndSize(true, directory, fileNames);
        }

        private void CheckFilesExistenceAndSize(bool shouldBeEmpty, string directory, params string[] fileNames)
        {
            foreach (var item in fileNames)
            {
                var fullPath = Path.Combine(directory, item);
                var fileInfo = new FileInfo(fullPath);

                fileInfo.Exists.Should().BeTrue($"file {item} should exist");

                if (shouldBeEmpty)
                {
                    fileInfo.Length.Should().Be(0, $"file {item} should be empty");
                }
                else
                {
                    fileInfo.Length.Should().BeGreaterThan(0, $"file {item} should be not empty");
                }
            }
        }

        #endregion Assertions methods
    }
}
