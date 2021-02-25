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

using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class SonarIntegrationTargetsTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [Description("Checks the properties are not set if the temp folder is not set")]
        public void IntTargets_TempFolderIsNotSet()
        {
            // Arrange
            var filePath = CreateProjectFile(null, null);

            // Act
            var projectInstance = new ProjectInstance(filePath);

            // Assert
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeOutputPath);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeConfigPath);
        }

        [TestMethod]
        [Description("Checks the SonarQube paths are not set when the TeamBuild build directories are missing")]
        public void IntTargets_SonarPaths_TeamBuildBuildDirNotSet()
        {
            // Arrange
            string projectXml = $@"
<PropertyGroup>
  <TF_BUILD_BUILDDIRECTORY />
  <AGENT_BUILDDIRECTORY />
</PropertyGroup>
";

            var filePath = CreateProjectFile(null, projectXml);

            // Act
            var projectInstance = new ProjectInstance(filePath);
            // Assert
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeOutputPath);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeConfigPath);
        }

        [TestMethod]
        [Description("Checks the SonarQube paths are set correctly when the legacy TeamBuild directory is provided")]
        public void IntTargets_SonarPaths_TeamBuildPropertySet_Legacy()
        {
            // Arrange
            string projectXml = $@"
<PropertyGroup>
  <SonarQubeTempPath>t:\TeamBuildDir_Legacy\.sonarqube</SonarQubeTempPath>
  <TF_BUILD_BUILDDIRECTORY>t:\TeamBuildDir_Legacy</TF_BUILD_BUILDDIRECTORY>
  <AGENT_BUILDDIRECTORY />
</PropertyGroup>
";

            var filePath = CreateProjectFile(null, projectXml);

            // Act
            var projectInstance = new ProjectInstance(filePath);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeOutputPath, @"t:\TeamBuildDir_Legacy\.sonarqube\out");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeConfigPath, @"t:\TeamBuildDir_Legacy\.sonarqube\conf");
        }

        [TestMethod]
        [Description("Checks the SonarQube paths are set correctly when the new TeamBuild build directory is provided")]
        public void IntTargets_SonarPaths_TeamBuildPropertySet_NonLegacy()
        {
            // Arrange
            string projectXml = $@"
<PropertyGroup>
  <SonarQubeTempPath>t:\TeamBuildDir_NonLegacy\.sonarqube</SonarQubeTempPath>
  <TF_BUILD_BUILDDIRECTORY></TF_BUILD_BUILDDIRECTORY>
  <AGENT_BUILDDIRECTORY>t:\TeamBuildDir_NonLegacy</AGENT_BUILDDIRECTORY>
</PropertyGroup>
";

            var filePath = CreateProjectFile(null, projectXml);

            // Act
            var projectInstance = new ProjectInstance(filePath);
            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeOutputPath, @"t:\TeamBuildDir_NonLegacy\.sonarqube\out");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeConfigPath, @"t:\TeamBuildDir_NonLegacy\.sonarqube\conf");
        }

        [TestMethod]
        [Description("Checks the SonarQube paths are set correctly when the SonarQubeTempPath property is provided")]
        public void IntTargets_SonarPaths_TempPathSet()
        {
            // Arrange
            string projectXml = $@"
<PropertyGroup>
  <SonarQubeTempPath>c:\sonarQTemp</SonarQubeTempPath>

  <!-- SonarQubeTempPath setting should take precedence -->
  <TF_BUILD_BUILDDIRECTORY>t:\Legacy TeamBuildPath\</TF_BUILD_BUILDDIRECTORY>
  <AGENT_BUILDDIRECTORY>x:\New Team Build Path\</AGENT_BUILDDIRECTORY>
</PropertyGroup>
";

            var filePath = CreateProjectFile(null, projectXml);

            // Act
            var projectInstance = new ProjectInstance(filePath);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeOutputPath, @"c:\sonarQTemp\out");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeConfigPath, @"c:\sonarQTemp\conf");
        }

        [TestMethod]
        [Description("Tests that the explicit property values for the output and config paths are used if supplied")]
        public void IntTargets_SonarPaths_OutputAndConfigPathsAreSet()
        {
            // The SonarQubeTempPath and TeamBuild paths should be ignored if the output and config are set explicitly

            // Arrange
            string projectXml = $@"
<PropertyGroup>
  <SonarQubeOutputPath>c:\output</SonarQubeOutputPath>
  <SonarQubeConfigPath>c:\config</SonarQubeConfigPath>
  <SonarQubeTempPath>c:\sonarQTemp</SonarQubeTempPath>

  <!-- SonarQubeTempPath setting should take precedence -->
  <TF_BUILD_BUILDDIRECTORY>t:\Legacy TeamBuildPath\</TF_BUILD_BUILDDIRECTORY>
  <AGENT_BUILDDIRECTORY>x:\New TeamBuildPath\</AGENT_BUILDDIRECTORY>
</PropertyGroup>
";

            var filePath = CreateProjectFile(null, projectXml);

            // Act
            var projectInstance = new ProjectInstance(filePath);


            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeOutputPath, @"c:\output");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeConfigPath, @"c:\config");
        }

        private string CreateProjectFile(AnalysisConfig config, string projectSnippet)
        {
            var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var targetTestUtils = new TargetsTestsUtils(TestContext);
            var projectTemplate = targetTestUtils.GetProjectTemplate(config, projectDirectory, null, projectSnippet, null);

            return targetTestUtils.CreateProjectFile(projectDirectory, projectTemplate);
        }

        #endregion Tests
    }
}
