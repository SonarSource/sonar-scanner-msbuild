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
        public void Razor_ForProductProject_CheckErrorLogProperties()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <ProjectSpecificOutDir>{rootOutputFolder}</ProjectSpecificOutDir>
  <!-- Value used in Sdk.Razor.CurrentVersion.targets -->
  <RazorTargetNameSuffix>.Views</RazorTargetNameSuffix>
</PropertyGroup>
";
            var filePath = CreateProjectFile(null, projectSnippet, TargetConstants.SetRazorCodeAnalysisProperties);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SetRazorCodeAnalysisProperties);

            // Assert
            result.AssertTargetExecuted(TargetConstants.SetRazorCodeAnalysisProperties);
            AssertExpectedErrorLog(result, rootOutputFolder + @"\Issues.Views.json");
        }

        [TestMethod]
        public void Razor_ExcludedProject_NoErrorLog()
        {
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarQubeExclude>true</SonarQubeExclude>
</PropertyGroup>";
            var filePath = CreateProjectFile(null, projectSnippet, TargetConstants.OverrideRoslynAnalysis);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);

            // Assert
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
            AssertExpectedErrorLog(result, string.Empty);
        }

        [TestMethod]
        public void Razor_ExcludedProject_PreserveRazorCompilationErrorLog()
        {
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarQubeExclude>true</SonarQubeExclude>
  <RazorCompilationErrorLog>C:\UserDefined.json</RazorCompilationErrorLog>
</PropertyGroup>";
            var filePath = CreateProjectFile(null, projectSnippet, TargetConstants.OverrideRoslynAnalysis);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);

            // Assert
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
            result.AssertExpectedCapturedPropertyValue(TargetProperties.SonarErrorLog, string.Empty);   // SetRazorCodeAnalysisProperties target doesn't change it
            result.AssertExpectedCapturedPropertyValue(TargetProperties.ErrorLog, string.Empty);
            result.AssertExpectedCapturedPropertyValue(TargetProperties.RazorSonarErrorLog, string.Empty);
            result.AssertExpectedCapturedPropertyValue(TargetProperties.RazorCompilationErrorLog, @"C:\UserDefined.json");
        }

        private static void AssertExpectedErrorLog(BuildLog result, string expectedErrorLog)
        {
            result.AssertExpectedCapturedPropertyValue(TargetProperties.SonarErrorLog, string.Empty);   // SetRazorCodeAnalysisProperties target doesn't change it
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
