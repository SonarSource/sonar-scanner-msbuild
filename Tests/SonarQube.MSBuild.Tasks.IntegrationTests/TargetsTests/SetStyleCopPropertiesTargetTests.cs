/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
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
            BuildAssertions.AssertExpectedAnalysisSetting(actualResult, TargetConstants.StyleCopProjectPathItemName, expectedValue);
        }

        #endregion
    }
}
