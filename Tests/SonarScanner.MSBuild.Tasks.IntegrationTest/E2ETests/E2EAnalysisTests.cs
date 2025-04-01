/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using System.Text;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Shim;
using TestUtilities;

namespace SonarScanner.MSBuild.Tasks.IntegrationTest.E2ETests;

[TestClass]
public class E2EAnalysisTests
{
    private const string ExpectedAnalysisFilesListFileName = "FilesToAnalyze.txt";
    private const string ExpectedProjectConfigFileName = "SonarProjectConfig.xml";
    private const string ExpectedProjectOutFolderFileName = "ProjectOutFolderPath.txt";
    private const string ExpectedIssuesFileName = "Issues.json";

    /// <summary>
    /// All the protobuf file names created by the utility analyzers.
    /// </summary>
    private readonly string[] protobufFileNames = { "encoding.pb", "file-metadata.pb", "metrics.pb", "symrefs.pb", "token-cpd.pb", "token-type.pb" };

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void E2E_OutputFolderStructure()
    {
        // Checks the output folder structure is correct for a simple solution

        // Arrange
        var context = CreateContext();

        var codeFilePath = context.CreateInputFile("codeFile1.txt");
        var projectXml = $"""
                          <ItemGroup>
                            <Compile Include='{codeFilePath}' />
                          </ItemGroup>
                          """;
        var projectFilePath = context.CreateProjectFile(projectXml);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild);
        result.AssertErrorCount(0);
        result.AssertTargetOrdering(
            TargetConstants.SonarCategoriseProject,
            TargetConstants.SonarWriteFilesToAnalyze,
            TargetConstants.DefaultBuild,
            TargetConstants.InvokeSonarWriteProjectData_NonRazorProject,
            TargetConstants.SonarWriteProjectData);

