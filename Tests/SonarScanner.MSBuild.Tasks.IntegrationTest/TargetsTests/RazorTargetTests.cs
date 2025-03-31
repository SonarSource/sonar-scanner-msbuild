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

using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Tasks.IntegrationTest;
using TestUtilities;

namespace SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests;

[TestClass]
public class RazorTargetTests
{
    private const string TestSpecificProperties =
@"<SonarQubeConfigPath>PROJECT_DIRECTORY_PATH</SonarQubeConfigPath>
  <SonarQubeTempPath>PROJECT_DIRECTORY_PATH</SonarQubeTempPath>";

    private static readonly char Separator = Path.DirectorySeparatorChar;

    public TestContext TestContext { get; set; }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void SonarPrepareRazorProjectCodeAnalysis_WhenNoSonarErrorLog_NoPropertiesAreSet()
    {
        // Arrange
        var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
        var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

        var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarQubeOutputPath>{rootOutputFolder}</SonarQubeOutputPath>
  <SonarErrorLog></SonarErrorLog>
  <!-- Value used in Sdk.Razor.CurrentVersion.targets -->
  <RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>
  <UseRazorSourceGenerator>false</UseRazorSourceGenerator>
</PropertyGroup>

<ItemGroup>
  <RazorCompile Include='SomeRandomValue'>
  </RazorCompile>
</ItemGroup>
";
        var filePath = CreateProjectFile(null, projectSnippet);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

        // Assert
        result.AssertTargetExecuted(TargetConstants.SonarPrepareRazorProjectCodeAnalysis);
        result.AssertPropertyValue(TargetProperties.SonarErrorLog, string.Empty);   // SetRazorCodeAnalysisProperties target doesn't change it
        result.AssertPropertyValue(TargetProperties.ErrorLog, null);
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLog, null);
        result.AssertPropertyValue(TargetProperties.RazorCompilationErrorLog, null);
        result.AssertPropertyValue(TargetProperties.SonarTemporaryProjectSpecificOutDir, $"{rootOutputFolder}{Separator}0.tmp");

        result.AssertItemGroupCount(TargetItemGroups.CoreCompileOutFiles, 1); // ProjectInfo.xml
    }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void SonarPrepareRazorProjectCodeAnalysis_WhenSonarErrorLogSet_SetsRazorErrorLogProperties()
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
  <UseRazorSourceGenerator>false</UseRazorSourceGenerator>
</PropertyGroup>

<ItemGroup>
  <RazorCompile Include='SomeRandomValue'>
  </RazorCompile>
</ItemGroup>
";
        var filePath = CreateProjectFile(null, projectSnippet);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

        // Assert
        result.AssertTargetExecuted(TargetConstants.SonarPrepareRazorProjectCodeAnalysis);
        AssertExpectedErrorLog(result, rootOutputFolder + $@"{Separator}0{Separator}Issues.Views.json");
        result.AssertPropertyValue(TargetProperties.SonarTemporaryProjectSpecificOutDir, $"{rootOutputFolder}{Separator}0.tmp");

        result.AssertItemGroupCount(TargetItemGroups.CoreCompileOutFiles, 1); // ProjectInfo.xml
    }

    [TestCategory("NoUnixNeedsReview")]
    [DataTestMethod]
    [DataRow(0, null)]
    [DataRow(1, "OriginalValueFromFirstBuild.json")]
    public void SonarPrepareRazorProjectCodeAnalysis_WithSourceGenerators_NotExecuted(int index, string sonarErrorLogValue)
    {
        // Arrange
        var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, $"Inputs{index}");
        var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, $"Outputs{index}");

        var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarQubeOutputPath>{rootOutputFolder}</SonarQubeOutputPath>
  <SonarErrorLog>{sonarErrorLogValue}</SonarErrorLog>
  <!-- Value used in Sdk.Razor.CurrentVersion.targets -->
  <RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>
  <UseRazorSourceGenerator>true</UseRazorSourceGenerator>
