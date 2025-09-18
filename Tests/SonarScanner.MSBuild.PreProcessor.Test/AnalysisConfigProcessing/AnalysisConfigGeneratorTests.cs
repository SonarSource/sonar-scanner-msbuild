﻿/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

using SonarScanner.MSBuild.Common.TFS;
using Path = System.IO.Path;

namespace SonarScanner.MSBuild.PreProcessor.AnalysisConfigProcessing.Test;

[TestClass]
public class AnalysisConfigGeneratorTests
{
    private static readonly Dictionary<string, string> EmptyProperties = [];

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void GenerateFile_NullArguments_Throw()
    {
        var args = CreateProcessedArgs();
        var settings = BuildSettings.CreateSettingsForTesting(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext));
        var empty = new Dictionary<string, string>();
        var analyzer = new List<AnalyzerSettings>();
        FluentActions.Invoking(() => AnalysisConfigGenerator.GenerateFile(null, settings, empty, empty, analyzer, "1.0", null, null, null))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("localSettings");
        FluentActions.Invoking(() => AnalysisConfigGenerator.GenerateFile(args, null, empty, empty, analyzer, "1.10.42", null, null, null))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("buildSettings");
        FluentActions.Invoking(() => AnalysisConfigGenerator.GenerateFile(args, settings, null, empty, analyzer, "1.42", null, null, null))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("additionalSettings");
        FluentActions.Invoking(() => AnalysisConfigGenerator.GenerateFile(args, settings, empty, null, analyzer, "1.42", null, null, null))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("serverProperties");
        FluentActions.Invoking(() => AnalysisConfigGenerator.GenerateFile(args, settings, empty, empty, null, "1.22.42", null, null, null))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("analyzersSettings");
        FluentActions.Invoking(() => AnalysisConfigGenerator.GenerateFile(args, settings, empty, empty, analyzer, "1.22.42", null, null, null))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("runtime");
    }

    [TestMethod]
    public void AnalysisConfGen_Simple()
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var runtime = new TestRuntime();
        var propertyProvider = new ListPropertiesProvider();
        propertyProvider.AddProperty(SonarProperties.HostUrl, "http://foo");
        var args = CreateProcessedArgs(EmptyPropertyProvider.Instance, propertyProvider, runtime);
        var localSettings = BuildSettings.CreateSettingsForTesting(analysisDir);
        var serverSettings = new Dictionary<string, string> { { "server.key.1", "server.value.1" } };
        var analyzerSettings = new AnalyzerSettings
        {
            RulesetPath = "c:\\xxx.ruleset",
            AdditionalFilePaths = []
        };
        analyzerSettings.AdditionalFilePaths.Add("f:\\additionalPath1.txt");
        analyzerSettings.AnalyzerPlugins = [new() { AssemblyPaths = [@"f:\temp\analyzer1.dll"] }];
        var analyzersSettings = new List<AnalyzerSettings> { analyzerSettings };
        var additionalSettings = new Dictionary<string, string> { { "UnchangedFilesPath", @"f:\UnchangedFiles.txt" } };
        Directory.CreateDirectory(localSettings.SonarConfigDirectory); // config directory needs to exist

        var actualConfig = AnalysisConfigGenerator.GenerateFile(args, localSettings, additionalSettings, serverSettings, analyzersSettings, "9.9", null, null, runtime);

        AssertConfigFileExists(actualConfig);
        runtime.Logger.Should().HaveNoErrors()
            .And.HaveNoWarnings();

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
        actualConfig.ScanAllAnalysis.Should().BeTrue();
        actualConfig.UseSonarScannerCli.Should().BeFalse();

        var serverProperty = actualConfig.ServerSettings.SingleOrDefault(x => string.Equals(x.Id, "server.key.1", StringComparison.Ordinal));
        serverProperty.Should().NotBeNull();
        serverProperty.Value.Should().Be("server.value.1");
    }

    [TestMethod]
    public void AnalysisConfGen_FileProperties()
    {
        // File properties should not be copied to the file. Instead, a pointer to the file should be created.
        var runtime = new TestRuntime();
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var fileProperties = new AnalysisProperties
        {
            new(SonarProperties.HostUrl, "http://myserver"),
            new("file.only", "file value"),
            new(SonarProperties.ScanAllAnalysis, "false"),
            new(SonarProperties.UseSonarScannerCLI, "true"),
        };
        var settingsFilePath = Path.Combine(analysisDir, "settings.txt");
        fileProperties.Save(settingsFilePath);
        var fileProvider = FilePropertyProvider.Load(settingsFilePath);
        var args = CreateProcessedArgs(EmptyPropertyProvider.Instance, fileProvider, runtime);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

        var actualConfig = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "9.9", null, null, runtime);

        AssertConfigFileExists(actualConfig);
        runtime.Logger.Should().HaveNoErrors()
            .And.HaveNoWarnings();

        var actualSettingsFilePath = actualConfig.GetSettingsFilePath();
        actualSettingsFilePath.Should().Be(settingsFilePath);

        // Check the file setting value do not appear in the config file
        AssertFileDoesNotContainText(actualConfig.FileName, "file.only");

        actualConfig.SourcesDirectory.Should().Be(settings.SourcesDirectory);
        actualConfig.SonarScannerWorkingDirectory.Should().Be(settings.SonarScannerWorkingDirectory);
        actualConfig.ScanAllAnalysis.Should().BeFalse();
        actualConfig.UseSonarScannerCli.Should().BeTrue();
        AssertExpectedLocalSetting(SonarProperties.Organization, "organization", actualConfig);
    }

    [TestMethod]
    public void AnalysisConfGen_UseSonarScannerCLI_SetToTrueInLegacyTeamBuildContext()
    {
        var runtime = new TestRuntime();
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        // Set the build environment to TFS Legacy. This forces the AnalysisConfig.UseSonarScannerCli property to be set to true
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir, BuildEnvironment.LegacyTeamBuild);
        // Explicit set sonar.scanner.useSonarScannerCLI argument to false. This get's ignored/overriden in TFS Legacy context
        var args = CreateProcessedArgs(new ListPropertiesProvider { { SonarProperties.UseSonarScannerCLI, "false" } }, EmptyPropertyProvider.Instance, runtime, settings);

        var actualConfig = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "9.9", null, null, runtime);

        AssertConfigFileExists(actualConfig);
        runtime.Logger.Should()
            .HaveNoErrors()
            .And.HaveNoWarnings();
