/*
 * SonarScanner for .NET
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

using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Tasks.IntegrationTests;
using TestUtilities;

namespace SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class RazorTargetTests
    {
        private const string TestSpecificImport = "<Import Project='$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), Capture.targets))Capture.targets' />";
        private const string TestSpecificProperties =
@"<SonarQubeConfigPath>PROJECT_DIRECTORY_PATH</SonarQubeConfigPath>
  <SonarQubeTempPath>PROJECT_DIRECTORY_PATH</SonarQubeTempPath>";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Razor_CheckErrorLogProperties()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarQubeOutputPath>{rootOutputFolder}</SonarQubeOutputPath>
  <SonarErrorLog>OriginalValueFromFirstBuild.json</SonarErrorLog>
  <!-- Value used in Sdk.Razor.CurrentVersion.targets -->
  <RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>
</PropertyGroup>

<ItemGroup>
  <RazorCompile Include='SomeRandomValue'>
  </RazorCompile>
</ItemGroup>
";
            var filePath = CreateProjectFile(null, projectSnippet, TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

            // Assert
            result.AssertTargetExecuted(TargetConstants.SonarPrepareRazorProjectCodeAnalysis);
            AssertExpectedErrorLog(result, rootOutputFolder + @"\0\Issues.Views.json");
        }

        [TestMethod]
        public void Razor_PreserveRazorCompilationErrorLog()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarQubeOutputPath>{rootOutputFolder}</SonarQubeOutputPath>
  <SonarErrorLog>OriginalValueFromFirstBuild.json</SonarErrorLog>
  <RazorCompilationErrorLog>C:\UserDefined.json</RazorCompilationErrorLog>
  <!-- Value used in Sdk.Razor.CurrentVersion.targets -->
  <RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>
</PropertyGroup>

<ItemGroup>
  <RazorCompile Include='SomeRandomValue'>
  </RazorCompile>
</ItemGroup>
";
            var filePath = CreateProjectFile(null, projectSnippet, TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

            // Assert
            result.AssertTargetExecuted(TargetConstants.SonarPrepareRazorProjectCodeAnalysis);
            AssertExpectedErrorLog(result, @"C:\UserDefined.json");
        }

        [TestMethod]
        public void Razor_ExcludedProject_NoErrorLog()
        {
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarQubeExclude>true</SonarQubeExclude>
  <SonarErrorLog>OriginalValueFromFirstBuild.json</SonarErrorLog>
</PropertyGroup>

<ItemGroup>
  <RazorCompile Include='SomeRandomValue'>
  </RazorCompile>
</ItemGroup>
";
            var filePath = CreateProjectFile(null, projectSnippet, TargetConstants.OverrideRoslynAnalysis);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);

            // Assert
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
            AssertExpectedErrorLog(result, string.Empty);
        }

        [TestMethod]
        public void Razor_RazorSpecificOutputAndProjectInfo_AreCopiedToCorrectFolders()
        {
            // Arrange
            var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var projectSpecificOutDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "0");
            var temporaryProjectSpecificOutDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, @"0.tmp");
            var razorSpecificOutDir = Path.Combine(root, "0.Razor");
            TestUtils.CreateEmptyFile(temporaryProjectSpecificOutDir, "Issues.FromMainBuild.json");
            TestUtils.CreateEmptyFile(projectSpecificOutDir, "Issues.FromRazorBuild.json");
            var razorIssuesPath = Path.Combine(razorSpecificOutDir, "Issues.FromRazorBuild.json");

            var projectSnippet = $@"
<PropertyGroup>
  <SonarTemporaryProjectSpecificOutDir>{temporaryProjectSpecificOutDir}</SonarTemporaryProjectSpecificOutDir>
  <ProjectSpecificOutDir>{projectSpecificOutDir}</ProjectSpecificOutDir>
  <RazorSonarErrorLogName>Issues.FromRazorBuild.json</RazorSonarErrorLogName>
</PropertyGroup>

<ItemGroup>
  <RazorCompile Include='SomeRandomValue'>
  </RazorCompile>
</ItemGroup>
";

            var filePath = CreateProjectFile(null, projectSnippet, TargetConstants.SonarFinishRazorProjectCodeAnalysis);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarFinishRazorProjectCodeAnalysis);

            // Assert
            var razorProjectInfo = ProjectInfoAssertions.AssertProjectInfoExists(root, filePath);
            result.AssertTargetExecuted(TargetConstants.SonarFinishRazorProjectCodeAnalysis);
            razorProjectInfo.AnalysisSettings.Single(x => x.Id.Equals("sonar.cs.analyzer.projectOutPaths")).Value.Should().Be(razorSpecificOutDir);
            razorProjectInfo.AnalysisSettings.Single(x => x.Id.Equals("sonar.cs.roslyn.reportFilePaths")).Value.Should().Be(razorIssuesPath);
            Directory.Exists(temporaryProjectSpecificOutDir).Should().BeFalse();
            File.Exists(Path.Combine(projectSpecificOutDir, "Issues.FromMainBuild.json")).Should().BeTrue();
            File.Exists(Path.Combine(razorSpecificOutDir, "Issues.FromRazorBuild.json")).Should().BeTrue();
        }

        [TestMethod]
        public void Razor_RazorSpecificOutputAndProjectInfo_PreserveUserDefinedErrorLogValue()
        {
            // Arrange
            var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var projectSpecificOutDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "0");
            var temporaryProjectSpecificOutDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, @"0.tmp");
            var razorSpecificOutDir = Path.Combine(root, "0.Razor");
            var userDefinedErrorLog = TestUtils.CreateEmptyFile(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "User"), "UserDefined.FromRazorBuild.json");

            var projectSnippet = $@"
<PropertyGroup>
  <!-- This value is considered to be set by user when $(RazorSonarErrorLogName) is empty -->
  <RazorSonarErrorLog>{userDefinedErrorLog}</RazorSonarErrorLog>
  <SonarTemporaryProjectSpecificOutDir>{temporaryProjectSpecificOutDir}</SonarTemporaryProjectSpecificOutDir>
  <ProjectSpecificOutDir>{projectSpecificOutDir}</ProjectSpecificOutDir>
</PropertyGroup>

<ItemGroup>
  <RazorCompile Include='SomeRandomValue'>
  </RazorCompile>
</ItemGroup>
";

            var filePath = CreateProjectFile(null, projectSnippet, TargetConstants.SonarFinishRazorProjectCodeAnalysis);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarFinishRazorProjectCodeAnalysis);

            // Assert
            var actualProjectInfo = ProjectInfoAssertions.AssertProjectInfoExists(root, filePath);
            result.AssertTargetExecuted(TargetConstants.SonarFinishRazorProjectCodeAnalysis);
            actualProjectInfo.AnalysisSettings.Single(x => x.Id.Equals("sonar.cs.analyzer.projectOutPaths")).Value.Should().Be(razorSpecificOutDir);
            actualProjectInfo.AnalysisSettings.Single(x => x.Id.Equals("sonar.cs.roslyn.reportFilePaths")).Value.Should().Be(userDefinedErrorLog);
            Directory.Exists(temporaryProjectSpecificOutDir).Should().BeFalse();
            File.Exists(userDefinedErrorLog).Should().BeTrue();
        }

        [TestMethod]
        public void Razor_ExcludedProject_PreserveRazorCompilationErrorLog()
        {
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarQubeExclude>true</SonarQubeExclude>
  <SonarErrorLog>OriginalValueFromFirstBuild.json</SonarErrorLog>
  <RazorCompilationErrorLog>C:\UserDefined.json</RazorCompilationErrorLog>
</PropertyGroup>";
            var filePath = CreateProjectFile(null, projectSnippet, TargetConstants.OverrideRoslynAnalysis);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);

            // Assert
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
            result.AssertExpectedCapturedPropertyValue(TargetProperties.SonarErrorLog, "OriginalValueFromFirstBuild.json");   // SetRazorCodeAnalysisProperties target doesn't change it
            result.AssertExpectedCapturedPropertyValue(TargetProperties.ErrorLog, string.Empty);
            result.AssertExpectedCapturedPropertyValue(TargetProperties.RazorSonarErrorLog, string.Empty);
            result.AssertExpectedCapturedPropertyValue(TargetProperties.RazorCompilationErrorLog, @"C:\UserDefined.json");
        }

        [TestMethod]
        [Description("Checks the targets are executed in the expected order")]
        public void TargetExecutionOrderForRazor()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");
            TestUtils.CreateEmptyFile(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext), "Index.cshtml");

            // We need to set the CodeAnalyisRuleSet property if we want ResolveCodeAnalysisRuleSet
            // to be executed. See test bug https://github.com/SonarSource/sonar-scanner-msbuild/issues/776
            var dummyQpRulesetPath = TestUtils.CreateValidEmptyRuleset(rootInputFolder, "dummyQp");

            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarQubeOutputPath>{rootInputFolder}</SonarQubeOutputPath>
  <SonarQubeConfigPath>{rootOutputFolder}</SonarQubeConfigPath>
  <CodeAnalysisRuleSet>{dummyQpRulesetPath}</CodeAnalysisRuleSet>
  <ImportMicrosoftCSharpTargets>false</ImportMicrosoftCSharpTargets>
  <TargetFramework>net5</TargetFramework>
  <!-- Prevent references resolution -->
  <DesignTimeBuild>true</DesignTimeBuild>
  <GenerateDependencyFile>false</GenerateDependencyFile>
  <GenerateRuntimeConfigurationFiles>false</GenerateRuntimeConfigurationFiles>
</PropertyGroup>
";
            var txtFilePath = CreateProjectFile(null, projectSnippet, string.Empty);
            var csprojFilePath = txtFilePath + ".csproj";
            File.WriteAllText(csprojFilePath, File.ReadAllText(txtFilePath).Replace("<Project ", @"<Project Sdk=""Microsoft.NET.Sdk.Web"" "));

            // Act
            var result = BuildRunner.BuildTargets(TestContext, csprojFilePath, TargetConstants.DefaultBuild);

            // Assert
            // Checks that should succeed irrespective of the MSBuild version
            result.AssertTargetSucceeded(TargetConstants.DefaultBuild);
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);

            result.AssertExpectedTargetOrdering(
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
            result.AssertExpectedCapturedPropertyValue(TargetProperties.SonarErrorLog, "OriginalValueFromFirstBuild.json");   // SetRazorCodeAnalysisProperties target doesn't change it
            result.AssertExpectedCapturedPropertyValue(TargetProperties.ErrorLog, expectedErrorLog);
            result.AssertExpectedCapturedPropertyValue(TargetProperties.RazorSonarErrorLog, expectedErrorLog);
            result.AssertExpectedCapturedPropertyValue(TargetProperties.RazorCompilationErrorLog, expectedErrorLog);
        }

        private string CreateProjectFile(AnalysisConfig config, string projectSnippet, string afterTargets)
        {
            var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var targetTestUtils = new TargetsTestsUtils(TestContext);
            var projectTemplate = targetTestUtils.GetProjectTemplate(config, projectDirectory, TestSpecificProperties, projectSnippet, TestSpecificImport);

            targetTestUtils.CreateCaptureDataTargetsFile(projectDirectory, afterTargets);

            return targetTestUtils.CreateProjectFile(projectDirectory, projectTemplate);
        }
    }
}
