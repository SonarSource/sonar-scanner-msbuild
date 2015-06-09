//-----------------------------------------------------------------------
// <copyright file="SonarIntegrationTargetsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class SonarIntegrationTargetsTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [Description("Checks the properties are not set if RunSonarQubeAnalysis is not set")]
        public void IntTargets_RunSonarQubeAnalysisNotSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, null);

            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);
            
            // Assert
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeOutputPath);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeConfigPath);
        }

        [TestMethod]
        [Description("Checks the properties are not set if RunSonarQubeAnalysis is not true")]
        public void IntTargets_RunSonarQubeAnalysisNotTrue()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarQubeAnalysis = "trueX";
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);

            // Assert
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeOutputPath);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeConfigPath);
        }

        [TestMethod]
        [Description("Checks the SonarQube paths are not set when the TeamBuild build directories are missing")]
        public void IntTargets_SonarPaths_TeamBuildBuildDirNotSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.TeamBuildLegacyBuildDirectory = "";
            preImportProperties.TeamBuild2105BuildDirectory = "";
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);

            // Assert
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeOutputPath);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeConfigPath);            
        }

        [TestMethod]
        [Description("Checks the SonarQube paths are set correctly when the legacy TeamBuild directory is provided")]
        public void IntTargets_SonarPaths_TeamBuildPropertySet_Legacy()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarQubeAnalysis = "TRUE";
            preImportProperties.SonarQubeTempPath = @"t:\TeamBuildDir_Legacy\.sonarqube";
            preImportProperties.TeamBuildLegacyBuildDirectory = @"t:\TeamBuildDir_Legacy";
            preImportProperties.TeamBuild2105BuildDirectory = "";
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeOutputPath, @"t:\TeamBuildDir_Legacy\.sonarqube\out\");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeConfigPath, @"t:\TeamBuildDir_Legacy\.sonarqube\conf\");
        }

        [TestMethod]
        [Description("Checks the SonarQube paths are set correctly when the new TeamBuild build directory is provided")]
        public void IntTargets_SonarPaths_TeamBuildPropertySet_NonLegacy()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarQubeAnalysis = "TRUE";
            preImportProperties.SonarQubeTempPath = @"t:\TeamBuildDir_NonLegacy\.sonarqube"; // FIXME
            preImportProperties.TeamBuildLegacyBuildDirectory = "";
            preImportProperties.TeamBuild2105BuildDirectory = @"t:\TeamBuildDir_NonLegacy";
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeOutputPath, @"t:\TeamBuildDir_NonLegacy\.sonarqube\out\");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeConfigPath, @"t:\TeamBuildDir_NonLegacy\.sonarqube\conf\");
        }

        [TestMethod]
        [Description("Checks the SonarQube paths are set correctly when the SonarQubeTempPath property is provided")]
        public void IntTargets_SonarPaths_TempPathSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarQubeAnalysis = "TRUE";
            preImportProperties.SonarQubeTempPath = @"c:\sonarQTemp";
            preImportProperties.TeamBuildLegacyBuildDirectory = @"t:\Legacy TeamBuildPath\"; // SonarQubeTempPath setting should take precedence
            preImportProperties.TeamBuild2105BuildDirectory = @"x:\New Team Build Path\";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeOutputPath, @"c:\sonarQTemp\out\");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeConfigPath, @"c:\sonarQTemp\conf\");
        }

        [TestMethod]
        [Description("Tests that the explicit property values for the output and config paths are used if supplied")]
        public void IntTargets_SonarPaths_OutputAndConfigPathsAreSet()
        {
            // The SonarQubeTempPath and TeamBuild paths should be ignored if the output and config are set explicitly

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarQubeAnalysis = "true";
            preImportProperties.SonarQubeOutputPath = @"c:\output";
            preImportProperties.SonarQubeConfigPath= @"c:\config";
            preImportProperties.SonarQubeTempPath = @"c:\sonarQTemp";
            preImportProperties.TeamBuildLegacyBuildDirectory = @"t:\Legacy TeamBuildPath\";
            preImportProperties.TeamBuild2105BuildDirectory = @"t:\New TeamBuildPath\";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeOutputPath, @"c:\output");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeConfigPath, @"c:\config");
        }

        #endregion
    }
}