#if NETFRAMEWORK
        runtime.Logger.Should().HaveDebugs("Falling back to SonarScannerCLI to guarantee TFS Legacy support.");
        actualConfig.UseSonarScannerCli.Should().BeTrue();
#else
        runtime.Logger.Should().NotHaveDebug("Falling back to SonarScannerCLI to guarantee TFS Legacy support.");
        actualConfig.UseSonarScannerCli.Should().BeFalse();
#endif
    }

    [TestMethod]
    [WorkItem(127)] // Do not store the db and server credentials in the config files: http://jira.sonarsource.com/browse/SONARMSBRU-127
    public void AnalysisConfGen_AnalysisConfigDoesNotContainSensitiveData()
    {
        var runtime = new TestRuntime();
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
        var args = CreateProcessedArgs(cmdLineArgs, fileProvider, runtime);
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
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], serverProperties, [], "9.9", null, null, runtime);

        AssertConfigFileExists(config);
        runtime.Logger.Should().HaveNoErrors()
            .And.HaveNoWarnings();

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
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.SonarUserName, "foo");
        var runtime = new TestRuntime();
        var args = CreateProcessedArgs(cmdLineArgs, EmptyPropertyProvider.Instance, runtime);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "9.9", null, null, runtime);

        AssertConfigFileExists(config);
        config.HasBeginStepCommandLineCredentials.Should().BeTrue();
    }

    [TestMethod]
    public void AnalysisConfGen_WhenTokenIsSpecified_SetsHasBeginStepCommandLineCredentialsToTrue()
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.SonarToken, "token");
        var runtime = new TestRuntime();
        var args = CreateProcessedArgs(cmdLineArgs, EmptyPropertyProvider.Instance, runtime);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "9.9", null, null, runtime);

        AssertConfigFileExists(config);
        config.HasBeginStepCommandLineCredentials.Should().BeTrue();
    }

    [TestMethod]
    public void AnalysisConfGen_WhenLoginNotSpecified_DoesNotStoreThatItWasSpecified()
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        var args = CreateProcessedArgs();
        Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "9.9", null, null, new TestRuntime());

        AssertConfigFileExists(config);
        config.HasBeginStepCommandLineCredentials.Should().BeFalse();
    }

    [TestMethod]
    public void GenerateFile_WritesSonarQubeVersion()
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        var args = CreateProcessedArgs();
        Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "1.2.3.4", null, null, new TestRuntime());

        config.SonarQubeVersion.Should().Be("1.2.3.4");
    }

    [TestMethod]
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
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var commandLineArguments = new ListPropertiesProvider([new Property(SonarProperties.JavaExePath, setByUser)]);
        var runtime = new TestRuntime();
        var args = CreateProcessedArgs(commandLineArguments, EmptyPropertyProvider.Instance, runtime);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "1.2.3.4", resolved, null, runtime);

        config.JavaExePath.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("userEngine.jar", "", "userEngine.jar")]
    [DataRow("userEngine.jar", " ", "userEngine.jar")]
    [DataRow("userEngine.jar", null, "userEngine.jar")]
    [DataRow("", "resolvedEngine.jar", "resolvedEngine.jar")]
    [DataRow("  ", "resolvedEngine.jar", "resolvedEngine.jar")]
    [DataRow(null, "resolvedEngine.jar", "resolvedEngine.jar")]
    [DataRow("userEngine.jar", "resolvedEngine.jar", "userEngine.jar")]
    public void GenerateFile_ScannerEngine(string setByUser, string resolved, string expected)
    {
        var settings = BuildSettings.CreateSettingsForTesting(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext));
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var runtime = new TestRuntime();
        var args = CreateProcessedArgs(new ListPropertiesProvider([new Property(SonarProperties.EngineJarPath, setByUser)]), EmptyPropertyProvider.Instance, runtime);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "1.2.3.4", null, resolved, runtime);

        config.EngineJarPath.Should().Be(expected);
    }

    [TestMethod]
    public void GenerateFile_ExcludeCoverage_ScanAllDisabled_Ignored()
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var commandLineArguments = new ListPropertiesProvider([
            new Property("sonar.cs.vscoveragexml.reportsPaths", "coverage1.xml"),
            new Property("sonar.cs.dotcover.reportsPaths", "coverage2.xml"),
            new Property("sonar.cs.opencover.reportsPaths", "coverage3.xml"),
            new Property("sonar.exclusions", "foo.js"),
            new Property(SonarProperties.ScanAllAnalysis, "false"),
            ]);
        var runtime = new TestRuntime();
        var args = CreateProcessedArgs(commandLineArguments, EmptyPropertyProvider.Instance, runtime);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "1.2.3.4", string.Empty, null, runtime);

        config.LocalSettings
            .Should().ContainSingle(x => x.Id == "sonar.exclusions")
            .Which.Value.Should().Be("foo.js");
    }

    [TestMethod]
    public void GenerateFile_ExcludeCoverage_NotSpecified_ExclusionsUnchanged()
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var commandLineArguments = new ListPropertiesProvider([new Property("sonar.exclusions", "foo.js")]);
        var runtime = new TestRuntime();
        var args = CreateProcessedArgs(commandLineArguments, EmptyPropertyProvider.Instance, runtime);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "1.2.3.4", string.Empty, null, runtime);

        config.LocalSettings
            .Should().ContainSingle(x => x.Id == "sonar.exclusions")
            .Which.Value.Should().Be("foo.js");
    }

    [TestMethod]
    [DataRow("sonar.cs.vscoveragexml.reportsPaths", "foo.js,coverage.xml")]
    [DataRow("sonar.cs.opencover.reportsPaths", "foo.js,coverage.xml")]
    [DataRow("sonar.cs.dotcover.reportsPaths", "foo.js,coverage.xml,coverage/**")]
    public void GenerateFile_ExcludeCoverage_Exclusions_Exist_Cases(string propertyName, string expectedExclusions)
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var commandLineArguments = new ListPropertiesProvider([
            new Property("sonar.exclusions", "foo.js"),
            new Property(propertyName, "coverage.xml")
            ]);
        var runtime = new TestRuntime();
        var args = CreateProcessedArgs(commandLineArguments, EmptyPropertyProvider.Instance, runtime);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "1.2.3.4", string.Empty, null, runtime);

        config.LocalSettings
            .Should().ContainSingle(x => x.Id == "sonar.exclusions")
            .Which.Value.Should().Be(expectedExclusions);
    }

    [TestMethod]
    [DataRow("sonar.cs.vscoveragexml.reportsPaths", "coverage.xml")]
    [DataRow("sonar.cs.opencover.reportsPaths", "coverage.xml")]
    [DataRow("sonar.cs.dotcover.reportsPaths", "coverage.xml,coverage/**")]
    public void GenerateFile_ExcludeCoverage_Exclusions_DoesNotExist_Cases(string propertyName, string expectedExclusions)
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var commandLineArguments = new ListPropertiesProvider([new Property(propertyName, "coverage.xml")]);
        var runtime = new TestRuntime();
        var args = CreateProcessedArgs(commandLineArguments, EmptyPropertyProvider.Instance, runtime);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "1.2.3.4", string.Empty, null, runtime);

        config.LocalSettings
            .Should().ContainSingle(x => x.Id == "sonar.exclusions")
            .Which.Value.Should().Be(expectedExclusions);
    }

    [TestMethod]
    public void GenerateFile_ExcludeCoverage_Exclusions_MultipleSpecified()
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var commandLineArguments = new ListPropertiesProvider([
            new Property("sonar.cs.vscoveragexml.reportsPaths", "coverage1.xml"),
            new Property("sonar.cs.opencover.reportsPaths", "coverage2.xml,coverage3.xml"),
            new Property("sonar.cs.dotcover.reportsPaths", "coverage4.xml"),
            ]);
        var runtime = new TestRuntime();
        var args = CreateProcessedArgs(commandLineArguments, EmptyPropertyProvider.Instance, runtime);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "1.2.3.4", string.Empty, null, runtime);

        config.LocalSettings
            .Should().ContainSingle(x => x.Id == "sonar.exclusions")
            .Which.Value.Should().Be("coverage1.xml,coverage2.xml,coverage3.xml,coverage4.xml,coverage4/**");
    }

    [TestMethod]
    [DataRow("coverage.xml", "", "", "", "coverage.xml", "")]
    [DataRow("coverage.xml", "", "local.cs,local.js", "", "local.cs,local.js,coverage.xml", "")]
    [DataRow("coverage.xml", "", "", "server.cs,server.js", "server.cs,server.js,coverage.xml", "server.cs,server.js")]
    [DataRow("coverage.xml", "", "local.cs,local.js", "server.cs,server.js", "local.cs,local.js,coverage.xml", "server.cs,server.js")]
    [DataRow("", "", "", "", "", "")]
    [DataRow("", "", "local.cs,local.js", "", "local.cs,local.js", "")]
    [DataRow("", "", "", "server.cs,server.js", "", "server.cs,server.js")]
    [DataRow("", "", "local.cs,local.js", "server.cs,server.js", "local.cs,local.js", "server.cs,server.js")]
    [DataRow("", "coverage.xml", "", "", "coverage.xml", "")]
    [DataRow("", "coverage.xml", "local.cs,local.js", "", "local.cs,local.js,coverage.xml", "")]
    [DataRow("", "coverage.xml", "", "server.cs,server.js", "server.cs,server.js,coverage.xml", "server.cs,server.js")]
    [DataRow("", "coverage.xml", "local.cs,local.js", "server.cs,server.js", "local.cs,local.js,coverage.xml", "server.cs,server.js")]
    [DataRow("localCoverage.xml", "serverCoverage.xml", "", "", "localCoverage.xml", "")]
    [DataRow("localCoverage.xml", "serverCoverage.xml", "local.cs,local.js", "", "local.cs,local.js,localCoverage.xml", "")]
    [DataRow("localCoverage.xml", "serverCoverage.xml", "", "server.cs,server.js", "server.cs,server.js,localCoverage.xml", "server.cs,server.js")]
    [DataRow("localCoverage.xml", "serverCoverage.xml", "local.cs,local.js", "server.cs,server.js", "local.cs,local.js,localCoverage.xml", "server.cs,server.js")]
    public void GenerateFile_ExcludeCoverage_VerifyCoverageAndExclusionCombinations(
        string localCoverageReportPath,
        string serverCoverageReportPath,
        string localExclusions,
        string serverExclusions,
        string expectedLocalExclusions,
        string expectedServerExclusions)
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var commandLineArguments = new ListPropertiesProvider();
        AddIfNotEmpty(commandLineArguments, "sonar.exclusions", localExclusions);
        AddIfNotEmpty(commandLineArguments, "sonar.cs.vscoveragexml.reportsPaths", localCoverageReportPath);
        var serverSettings = new Dictionary<string, string>();
        AddIfNotEmpty(serverSettings, "sonar.exclusions", serverExclusions);
        AddIfNotEmpty(serverSettings, "sonar.cs.vscoveragexml.reportsPaths", serverCoverageReportPath);
        var runtime = new TestRuntime();
        var args = CreateProcessedArgs(commandLineArguments, EmptyPropertyProvider.Instance, runtime);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], serverSettings, [], "1.2.3.4", string.Empty, null, runtime);

        if (string.IsNullOrWhiteSpace(expectedLocalExclusions))
        {
            config.LocalSettings.Should().NotContain(x => x.Id == "sonar.exclusions");
        }
        else
        {
            config.LocalSettings.Should().ContainSingle(x => x.Id == "sonar.exclusions")
                .Which.Value.Should().Be(expectedLocalExclusions);
        }

        if (string.IsNullOrWhiteSpace(expectedServerExclusions))
        {
            config.ServerSettings.Should().NotContain(x => x.Id == "sonar.exclusions");
        }
        else
        {
            config.ServerSettings.Should().ContainSingle(x => x.Id == "sonar.exclusions")
                .Which.Value.Should().Be(expectedServerExclusions);
        }
    }

    [TestMethod]
    [DataRow("", "", "", "", "", "", "")]
    [DataRow("", "", "", "", "e", "f", "e,f,f/**")]
    [DataRow("a", "b", "c", "", "e", "", "a,b,c,c/**")]
    [DataRow("a", "", "c", "d", "e", "f", "a,e,c,c/**")]
    [DataRow("a", "", "", "", "", "f", "a,f,f/**")]
    [DataRow("a", "b", "c", "", "", "", "a,b,c,c/**")]
    [DataRow("a", "b", "c", "d", "e", "f", "a,b,c,c/**")]
    public void GenerateFile_ExcludeCoverage_VerifyCoverageCombinations(string vsCoverageLocal,
                                                                        string openCoverLocal,
                                                                        string dotCoverLocal,
                                                                        string vsCoverageServer,
                                                                        string openCoverServer,
                                                                        string dotCoverServer,
                                                                        string expectedExclusions)
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var commandLineArguments = new ListPropertiesProvider();
        AddIfNotEmpty(commandLineArguments, "sonar.cs.vscoveragexml.reportsPaths", vsCoverageLocal);
        AddIfNotEmpty(commandLineArguments, "sonar.cs.opencover.reportsPaths", openCoverLocal);
        AddIfNotEmpty(commandLineArguments, "sonar.cs.dotcover.reportsPaths", dotCoverLocal);
        var serverSettings = new Dictionary<string, string>();
        AddIfNotEmpty(serverSettings, "sonar.cs.vscoveragexml.reportsPaths", vsCoverageServer);
        AddIfNotEmpty(serverSettings, "sonar.cs.opencover.reportsPaths", openCoverServer);
        AddIfNotEmpty(serverSettings, "sonar.cs.dotcover.reportsPaths", dotCoverServer);
        var runtime = new TestRuntime();
        var args = CreateProcessedArgs(commandLineArguments, EmptyPropertyProvider.Instance, runtime);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], serverSettings, [], "1.2.3.4", string.Empty, null, runtime);

        if (string.IsNullOrWhiteSpace(expectedExclusions))
        {
            config.LocalSettings.Should().NotContain(x => x.Id == "sonar.exclusions");
        }
        else
        {
            config.LocalSettings.Should().ContainSingle(x => x.Id == "sonar.exclusions")
                .Which.Value.Should().Be(expectedExclusions);
        }
    }

    [TestMethod]
    [DataRow("", "")]
    [DataRow("foo", "foo,foo/**")]
    [DataRow("foo.html", "foo.html,foo/**")]
    [DataRow("foo,bar", "foo,foo/**,bar,bar/**")]
    [DataRow("foo.bar.html", "foo.bar.html,foo.bar/**")]
    [DataRow("foo.bar.baz.html", "foo.bar.baz.html,foo.bar.baz/**")]
    [DataRow(".html", ".html")]
    [DataRow(".", ".")]
    [DataRow("foo.", "foo.,foo/**")]
    [DataRow("foo..", "foo..,foo./**")]
    public void GenerateFile_ExcludeCoverage_VerifyDotCoverDirectories(string dotCoverPaths, string expectedExclusions)
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var commandLineArguments = new ListPropertiesProvider();
        AddIfNotEmpty(commandLineArguments, "sonar.cs.dotcover.reportsPaths", dotCoverPaths);
        var runtime = new TestRuntime();
        var args = CreateProcessedArgs(commandLineArguments, EmptyPropertyProvider.Instance, runtime);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], new Dictionary<string, string>(), [], "1.2.3.4", string.Empty, null, runtime);

        if (string.IsNullOrWhiteSpace(expectedExclusions))
        {
            config.LocalSettings.Should().NotContain(x => x.Id == "sonar.exclusions");
        }
        else
        {
            config.LocalSettings.Should().ContainSingle(x => x.Id == "sonar.exclusions")
                .Which.Value.Should().Be(expectedExclusions);
        }
    }

    [TestMethod]
    [DataRow("foo/", "")]
    [DataRow("", "bar/")]
    [DataRow("foo/", "bar/")]
    public void GenerateFile_SourcesTestsIgnored(string sources, string tests)
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        Directory.CreateDirectory(settings.SonarConfigDirectory);
        var commandLineArguments = new ListPropertiesProvider();
        AddIfNotEmpty(commandLineArguments, "sonar.sources", sources);
        AddIfNotEmpty(commandLineArguments, "sonar.tests", tests);
        var runtime = new TestRuntime();
        var args = CreateProcessedArgs(commandLineArguments, EmptyPropertyProvider.Instance, runtime);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], new Dictionary<string, string>(), [], "1.2.3.4", string.Empty, null, runtime);

        config.LocalSettings.Should().NotContain(x => x.Id == "sonar.sources");
        config.LocalSettings.Should().NotContain(x => x.Id == "sonar.tests");
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void GenerateFile_TrustStoreProperties_Mapped()
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        var propertiesProvider = new ListPropertiesProvider();
        AddIfNotEmpty(propertiesProvider, SonarProperties.HostUrl, "https://localhost:9000");
        AddIfNotEmpty(propertiesProvider, "sonar.scanner.truststorePath", "\"C:\\path\\to\\truststore.pfx\"");
        AddIfNotEmpty(propertiesProvider, "sonar.scanner.truststorePassword", "password");
        var args = CreateProcessedArgs(propertiesProvider);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "9.9", null, null, new TestRuntime());

        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", "\"C:/path/to/truststore.pfx\"", config);
        Property.TryGetProperty("javax.net.ssl.trustStore", config.LocalSettings, out _).Should().BeFalse();
        config.HasBeginStepCommandLineTruststorePassword.Should().BeTrue();
        Property.TryGetProperty("javax.net.ssl.trustStorePassword", config.LocalSettings, out _).Should().BeFalse();
        Property.TryGetProperty("javax.net.ssl.trustStorePassword", config.ScannerOptsSettings, out _).Should().BeFalse();
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void GenerateFile_TrustStorePropertiesNullValue_Unmapped()
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        var propertiesProvider = new ListPropertiesProvider();
        propertiesProvider.AddProperty("sonar.scanner.truststorePath", null);
        propertiesProvider.AddProperty("sonar.scanner.truststorePassword", null);
        var args = CreateProcessedArgs(propertiesProvider);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "9.9", null, null, new TestRuntime());
        config.ScannerOptsSettings.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Id = "javax.net.ssl.trustStoreType", Value = "Windows-ROOT" });

        Property.TryGetProperty("javax.net.ssl.trustStore", config.LocalSettings, out _).Should().BeFalse();
        Property.TryGetProperty("javax.net.ssl.trustStorePassword", config.LocalSettings, out _).Should().BeFalse();
        Property.TryGetProperty("javax.net.ssl.trustStorePassword", config.ScannerOptsSettings, out _).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(SonarProperties.Verbose, "true")]
    [DataRow(SonarProperties.Organization, "org")]
    [DataRow(SonarProperties.HostUrl, "http://localhost:9000")]
    [DataRow(SonarProperties.HostUrl, @"http://localhost:9000\")]
    [DataRow(SonarProperties.Region, @"us")]
    public void GenerateFile_UnmappedProperties(string id, string value)
    {
        var analysisDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settings = BuildSettings.CreateSettingsForTesting(analysisDir);
        var propertiesProvider = new ListPropertiesProvider([new Property(id, value)]);
        var args = CreateProcessedArgs(propertiesProvider);

        var config = AnalysisConfigGenerator.GenerateFile(args, settings, [], EmptyProperties, [], "9.9", null, null, new TestRuntime());

        AssertExpectedLocalSetting(id, value, config);
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

    private static void AssertExpectedScannerOptsSettings(string key, string expectedValue, AnalysisConfig actualConfig)
    {
        var found = Property.TryGetProperty(key, actualConfig.ScannerOptsSettings, out var property);
        found.Should().BeTrue("Expected local property was not found. Key: {0}", key);
        property.Value.Should().Be(expectedValue, "Unexpected local value. Key: {0}", key);
    }

    private static void AssertFileDoesNotContainText(string filePath, string text)
    {
        File.Exists(filePath).Should().BeTrue("File should exist: {0}", filePath);
        var content = File.ReadAllText(filePath);
        content.IndexOf(text, StringComparison.InvariantCultureIgnoreCase).Should().BeNegative($"Not expecting text to be found in the file. Text: '{text}', file: {filePath}");
    }

    private static void AddIfNotEmpty(Dictionary<string, string> settings, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            settings.Add(key, value);
        }
    }

    private static void AddIfNotEmpty(ListPropertiesProvider settings, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            settings.AddProperty(key, value);
        }
    }

    private static ProcessedArgs CreateProcessedArgs() =>
        CreateProcessedArgs(EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance, new TestRuntime(), null);

    private static ProcessedArgs CreateProcessedArgs(IAnalysisPropertyProvider cmdLineProperties) =>
        CreateProcessedArgs(cmdLineProperties, EmptyPropertyProvider.Instance, new TestRuntime(), null);

    private static ProcessedArgs CreateProcessedArgs(
        IAnalysisPropertyProvider cmdLineProperties,
        IAnalysisPropertyProvider globalFileProperties,
        IRuntime runtime,
        BuildSettings buildSettings = null) =>
        new(
            "valid.key",
            "valid.name",
            "1.0",
            "organization",
            false,
            cmdLineProperties,
            globalFileProperties,
            EmptyPropertyProvider.Instance,
            buildSettings,
            runtime);
}
