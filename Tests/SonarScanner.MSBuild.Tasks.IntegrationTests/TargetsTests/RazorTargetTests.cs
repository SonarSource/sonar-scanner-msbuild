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

using System.IO;
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
            var projectSpecificOutDir = Path.Combine(root, "0");
            var temporaryProjectSpecificOutDir = Path.Combine(root, "0.tmp");
            var razorSpecificOutDir = Path.Combine(root, "0.Razor");
            Directory.CreateDirectory(projectSpecificOutDir);
            Directory.CreateDirectory(temporaryProjectSpecificOutDir);
            File.WriteAllText(Path.Combine(projectSpecificOutDir, "ProjectInfo.xml"),
$@"
<ProjectInfo>
  <AnalysisSettings>
    <Property Name = ""sonar.cs.roslyn.reportFilePaths"" > C:\SonarSource\Sandbox\Mixed\Mixed\Main2\bin\Debug\netcoreapp3.1\Main2.dll.RoslynCA.json | C:\SonarSource\Sandbox\Mixed\Mixed\Main2\bin\Debug\netcoreapp3.1\Main2.dll.RoslynCA.json </ Property >
    <Property Name = ""sonar.cs.analyzer.projectOutPaths"" >{projectSpecificOutDir}</Property>
  </ AnalysisSettings >
  <Configuration > Debug </ Configuration >
  <Platform > AnyCPU </ Platform >
  <TargetFramework > netcoreapp3.1 </TargetFramework>
</ProjectInfo> ");
            File.WriteAllText(Path.Combine(temporaryProjectSpecificOutDir, "ProtoBuf.txt"), string.Empty);
            var razorSpecificProjectInfo = Path.Combine(razorSpecificOutDir, "ProjectInfo.xml");

            var projectSnippet = $@"
<PropertyGroup>
  <SonarTemporaryProjectSpecificOutDir>{temporaryProjectSpecificOutDir}</SonarTemporaryProjectSpecificOutDir>
  <ProjectSpecificOutDir>{projectSpecificOutDir}</ProjectSpecificOutDir>
</PropertyGroup>
";

            var filePath = CreateProjectFile(null, projectSnippet, TargetConstants.RazorSonarCopyProjectInfoFile);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.RazorSonarCopyProjectInfoFile);

            // Assert
            result.AssertTargetExecuted(TargetConstants.RazorSonarCopyProjectInfoFile);
            File.Exists(Path.Combine(projectSpecificOutDir, "ProtoBuf.txt")).Should().BeTrue();
            File.Exists(razorSpecificProjectInfo).Should().BeTrue();
            File.ReadAllText(razorSpecificProjectInfo).Should().Contain($@"<Property Name = ""sonar.cs.analyzer.projectOutPaths"" >{razorSpecificOutDir}</Property>");
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
