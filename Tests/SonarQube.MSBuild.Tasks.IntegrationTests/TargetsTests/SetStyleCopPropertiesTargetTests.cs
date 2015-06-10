//-----------------------------------------------------------------------
// <copyright file="SonarIntegrationTargetsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class SetStyleCopPropertiesTargetTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [Description("Checks the target is not executed if the temp folder is not set")]
        public void StyleCop_TempFolderIsNotSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = "";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.SetStyleCopSettingsTarget);

            // Assert
            logger.AssertTargetNotExecuted(TargetConstants.SetStyleCopSettingsTarget);
        }

        [TestMethod]
        [Description("Checks the target is executed if the temp folder has been provided")]
        public void StyleCop_TempFolderIsSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.SonarQubeOutputPath = rootInputFolder;

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.SetStyleCopSettingsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.SetStyleCopSettingsTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.SetStyleCopSettingsTarget);
        }

        [TestMethod]
        [Description("Checks the targets are executed in the expected order")]
        public void StyleCop_TargetExecutionOrder()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.SonarQubeOutputPath = rootInputFolder;
            properties.SonarQubeConfigPath = rootOutputFolder;

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            // Add some settings we expect to be ignored
            AddAnalysisSetting("sonar.other.setting", "other value", projectRoot);
            AddAnalysisSetting("sonar.other.setting.2", "other value 2", projectRoot);
            projectRoot.Save();

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.DefaultBuildTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.SetStyleCopSettingsTarget);
            logger.AssertExpectedTargetOrdering(TargetConstants.SetStyleCopSettingsTarget, TargetConstants.WriteProjectDataTarget);

            AssertExpectedStyleCopSetting(projectRoot.ProjectFileLocation.File, result);
        }

        [TestMethod]
        [Description("Checks the item value will not be overridden if it is already set")]
        public void StyleCop_ValueAlreadySet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.SonarQubeOutputPath = rootInputFolder;
            properties.SonarQubeConfigPath = rootOutputFolder;

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            // Apply some SonarQubeSettings, one of which specifies the StyleCop setting
            AddAnalysisSetting("sonar.other.setting", "other value", projectRoot);
            AddAnalysisSetting(TargetConstants.StyleCopProjectPathItemName, "xxx.yyy", projectRoot);
            AddAnalysisSetting("sonar.other.setting.2", "other value 2", projectRoot);
            projectRoot.Save();

            BuildLogger logger = new BuildLogger();
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.SetStyleCopSettingsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.SetStyleCopSettingsTarget);
            AssertExpectedStyleCopSetting("xxx.yyy", result);
        }

        #endregion

        #region Private methods

        private static void AddAnalysisSetting(string name, string value, ProjectRootElement project)
        {
            ProjectItemElement element = project.AddItem(BuildTaskConstants.SettingItemName, name);
            element.AddMetadata(BuildTaskConstants.SettingValueMetadataName, value);
        }

        #endregion

        #region Checks

        private static void AssertExpectedStyleCopSetting(string expectedValue, BuildResult actualResult)
        {
            Assert.IsNotNull(actualResult.ProjectStateAfterBuild, "Test error: ProjectStateAfterBuild should not be null");

            IEnumerable<ProjectItemInstance> matches = actualResult.ProjectStateAfterBuild.GetItemsByItemTypeAndEvaluatedInclude(BuildTaskConstants.SettingItemName, TargetConstants.StyleCopProjectPathItemName);
            
            Assert.AreNotEqual(0, matches.Count(), "Expected SonarQubeSetting with include value of '{0}' does not exist", TargetConstants.StyleCopProjectPathItemName);
            Assert.AreEqual(1, matches.Count(), "Only expecting one SonarQubeSetting with include value of '{0}' to exist", TargetConstants.StyleCopProjectPathItemName);

            ProjectItemInstance item = matches.Single();
            string value = item.GetMetadataValue(BuildTaskConstants.SettingValueMetadataName);

            Assert.AreEqual(expectedValue, value, "SonarQubeSetting with include value '{0}' does not have the expected value");

        }

        #endregion
    }
}
