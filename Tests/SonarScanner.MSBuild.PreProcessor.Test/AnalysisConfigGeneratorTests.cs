/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using Moq;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class AnalysisConfigGeneratorTests
    {
        private static readonly Dictionary<string, string> EmptyProperties = new();

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void GenerateFile_WhenLocalSettingsNull_ThrowArgumentNullException()
        {
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var tbSettings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Action act = () => AnalysisConfigGenerator.GenerateFile(null, tbSettings, new(), EmptyProperties, new List<AnalyzerSettings>(), "9.9");

            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("localSettings");
        }

        [TestMethod]
        public void GenerateFile_WhenBuildSettingsNull_ThrowArgumentNullException()
        {
            Action act = () => AnalysisConfigGenerator.GenerateFile(CreateProcessedArgs(), null, new(), EmptyProperties, new(), "1.0");

            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("buildSettings");
        }

        [TestMethod]
        public void GenerateFile_WhenAdditionalSettingsNull_ThrowArgumentNullException()
        {
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var tbSettings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Action act = () => AnalysisConfigGenerator.GenerateFile(CreateProcessedArgs(), tbSettings, null, EmptyProperties, new(), "1.0");

            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("additionalSettings");
        }

        [TestMethod]
        public void GenerateFile_WhenServerPropertiesNull_ThrowArgumentNullException()
        {
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var tbSettings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Action act = () => AnalysisConfigGenerator.GenerateFile(CreateProcessedArgs(), tbSettings, new(), null, new(), "1.0");

            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("serverProperties");
        }

        [TestMethod]
        public void AnalysisConfGen_Simple()
        {
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var logger = new TestLogger();
            var propertyProvider = new ListPropertiesProvider();
            propertyProvider.AddProperty(SonarProperties.HostUrl, "http://foo");
            var args = CreateProcessedArgs(EmptyPropertyProvider.Instance, propertyProvider, logger);
            var tbSettings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            var serverSettings = new Dictionary<string, string> { { "server.key.1", "server.value.1" } };
            var analyzerSettings = new AnalyzerSettings
            {
                RulesetPath = "c:\\xxx.ruleset",
                AdditionalFilePaths = new List<string>()
            };
            analyzerSettings.AdditionalFilePaths.Add("f:\\additionalPath1.txt");
            analyzerSettings.AnalyzerPlugins = new List<AnalyzerPlugin> { new AnalyzerPlugin { AssemblyPaths = new List<string> { @"f:\temp\analyzer1.dll" } } };
            var analyzersSettings = new List<AnalyzerSettings> { analyzerSettings };
            var additionalSettings = new Dictionary<string, string> { { "UnchangedFilesPath", @"f:\UnchangedFiles.txt" } };
            Directory.CreateDirectory(tbSettings.SonarConfigDirectory); // config directory needs to exist

            var actualConfig = AnalysisConfigGenerator.GenerateFile(args, tbSettings, additionalSettings, serverSettings, analyzersSettings, "9.9");

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
            actualConfig.GetConfigValue("UnchangedFilesPath", null).Should().Be(@"f:\UnchangedFiles.txt");
            actualConfig.GetBuildUri().Should().Be(tbSettings.BuildUri);
            actualConfig.GetTfsUri().Should().Be(tbSettings.TfsUri);
            actualConfig.ServerSettings.Should().NotBeNull();
            actualConfig.AnalyzersSettings[0].Should().Be(analyzerSettings);

            var serverProperty = actualConfig.ServerSettings.SingleOrDefault(s => string.Equals(s.Id, "server.key.1", StringComparison.Ordinal));
            serverProperty.Should().NotBeNull();
            serverProperty.Value.Should().Be("server.value.1");
        }

        [TestMethod]
        public void AnalysisConfGen_FileProperties()
        {
            // File properties should not be copied to the file. Instead, a pointer to the file should be created.
            var logger = new TestLogger();
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var fileProperties = new AnalysisProperties
            {
                new Property() { Id = SonarProperties.HostUrl, Value = "http://myserver" },
                new Property() { Id = "file.only", Value = "file value" }
            };
            var settingsFilePath = Path.Combine(analysisDir, "settings.txt");
            fileProperties.Save(settingsFilePath);
            var fileProvider = FilePropertyProvider.Load(settingsFilePath);
            var args = CreateProcessedArgs(EmptyPropertyProvider.Instance, fileProvider, logger);
            var settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

            var actualConfig = AnalysisConfigGenerator.GenerateFile(args, settings, new(), EmptyProperties, new(), "9.9");

            AssertConfigFileExists(actualConfig);
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            var actualSettingsFilePath = actualConfig.GetSettingsFilePath();
            actualSettingsFilePath.Should().Be(settingsFilePath);

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
            var logger = new TestLogger();
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var cmdLineArgs = new ListPropertiesProvider();
            // Public args - should be written to the config file
            cmdLineArgs.AddProperty("sonar.host.url", "http://host");
            cmdLineArgs.AddProperty("public.key", "public value");
            cmdLineArgs.AddProperty("sonar.user.license.secured", "user input license");
            cmdLineArgs.AddProperty("server.key.secured.xxx", "not really secure");
            cmdLineArgs.AddProperty("sonar.value", "value.secured");
            // Sensitive values - should not be written to the config file
            cmdLineArgs.AddProperty(SonarProperties.ClientCertPassword, "secret client certificate password");
            // Create a settings file with public and sensitive data
            var fileSettings = new AnalysisProperties
            {
                new Property() { Id = "file.public.key", Value = "file public value" },
                new Property() { Id = SonarProperties.SonarUserName, Value = "secret username" },
                new Property() { Id = SonarProperties.SonarPassword, Value = "secret password" },
                new Property() { Id = SonarProperties.ClientCertPassword, Value = "secret client certificate password" }
            };
            var fileSettingsPath = Path.Combine(analysisDir, "fileSettings.txt");
            fileSettings.Save(fileSettingsPath);
            var fileProvider = FilePropertyProvider.Load(fileSettingsPath);
            var args = CreateProcessedArgs(cmdLineArgs, fileProvider, logger);
            var serverProperties = new Dictionary<string, string>
            {
                // Public server settings
                { "server.key.1", "server value 1" },
                // Sensitive server settings
                { SonarProperties.SonarUserName, "secret user" },
                { SonarProperties.SonarPassword, "secret pwd" },
                { "sonar.vbnet.license.secured", "secret license" },
                { "sonar.cpp.License.Secured", "secret license 2" }
            };
            var settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

            var config = AnalysisConfigGenerator.GenerateFile(args, settings, new(), serverProperties, new(), "9.9");

            AssertConfigFileExists(config);
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            // "Public" arguments should be in the file
            config.SonarProjectKey.Should().Be("valid.key");
            config.SonarProjectName.Should().Be("valid.name");
            config.SonarProjectVersion.Should().Be("1.0");

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
            var logger = new TestLogger();
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist
            var cmdLineArgs = new ListPropertiesProvider();
            cmdLineArgs.AddProperty(SonarProperties.SonarUserName, "foo");
            var args = CreateProcessedArgs(cmdLineArgs, EmptyPropertyProvider.Instance, logger);

            var config = AnalysisConfigGenerator.GenerateFile(args, settings, new(), EmptyProperties, new(), "9.9");

            AssertConfigFileExists(config);
            config.HasBeginStepCommandLineCredentials.Should().BeTrue();
        }

        [TestMethod]
        public void AnalysisConfGen_WhenLoginNotSpecified_DoesNotStoreThatItWasSpecified()
        {
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            var args = CreateProcessedArgs();
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

            var config = AnalysisConfigGenerator.GenerateFile(args, settings, new(), EmptyProperties, new(), "9.9");

            AssertConfigFileExists(config);
            config.HasBeginStepCommandLineCredentials.Should().BeFalse();
        }

        [TestMethod]
        public void GenerateFile_WritesSonarQubeVersion()
        {
            var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            var args = CreateProcessedArgs();
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

            var config = AnalysisConfigGenerator.GenerateFile(args, settings, new(), EmptyProperties, new(), "1.2.3.4");

            config.SonarQubeVersion.Should().Be("1.2.3.4");
        }

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
            content.IndexOf(text, System.StringComparison.InvariantCultureIgnoreCase).Should().BeNegative($"Not expecting text to be found in the file. Text: '{text}', file: {filePath}");
        }

        private static ProcessedArgs CreateProcessedArgs() =>
            CreateProcessedArgs(EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance, Mock.Of<ILogger>());

        private static ProcessedArgs CreateProcessedArgs(IAnalysisPropertyProvider cmdLineProperties, IAnalysisPropertyProvider globalFileProperties, ILogger logger) =>
            new("valid.key", "valid.name", "1.0", "organization", false, cmdLineProperties, globalFileProperties, EmptyPropertyProvider.Instance, logger);
    }
}