</PropertyGroup>

<ItemGroup>
  <RazorCompile Include='SomeRandomValue'>
  </RazorCompile>
</ItemGroup>
";
        var filePath = CreateProjectFile(null, projectSnippet);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

        // Assert
        result.AssertTargetNotExecuted(TargetConstants.SonarPrepareRazorProjectCodeAnalysis);
    }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void SonarPrepareRazorProjectCodeAnalysis_PreserveRazorCompilationErrorLog()
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
        var filePath = CreateProjectFile(null, projectSnippet);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

        // Assert
        result.AssertTargetExecuted(TargetConstants.SonarPrepareRazorProjectCodeAnalysis);
        AssertExpectedErrorLog(result, @"C:\UserDefined.json");
        result.AssertPropertyValue(TargetProperties.SonarTemporaryProjectSpecificOutDir, $"{rootOutputFolder}{Separator}0.tmp");

        result.AssertItemGroupCount(TargetItemGroups.CoreCompileOutFiles, 1); // ProjectInfo.xml
    }

    [TestCategory("NoUnixNeedsReview")]
    [DataTestMethod]
    [DataRow(0, null)]
    [DataRow(1, "OriginalValueFromFirstBuild.json")]
    public void SonarPrepareRazorProjectCodeAnalysis_CreatesTempFolderAndPreservesMainFolder(int index, string sonarErrorLogValue)
    {
        // Arrange
        var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, $"Inputs{index}");
        var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, $"Outputs{index}");

        var testTargetName = "CreateDirectoryAndFile";
        var subDirName = "foo";
        var subDirFileName = "bar.txt";

        var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarQubeOutputPath>{rootOutputFolder}</SonarQubeOutputPath>
  <SonarErrorLog>{sonarErrorLogValue}</SonarErrorLog>
  <!-- Value used in Sdk.Razor.CurrentVersion.targets -->
  <RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>
</PropertyGroup>

<Target Name=""{testTargetName}"" AfterTargets=""SonarCreateProjectSpecificDirs"" BeforeTargets=""SonarPrepareRazorProjectCodeAnalysis"">
    <!-- I do not use properties for the paths to keep the properties strictly to what is needed by the target under test -->
    <MakeDir Directories=""$(ProjectSpecificOutDir){Separator}{subDirName}"" />
    <WriteLinesToFile File=""$(ProjectSpecificOutDir){Separator}{subDirName}{Separator}{subDirFileName}"" Lines=""foobar"" />
</Target>

<ItemGroup>
  <RazorCompile Include='SomeRandomValue'>
  </RazorCompile>
</ItemGroup>
";
        var filePath = CreateProjectFile(null, projectSnippet);

        // Act
        var result = BuildRunner.BuildTargets(
            TestContext,
            filePath,
            // we need to explicitly pass this targets in order for the Directory to get generated before executing the 'testTarget'
            // otherwise, 'SonarCreateProjectSpecificDirs' gets executed only when invoked by 'SonarPrepareRazorProjectCodeAnalysis', after 'testTarget'
            TargetConstants.SonarCreateProjectSpecificDirs,
            testTargetName,
            TargetConstants.SonarPrepareRazorProjectCodeAnalysis);

        // Assert
        result.AssertTargetExecuted(TargetConstants.SonarPrepareRazorProjectCodeAnalysis);
        var specificOutputDir = Path.Combine(rootOutputFolder, "0");
        // main folder should still be on disk
        Directory.Exists(specificOutputDir).Should().BeTrue();
        File.Exists(Path.Combine(specificOutputDir, "ProjectInfo.xml")).Should().BeFalse();

        // contents should be moved to temporary folder
        var temporaryProjectSpecificOutDir = Path.Combine(rootOutputFolder, "0.tmp");
        result.AssertPropertyValue(TargetProperties.SonarTemporaryProjectSpecificOutDir, temporaryProjectSpecificOutDir);
        result.AssertItemGroupCount(TargetItemGroups.CoreCompileOutFiles, 2); // ProjectInfo.xml and bar.txt
        Directory.Exists(temporaryProjectSpecificOutDir).Should().BeTrue();
        File.Exists(Path.Combine(temporaryProjectSpecificOutDir, "ProjectInfo.xml")).Should().BeTrue();
        // the dir and file should have been moved as well
        File.Exists(Path.Combine(temporaryProjectSpecificOutDir, subDirName, subDirFileName)).Should().BeTrue();
        result.Messages.Should().ContainMatch($@"Sonar: Preparing for Razor compilation, moved files (*{Separator}foo{Separator}bar.txt;*{Separator}ProjectInfo.xml) to *{Separator}0.tmp.");
    }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void SonarFinishRazorProjectCodeAnalysis_WithSourceGenerators_NotExecuted()
    {
        // Arrange
        var projectSpecificOutDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "0");
        var temporaryProjectSpecificOutDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, @"0.tmp");
        TestUtils.CreateEmptyFile(temporaryProjectSpecificOutDir, "Issues.FromMainBuild.json");

        var projectSnippet = $@"
