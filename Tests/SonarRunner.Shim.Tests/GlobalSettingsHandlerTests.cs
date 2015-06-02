//-----------------------------------------------------------------------
// <copyright file="GlobalSettingsHandlerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.Collections.Generic;
using System.Linq;
using TestUtilities;

namespace SonarRunner.Shim.Tests
{
    [TestClass]
    public class GlobalSettingsHandlerTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void GlobalSettings_NoAdditionalSettings()
        {
            // Arrange
            AnalysisConfig config = CreateValidAnalysisConfig();
            TestLogger logger = new TestLogger();
            IList<ProjectInfo> projects = new List<ProjectInfo>();

            // Act
            IEnumerable<AnalysisSetting> actual = GlobalSettingsHandler.GetGlobalSettings(config, projects, logger);
            DumpSettings(actual);

            // Assert
            AssertExpectedSettingsCount(0, actual);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void GlobalSettings_WithAdditionalSettings()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidAnalysisConfig();

            // Specify some additional settings in the config
            config.AdditionalSettings = new List<AnalysisSetting>();
            AddSetting(config.AdditionalSettings, "config.file.1", "config file value 1");
            AddSetting(config.AdditionalSettings, "config.file.2", "config file value 2");

            // Specify some additional settings in two project info instance
            ProjectInfo proj1 = new ProjectInfo();
            proj1.GlobalAnalysisSettings = new List<AnalysisSetting>();
            AddSetting(proj1.GlobalAnalysisSettings, "proj.1.setting.1", "proj 1 setting 1");
            AddSetting(proj1.GlobalAnalysisSettings, "proj.1.setting.2", "proj 1 setting 2");

            proj1.AnalysisSettings = new List<AnalysisSetting>();
            AddSetting(proj1.AnalysisSettings, "local setting 1", "not global - should be ignored");
            AddSetting(proj1.AnalysisSettings, "local setting 2", "not global - should be ignored");

            ProjectInfo proj2 = new ProjectInfo();
            proj2.GlobalAnalysisSettings = new List<AnalysisSetting>();
            AddSetting(proj2.GlobalAnalysisSettings, "proj.2.setting.A", "proj 2 setting A");
            AddSetting(proj2.GlobalAnalysisSettings, "proj.2.setting.B", "proj 2 setting B");

            IList<ProjectInfo> projects = new List<ProjectInfo>();
            projects.Add(proj1);
            projects.Add(proj2);

            // Act
            IEnumerable<AnalysisSetting> actual = GlobalSettingsHandler.GetGlobalSettings(config, projects, logger);
            DumpSettings(actual);

            // Assert
            // Check the additional settings
            AssertSettingExists(actual, "config.file.1", "config file value 1");
            AssertSettingExists(actual, "config.file.2", "config file value 2");
            AssertSettingExists(actual, "proj.1.setting.1", "proj 1 setting 1");
            AssertSettingExists(actual, "proj.1.setting.2", "proj 1 setting 2");
            AssertSettingExists(actual, "proj.2.setting.A", "proj 2 setting A");
            AssertSettingExists(actual, "proj.2.setting.B", "proj 2 setting B");

