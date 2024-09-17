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
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class PreProcessorTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        var logger = Substitute.For<ILogger>();
        var factory = Substitute.For<IPreprocessorObjectFactory>();
        ((Func<PreProcessor>)(() => new PreProcessor(null, logger))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("factory");
        ((Func<PreProcessor>)(() => new PreProcessor(factory, null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void Execute_NullArguments_ThrowsArgumentNullException()
    {
        var factory = new MockObjectFactory();
        var preProcessor = new PreProcessor(factory, factory.Logger);

        preProcessor.Invoking(async x => await x.Execute(null)).Should().ThrowExactlyAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task Execute_InvalidArguments_ReturnsFalseAndLogsError()
    {
        var factory = new MockObjectFactory();
        var sut = new PreProcessor(factory, factory.Logger);

        (await sut.Execute(new[] { "invalid args" })).Should().Be(false);
        factory.Logger.AssertErrorLogged(
@"Expecting at least the following command line argument:
- SonarQube/SonarCloud project key
The full path to a settings file can also be supplied. If it is not supplied, the exe will attempt to locate a default settings file in the same directory as the SonarQube Scanner for MSBuild.
Use '/?' or '/h' to see the help message.");
    }

    [TestMethod]
    public async Task Execute_CannotCreateDirectories_ReturnsFalseAndLogsError()
    {
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        var preProcessor = new PreProcessor(factory, factory.Logger);
        var configDirectory = Path.Combine(scope.WorkingDir, "conf");
        Directory.CreateDirectory(configDirectory);
        using var lockedFile = new FileStream(Path.Combine(configDirectory, "LockedFile.txt"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);

        (await preProcessor.Execute(CreateArgs())).Should().BeFalse();
        factory.Logger.Errors.Should().ContainMatch($"Failed to create an empty directory '{configDirectory}'. Please check that there are no open or read-only files in the directory and that you have the necessary read/write permissions.*  Detailed error message: The process cannot access the file 'LockedFile.txt' because it is being used by another process.");
    }

    [TestMethod]
    public async Task Execute_InvalidLicense_ReturnsFalse()
    {
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        var preProcessor = new PreProcessor(factory, factory.Logger);
        factory.Server.IsServerLicenseValidImplementation = () => Task.FromResult(false);

        var result = await preProcessor.Execute(CreateArgs());

        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task Execute_LicenseCheckThrows_ReturnsFalseAndLogsError()
    {
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        var preProcessor = new PreProcessor(factory, factory.Logger);
        factory.Server.IsServerLicenseValidImplementation = () => throw new InvalidOperationException("Some error was thrown during license check.");

        (await preProcessor.Execute(CreateArgs())).Should().BeFalse();
        factory.Logger.AssertErrorLogged("Some error was thrown during license check.");
    }

    [TestMethod]
    public async Task Execute_TargetsNotInstalled_ReturnsFalseAndLogsDebugMessage()
    {
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        var preProcessor = new PreProcessor(factory, factory.Logger);

        (await preProcessor.Execute(CreateArgs().Append("/install:false"))).Should().BeTrue();
        factory.Logger.AssertDebugLogged("Skipping installing the ImportsBefore targets file.");
    }

    [TestMethod]
    public async Task Execute_FetchArgumentsAndRuleSets_ConnectionIssue_ReturnsFalseAndLogsError()
    {
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        var preProcessor = new PreProcessor(factory, factory.Logger);
        factory.Server.TryDownloadQualityProfilePreprocessing = () => throw new WebException("Could not connect to remote server", WebExceptionStatus.ConnectFailure);

        (await preProcessor.Execute(CreateArgs())).Should().BeFalse();
        factory.Logger.AssertErrorLogged("Could not connect to the SonarQube server. Check that the URL is correct and that the server is available. URL: http://host");
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Execute_ExplicitScanAllParameter_ReturnsTrue(bool scanAll)
    {
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        factory.Server.Data.SonarQubeVersion = new Version(9, 10, 1, 2);
        var preProcessor = new PreProcessor(factory, factory.Logger);
        var args = new List<string>(CreateArgs())
        {
            $"/d:sonar.scanner.scanAll={scanAll}",
        };

        var success = await preProcessor.Execute(args);
        success.Should().BeTrue("Expecting the pre-processing to complete successfully");

        if (scanAll)
        {
            factory.Logger.AssertUIWarningLogged("Multi-Language analysis is enabled. " +
                "If this was not intended and you have issues such as hitting your LOC limit or analyzing unwanted files, please set \"/d:sonar.scanner.scanAll=false\" in the begin step.");
        }
        else
        {
            factory.Logger.AssertNoWarningsLogged();
            factory.Logger.AssertNoUIWarningsLogged();
        }
    }

    [TestMethod]
    public async Task Execute_ServerNotAvailable_ReturnsFalse()
    {
        using var scope = new TestScope(TestContext);
        var factory = Substitute.For<IPreprocessorObjectFactory>();
        factory.CreateTargetInstaller().Returns(Substitute.For<ITargetsInstaller>());
        factory.CreateSonarWebServer(Arg.Any<ProcessedArgs>(), null).Returns(Task.FromResult<ISonarWebServer>(null));
        var preProcessor = new PreProcessor(factory, new TestLogger());

        var result = await preProcessor.Execute(CreateArgs());

        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task Execute_FetchArgumentsAndRuleSets_ServerReturnsUnexpectedStatus()
    {
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        var preProcessor = new PreProcessor(factory, factory.Logger);
        factory.Server.TryDownloadQualityProfilePreprocessing = () => throw new WebException("Something else went wrong");

        await preProcessor.Invoking(async x => await x.Execute(CreateArgs())).Should().ThrowAsync<WebException>().WithMessage("Something else went wrong");
    }

    [TestMethod]
    public async Task Execute_EndToEnd_SuccessCase()
    {
        // Checks end-to-end happy path for the pre-processor i.e.
        // * arguments are parsed
        // * targets are installed
        // * server properties are fetched
        // * rule sets are generated
        // * config file is created
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        factory.Server.Data.SonarQubeVersion = new Version(9, 10, 1, 2);
        var settings = factory.ReadSettings();
        var preProcessor = new PreProcessor(factory, factory.Logger);

        var success = await preProcessor.Execute(CreateArgs());
        success.Should().BeTrue("Expecting the pre-processing to complete successfully");

        AssertDirectoriesCreated(settings);

        factory.TargetsInstaller.Received(1).InstallLoaderTargets(scope.WorkingDir);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadProperties), 1);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadAllLanguages), 1);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadQualityProfile), 2); // C# and VBNet
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadRules), 2); // C# and VBNet

        factory.Logger.AssertInfoLogged("Cache data is empty. A full analysis will be performed.");
        factory.Logger.AssertDebugLogged("Processing analysis cache");

        var config = AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, factory.Logger);
        config.SonarQubeVersion.Should().Be("9.10.1.2");
        config.GetConfigValue(SonarProperties.PullRequestCacheBasePath, null).Should().Be(Path.GetDirectoryName(scope.WorkingDir));
    }

    [TestMethod]
    public async Task Execute_CreateCustomTempCachePath_SuccessCase()
    {
        // Checks end-to-end happy path for the pre-processor i.e.
        // * arguments are parsed
        // * targets are installed
        // * server properties are fetched
        // * rule sets are generated
        // * config file is created
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        factory.Server.Data.SonarQubeVersion = new Version(9, 10, 1, 2);
        var settings = factory.ReadSettings();
        var preProcessor = new PreProcessor(factory, factory.Logger);

        var tmpCachePath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).FullName, ".temp-cache");
        var args = new List<string>(CreateArgs())
        {
            $"/d:sonar.plugin.cache.directory={tmpCachePath}",
        };

        var success = await preProcessor.Execute(args);
        success.Should().BeTrue("Expecting the pre-processing to complete successfully");

        AssertDirectoriesCreated(settings);

        factory.AssertMethodCalled(nameof(MockObjectFactory.CreateRoslynAnalyzerProvider), 2); // C# and VBNet
        factory.PluginCachePath.Should().Be(tmpCachePath);

        factory.TargetsInstaller.Received(1).InstallLoaderTargets(scope.WorkingDir);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadProperties), 1);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadAllLanguages), 1);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadQualityProfile), 2); // C# and VBNet
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadRules), 2); // C# and VBNet

        factory.Logger.AssertInfoLogged("Cache data is empty. A full analysis will be performed.");
        factory.Logger.AssertDebugLogged("Processing analysis cache");

        var config = AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, factory.Logger);
        config.SonarQubeVersion.Should().Be("9.10.1.2");
        config.GetConfigValue(SonarProperties.PullRequestCacheBasePath, null).Should().Be(Path.GetDirectoryName(scope.WorkingDir));
    }

    [TestMethod]
    public async Task Execute_EndToEnd_SuccessCase_NoActiveRule()
    {
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        factory.Server.Data.FindProfile("qp1").Rules.Clear();
        var settings = factory.ReadSettings();
        var preProcessor = new PreProcessor(factory, factory.Logger);

        var success = await preProcessor.Execute(CreateArgs());
        success.Should().BeTrue("Expecting the pre-processing to complete successfully");

        AssertDirectoriesCreated(settings);

        factory.TargetsInstaller.Received(1).InstallLoaderTargets(scope.WorkingDir);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadProperties), 1);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadAllLanguages), 1);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadQualityProfile), 2); // C# and VBNet
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadRules), 2); // C# and VBNet

        AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, factory.Logger);
    }

    [TestMethod]
    public async Task Execute_EndToEnd_SuccessCase_With_Organization()
    {
        // Checks end-to-end happy path for the pre-processor i.e.
        // * arguments are parsed
        // * targets are installed
        // * server properties are fetched
        // * rule sets are generated
        // * config file is created
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory(organization: "organization");
        var settings = factory.ReadSettings();
        var preProcessor = new PreProcessor(factory, factory.Logger);

        var success = await preProcessor.Execute(CreateArgs("organization"));
        success.Should().BeTrue("Expecting the pre-processing to complete successfully");

        AssertDirectoriesCreated(settings);

        factory.TargetsInstaller.Received(1).InstallLoaderTargets(scope.WorkingDir);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadProperties), 1);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadAllLanguages), 1);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadQualityProfile), 2); // C# and VBNet
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadRules), 2); // C# and VBNet

        AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, factory.Logger);
    }

    [TestMethod]
    public async Task Execute_NoPlugin_ReturnsFalseAndLogsError()
    {
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        factory.Server.Data.Languages.Clear();
        factory.Server.Data.Languages.Add("invalid_plugin");
        var preProcessor = new PreProcessor(factory, factory.Logger);

        var success = await preProcessor.Execute(CreateArgs());
        success.Should().BeFalse("Expecting the pre-processing to fail");

        factory.Logger.AssertErrorLogged("Could not find any dotnet analyzer plugin on the server (SonarQube/SonarCloud)!");
    }

    [TestMethod]
    public async Task Execute_NoProject_ReturnsTrue()
    {
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory(false);
        factory.Server.Data
            .AddQualityProfile("qp1", "cs", null)
            .AddProject("invalid")
            .AddRule(new SonarRule("fxcop", "cs.rule1"))
            .AddRule(new SonarRule("fxcop", "cs.rule2"));
        factory.Server.Data
            .AddQualityProfile("qp2", "vbnet", null)
            .AddProject("invalid")
            .AddRule(new SonarRule("fxcop-vbnet", "vb.rule1"))
            .AddRule(new SonarRule("fxcop-vbnet", "vb.rule2"));
        var settings = factory.ReadSettings();
        var preProcessor = new PreProcessor(factory, factory.Logger);

        var success = await preProcessor.Execute(CreateArgs());
        success.Should().BeTrue("Expecting the pre-processing to complete successfully");

        AssertDirectoriesCreated(settings);

        factory.TargetsInstaller.Received(1).InstallLoaderTargets(scope.WorkingDir);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadProperties), 1);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadAllLanguages), 1);
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadQualityProfile), 2); // C# and VBNet
        factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadRules), 0); // no quality profile assigned to project

        AssertAnalysisConfig(settings.AnalysisConfigFilePath, 0, factory.Logger);

        // only contains SonarQubeAnalysisConfig (no rulesets or additional files)
        AssertDirectoryContains(settings.SonarConfigDirectory, Path.GetFileName(settings.AnalysisConfigFilePath));
    }

    [TestMethod]
    public async Task Execute_HandleAnalysisException_ReturnsFalse()
    {
        // Checks end-to-end behavior when AnalysisException is thrown inside FetchArgumentsAndRulesets
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        var exceptionWasThrown = false;
        factory.Server.TryDownloadQualityProfilePreprocessing = () =>
        {
            exceptionWasThrown = true;
            throw new AnalysisException("This message and stacktrace should not propagate to the users");
        };
        var preProcessor = new PreProcessor(factory, factory.Logger);
        var success = await preProcessor.Execute(CreateArgs("InvalidOrganization"));    // Should not throw
        success.Should().BeFalse("Expecting the pre-processing to fail");
        exceptionWasThrown.Should().BeTrue();
    }

    [TestMethod]
    // Regression test for https://github.com/SonarSource/sonar-scanner-msbuild/issues/699
    public async Task Execute_EndToEnd_Success_LocalSettingsAreUsedInSonarLintXML()
    {
        // Checks that local settings are used when creating the SonarLint.xml file, overriding
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        factory.JreResolver
            .ResolveJrePath(Arg.Any<ProcessedArgs>(), "homeSweetHome")
            .Returns("some/path/bin/java.exe");
        factory.Server.Data.ServerProperties.Add("shared.key1", "server shared value 1");
        factory.Server.Data.ServerProperties.Add("shared.CASING", "server upper case value");
        // Local settings that should override matching server settings
        var args = new List<string>(CreateArgs())
        {
            "/d:local.key=local value 1",
            "/d:shared.key1=local shared value 1 - should override server value",
            "/d:shared.casing=local lower case value",
            "/d:sonar.userHome=homeSweetHome"
        };
        var settings = factory.ReadSettings();
        var preProcessor = new PreProcessor(factory, factory.Logger);

        var success = await preProcessor.Execute(args);
        success.Should().BeTrue("Expecting the pre-processing to complete successfully");

        // Check the settings used when creating the SonarLint file - local and server settings should be merged
        factory.AnalyzerProvider.SuppliedSonarProperties.Should().NotBeNull();
        factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("server.key", "server value 1");
        factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("local.key", "local value 1");
        factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("shared.key1", "local shared value 1 - should override server value");
        // Keys are case-sensitive so differently cased values should be preserved
        factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("shared.CASING", "server upper case value");
        factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("shared.casing", "local lower case value");

        // Check the settings used when creating the config file - settings should be separate
        var actualConfig = AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, factory.Logger);
        actualConfig.JavaExePath.Should().Be("some/path/bin/java.exe");
        AssertExpectedLocalSetting(actualConfig, "local.key", "local value 1");
        AssertExpectedLocalSetting(actualConfig, "shared.key1", "local shared value 1 - should override server value");
        AssertExpectedLocalSetting(actualConfig, "shared.casing", "local lower case value");

        AssertExpectedServerSetting(actualConfig, "server.key", "server value 1");
        AssertExpectedServerSetting(actualConfig, "shared.key1", "server shared value 1");
        AssertExpectedServerSetting(actualConfig, "shared.CASING", "server upper case value");
    }

    private static IEnumerable<string> CreateArgs(string organization = null, Dictionary<string, string> properties = null)
    {
        yield return "/k:key";
        yield return "/n:name";
        yield return "/v:1.0";
        if (organization != null)
        {
            yield return $"/o:{organization}";
        }
        yield return "/d:cmd.line1=cmdline.value.1";
        yield return "/d:sonar.host.url=http://host";
        yield return "/d:sonar.log.level=INFO|DEBUG";

        if (properties != null)
        {
            foreach (var pair in properties)
            {
                yield return $"/d:{pair.Key}={pair.Value}";
            }
        }
    }

    private static void AssertDirectoriesCreated(IBuildSettings settings)
    {
        AssertDirectoryExists(settings.AnalysisBaseDirectory);
        AssertDirectoryExists(settings.SonarConfigDirectory);
        AssertDirectoryExists(settings.SonarOutputDirectory);
        // The bootstrapper is responsible for creating the bin directory
    }

    private AnalysisConfig AssertAnalysisConfig(string filePath, int noAnalyzers, TestLogger logger)
    {
        logger.AssertNoErrorsLogged();
        logger.AssertUIWarningLogged("Multi-Language analysis is enabled. If this was not intended and you have issues such as hitting your LOC limit or analyzing unwanted files, please set \"/d:sonar.scanner.scanAll=false\" in the begin step.");
        logger.AssertVerbosity(LoggerVerbosity.Debug);

        AssertConfigFileExists(filePath);
        var actualConfig = AnalysisConfig.Load(filePath);
        actualConfig.SonarProjectKey.Should().Be("key", "Unexpected project key");
        actualConfig.SonarProjectName.Should().Be("name", "Unexpected project name");
        actualConfig.SonarProjectVersion.Should().Be("1.0", "Unexpected project version");
        actualConfig.AnalyzersSettings.Should().NotBeNull("Analyzer settings should not be null");
        actualConfig.AnalyzersSettings.Should().HaveCount(noAnalyzers);

        AssertExpectedLocalSetting(actualConfig, SonarProperties.HostUrl, "http://host");
        AssertExpectedLocalSetting(actualConfig, "cmd.line1", "cmdline.value.1");
        AssertExpectedServerSetting(actualConfig, "server.key", "server value 1");

        return actualConfig;
    }

    private void AssertConfigFileExists(string filePath)
    {
        File.Exists(filePath).Should().BeTrue("Expecting the analysis config file to exist. Path: {0}", filePath);
        TestContext.AddResultFile(filePath);
    }

    private static void AssertDirectoryContains(string dirPath, params string[] fileNames)
    {
        Directory.Exists(dirPath);
        var actualFileNames = Directory.GetFiles(dirPath).Select(Path.GetFileName);
        actualFileNames.Should().BeEquivalentTo(fileNames);
    }

    private static void AssertExpectedLocalSetting(AnalysisConfig actualConfig, string key, string expectedValue)
    {
        var found = Property.TryGetProperty(key, actualConfig.LocalSettings, out var actualProperty);

        found.Should().BeTrue("Failed to find the expected local setting: {0}", key);
        actualProperty.Value.Should().Be(expectedValue, "Unexpected property value. Key: {0}", key);
    }

    private static void AssertExpectedServerSetting(AnalysisConfig actualConfig, string key, string expectedValue)
    {
        var found = Property.TryGetProperty(key, actualConfig.ServerSettings, out var actualProperty);

        found.Should().BeTrue("Failed to find the expected server setting: {0}", key);
        actualProperty.Value.Should().Be(expectedValue, "Unexpected property value. Key: {0}", key);
    }

    private static void AssertDirectoryExists(string path) =>
        Directory.Exists(path).Should().BeTrue("Expected directory does not exist: {0}", path);

    private sealed class TestScope : IDisposable
    {
        public readonly string WorkingDir;

        private readonly WorkingDirectoryScope workingDirectory;
        private readonly EnvironmentVariableScope environmentScope;

        public TestScope(TestContext context)
        {
            WorkingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(context);
            workingDirectory = new WorkingDirectoryScope(WorkingDir);
            environmentScope = PreprocessTestUtils.CreateValidNonTeamBuildScope();
        }

        public void Dispose()
        {
            workingDirectory.Dispose();
            environmentScope.Dispose();
        }
    }
}
