/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using NSubstitute;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class AnalysisConfigGeneratorTests
{
    private static readonly Dictionary<string, string> EmptyProperties = new();

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void GenerateFile_NullArguments_Throw()
    {
        var args = CreateProcessedArgs();
        var settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext));
        var empty = new Dictionary<string, string>();
        var analyzer = new List<AnalyzerSettings>();
        ((Func<AnalysisConfig>)(() => AnalysisConfigGenerator.GenerateFile(null, settings, empty, empty, analyzer, "1.0", null)))
            .Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("localSettings");
        ((Func<AnalysisConfig>)(() => AnalysisConfigGenerator.GenerateFile(args, null, empty, empty, analyzer, "1.10.42", null)))
            .Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("buildSettings");
        ((Func<AnalysisConfig>)(() => AnalysisConfigGenerator.GenerateFile(args, settings, null, empty, analyzer, "1.42", null)))
            .Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("additionalSettings");
        ((Func<AnalysisConfig>)(() => AnalysisConfigGenerator.GenerateFile(args, settings, empty, null, analyzer, "1.42", null)))
            .Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("serverProperties");
        ((Func<AnalysisConfig>)(() => AnalysisConfigGenerator.GenerateFile(args, settings, empty, empty, null, "1.22.42", null)))
            .Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("analyzersSettings");
    }

    [TestMethod]
    public void AnalysisConfGen_Simple()
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var logger = new TestLogger();
        var propertyProvider = new ListPropertiesProvider();
        propertyProvider.AddProperty(SonarProperties.HostUrl, "http://foo");
        var args = CreateProcessedArgs(EmptyPropertyProvider.Instance, propertyProvider, logger);
        var localSettings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
        var serverSettings = new Dictionary<string, string> { { "server.key.1", "server.value.1" } };
        var analyzerSettings = new AnalyzerSettings
        {
            RulesetPath = "c:\\xxx.ruleset",
            AdditionalFilePaths = new List<string>()
        };
        analyzerSettings.AdditionalFilePaths.Add("f:\\additionalPath1.txt");
        analyzerSettings.AnalyzerPlugins = new List<AnalyzerPlugin> { new() { AssemblyPaths = new List<string> { @"f:\temp\analyzer1.dll" } } };
        var analyzersSettings = new List<AnalyzerSettings> { analyzerSettings };
        var additionalSettings = new Dictionary<string, string> { { "UnchangedFilesPath", @"f:\UnchangedFiles.txt" } };
        Directory.CreateDirectory(localSettings.SonarConfigDirectory); // config directory needs to exist

        var actualConfig = AnalysisConfigGenerator.GenerateFile(args, localSettings, additionalSettings, serverSettings, analyzersSettings, "9.9", null);

        AssertConfigFileExists(actualConfig);
        logger.AssertErrorsLogged(0);
        logger.AssertWarningsLogged(0);

        actualConfig.SonarProjectKey.Should().Be("valid.key");
        actualConfig.SonarProjectName.Should().Be("valid.name");
        actualConfig.SonarProjectVersion.Should().Be("1.0");
        actualConfig.SonarQubeHostUrl.Should().Be("http://foo");
        actualConfig.SonarBinDir.Should().Be(localSettings.SonarBinDirectory);
        actualConfig.SonarConfigDir.Should().Be(localSettings.SonarConfigDirectory);
        actualConfig.SonarOutputDir.Should().Be(localSettings.SonarOutputDirectory);
        actualConfig.SonarScannerWorkingDirectory.Should().Be(localSettings.SonarScannerWorkingDirectory);
        actualConfig.GetConfigValue("UnchangedFilesPath", null).Should().Be(@"f:\UnchangedFiles.txt");
        actualConfig.GetBuildUri().Should().Be(localSettings.BuildUri);
        actualConfig.GetTfsUri().Should().Be(localSettings.TfsUri);
        actualConfig.ServerSettings.Should().NotBeNull();
        actualConfig.AnalyzersSettings.Should().HaveElementAt(0, analyzerSettings);
        actualConfig.MultiFileAnalysis.Should().BeTrue();

        var serverProperty = actualConfig.ServerSettings.SingleOrDefault(x => string.Equals(x.Id, "server.key.1", StringComparison.Ordinal));
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
            new(SonarProperties.HostUrl, "http://myserver"),
            new("file.only", "file value"),
            new(SonarProperties.MultiFileAnalysis, "false")
        };
        var settingsFilePath = Path.Combine(analysisDir, "settings.txt");
        fileProperties.Save(settingsFilePath);
        var fileProvider = FilePropertyProvider.Load(settingsFilePath);
        var args = CreateProcessedArgs(EmptyPropertyProvider.Instance, fileProvider, logger);
        var settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

        var actualConfig = AnalysisConfigGenerator.GenerateFile(args, settings, new(), EmptyProperties, new(), "9.9", null);

        AssertConfigFileExists(actualConfig);
        logger.AssertErrorsLogged(0);
        logger.AssertWarningsLogged(0);

        var actualSettingsFilePath = actualConfig.GetSettingsFilePath();
        actualSettingsFilePath.Should().Be(settingsFilePath);

        // Check the file setting value do not appear in the config file
        AssertFileDoesNotContainText(actualConfig.FileName, "file.only");

        actualConfig.SourcesDirectory.Should().Be(settings.SourcesDirectory);
        actualConfig.SonarScannerWorkingDirectory.Should().Be(settings.SonarScannerWorkingDirectory);
        actualConfig.MultiFileAnalysis.Should().BeFalse();
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
        cmdLineArgs.AddProperty("sonar.token", "secret token");
        // Create a settings file with public and sensitive data
        var fileSettings = new AnalysisProperties
        {
            new("file.public.key", "file public value"),
            new(SonarProperties.SonarToken, "secret token"),
            new(SonarProperties.SonarUserName, "secret username"),
            new(SonarProperties.SonarPassword, "secret password"),
            new(SonarProperties.ClientCertPassword, "secret client certificate password")
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
            { SonarProperties.SonarToken, "secret token" },
            { "sonar.vbnet.license.secured", "secret license" },
            { "sonar.cpp.License.Secured", "secret license 2" }
        };
        var settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, new(), serverProperties, new(), "9.9", null);

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

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, new(), EmptyProperties, new(), "9.9", null);

        AssertConfigFileExists(config);
        config.HasBeginStepCommandLineCredentials.Should().BeTrue();
    }

    [TestMethod]
    public void AnalysisConfGen_WhenTokenIsSpecified_SetsHasBeginStepCommandLineCredentialsToTrue()
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.SonarToken, "token");
        var args = CreateProcessedArgs(cmdLineArgs, EmptyPropertyProvider.Instance, new TestLogger());

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, new(), EmptyProperties, new(), "9.9", null);

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

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, new(), EmptyProperties, new(), "9.9", null);

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

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, new(), EmptyProperties, new(), "1.2.3.4", null);

        config.SonarQubeVersion.Should().Be("1.2.3.4");
    }

    [DataTestMethod]
    [DataRow("java1.exe", "", "java1.exe")]
    [DataRow("java1.exe", " ", "java1.exe")]
    [DataRow("java1.exe", null, "java1.exe")]
    [DataRow("", "java2.exe", "java2.exe")]
    [DataRow("  ", "java2.exe", "java2.exe")]
    [DataRow(null, "java2.exe", "java2.exe")]
    [DataRow("java1.exe", "java2.exe", "java1.exe")]
    public void GenerateFile_JavaExePath_Cases(string setByUser, string resolved, string expected)
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var commandLineArguments = new ListPropertiesProvider([new Property(SonarProperties.JavaExePath, setByUser)]);
        var args = CreateProcessedArgs(commandLineArguments, EmptyPropertyProvider.Instance, Substitute.For<ILogger>());

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, new(), EmptyProperties, new(), "1.2.3.4", resolved);

        config.JavaExePath.Should().Be(expected);
    }

    private void AssertConfigFileExists(AnalysisConfig config)
    {
        config.Should().NotBeNull("Supplied config should not be null");
        config.FileName.Should().NotBeNullOrWhiteSpace("Config file name should be set");
        File.Exists(config.FileName).Should().BeTrue("Expecting the analysis config file to exist. Path: {0}", config.FileName);
        TestContext.AddResultFile(config.FileName);
    }

    private static void AssertExpectedServerSetting(string key, string expectedValue, AnalysisConfig actualConfig)
    {
        var found = Property.TryGetProperty(key, actualConfig.ServerSettings, out var property);
        found.Should().BeTrue("Expected server property was not found. Key: {0}", key);
        property.Value.Should().Be(expectedValue, "Unexpected server value. Key: {0}", key);
    }

    private static void AssertExpectedLocalSetting(string key, string expectedValue, AnalysisConfig actualConfig)
    {
        var found = Property.TryGetProperty(key, actualConfig.LocalSettings, out var property);
        found.Should().BeTrue("Expected local property was not found. Key: {0}", key);
        property.Value.Should().Be(expectedValue, "Unexpected local value. Key: {0}", key);
    }

    private static void AssertFileDoesNotContainText(string filePath, string text)
    {
        File.Exists(filePath).Should().BeTrue("File should exist: {0}", filePath);
        var content = File.ReadAllText(filePath);
        content.IndexOf(text, StringComparison.InvariantCultureIgnoreCase).Should().BeNegative($"Not expecting text to be found in the file. Text: '{text}', file: {filePath}");
    }

    private static ProcessedArgs CreateProcessedArgs() =>
        CreateProcessedArgs(EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance, Substitute.For<ILogger>());

    private static ProcessedArgs CreateProcessedArgs(IAnalysisPropertyProvider cmdLineProperties, IAnalysisPropertyProvider globalFileProperties, ILogger logger) =>
        new("valid.key",
            "valid.name",
            "1.0",
            "organization",
            false,
            cmdLineProperties,
            globalFileProperties,
            EmptyPropertyProvider.Instance,
            Substitute.For<IFileWrapper>(),
            Substitute.For<IDirectoryWrapper>(),
            Substitute.For<IOperatingSystemProvider>(),
            logger);
}