            AssertExpectedSettingsCount(6, actual);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void GlobalSettings_AttemptToSetCoreSettings()
        {
            // Attempting to specify the core properties in the config file
            // or project properties should result in warnings and the invalid
            // settings being ignored
            
            // Arrange
            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidAnalysisConfig();

            config.AdditionalSettings = new List<AnalysisSetting>();
            AddSetting(config.AdditionalSettings, SonarProperties.ProjectKey, "bad key");
            AddSetting(config.AdditionalSettings, SonarProperties.ProjectName, "bad name");
            AddSetting(config.AdditionalSettings, "valid.additional.setting", "valid value");

            ProjectInfo proj1 = new ProjectInfo();
            proj1.GlobalAnalysisSettings = new List<AnalysisSetting>();
            proj1.FullPath = @"c:\\project1.proj";
            AddSetting(proj1.GlobalAnalysisSettings, SonarProperties.ProjectVersion, "bad version");

            ProjectInfo proj2 = new ProjectInfo();
            proj2.GlobalAnalysisSettings = new List<AnalysisSetting>();
            proj2.FullPath = @"c:\\project2.proj";
            AddSetting(proj2.GlobalAnalysisSettings, SonarProperties.ProjectBaseDir, "bad dir");

            IList<ProjectInfo> projects = new List<ProjectInfo>();
            projects.Add(proj1);
            projects.Add(proj2);

            
            // Act
            IEnumerable<AnalysisSetting> actual = GlobalSettingsHandler.GetGlobalSettings(config, projects, logger);
            DumpSettings(actual);

            // Assert
            AssertSettingExists(actual, "valid.additional.setting", "valid value");

            AssertExpectedSettingsCount(1, actual);

            logger.AssertSingleWarningExists(SonarProperties.ProjectKey, config.SonarConfigDir);
            logger.AssertSingleWarningExists(SonarProperties.ProjectName, config.SonarConfigDir);
            logger.AssertSingleWarningExists(SonarProperties.ProjectVersion, @"c:\\project1.proj");
            logger.AssertSingleWarningExists(SonarProperties.ProjectBaseDir, @"c:\\project2.proj");

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(4);
        }

        [TestMethod]
        public void GlobalSettings_DuplicatesWithSameValue()
        {
            // Duplicate settings with the same value should be used with info messages

            // Arrange
            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidAnalysisConfig();

            config.AdditionalSettings = new List<AnalysisSetting>();
            AddSetting(config.AdditionalSettings, "config.and.project1", "XXX");

            ProjectInfo proj1 = new ProjectInfo();
            proj1.GlobalAnalysisSettings = new List<AnalysisSetting>();
            proj1.FullPath = @"c:\\project1.proj";
            AddSetting(proj1.GlobalAnalysisSettings, "config.and.project1", "XXX");
            AddSetting(proj1.GlobalAnalysisSettings, "project1.and.project2", "YYY");

            ProjectInfo proj2 = new ProjectInfo();
            proj2.GlobalAnalysisSettings = new List<AnalysisSetting>();
            proj2.FullPath = @"c:\\project2.proj";
            AddSetting(proj2.GlobalAnalysisSettings, "project1.and.project2", "YYY");

            IList<ProjectInfo> projects = new List<ProjectInfo>();
            projects.Add(proj1);
            projects.Add(proj2);

            // Act
            IEnumerable<AnalysisSetting> actual = GlobalSettingsHandler.GetGlobalSettings(config, projects, logger);
            DumpSettings(actual);

            // Assert
            AssertSettingExists(actual, "config.and.project1", "XXX");
            AssertSettingExists(actual, "project1.and.project2", "YYY");

            logger.AssertSingleMessageExists("config.and.project1", config.SonarConfigDir, @"c:\\project1.proj");
            logger.AssertSingleMessageExists("project1.and.project2", @"c:\\project1.proj", @"c:\\project2.proj");

            AssertExpectedSettingsCount(2, actual); // 2 same

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void GlobalSettings_DuplicatesDifferentValue()
        {
            // Duplicate settings with different values should be ignored with warnings

            // Arrange
            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidAnalysisConfig();

            config.AdditionalSettings = new List<AnalysisSetting>();
            AddSetting(config.AdditionalSettings, "config.and.project1", "111");
            AddSetting(config.AdditionalSettings, "config.and.projects", "XXX"); // same value in config and project1, different in project 2

            ProjectInfo proj1 = new ProjectInfo();
            proj1.GlobalAnalysisSettings = new List<AnalysisSetting>();
            proj1.FullPath = @"c:\\project1.proj";
            AddSetting(proj1.GlobalAnalysisSettings, "config.and.projects", "XXX");
            AddSetting(proj1.GlobalAnalysisSettings, "config.and.project1", "222");
            AddSetting(proj1.GlobalAnalysisSettings, "project1.and.project2", "333");
            
            ProjectInfo proj2 = new ProjectInfo();
            proj2.GlobalAnalysisSettings = new List<AnalysisSetting>();
            proj2.FullPath = @"c:\\project2.proj";
            AddSetting(proj2.GlobalAnalysisSettings, "config.and.projects", "YYY");
            AddSetting(proj2.GlobalAnalysisSettings, "project1.and.project2", "444");

            IList<ProjectInfo> projects = new List<ProjectInfo>();
            projects.Add(proj1);
            projects.Add(proj2);

            // Act
            IEnumerable<AnalysisSetting> actual = GlobalSettingsHandler.GetGlobalSettings(config, projects, logger);
            DumpSettings(actual);

            // Assert
            logger.AssertSingleWarningExists("config.and.projects", config.SonarConfigDir, @"c:\\project1.proj", @"c:\\project2.proj");
            logger.AssertSingleWarningExists("config.and.project1", config.SonarConfigDir, @"c:\\project1.proj");
            logger.AssertSingleWarningExists("project1.and.project2", @"c:\\project1.proj", @"c:\\project2.proj");

            AssertExpectedSettingsCount(0, actual);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(3);
        }


        [TestMethod]
        public void GlobalSettings_InvalidKeysAreIgnored()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidAnalysisConfig();

            config.AdditionalSettings = new List<AnalysisSetting>();
            AddSetting(config.AdditionalSettings, "  invalid config key", "invalid");

            ProjectInfo proj1 = new ProjectInfo();
            proj1.GlobalAnalysisSettings = new List<AnalysisSetting>();
            proj1.FullPath = @"c:\\project1.proj";
            AddSetting(proj1.GlobalAnalysisSettings, ".invalid.project.key", "invalid");

            IList<ProjectInfo> projects = new List<ProjectInfo>();
            projects.Add(proj1);

            // Act
            IEnumerable<AnalysisSetting> actual = GlobalSettingsHandler.GetGlobalSettings(config, projects, logger);
            DumpSettings(actual);

            // Assert
            logger.AssertSingleWarningExists("  invalid config key", config.SonarConfigDir);
            logger.AssertSingleWarningExists(".invalid.project.key", @"c:\\project1.proj");

            AssertExpectedSettingsCount(0, actual);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(2);
        }

