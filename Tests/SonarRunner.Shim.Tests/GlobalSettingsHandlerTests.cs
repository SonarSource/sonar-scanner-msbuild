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

            IList<ProjectInfo> projects = new List<ProjectInfo>();
            ProjectInfo proj1 = CreateProjectInfo(@"c:\p1.proj");
            ProjectInfo proj2 = CreateProjectInfo(@"c:\p2.proj");
            projects.Add(proj1);
            projects.Add(proj2);

            // Specify some additional settings in the config
            config.AdditionalSettings = new List<AnalysisSetting>();
            AddSetting(config.AdditionalSettings, "config.file.1", "config file value 1");
            AddSetting(config.AdditionalSettings, "config.file.2", "config file value 2");

            // Specify some additional settings in two project info instance
            AddSetting(proj1.GlobalAnalysisSettings, "proj.1.setting.1", "proj 1 setting 1");
            AddSetting(proj1.GlobalAnalysisSettings, "proj.1.setting.2", "proj 1 setting 2");

            proj1.AnalysisSettings = new List<AnalysisSetting>();
            AddSetting(proj1.AnalysisSettings, "local setting 1", "not global - should be ignored");
            AddSetting(proj1.AnalysisSettings, "local setting 2", "not global - should be ignored");

            AddSetting(proj2.GlobalAnalysisSettings, "proj.2.setting.A", "proj 2 setting A");
            AddSetting(proj2.GlobalAnalysisSettings, "proj.2.setting.B", "proj 2 setting B");

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
            ProjectInfo proj1 = CreateProjectInfo(@"c:\\project1.proj");
            ProjectInfo proj2 = CreateProjectInfo(@"c:\\project2.proj");
            IList<ProjectInfo> projects = new List<ProjectInfo>();
            projects.Add(proj1);
            projects.Add(proj2);


            // Config settings
            config.AdditionalSettings = new List<AnalysisSetting>();
            AddSetting(config.AdditionalSettings, SonarProperties.ProjectKey, "bad key");
            AddSetting(config.AdditionalSettings, SonarProperties.ProjectName, "bad name");
            AddSetting(config.AdditionalSettings, "valid.additional.setting", "valid value");

            // Project settings
            AddSetting(proj1.GlobalAnalysisSettings, SonarProperties.ProjectVersion, "bad version");
            AddSetting(proj2.GlobalAnalysisSettings, SonarProperties.ProjectBaseDir, "bad dir");
           
            // Act
            IEnumerable<AnalysisSetting> actual = GlobalSettingsHandler.GetGlobalSettings(config, projects, logger);
            DumpSettings(actual);

            // Assert
            AssertSettingExists(actual, "valid.additional.setting", "valid value");

            AssertExpectedSettingsCount(1, actual);

            logger.AssertSingleWarningExists(SonarProperties.ProjectKey, config.SonarConfigDir);
            logger.AssertSingleWarningExists(SonarProperties.ProjectName, config.SonarConfigDir);
            logger.AssertSingleWarningExists(SonarProperties.ProjectVersion, proj1.FullPath);
            logger.AssertSingleWarningExists(SonarProperties.ProjectBaseDir, proj2.FullPath);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(4);
        }

        [TestMethod]
        public void GlobalSettings_Duplicates_Project()
        {
            // Duplicate project settings with the same value should be used with info messages.
            // Duplicate project settings with different values should be ignored with warnings.

            // Arrange
            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidAnalysisConfig();

            IList<ProjectInfo> projects = new List<ProjectInfo>();
            ProjectInfo proj1 = CreateProjectInfo(@"c:\\project1.proj");
            ProjectInfo proj2 = CreateProjectInfo(@"c:\\project2.proj");
            projects.Add(proj1);
            projects.Add(proj2);
            
            AddSetting(proj1.GlobalAnalysisSettings, "project.same", "YYY");
            AddSetting(proj1.GlobalAnalysisSettings, "project.different", "111");

            AddSetting(proj2.GlobalAnalysisSettings, "project.same", "YYY");
            AddSetting(proj2.GlobalAnalysisSettings, "project.different", "222");

            // Act
            IEnumerable<AnalysisSetting> actual = GlobalSettingsHandler.GetGlobalSettings(config, projects, logger);
            DumpSettings(actual);

            // Assert
            AssertSettingExists(actual, "project.same", "YYY");

            logger.AssertSingleMessageExists("project.same", proj1.FullPath, proj2.FullPath);
            logger.AssertSingleWarningExists("project.different", proj1.FullPath, proj2.FullPath);

            AssertExpectedSettingsCount(1, actual);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(1);
        }

        [TestMethod]
        public void GlobalSettings_DuplicatesInConfig()
        {
            // Duplicate config settings with the same value should be used with info messages.
            // Duplicate config settings with different values should be ignored with warnings.

            // Arrange
            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidAnalysisConfig();

            config.AdditionalSettings = new List<AnalysisSetting>();
            AddSetting(config.AdditionalSettings, "config.dup.same", "XXX");
            AddSetting(config.AdditionalSettings, "config.dup.same", "XXX");
            AddSetting(config.AdditionalSettings, "config.dup.different", "value 1");
            AddSetting(config.AdditionalSettings, "config.dup.different", "value 2");

            // Act
            IEnumerable<AnalysisSetting> actual = GlobalSettingsHandler.GetGlobalSettings(config, Enumerable.Empty<ProjectInfo>(), logger);
            DumpSettings(actual);

            // Assert
            AssertSettingExists(actual, "config.dup.same", "XXX");

            logger.AssertSingleMessageExists("config.dup.same", config.SonarConfigDir);
            logger.AssertSingleWarningExists("config.dup.different", config.SonarConfigDir);
            AssertExpectedSettingsCount(1, actual);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(1);
        }

        [TestMethod]
        public void GlobalSettings_ValueFromProjectOverridesAnalysisConfig()
        {
            // Values set in a project file should override values set in
            // analysis config file.
            
            // Arrange
            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidAnalysisConfig();
            config.AdditionalSettings = new List<AnalysisSetting>();

            IList<ProjectInfo> projects = new List<ProjectInfo>();
            ProjectInfo proj1 = CreateProjectInfo(@"pr1.vbproj");
            projects.Add(proj1);

            // Config settings
            AddSetting(config.AdditionalSettings, "config.only", "config only value");
            AddSetting(config.AdditionalSettings, "config.and.project1", "config value");

            // Project settings
            AddSetting(proj1.GlobalAnalysisSettings, "config.and.project1", "project value");

            // Act
            IEnumerable<AnalysisSetting> actual = GlobalSettingsHandler.GetGlobalSettings(config, projects, logger);
            DumpSettings(actual);

            // Assert
            AssertSettingExists(actual, "config.only", "config only value");
            AssertSettingExists(actual, "config.and.project1", "project value");

            logger.AssertSingleMessageExists("config.and.project1", proj1.FullPath);

            AssertExpectedSettingsCount(2, actual);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void GlobalSettings_DuplicateInProjectWithConfig()
        {
            // Scenario: the same settings are provided in the config file
            // and in multiple project files.
            // * if the values in the project files agree, that value will be used
            // * if the values in the project files diagree, then the config value will be used

            // Arrange
            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidAnalysisConfig();
            config.AdditionalSettings = new List<AnalysisSetting>();

            IList<ProjectInfo> projects = new List<ProjectInfo>();
            ProjectInfo proj1 = CreateProjectInfo(@"p1.proj");
            ProjectInfo proj2 = CreateProjectInfo(@"p2.proj");
            projects.Add(proj1);
            projects.Add(proj2);

            // Config settings
            AddSetting(config.AdditionalSettings, "all.same.in.projects", "same config value");
            AddSetting(config.AdditionalSettings, "all.different.in.projects", "different config value");

            // Project settings
            AddSetting(proj1.GlobalAnalysisSettings, "all.same.in.projects", "same project value");
            AddSetting(proj1.GlobalAnalysisSettings, "all.different.in.projects", "different project value 1");

            AddSetting(proj2.GlobalAnalysisSettings, "all.same.in.projects", "same project value");
            AddSetting(proj2.GlobalAnalysisSettings, "all.different.in.projects", "different project value 2");

            // Act
            IEnumerable<AnalysisSetting> actual = GlobalSettingsHandler.GetGlobalSettings(config, projects, logger);
            DumpSettings(actual);

            // Assert
            AssertSettingExists(actual, "all.same.in.projects", "same project value");
            AssertSettingExists(actual, "all.different.in.projects", "different config value");

            logger.AssertSingleMessageExists("all.same.in.projects", proj1.FullPath, proj2.FullPath); // message about the same project values
            logger.AssertSingleMessageExists("all.same.in.projects", config.SonarConfigDir, proj1.FullPath); // message about the config value being overridden
            logger.AssertSingleWarningExists("all.different.in.projects", proj1.FullPath, proj2.FullPath); // warning about the project values being different
            
            AssertExpectedSettingsCount(2, actual);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(1);
        }

        [TestMethod]
        public void GlobalSettings_InvalidKeysAreIgnored()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidAnalysisConfig();

            IList<ProjectInfo> projects = new List<ProjectInfo>();
            ProjectInfo proj1 = CreateProjectInfo(@"c:\\project1.proj");
            projects.Add(proj1);

            config.AdditionalSettings = new List<AnalysisSetting>();
            AddSetting(config.AdditionalSettings, "  invalid config key", "invalid");

            AddSetting(proj1.GlobalAnalysisSettings, ".invalid.project.key", "invalid");

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
            Assert.IsTrue(AnalysisSetting.SettingValueComparer.Equals(expectedValue, setting.Value), "Global setting has unexpected value. Key: {0}, Value: {1}", expectedKey, expectedValue);
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

        private static ProjectInfo CreateProjectInfo(string configPath)
        {
            ProjectInfo proj = new ProjectInfo();
            proj.GlobalAnalysisSettings = new List<AnalysisSetting>();
            proj.FullPath = configPath;
            return proj;
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
