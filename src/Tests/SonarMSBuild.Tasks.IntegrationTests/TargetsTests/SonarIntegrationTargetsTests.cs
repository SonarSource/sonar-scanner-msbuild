//-----------------------------------------------------------------------
// <copyright file="SonarIntegrationTargetsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    [DeploymentItem("LinkedFiles\\SonarQube.Integration.v0.1.targets")]
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
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.TeamBuildBuildDirectory);
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
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.TeamBuildBuildDirectory);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeOutputPath);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeConfigPath);
        }

        [TestMethod]
        [Description("Checks the SonarQube paths are set correctly when the TeamBuild property is missing")]
        public void IntTargets_SonarPaths_TeamBuildPropertyNotSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);

            // Assert
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.TeamBuildBuildDirectory);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeOutputPath);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeConfigPath);            
        }

        [TestMethod]
        [Description("Checks the SonarQube paths are set correctly when the TeamBuild property is provided")]
        public void IntTargets_SonarPaths_TeamBuildPropertySet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarQubeAnalysis = "TRUE";
            preImportProperties.TeamBuildBuildDirectory = @"t:\TeamBuildDir";
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);
            
            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeOutputPath, @"t:\TeamBuildDir\SQTemp\Output\");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeConfigPath, @"t:\TeamBuildDir\SQTemp\Config\");
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
            preImportProperties.TeamBuildBuildDirectory = @"t:\TeamBuildPath\"; // SonarQubeTempPath setting should take precedence

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeOutputPath, @"c:\sonarQTemp\Output\");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeConfigPath, @"c:\sonarQTemp\Config\");
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
            preImportProperties.TeamBuildBuildDirectory = @"t:\TeamBuildPath\";

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