        #endregion

        #region Checks

        private static void AssertSettingExists(IEnumerable<AnalysisSetting> actual, string expectedKey, string expectedValue)
        {
            IEnumerable<AnalysisSetting> matches = actual.Where(s => AnalysisSetting.SettingKeyComparer.Equals(expectedKey, s.Id));

            Assert.AreEqual(1, matches.Count(), "Unexpected number of settings found for key: {0}", expectedKey);

            AnalysisSetting setting = matches.Single();
            Assert.IsTrue(AnalysisSetting.SettingValueComparer.Equals(expectedValue, setting.Value), "Global setting has unexpected value. Key: {0}", expectedKey);
        }

        private static void AssertExpectedSettingsCount(int expected, IEnumerable<AnalysisSetting> actual)
        {
            Assert.IsNotNull(actual, "The list of analysis settings should not be null");
            Assert.AreEqual(expected, actual.Count(), "Unexpected number of global settings");
        }

        #endregion

        #region Helpers

        private static AnalysisConfig CreateValidAnalysisConfig()
        {
            AnalysisConfig config = new AnalysisConfig()
            {
                SonarProjectKey = "my.project",
                SonarProjectName = "My project",
                SonarProjectVersion = "1.0",
                SonarOutputDir = @"c:\output",
                SonarConfigDir = @"c:\config",
                SonarRunnerPropertiesPath = @"c:\properties\a.props"
            };
            return config;
        }

        private static void AddSetting(IList<AnalysisSetting> settings, string key, string value)
        {
            settings.Add(new AnalysisSetting() { Id = key, Value = value });
        }

        private void DumpSettings(IEnumerable<AnalysisSetting> settings)
        {
            this.TestContext.WriteLine("");
            this.TestContext.WriteLine("Global analysis settings");
            foreach (AnalysisSetting setting in settings)
            {
                this.TestContext.WriteLine("\t{0}={1}", setting.Id, setting.Value);
            }
            this.TestContext.WriteLine("");
        }
        
        #endregion
    }
}
