/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

using SonarScanner.MSBuild.Shim;
using SonarScanner.MSBuild.Tasks.IntegrationTest.TargetsTests;

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

    // Checks the output folder structure is correct for a simple solution
    [TestMethod]
    public void E2E_OutputFolderStructure()
    {
        var context = CreateContext();

        var codeFilePath = context.CreateInputFile("codeFile1.txt");
        var projectXml = $"""
            <ItemGroup>
              <Compile Include='{codeFilePath}' />
            </ItemGroup>
            """;
        var projectFilePath = context.CreateProjectFile(projectXml);

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild);
        result.AssertErrorCount(0);
        result.AssertTargetOrdering(
            TargetConstants.SonarCategoriseProject,
            TargetConstants.SonarWriteFilesToAnalyze,
            TargetConstants.DefaultBuild,
            TargetConstants.InvokeSonarWriteProjectData_NonRazorProject,
            TargetConstants.SonarWriteProjectData);

        ValidateAndLoadProjectStructure(context);
    }

    // Projects with missing guids should have a warning emitted. The project info
    // should still be generated.
    [TestMethod]
    [Description("Tests that projects with missing project guids are handled correctly")]
    public void E2E_MissingProjectGuid_ShouldGenerateRandomOne()
    {
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

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        var actualStructure = ValidateAndLoadProjectStructure(context);
        actualStructure.ProjectInfo.ProjectGuid.Should().NotBeEmpty();
        actualStructure.ProjectInfo.ProjectGuid.Should().NotBe(Guid.Empty);

        result.AssertErrorCount(0);
        result.Warnings.Should().NotContain(x => x.Contains(projectFilePath), "Expecting no warnings for bad project file.");
    }

    // Projects with invalid guids should have a warning emitted. The project info should not be generated.
    [TestMethod]
    [Description("Tests that projects with invalid project guids are handled correctly")]
    public void E2E_InvalidGuid()
    {
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

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        var actualStructure = ValidateAndLoadProjectStructure(context);
        actualStructure.ProjectInfo.ProjectGuid.Should().Be(Guid.Empty);

        result.AssertErrorCount(0);
        result.AssertNoWarningsOrErrors();
        result.Messages.Should().Contain(x => x.Contains(projectFilePath), "Expecting the warning to contain the full path to the bad project file");
    }

    [TestMethod]
    public void E2E_HasAnalyzableFiles()
    {
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

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        // Check the ProjectInfo.xml file points to the file containing the list of files to analyze
        var actualStructure = ValidateAndLoadProjectStructure(context);

        // Check the list of files to be analyzed
        actualStructure.AssertExpectedFileList("none1.txt", "content1.txt", "code1.txt", "content2.txt");
        actualStructure.ProjectInfo.ProjectGuidAsString().Should().Be("4077C120-AF29-422F-8360-8D7192FA03F3");

        AssertNoAdditionalFilesInFolder(actualStructure.ProjectSpecificConfigDir, ExpectedAnalysisFilesListFileName, ExpectedProjectConfigFileName, ExpectedProjectOutFolderFileName);
        AssertNoAdditionalFilesInFolder(actualStructure.ProjectSpecificOutputDir, ExpectedIssuesFileName, FileConstants.ProjectInfoFileName, FileConstants.TelemetryProjectFileName);
    }

    [TestMethod]
    public void E2E_NoAnalyzableFiles()
    {
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

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings
        var actualStructure = ValidateAndLoadProjectStructure(context);
        actualStructure.AssertConfigFileDoesNotExist(ExpectedAnalysisFilesListFileName);

        // Check the projectInfo.xml does not have an analysis result
        actualStructure.ProjectInfo.AssertAnalysisResultDoesNotExists(AnalysisResultFileType.FilesToAnalyze.ToString());
    }

    [TestMethod]
    public void E2E_HasManagedAndContentFiles_VB()
    {
        var context = CreateContext("VB");

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
        var projectFilePath = context.CreateProjectFile(projectXml);

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        // Check the projectInfo.xml file points to the file containing the list of files to analyze
        var actualStructure = ValidateAndLoadProjectStructure(context);
        actualStructure.AssertExpectedFileList("none1.txt", "code1.vb", "code2.vb");
    }

    [TestMethod]
    public void E2E_IntermediateOutputFilesAreExcluded()
    {
        var context = CreateContext("VB", string.Empty);

        // Add files that should be analyzed
        var nonObjFolder = Path.Combine(context.InputFolder, "foo");
        Directory.CreateDirectory(nonObjFolder);
        var compile1 = context.CreateInputFile("compile1.cs");
        CreateEmptyFile(nonObjFolder, "compile2.cs");

        // Add files under the obj folder that should not be analyzed
        var objFolder = Path.Combine(context.InputFolder, "obj");
        var objSubFolder1 = Path.Combine(objFolder, "debug");
        var objSubFolder2 = Path.Combine(objFolder, "xxx"); // any folder under obj should be ignored
        Directory.CreateDirectory(objSubFolder1);
        Directory.CreateDirectory(objSubFolder2);

        // File in obj
        var objFile = CreateEmptyFile(objFolder, "objFile1.cs");

        // File in obj\debug
        CreateEmptyFile(objSubFolder1, "objDebugFile1.cs");

        // File in obj\xxx
        var objFooFile = CreateEmptyFile(objSubFolder2, "objFooFile.cs");

        var projectXml = $"""
            <ItemGroup>
              <Compile Include='{compile1}' />
              <Compile Include='foo\compile2.cs' />
              <Compile Include='{objFile}' />
              <Compile Include='obj\debug\objDebugFile1.cs' />
              <Compile Include='{objFooFile}' />
            </ItemGroup>
            """;
        var projectFilePath = context.CreateProjectFile(projectXml);

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        // Check the list of files to be analyzed
        var actualStructure = ValidateAndLoadProjectStructure(context);
        actualStructure.AssertExpectedFileList("compile1.cs", Path.Combine("foo", "compile2.cs"));
    }

    [TestMethod]
    public void E2E_UsingTaskHandlesBracketsInName() // Analysis build fails if the build definition name contains brackets
    {
        var context = CreateContext("VB", "Input folder with (brackets) in name");

        // Copy the task assembly and supporting assemblies to a folder with brackets in the name
        var taskAssemblyFilePath = typeof(WriteProjectInfoFile).Assembly.Location;
        var asmName = Path.GetFileName(taskAssemblyFilePath);
        var dllDir = Path.GetDirectoryName(taskAssemblyFilePath);
        foreach (var file in Directory.EnumerateFiles(dllDir, "*Sonar*.dll").Concat(Directory.EnumerateFiles(dllDir, "*AltCover*")))
        {
            File.Copy(file, Path.Combine(context.InputFolder, Path.GetFileName(file)));
        }

        // Set the project property to use that file. To reproduce the bug, we need to have MSBuild search for
        // the assembly using "GetDirectoryNameOfFileAbove".
        var val = $"$([MSBuild]::GetDirectoryNameOfFileAbove('{context.InputFolder}', '{asmName}')){Path.DirectorySeparatorChar}{asmName}";
        var code1 = context.CreateInputFile("code1.vb");
        var projectXml = $"""
            <PropertyGroup>
              <SonarQubeBuildTasksAssemblyFile>{val}</SonarQubeBuildTasksAssemblyFile>
            </PropertyGroup>
            <ItemGroup>
              <Compile Include='{code1}' />
            </ItemGroup>
            """;
        var projectFilePath = context.CreateProjectFile(projectXml);

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        // Check the list of files to be analyzed
        var actualStructure = ValidateAndLoadProjectStructure(context);
        actualStructure.AssertExpectedFileList("code1.vb");
    }

    [TestMethod]
    public void E2E_ExcludedProjects()
    {
        // Project info should still be written for files with $(SonarQubeExclude) set to true
        var context = CreateContext();
        var userDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "User");
        var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

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

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings
        // Do not override user-provided value
        File.Exists(Path.Combine(userDir, "UserDefined.json")).Should().BeTrue();

        var actualStructure = ValidateAndLoadProjectStructure(context, checkAndLoadConfigFile: false);
        actualStructure.ProjectInfo.IsExcluded.Should().BeTrue();
        actualStructure.AssertConfigFileDoesNotExist(ExpectedProjectConfigFileName);
        actualStructure.AssertExpectedFileList("code1.txt");
        actualStructure.ProjectInfo.AnalysisSettings.Should().NotContain(x => ScannerEngineInputGenerator.IsReportFilePaths(x.Id));
        var solutionTargetTelemetryFile = Path.Combine(rootOutputFolder, "Telemetry.Targets.S4NET.json");
        File.Exists(solutionTargetTelemetryFile).Should().BeTrue();
        File.ReadAllLines(solutionTargetTelemetryFile).Should().Contain("""{"dotnetenterprise.s4net.build.exclusion_proj.cnt":"true"}""");
    }

    [TestMethod]
    public void E2E_TestProjects()
    {
        // Project info and config should be written for files with $(SonarQubeTestProject) set to true
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

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        var actualStructure = ValidateAndLoadProjectStructure(context);
        actualStructure.ProjectInfo.ProjectType.Should().Be(ProjectType.Test);
        actualStructure.ProjectConfig.ProjectType.Should().Be(ProjectType.Test);
        actualStructure.AssertExpectedFileList("code1.txt");
    }

    [TestMethod]
    public void E2E_ProductProjects()
    {
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

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        var actualStructure = ValidateAndLoadProjectStructure(context);
        actualStructure.ProjectInfo.ProjectType.Should().Be(ProjectType.Product);
        actualStructure.ProjectConfig.ProjectType.Should().Be(ProjectType.Product);
        actualStructure.AssertExpectedFileList("code1.txt");
    }

    [TestMethod]
    public void E2E_CustomErrorLogPath()
    {
        var context = CreateContext();
        var userDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "User");
        var customErrorLog = Path.Combine(userDir, "UserDefined.json");
        var projectXml = $"""
            <PropertyGroup>
              <ErrorLog>{customErrorLog}</ErrorLog>
            </PropertyGroup>
            """;
        var projectFilePath = context.CreateProjectFile(projectXml);

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings

        var actualStructure = ValidateAndLoadProjectStructure(context);
        actualStructure.ProjectInfo.ProjectType.Should().Be(ProjectType.Product);
        actualStructure.ProjectInfo.AnalysisSettings.Single(x => ScannerEngineInputGenerator.IsReportFilePaths(x.Id)).Value.Should().Be(customErrorLog);
    }

    // Checks the integration targets handle non-VB/C# project types
    // that don't import the standard targets or set the expected properties
    // The project info should be created as normal and the correct files to analyze detected.
    [TestMethod]
    public void E2E_BareProject_FilesToAnalyze()
    {
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

        var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath, TargetConstants.DefaultBuild);

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
        projectInfo.AnalysisResultFiles.Should().ContainSingle("Unexpected number of analysis results created");

        // Check the correct list of files to analyze were returned
        var filesToAnalyze = projectInfo.AssertAnalysisResultFileExists(nameof(AnalysisResultFileType.FilesToAnalyze));
        var actualFilesToAnalyze = File.ReadAllLines(filesToAnalyze.Location);
        actualFilesToAnalyze.Should().BeEquivalentTo([codeFile, contentFile], "Unexpected list of files to analyze");
        var projectTelemetryFile = Path.Combine(context.OutputFolder, "0", "Telemetry.json");
        File.Exists(projectTelemetryFile).Should().BeTrue();
        File.ReadAllLines(projectTelemetryFile).Should().Contain("""{"dotnetenterprise.s4net.build.exclusion_file.cnt":"true"}""");
    }

    // Checks that projects that don't include the standard managed targets are still
    // processed correctly e.g. can be excluded, marked as test projects etc
    [TestMethod]
    public void E2E_BareProject_CorrectlyCategorised()
    {
        var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
        var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");
        var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
        var projectFilePath = Path.Combine(rootInputFolder, "project.txt");
        var projectXml = $"""
            <?xml version='1.0' encoding='utf-8'?>
            <Project ToolsVersion='12.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
              <PropertyGroup>
                <SonarQubeExclude>true</SonarQubeExclude>
                <Language>my.language</Language>
                <ProjectTypeGuids>{TargetConstants.MsTestProjectTypeGuid}</ProjectTypeGuids>
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

        var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath, TargetConstants.DefaultBuild);

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
        projectInfo.AnalysisResultFiles.Should().BeEmpty("Unexpected number of analysis results created");
        var solutionTargetTelemetryFile = Path.Combine(rootOutputFolder, "Telemetry.Targets.S4NET.json");
        File.Exists(solutionTargetTelemetryFile).Should().BeTrue();
        File.ReadAllLines(solutionTargetTelemetryFile).Should().Contain("""{"dotnetenterprise.s4net.build.exclusion_proj.cnt":"true"}""");
    }

    // Checks that projects that don't include the standard managed targets are still
    // processed correctly e.g. can be excluded, marked as test projects etc
    [TestMethod]
    public void E2E_RazorProjectWithoutSourceGeneration_ValidProjectInfoFilesGenerated()
    {
        var context = CreateContext();
        var defaultProjectInfoPath = Path.Combine(context.OutputFolder, "0", "ProjectInfo.xml");
        var razorProjectInfoPath = Path.Combine(context.OutputFolder, "0.Razor", "ProjectInfo.xml");
        var defaultProjectOutPaths = Path.Combine(context.OutputFolder, "0");
        var razorProjectOutPaths = Path.Combine(context.OutputFolder, "0.Razor");
        var defaultReportFilePaths = Path.Combine(defaultProjectOutPaths, "Issues.json");
        var razorReportFilePaths = Path.Combine(razorProjectOutPaths, "Issues.Views.json");
        var filesToAnalyzePath = Path.Combine(context.ConfigFolder, "0", "FilesToAnalyze.txt");
        var telemetryPath = Path.Combine(defaultProjectOutPaths, "Telemetry.json");
        var projectXml = """
                           <PropertyGroup>
                             <TargetFramework>net5</TargetFramework>
                             <RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>
                             <UseRazorSourceGenerator>false</UseRazorSourceGenerator>
                           </PropertyGroup>

              <ItemGroup>
                <RazorCompile Include='SomeRandomValue' />
                <SonarQubeAnalysisFiles Include='SomeRandomFile' />
              </ItemGroup>

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
            """;
        var projectRoot = context.CreateProjectFile(projectXml);

        var result = BuildRunner.BuildTargets(TestContext, projectRoot);

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
        AssertProjectInfoContent(defaultProjectInfo, defaultReportFilePaths, defaultProjectOutPaths, filesToAnalyzePath, telemetryPath);
        AssertProjectInfoContent(razorProjectInfo, razorReportFilePaths, razorProjectOutPaths, filesToAnalyzePath, null);
    }

    // Checks that projects that don't include the standard managed targets are still
    // processed correctly e.g. can be excluded, marked as test projects etc
    [TestMethod]
    public void E2E_RazorProjectWithSourceGenerationEnabled_ValidProjectInfoFilesGenerated()
    {
        var context = CreateContext();
        var defaultProjectInfoPath = Path.Combine(context.OutputFolder, "0", "ProjectInfo.xml");
        var razorProjectInfoPath = Path.Combine(context.OutputFolder, "0.Razor", "ProjectInfo.xml");
        var telemetryPath = Path.Combine(context.OutputFolder, "0", "Telemetry.json");
        var projectGuid = Guid.NewGuid();
        var projectXml = $"""
                            <PropertyGroup>
                              <ProjectGuid>{projectGuid}</ProjectGuid>
                              <TargetFramework>net5</TargetFramework>
                              <RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>
                              <UseRazorSourceGenerator>true</UseRazorSourceGenerator>
                            </PropertyGroup>

              <ItemGroup>
                <RazorCompile Include='SomeRandomValue' />
                <SonarQubeAnalysisFiles Include='SomeRandomFile' />
              </ItemGroup>

              <Target Name='CoreCompile'>
                <Message Importance='high' Text='In dummy core compile target' />
                <WriteLinesToFile File='$(ErrorLog)' Overwrite='true' />
              </Target>

              <Target Name='Build' DependsOnTargets='CoreCompile'>
                <Message Importance='high' Text='In dummy build target' />
              </Target>
            """;
        var projectFile = context.CreateProjectFile(projectXml);

        var result = BuildRunner.BuildTargets(TestContext, projectFile);

        result.BuildSucceeded.Should().BeTrue();
        result.AssertTargetOrdering(
            TargetConstants.SonarCategoriseProject,
            TargetConstants.SonarWriteFilesToAnalyze,
            TargetConstants.CoreCompile,
            TargetConstants.DefaultBuild,
            TargetConstants.InvokeSonarWriteProjectData_NonRazorProject,
            TargetConstants.SonarWriteProjectData);

        // Check the project info
        var defaultProjectOutPaths = Path.Combine(context.OutputFolder, "0");
        var razorProjectOutPaths = Path.Combine(context.OutputFolder, "0.Razor");
        var defaultReportFilePaths = Path.Combine(defaultProjectOutPaths, "Issues.json");
        var razorReportFilePaths = Path.Combine(razorProjectOutPaths, "Issues.Views.json");
        var filesToAnalyzePath = Path.Combine(context.ConfigFolder, "0", "FilesToAnalyze.txt");
        File.Exists(defaultProjectInfoPath).Should().BeTrue();
        File.Exists(razorProjectInfoPath).Should().BeFalse();
        File.Exists(razorReportFilePaths).Should().BeFalse();
        var defaultProjectInfo = ProjectInfo.Load(defaultProjectInfoPath);
        AssertProjectInfoContent(defaultProjectInfo, defaultReportFilePaths, defaultProjectOutPaths, filesToAnalyzePath, telemetryPath);
    }

    [TestMethod]
    public void E2E_TestProjects_ProtobufFilesAreUpdated()
    {
        var result = Execute_E2E_TestProjects_ProtobufFileNamesAreUpdated(true, "subdir1");

        result.AssertTargetExecuted("FixUpTestProjectOutputs");

        var protobufDir = Path.Combine(result.GetPropertyValue(TargetProperties.ProjectSpecificOutDir), "subdir1");

        AssertFilesExistsAndAreNotEmpty(protobufDir, "encoding.pb", "file-metadata.pb", "symrefs.pb", "token-type.pb");
        AssertFilesExistsAndAreEmpty(protobufDir, "metrics.pb", "token-cpd.pb");
    }

    [TestMethod]
    public void E2E_NonTestProjects_ProtobufFilesAreNotUpdated()
    {
        var result = Execute_E2E_TestProjects_ProtobufFileNamesAreUpdated(false, "subdir2");

        result.AssertTargetNotExecuted("FixUpTestProjectOutputs");

        var protobufDir = Path.Combine(result.GetPropertyValue(TargetProperties.ProjectSpecificOutDir), "subdir2");

        // Protobuf files should not change for non-test project
        AssertFilesExistsAndAreNotEmpty(protobufDir, protobufFileNames);
    }

    [TestMethod]
    public void E2E_AnalysisSettings_HasCorrectTelemetryPath()
    {
        var context = CreateContext();
        var codeFilePath = context.CreateInputFile("codeFile1.txt");
        var projectXml = $"""
            <ItemGroup>
              <Compile Include='{codeFilePath}' />
            </ItemGroup>
            """;
        var projectFilePath = context.CreateProjectFile(projectXml);
        var defaultProjectInfoPath = Path.Combine(context.OutputFolder, @"0", "ProjectInfo.xml");

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild);
        result.AssertErrorCount(0);
        result.AssertTargetOrdering(
            TargetConstants.SonarCategoriseProject,
            TargetConstants.SonarWriteFilesToAnalyze,
            TargetConstants.DefaultBuild,
            TargetConstants.InvokeSonarWriteProjectData_NonRazorProject,
            TargetConstants.SonarWriteProjectData);

        var projectInfo = ProjectInfo.Load(defaultProjectInfoPath);
        // this assertion will fail once the path for sonar.cs.scanner.telemetry" has a file with contents. Once it fails the assertion should be replaced with
        projectInfo.AnalysisSettings.Should().ContainSingle(x => x.Id.Equals("sonar.cs.scanner.telemetry")).Which.Value.Should().Be(Path.Combine(context.OutputFolder, "0", "Telemetry.json"));
    }

    [TestMethod]
    public void E2E_TelemetryFiles_AllWritten()
    {
        var context = CreateContext();
        var codeFilePath = context.CreateInputFile("codeFile1.cs");
        var projectXml = $"""
            <PropertyGroup>
              <SonarQubeTestProject>true</SonarQubeTestProject>
            </PropertyGroup>
            <ItemGroup>
              <Compile Include='{codeFilePath}' />
            </ItemGroup>
            <ItemGroup>
              <SonarQubeSetting Include="sonar.my.custom.setting">
                <Value>customValue</Value>
              </SonarQubeSetting>
            </ItemGroup>
            """;
        var projectFilePath = context.CreateProjectFile(projectXml);
        var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.BuildSucceeded.Should().BeTrue();

        result.AssertTargetOrdering(
            TargetConstants.SonarCategoriseProject,
            TargetConstants.SonarWriteFilesToAnalyze,
            TargetConstants.CoreCompile,
            TargetConstants.DefaultBuild,
            TargetConstants.InvokeSonarWriteProjectData_NonRazorProject,
            TargetConstants.SonarWriteProjectData);

        var solutionTargetTelemetryFile = Path.Combine(rootOutputFolder, "Telemetry.Targets.S4NET.json");
        File.Exists(solutionTargetTelemetryFile).Should().BeTrue();
        File.ReadAllLines(solutionTargetTelemetryFile).Should().SatisfyRespectively(
            x => x.Should().StartWith("""{"dotnetenterprise.s4net.build.visual_studio_version":"""),
            x => x.Should().StartWith("""{"dotnetenterprise.s4net.build.msbuild_version":"""));

        var projectTelemetryFile = Path.Combine(rootOutputFolder, "0", "Telemetry.json");
        File.Exists(projectTelemetryFile).Should().BeTrue();
        File.ReadAllLines(projectTelemetryFile).Should().SatisfyRespectively(
            x => x.Should().Be(""""{"dotnetenterprise.s4net.build.override_warnings_as_errors.cnt":"true"}""""),
            x => x.Should().StartWith("""{"dotnetenterprise.s4net.build.target_framework_moniker":"""),
            x => x.Should().Be("""{"dotnetenterprise.s4net.build.using_microsoft_net_sdk.cnt":"true"}"""),
            x => x.Should().Be("""{"dotnetenterprise.s4net.build.deterministic.cnt":"true"}"""),
            x => x.Should().StartWith("""{"dotnetenterprise.s4net.build.netcore_sdk_version.cnt":"""),
            x => x.Should().Be("""{"dotnetenterprise.s4net.build.sonar_properties_in_project_file.cnt":"set"}"""),
            x => x.Should().Be("""{"dotnetenterprise.s4net.build.test_project_in_proj.cnt":"true"}"""));
    }

    private BuildLog Execute_E2E_TestProjects_ProtobufFileNamesAreUpdated(bool isTestProject, string projectSpecificSubDir)
    {
        // Protobuf files containing metrics information should be created for test projects.
        // However, some of the metrics files should be empty, as should the issues report.
        // See [MMF-485] : https://jira.sonarsource.com/browse/MMF-486
        // This method creates some non-empty dummy protobuf files during a build.
        // The caller can should check that the protobufs have been updated/not-updated,
        // as expected, depending on the type of the project being built.
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

        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild); // Build should succeed with warnings
        var actualStructure = ValidateAndLoadProjectStructure(context);

        // Sanity check that the above target was executed
        result.AssertTargetExecuted("CreateDummyProtobufFiles");

        var projectSpecificOutputDir2 = result.GetPropertyValue(TargetProperties.ProjectSpecificOutDir);
        projectSpecificOutputDir2.Should().Be(actualStructure.ProjectSpecificOutputDir);

        AssertNoAdditionalFilesInFolder(
            actualStructure.ProjectSpecificOutputDir,
            protobufFileNames.Concat([ExpectedAnalysisFilesListFileName, ExpectedIssuesFileName, FileConstants.ProjectInfoFileName, FileConstants.TelemetryProjectFileName]).ToArray());
        return result;
    }

    private static string CreateEmptyFile(string folder, string fileName)
    {
        var filePath = Path.Combine(folder, fileName);
        File.WriteAllText(filePath, null);
        return filePath;
    }

    private TargetsTestsContext CreateContext(string language = "C#", string inputFolderName = "Inputs") =>
        new(TestContext, language, inputFolderName);

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
            Assert.Fail($"Additional files exist in the project output folder: {folderPath}");
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

    private static void AssertProjectInfoContent(ProjectInfo projectInfo,
                                                 string expectedReportFilePaths,
                                                 string expectedProjectOutPaths,
                                                 string expectedFilesToAnalyzePath,
                                                 string expectedTelemetryPath)
    {
        projectInfo.ProjectType.Should().Be(ProjectType.Product, "Project should be marked as a product project");
        projectInfo.AnalysisResultFiles.Should().ContainSingle(x => x.Id.Equals(AnalysisResultFileType.FilesToAnalyze.ToString())).Which.Location.Should().Be(expectedFilesToAnalyzePath);
        projectInfo.AnalysisSettings.Should().ContainSingle(x => x.Id.Equals("sonar.cs.roslyn.reportFilePaths")).Which.Value.Should().Be(expectedReportFilePaths);
        projectInfo.AnalysisSettings.Should().ContainSingle(x => x.Id.Equals("sonar.cs.analyzer.projectOutPaths")).Which.Value.Should().Be(expectedProjectOutPaths);
        if (expectedTelemetryPath is null)
        {
            projectInfo.AnalysisSettings.Should().NotContain(x => x.Id.Equals("sonar.cs.scanner.telemetry"));
        }
        else
        {
            projectInfo.AnalysisSettings.Should().ContainSingle(x => x.Id.Equals("sonar.cs.scanner.telemetry")).Which.Value.Should().Be(expectedTelemetryPath);
        }
    }

    private static ProjectStructure ValidateAndLoadProjectStructure(TargetsTestsContext context, bool checkAndLoadConfigFile = true)
    {
        var projectSpecificConfigDir = FindAndValidateProjectSpecificDirectory(context.ConfigFolder, "config");
        var projectSpecificOutputDir = FindAndValidateProjectSpecificDirectory(context.OutputFolder, "output");
        return new ProjectStructure(context, projectSpecificConfigDir, projectSpecificOutputDir, checkAndLoadConfigFile);
    }

    private static string FindAndValidateProjectSpecificDirectory(string rootFolder, string logType)
    {
        Directory.Exists(rootFolder).Should().BeTrue($"Expected root {logType} folder does not exist");

        // We've only built one project, so we only expect one directory under the root
        Directory.EnumerateDirectories(rootFolder).Should().ContainSingle($"Only expecting one child directory to exist under the root analysis {logType} folder");

        var fileCount = Directory.GetFiles(rootFolder, "*.*", SearchOption.TopDirectoryOnly).Count(x => !x.Contains("Telemetry.Targets.S4NET.json"));
        fileCount.Should().Be(0, $"Not expecting the top-level {logType} folder to contain any files");

        var projectSpecificPath = Directory.EnumerateDirectories(rootFolder).Single();

        // Check folder naming
        var folderName = Path.GetFileName(projectSpecificPath);
        int.TryParse(folderName, out _).Should().BeTrue($"Expecting the folder name to be numeric: {folderName}");

        return projectSpecificPath;
    }

    private class ProjectStructure
    {
        public readonly string ProjectSpecificConfigDir;
        public readonly string ProjectSpecificOutputDir;
        public readonly ProjectConfig ProjectConfig;
        public readonly ProjectInfo ProjectInfo;

        private readonly TargetsTestsContext context;

        public ProjectStructure(TargetsTestsContext context, string projectSpecificConfigDir, string projectSpecificOutputDir, bool checkAndLoadConfigFile)
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

            var expectedFullPaths = fileNames.Select(x => Path.Combine(context.InputFolder, x));
            File.ReadLines(filesToAnalyzeFile.FullPath).Should().BeEquivalentTo(expectedFullPaths);

            var actualFilesToAnalyze = ProjectInfo.AssertAnalysisResultFileExists(AnalysisResultFileType.FilesToAnalyze.ToString());
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