        context.ValidateAndLoadProjectStructure();
    }

    [TestMethod]
    [Description("Tests that projects with missing project guids are handled correctly")]
    public void E2E_MissingProjectGuid_ShouldGenerateRandomOne()
    {
        // Projects with missing guids should have a warning emitted. The project info
        // should still be generated.

        // Arrange
        var context = CreateContext();

        // Include a compilable file to avoid warnings about no files
        var codeFilePath = context.CreateInputFile("codeFile1.txt");
        var projectXml = $"""
                          <PropertyGroup>
                            <ProjectGuid />
                          </PropertyGroup>

                          <ItemGroup>
                            <Compile Include='{codeFilePath}' />
                          </ItemGroup>
                          """;
        var projectFilePath = context.CreateProjectFile(projectXml);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        var actualStructure = context.ValidateAndLoadProjectStructure();
        actualStructure.ProjectInfo.ProjectGuid.Should().NotBeEmpty();
        actualStructure.ProjectInfo.ProjectGuid.Should().NotBe(Guid.Empty);

        result.AssertErrorCount(0);
        result.Warnings.Should().NotContain(x => x.Contains(projectFilePath), "Expecting no warnings for bad project file.");
    }

    [TestMethod]
    [Description("Tests that projects with invalid project guids are handled correctly")]
    public void E2E_InvalidGuid()
    {
        // Projects with invalid guids should have a warning emitted. The project info should not be generated.

        // Arrange
        var context = CreateContext();

        // Include a compilable file to avoid warnings about no files
        var codeFilePath = context.CreateInputFile("codeFile1.txt");
        var projectXml = $"""
                          <PropertyGroup>
                            <ProjectGuid>bad guid</ProjectGuid>
                          </PropertyGroup>

                          <ItemGroup>
                            <Compile Include='{codeFilePath}' />
                          </ItemGroup>
                          """;
        var projectFilePath = context.CreateProjectFile(projectXml);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        var actualStructure = context.ValidateAndLoadProjectStructure();
        actualStructure.ProjectInfo.ProjectGuid.Should().Be(Guid.Empty);

        result.AssertErrorCount(0);
        result.AssertNoWarningsOrErrors();
        result.Messages.Should().Contain(x => x.Contains(projectFilePath), "Expecting the warning to contain the full path to the bad project file");
    }

    [TestMethod]
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
        var projectXml = $"""
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
                          """;
        var projectFilePath = context.CreateProjectFile(projectXml);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        // Check the ProjectInfo.xml file points to the file containing the list of files to analyze
        var actualStructure = context.ValidateAndLoadProjectStructure();

        // Check the list of files to be analyzed
        actualStructure.AssertExpectedFileList("\\none1.txt", "\\content1.txt", "\\code1.txt", "\\content2.txt");
        actualStructure.ProjectInfo.GetProjectGuidAsString().Should().Be("4077C120-AF29-422F-8360-8D7192FA03F3");

        AssertNoAdditionalFilesInFolder(actualStructure.ProjectSpecificConfigDir, ExpectedAnalysisFilesListFileName, ExpectedProjectConfigFileName, ExpectedProjectOutFolderFileName);
        AssertNoAdditionalFilesInFolder(actualStructure.ProjectSpecificOutputDir, ExpectedIssuesFileName, FileConstants.ProjectInfoFileName);
    }

    [TestMethod]
    public void E2E_NoAnalyzableFiles()
    {
        // Arrange
        var context = CreateContext();

        // Only non-analyzable files
        var foo1 = context.CreateInputFile("foo1.txt");
        var foo2 = context.CreateInputFile("foo2.txt");
        var bar1 = context.CreateInputFile("bar1.txt");
        var projectXml = $"""
                          <ItemGroup>
                            <Compile Include='*.cs' />
                            <Foo Include='{foo1}' />
                            <Foo Include='{foo2}' />
                            <Bar Include='{bar1}' />
                          </ItemGroup>
                          """;
        var projectFilePath = context.CreateProjectFile(projectXml);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings
        var actualStructure = context.ValidateAndLoadProjectStructure();
        actualStructure.AssertConfigFileDoesNotExist(ExpectedAnalysisFilesListFileName);

        // Check the projectInfo.xml does not have an analysis result
        actualStructure.ProjectInfo.AssertAnalysisResultDoesNotExists(TestUtils.FilesToAnalyze);
    }

    [TestMethod]
    public void E2E_HasManagedAndContentFiles_VB()
    {
        // Arrange
        var context = CreateContext();

        // Mix of analyzable and non-analyzable files
        var none1 = context.CreateInputFile("none1.txt");
        var foo1 = context.CreateInputFile("foo1.txt");
        var code1 = context.CreateInputFile("code1.vb");
        var code2 = context.CreateInputFile("code2.vb");

        var projectXml = $"""
                          <ItemGroup>
                            <None Include='{none1}' />
                            <Foo Include='{foo1}' />
                            <Compile Include='{code1}' />
                            <Compile Include='{code2}' />
                          </ItemGroup>
                          """;
        var projectFilePath = context.CreateProjectFile(projectXml, isVB: true);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        // Check the projectInfo.xml file points to the file containing the list of files to analyze
        var actualStructure = context.ValidateAndLoadProjectStructure();
        actualStructure.AssertExpectedFileList("\\none1.txt", "\\code1.vb", "\\code2.vb");
    }

    [TestMethod]
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

        var projectXml = $"""
                          <ItemGroup>
                            <Compile Include='{compile1}' />
                            <Compile Include='foo\compile2.cs' />
                            <Compile Include='{objFile}' />
                            <Compile Include='obj\debug\objDebugFile1.cs' />
                            <Compile Include='{objFooFile}' />
                          </ItemGroup>
                          """;
        var projectFilePath = context.CreateProjectFile(projectXml, isVB: true);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        // Check the list of files to be analyzed
        var actualStructure = context.ValidateAndLoadProjectStructure();
        actualStructure.AssertExpectedFileList("\\compile1.cs", "\\foo\\compile2.cs");
    }

    [TestMethod]
    public void E2E_UsingTaskHandlesBracketsInName() // Analysis build fails if the build definition name contains brackets
    {
        // Arrange
        var context = CreateContext("Input folder with brackets in name");

        // Copy the task assembly and supporting assemblies to a folder with brackets in the name
        var taskAssemblyFilePath = typeof(WriteProjectInfoFile).Assembly.Location;
        var asmName = Path.GetFileName(taskAssemblyFilePath);
        foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(taskAssemblyFilePath)!, "*sonar*.dll"))
        {
            File.Copy(file, Path.Combine(context.InputFolder, Path.GetFileName(file)));
        }

        // Set the project property to use that file. To reproduce the bug, we need to have MSBuild search for
        // the assembly using "GetDirectoryNameOfFileAbove".
        var val = @"$([MSBuild]::GetDirectoryNameOfFileAbove('{0}', '{1}'))\{1}";
        val = string.Format(System.Globalization.CultureInfo.InvariantCulture, val, context.InputFolder, asmName);

        // Arrange
        var code1 = context.CreateInputFile("code1.vb");
        var projectXml = $"""
                          <PropertyGroup>
                            <SonarQubeBuildTasksAssemblyFile>{val}</SonarQubeBuildTasksAssemblyFile>
                          </PropertyGroup>
                          <ItemGroup>
                            <Compile Include='{code1}' />
                          </ItemGroup>
                          """;
        var projectFilePath = context.CreateProjectFile(projectXml, isVB: true);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        // Check the list of files to be analyzed
        var actualStructure = context.ValidateAndLoadProjectStructure();
        actualStructure.AssertExpectedFileList("\\code1.vb");
    }

    [TestMethod]
    public void E2E_ExcludedProjects()
    {
        // Project info should still be written for files with $(SonarQubeExclude) set to true
        // Arrange
        var context = CreateContext();
        var userDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "User");

        // Mix of analyzable and non-analyzable files
        var foo1 = context.CreateInputFile("foo1.txt");
        var code1 = context.CreateInputFile("code1.txt");
        var projectXml = $"""
                          <PropertyGroup>
                            <SonarQubeExclude>true</SonarQubeExclude>
                            <ErrorLog>{userDir}\UserDefined.json</ErrorLog>
                          </PropertyGroup>
                          <ItemGroup>
                            <Foo Include='{foo1}' />
                            <Compile Include='{code1}' />
                          </ItemGroup>
                          """;
        var projectFilePath = context.CreateProjectFile(projectXml);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings
        // Do not override user-provided value
        File.Exists(userDir + @"\UserDefined.json").Should().BeTrue();

        var actualStructure = context.ValidateAndLoadProjectStructure(checkAndLoadConfigFile: false);
        actualStructure.ProjectInfo.IsExcluded.Should().BeTrue();
        actualStructure.AssertConfigFileDoesNotExist(ExpectedProjectConfigFileName);
        actualStructure.AssertExpectedFileList("\\code1.txt");
        actualStructure.ProjectInfo.AnalysisSettings.Should().NotContain(x => PropertiesFileGenerator.IsReportFilePaths(x.Id));
    }

    [TestMethod]
    public void E2E_TestProjects()
    {
        // Project info and config should be written for files with $(SonarQubeTestProject) set to true
        // Arrange
        var context = CreateContext();

        // Mix of analyzable and non-analyzable files
        var foo1 = context.CreateInputFile("foo1.txt");
        var code1 = context.CreateInputFile("code1.txt");
        var projectXml = $"""
                          <PropertyGroup>
                            <SonarQubeTestProject>true</SonarQubeTestProject>
                          </PropertyGroup>
                          <ItemGroup>
                            <Foo Include='{foo1}' />
                            <Compile Include='{code1}' />
                          </ItemGroup>
                          """;
        var projectFilePath = context.CreateProjectFile(projectXml);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        var actualStructure = context.ValidateAndLoadProjectStructure();
        actualStructure.ProjectInfo.ProjectType.Should().Be(ProjectType.Test);
        actualStructure.ProjectConfig.ProjectType.Should().Be(ProjectType.Test);
        actualStructure.AssertExpectedFileList("\\code1.txt");
    }

    [TestMethod]
    public void E2E_ProductProjects()
    {
        // Arrange
        var context = CreateContext();

        // Mix of analyzable and non-analyzable files
        var foo1 = context.CreateInputFile("foo1.txt");
        var code1 = context.CreateInputFile("code1.txt");
        var projectXml = $"""
                          <PropertyGroup>
                            <SonarQubeTestProject>false</SonarQubeTestProject>
                          </PropertyGroup>
                          <ItemGroup>
                            <Foo Include='{foo1}' />
                            <Compile Include='{code1}' />
                          </ItemGroup>
                          """;
        var projectFilePath = context.CreateProjectFile(projectXml);
        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        var actualStructure = context.ValidateAndLoadProjectStructure();
        actualStructure.ProjectInfo.ProjectType.Should().Be(ProjectType.Product);
        actualStructure.ProjectConfig.ProjectType.Should().Be(ProjectType.Product);
        actualStructure.AssertExpectedFileList("\\code1.txt");
    }

    [TestMethod]
    public void E2E_CustomErrorLogPath()
    {
        // Arrange
        var context = CreateContext();
        var userDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "User");
        var customErrorLog = Path.Combine(userDir, "UserDefined.json");
        var projectXml = $"""
                          <PropertyGroup>
                            <ErrorLog>{customErrorLog}</ErrorLog>
                          </PropertyGroup>
                          """;
        var projectFilePath = context.CreateProjectFile(projectXml);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        var actualStructure = context.ValidateAndLoadProjectStructure();
        actualStructure.ProjectInfo.ProjectType.Should().Be(ProjectType.Product);
        actualStructure.ProjectInfo.AnalysisSettings.Single(x => PropertiesFileGenerator.IsReportFilePaths(x.Id)).Value.Should().Be(customErrorLog);
    }

    [TestMethod]
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
        var projectXml = $"""
                          <?xml version='1.0' encoding='utf-8'?>
                          <Project ToolsVersion='12.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <PropertyGroup>
                              <ProjectGuid>{projectGuid}</ProjectGuid>
                              <SonarQubeTempPath>{context.OutputFolder}</SonarQubeTempPath>
                              <SonarQubeOutputPath>{context.OutputFolder}</SonarQubeOutputPath>
                              <SonarQubeBuildTasksAssemblyFile>{typeof(WriteProjectInfoFile).Assembly.Location}</SonarQubeBuildTasksAssemblyFile>
                            </PropertyGroup>

                            <ItemGroup>
                              <ClCompile Include='{codeFile}' />
                              <Content Include='{contentFile}' />
                              <ShouldBeIgnored Include='{unanalysedFile}' />
                              <ClCompile Include='{excludedFile}'>
                                <SonarQubeExclude>true</SonarQubeExclude>
                              </ClCompile>
                            </ItemGroup>

                            <Import Project='{sqTargetFile}' />

                            <Target Name='CoreCompile' BeforeTargets="Build">
                              <Message Importance='high' Text='In dummy core compile target' />
                            </Target>

                            <Target Name='Build'>
                              <Message Importance='high' Text='In dummy build target' />
                            </Target>
                          </Project>
                          """;
        var projectRoot = BuildUtilities.CreateProjectFromTemplate(projectFilePath, TestContext, projectXml);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath, TargetConstants.DefaultBuild);

        // Assert
        result.BuildSucceeded.Should().BeTrue();

        result.AssertTargetOrdering(
            TargetConstants.SonarCategoriseProject,
            TargetConstants.SonarWriteFilesToAnalyze,
            TargetConstants.CoreCompile,
            TargetConstants.DefaultBuild,
            TargetConstants.InvokeSonarWriteProjectData_NonRazorProject,
            TargetConstants.SonarWriteProjectData);

        // Check the content of the project info xml
        var projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(context.OutputFolder, projectRoot.FullPath);
        projectInfo.ProjectGuid.Should().Be(projectGuid, "Unexpected project guid");
        projectInfo.ProjectLanguage.Should().BeNull("Expecting the project language to be null");
        projectInfo.IsExcluded.Should().BeFalse("Project should not be marked as excluded");
        projectInfo.ProjectType.Should().Be(ProjectType.Product, "Project should be marked as a product project");
        projectInfo.AnalysisResults.Should().ContainSingle("Unexpected number of analysis results created");

        // Check the correct list of files to analyze were returned
        var filesToAnalyze = projectInfo.AssertAnalysisResultExists(AnalysisType.FilesToAnalyze.ToString());
        var actualFilesToAnalyze = File.ReadAllLines(filesToAnalyze.Location);
        actualFilesToAnalyze.Should().BeEquivalentTo([codeFile, contentFile], "Unexpected list of files to analyze");
    }

    [TestMethod]
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
        var projectXml = $"""
                          <?xml version='1.0' encoding='utf-8'?>
                          <Project ToolsVersion='12.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <PropertyGroup>
                              <SonarQubeExclude>true</SonarQubeExclude>
                              <Language>my.language</Language>
                              <ProjectTypeGuids>{TargetConstants.MsTestProjectTypeGuid}</ProjectTypeGuids>
                              <ProjectGuid>{projectGuid}</ProjectGuid>
                              <SonarQubeTempPath>{rootOutputFolder}</SonarQubeTempPath>
                              <SonarQubeOutputPath>{rootOutputFolder}</SonarQubeOutputPath>
                              <SonarQubeBuildTasksAssemblyFile>{typeof(WriteProjectInfoFile).Assembly.Location}</SonarQubeBuildTasksAssemblyFile>
                            </PropertyGroup>

                            <ItemGroup>
                              <!-- no recognized content -->
                            </ItemGroup>

                            <Import Project='{sqTargetFile}' />

                            <Target Name='CoreCompile' BeforeTargets="Build">
                              <Message Importance='high' Text='In dummy core compile target' />
                            </Target>

                            <Target Name='Build'>
                              <Message Importance='high' Text='In dummy build target' />
                            </Target>
                          </Project>
                          """;
        var projectRoot = BuildUtilities.CreateProjectFromTemplate(projectFilePath, TestContext, projectXml);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath, TargetConstants.DefaultBuild);

        // Assert
        result.BuildSucceeded.Should().BeTrue();

        result.AssertTargetOrdering(
            TargetConstants.SonarCategoriseProject,
            TargetConstants.SonarWriteFilesToAnalyze,
            TargetConstants.CoreCompile,
            TargetConstants.DefaultBuild,
            TargetConstants.InvokeSonarWriteProjectData_NonRazorProject,
            TargetConstants.SonarWriteProjectData);

        // Check the project info
        var projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);
        projectInfo.IsExcluded.Should().BeTrue("Expecting the project to be marked as excluded");
        projectInfo.ProjectLanguage.Should().Be("my.language", "Unexpected project language");
        projectInfo.ProjectType.Should().Be(ProjectType.Test, "Project should be marked as a test project");
        projectInfo.AnalysisResults.Should().BeEmpty("Unexpected number of analysis results created");
    }

    [TestMethod]
    public void E2E_RazorProjectWithoutSourceGeneration_ValidProjectInfoFilesGenerated()
    {
        // Checks that projects that don't include the standard managed targets are still
        // processed correctly e.g. can be excluded, marked as test projects etc

        // Arrange
        var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
        var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");
        var defaultProjectInfoPath = Path.Combine(rootOutputFolder, @"0\ProjectInfo.xml");
        var razorProjectInfoPath = Path.Combine(rootOutputFolder, @"0.Razor\ProjectInfo.xml");
        var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
        var projectFilePath = Path.Combine(rootInputFolder, "project.txt");
        var projectGuid = Guid.NewGuid();
        var defaultProjectOutPaths = Path.Combine(rootOutputFolder, @"0");
        var razorProjectOutPaths = Path.Combine(rootOutputFolder, @"0.Razor");
        var defaultReportFilePaths = Path.Combine(defaultProjectOutPaths, @"Issues.json");
        var razorReportFilePaths = Path.Combine(razorProjectOutPaths, @"Issues.Views.json");
        var filesToAnalyzePath = Path.Combine(rootOutputFolder, @"conf\0\FilesToAnalyze.txt");
        var projectXml = $"""
                          <?xml version='1.0' encoding='utf-8'?>
                          <Project ToolsVersion='12.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <PropertyGroup>
                              <Language>my.language</Language>
                              <ProjectGuid>{projectGuid}</ProjectGuid>
                              <SQLanguage>cs</SQLanguage>
                              <SonarQubeTempPath>{rootOutputFolder}</SonarQubeTempPath>
                              <SonarQubeOutputPath>{rootOutputFolder}</SonarQubeOutputPath>
                              <SonarQubeBuildTasksAssemblyFile>{typeof(WriteProjectInfoFile).Assembly.Location}</SonarQubeBuildTasksAssemblyFile>
                              <TargetFramework>net6</TargetFramework>
                              <RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>
                              <UseRazorSourceGenerator>false</UseRazorSourceGenerator>
                            </PropertyGroup>

                            <ItemGroup>
                              <RazorCompile Include='SomeRandomValue' />
                              <SonarQubeAnalysisFiles Include='SomeRandomFile' />
                            </ItemGroup>

                            <Import Project='{sqTargetFile}' />

                            <Target Name='CoreCompile'>
                              <Message Importance='high' Text='In dummy core compile target' />
                              <WriteLinesToFile File='$(ErrorLog)' Overwrite='true' />
                            </Target>

                            <Target Name='RazorCoreCompile' AfterTargets='CoreCompile'>
                              <Message Importance='high' Text='In dummy razor core compile target' />
                              <WriteLinesToFile File='$(RazorSonarErrorLog)' Overwrite='true' />
                            </Target>

                            <Target Name='Build' DependsOnTargets='CoreCompile;RazorCoreCompile'>
                              <Message Importance='high' Text='In dummy build target' />
                            </Target>
                          </Project>
                          """;
        var projectRoot = BuildUtilities.CreateProjectFromTemplate(projectFilePath, TestContext, projectXml);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath, TargetConstants.DefaultBuild);

        // Assert
        result.BuildSucceeded.Should().BeTrue();

        result.AssertTargetOrdering(
            TargetConstants.SonarCategoriseProject,
            TargetConstants.SonarWriteFilesToAnalyze,
            TargetConstants.CoreCompile,
            TargetConstants.InvokeSonarWriteProjectData_RazorProject,
            TargetConstants.SonarWriteProjectData,
            TargetConstants.SonarPrepareRazorProjectCodeAnalysis,
            TargetConstants.RazorCoreCompile,
            TargetConstants.SonarFinishRazorProjectCodeAnalysis,
            TargetConstants.DefaultBuild);

        // Check the project info
        File.Exists(defaultProjectInfoPath).Should().BeTrue();
        File.Exists(razorProjectInfoPath).Should().BeTrue();
        var defaultProjectInfo = ProjectInfo.Load(defaultProjectInfoPath);
        var razorProjectInfo = ProjectInfo.Load(razorProjectInfoPath);
        AssertProjectInfoContent(defaultProjectInfo, defaultReportFilePaths, defaultProjectOutPaths, filesToAnalyzePath);
        AssertProjectInfoContent(razorProjectInfo, razorReportFilePaths, razorProjectOutPaths, filesToAnalyzePath);
    }

    [TestMethod]
    public void E2E_Net6RazorProjectWithSourceGenerationEnabled_ValidProjectInfoFilesGenerated()
    {
        // Checks that projects that don't include the standard managed targets are still
        // processed correctly e.g. can be excluded, marked as test projects etc

        // Arrange
        var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
        var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");
        var defaultProjectInfoPath = Path.Combine(rootOutputFolder, @"0\ProjectInfo.xml");
        var razorProjectInfoPath = Path.Combine(rootOutputFolder, @"0.Razor\ProjectInfo.xml");
        var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
        var projectFilePath = Path.Combine(rootInputFolder, "project.txt");
        var projectGuid = Guid.NewGuid();
        var projectXml = $"""
                          <?xml version='1.0' encoding='utf-8'?>
                          <Project ToolsVersion='12.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <PropertyGroup>
                              <Language>my.language</Language>
                              <ProjectGuid>{projectGuid}</ProjectGuid>
                              <SQLanguage>cs</SQLanguage>
                              <SonarQubeTempPath>{rootOutputFolder}</SonarQubeTempPath>
                              <SonarQubeOutputPath>{rootOutputFolder}</SonarQubeOutputPath>
                              <SonarQubeBuildTasksAssemblyFile>{typeof(WriteProjectInfoFile).Assembly.Location}</SonarQubeBuildTasksAssemblyFile>
                              <TargetFramework>net6</TargetFramework>
                              <RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>
                              <UseRazorSourceGenerator>true</UseRazorSourceGenerator>
                            </PropertyGroup>

                            <ItemGroup>
                              <RazorCompile Include='SomeRandomValue' />
                              <SonarQubeAnalysisFiles Include='SomeRandomFile' />
                            </ItemGroup>

                            <Import Project='{sqTargetFile}' />

                            <Target Name='CoreCompile'>
                              <Message Importance='high' Text='In dummy core compile target' />
                              <WriteLinesToFile File='$(ErrorLog)' Overwrite='true' />
                            </Target>

                            <Target Name='Build' DependsOnTargets='CoreCompile'>
                              <Message Importance='high' Text='In dummy build target' />
                            </Target>
                          </Project>
                          """;
        var projectRoot = BuildUtilities.CreateProjectFromTemplate(projectFilePath, TestContext, projectXml);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath, TargetConstants.DefaultBuild);

        // Assert
        result.BuildSucceeded.Should().BeTrue();
        result.AssertTargetOrdering(
            TargetConstants.SonarCategoriseProject,
            TargetConstants.SonarWriteFilesToAnalyze,
            TargetConstants.CoreCompile,
            TargetConstants.DefaultBuild,
            TargetConstants.InvokeSonarWriteProjectData_NonRazorProject,
            TargetConstants.SonarWriteProjectData);

        // Check the project info
        var defaultProjectOutPaths = Path.Combine(rootOutputFolder, @"0");
        var razorProjectOutPaths = Path.Combine(rootOutputFolder, @"0.Razor");
        var defaultReportFilePaths = Path.Combine(defaultProjectOutPaths, @"Issues.json");
        var razorReportFilePaths = Path.Combine(razorProjectOutPaths, @"Issues.Views.json");
        var filesToAnalyzePath = Path.Combine(rootOutputFolder, @"conf\0\FilesToAnalyze.txt");
        File.Exists(defaultProjectInfoPath).Should().BeTrue();
        File.Exists(razorProjectInfoPath).Should().BeFalse();
        File.Exists(razorReportFilePaths).Should().BeFalse();
        var defaultProjectInfo = ProjectInfo.Load(defaultProjectInfoPath);
        AssertProjectInfoContent(defaultProjectInfo, defaultReportFilePaths, defaultProjectOutPaths, filesToAnalyzePath);
    }

    [TestMethod]
    public void E2E_TestProjects_ProtobufFilesAreUpdated()
    {
        // Arrange and Act
        var result = Execute_E2E_TestProjects_ProtobufFileNamesAreUpdated(true, "subdir1");

        // Assert
        result.AssertTargetExecuted("FixUpTestProjectOutputs");

        var protobufDir = Path.Combine(result.GetPropertyValue(TargetProperties.ProjectSpecificOutDir), "subdir1");

        AssertFilesExistsAndAreNotEmpty(protobufDir, "encoding.pb", "file-metadata.pb", "symrefs.pb", "token-type.pb");
        AssertFilesExistsAndAreEmpty(protobufDir, "metrics.pb", "token-cpd.pb");
    }

    [TestMethod]
    public void E2E_NonTestProjects_ProtobufFilesAreNotUpdated()
    {
        // Arrange and Act
        var result = Execute_E2E_TestProjects_ProtobufFileNamesAreUpdated(false, "subdir2");

        // Assert
        result.AssertTargetNotExecuted("FixUpTestProjectOutputs");

        var protobufDir = Path.Combine(result.GetPropertyValue(TargetProperties.ProjectSpecificOutDir), "subdir2");

        // Protobuf files should not change for non-test project
        AssertFilesExistsAndAreNotEmpty(protobufDir, protobufFileNames);
    }

    private BuildLog Execute_E2E_TestProjects_ProtobufFileNamesAreUpdated(bool isTestProject, string projectSpecificSubDir)
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
        var projectXml = $"""
                          <PropertyGroup>
                            <SonarQubeTestProject>{isTestProject.ToString()}</SonarQubeTestProject>
                          </PropertyGroup>
                          <ItemGroup>
                            <Compile Include='{code1}' />
                          </ItemGroup>

                          <!-- Target to create dummy, non-empty protobuf files. We can't do this from code since the targets create
                               a unique folder. We have to insert this target into the build after the unique folder has been created,
                               but before the targets that modify the protobufs are executed -->
                          <Target Name='CreateDummyProtobufFiles' DependsOnTargets='SonarCreateProjectSpecificDirs' BeforeTargets='OverrideRoslynCodeAnalysisProperties'>
                            <Error Condition="$(ProjectSpecificOutDir)==''" Text='Test error: ProjectSpecificOutDir is not set' />
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
                          """;
        var projectFilePath = context.CreateProjectFile(projectXml);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings
        var actualStructure = context.ValidateAndLoadProjectStructure();

        // Sanity check that the above target was executed
        result.AssertTargetExecuted("CreateDummyProtobufFiles");

        var projectSpecificOutputDir2 = result.GetPropertyValue(TargetProperties.ProjectSpecificOutDir);
        projectSpecificOutputDir2.Should().Be(actualStructure.ProjectSpecificOutputDir);

        AssertNoAdditionalFilesInFolder(
            actualStructure.ProjectSpecificOutputDir,
            protobufFileNames.Concat([ExpectedAnalysisFilesListFileName, ExpectedIssuesFileName, FileConstants.ProjectInfoFileName]).ToArray());
        return result;
    }

    private Context CreateContext(string inputFolderName = "Inputs") =>
        new(TestContext, inputFolderName);

    private static void AssertNoAdditionalFilesInFolder(string folderPath, params string[] allowedFileNames)
    {
        var files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName);
        var additionalFiles = files.Except(allowedFileNames).ToArray();
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

    private static void AssertFilesExistsAndAreNotEmpty(string directory, params string[] fileNames) =>
        CheckFilesExistenceAndSize(false, directory, fileNames);

    private static void AssertFilesExistsAndAreEmpty(string directory, params string[] fileNames) =>
        CheckFilesExistenceAndSize(true, directory, fileNames);

    private static void CheckFilesExistenceAndSize(bool shouldBeEmpty, string directory, params string[] fileNames)
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

    private static void AssertProjectInfoContent(ProjectInfo projectInfo, string expectedReportFilePaths, string expectedProjectOutPaths, string expectedFilesToAnalyzePath)
    {
        projectInfo.ProjectLanguage.Should().Be("my.language", "Unexpected project language");
        projectInfo.ProjectType.Should().Be(ProjectType.Product, "Project should be marked as a product project");
        projectInfo.AnalysisResults.Single(x => x.Id.Equals(TestUtils.FilesToAnalyze)).Location.Should().Be(expectedFilesToAnalyzePath);
        projectInfo.AnalysisSettings.Single(x => x.Id.Equals("sonar.cs.roslyn.reportFilePaths")).Value.Should().Be(expectedReportFilePaths);
        projectInfo.AnalysisSettings.Single(x => x.Id.Equals("sonar.cs.analyzer.projectOutPaths")).Value.Should().Be(expectedProjectOutPaths);
    }

    private class Context(TestContext testContext, string inputFolderName)
    {
        public readonly string InputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext, inputFolderName);
        public readonly string ConfigFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext, "Config");
        public readonly string OutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext, "Outputs");
        public readonly TestContext TestContext = testContext;

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
            var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var language = isVB ? "VB" : "C#";
            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            File.Exists(sqTargetFile).Should().BeTrue("Test error: the SonarQube analysis targets file could not be found. Full path: {0}", sqTargetFile);
            TestContext.AddResultFile(sqTargetFile);

            const string template = """
                                    <?xml version='1.0' encoding='utf-8'?>
                                    <Project ToolsVersion='Current' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
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
                                        <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
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

                                    """;
            var projectData = template.Replace("PROJECT_DIRECTORY_PATH", projectDirectory)
                .Replace("SONARSCANNER_MSBUILD_TASKS_DLL", typeof(WriteProjectInfoFile).Assembly.Location)
                .Replace("TEST_SPECIFIC_XML", testSpecificProjectXml ?? "<!-- none -->")
                .Replace("SQ_OUTPUT_PATH", OutputFolder)
                .Replace("SQ_CONFIG_PATH", ConfigFolder)
                .Replace("LANGUAGE", language);

            var projectFilePath = Path.Combine(projectDirectory, TestContext.TestName + ".proj.txt");
            File.WriteAllText(projectFilePath, projectData);
            TestContext.AddResultFile(projectFilePath);

            return projectFilePath;
        }

        public ProjectStructure ValidateAndLoadProjectStructure(bool checkAndLoadConfigFile = true)
        {
            var projectSpecificConfigDir = FindAndValidateProjectSpecificDirectory(ConfigFolder, "config");
            var projectSpecificOutputDir = FindAndValidateProjectSpecificDirectory(OutputFolder, "output");
            return new ProjectStructure(this, projectSpecificConfigDir, projectSpecificOutputDir, checkAndLoadConfigFile);
        }

        private static string FindAndValidateProjectSpecificDirectory(string rootFolder, string logType)
        {
            Directory.Exists(rootFolder).Should().BeTrue($"Expected root {logType} folder does not exist");

            // We've only built one project, so we only expect one directory under the root
            Directory.EnumerateDirectories(rootFolder).Should().ContainSingle($"Only expecting one child directory to exist under the root analysis {logType} folder");

            var fileCount = Directory.GetFiles(rootFolder, "*.*", SearchOption.TopDirectoryOnly).Count();
            fileCount.Should().Be(0, $"Not expecting the top-level {logType} folder to contain any files");

            var projectSpecificPath = Directory.EnumerateDirectories(rootFolder).Single();

            // Check folder naming
            var folderName = Path.GetFileName(projectSpecificPath);
            int.TryParse(folderName, out _).Should().BeTrue($"Expecting the folder name to be numeric: {folderName}");

            return projectSpecificPath;
        }
    }

    private class ProjectStructure
    {
        public readonly string ProjectSpecificConfigDir;
        public readonly string ProjectSpecificOutputDir;
        public readonly ProjectConfig ProjectConfig;
        public readonly ProjectInfo ProjectInfo;
        private readonly Context context;

        public ProjectStructure(Context context, string projectSpecificConfigDir, string projectSpecificOutputDir, bool checkAndLoadConfigFile)
        {
            this.context = context;
            ProjectSpecificConfigDir = projectSpecificConfigDir;
            ProjectSpecificOutputDir = projectSpecificOutputDir;

            // ProjectInfo.xml should always exist (even for excluded projects) to provide file list for sonar-project.properties
            var projectInfoFile = TryAddToResults(projectSpecificOutputDir, FileConstants.ProjectInfoFileName);
            AssertFileExists(projectInfoFile);
            ProjectInfo = ProjectInfo.Load(projectInfoFile.FullPath);

            if (checkAndLoadConfigFile)
            {
                var projectConfigFile = TryAddToResults(projectSpecificConfigDir, ExpectedProjectConfigFileName);
                AssertFileExists(projectConfigFile);
                ProjectConfig = ProjectConfig.Load(projectConfigFile.FullPath);
            }
        }

        public void AssertConfigFileDoesNotExist(string fileName)
        {
            var file = TryAddToResults(ProjectSpecificConfigDir, fileName);
            file.Exists.Should().BeFalse("Not expecting file to exist: {0}", file.FullPath);
        }

        public void AssertExpectedFileList(params string[] fileNames)
        {
            var filesToAnalyzeFile = TryAddToResults(ProjectSpecificConfigDir, ExpectedAnalysisFilesListFileName);
            AssertFileExists(filesToAnalyzeFile);

            var expectedFullPaths = fileNames.Select(x => context.InputFolder + x);
            File.ReadLines(filesToAnalyzeFile.FullPath).Should().BeEquivalentTo(expectedFullPaths);

            var actualFilesToAnalyze = ProjectInfo.AssertAnalysisResultExists(TestUtils.FilesToAnalyze);
            actualFilesToAnalyze.Location.Should().Be(filesToAnalyzeFile.FullPath);

            AssertFileIsUtf8Bom(filesToAnalyzeFile.FullPath);

            if (ProjectConfig is not null)
            {
                ProjectConfig.FilesToAnalyzePath.Should().Be(filesToAnalyzeFile.FullPath);
            }
        }

        private static void AssertFileIsUtf8Bom(string filePath)
        {
            using var filesToAnalyzeStream = File.Open(filePath, FileMode.Open);
            var buffer = new byte[3];
            filesToAnalyzeStream.Read(buffer, 0, buffer.Length).Should().Be(3);
            buffer.Should().Equal(Encoding.UTF8.GetPreamble());
        }

        private static void AssertFileExists((string FullPath, bool Exists) file) =>
            file.Exists.Should().BeTrue("Expected file does not exist: {0}", file.FullPath);

        private (string FullPath, bool Exists) TryAddToResults(string folder, string fileName)
        {
            var fullPath = Path.Combine(folder, fileName);
            var exists = File.Exists(fullPath);
            if (exists)
            {
                context.TestContext.AddResultFile(fullPath);
            }
            return (fullPath, exists);
        }
    }
}