<PropertyGroup>
  <SonarTemporaryProjectSpecificOutDir>{temporaryProjectSpecificOutDir}</SonarTemporaryProjectSpecificOutDir>
  <ProjectSpecificOutDir>{projectSpecificOutDir}</ProjectSpecificOutDir>
  <RazorSonarErrorLogName>Issues.FromRazorBuild.json</RazorSonarErrorLogName>
  <UseRazorSourceGenerator>true</UseRazorSourceGenerator>
</PropertyGroup>

<ItemGroup>
  <RazorCompile Include='SomeRandomValue'>
  </RazorCompile>
</ItemGroup>
";

        var filePath = CreateProjectFile(null, projectSnippet);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarFinishRazorProjectCodeAnalysis);

        // Assert
        result.AssertTargetNotExecuted(TargetConstants.SonarFinishRazorProjectCodeAnalysis);
    }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void SonarFinishRazorProjectCodeAnalysis_WhenRazorSonarErrorLogOrLogNameAreNotSet_DoesNotCreateAnalysisSettings()
    {
        // Arrange
        var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var projectSpecificOutDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "0");
        var temporaryProjectSpecificOutDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, @"0.tmp");
        var razorSpecificOutDir = Path.Combine(root, "0.Razor");

        var projectSnippet = $@"
<PropertyGroup>
  <!-- This should not happen as long as SonarPrepareRazorProjectCodeAnalysis works as expected -->
  <RazorSonarErrorLog></RazorSonarErrorLog>
  <RazorSonarErrorLogName></RazorSonarErrorLogName>
  <SonarTemporaryProjectSpecificOutDir>{temporaryProjectSpecificOutDir}</SonarTemporaryProjectSpecificOutDir>
  <ProjectSpecificOutDir>{projectSpecificOutDir}</ProjectSpecificOutDir>
</PropertyGroup>

<ItemGroup>
  <RazorCompile Include='SomeRandomValue'>
  </RazorCompile>
</ItemGroup>
";

        var filePath = CreateProjectFile(null, projectSnippet);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarFinishRazorProjectCodeAnalysis);

        // Assert
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

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void SonarFinishRazorProjectCodeAnalysis_RazorSpecificOutputAndProjectInfo_AreCopiedToCorrectFolders()
    {
        // Arrange
        var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var projectSpecificOutDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "0");
        var temporaryProjectSpecificOutDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "0.tmp");
        var razorSpecificOutDir = Path.Combine(root, "0.Razor");
        TestUtils.CreateEmptyFile(temporaryProjectSpecificOutDir, "Issues.FromMainBuild.json");
        TestUtils.CreateEmptyFile(projectSpecificOutDir, "Issues.FromRazorBuild.json");
        var razorIssuesPath = Path.Combine(razorSpecificOutDir, "Issues.FromRazorBuild.json");

        var testTargetName = "CreateDirectoryAndFile";
        var subDirName = "foo";
        var subDirFileName = "bar.txt";

        // RazorSonarErrorLog is set to the MSBuild $(RazorCompilationErrorLog) value
        // RazorSonarErrorLogName is set when the $(RazorCompilationErrorLog) is not set / empty
        var projectSnippet = $@"
