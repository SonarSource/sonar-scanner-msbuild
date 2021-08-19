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

using System.Collections.Generic;
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
        private const string TestSpecificProperties = @"<SonarQubeConfigPath>PROJECT_DIRECTORY_PATH</SonarQubeConfigPath>
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
  <ProjectSpecificOutDir>{rootOutputFolder}</ProjectSpecificOutDir>
  <SonarErrorLog>OriginalValueFromFirstBuild.json</SonarErrorLog>
  <!-- Value used in Sdk.Razor.CurrentVersion.targets -->
  <RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>
  <RazorCompileOnBuild>true</RazorCompileOnBuild>
</PropertyGroup>
";
            var filePath = CreateProjectFile(null, projectSnippet, TargetConstants.SonarPrepareRazorCodeAnalysis);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarPrepareRazorCodeAnalysis);

            // Assert
            result.AssertTargetExecuted(TargetConstants.SonarPrepareRazorCodeAnalysis);
            AssertExpectedErrorLog(result, rootOutputFolder + @"\Issues.Views.json");
        }

        [TestMethod]
        public void Razor_PreserveRazorCompilationErrorLog()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            var destinationFolder = rootOutputFolder + ".tmp";
            File.WriteAllText(Path.Combine(rootOutputFolder, "ProtoBuf.txt"), string.Empty);

            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <ProjectSpecificOutDir>{rootOutputFolder}</ProjectSpecificOutDir>
  <SonarErrorLog>OriginalValueFromFirstBuild.json</SonarErrorLog>
  <RazorCompilationErrorLog>C:\UserDefined.json</RazorCompilationErrorLog>
  <!-- Value used in Sdk.Razor.CurrentVersion.targets -->
  <RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>
  <RazorCompileOnBuild>true</RazorCompileOnBuild>
</PropertyGroup>
";
            var filePath = CreateProjectFile(null, projectSnippet, TargetConstants.SonarPrepareRazorCodeAnalysis);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarPrepareRazorCodeAnalysis);

            // Assert
            result.AssertTargetExecuted(TargetConstants.SonarPrepareRazorCodeAnalysis);
            AssertExpectedErrorLog(result, @"C:\UserDefined.json");
            File.Exists(Path.Combine(destinationFolder, "ProtoBuf.txt")).Should().BeTrue();
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
  <RazorCompileOnBuild>true</RazorCompileOnBuild>
</PropertyGroup>";
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
            var protoBufFile = Path.Combine(temporaryProjectSpecificOutDir, "ProtoBuf.txt");
            var issues = Path.Combine(projectSpecificOutDir, "Issues.json");
            File.WriteAllText(protoBufFile, string.Empty);
            File.WriteAllText(issues, string.Empty);

            var projectSnippet = $@"
<PropertyGroup>
  <SonarTemporaryProjectSpecificOutDir>{temporaryProjectSpecificOutDir}</SonarTemporaryProjectSpecificOutDir>
  <ProjectSpecificOutDir>{projectSpecificOutDir}</ProjectSpecificOutDir>
  <RazorCompileOnBuild>true</RazorCompileOnBuild>
  <RazorSonarErrorLog>{issues}</RazorSonarErrorLog>
</PropertyGroup>
";

            var filePath = CreateProjectFile(null, projectSnippet, TargetConstants.SonarFinishRazorCodeAnalysis);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarFinishRazorCodeAnalysis);

            // Assert
            var actualProjectInfo = ProjectInfoAssertions.AssertProjectInfoExists(root, Path.Combine(root, "Razor_RazorSpecificOutputAndProjectInfo_AreCopiedToCorrectFolders.proj.txt"));
            result.AssertTargetExecuted(TargetConstants.SonarFinishRazorCodeAnalysis);
            File.Exists(Path.Combine(projectSpecificOutDir, "ProtoBuf.txt")).Should().BeTrue();
            actualProjectInfo.AnalysisSettings.FirstOrDefault(x => x.Id.Equals("sonar.cs.analyzer.projectOutPaths")).Value.Should().Be(razorSpecificOutDir);
            Directory.Exists(temporaryProjectSpecificOutDir).Should().BeFalse();
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
  <RazorCompileOnBuild>true</RazorCompileOnBuild>
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

            // We need to set the CodeAnalyisRuleSet property if we want ResolveCodeAnalysisRuleSet
            // to be executed. See test bug https://github.com/SonarSource/sonar-scanner-msbuild/issues/776
            var dummyQpRulesetPath = TestUtils.CreateValidEmptyRuleset(rootInputFolder, "dummyQp");

            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarQubeOutputPath>{rootInputFolder}</SonarQubeOutputPath>
  <SonarQubeConfigPath>{rootOutputFolder}</SonarQubeConfigPath>
  <CodeAnalysisRuleSet>{dummyQpRulesetPath}</CodeAnalysisRuleSet>
</PropertyGroup>

<ItemGroup>
  <SonarQubeSetting Include='sonar.other.setting'>
    <Value>other value</Value>
  </SonarQubeSetting>
</ItemGroup>
";

            var filePath = CreateRazorProjectFile(null, projectSnippet, string.Empty);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.DefaultBuild);

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
                TargetConstants.DefaultBuild,
                TargetConstants.SetRoslynResults,
                TargetConstants.SonarWriteProjectData);
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

        private string CreateRazorProjectFile(AnalysisConfig config, string projectSnippet, string afterTargets)
        {
            var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var targetTestUtils = new TargetsTestsUtils(TestContext);
            var projectTemplate = targetTestUtils.GetProjectTemplate(config, projectDirectory, TestSpecificProperties, projectSnippet, TestSpecificImport);
            projectTemplate = projectTemplate.Replace("</Project>", @"
  <PropertyGroup>
    <TargetFramework>net5</TargetFramework>
    <DesignTimeBuild>true</DesignTimeBuild>
    <GenerateDependencyFile>false</GenerateDependencyFile>
    <GenerateRuntimeConfigurationFiles>false</GenerateRuntimeConfigurationFiles>
  </PropertyGroup>
</Project>");
            projectTemplate = projectTemplate.Replace(@"    <OutputType>library</OutputType>", string.Empty);
            projectTemplate = projectTemplate.Replace("<Project ToolsVersion='Current'", @"<Project ToolsVersion='Current' Sdk=""Microsoft.NET.Sdk.Web""");
            File.WriteAllText(Path.Combine(projectDirectory, "Dummy.cshtml"), string.Empty);
            File.WriteAllText(Path.Combine(projectDirectory, "Program.cs"),
@"namespace AspNetCoreMvc5
{
    public class Program
    {
        public static void Main(string[] args)
        {

        }
    }
}");
            var projectFile = targetTestUtils.CreateProjectFile(projectDirectory, projectTemplate);
            targetTestUtils.CreateCaptureDataTargetsFile(projectDirectory, afterTargets);
            return projectFile;
        }
    }
}
