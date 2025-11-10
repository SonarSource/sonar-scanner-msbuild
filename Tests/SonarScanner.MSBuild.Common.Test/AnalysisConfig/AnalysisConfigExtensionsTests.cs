/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class AnalysisConfigExtensionsTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void GetConfigValue_WhenConfigIsNull_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.GetConfigValue(null, "settingId", "default")).Should().Throw<ArgumentNullException>().WithParameterName("config");

    [TestMethod]
    public void GetConfigValue_WhenSettingIdIsNull_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.GetConfigValue(new(), null, "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    public void GetConfigValue_WhenSettingIdIsEmpty_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.GetConfigValue(new(), string.Empty, "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    public void GetConfigValue_WhenSettingIdIsWhitespace_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.GetConfigValue(new(), "   ", "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    public void AnalysisSettings_WhenConfigIsNull_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.AnalysisSettings(null, false, new TestLogger())).Should().Throw<ArgumentNullException>().WithParameterName("config");

    [TestMethod]
    public void AnalysisSettings_WhenLoggerIsNull_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.AnalysisSettings(new(), false, null)).Should().Throw<ArgumentNullException>().WithParameterName("logger");

    [TestMethod]
    public void SetSettingsFilePath_WhenConfigIsNull_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.SetSettingsFilePath(null, "fileName")).Should().Throw<ArgumentNullException>().WithParameterName("config");

    [TestMethod]
    public void SetSettingsFilePath_WhenFileNameIsNull_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.SetSettingsFilePath(new(), null)).Should().Throw<ArgumentNullException>().WithParameterName("fileName");

    [TestMethod]
    public void SetSettingsFilePath_WhenFileNameIsEmpty_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.SetSettingsFilePath(new(), string.Empty)).Should().Throw<ArgumentNullException>().WithParameterName("fileName");

    [TestMethod]
    public void SetSettingsFilePath_WhenFileNameIsWhitespace_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.SetSettingsFilePath(new(), "   ")).Should().Throw<ArgumentNullException>().WithParameterName("fileName");

    [TestMethod]
    public void GetSettingsFilePath_WhenConfigIsNull_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.GetSettingsFilePath(null)).Should().Throw<ArgumentNullException>().WithParameterName("config");

    [TestMethod]
    public void SetConfigValue_WhenConfigIsNull_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.SetConfigValue(null, "settingId", "default")).Should().Throw<ArgumentNullException>().WithParameterName("config");

    [TestMethod]
    public void SetConfigValue_WhenSettingIdIsNull_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.SetConfigValue(new(), null, "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    public void SetConfigValue_WhenSettingIdIsEmpty_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.SetConfigValue(new(), string.Empty, "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    public void SetConfigValue_WhenSettingIdIsWhitespace_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.SetConfigValue(new AnalysisConfig(), "   ", "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    [Description("Checks the extension methods for getting and setting values")]
    public void ConfigExt_GetAndSet()
    {
        var config = new AnalysisConfig();
        var result = config.GetConfigValue("missing", "123");
        result.Should().Be("123", "Unexpected config value returned");

        // Add new
        config.SetConfigValue("id1", "value1");
        config.GetConfigValue("id1", "XXX").Should().Be("value1", "Unexpected config value returned");

        // Update
        config.SetConfigValue("id1", "value2");
        config.GetConfigValue("id1", "XXX").Should().Be("value2", "Unexpected config value returned");
    }

    [TestMethod]
    public void ConfigExt_AnalysisSettings_LocalOnly()
    {
        // Check that local settings are always retrieved by AnalysisSettings
        var logger = new TestLogger();
        var config = new AnalysisConfig
        {
            LocalSettings = [
                new("local.1", "local.value.1"),
                new("local.2", "local.value.2")
            ]
        };

        // Local only
        var localProperties = config.AnalysisSettings(false, logger);
        localProperties.AssertExpectedPropertyCount(2);
        localProperties.AssertExpectedPropertyValue("local.1", "local.value.1");
        localProperties.AssertExpectedPropertyValue("local.2", "local.value.2");

        // Local and server
        var allProperties = config.AnalysisSettings(true, logger);
        allProperties.AssertExpectedPropertyCount(2);
        allProperties.AssertExpectedPropertyValue("local.1", "local.value.1");
        allProperties.AssertExpectedPropertyValue("local.2", "local.value.2");
    }

    [TestMethod]
    public void ConfigExt_AnalysisSettings_ServerOnly()
    {
        // Check that local settings are only retrieved by AnalysisSettings if includeServerSettings is true
        var logger = new TestLogger();
        var config = new AnalysisConfig
        {
            ServerSettings = [
                new("server.1", "server.value.1"),
                new("server.2", "server.value.2")
            ]
        };

        // Local only
        var localProperties = config.AnalysisSettings(false, logger);
        localProperties.AssertExpectedPropertyCount(0);

        localProperties.AssertPropertyDoesNotExist("server.1");
        localProperties.AssertPropertyDoesNotExist("server.2");

        // Local and server
        var allProperties = config.AnalysisSettings(true, logger);
        allProperties.AssertExpectedPropertyCount(2);
        allProperties.AssertExpectedPropertyValue("server.1", "server.value.1");
        allProperties.AssertExpectedPropertyValue("server.2", "server.value.2");
    }

    [TestMethod]
    public void ConfigExt_AnalysisSettings_FileSettings()
    {
        // Check that file settings are always retrieved by AnalysisSettings and that the file name config property is set and retrieved correctly
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var logger = new TestLogger();
        var config = new AnalysisConfig();
        var fileSettings = new AnalysisProperties
        {
            new("file.1", "file.value.1"),
            new("file.2", "file.value.2")
        };
        var settingsFilePath = Path.Combine(testDir, "settings.txt");
        fileSettings.Save(settingsFilePath);
        config.GetSettingsFilePath().Should().BeNull("Expecting the settings file path to be null");
        config.SetSettingsFilePath(settingsFilePath);
        config.GetSettingsFilePath().Should().Be(settingsFilePath, "Unexpected settings file path value returned");

        // Check file properties are retrieved
        var provider = config.AnalysisSettings(false, logger);
        provider.AssertExpectedPropertyCount(2);
        provider.AssertExpectedPropertyValue("file.1", "file.value.1");
        provider.AssertExpectedPropertyValue("file.2", "file.value.2");
    }

    [TestMethod]
    public void ConfigExt_AnalysisSettings_Precedence()
    {
        // Expected precedence: local -> file -> server
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var config = new AnalysisConfig();
        var logger = new TestLogger();
        var fileSettings = new AnalysisProperties
        {
            new("file.1", "file.value.1"),
            new("shared.property", "shared value from file - should never be returned"),
            new("shared.property2", "shared value 2 from file")
        };
        var settingsFilePath = Path.Combine(testDir, "settings.txt");
        fileSettings.Save(settingsFilePath);
        config.SetSettingsFilePath(settingsFilePath);
        config.LocalSettings = [
            new("local.1", "local.value.1"),
            new("local.2", "local.value.2"),
            new("shared.property", "shared value from local")
        ];
        config.ServerSettings = [
            new("server.1", "server.value.1"),
            new("server.2", "server.value.2"),
            new("shared.property", "shared value from server - should never be returned"),
            new("shared.property2", "shared value 2 from server - should never be returned")
        ];

        // Precedence - local should win over file
        var provider = config.AnalysisSettings(false, logger);
        provider.AssertExpectedPropertyCount(5);
        provider.AssertExpectedPropertyValue("local.1", "local.value.1");
        provider.AssertExpectedPropertyValue("local.2", "local.value.2");
        provider.AssertExpectedPropertyValue("file.1", "file.value.1");
        provider.AssertExpectedPropertyValue("shared.property", "shared value from local");
        provider.AssertExpectedPropertyValue("shared.property2", "shared value 2 from file");

        provider.AssertPropertyDoesNotExist("server.1");
        provider.AssertPropertyDoesNotExist("server.2");

        // Server and non-server
        provider = config.AnalysisSettings(true, logger);
        provider.AssertExpectedPropertyCount(7);
        provider.AssertExpectedPropertyValue("local.1", "local.value.1");
        provider.AssertExpectedPropertyValue("local.2", "local.value.2");
        provider.AssertExpectedPropertyValue("file.1", "file.value.1");
        provider.AssertExpectedPropertyValue("shared.property", "shared value from local");
        provider.AssertExpectedPropertyValue("shared.property2", "shared value 2 from file");
        provider.AssertExpectedPropertyValue("server.1", "server.value.1");
        provider.AssertExpectedPropertyValue("server.2", "server.value.2");
    }

    [TestMethod]
    public void ConfigExt_AnalysisSettings_NoSettings()
    {
        var logger = new TestLogger();
        var config = new AnalysisConfig();
        using var scope = new EnvironmentVariableScope().SetVariable("SONARQUBE_SCANNER_PARAMS", "Invalid Json to prevent provider SONARQUBE_SCANNER_PARAMS from being created");

        // No server settings
        var provider = config.AnalysisSettings(false, logger);
        provider.Should().NotBeNull("Returned provider should not be null");
        provider.AssertExpectedPropertyCount(0);

        // With server settings
        provider = config.AnalysisSettings(true, logger);
        provider.Should().NotBeNull("Returned provider should not be null");
        provider.AssertExpectedPropertyCount(0);
    }

    [TestMethod]
    public void ConfigExt_FindServerVersion_WhenConfigIsNull_Throws() =>
        FluentActions.Invoking(() => ConfigSettingsExtensions.FindServerVersion(null)).Should().Throw<ArgumentNullException>().WithParameterName("config");

    [TestMethod]
    public void ConfigExt_FindServerVersion_NoSetting_ReturnsNull() =>
        ConfigSettingsExtensions.FindServerVersion(new AnalysisConfig()).Should().BeNull();

    [TestMethod]
    public void ConfigExt_FindServerVersion_InvalidVersion_ReturnsNull() =>
        ConfigSettingsExtensions.FindServerVersion(new AnalysisConfig { SonarQubeVersion = "invalid" }).Should().BeNull();

    [TestMethod]
    public void ConfigExt_FindServerVersion_ValidVersion_ReturnsVersion() =>
        ConfigSettingsExtensions.FindServerVersion(new AnalysisConfig { SonarQubeVersion = "6.7.1.2" }).Should().Be(new Version("6.7.1.2"));

    [TestMethod]
    public void ConfigExt_GetSettingOrDefault_InvalidArgs_Throw()
    {
        var config = new AnalysisConfig();
        var logger = new TestLogger();
        FluentActions.Invoking(() => ConfigSettingsExtensions.GetSettingOrDefault(null, "any", true, "value", logger)).Should().Throw<ArgumentNullException>().WithParameterName("config");
        FluentActions.Invoking(() => ConfigSettingsExtensions.GetSettingOrDefault(config, null, true, "value", logger)).Should().Throw<ArgumentNullException>().WithParameterName("settingName");
        FluentActions.Invoking(() => ConfigSettingsExtensions.GetSettingOrDefault(config, "any", true, "value", null)).Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [TestMethod]
    public void ConfigExt_GetSettingOrDefault_NoSetting_DefaultIsReturned()
    {
        var config = new AnalysisConfig { ServerSettings = new AnalysisProperties { new("id", "value") } };
        var logger = new TestLogger();
        ConfigSettingsExtensions.GetSettingOrDefault(config, "missing", true, "default", logger).Should().Be("default");
        ConfigSettingsExtensions.GetSettingOrDefault(config, "missing", true, null, logger).Should().BeNull();
        ConfigSettingsExtensions.GetSettingOrDefault(config, "ID", true, "default", logger).Should().Be("default");
        ConfigSettingsExtensions.GetSettingOrDefault(config, "id", false, "default", logger).Should().Be("default");
    }

    [TestMethod]
    public void ConfigExt_GetSettingOrDefault_SettingExists_ValueIsReturned()
    {
        var config = new AnalysisConfig
        {
            ServerSettings = new AnalysisProperties { new("id1", "server value") },
            LocalSettings = new AnalysisProperties { new("id1", "local value") }
        };
        var logger = new TestLogger();

        // Local value should take precedence
        var result = ConfigSettingsExtensions.GetSettingOrDefault(config, "id1", true, "local value", logger);
        result.Should().Be("local value");
    }
}