<PropertyGroup>
  <SonarTemporaryProjectSpecificOutDir>{temporaryProjectSpecificOutDir}</SonarTemporaryProjectSpecificOutDir>
  <ProjectSpecificOutDir>{projectSpecificOutDir}</ProjectSpecificOutDir>
  <RazorSonarErrorLogName>Issues.FromRazorBuild.json</RazorSonarErrorLogName>
  <RazorSonarErrorLog></RazorSonarErrorLog> <!-- make it explicit in test that this won't be set when the RazorSonarErrorLogName is set -->
</PropertyGroup>
<Target Name=""{testTargetName}"">
    <!-- I do not use properties for the paths to keep the properties strictly to what is needed by the target under test -->
    <MakeDir Directories = ""$(ProjectSpecificOutDir){Separator}{subDirName}"" />
    <WriteLinesToFile File=""$(ProjectSpecificOutDir){Separator}{subDirName}{Separator}{subDirFileName}"" Lines = ""foobar"" />
</Target>

<ItemGroup>
  <RazorCompile Include='SomeRandomValue'>
  </RazorCompile>
</ItemGroup>
";

        var filePath = CreateProjectFile(null, projectSnippet);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, filePath, testTargetName, TargetConstants.SonarFinishRazorProjectCodeAnalysis);

        // Assert
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
        result.Messages.Should().Contain(s =>
            s.Contains("Sonar: After Razor compilation, moved files (")
            && s.Contains($@"{Separator}0{Separator}foo{Separator}bar.txt")
            && s.Contains($@"{Separator}0{Separator}Issues.FromRazorBuild.json")
            && s.Contains($@"{Separator}0.Razor."));
        result.Messages.Should().ContainMatch($@"Sonar: After Razor compilation, moved files (*{Separator}0.tmp{Separator}Issues.FromMainBuild.json) to *{Separator}0 and will remove the temporary folder.");
        result.Messages.Should().ContainMatch($@"Removing directory ""*{Separator}0.tmp"".");

        result.AssertPropertyValue(TargetProperties.RazorSonarProjectSpecificOutDir, razorSpecificOutDir);
        result.AssertPropertyValue(TargetProperties.RazorSonarProjectInfo, $"{razorSpecificOutDir}{Separator}ProjectInfo.xml");
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLog, $"{razorSpecificOutDir}{Separator}Issues.FromRazorBuild.json");
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLogExists, "true");
        result.AssertItemGroupCount(TargetItemGroups.RazorCompilationOutFiles, 2); // ProjectInfo.xml and bar.txt
        result.AssertItemGroupCount(TargetItemGroups.SonarTempFiles, 1);
        result.AssertItemGroupCount(TargetItemGroups.RazorSonarReportFilePath, 1);
        result.AssertItemGroupCount(TargetItemGroups.RazorSonarQubeSetting, 2);
    }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void SonarFinishRazorProjectCodeAnalysis_WithRazorSpecificOutputAndProjectInfo_PreserveUserDefinedErrorLogValue()
    {
        // Arrange
        var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var projectSpecificOutDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "0");
        var temporaryProjectSpecificOutDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, @"0.tmp");
        var razorSpecificOutDir = Path.Combine(root, "0.Razor");
        var userDefinedErrorLog = TestUtils.CreateEmptyFile(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "User"), "UserDefined.FromRazorBuild.json");

        // RazorSonarErrorLog is set to the MSBuild $(RazorCompilationErrorLog) value
        // RazorSonarErrorLogName is set when the $(RazorCompilationErrorLog) is not set / empty
        var projectSnippet = $@"
