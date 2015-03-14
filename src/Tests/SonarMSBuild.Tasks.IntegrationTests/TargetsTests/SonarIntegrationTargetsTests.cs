//-----------------------------------------------------------------------
// <copyright file="SonarIntegrationTargetsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Sonar.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    [DeploymentItem("LinkedFiles\\Sonar.Integration.v0.1.targets")]
    public class SonarIntegrationTargetsTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [Description("Checks the properties are not set if RunSonarAnalysis is not set")]
        public void IntTargets_RunSonarAnalysisNotSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, null);

            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);
            
            // Assert
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.TeamBuildBuildDirectory);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarOutputPath);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarConfigPath);
        }

        [TestMethod]
        [Description("Checks the properties are not set if RunSonarAnalysis is not true")]
        public void IntTargets_RunSonarAnalysisNotTrue()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarAnalysis = "trueX";
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);

            // Assert
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.TeamBuildBuildDirectory);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarOutputPath);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarConfigPath);
        }

        [TestMethod]
        [Description("Checks the sonar paths are set correctly when the TeamBuild property is missing")]
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
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarOutputPath);
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarConfigPath);            
        }

        [TestMethod]
        [Description("Checks the sonar paths are set correctly when the TeamBuild property is provided")]
        public void IntTargets_SonarPaths_TeamBuildPropertySet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarAnalysis = "TRUE";
            preImportProperties.TeamBuildBuildDirectory = @"t:\TeamBuildDir";
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);
            
            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarOutputPath, @"t:\TeamBuildDir\SonarTemp\Output\");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarConfigPath, @"t:\TeamBuildDir\SonarTemp\Config\");
        }

        [TestMethod]
        [Description("Checks the sonar paths are set correctly when the SonarTemp property is provided")]
        public void IntTargets_SonarPaths_SonarTempSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarAnalysis = "TRUE";
            preImportProperties.SonarTempPath = @"c:\sonarTemp";
            preImportProperties.TeamBuildBuildDirectory = @"t:\TeamBuildPath\"; // SonarTempPath setting should take precedence

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarOutputPath, @"c:\sonarTemp\Output\");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarConfigPath, @"c:\sonarTemp\Config\");
        }

        [TestMethod]
        [Description("Tests that the explicit property values for the output and config paths are used if supplied")]
        public void IntTargets_SonarPaths_OutputAndConfigPathsAreSet()
        {
            // The SonarTemp and TeamBuild paths should be ignored if the output and config are set explicitly

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarAnalysis = "true";
            preImportProperties.SonarOutputPath = @"c:\output";
            preImportProperties.SonarConfigPath= @"c:\config";
            preImportProperties.SonarTempPath = @"c:\sonarTemp";
            preImportProperties.TeamBuildBuildDirectory = @"t:\TeamBuildPath\";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Act
            ProjectInstance projectInstance = new ProjectInstance(projectRoot.FullPath);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarOutputPath, @"c:\output");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarConfigPath, @"c:\config");
        }

        #endregion
    }
}
