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
        public void AnalysisConfGen_FileProperties()
        {
            // File properties should not be copied to the file.
            // Instead, a pointer to the file should be created.

            // Arrange
            string analysisDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            TestLogger logger = new TestLogger();

            // The set of file properties to supply
            AnalysisProperties fileProperties = new AnalysisProperties();
            fileProperties.Add(new Property() { Id = SonarProperties.HostUrl, Value = "http://myserver" });
            fileProperties.Add(new Property() { Id = "file.only", Value = "file value" });
            string settingsFilePath = Path.Combine(analysisDir, "settings.txt");
            fileProperties.Save(settingsFilePath);

            FilePropertyProvider fileProvider = FilePropertyProvider.Load(settingsFilePath);

            ProcessedArgs args = new ProcessedArgs("key", "name", "version", false, EmptyPropertyProvider.Instance, fileProvider);

            TeamBuildSettings settings = TeamBuildSettings.CreateNonTeamBuildSettings(analysisDir);
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

            // Act
            AnalysisConfig actualConfig = AnalysisConfigGenerator.GenerateFile(args, settings, new Dictionary<string, string>(), logger);

            // Assert
            AssertConfigFileExists(actualConfig);
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            string actualSettingsFilePath = actualConfig.GetSettingsFilePath();
            Assert.AreEqual(settingsFilePath, actualSettingsFilePath, "Unexpected settings file path");

            // Check the file setting value do not appear in the config file
            AssertFileDoesNotContainText(actualConfig.FileName, "file.only");
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
            cmdLineArgs.AddProperty(SonarProperties.DbPassword, "secret db password");
            cmdLineArgs.AddProperty(SonarProperties.DbUserName, "secret db user");

            // Create a settings file with public and sensitive data
            AnalysisProperties fileSettings = new AnalysisProperties();
            fileSettings.Add(new Property() { Id = "file.public.key", Value = "file public value" });
            fileSettings.Add(new Property() {Id = SonarProperties.DbUserName, Value = "secret db user"});
            fileSettings.Add(new Property() { Id = SonarProperties.DbPassword, Value = "secret db password"});
            string fileSettingsPath = Path.Combine(analysisDir, "fileSettings.txt");
            fileSettings.Save(fileSettingsPath);
            FilePropertyProvider fileProvider = FilePropertyProvider.Load(fileSettingsPath);

            ProcessedArgs args = new ProcessedArgs("key", "name", "1.0", false, cmdLineArgs, fileProvider);

            IDictionary<string, string> serverProperties = new Dictionary<string, string>();
            // Public server settings
            serverProperties.Add("server.key.1", "server value 1");
            // Sensitive server settings
            serverProperties.Add(SonarProperties.SonarUserName, "secret user");
            serverProperties.Add(SonarProperties.SonarPassword, "secret pwd");

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
            Assert.AreEqual("key", config.SonarProjectKey, "Unexpected project key");
            Assert.AreEqual("name", config.SonarProjectName, "Unexpected project name");
            Assert.AreEqual("1.0", config.SonarProjectVersion, "Unexpected project version");

            AssertExpectedLocalSetting(SonarProperties.HostUrl, "http://host", config);
            AssertExpectedServerSetting("server.key.1", "server value 1", config);

            AssertFileDoesNotContainText(config.FileName, "file.public.key"); // file settings values should not be in the config

            // SONARMSBRU-136: TODO - uncomment the following code:
            AssertFileDoesNotContainText(config.FileName, "secret"); // sensitive data should not be in config
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

        private static void AssertSettingDoesNotExist(string key, AnalysisConfig actualConfig)
        {
            Property setting;
            bool found = actualConfig.GetAnalysisSettings(true).TryGetProperty(key, out setting);
            Assert.IsFalse(found, "The setting should not exist. Key: {0}", key);
        }

        private static void AssertExpectedServerSetting(string key, string expectedValue, AnalysisConfig actualConfig)
        {
            Property property;
            bool found = Property.TryGetProperty(key, actualConfig.ServerSettings, out property);

            Assert.IsTrue(found, "Expected server property was not found. Key: {0}", key);
            Assert.AreEqual(expectedValue, property.Value, "Unexpected server value. Key: {0}", key);
        }

        private static void AssertExpectedLocalSetting(string key, string expectedValue, AnalysisConfig acutalConfig)
        {
            Property property;
            bool found = Property.TryGetProperty(key, acutalConfig.LocalSettings, out property);

            Assert.IsTrue(found, "Expected local property was not found. Key: {0}", key);
            Assert.AreEqual(expectedValue, property.Value, "Unexpected local value. Key: {0}", key);
        }

        private static void AssertFileDoesNotContainText(string filePath, string text)
        {
            Assert.IsTrue(File.Exists(filePath), "File should exist: {0}", filePath);

            string content = File.ReadAllText(filePath);
            Assert.IsTrue(content.IndexOf(text, System.StringComparison.InvariantCultureIgnoreCase) < 0, "Not expecting text to be found in the file. Text: '{0}', file: {1}",
                text, filePath);
        }

        #endregion

    }
}
