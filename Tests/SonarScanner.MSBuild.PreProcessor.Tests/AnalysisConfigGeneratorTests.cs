/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.TFS;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Tests
{
    [TestClass]
    public class AnalysisConfigGeneratorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void GenerateFile_WhenLocalSettingsNull_ThrowArgumentNullException()
        {
            // Arrange
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var tbSettings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Action act = () => AnalysisConfigGenerator.GenerateFile(null, tbSettings, new Dictionary<string, string>(),
                new List<AnalyzerSettings>(), new MockSonarQubeServer(), new TestLogger());

            // Act & Assert
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("localSettings");
        }

        [TestMethod]
        public void GenerateFile_WhenBuildSettingsNull_ThrowArgumentNullException()
        {
            // Arrange
            Action act = () => AnalysisConfigGenerator.GenerateFile(new ProcessedArgs("key", "name", "version", "org", false,
                EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance), null,
                new Dictionary<string, string>(), new List<AnalyzerSettings>(), new MockSonarQubeServer(), new TestLogger());

            // Act & Assert
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("buildSettings");
        }

        [TestMethod]
        public void GenerateFile_WhenServerPropertiesNull_ThrowArgumentNullException()
        {
            // Arrange
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var tbSettings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Action act = () => AnalysisConfigGenerator.GenerateFile(new ProcessedArgs("key", "name", "version", "org", false,
                EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance), tbSettings,
                null, new List<AnalyzerSettings>(), new MockSonarQubeServer(), new TestLogger());

            // Act & Assert
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("serverProperties");
        }

        [TestMethod]
        public void GenerateFile_WhenSonarQubeServerNull_ThrowArgumentNullException()
        {
            // Arrange
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var tbSettings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Action act = () => AnalysisConfigGenerator.GenerateFile(new ProcessedArgs("key", "name", "version", "org", false,
                EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance), tbSettings,
                new Dictionary<string, string>(), new List<AnalyzerSettings>(), null, new TestLogger());

            // Act & Assert
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("sonarQubeServer");
        }

        [TestMethod]
        public void GenerateFile_WhenLoggerNull_ThrowArgumentNullException()
        {
            // Arrange
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var tbSettings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Action act = () => AnalysisConfigGenerator.GenerateFile(new ProcessedArgs("key", "name", "version", "org", false,
                EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance), tbSettings,
                new Dictionary<string, string>(), new List<AnalyzerSettings>(), new MockSonarQubeServer(), null);

            // Act & Assert
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void AnalysisConfGen_Simple()
        {
            // Arrange
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var logger = new TestLogger();

            var propertyProvider = new ListPropertiesProvider();
            propertyProvider.AddProperty(SonarProperties.HostUrl, "http://foo");
            var args = new ProcessedArgs("valid.key", "valid.name", "1.0", null, false, EmptyPropertyProvider.Instance, propertyProvider, EmptyPropertyProvider.Instance);

            var tbSettings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);

            var serverSettings = new Dictionary<string, string>
            {
                { "server.key.1", "server.value.1" }
            };

            var analyzerSettings = new AnalyzerSettings
            {
                RuleSetFilePath = "c:\\xxx.ruleset",
                AdditionalFilePaths = new List<string>()
            };
            analyzerSettings.AdditionalFilePaths.Add("f:\\additionalPath1.txt");
            analyzerSettings.AnalyzerPlugins = new List<AnalyzerPlugin>
            {
                new AnalyzerPlugin
                {
                    AssemblyPaths = new List<string> { "f:\\temp\\analyzer1.dll" }
                }
            };

            var analyzersSettings = new List<AnalyzerSettings>
            {
                analyzerSettings
            };
            Directory.CreateDirectory(tbSettings.SonarConfigDirectory); // config directory needs to exist

            // Act
            var actualConfig = AnalysisConfigGenerator.GenerateFile(args, tbSettings, serverSettings, analyzersSettings, new MockSonarQubeServer(), logger);

            // Assert
            AssertConfigFileExists(actualConfig);
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            actualConfig.SonarProjectKey.Should().Be("valid.key");
            actualConfig.SonarProjectName.Should().Be("valid.name");
            actualConfig.SonarProjectVersion.Should().Be("1.0");

            actualConfig.SonarQubeHostUrl.Should().Be("http://foo");

            actualConfig.SonarBinDir.Should().Be(tbSettings.SonarBinDirectory);
            actualConfig.SonarConfigDir.Should().Be(tbSettings.SonarConfigDirectory);
            actualConfig.SonarOutputDir.Should().Be(tbSettings.SonarOutputDirectory);
            actualConfig.SonarScannerWorkingDirectory.Should().Be(tbSettings.SonarScannerWorkingDirectory);
            actualConfig.GetBuildUri().Should().Be(tbSettings.BuildUri);
            actualConfig.GetTfsUri().Should().Be(tbSettings.TfsUri);

            actualConfig.ServerSettings.Should().NotBeNull();
            var serverProperty = actualConfig.ServerSettings.SingleOrDefault(s => string.Equals(s.Id, "server.key.1", System.StringComparison.Ordinal));
            serverProperty.Should().NotBeNull();
            serverProperty.Value.Should().Be("server.value.1");
            actualConfig.AnalyzersSettings[0].Should().Be(analyzerSettings);
        }

        [TestMethod]
        public void AnalysisConfGen_FileProperties()
        {
            // File properties should not be copied to the file.
            // Instead, a pointer to the file should be created.

            // Arrange
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var logger = new TestLogger();

            // The set of file properties to supply
            var fileProperties = new AnalysisProperties
            {
                new Property() { Id = SonarProperties.HostUrl, Value = "http://myserver" },
                new Property() { Id = "file.only", Value = "file value" }
            };
            var settingsFilePath = Path.Combine(analysisDir, "settings.txt");
            fileProperties.Save(settingsFilePath);

            var fileProvider = FilePropertyProvider.Load(settingsFilePath);

            var args = new ProcessedArgs("key", "name", "version", "organization", false, EmptyPropertyProvider.Instance, fileProvider, EmptyPropertyProvider.Instance);

            var settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

            // Act
            var actualConfig = AnalysisConfigGenerator.GenerateFile(args, settings, new Dictionary<string, string>(), new List<AnalyzerSettings>(), new MockSonarQubeServer(), logger);

            // Assert
            AssertConfigFileExists(actualConfig);
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            var actualSettingsFilePath = actualConfig.GetSettingsFilePath();
            actualSettingsFilePath.Should().Be(settingsFilePath, "Unexpected settings file path");

            // Check the file setting value do not appear in the config file
            AssertFileDoesNotContainText(actualConfig.FileName, "file.only");

            actualConfig.SourcesDirectory.Should().Be(settings.SourcesDirectory);
            actualConfig.SonarScannerWorkingDirectory.Should().Be(settings.SonarScannerWorkingDirectory);
            AssertExpectedLocalSetting(SonarProperties.Organization, "organization", actualConfig);
        }

        [TestMethod]
        [WorkItem(127)] // Do not store the db and server credentials in the config files: http://jira.sonarsource.com/browse/SONARMSBRU-127
        public void AnalysisConfGen_AnalysisConfigDoesNotContainSensitiveData()
        {
            // Arrange
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var logger = new TestLogger();

            var cmdLineArgs = new ListPropertiesProvider();
            // Public args - should be written to the config file
            cmdLineArgs.AddProperty("sonar.host.url", "http://host");
            cmdLineArgs.AddProperty("public.key", "public value");
            cmdLineArgs.AddProperty("sonar.user.license.secured", "user input license");
            cmdLineArgs.AddProperty("server.key.secured.xxx", "not really secure");
            cmdLineArgs.AddProperty("sonar.value", "value.secured");

            // Sensitive values - should not be written to the config file
            cmdLineArgs.AddProperty(SonarProperties.DbPassword, "secret db password");

            // Create a settings file with public and sensitive data
            var fileSettings = new AnalysisProperties
            {
                new Property() { Id = "file.public.key", Value = "file public value" },
                new Property() { Id = SonarProperties.DbUserName, Value = "secret db user" },
                new Property() { Id = SonarProperties.DbPassword, Value = "secret db password" }
            };
            var fileSettingsPath = Path.Combine(analysisDir, "fileSettings.txt");
            fileSettings.Save(fileSettingsPath);
            var fileProvider = FilePropertyProvider.Load(fileSettingsPath);

            var args = new ProcessedArgs("key", "name", "1.0", null, false, cmdLineArgs, fileProvider, EmptyPropertyProvider.Instance);

            IDictionary<string, string> serverProperties = new Dictionary<string, string>
            {
                // Public server settings
                { "server.key.1", "server value 1" },
                // Sensitive server settings
                { SonarProperties.SonarUserName, "secret user" },
                { SonarProperties.SonarPassword, "secret pwd" },
                { "sonar.vbnet.license.secured", "secret license" },
                { "sonar.cpp.License.Secured", "secret license 2" }
            };

            var settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

            // Act
            var config = AnalysisConfigGenerator.GenerateFile(args, settings, serverProperties, new List<AnalyzerSettings>(), new MockSonarQubeServer(), logger);

            // Assert
            AssertConfigFileExists(config);
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            // Check the config

            // "Public" arguments should be in the file
            config.SonarProjectKey.Should().Be("key", "Unexpected project key");
            config.SonarProjectName.Should().Be("name", "Unexpected project name");
            config.SonarProjectVersion.Should().Be("1.0", "Unexpected project version");

            AssertExpectedLocalSetting(SonarProperties.HostUrl, "http://host", config);
            AssertExpectedLocalSetting("sonar.user.license.secured", "user input license", config); // we only filter out *.secured server settings
            AssertExpectedLocalSetting("sonar.value", "value.secured", config);
            AssertExpectedLocalSetting("server.key.secured.xxx", "not really secure", config);
            AssertExpectedServerSetting("server.key.1", "server value 1", config);

            AssertFileDoesNotContainText(config.FileName, "file.public.key"); // file settings values should not be in the config
            AssertFileDoesNotContainText(config.FileName, "secret"); // sensitive data should not be in config
        }

        [TestMethod]
        public void AnalysisConfGen_WhenLoginSpecified_StoresThatItWasSpecified()
        {
            // Arrange
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var logger = new TestLogger();

            var settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

            var cmdLineArgs = new ListPropertiesProvider();
            cmdLineArgs.AddProperty(SonarProperties.SonarUserName, "foo");
            var args = new ProcessedArgs("valid.key", "valid.name", "1.0", null, false, cmdLineArgs, EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance);

            // Act
            var config = AnalysisConfigGenerator.GenerateFile(args, settings, new Dictionary<string, string>(), new List<AnalyzerSettings>(), new MockSonarQubeServer(), logger);

            // Assert
            AssertConfigFileExists(config);
            config.HasBeginStepCommandLineCredentials.Should().BeTrue();
        }

        [TestMethod]
        public void AnalysisConfGen_WhenLoginNotSpecified_DoesNotStoreThatItWasSpecified()
        {
            // Arrange
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var logger = new TestLogger();

            var settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

            var args = new ProcessedArgs("valid.key", "valid.name", "1.0", null, false, EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance);

            // Act
            var config = AnalysisConfigGenerator.GenerateFile(args, settings, new Dictionary<string, string>(), new List<AnalyzerSettings>(), new MockSonarQubeServer(), logger);

            // Assert
            AssertConfigFileExists(config);
            config.HasBeginStepCommandLineCredentials.Should().BeFalse();
        }

        [TestMethod]
        public void GenerateFile_WritesSonarQubeVersion()
        {
            // Arrange
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);

            var args = new ProcessedArgs("valid.key", "valid.name", "1.0", null, false, EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance);

            var sonarqubeServer = new MockSonarQubeServer();
            sonarqubeServer.Data.SonarQubeVersion = new Version(1, 2, 3, 4);

            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

            // Act
            var config = AnalysisConfigGenerator.GenerateFile(args, settings, new Dictionary<string, string>(), new List<AnalyzerSettings>(), sonarqubeServer, new TestLogger());

            // Assert
            config.SonarQubeVersion.Should().Be("1.2.3.4");
        }

        #endregion Tests

        #region Checks

        private void AssertConfigFileExists(AnalysisConfig config)
        {
            config.Should().NotBeNull("Supplied config should not be null");

            string.IsNullOrWhiteSpace(config.FileName).Should().BeFalse("Config file name should be set");
            File.Exists(config.FileName).Should().BeTrue("Expecting the analysis config file to exist. Path: {0}", config.FileName);

            TestContext.AddResultFile(config.FileName);
        }

        private static void AssertExpectedServerSetting(string key, string expectedValue, AnalysisConfig actualConfig)
        {
            var found = Property.TryGetProperty(key, actualConfig.ServerSettings, out Property property);

            found.Should().BeTrue("Expected server property was not found. Key: {0}", key);
            property.Value.Should().Be(expectedValue, "Unexpected server value. Key: {0}", key);
        }

        private static void AssertExpectedLocalSetting(string key, string expectedValue, AnalysisConfig acutalConfig)
        {
            var found = Property.TryGetProperty(key, acutalConfig.LocalSettings, out Property property);

            found.Should().BeTrue("Expected local property was not found. Key: {0}", key);
            property.Value.Should().Be(expectedValue, "Unexpected local value. Key: {0}", key);
        }

        private static void AssertFileDoesNotContainText(string filePath, string text)
        {
            File.Exists(filePath).Should().BeTrue("File should exist: {0}", filePath);

            var content = File.ReadAllText(filePath);
            content.IndexOf(text, System.StringComparison.InvariantCultureIgnoreCase).Should().BeNegative("Not expecting text to be found in the file. Text: '{0}', file: {1}",
                text, filePath);
        }

        #endregion Checks
    }
}
