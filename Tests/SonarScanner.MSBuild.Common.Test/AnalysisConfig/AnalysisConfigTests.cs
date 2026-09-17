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
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\r\t ")]
    public void Save_InvalidFileName(string fileName) =>
        new AnalysisConfig().Invoking(x => x.Save(fileName)).Should().ThrowExactly<ArgumentNullException>();

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\r\t ")]
    public void Load_InvalidFileName(string fileName) =>
        FluentActions.Invoking(() => AnalysisConfig.Load(fileName)).Should().ThrowExactly<ArgumentNullException>();

    [TestMethod]
    [Description("Checks AnalysisConfig can be serialized and deserialized")]
    public void Serialization_SaveAndReload()
    {
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var originalConfig = new AnalysisConfig
        {
            SonarConfigDir = @"c:\config",
            SonarOutputDir = @"c:\output",
            SonarProjectKey = @"key.1.2",
            SonarProjectName = @"My project",
            SonarProjectVersion = @"1.0",
            LocalSettings = [new("local.key", "local.value")],
            ServerSettings = [new("server.key", "server.value")],
            AnalyzersSettings = [
                new()
                {
                    RulesetPath = "ruleset path",
                    AdditionalFilePaths = ["additional path1", "additional path2"],
                    AnalyzerPlugins = [
                        new AnalyzerPlugin("pluginkey1", "1.2.3.4", "static-resource.zip", ["analyzer path1", "analyzer path2"]),
                        new AnalyzerPlugin("plugin-key2", "a-version", "a/b/c/d.zip", ["analyzer path3", "analyzer path4"])]
                }
            ]
        };
        SaveAndReloadConfig(originalConfig, Path.Combine(testFolder, "config1.xml"));
    }

    [TestMethod]
    public void Serialization_SaveAndReload_EmptySettings()
    {
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var originalConfig = new AnalysisConfig();
        var fileName = Path.Combine(testFolder, "empty_config.xml");
        SaveAndReloadConfig(originalConfig, fileName);
    }

    [TestMethod]
    public void Serialization_AdditionalConfig()
    {
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var config = new AnalysisConfig();

        // AdditionalConfig is null
        SaveAndReloadConfig(config, Path.Combine(testFolder, "NullAdditionalSettings.xml"));

        config.AdditionalConfig = [];
        SaveAndReloadConfig(config, Path.Combine(testFolder, "EmptyAdditionalSettings.xml"));

        config.AdditionalConfig = [
            new() { Id = string.Empty, Value = string.Empty },
            new() { Id = "Id1", Value = "http://www.foo.xxx" },
            new() { Id = "Id2", Value = "value 2" }];
        SaveAndReloadConfig(config, Path.Combine(testFolder, "NonEmptyList.xml"));
    }

    [TestMethod]
    public void Load_SharedReadAllowed()
    {
        // Regression test for http://jira.sonarsource.com/browse/SONARMSBRU-120
        var filePath = Path.Combine(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext), "config.xml");
        var config = new AnalysisConfig();
        config.Save(filePath);
        using var lockingStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        AnalysisConfig.Load(filePath).Should().BeEquivalentTo(config);
    }

    [TestMethod]
    public void ExpectedXmlFormat()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <AnalysisConfig xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://www.sonarsource.com/msbuild/integration/2015/1">
              <SonarConfigDir>c:\config</SonarConfigDir>
              <SonarOutputDir>c:\output</SonarOutputDir>
              <SonarProjectKey>key.1.2</SonarProjectKey>
              <SonarProjectVersion>1.0</SonarProjectVersion>
              <SonarProjectName>My project</SonarProjectName>
              <ServerSettings>
                <Property Name="server.key">server.value</Property>
              </ServerSettings>

              <!-- Unexpected additional elements should be silently ignored -->
              <UnexpectedElement1 />

              <LocalSettings>
                <Property Name="local.key">local.value</Property>
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
            </AnalysisConfig>
            """;
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var fullPath = TestUtils.CreateTextFile(testDir, "input.txt", xml);
        var actual = AnalysisConfig.Load(fullPath);
        var expected = new AnalysisConfig
        {
            SonarConfigDir = @"c:\config",
            SonarOutputDir = @"c:\output",
            SonarProjectKey = "key.1.2",
            SonarProjectVersion = "1.0",
            SonarProjectName = "My project",
            ServerSettings = [new("server.key", "server.value")],
            LocalSettings = [new("local.key", "local.value")],
            AnalyzersSettings = [
                new()
                {
                    RulesetPath = @"d:\ruleset path.ruleset",
                    AdditionalFilePaths = [@"c:\additional1.txt"],
                    AnalyzerPlugins = [
                        new("csharp", "7.10.0.7896", "SonarAnalyzer-7.10.0.7896.zip", [@"c:\assembly1.dll", @"C:\assembly2.dll"]),
                        new("pluginkey2", "1.2.3", "staticresource.zip", [@"C:\assembly3.dll"])
                    ]
                }
            ]
        };
        AssertAnalysisConfig(expected, actual);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("    ")]
    public void ReadAdditionalSetting_InvalidSettingId(string value) =>
        new AnalysisConfig().Invoking(x => x.ReadAdditionalSetting(value, "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    public void CreatePropertyProvider_WhenLoggerIsNull() =>
        new AnalysisConfig().Invoking(x => x.CreatePropertyProvider(false, null)).Should().Throw<ArgumentNullException>().WithParameterName("logger");

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("    ")]
    public void SetAdditionalSetting_InvalidSettingId(string value) =>
        new AnalysisConfig().Invoking(x => x.SetAdditionalSetting(value, "default")).Should().Throw<ArgumentNullException>().WithParameterName("settingId");

    [TestMethod]
    public void ReadAdditionalSetting_SetAdditionalSetting()
    {
        var config = new AnalysisConfig();
        config.ReadAdditionalSetting("missing", "DefaultValue").Should().Be("DefaultValue");
        // Add new
        config.SetAdditionalSetting("id1", "value1");
        config.ReadAdditionalSetting("id1", "XXX").Should().Be("value1");
        // Update
        config.SetAdditionalSetting("id1", "value2");
        config.ReadAdditionalSetting("id1", "XXX").Should().Be("value2");
        config.AdditionalConfig.Should().ContainSingle();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CreatePropertyProvider_AlwaysReturnLocalSettings(bool includeServerSettings)
    {
        var config = new AnalysisConfig
        {
            LocalSettings = [
                new("local.key.1", "local.value.1"),
                new("local.key.2", "local.value.2")
            ]
        };
        config.CreatePropertyProvider(includeServerSettings, new TestLogger()).GetAllProperties().Should().BeEquivalentTo([
            new Property("local.key.1", "local.value.1"),
            new Property("local.key.2", "local.value.2")]);
    }

    [TestMethod]
    public void CreatePropertyProvider_ServerOnly()
    {
        var logger = new TestLogger();
        var config = new AnalysisConfig
        {
            ServerSettings = [
                new("server.key.1", "server.value.1"),
                new("server.key.2", "server.value.2")
            ]
        };
        config.CreatePropertyProvider(false, logger).GetAllProperties().Should().BeEmpty();
        config.CreatePropertyProvider(true, logger).GetAllProperties().Should().BeEquivalentTo([
            new Property("server.key.1", "server.value.1"),
            new Property("server.key.2", "server.value.2")
        ]);
    }

    [TestMethod]
    public void CreatePropertyProvider_FileSettings()
    {
        // Check that file settings are always retrieved by CreatePropertyProvider and that the file name config property is set and retrieved correctly
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var config = new AnalysisConfig();
        var fileSettings = new AnalysisProperties
        {
            new("file.key.1", "file.value.1"),
            new("file.key.2", "file.value.2")
        };
        var settingsFilePath = Path.Combine(testDir, "settings.txt");
        fileSettings.Save(settingsFilePath);
        config.ReadSettingsFilePath().Should().BeNull("path was not set yet");
        config.SetSettingsFilePath(settingsFilePath);
        config.ReadSettingsFilePath().Should().Be(settingsFilePath);

        // Check file properties are retrieved
        config.CreatePropertyProvider(false, new TestLogger()).GetAllProperties().Should().BeEquivalentTo([
            new Property("file.key.1", "file.value.1"),
            new Property("file.key.2", "file.value.2")
        ]);
    }

    [TestMethod]
    public void CreatePropertyProvider_Precedence()
    {
        // Expected precedence: local -> file -> server
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var config = new AnalysisConfig
        {
            LocalSettings = [
                new("local.key.1", "local.value.1"),
                new("local.key.2", "local.value.2"),
                new("shared.with.local", "shared value from local")],
            ServerSettings = [
                new("server.key.1", "server.value.1"),
                new("server.key.2", "server.value.2"),
                new("shared.with.local", "this should never be returned"),
                new("shared.with.server", "this should never be returned")]
        };
        var fileSettings = new AnalysisProperties
        {
            new("file.key.1", "file.value.1"),
            new("file.key.2", "file.value.2"),
            new("shared.with.local", "shared value from file - should never be returned"),
            new("shared.with.server", "shared value from file")
        };
        var settingsFilePath = Path.Combine(testDir, "settings.txt");
        fileSettings.Save(settingsFilePath);
        config.SetSettingsFilePath(settingsFilePath);
        config.CreatePropertyProvider(true, new TestLogger()).GetAllProperties().Should().BeEquivalentTo([
            new Property("local.key.1", "local.value.1"),
            new Property("local.key.2", "local.value.2"),
            new Property("file.key.1", "file.value.1"),
            new Property("file.key.2", "file.value.2"),
            new Property("shared.with.local", "shared value from local"),
            new Property("shared.with.server", "shared value from file"),
            new Property("server.key.1", "server.value.1"),
            new Property("server.key.2", "server.value.2")]);
    }

    [TestMethod]
    public void CreatePropertyProvider_NoSettings()
    {
        var config = new AnalysisConfig();
        using var scope = new EnvironmentVariableScope().SetVariable("SONARQUBE_SCANNER_PARAMS", "Invalid Json to prevent provider SONARQUBE_SCANNER_PARAMS from being created");
        config.CreatePropertyProvider(true, new TestLogger()).GetAllProperties().Should().BeEmpty();
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
            ServerSettings = [new("id1", "server value")],
            LocalSettings = [new("id1", "local value")]
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

        AssertAnalysisConfig(original, reloaded);
    }

    private static void AssertAnalysisConfig(AnalysisConfig expected, AnalysisConfig actual) =>
        actual.Should().BeEquivalentTo(expected, x => x.Excluding(config => config.FileName));
}