<PropertyGroup>
  <!-- This value is considered to be set by user when $(RazorSonarErrorLogName) is empty -->
  <RazorSonarErrorLog>{userDefinedErrorLog}</RazorSonarErrorLog>
  <RazorSonarErrorLogName></RazorSonarErrorLogName> <!-- make it explicit in test that this won't be set when the RazorSonarErrorLog is set -->
  <SonarTemporaryProjectSpecificOutDir>{temporaryProjectSpecificOutDir}</SonarTemporaryProjectSpecificOutDir>
  <ProjectSpecificOutDir>{projectSpecificOutDir}</ProjectSpecificOutDir>
</PropertyGroup>

<ItemGroup>
  <RazorCompile Include='SomeRandomValue'>
  </RazorCompile>
</ItemGroup>
";

        var filePath = CreateProjectFile(null, projectSnippet);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarFinishRazorProjectCodeAnalysis);

        // Assert
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

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void OverrideRoslynAnalysis_ExcludedProject_NoErrorLog()
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
        var filePath = CreateProjectFile(null, projectSnippet);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);

        // Assert
        result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
        AssertExpectedErrorLog(result, null);
    }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void OverrideRoslynAnalysis_ExcludedProject_PreserveRazorCompilationErrorLog()
    {
        var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");

        var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarQubeExclude>true</SonarQubeExclude>
  <SonarErrorLog>OriginalValueFromFirstBuild.json</SonarErrorLog>
  <RazorCompilationErrorLog>C:\UserDefined.json</RazorCompilationErrorLog>
</PropertyGroup>";
        var filePath = CreateProjectFile(null, projectSnippet);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);

        // Assert
        result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
        result.AssertPropertyValue(TargetProperties.SonarErrorLog, "OriginalValueFromFirstBuild.json");   // SetRazorCodeAnalysisProperties target doesn't change it
        result.AssertPropertyValue(TargetProperties.ErrorLog, null);
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLog, null);
        result.AssertPropertyValue(TargetProperties.RazorCompilationErrorLog, @"C:\UserDefined.json");
    }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    [Description("Checks the targets are executed in the expected order")]
    public void TargetExecutionOrderForRazor()
    {
        // Arrange
        var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
        var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");
        TestUtils.CreateEmptyFile(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext), "Index.cshtml");

        // We need to set the CodeAnalysisRuleSet property if we want ResolveCodeAnalysisRuleSet
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
        var txtFilePath = CreateProjectFile(null, projectSnippet);
        var csprojFilePath = txtFilePath + ".csproj";
        File.WriteAllText(csprojFilePath, File.ReadAllText(txtFilePath).Replace("<Project ", @"<Project Sdk=""Microsoft.NET.Sdk.Web"" "));

        // Act
        var result = BuildRunner.BuildTargets(TestContext, csprojFilePath, TargetConstants.Restore, TargetConstants.DefaultBuild);

        // Assert
        // Checks that should succeed irrespective of the MSBuild version
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
        result.AssertPropertyValue(TargetProperties.SonarErrorLog, "OriginalValueFromFirstBuild.json");   // SetRazorCodeAnalysisProperties target doesn't change it
        result.AssertPropertyValue(TargetProperties.ErrorLog, expectedErrorLog);
        result.AssertPropertyValue(TargetProperties.RazorSonarErrorLog, expectedErrorLog);
        result.AssertPropertyValue(TargetProperties.RazorCompilationErrorLog, expectedErrorLog);
    }

    private string CreateProjectFile(AnalysisConfig config, string projectSnippet)
    {
        var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var targetTestUtils = new TargetsTestsUtils(TestContext);
        var projectTemplate = targetTestUtils.GetProjectTemplate(config, projectDirectory, TestSpecificProperties, projectSnippet);
        return targetTestUtils.CreateProjectFile(projectDirectory, projectTemplate);
    }
}
