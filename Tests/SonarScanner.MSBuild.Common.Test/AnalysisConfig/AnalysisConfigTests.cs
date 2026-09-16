/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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
public class AnalysisConfigTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void AnalysisConfig_Serialization_InvalidFileName()
    {
        // 0. Setup
        var config = new AnalysisConfig();

        // 1a. Missing file name - save
        Action act = () => config.Save(null);
        act.Should().ThrowExactly<ArgumentNullException>();

        act = () => config.Save(string.Empty);
        act.Should().ThrowExactly<ArgumentNullException>();

        act = () => config.Save("\r\t ");
        act.Should().ThrowExactly<ArgumentNullException>();

        // 1b. Missing file name - load
        act = () => ProjectInfo.Load(null);
        act.Should().ThrowExactly<ArgumentNullException>();

        act = () => ProjectInfo.Load(string.Empty);
        act.Should().ThrowExactly<ArgumentNullException>();

        act = () => ProjectInfo.Load("\r\t ");
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    [Description("Checks AnalysisConfig can be serialized and deserialized")]
    public void AnalysisConfig_Serialization_SaveAndReload()
    {
        // 0. Setup
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var originalConfig = new AnalysisConfig
        {
            SonarConfigDir = @"c:\config",
            SonarOutputDir = @"c:\output",
            SonarProjectKey = @"key.1.2",
            SonarProjectName = @"My project",
            SonarProjectVersion = @"1.0",

            LocalSettings = new AnalysisProperties()
        };
        originalConfig.LocalSettings.Add(new("local.key", "local.value"));
        originalConfig.ServerSettings = new AnalysisProperties { new("server.key", "server.value") };
        var settings = new AnalyzerSettings
        {
            RulesetPath = "ruleset path",
            AdditionalFilePaths = new List<string>()
        };
        settings.AdditionalFilePaths.Add("additional path1");
        settings.AdditionalFilePaths.Add("additional path2");

        settings.AnalyzerPlugins = new List<AnalyzerPlugin>
        {
            new AnalyzerPlugin("pluginkey1", "1.2.3.4", "static-resource.zip", new List<string> { "analyzer path1", "analyzer path2" }),
            new AnalyzerPlugin("plugin-key2", "a-version", "a/b/c/d.zip", new List<string> { "analyzer path3", "analyzer path4" })
        };

        originalConfig.AnalyzersSettings = new List<AnalyzerSettings>
        {
            settings
        };

        var fileName = Path.Combine(testFolder, "config1.xml");

        SaveAndReloadConfig(originalConfig, fileName);
    }

    [TestMethod]
    [Description("Checks AnalysisConfig can be serialized and deserialized with missing values and empty collections")]
    public void AnalysisConfig_Serialization_SaveAndReload_EmptySettings()
    {
        // Arrange
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var originalConfig = new AnalysisConfig();
        var fileName = Path.Combine(testFolder, "empty_config.xml");

        // Act and assert
        SaveAndReloadConfig(originalConfig, fileName);
    }

    [TestMethod]
    [Description("Checks additional analysis settings can be serialized and deserialized")]
    public void AnalysisConfig_Serialization_AdditionalConfig()
    {
        // 0. Setup
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var originalConfig = new AnalysisConfig();

        // 1. Null list
        SaveAndReloadConfig(originalConfig, Path.Combine(testFolder, "AnalysisConfig_NullAdditionalSettings.xml"));

        // 2. Empty list
        originalConfig.AdditionalConfig = new List<ConfigSetting>();
        SaveAndReloadConfig(originalConfig, Path.Combine(testFolder, "AnalysisConfig_EmptyAdditionalSettings.xml"));

        // 3. Non-empty list
        originalConfig.AdditionalConfig.Add(new ConfigSetting() { Id = string.Empty, Value = string.Empty }); // empty item
        originalConfig.AdditionalConfig.Add(new ConfigSetting() { Id = "Id1", Value = "http://www.foo.xxx" });
        originalConfig.AdditionalConfig.Add(new ConfigSetting() { Id = "Id2", Value = "value 2" });
        SaveAndReloadConfig(originalConfig, Path.Combine(testFolder, "AnalysisConfig_NonEmptyList.xml"));
    }

    [TestMethod]
    [Description("Checks the serializer does not take an exclusive read lock")]
    [WorkItem(120)] // Regression test for http://jira.sonarsource.com/browse/SONARMSBRU-120
    public void AnalysisConfig_SharedReadAllowed()
    {
        // 0. Setup
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var filePath = Path.Combine(testFolder, "config.txt");

        var config = new AnalysisConfig();
        config.Save(filePath);

        using (var lockingStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Action a = () => AnalysisConfig.Load(filePath);
            a.Should().NotThrow();
        }
    }

    [TestMethod]
    [Description("Checks that the XML uses the expected element and attribute names, and that unrecognized elements are silently ignored")]
    public void AnalysisConfig_ExpectedXmlFormat()
    {
        // Arrange
        var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<AnalysisConfig xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""http://www.sonarsource.com/msbuild/integration/2015/1"">
  <SonarConfigDir>c:\config</SonarConfigDir>
  <SonarOutputDir>c:\output</SonarOutputDir>
  <SonarProjectKey>key.1.2</SonarProjectKey>
  <SonarProjectVersion>1.0</SonarProjectVersion>
  <SonarProjectName>My project</SonarProjectName>
  <ServerSettings>
    <Property Name=""server.key"">server.value</Property>
  </ServerSettings>

  <!-- Unexpected additional elements should be silently ignored -->
  <UnexpectedElement1 />

  <LocalSettings>
    <Property Name=""local.key"">local.value</Property>
  </LocalSettings>
  <AnalyzersSettings>
    <AnalyzerSettings>
      <RulesetPath>d:\ruleset path.ruleset</RulesetPath>
      <AnalyzerPlugins>
        <AnalyzerPlugin Key='csharp' Version='7.10.0.7896' StaticResourceName='SonarAnalyzer-7.10.0.7896.zip'>
          <AssemblyPaths>
            <Path>c:\assembly1.dll</Path>
            <Path>C:\assembly2.dll</Path>
          </AssemblyPaths>
        </AnalyzerPlugin>
        <AnalyzerPlugin Key='pluginkey2' Version='1.2.3' StaticResourceName='staticresource.zip'>
          <AssemblyPaths>
            <Path>C:\assembly3.dll</Path>
          </AssemblyPaths>
        </AnalyzerPlugin>
      </AnalyzerPlugins>
      <AdditionalFilePaths>

        <MoreUnexpectedData><Foo /></MoreUnexpectedData>

        <Path>c:\additional1.txt</Path>
      </AdditionalFilePaths>
    </AnalyzerSettings>
  </AnalyzersSettings>
</AnalysisConfig>";

        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var fullPath = TestUtils.CreateTextFile(testDir, "input.txt", xml);

        // Act
        var actual = AnalysisConfig.Load(fullPath);

        // Assert
        var expected = new AnalysisConfig
        {
            SonarConfigDir = "c:\\config",
            SonarOutputDir = "c:\\output",
            SonarProjectKey = "key.1.2",
            SonarProjectVersion = "1.0",
            SonarProjectName = "My project",
            ServerSettings = new AnalysisProperties()
        };
        expected.ServerSettings.Add(new("server.key", "server.value"));
        expected.LocalSettings = new AnalysisProperties { new("local.key", "local.value") };
        var settings = new AnalyzerSettings
        {
            RulesetPath = "d:\\ruleset path.ruleset",
            AdditionalFilePaths = new List<string>()
        };
        settings.AdditionalFilePaths.Add("c:\\additional1.txt");
        settings.AnalyzerPlugins = new List<AnalyzerPlugin>
        {
            new AnalyzerPlugin("csharp","7.10.0.7896","SonarAnalyzer-7.10.0.7896.zip", new List<string> { "c:\\assembly1.dll", "C:\\assembly2.dll" } ),
            new AnalyzerPlugin("pluginkey2", "1.2.3", "staticresource.zip", new List<string> { "C:\\assembly3.dll" })
        };

        expected.AnalyzersSettings = new List<AnalyzerSettings>
        {
            settings
        };

        AssertExpectedValues(expected, actual);
    }

    [TestMethod]
    public void ReadAdditionalSetting_WhenSettingIdIsNull_ThrowsArgumentNullException() =>
        new AnalysisConfig().Invoking(x => x.ReadAdditionalSetting(null, "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    public void ReadAdditionalSetting_WhenSettingIdIsEmpty_ThrowsArgumentNullException() =>
        new AnalysisConfig().Invoking(x => x.ReadAdditionalSetting(string.Empty, "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    public void ReadAdditionalSetting_WhenSettingIdIsWhitespace_ThrowsArgumentNullException() =>
        new AnalysisConfig().Invoking(x => x.ReadAdditionalSetting("   ", "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    public void CreatePropertyProvider_WhenLoggerIsNull_ThrowsArgumentNullException() =>
        new AnalysisConfig().Invoking(x => x.CreatePropertyProvider(false, null)).Should().Throw<ArgumentNullException>().WithParameterName("logger");

    [TestMethod]
    public void SetSettingsFilePath_WhenFileNameIsNull_ThrowsArgumentNullException() =>
        new AnalysisConfig().Invoking(x => x.SetSettingsFilePath(null)).Should().Throw<ArgumentNullException>().WithParameterName("fileName");

    [TestMethod]
    public void SetSettingsFilePath_WhenFileNameIsEmpty_ThrowsArgumentNullException() =>
        new AnalysisConfig().Invoking(x => x.SetSettingsFilePath(string.Empty)).Should().Throw<ArgumentNullException>().WithParameterName("fileName");

    [TestMethod]
    public void SetSettingsFilePath_WhenFileNameIsWhitespace_ThrowsArgumentNullException() =>
        new AnalysisConfig().Invoking(x => x.SetSettingsFilePath("   ")).Should().Throw<ArgumentNullException>().WithParameterName("fileName");

    [TestMethod]
    public void SetAdditionalSetting_WhenSettingIdIsNull_ThrowsArgumentNullException() =>
        new AnalysisConfig().Invoking(x => x.SetAdditionalSetting(null, "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    public void SetAdditionalSetting_WhenSettingIdIsEmpty_ThrowsArgumentNullException() =>
        new AnalysisConfig().Invoking(x => x.SetAdditionalSetting(string.Empty, "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    public void SetAdditionalSetting_WhenSettingIdIsWhitespace_ThrowsArgumentNullException() =>
        new AnalysisConfig().Invoking(x => x.SetAdditionalSetting("   ", "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    public void ReadAdditionalSetting_SetAdditionalSetting()
    {
        var config = new AnalysisConfig();
        var result = config.ReadAdditionalSetting("missing", "123");
        result.Should().Be("123", "Unexpected config value returned");

        // Add new
        config.SetAdditionalSetting("id1", "value1");
        config.ReadAdditionalSetting("id1", "XXX").Should().Be("value1", "Unexpected config value returned");

        // Update
        config.SetAdditionalSetting("id1", "value2");
        config.ReadAdditionalSetting("id1", "XXX").Should().Be("value2", "Unexpected config value returned");
    }

    [TestMethod]
    public void CreatePropertyProvider_LocalOnly()
    {
        // Check that local settings are always retrieved by CreatePropertyProvider
        var logger = new TestLogger();
        var config = new AnalysisConfig
        {
            LocalSettings = [
                new("local.1", "local.value.1"),
                new("local.2", "local.value.2")
            ]
        };

        // Local only
        var localProperties = config.CreatePropertyProvider(false, logger);
        localProperties.AssertExpectedPropertyCount(2);
        localProperties.AssertExpectedPropertyValue("local.1", "local.value.1");
        localProperties.AssertExpectedPropertyValue("local.2", "local.value.2");

        // Local and server
        var allProperties = config.CreatePropertyProvider(true, logger);
        allProperties.AssertExpectedPropertyCount(2);
        allProperties.AssertExpectedPropertyValue("local.1", "local.value.1");
        allProperties.AssertExpectedPropertyValue("local.2", "local.value.2");
    }

    [TestMethod]
    public void CreatePropertyProvider_ServerOnly()
    {
        // Check that local settings are only retrieved by CreatePropertyProvider if includeServerSettings is true
        var logger = new TestLogger();
        var config = new AnalysisConfig
        {
            ServerSettings = [
                new("server.1", "server.value.1"),
                new("server.2", "server.value.2")
            ]
        };

        // Local only
        var localProperties = config.CreatePropertyProvider(false, logger);
        localProperties.AssertExpectedPropertyCount(0);

        localProperties.AssertPropertyDoesNotExist("server.1");
        localProperties.AssertPropertyDoesNotExist("server.2");

        // Local and server
        var allProperties = config.CreatePropertyProvider(true, logger);
        allProperties.AssertExpectedPropertyCount(2);
        allProperties.AssertExpectedPropertyValue("server.1", "server.value.1");
        allProperties.AssertExpectedPropertyValue("server.2", "server.value.2");
    }

    [TestMethod]
    public void CreatePropertyProvider_FileSettings()
    {
        // Check that file settings are always retrieved by CreatePropertyProvider and that the file name config property is set and retrieved correctly
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
        config.ReadSettingsFilePath().Should().BeNull("Expecting the settings file path to be null");
        config.SetSettingsFilePath(settingsFilePath);
        config.ReadSettingsFilePath().Should().Be(settingsFilePath, "Unexpected settings file path value returned");

        // Check file properties are retrieved
        var provider = config.CreatePropertyProvider(false, logger);
        provider.AssertExpectedPropertyCount(2);
        provider.AssertExpectedPropertyValue("file.1", "file.value.1");
        provider.AssertExpectedPropertyValue("file.2", "file.value.2");
    }

    [TestMethod]
    public void CreatePropertyProvider_Precedence()
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
        var provider = config.CreatePropertyProvider(false, logger);
        provider.AssertExpectedPropertyCount(5);
        provider.AssertExpectedPropertyValue("local.1", "local.value.1");
        provider.AssertExpectedPropertyValue("local.2", "local.value.2");
        provider.AssertExpectedPropertyValue("file.1", "file.value.1");
        provider.AssertExpectedPropertyValue("shared.property", "shared value from local");
        provider.AssertExpectedPropertyValue("shared.property2", "shared value 2 from file");

        provider.AssertPropertyDoesNotExist("server.1");
        provider.AssertPropertyDoesNotExist("server.2");

        // Server and non-server
        provider = config.CreatePropertyProvider(true, logger);
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
    public void CreatePropertyProvider_NoSettings()
    {
        var logger = new TestLogger();
        var config = new AnalysisConfig();
        using var scope = new EnvironmentVariableScope().SetVariable("SONARQUBE_SCANNER_PARAMS", "Invalid Json to prevent provider SONARQUBE_SCANNER_PARAMS from being created");

        // No server settings
        var provider = config.CreatePropertyProvider(false, logger);
        provider.Should().NotBeNull("Returned provider should not be null");
        provider.AssertExpectedPropertyCount(0);

        // With server settings
        provider = config.CreatePropertyProvider(true, logger);
        provider.Should().NotBeNull("Returned provider should not be null");
        provider.AssertExpectedPropertyCount(0);
    }

    [TestMethod]
    public void ReadSetting_InvalidArgs_Throw()
    {
        var sut = new AnalysisConfig();
        var logger = new TestLogger();
        sut.Invoking(x => x.ReadSetting(null, true, "value", logger)).Should().Throw<ArgumentNullException>().WithParameterName("settingName");
        sut.Invoking(x => x.ReadSetting("any", true, "value", null)).Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [TestMethod]
    public void ReadSetting_NoSetting_DefaultIsReturned()
    {
        var sut = new AnalysisConfig { ServerSettings = new AnalysisProperties { new("id", "value") } };
        var logger = new TestLogger();
        sut.ReadSetting("missing", true, "default", logger).Should().Be("default");
        sut.ReadSetting("missing", true, null, logger).Should().BeNull();
        sut.ReadSetting("ID", true, "default", logger).Should().Be("default");
        sut.ReadSetting("id", false, "default", logger).Should().Be("default");
    }

    [TestMethod]
    public void ReadSetting_SettingExists_ValueIsReturned()
    {
        var sut = new AnalysisConfig
        {
            ServerSettings = new AnalysisProperties { new("id1", "server value") },
            LocalSettings = new AnalysisProperties { new("id1", "local value") }
        };
        sut.ReadSetting("id1", true, "local value", new TestLogger()).Should().Be("local value", "Local value should take precedence");
    }

    private void SaveAndReloadConfig(AnalysisConfig original, string outputFileName)
    {
        File.Exists(outputFileName).Should().BeFalse("Test error: file should not exist at the start of the test. File: {0}", outputFileName);
        original.Save(outputFileName);
        File.Exists(outputFileName).Should().BeTrue("Failed to create the output file. File: {0}", outputFileName);
        TestContext.AddResultFile(outputFileName);

        var reloaded = AnalysisConfig.Load(outputFileName);
        reloaded.Should().NotBeNull("Reloaded analysis config should not be null");

        AssertExpectedValues(original, reloaded);
    }

    private static void AssertExpectedValues(AnalysisConfig expected, AnalysisConfig actual)
    {
        actual.SonarProjectKey.Should().Be(expected.SonarProjectKey, "Unexpected project key");
        actual.SonarProjectName.Should().Be(expected.SonarProjectName, "Unexpected project name");
        actual.SonarProjectVersion.Should().Be(expected.SonarProjectVersion, "Unexpected project version");

        actual.SonarConfigDir.Should().Be(expected.SonarConfigDir, "Unexpected config directory");
        actual.SonarOutputDir.Should().Be(expected.SonarOutputDir, "Unexpected output directory");

        CompareAdditionalSettings(expected, actual);

        CompareAnalyzerSettings(expected.AnalyzersSettings, actual.AnalyzersSettings);
    }

    private static void CompareAdditionalSettings(AnalysisConfig expected, AnalysisConfig actual)
    {
        // The XmlSerializer should create an empty list
        actual.AdditionalConfig.Should().NotBeNull("Not expecting the AdditionalSettings to be null for a reloaded file");

        if (expected.AdditionalConfig == null || expected.AdditionalConfig.Count == 0)
        {
            actual.AdditionalConfig.Should().BeEmpty("Not expecting any additional items. Count: {0}", actual.AdditionalConfig.Count);
            return;
        }

        foreach (var expectedSetting in expected.AdditionalConfig)
        {
            AssertSettingExists(expectedSetting.Id, expectedSetting.Value, actual);
        }
        actual.AdditionalConfig.Should().HaveCount(expected.AdditionalConfig.Count, "Unexpected number of additional settings");
    }

    private static void AssertSettingExists(string settingId, string expectedValue, AnalysisConfig actual)
    {
        actual.AdditionalConfig.Should().NotBeNull("Not expecting the additional settings to be null");

        var actualSetting = actual.AdditionalConfig.FirstOrDefault(s => string.Equals(settingId, s.Id, StringComparison.InvariantCultureIgnoreCase));
        actualSetting.Should().NotBeNull("Expected setting not found: {0}", settingId);
        actualSetting.Value.Should().Be(expectedValue, "Setting does not have the expected value. SettingId: {0}", settingId);
    }

    private static void CompareAnalyzerSettings(IList<AnalyzerSettings> expectedList, IList<AnalyzerSettings> actualList)
    {
        actualList.Should().NotBeNull("Not expecting the AnalyzersSettings to be null for a reloaded file");

        if (expectedList == null)
        {
            actualList.Should().BeEmpty("Expecting the reloaded analyzers settings to be empty");
            return;
        }

        actualList.Should().NotBeNull("Not expecting the actual analyzers settings to be null for a reloaded file");

        actualList.Should().HaveCount(expectedList.Count, "Expecting number of analyzer settings to be the same");

        for (var i = 0; i < actualList.Count; i++)
        {
            var actual = actualList[i];
            var expected = expectedList[i];

            actual.RulesetPath.Should().Be(expected.RulesetPath, "Unexpected Ruleset value");

            actual.AnalyzerPlugins.Should().BeEquivalentTo(expected.AnalyzerPlugins, "Analyzer plugins do not match");
            actual.AdditionalFilePaths.Should().BeEquivalentTo(expected.AdditionalFilePaths, "Additional file paths do not match");
        }
    }
}
