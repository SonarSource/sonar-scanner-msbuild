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

using SonarScanner.MSBuild.Tasks.IntegrationTest;
using SonarScanner.MSBuild.Tasks.IntegrationTest.TargetsTests;
using static TestUtilities.TestUtils;

namespace SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests;

[TestClass]
public class RazorTargetTests
{
    private static readonly char Separator = Path.DirectorySeparatorChar;

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void SonarPrepareRazorProjectCodeAnalysis_WhenNoSonarErrorLog_NoPropertiesAreSet()
    {
        var context = new TargetsTestsContext(TestContext);
        var filePath = context.CreateProjectFile(CreateProjectSnippet(razorCompilationErrorLog: null, additionalProperties: "<RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>"));

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

        result.AssertTargetExecuted(TargetConstants.SonarPrepareRazorProjectCodeAnalysis);
        result.AssertPropertyValue(TargetProperties.SonarErrorLog, string.Empty); // SetRazorCodeAnalysisProperties target doesn't change it
        result.AssertPropertyValue(TargetProperties.ErrorLog, null);
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLog, null);
        result.AssertPropertyValue(TargetProperties.RazorCompilationErrorLog, null);
        result.AssertPropertyValue(TargetProperties.SonarTemporaryProjectSpecificOutDir, $"{context.OutputFolder}{Separator}0.tmp");
        result.AssertItemGroupCount(TargetItemGroups.CoreCompileOutFiles, 2); // ProjectInfo.xml and Telemetry.S4NET.Targets.json
    }

    [TestMethod]
    public void SonarPrepareRazorProjectCodeAnalysis_WhenSonarErrorLogSet_SetsRazorErrorLogProperties()
    {
        var context = new TargetsTestsContext(TestContext);
        var filePath = context.CreateProjectFile(
            CreateProjectSnippet(
                sonarErrorLog: "OriginalValueFromFirstBuild.json",
                razorCompilationErrorLog: null,
                additionalProperties: "<RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>"));

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

        result.AssertTargetExecuted(TargetConstants.SonarPrepareRazorProjectCodeAnalysis);
        AssertExpectedErrorLog(result, context.OutputFolder + $"{Separator}0{Separator}Issues.Views.json");
        result.AssertPropertyValue(TargetProperties.SonarTemporaryProjectSpecificOutDir, $"{context.OutputFolder}{Separator}0.tmp");
        result.AssertItemGroupCount(TargetItemGroups.CoreCompileOutFiles, 2); // ProjectInfo.xml and Telemetry.S4NET.Targets.json
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("OriginalValueFromFirstBuild.json")]
    public void SonarPrepareRazorProjectCodeAnalysis_WithSourceGenerators_NotExecuted(string sonarErrorLogValue)
    {
        var filePath = new TargetsTestsContext(TestContext).CreateProjectFile(
            CreateProjectSnippet(
                sonarErrorLog: sonarErrorLogValue,
                razorCompilationErrorLog: null,
                useRazorSourceGenerator: "true",
                additionalProperties: "<RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>"));

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

        result.AssertTargetNotExecuted(TargetConstants.SonarPrepareRazorProjectCodeAnalysis);
    }

    [TestMethod]
    public void SonarPrepareRazorProjectCodeAnalysis_PreserveRazorCompilationErrorLog()
    {
        var context = new TargetsTestsContext(TestContext);
        var filePath = context.CreateProjectFile(
            CreateProjectSnippet(
                sonarErrorLog: "OriginalValueFromFirstBuild.json",
                razorCompilationErrorLog: $"{DriveRoot()}UserDefined.json",
                useRazorSourceGenerator: null,
                additionalProperties: "<RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>"));

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

        result.AssertTargetExecuted(TargetConstants.SonarPrepareRazorProjectCodeAnalysis);
        AssertExpectedErrorLog(result, $"{DriveRoot()}UserDefined.json");
        result.AssertPropertyValue(TargetProperties.SonarTemporaryProjectSpecificOutDir, $"{context.OutputFolder}{Separator}0.tmp");
        result.AssertItemGroupCount(TargetItemGroups.CoreCompileOutFiles, 2); // ProjectInfo.xml and Telemetry.S4NET.Targets.json
    }

    [TestMethod]
    [DataRow(0, null)]
    [DataRow(1, "OriginalValueFromFirstBuild.json")]
    public void SonarPrepareRazorProjectCodeAnalysis_CreatesTempFolderAndPreservesMainFolder(int index, string sonarErrorLogValue)
    {
        var context = new TargetsTestsContext(TestContext, inputFolder: $"Inputs{index}", outputFolder: $"Outputs{index}");
        var testTargetName = "CreateDirectoryAndFile";
        var subDirName = "foo";
        var subDirFileName = "bar.txt";
        var projectSnippet = CreateProjectSnippet(
            sonarErrorLog: sonarErrorLogValue,
            razorCompilationErrorLog: null,
            useRazorSourceGenerator: null,
            additionalProperties: "<RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>");

        projectSnippet += $"""
            <Target Name="{testTargetName}" AfterTargets="SonarCreateProjectSpecificDirs" BeforeTargets="SonarPrepareRazorProjectCodeAnalysis">
                <!-- I do not use properties for the paths to keep the properties strictly to what is needed by the target under test -->
                <MakeDir Directories="$(ProjectSpecificOutDir){Separator}{subDirName}" />
                <WriteLinesToFile File="$(ProjectSpecificOutDir){Separator}{subDirName}{Separator}{subDirFileName}" Lines="foobar" />
            </Target>
            """;
        var filePath = context.CreateProjectFile(projectSnippet);

        var result = BuildRunner.BuildTargets(
            TestContext,
            filePath,
            // we need to explicitly pass this targets in order for the Directory to get generated before executing the 'testTarget'
            // otherwise, 'SonarCreateProjectSpecificDirs' gets executed only when invoked by 'SonarPrepareRazorProjectCodeAnalysis', after 'testTarget'
            TargetConstants.SonarCreateProjectSpecificDirs,
            testTargetName,
            TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

        result.AssertTargetExecuted(TargetConstants.SonarPrepareRazorProjectCodeAnalysis);
        var specificOutputDir = Path.Combine(context.OutputFolder, "0");
        // main folder should still be on disk
        Directory.Exists(specificOutputDir).Should().BeTrue();
        File.Exists(Path.Combine(specificOutputDir, "ProjectInfo.xml")).Should().BeFalse();

        // contents should be moved to temporary folder
        var temporaryProjectSpecificOutDir = Path.Combine(context.OutputFolder, "0.tmp");
        result.AssertPropertyValue(TargetProperties.SonarTemporaryProjectSpecificOutDir, temporaryProjectSpecificOutDir);
        result.AssertItemGroupCount(TargetItemGroups.CoreCompileOutFiles, 3); // ProjectInfo.xml, bar.txt and Telemetry.S4NET.Targets.json
        Directory.Exists(temporaryProjectSpecificOutDir).Should().BeTrue();
        File.Exists(Path.Combine(temporaryProjectSpecificOutDir, "ProjectInfo.xml")).Should().BeTrue();
        // the dir and file should have been moved as well
        File.Exists(Path.Combine(temporaryProjectSpecificOutDir, subDirName, subDirFileName)).Should().BeTrue();
        result.Messages.Should()
            .ContainMatch($"Sonar: Preparing for Razor compilation, moved files (*{Separator}foo{Separator}bar.txt;*{Separator}ProjectInfo.xml*{Separator}Telemetry.json) to *{Separator}0.tmp.");
    }

    [TestMethod]
    public void SonarFinishRazorProjectCodeAnalysis_WithSourceGenerators_NotExecuted()
    {
        var projectSpecificOutDir = CreateTestSpecificFolderWithSubPaths(TestContext, "0");
        var temporaryProjectSpecificOutDir = CreateTestSpecificFolderWithSubPaths(TestContext, "0.tmp");
        CreateEmptyFile(temporaryProjectSpecificOutDir, "Issues.FromMainBuild.json");
        var filePath = new TargetsTestsContext(TestContext).CreateProjectFile(
            CreateProjectSnippet(
                sonarErrorLog: null,
                useRazorSourceGenerator: "true",
                razorCompilationErrorLog: null,
                razorCompile: "SomeRandomValue",
                "<RazorSonarErrorLogName>Issues.FromRazorBuild.json</RazorSonarErrorLogName>",
                $"<ProjectSpecificOutDir>{projectSpecificOutDir}</ProjectSpecificOutDir>",
                $"<SonarTemporaryProjectSpecificOutDir>{temporaryProjectSpecificOutDir}</SonarTemporaryProjectSpecificOutDir>"));

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarFinishRazorProjectCodeAnalysis);

        result.AssertTargetNotExecuted(TargetConstants.SonarFinishRazorProjectCodeAnalysis);
    }

    [TestMethod]
    public void SonarFinishRazorProjectCodeAnalysis_WhenRazorSonarErrorLogOrLogNameAreNotSet_DoesNotCreateAnalysisSettings()
    {
        var root = CreateTestSpecificFolderWithSubPaths(TestContext);
        var projectSpecificOutDir = CreateTestSpecificFolderWithSubPaths(TestContext, "0");
        var temporaryProjectSpecificOutDir = CreateTestSpecificFolderWithSubPaths(TestContext, "0.tmp");
        var razorSpecificOutDir = Path.Combine(root, "0.Razor");
        var filePath = new TargetsTestsContext(TestContext).CreateProjectFile(
            CreateProjectSnippet(
                sonarErrorLog: string.Empty,
                useRazorSourceGenerator: null,
                razorCompilationErrorLog: null,
                razorCompile: "SomeRandomValue",
                "<RazorSonarErrorLog></RazorSonarErrorLog>",        // This should not happen as long as SonarPrepareRazorProjectCodeAnalysis works as expected
                "<RazorSonarErrorLogName></RazorSonarErrorLogName>",
                $"<ProjectSpecificOutDir>{projectSpecificOutDir}</ProjectSpecificOutDir>",
                $"<SonarTemporaryProjectSpecificOutDir>{temporaryProjectSpecificOutDir}</SonarTemporaryProjectSpecificOutDir>"));

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarFinishRazorProjectCodeAnalysis);

        var actualProjectInfo = ProjectInfoAssertions.AssertProjectInfoExists(root, filePath);
        result.AssertTargetExecuted(TargetConstants.SonarFinishRazorProjectCodeAnalysis);

        actualProjectInfo.AnalysisSettings.Should().BeEmpty();
        Directory.Exists(temporaryProjectSpecificOutDir).Should().BeFalse();

        result.AssertPropertyValue(TargetProperties.RazorSonarProjectSpecificOutDir, razorSpecificOutDir);
        result.AssertPropertyValue(TargetProperties.RazorSonarProjectInfo, $"{razorSpecificOutDir}{Separator}ProjectInfo.xml");
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLog, string.Empty);
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLogExists, null);
        result.AssertItemGroupCount(TargetItemGroups.RazorCompilationOutFiles, 0);
        result.AssertItemGroupCount(TargetItemGroups.SonarTempFiles, 0);
        result.AssertItemGroupCount(TargetItemGroups.RazorSonarReportFilePath, 0);
        result.AssertItemGroupCount(TargetItemGroups.RazorSonarQubeSetting, 0);
    }

    [TestMethod]
    public void SonarFinishRazorProjectCodeAnalysis_RazorSpecificOutputAndProjectInfo_AreCopiedToCorrectFolders()
    {
        var root = CreateTestSpecificFolderWithSubPaths(TestContext);
        var projectSpecificOutDir = CreateTestSpecificFolderWithSubPaths(TestContext, "0");
        var temporaryProjectSpecificOutDir = CreateTestSpecificFolderWithSubPaths(TestContext, "0.tmp");
        var razorSpecificOutDir = Path.Combine(root, "0.Razor");
        CreateEmptyFile(temporaryProjectSpecificOutDir, "Issues.FromMainBuild.json");
        CreateEmptyFile(projectSpecificOutDir, "Issues.FromRazorBuild.json");
        var razorIssuesPath = Path.Combine(razorSpecificOutDir, "Issues.FromRazorBuild.json");

        var testTargetName = "CreateDirectoryAndFile";
        var subDirName = "foo";
        var subDirFileName = "bar.txt";

        // RazorSonarErrorLog is set to the MSBuild $(RazorCompilationErrorLog) value
        // RazorSonarErrorLogName is set when the $(RazorCompilationErrorLog) is not set / empty
        var projectSnippet = CreateProjectSnippet(
            sonarErrorLog: null,
            useRazorSourceGenerator: null,
            razorCompilationErrorLog: null,
            razorCompile: "SomeRandomValue",
            "<RazorSonarErrorLog></RazorSonarErrorLog>",
            "<RazorSonarErrorLogName>Issues.FromRazorBuild.json</RazorSonarErrorLogName>",
            $"<ProjectSpecificOutDir>{projectSpecificOutDir}</ProjectSpecificOutDir>",
            $"<SonarTemporaryProjectSpecificOutDir>{temporaryProjectSpecificOutDir}</SonarTemporaryProjectSpecificOutDir>");

        projectSnippet += $"""
            <Target Name="{testTargetName}">
                <!-- I do not use properties for the paths to keep the properties strictly to what is needed by the target under test -->
                <MakeDir Directories = "$(ProjectSpecificOutDir){Separator}{subDirName}" />
                <WriteLinesToFile File="$(ProjectSpecificOutDir){Separator}{subDirName}{Separator}{subDirFileName}" Lines = "foobar" />
            </Target>
            """;

        var filePath = new TargetsTestsContext(TestContext).CreateProjectFile(projectSnippet);
        var result = BuildRunner.BuildTargets(TestContext, filePath, testTargetName, TargetConstants.SonarFinishRazorProjectCodeAnalysis);
        var razorProjectInfo = ProjectInfoAssertions.AssertProjectInfoExists(root, filePath);
        result.AssertTargetExecuted(TargetConstants.SonarFinishRazorProjectCodeAnalysis);
        razorProjectInfo.AnalysisSettings.Single(x => x.Id.Equals("sonar.cs.analyzer.projectOutPaths")).Value.Should().Be(razorSpecificOutDir);
        razorProjectInfo.AnalysisSettings.Single(x => x.Id.Equals("sonar.cs.roslyn.reportFilePaths")).Value.Should().Be(razorIssuesPath);
        Directory.Exists(temporaryProjectSpecificOutDir).Should().BeFalse();
        File.Exists(Path.Combine(projectSpecificOutDir, "Issues.FromMainBuild.json")).Should().BeTrue();
        File.Exists(Path.Combine(razorSpecificOutDir, "Issues.FromRazorBuild.json")).Should().BeTrue();
        // the dir and file should have been moved as well
        File.Exists(Path.Combine(razorSpecificOutDir, subDirName, subDirFileName)).Should().BeTrue();
        // testing with substrings because the order of the files might differ
        result.Messages.Should().Contain(x =>
            x.Contains("Sonar: After Razor compilation, moved files (")
            && x.Contains($"{Separator}0{Separator}foo{Separator}bar.txt")
            && x.Contains($"{Separator}0{Separator}Issues.FromRazorBuild.json")
            && x.Contains($"{Separator}0.Razor."));
        result.Messages.Should()
            .ContainMatch($"Sonar: After Razor compilation, moved files (*{Separator}0.tmp{Separator}Issues.FromMainBuild.json) to *{Separator}0 and will remove the temporary folder.");
        result.Messages.Should().ContainMatch($"""Removing directory "*{Separator}0.tmp".""");

        result.AssertPropertyValue(TargetProperties.RazorSonarProjectSpecificOutDir, razorSpecificOutDir);
        result.AssertPropertyValue(TargetProperties.RazorSonarProjectInfo, $"{razorSpecificOutDir}{Separator}ProjectInfo.xml");
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLog, $"{razorSpecificOutDir}{Separator}Issues.FromRazorBuild.json");
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLogExists, "true");
        result.AssertItemGroupCount(TargetItemGroups.RazorCompilationOutFiles, 2); // ProjectInfo.xml and bar.txt
        result.AssertItemGroupCount(TargetItemGroups.SonarTempFiles, 1);
        result.AssertItemGroupCount(TargetItemGroups.RazorSonarReportFilePath, 1);
        result.AssertItemGroupCount(TargetItemGroups.RazorSonarQubeSetting, 2);
    }

    [TestMethod]
    public void SonarFinishRazorProjectCodeAnalysis_WithRazorSpecificOutputAndProjectInfo_PreserveUserDefinedErrorLogValue()
    {
        var root = CreateTestSpecificFolderWithSubPaths(TestContext);
        var projectSpecificOutDir = CreateTestSpecificFolderWithSubPaths(TestContext, "0");
        var temporaryProjectSpecificOutDir = CreateTestSpecificFolderWithSubPaths(TestContext, "0.tmp");
        var razorSpecificOutDir = Path.Combine(root, "0.Razor");
        var userDefinedErrorLog = CreateEmptyFile(CreateTestSpecificFolderWithSubPaths(TestContext, "User"), "UserDefined.FromRazorBuild.json");

        // RazorSonarErrorLog is set to the MSBuild $(RazorCompilationErrorLog) value
        // RazorSonarErrorLogName is set when the $(RazorCompilationErrorLog) is not set / empty
        var filePath = new TargetsTestsContext(TestContext).CreateProjectFile(
            CreateProjectSnippet(
                sonarErrorLog: null,
                useRazorSourceGenerator: null,
                razorCompilationErrorLog: null,
                razorCompile: "SomeRandomValue",
                $"<RazorSonarErrorLog>{userDefinedErrorLog}</RazorSonarErrorLog>",
                "<RazorSonarErrorLogName></RazorSonarErrorLogName> <!-- make it explicit in test that this won't be set when the RazorSonarErrorLog is set -->",
                $"<ProjectSpecificOutDir>{projectSpecificOutDir}</ProjectSpecificOutDir>",
                $"<SonarTemporaryProjectSpecificOutDir>{temporaryProjectSpecificOutDir}</SonarTemporaryProjectSpecificOutDir>"));

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarFinishRazorProjectCodeAnalysis);

        var actualProjectInfo = ProjectInfoAssertions.AssertProjectInfoExists(root, filePath);
        result.AssertTargetExecuted(TargetConstants.SonarFinishRazorProjectCodeAnalysis);
        actualProjectInfo.AnalysisSettings.Single(x => x.Id.Equals("sonar.cs.analyzer.projectOutPaths")).Value.Should().Be(razorSpecificOutDir);
        actualProjectInfo.AnalysisSettings.Single(x => x.Id.Equals("sonar.cs.roslyn.reportFilePaths")).Value.Should().Be(userDefinedErrorLog);
        Directory.Exists(temporaryProjectSpecificOutDir).Should().BeFalse();
        File.Exists(userDefinedErrorLog).Should().BeTrue();

        result.AssertPropertyValue(TargetProperties.RazorSonarProjectSpecificOutDir, razorSpecificOutDir);
        result.AssertPropertyValue(TargetProperties.RazorSonarProjectInfo, $"{razorSpecificOutDir}{Separator}ProjectInfo.xml");
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLog, userDefinedErrorLog);
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLogExists, "true");
        result.AssertItemGroupCount(TargetItemGroups.RazorCompilationOutFiles, 0);
        result.AssertItemGroupCount(TargetItemGroups.SonarTempFiles, 0);
        result.AssertItemGroupCount(TargetItemGroups.RazorSonarReportFilePath, 1);
        result.AssertItemGroupCount(TargetItemGroups.RazorSonarQubeSetting, 2);
    }

    [TestMethod]
    public void OverrideRoslynAnalysis_ExcludedProject_NoErrorLog()
    {
        var filePath = new TargetsTestsContext(TestContext).CreateProjectFile(
            CreateProjectSnippet(
                sonarErrorLog: "OriginalValueFromFirstBuild.json",
                useRazorSourceGenerator: null,
                razorCompilationErrorLog: null,
                razorCompile: "SomeRandomValue",
                "<SonarQubeExclude>true</SonarQubeExclude>"));
        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);
        result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
        AssertExpectedErrorLog(result, null);
    }

    [TestMethod]
    public void OverrideRoslynAnalysis_ExcludedProject_PreserveRazorCompilationErrorLog()
    {
        var filePath = new TargetsTestsContext(TestContext).CreateProjectFile(
            CreateProjectSnippet(
                sonarErrorLog: "OriginalValueFromFirstBuild.json",
                useRazorSourceGenerator: null,
                razorCompilationErrorLog: $"{DriveRoot()}UserDefined.json",
                razorCompile: null,
                "<SonarQubeExclude>true</SonarQubeExclude>"));

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);

        result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
        // SetRazorCodeAnalysisProperties target doesn't change it
        result.AssertPropertyValue(TargetProperties.SonarErrorLog, "OriginalValueFromFirstBuild.json");
        result.AssertPropertyValue(TargetProperties.ErrorLog, null);
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLog, null);
        result.AssertPropertyValue(TargetProperties.RazorCompilationErrorLog, $"{DriveRoot()}UserDefined.json");
    }

    [TestMethod]
    [Description("Checks the targets are executed in the expected order")]
    public void TargetExecutionOrderForRazor()
    {
        var context = new TargetsTestsContext(TestContext);
        var program = context.CreateInputFile("Program.cs");
        File.WriteAllText(program, "System.Console.WriteLine();");
        context.CreateInputFile("Index.cshtml");
        // We need to set the CodeAnalysisRuleSet property if we want ResolveCodeAnalysisRuleSet
        // to be executed. See test bug https://github.com/SonarSource/sonar-scanner-msbuild/issues/776
        var dummyQpRulesetPath = CreateValidEmptyRuleset(context.InputFolder, "dummyQp");
        var csprojFilePath = context.CreateProjectFile(
            CreateProjectSnippet(
                sonarErrorLog: null,
                useRazorSourceGenerator: "false",
                razorCompilationErrorLog: null,
                razorCompile: null,
                "<EnableDefaultCompileItems>true</EnableDefaultCompileItems>",
                $"<CodeAnalysisRuleSet>{dummyQpRulesetPath}</CodeAnalysisRuleSet>",
                "<ImportMicrosoftCSharpTargets>false</ImportMicrosoftCSharpTargets>",
                "<TargetFramework>net5</TargetFramework>",
                "<DesignTimeBuild>true</DesignTimeBuild>", // Prevent references resolution
                "<GenerateDependencyFile>false</GenerateDependencyFile>",
                "<GenerateRuntimeConfigurationFiles>false</GenerateRuntimeConfigurationFiles>"));
        File.WriteAllText(csprojFilePath, File.ReadAllText(csprojFilePath).Replace(@"Sdk=""Microsoft.NET.Sdk""", @"Sdk=""Microsoft.NET.Sdk.Web"""));

        // Checks that should succeed irrespective of the MSBuild version
        var result = BuildRunner.BuildTargets(TestContext, csprojFilePath, TargetConstants.Restore, TargetConstants.DefaultBuild);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild);
        result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);

        result.AssertTargetOrdering(
            TargetConstants.SonarResolveReferences,
            TargetConstants.SonarOverrideRunAnalyzers,
            TargetConstants.BeforeCompile,
            TargetConstants.ResolveCodeAnalysisRuleSet,
            TargetConstants.SonarCategoriseProject,
            TargetConstants.OverrideRoslynAnalysis,
            TargetConstants.SetRoslynAnalysisProperties,
            TargetConstants.CoreCompile,
            TargetConstants.InvokeSonarWriteProjectData_RazorProject,
            TargetConstants.SonarWriteProjectData,
            TargetConstants.SonarPrepareRazorProjectCodeAnalysis,
            TargetConstants.RazorCoreCompile,
            TargetConstants.SonarFinishRazorProjectCodeAnalysis,
            TargetConstants.DefaultBuild);
    }

    private static void AssertExpectedErrorLog(BuildLog result, string expectedErrorLog)
    {
        result.AssertPropertyValue(TargetProperties.SonarErrorLog, "OriginalValueFromFirstBuild.json"); // SetRazorCodeAnalysisProperties target doesn't change it
        result.AssertPropertyValue(TargetProperties.ErrorLog, expectedErrorLog);
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLog, expectedErrorLog);
        result.AssertPropertyValue(TargetProperties.RazorCompilationErrorLog, expectedErrorLog);
    }

    private static string CreateProjectSnippet(
        string sonarErrorLog = "",
        string useRazorSourceGenerator = "false",
        string razorCompilationErrorLog = "",
        string razorCompile = "SomeRandomValue",
        params string[] additionalProperties)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<PropertyGroup>");
        if (sonarErrorLog is not null)
        {
            sb.AppendLine($"  <SonarErrorLog>{sonarErrorLog}</SonarErrorLog>");
        }
        if (razorCompilationErrorLog is not null)
        {
            sb.AppendLine($"  <RazorCompilationErrorLog>{razorCompilationErrorLog}</RazorCompilationErrorLog>");
        }
        if (useRazorSourceGenerator is not null)
        {
            sb.AppendLine($"  <UseRazorSourceGenerator>{useRazorSourceGenerator}</UseRazorSourceGenerator>");
        }
        foreach (var prop in additionalProperties)
        {
            sb.AppendLine(prop);
        }
        sb.AppendLine("</PropertyGroup>");
        sb.AppendLine();
        if (razorCompile is not null)
        {
            sb.AppendLine("<ItemGroup>");
            sb.AppendLine($"  <RazorCompile Include='{razorCompile}'/>");
            sb.AppendLine("</ItemGroup>");
        }

        return sb.ToString();
    }
}
