//-----------------------------------------------------------------------
// <copyright file="AnalysisConfigGeneratorTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System.Collections.Generic;
using System.IO;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class AnalysisConfigGeneratorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void AnalysisConfGen_LocalPropertiesOverrideServerSettings()
        {
            // Checks command line properties override those fetched from the server

            // Arrange
            string analysisDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            TestLogger logger = new TestLogger();
            
            // The set of server properties to return
            Dictionary<string, string> serverSettings = new Dictionary<string, string>();
            serverSettings.Add("shared.key1", "server value 1 - should be overridden by cmd line");
            serverSettings.Add("shared.key2", "server value 2 - should be overridden by file");
            serverSettings.Add("server.only", "server value 3 - only on server");
            serverSettings.Add("xxx", "server value xxx - lower case");

            // The set of command line properties to supply
            ListPropertiesProvider cmdLineProperties = new ListPropertiesProvider();
            cmdLineProperties.AddProperty("shared.key1", "cmd line value1 - should override server value");
            cmdLineProperties.AddProperty("cmd.line.only", "cmd line value4 - only on command line");
            cmdLineProperties.AddProperty("XXX", "cmd line value XXX - upper case");
            cmdLineProperties.AddProperty(SonarProperties.HostUrl, "http://host");

            // The set of file properties to supply
            ListPropertiesProvider fileProperties = new ListPropertiesProvider();
            fileProperties.AddProperty("shared.key1", "file value1 - should be overridden");
            fileProperties.AddProperty("shared.key2", "file value2 - should override server value");
            fileProperties.AddProperty("file.only", "file value3 - only in file");
            fileProperties.AddProperty("XXX", "cmd line value XXX - upper case");

            ProcessedArgs args = new ProcessedArgs("key", "name", "version", false, cmdLineProperties, fileProperties);

            TeamBuildSettings settings = TeamBuildSettings.CreateNonTeamBuildSettings(analysisDir);
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist


            // Act
            AnalysisConfig actualConfig =  AnalysisConfigGenerator.GenerateFile(args, settings, serverSettings, logger);


            // Assert
            AssertConfigFileExists(actualConfig);
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            AssertExpectedAnalysisSetting("shared.key1", "cmd line value1 - should override server value", actualConfig);
            AssertExpectedAnalysisSetting("shared.key2", "file value2 - should override server value", actualConfig);
            AssertExpectedAnalysisSetting("server.only", "server value 3 - only on server", actualConfig);
            AssertExpectedAnalysisSetting("cmd.line.only", "cmd line value4 - only on command line", actualConfig);
            AssertExpectedAnalysisSetting("file.only", "file value3 - only in file", actualConfig);
            AssertExpectedAnalysisSetting("xxx", "server value xxx - lower case", actualConfig);
            AssertExpectedAnalysisSetting("XXX", "cmd line value XXX - upper case", actualConfig);
            AssertExpectedAnalysisSetting(SonarProperties.HostUrl, "http://host", actualConfig);

        }

        [TestMethod]
        [WorkItem(127)] // Do not store the db and server credentials in the config files: http://jira.sonarsource.com/browse/SONARMSBRU-127
        public void AnalysisConfGen_AnalysisConfigDoesNotContainSensitiveData()
        {
            // Arrange
            string analysisDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            TestLogger logger = new TestLogger();

            ListPropertiesProvider cmdLineArgs = new ListPropertiesProvider();
            // Public args - should be written to the config file
            cmdLineArgs.AddProperty("sonar.host.url", "http://host");
            cmdLineArgs.AddProperty("public.key", "public value");

            // Sensitive values - should not be written to the config file
            cmdLineArgs.AddProperty("sonar.login", "secret login");
            cmdLineArgs.AddProperty("sonar.password", "secret password");
            cmdLineArgs.AddProperty("sonar.jdbc.username", "secret db password");
            cmdLineArgs.AddProperty("sonar.jdbc.password", "secret db password");

            ProcessedArgs args = new ProcessedArgs("key", "name", "1.0", false, cmdLineArgs, EmptyPropertyProvider.Instance);

            IDictionary<string, string> serverProperties = new Dictionary<string, string>();

            TeamBuildSettings settings = TeamBuildSettings.CreateNonTeamBuildSettings(analysisDir);
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist


            // Act
            AnalysisConfig config = AnalysisConfigGenerator.GenerateFile(args, settings, serverProperties, logger);

            // Assert
            AssertConfigFileExists(config);
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            // Check the config

            // "Public" arguments should be in the file
            AssertExpectedAnalysisSetting(SonarProperties.ProjectKey, "key", config);
            AssertExpectedAnalysisSetting(SonarProperties.ProjectName, "name", config);
            AssertExpectedAnalysisSetting(SonarProperties.ProjectVersion, "1.0", config);
            AssertExpectedAnalysisSetting(SonarProperties.HostUrl, "http://host", config);

            // Sensitive arguments should not be in the file
            AssertSettingDoesNotExist(SonarProperties.SonarUserName, config);
            AssertSettingDoesNotExist(SonarProperties.SonarPassword, config);
            AssertSettingDoesNotExist(SonarProperties.DbUserName, config);
            AssertSettingDoesNotExist(SonarProperties.DbPassword, config);
        }

        #endregion


        #region Checks

        private void AssertConfigFileExists(AnalysisConfig config)
        {
            Assert.IsNotNull(config, "Supplied config should not be null");

            Assert.IsFalse(string.IsNullOrWhiteSpace(config.FileName), "Config file name should be set");
            Assert.IsTrue(File.Exists(config.FileName), "Expecting the analysis config file to exist. Path: {0}", config.FileName);

            this.TestContext.AddResultFile(config.FileName);

        }

        private static void AssertExpectedAnalysisSetting(string key, string expectedValue, AnalysisConfig actualConfig)
        {
            AnalysisSetting setting;
            actualConfig.TryGetSetting(key, out setting);

            Assert.IsNotNull(setting, "Failed to retrieve the expected setting. Key: {0}", key);
            Assert.AreEqual(expectedValue, setting.Value, "Unexpected setting value. Key: {0}", key);
        }

        private static void AssertSettingDoesNotExist(string key, AnalysisConfig actualConfig)
        {
            AnalysisSetting setting;
            bool found = actualConfig.TryGetSetting(key, out setting);
            Assert.IsFalse(found, "The setting should not exist. Key: {0}", key);
        }

        #endregion

    }
}
