/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using SonarScanner.MSBuild.Common;
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
            HackForVs2017Update3.Enable();
            TestUtils.EnsureImportBeforeTargetsExists(TestContext);
        }

        #region Tests

        [TestMethod]
        [Description("Checks the properties are not set if SonarQubeTargetsPath is missing")]
        public void ImportsBefore_SonarQubeTargetsPathNotSet()
        {
            // 1. Prebuild
            // Arrange
            EnsureDummyIntegrationTargetsFileExists();

            var preImportProperties = new WellKnownProjectProperties
            {
                SonarQubeTargetsPath = "",
                TeamBuild2105BuildDirectory = "",
                TeamBuildLegacyBuildDirectory = ""
            };

            // Act
            var projectInstance = CreateAndEvaluateProject(preImportProperties);

            // Assert
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeTargetFilePath);
            AssertAnalysisTargetsAreNotImported(projectInstance);

            // 2. Now build -> succeeds. Info target not executed
            var logger = new BuildLogger();

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

            var preImportProperties = new WellKnownProjectProperties
            {
                SonarQubeTempPath = Path.GetTempPath(),
                SonarQubeTargetsPath = Path.GetDirectoryName(dummySonarTargetsDir),
                BuildingInsideVS = "tRuE" // should not be case-sensitive
            };

            // Act
            var projectInstance = CreateAndEvaluateProject(preImportProperties);

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
            var preImportProperties = new WellKnownProjectProperties
            {
                SonarQubeTempPath = "nonExistentPath",
                MSBuildExtensionsPath = "nonExistentPath",
                TeamBuild2105BuildDirectory = "",
                TeamBuildLegacyBuildDirectory = ""
            };

            // Act
            var projectInstance = CreateAndEvaluateProject(preImportProperties);

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
            Assert.IsTrue(result.Errors[0].Contains(projectName), "Expecting the error message to contain the project file name");
        }

        [TestMethod]
        [Description("Checks that the targets are imported if the properties are set correctly and the targets can be found")]
        public void ImportsBefore_TargetsExist()
        {
            // 1. Pre-build
            // Arrange
            var dummySonarTargetsDir = EnsureDummyIntegrationTargetsFileExists();

            var preImportProperties = new WellKnownProjectProperties
            {
                SonarQubeTempPath = Path.GetTempPath(),
                SonarQubeTargetsPath = Path.GetDirectoryName(dummySonarTargetsDir),
                TeamBuild2105BuildDirectory = "",
                TeamBuildLegacyBuildDirectory = ""
            };

            // Act
            var projectInstance = CreateAndEvaluateProject(preImportProperties);

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

        private ProjectInstance CreateAndEvaluateProject(Dictionary<string, string> preImportProperties)
        {
            // TODO: consider changing these tests to redirect where the common targets look for ImportBefore assemblies.
            // That would allow us to test the actual ImportBefore behavior (we're currently creating a project that
            // explicitly imports our SonarQube "ImportBefore" project).
            BuildUtilities.DisableStandardTargetsWildcardImporting(preImportProperties);

            var projectRoot = CreateImportsBeforeTestProject(preImportProperties);

            // Evaluate the imports
            var projectInstance = new ProjectInstance(projectRoot);

            SavePostEvaluationProject(projectInstance);
            return projectInstance;
        }

        /// <summary>
        /// Creates and returns a minimal project file that has imported the ImportsBefore targets file
        /// </summary>
        /// <param name="preImportProperties">Any properties that need to be set before the C# targets are imported. Can be null.</param>
        private ProjectRootElement CreateImportsBeforeTestProject(IDictionary<string, string> preImportProperties)
        {
            // Create a dummy SonarQube analysis targets file
            EnsureDummyIntegrationTargetsFileExists();

            // Locate the real "ImportsBefore" target file
            var importsBeforeTargets = Path.Combine(TestUtils.GetTestSpecificFolderName(TestContext), TargetConstants.ImportsBeforeFile);
            Assert.IsTrue(File.Exists(importsBeforeTargets), "Test error: the SonarQube imports before target file does not exist. Path: {0}", importsBeforeTargets);

            var projectName = TestContext.TestName + ".proj";
            var testSpecificFolder = TestUtils.EnsureTestSpecificFolder(TestContext);
            var fullProjectPath = Path.Combine(testSpecificFolder, projectName);

            var root = BuildUtilities.CreateMinimalBuildableProject(preImportProperties, false /* not a VB project */, importsBeforeTargets);
            root.AddProperty(TargetProperties.ProjectGuid, Guid.NewGuid().ToString("D"));

            root.Save(fullProjectPath);
            TestContext.AddResultFile(fullProjectPath);

            return root;
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
            var testSpecificDir = TestUtils.EnsureTestSpecificFolder(TestContext);

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

        /// <summary>
        /// Ensures a file with the correct file name exists in the correct sub-directory
        /// under the specified root
        /// </summary>
        private static string EnsureDummyAnalysisConfigFileExists(string rootBuildDir)
        {
            var subDir = Path.Combine(rootBuildDir, ".sonarqube", "conf");
            Directory.CreateDirectory(subDir);

            var fullPath = Path.Combine(subDir, FileConstants.ConfigFileName);
            File.WriteAllText(fullPath, "Dummy config file");
            return fullPath;
        }

        #endregion Private methods

        #region Assertions

        private static void AssertAnalysisTargetsAreNotImported(ProjectInstance projectInstance)
        {
            var propertyInstance = projectInstance.GetProperty(DummyAnalysisTargetsMarkerProperty);
            Assert.IsNull(propertyInstance, "SonarQube Analysis targets should not have been imported");
        }

        private static void AssertAnalysisTargetsAreImported(ProjectInstance projectInstance)
        {
            var propertyInstance = projectInstance.GetProperty(DummyAnalysisTargetsMarkerProperty);
            Assert.IsNotNull(propertyInstance, "Failed to import the SonarQube Analysis targets");
        }

        #endregion Assertions
    }
}
