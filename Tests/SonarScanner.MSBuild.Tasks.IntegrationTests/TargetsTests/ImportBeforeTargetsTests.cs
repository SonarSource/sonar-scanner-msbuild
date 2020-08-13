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
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class ImportBeforeTargetsTests
    {
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Name of the property to check for to determine whether or not
        /// the targets have been imported or not
        /// </summary>
        private const string DummyAnalysisTargetsMarkerProperty = "DummyProperty";

        [TestInitialize]
        public void TestInitialize()
        {
            TestUtils.EnsureImportBeforeTargetsExists(TestContext);
        }

        #region Tests

        [TestMethod]
        [Description("Checks the properties are not set if SonarQubeTargetsPath is missing")]
        public void ImportsBefore_SonarQubeTargetsPathNotSet()
        {
            // 1. Prebuild
            // Arrange
            var projectXml = @"
<PropertyGroup>
  <SonarQubeTargetsPath />
  <AGENT_BUILDDIRECTORY />
  <TF_BUILD_BUILDDIRECTORY />
</PropertyGroup>
";
            var projectFilePath = CreateProjectFile(projectXml);

            // Act
            var projectInstance = CreateAndEvaluateProject(projectFilePath);

            // Assert
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeTargetFilePath);
            AssertAnalysisTargetsAreNotImported(projectInstance);

            // 2. Now build -> succeeds. Info target not executed
            var result = BuildRunner.BuildTargets(TestContext, projectInstance.FullPath);

            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget);
            result.AssertTargetNotExecuted(TargetConstants.ImportBeforeInfoTarget);
            result.AssertExpectedErrorCount(0);
        }

        [TestMethod]
        [Description("Checks the properties are not set if the project is building inside Visual Studio")]
        public void ImportsBefore_BuildingInsideVS_NotImported()
        {
            // 1. Pre-build
            // Arrange
            var dummySonarTargetsDir = EnsureDummyIntegrationTargetsFileExists();
            
            var projectXml = $@"
<PropertyGroup>
  <SonarQubeTempPath>{Path.GetTempPath()}</SonarQubeTempPath>
  <SonarQubeTargetsPath>{Path.GetDirectoryName(dummySonarTargetsDir)}</SonarQubeTargetsPath>
  <BuildingInsideVisualStudio>tRuE</BuildingInsideVisualStudio>
</PropertyGroup>
";
            var projectFilePath = CreateProjectFile(projectXml);

            // Act
            var projectInstance = CreateAndEvaluateProject(projectFilePath);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeTargetFilePath, dummySonarTargetsDir);
            AssertAnalysisTargetsAreNotImported(projectInstance);

            // 2. Now build -> succeeds
            var result = BuildRunner.BuildTargets(TestContext, projectInstance.FullPath);

            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget);
            result.AssertTargetNotExecuted(TargetConstants.ImportBeforeInfoTarget);
            result.AssertExpectedErrorCount(0);
        }

        [TestMethod]
        [Description("Checks what happens if the analysis targets cannot be located")]
        public void ImportsBefore_MissingAnalysisTargets()
        {
            // 1. Prebuild
            // Arrange
            var projectXml = @"
<PropertyGroup>
  <SonarQubeTempPath>nonExistentPath</SonarQubeTempPath>
  <MSBuildExtensionsPath>nonExistentPath</MSBuildExtensionsPath>
  <AGENT_BUILDDIRECTORY />
  <TF_BUILD_BUILDDIRECTORY />
</PropertyGroup>
";
            var projectFilePath = CreateProjectFile(projectXml);

            // Act
            var projectInstance = CreateAndEvaluateProject(projectFilePath);


            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeTargetsPath, @"nonExistentPath\bin\targets");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeTargetFilePath, @"nonExistentPath\bin\targets\SonarQube.Integration.targets");

            AssertAnalysisTargetsAreNotImported(projectInstance); // Targets should not be imported

            // 2. Now build -> fails with an error message
            var result = BuildRunner.BuildTargets(TestContext, projectInstance.FullPath, buildShouldSucceed: false);

            result.BuildSucceeded.Should().BeFalse();
            result.AssertTargetExecuted(TargetConstants.ImportBeforeInfoTarget);
            result.AssertExpectedErrorCount(1);

            var projectName = Path.GetFileName(projectInstance.FullPath);
            result.Errors[0].Contains(projectName).Should().BeTrue("Expecting the error message to contain the project file name");
        }

        [TestMethod]
        [Description("Checks that the targets are imported if the properties are set correctly and the targets can be found")]
        public void ImportsBefore_TargetsExist()
        {
            // 1. Pre-build
            // Arrange
            var dummySonarTargetsDir = EnsureDummyIntegrationTargetsFileExists();

            var projectXml = $@"
<PropertyGroup>
  <SonarQubeTempPath>{Path.GetTempPath()}</SonarQubeTempPath>
  <SonarQubeTargetsPath>{Path.GetDirectoryName(dummySonarTargetsDir)}</SonarQubeTargetsPath>
  <AGENT_BUILDDIRECTORY />
  <TF_BUILD_BUILDDIRECTORY />
</PropertyGroup>
";
            var projectFilePath = CreateProjectFile(projectXml);

            // Act
            var projectInstance = CreateAndEvaluateProject(projectFilePath);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeTargetFilePath, dummySonarTargetsDir);
            AssertAnalysisTargetsAreImported(projectInstance);

            // 2. Now build -> succeeds
            var result = BuildRunner.BuildTargets(TestContext, projectInstance.FullPath);

            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget);
            result.AssertTargetExecuted(TargetConstants.ImportBeforeInfoTarget);
            result.AssertExpectedErrorCount(0);
        }

        #endregion Tests

        #region Private methods

        private ProjectInstance CreateAndEvaluateProject(string projectFilePath)
        {
            // TODO: consider changing these tests to redirect where the common targets look for ImportBefore assemblies.
            // That would allow us to test the actual ImportBefore behavior (we're currently creating a project that
            // explicitly imports our SonarQube "ImportBefore" project).

            // Evaluate the imports
            var projectInstance = new ProjectInstance(projectFilePath);

            SavePostEvaluationProject(projectInstance);
            return projectInstance;
        }

        /// <summary>
        /// Saves the project once the imports have been evaluated
        /// </summary>
        private void SavePostEvaluationProject(ProjectInstance projectInstance)
        {
            var postBuildProject = projectInstance.FullPath + ".postbuild.proj";
            projectInstance.ToProjectRootElement().Save(postBuildProject);
            TestContext.AddResultFile(postBuildProject);
        }

        /// <summary>
        /// Ensures that a dummy targets file with the name of the SonarQube analysis targets file exists.
        /// Return the full path to the targets file.
        /// </summary>
        private string EnsureDummyIntegrationTargetsFileExists()
        {
            // This can't just be in the TestContext.DeploymentDirectory as this will
            // be shared with other tests, and some of those tests might be deploying
            // the real analysis targets to that location.
            var testSpecificDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var fullPath = Path.Combine(testSpecificDir, TargetConstants.AnalysisTargetFile);
            if (!File.Exists(fullPath))
            {
// To check whether the targets are imported or not we check for
// the existence of the DummyProperty, below.
                var contents = @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <DummyProperty>123</DummyProperty>
  </PropertyGroup>
  <Target Name='DummyTarget' />
</Project>
";
                File.WriteAllText(fullPath, contents);
            }
            return fullPath;
        }

        private string CreateProjectFile(string testSpecificProjectXml)
        {
            var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            // Locate the real "ImportsBefore" target file
            var importsBeforeTargets = Path.Combine(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext), TargetConstants.ImportsBeforeFile);
            File.Exists(importsBeforeTargets).Should().BeTrue("Test error: the SonarQube imports before target file does not exist. Path: {0}", importsBeforeTargets);

            var template = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='Current' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>

  <!-- Test-specific data -->
  TEST_SPECIFIC_XML

  <!-- Boilerplate -->
  <PropertyGroup>
    <ImportByWildcardBeforeMicrosoftCommonTargets>false</ImportByWildcardBeforeMicrosoftCommonTargets>
    <ImportByWildcardAfterMicrosoftCommonTargets>false</ImportByWildcardAfterMicrosoftCommonTargets>
    <ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets>false</ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets>
    <ImportUserLocationsByWildcardAfterMicrosoftCommonTargets>false</ImportUserLocationsByWildcardAfterMicrosoftCommonTargets>
    <OutputPath>bin\</OutputPath>
    <Language>C#</Language>
    <OutputType>library</OutputType>
    <ProjectGuid>ffdb93c0-2880-44c7-89a6-bbd4ddab034a</ProjectGuid>
  </PropertyGroup>

  <!-- Standard boilerplate closing imports -->
  <Import Project='SQ_IMPORTS_BEFORE' />
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>
";
            var projectData = template.Replace("SQ_IMPORTS_BEFORE", importsBeforeTargets)
                .Replace("TEST_SPECIFIC_XML", testSpecificProjectXml);

            var projectFilePath = Path.Combine(projectDirectory, TestContext.TestName + ".proj.txt");
            File.WriteAllText(projectFilePath, projectData);
            TestContext.AddResultFile(projectFilePath);

            return projectFilePath;
        }

        #endregion Private methods

        #region Assertions

        private static void AssertAnalysisTargetsAreNotImported(ProjectInstance projectInstance)
        {
            var propertyInstance = projectInstance.GetProperty(DummyAnalysisTargetsMarkerProperty);
            propertyInstance.Should().BeNull("SonarQube Analysis targets should not have been imported");
        }

        private static void AssertAnalysisTargetsAreImported(ProjectInstance projectInstance)
        {
            var propertyInstance = projectInstance.GetProperty(DummyAnalysisTargetsMarkerProperty);
            propertyInstance.Should().NotBeNull("Failed to import the SonarQube Analysis targets");
        }

        #endregion Assertions
    }
}
