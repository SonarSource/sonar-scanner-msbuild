/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
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
        private const string ExpectedProjectOutFolderFileName = "ProjectOutFolderPath.txt";

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
            var context = CreateContext();

            var codeFilePath = context.CreateInputFile("codeFile1.txt");
            var projectXml = $@"
<ItemGroup>
  <Compile Include='{codeFilePath}' />
</ItemGroup>
";
            var projectFilePath = context.CreateProjectFile(projectXml);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget);

            result.AssertExpectedErrorCount(0);
            result.AssertExpectedWarningCount(0);

            result.AssertExpectedTargetOrdering(
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.WriteFilesToAnalyzeTarget,
                TargetConstants.DefaultBuildTarget,
                TargetConstants.WriteProjectDataTarget);

            context.CheckProjectSpecificStructure();
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        [Description("Tests that projects with missing project guids are handled correctly")]
        public void E2E_MissingProjectGuid_ShouldGenerateRandomOne()
        {
            // Projects with missing guids should have a warning emitted. The project info
            // should still be generated.

            // Arrange
            var context = CreateContext();

            // Include a compilable file to avoid warnings about no files
            var codeFilePath = context.CreateInputFile("codeFile1.txt");
            var projectXml = $@"
<PropertyGroup>
  <ProjectGuid />
</PropertyGroup>

<ItemGroup>
  <Compile Include='{codeFilePath}' />
</ItemGroup>
";
            var projectFilePath = context.CreateProjectFile(projectXml);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings

            var actualStructure = context.CheckProjectSpecificStructure();
            actualStructure.ProjectInfo.ProjectGuid.Should().NotBeEmpty();
            actualStructure.ProjectInfo.ProjectGuid.Should().NotBe(Guid.Empty);

            result.AssertNoWarningsOrErrors();
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        [Description("Tests that projects with invalid project guids are handled correctly")]
        public void E2E_InvalidGuid()
        {
            // Projects with invalid guids should have a warning emitted. The project info
            // should not be generated.

            // Arrange
            var context = CreateContext();

            // Include a compilable file to avoid warnings about no files
            var codeFilePath = context.CreateInputFile("codeFile1.txt");
            var projectXml = $@"
<PropertyGroup>
  <ProjectGuid>bad guid</ProjectGuid>
</PropertyGroup>

<ItemGroup>
  <Compile Include='{codeFilePath}' />
</ItemGroup>
";
            var projectFilePath = context.CreateProjectFile(projectXml);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings

            var actualStructure = context.CheckProjectSpecificStructure();
            actualStructure.ProjectInfo.ProjectGuid.Should().Be(Guid.Empty);

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
            var context = CreateContext();

            // Mix of analyzable and non-analyzable files
            var none1 = context.CreateInputFile("none1.txt");
            var foo1 = context.CreateInputFile("foo1.txt");
            var foo2 = context.CreateInputFile("foo2.txt");
            var content1 = context.CreateInputFile("content1.txt");
            var code1 = context.CreateInputFile("code1.txt");
            var bar1 = context.CreateInputFile("bar1.txt");
            var junk1 = context.CreateInputFile("junk1.txt");
            var content2 = context.CreateInputFile("content2.txt");

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
            var projectFilePath = context.CreateProjectFile(projectXml);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings

            // Check the ProjectInfo.xml file points to the file containing the list of files to analyze
            var actualStructure = context.CheckProjectSpecificStructure();

            // Check the list of files to be analyzed
            var expectedFilesToAnalyzeFilePath = actualStructure.CheckExpectedFileList("\\none1.txt", "\\content1.txt", "\\code1.txt", "\\content2.txt");
            var actualFilesToAnalyze = actualStructure.ProjectInfo.AssertAnalysisResultExists("FilesToAnalyze");
            actualFilesToAnalyze.Location.Should().Be(expectedFilesToAnalyzeFilePath);
            actualStructure.ProjectInfo.GetProjectGuidAsString().Should().Be("4077C120-AF29-422F-8360-8D7192FA03F3");

            AssertNoAdditionalFilesInFolder(actualStructure.ProjectSpecificConfigDir, ExpectedAnalysisFilesListFileName, ExpectedProjectOutFolderFileName);
            AssertNoAdditionalFilesInFolder(actualStructure.ProjectSpecificOutputDir, FileConstants.ProjectInfoFileName);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_NoAnalyzableFiles()
        {
            // Arrange
            var context = CreateContext();

            // Only non-analyzable files
            var foo1 = context.CreateInputFile("foo1.txt");
            var foo2 = context.CreateInputFile("foo2.txt");
            var bar1 = context.CreateInputFile("bar1.txt");

            var projectXml = $@"
<ItemGroup>
  <Compile Include='*.cs' />
  <Foo Include='{foo1}' />
  <Foo Include='{foo2}' />
  <Bar Include='{bar1}' />
</ItemGroup>
";
            var projectFilePath = context.CreateProjectFile(projectXml);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings
            var actualStructure = context.CheckProjectSpecificStructure();
            context.AssertConfigFileDoesNotExist(ExpectedAnalysisFilesListFileName);

            // Check the projectInfo.xml does not have an analysis result
            actualStructure.ProjectInfo.AssertAnalysisResultDoesNotExists("FilesToAnalyze");
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets"), TestCategory("VB")]
        public void E2E_HasManagedAndContentFiles_VB()
        {
            // Arrange
            var context = CreateContext();

            // Mix of analyzable and non-analyzable files
            var none1 = context.CreateInputFile("none1.txt");
            var foo1 = context.CreateInputFile("foo1.txt");
            var code1 = context.CreateInputFile("code1.vb");
            var code2 = context.CreateInputFile("code2.vb");

            var projectXml = $@"
<ItemGroup>
  <None Include='{none1}' />
  <Foo Include='{foo1}' />
  <Compile Include='{code1}' />
  <Compile Include='{code2}' />
</ItemGroup>
";
            var projectFilePath = context.CreateProjectFile(projectXml, isVB: true);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings

            // Check the projectInfo.xml file points to the file containing the list of files to analyze
            var actualStructure = context.CheckProjectSpecificStructure();
            var expectedFilesToAnalyzeFilePath = actualStructure.CheckExpectedFileList("\\none1.txt", "\\code1.vb", "\\code2.vb");
            var actualFilesToAnalyze = actualStructure.ProjectInfo.AssertAnalysisResultExists("FilesToAnalyze");
            actualFilesToAnalyze.Location.Should().Be(expectedFilesToAnalyzeFilePath);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")] // SONARMSBRU-104: files under the obj folder should be excluded from analysis
        public void E2E_IntermediateOutputFilesAreExcluded()
        {
            // Arrange
            var context = CreateContext(string.Empty);

            // Add files that should be analyzed
            var nonObjFolder = Path.Combine(context.InputFolder, "foo");
            Directory.CreateDirectory(nonObjFolder);
            var compile1 = context.CreateInputFile("compile1.cs");
            context.CreateEmptyFile(nonObjFolder, "compile2.cs");

            // Add files under the obj folder that should not be analyzed
            var objFolder = Path.Combine(context.InputFolder, "obj");
            var objSubFolder1 = Path.Combine(objFolder, "debug");
            var objSubFolder2 = Path.Combine(objFolder, "xxx"); // any folder under obj should be ignored
            Directory.CreateDirectory(objSubFolder1);
            Directory.CreateDirectory(objSubFolder2);

            // File in obj
            var objFile = context.CreateEmptyFile(objFolder, "objFile1.cs");

            // File in obj\debug
            context.CreateEmptyFile(objSubFolder1, "objDebugFile1.cs");

            // File in obj\xxx
            var objFooFile = context.CreateEmptyFile(objSubFolder2, "objFooFile.cs");

            var projectXml = $@"
<ItemGroup>
  <Compile Include='{compile1}' />
  <Compile Include='foo\compile2.cs' />
  <Compile Include='{objFile}' />
  <Compile Include='obj\debug\objDebugFile1.cs' />
  <Compile Include='{objFooFile}' />
</ItemGroup>
";
            var projectFilePath = context.CreateProjectFile(projectXml, isVB: true);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings

            // Check the list of files to be analyzed
            var actualStructure = context.CheckProjectSpecificStructure();
            actualStructure.CheckExpectedFileList("\\compile1.cs", "\\foo\\compile2.cs");
        }

        [TestCategory("E2E"), TestCategory("Targets")] // SONARMSBRU-12: Analysis build fails if the build definition name contains brackets
        [TestMethod]
        public void E2E_UsingTaskHandlesBracketsInName()
        {
            // Arrange
            var context = CreateContext("Input folder with brackets in name (SONARMSBRU-12)");

            // Copy the task assembly and supporting assemblies to a folder with brackets in the name
            var taskAssemblyFilePath = typeof(WriteProjectInfoFile).Assembly.Location;
            var asmName = Path.GetFileName(taskAssemblyFilePath);
            foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(taskAssemblyFilePath), "*sonar*.dll"))
            {
                File.Copy(file, Path.Combine(context.InputFolder, Path.GetFileName(file)));
            }

            // Set the project property to use that file. To reproduce the bug, we need to have MSBuild search for
            // the assembly using "GetDirectoryNameOfFileAbove".
            var val = @"$([MSBuild]::GetDirectoryNameOfFileAbove('{0}', '{1}'))\{1}";
            val = string.Format(System.Globalization.CultureInfo.InvariantCulture, val, context.InputFolder, asmName);

            // Arrange
            var code1 = context.CreateInputFile("code1.vb");
            var projectXml = $@"
<PropertyGroup>
  <SonarQubeBuildTasksAssemblyFile>{val}</SonarQubeBuildTasksAssemblyFile>
</PropertyGroup>

<ItemGroup>
  <Compile Include='{code1}' />
</ItemGroup>
";
            var projectFilePath = context.CreateProjectFile(projectXml, isVB: true);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings

            // Check the list of files to be analyzed
            var actualStructure = context.CheckProjectSpecificStructure();
            actualStructure.CheckExpectedFileList("\\code1.vb");
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_ExcludedProjects()
        {
            // Project info should still be written for files with $(SonarQubeExclude) set to true
            // Arrange
            var context = CreateContext();

            // Mix of analyzable and non-analyzable files
            var foo1 = context.CreateInputFile("foo1.txt");
            var code1 = context.CreateInputFile("code1.txt");

            var projectXml = $@"
<PropertyGroup>
  <SonarQubeExclude>true</SonarQubeExclude>
</PropertyGroup>

<ItemGroup>
  <Foo Include='{foo1}' />
  <Compile Include='{code1}' />
</ItemGroup>
";
            var projectFilePath = context.CreateProjectFile(projectXml);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings

            var actualStructure = context.CheckProjectSpecificStructure();
            actualStructure.ProjectInfo.IsExcluded.Should().BeTrue();

            // This list of files should also be written
            var expectedFilesToAnalyzeFilePath = actualStructure.CheckExpectedFileList("\\code1.txt");
            var actualFilesToAnalyze = actualStructure.ProjectInfo.AssertAnalysisResultExists("FilesToAnalyze");
            actualFilesToAnalyze.Location.Should().Be(expectedFilesToAnalyzeFilePath);
        }

        [TestMethod]
        [TestCategory("E2E"), TestCategory("Targets")]
        public void E2E_BareProject_FilesToAnalyze()
        {
            // Checks the integration targets handle non-VB/C# project types
            // that don't import the standard targets or set the expected properties
            // The project info should be created as normal and the correct files to analyze detected.

            // Arrange
            var context = CreateContext();

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            var projectFilePath = Path.Combine(context.InputFolder, "project.txt");
            var projectGuid = Guid.NewGuid();

            var codeFile = context.CreateInputFile("code.cpp");
            var contentFile = context.CreateInputFile("code.js");
            var unanalysedFile = context.CreateInputFile("text.shouldnotbeanalysed");
            var excludedFile = context.CreateInputFile("excluded.cpp");

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
                context.OutputFolder,
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
                TargetConstants.WriteFilesToAnalyzeTarget,
                TargetConstants.WriteProjectDataTarget);

            // Check the content of the project info xml
            var projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(context.OutputFolder, projectRoot.FullPath);

            projectInfo.ProjectGuid.Should().Be(projectGuid, "Unexpected project guid");
            projectInfo.ProjectLanguage.Should().BeNull("Expecting the project language to be null");
            projectInfo.IsExcluded.Should().BeFalse("Project should not be marked as excluded");
            projectInfo.ProjectType.Should().Be(ProjectType.Product, "Project should be marked as a product project");
            projectInfo.AnalysisResults.Should().HaveCount(1, "Unexpected number of analysis results created");

            // Check the correct list of files to analyze were returned
            var filesToAnalyze = ProjectInfoAssertions.AssertAnalysisResultExists(projectInfo, AnalysisType.FilesToAnalyze.ToString());
            var actualFilesToAnalyze = File.ReadAllLines(filesToAnalyze.Location);
            actualFilesToAnalyze.Should().BeEquivalentTo(new string[] { codeFile, contentFile }, "Unexpected list of files to analyze");
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
                TargetConstants.WriteFilesToAnalyzeTarget,
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
            var context = CreateContext();
            var code1 = context.CreateInputFile("code1.cs");
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
            var projectFilePath = context.CreateProjectFile(projectXml);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget); // Build should succeed with warnings
            var actualStructure = context.CheckProjectSpecificStructure();

            // Sanity check that the above target was executed
            result.AssertTargetExecuted("CreateDummyProtobufFiles");

            var projectSpecificOutputDir2 = result.GetCapturedPropertyValue("ProjectSpecificOutDir");
            projectSpecificOutputDir2.Should().Be(actualStructure.ProjectSpecificOutputDir);

            AssertNoAdditionalFilesInFolder(actualStructure.ProjectSpecificOutputDir, ProtobufFileNames.Concat(new[] { ExpectedAnalysisFilesListFileName, FileConstants.ProjectInfoFileName }).ToArray());
            return result;
        }

        #endregion Tests

        private Context CreateContext(string inputFolderName = "Inputs") =>
            new Context(TestContext, inputFolderName);

        #region Assertions methods

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

        private class Context
        {
            public readonly string InputFolder;
            public readonly string ConfigFolder;
            public readonly string OutputFolder;
            private readonly TestContext testContext;

            public Context(TestContext testContext, string inputFolderName)
            {
                this.testContext = testContext;
                InputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext, inputFolderName);
                ConfigFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext, "Config");
                OutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext, "Outputs");
            }

            public string CreateInputFile(string fileName) =>
                CreateEmptyFile(InputFolder, fileName);

            public string CreateEmptyFile(string folder, string fileName)
            {
                var filePath = Path.Combine(folder, fileName);
                File.WriteAllText(filePath, null);
                return filePath;
            }

            public string CreateProjectFile(string testSpecificProjectXml, bool isVB = false)
            {
                var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
                var language = isVB ? "VB" : "C#";
                var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(testContext);
                File.Exists(sqTargetFile).Should().BeTrue("Test error: the SonarQube analysis targets file could not be found. Full path: {0}", sqTargetFile);
                testContext.AddResultFile(sqTargetFile);

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
    <SonarQubeConfigPath>SQ_CONFIG_PATH</SonarQubeConfigPath>

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
                    .Replace("SQ_OUTPUT_PATH", OutputFolder)
                    .Replace("SQ_CONFIG_PATH", ConfigFolder)
                    .Replace("LANGUAGE", language);

                var projectFilePath = Path.Combine(projectDirectory, testContext.TestName + ".proj.txt");
                File.WriteAllText(projectFilePath, projectData);
                testContext.AddResultFile(projectFilePath);

                return projectFilePath;
            }

            public ProjectStructure CheckProjectSpecificStructure()
            {
                var projectSpecificConfigDir = CheckProjectSpecificDirectoryStructure(ConfigFolder, "config");
                var projectSpecificOutputDir = CheckProjectSpecificDirectoryStructure(OutputFolder, "output");
                var projectInfo = CheckProjectInfoExists(projectSpecificOutputDir);
                return new ProjectStructure(this, projectSpecificConfigDir, projectSpecificOutputDir, projectInfo);
            }

            public void AssertConfigFileDoesNotExist(string fileName)
            {
                var fullPath = Path.Combine(ConfigFolder, fileName);
                var exists = CheckExistenceAndAddToResults(fullPath);
                exists.Should().BeFalse("Not expecting file to exist: {0}", fullPath);
            }

            public string AssertFileExists(string folder, string fileName)
            {
                var fullPath = Path.Combine(folder, fileName);
                var exists = CheckExistenceAndAddToResults(fullPath);

                exists.Should().BeTrue("Expected file does not exist: {0}", fullPath);
                return fullPath;
            }

            private ProjectInfo CheckProjectInfoExists(string projectSpecificOutputDir)
            {
                var fullName = AssertFileExists(projectSpecificOutputDir, FileConstants.ProjectInfoFileName); // should always exist
                return ProjectInfo.Load(fullName);
            }

            private string CheckProjectSpecificDirectoryStructure(string rootFolder, string logType)
            {
                Directory.Exists(rootFolder).Should().BeTrue($"Expected root {logType} folder does not exist");

                // We've only built one project, so we only expect one directory under the root
                Directory.EnumerateDirectories(rootFolder).Should().HaveCount(1, $"Only expecting one child directory to exist under the root analysis {logType} folder");

                var fileCount = Directory.GetFiles(rootFolder, "*.*", SearchOption.TopDirectoryOnly).Count();
                fileCount.Should().Be(0, $"Not expecting the top-level {logType} folder to contain any files");

                var projectSpecificPath = Directory.EnumerateDirectories(rootFolder).Single();

                // Check folder naming
                var folderName = Path.GetFileName(projectSpecificPath);
                int.TryParse(folderName, out _).Should().BeTrue($"Expecting the folder name to be numeric: {folderName}");

                return projectSpecificPath;
            }

            private bool CheckExistenceAndAddToResults(string fullPath)
            {
                var exists = File.Exists(fullPath);
                if (exists)
                {
                    testContext.AddResultFile(fullPath);
                }
                return exists;
            }
        }

        private class ProjectStructure
        {
            public readonly string ProjectSpecificConfigDir;
            public readonly string ProjectSpecificOutputDir;
            public readonly ProjectInfo ProjectInfo;
            private readonly Context context;

            public ProjectStructure(Context context, string projectSpecificConfigDir, string projectSpecificOutputDir, ProjectInfo projectInfo)
            {
                this.context = context;
                ProjectSpecificConfigDir = projectSpecificConfigDir;
                ProjectSpecificOutputDir = projectSpecificOutputDir;
                ProjectInfo = projectInfo;
            }

            public string CheckExpectedFileList(params string[] fileNames)
            {
                var expectedFilesToAnalyzeFilePath = context.AssertFileExists(ProjectSpecificConfigDir, ExpectedAnalysisFilesListFileName);
                var expectedFullPaths = fileNames.Select(x => context.InputFolder + x);
                File.ReadLines(expectedFilesToAnalyzeFilePath).Should().BeEquivalentTo(expectedFullPaths);
                return expectedFilesToAnalyzeFilePath;
            }
        }
    }
}
