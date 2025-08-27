/*
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

using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public partial class PreProcessorTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        ((Func<PreProcessor>)(() => new PreProcessor(null, Substitute.For<ILogger>()))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("factory");
        ((Func<PreProcessor>)(() => new PreProcessor(Substitute.For<IPreprocessorObjectFactory>(), null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
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
        var preProcessor = new PreProcessor(factory, factory.Logger);

        (await preProcessor.Execute(["invalid args"])).Should().Be(false);
        factory.Logger.AssertErrorLogged("""
            Expecting at least the following command line argument:
            - SonarQube/SonarCloud project key
            The full path to a settings file can also be supplied. If it is not supplied, the exe will attempt to locate a default settings file in the same directory as the SonarQube Scanner for .NET.
            Use '/?' or '/h' to see the help message.
            """);
    }

    // Windows enforces file locks at the OS level.
    // Unix does not enforce file locks at the OS level, so this doesn't work.
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public async Task Execute_CannotCreateDirectories_ReturnsFalseAndLogsError()
    {
        using var scope = new TestScope(TestContext);
        var configDirectory = Path.Combine(scope.WorkingDir, "conf");
        Directory.CreateDirectory(configDirectory);
        using var lockedFile = new FileStream(Path.Combine(configDirectory, "LockedFile.txt"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);

        (await scope.Execute()).Should().BeFalse();
        scope.Factory.Logger.Errors.Should().ContainMatch($"Failed to create an empty directory '{configDirectory}'. Please check that there are no open or read-only files in the directory and that you have the necessary read/write permissions.*  Detailed error message: The process cannot access the file 'LockedFile.txt' because it is being used by another process.");
    }

    [TestMethod]
    public async Task Execute_InvalidLicense_ReturnsFalse()
    {
        using var scope = new TestScope(TestContext);
        scope.Factory.Server.IsServerLicenseValidImplementation = () => Task.FromResult(false);

        var result = await scope.Execute();

        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task Execute_LicenseCheckThrows_ReturnsFalseAndLogsError()
    {
        using var scope = new TestScope(TestContext);
        scope.Factory.Server.IsServerLicenseValidImplementation = () => throw new InvalidOperationException("Some error was thrown during license check.");

        (await scope.Execute()).Should().BeFalse();
        scope.Factory.Logger.AssertErrorLogged("Some error was thrown during license check.");
    }

    [TestMethod]
    public async Task Execute_TargetsNotInstalled_ReturnsFalseAndLogsDebugMessage()
    {
        using var scope = new TestScope(TestContext);
        scope.Factory.Server.IsServerLicenseValidImplementation = () => throw new InvalidOperationException("Some error was thrown during license check.");

        (await scope.Execute()).Should().BeFalse();
        scope.Factory.Logger.AssertErrorLogged("Some error was thrown during license check.");
    }

    [TestMethod]
    public async Task Execute_FetchArgumentsAndRuleSets_ConnectionIssue_ReturnsFalseAndLogsError()
    {
        using var scope = new TestScope(TestContext);
        scope.Factory.Server.TryDownloadQualityProfilePreprocessing = () => throw new WebException("Could not connect to remote server", WebExceptionStatus.ConnectFailure);

        (await scope.Execute()).Should().BeFalse();
        scope.Factory.Logger.AssertErrorLogged("Could not connect to the SonarQube server. Check that the URL is correct and that the server is available. URL: http://host");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Execute_ExplicitScanAllParameter_ReturnsTrue(bool scanAll)
    {
        using var scope = new TestScope(TestContext);
        scope.Factory.Server.Data.SonarQubeVersion = new Version(9, 10, 1, 2);
        var args = new List<string>(CreateArgs())
        {
            $"/d:sonar.scanner.scanAll={scanAll}",
        };

        var success = await scope.Execute(args);

        success.Should().BeTrue("Expecting the pre-processing to complete successfully");
        scope.Factory.Logger.AssertNoWarningsLogged();
        scope.Factory.Logger.AssertNoUIWarningsLogged();
    }

    [TestMethod]
    public async Task Execute_ServerNotAvailable_ReturnsFalse()
    {
        using var scope = new TestScope(TestContext);
        // Factory needs to be mocked, so cannot use TestScopes built-in factory/pre-processor, but still want to use working directory scope
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
        scope.Factory.Server.TryDownloadQualityProfilePreprocessing = () => throw new WebException("Something else went wrong");

        await scope.PreProcessor.Invoking(async x => await x.Execute(CreateArgs())).Should().ThrowAsync<WebException>().WithMessage("Something else went wrong");
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
        scope.Factory.Server.Data.SonarQubeVersion = new Version(9, 10, 1, 2);

        var success = await scope.Execute();
        success.Should().BeTrue("Expecting the pre-processing to complete successfully");

        scope.AssertDirectoriesCreated();
        scope.AssertCorrectMethodsCalled(1, 1, 2, 2);

        scope.Factory.Logger.AssertInfoLogged("Cache data is empty. A full analysis will be performed.");
        scope.Factory.Logger.AssertDebugLogged("Processing analysis cache");

        var config = scope.AssertAnalysisConfig(2);
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
        scope.Factory.Server.Data.SonarQubeVersion = new Version(9, 10, 1, 2);

        var tmpCachePath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).FullName, ".temp-cache");
        var args = new List<string>(CreateArgs())
        {
            $"/d:sonar.plugin.cache.directory={tmpCachePath}",
        };

        var success = await scope.Execute(args);
        success.Should().BeTrue("Expecting the pre-processing to complete successfully");

        scope.AssertDirectoriesCreated();

        scope.Factory.AssertMethodCalled(nameof(scope.Factory.CreateRoslynAnalyzerProvider), 2); // C# and VBNet
        scope.Factory.PluginCachePath.Should().Be(tmpCachePath);

        scope.AssertCorrectMethodsCalled(1, 1, 2, 2);

        scope.Factory.Logger.AssertInfoLogged("Cache data is empty. A full analysis will be performed.");
        scope.Factory.Logger.AssertDebugLogged("Processing analysis cache");

        var config = scope.AssertAnalysisConfig(2);
        config.SonarQubeVersion.Should().Be("9.10.1.2");
        config.GetConfigValue(SonarProperties.PullRequestCacheBasePath, null).Should().Be(Path.GetDirectoryName(scope.WorkingDir));
    }

    [TestMethod]
    public async Task Execute_EndToEnd_SuccessCase_NoActiveRule()
    {
        using var scope = new TestScope(TestContext);
        scope.Factory.Server.Data.FindProfile("qp1").Rules.Clear();

        var success = await scope.Execute();
        success.Should().BeTrue("Expecting the pre-processing to complete successfully");

        scope.AssertDirectoriesCreated();
        scope.AssertCorrectMethodsCalled(1, 1, 2, 2);
        scope.AssertAnalysisConfig(2);
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
        using var scope = new TestScope(TestContext, new MockObjectFactory(organization: "organization"));

        var success = await scope.Execute(CreateArgs("organization"));
        success.Should().BeTrue("Expecting the pre-processing to complete successfully");

        scope.AssertDirectoriesCreated();
        scope.AssertCorrectMethodsCalled(1, 1, 2, 2);
        scope.AssertAnalysisConfig(2);
    }

    [TestMethod]
    public async Task Execute_NoPlugin_ReturnsFalseAndLogsError()
    {
        using var scope = new TestScope(TestContext);
        scope.Factory.Server.Data.Languages.Clear();
        scope.Factory.Server.Data.Languages.Add("invalid_plugin");

        var success = await scope.Execute();

        success.Should().BeFalse("Expecting the pre-processing to fail");
        scope.Factory.Logger.AssertErrorLogged("Could not find any dotnet analyzer plugin on the server (SonarQube/SonarCloud)!");
    }

    [TestMethod]
    public async Task Execute_NoProject_ReturnsTrue()
    {
        using var scope = new TestScope(TestContext, new MockObjectFactory(false));
        scope.Factory.Server.Data
            .AddQualityProfile("qp1", "cs", null)
            .AddProject("invalid")
            .AddRule(new SonarRule("fxcop", "cs.rule1"))
            .AddRule(new SonarRule("fxcop", "cs.rule2"));
        scope.Factory.Server.Data
            .AddQualityProfile("qp2", "vbnet", null)
            .AddProject("invalid")
            .AddRule(new SonarRule("fxcop-vbnet", "vb.rule1"))
            .AddRule(new SonarRule("fxcop-vbnet", "vb.rule2"));

        var success = await scope.Execute();

        success.Should().BeTrue("Expecting the pre-processing to complete successfully");
        scope.AssertDirectoriesCreated();
        scope.AssertCorrectMethodsCalled(1, 1, 2, 0); // no quality profile assigned to project
        scope.AssertAnalysisConfig(0);
        // only contains SonarQubeAnalysisConfig (no rulesets or additional files)
        scope.AssertAnalysisConfigPathInSonarConfigDirectory();
    }

    [TestMethod]
    public async Task Execute_HandleAnalysisException_ReturnsFalse()
    {
        // Checks end-to-end behavior when AnalysisException is thrown inside FetchArgumentsAndRulesets
        using var scope = new TestScope(TestContext);
        var exceptionWasThrown = false;
        scope.Factory.Server.TryDownloadQualityProfilePreprocessing = () =>
        {
            exceptionWasThrown = true;
            throw new AnalysisException("This message and stacktrace should not propagate to the users");
        };

        var success = await scope.Execute(CreateArgs("InvalidOrganization"));    // Should not throw

        success.Should().BeFalse("Expecting the pre-processing to fail");
        exceptionWasThrown.Should().BeTrue();
    }

    [TestMethod]
    // Regression test for https://github.com/SonarSource/sonar-scanner-msbuild/issues/699
    public async Task Execute_EndToEnd_Success_LocalSettingsAreUsedInSonarLintXML()
    {
        // Checks that local settings are used when creating the SonarLint.xml file, overriding
        using var scope = new TestScope(TestContext);
        scope.Factory.JreResolver
            .ResolvePath(Arg.Any<ProcessedArgs>())
            .Returns("some/path/bin/java.exe");
        scope.Factory.EngineResolver
            .ResolvePath(Arg.Any<ProcessedArgs>())
            .Returns("some/path/to/engine.jar");

        scope.Factory.Server.Data.ServerProperties.Add("shared.key1", "server shared value 1");
        scope.Factory.Server.Data.ServerProperties.Add("shared.CASING", "server upper case value");
        // Local settings that should override matching server settings
        var args = new List<string>(CreateArgs())
        {
            "/d:local.key=local value 1",
            "/d:shared.key1=local shared value 1 - should override server value",
            "/d:shared.casing=local lower case value",
            "/d:sonar.userHome=homeSweetHome"
        };

        var success = await scope.Execute(args);
        success.Should().BeTrue("Expecting the pre-processing to complete successfully");

        // Check the settings used when creating the SonarLint file - local and server settings should be merged
        scope.Factory.AnalyzerProvider.SuppliedSonarProperties.Should().NotBeNull();
        scope.Factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("server.key", "server value 1");
        scope.Factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("local.key", "local value 1");
        scope.Factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("shared.key1", "local shared value 1 - should override server value");
        // Keys are case-sensitive so differently cased values should be preserved
        scope.Factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("shared.CASING", "server upper case value");
        scope.Factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("shared.casing", "local lower case value");

        // Check the settings used when creating the config file - settings should be separate
        var actualConfig = scope.AssertAnalysisConfig(2);
        actualConfig.JavaExePath.Should().Be("some/path/bin/java.exe");
        actualConfig.EngineJarPath.Should().Be("some/path/to/engine.jar");
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
        if (organization is not null)
        {
            yield return $"/o:{organization}";
        }
        yield return "/d:cmd.line1=cmdline.value.1";
        yield return "/d:sonar.host.url=http://host";
        yield return "/d:sonar.log.level=INFO|DEBUG";

        if (properties is not null)
        {
            foreach (var pair in properties)
            {
                yield return $"/d:{pair.Key}={pair.Value}";
            }
        }
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

    private sealed class TestScope : IDisposable
    {
        public readonly string WorkingDir;
        public readonly MockObjectFactory Factory;
        public readonly PreProcessor PreProcessor;

        private readonly WorkingDirectoryScope workingDirectory;
        private readonly TestContext testContext;

        public TestScope(TestContext context, MockObjectFactory factory = null)
        {
            testContext = context;
            WorkingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(context);
            workingDirectory = new WorkingDirectoryScope(WorkingDir);
            Factory = factory ?? new MockObjectFactory();
            PreProcessor = new PreProcessor(Factory, Factory.Logger);
        }

        public void AssertDirectoriesCreated()
        {
            var settings = Factory.ReadSettings();
            AssertDirectoryExists(settings.AnalysisBaseDirectory);
            AssertDirectoryExists(settings.SonarConfigDirectory);
            AssertDirectoryExists(settings.SonarOutputDirectory);
            // The bootstrapper is responsible for creating the bin directory
        }

        public async Task<bool> Execute(IEnumerable<string> args = null)
        {
            args ??= CreateArgs();
            return await PreProcessor.Execute(args);
        }

        public AnalysisConfig AssertAnalysisConfig(int numAnalyzers)
        {
            var filePath = Factory.ReadSettings().AnalysisConfigFilePath;
            Factory.Logger.AssertNoErrorsLogged();
            Factory.Logger.AssertVerbosity(LoggerVerbosity.Debug);

            AssertConfigFileExists(filePath);
            var actualConfig = AnalysisConfig.Load(filePath);
            actualConfig.SonarProjectKey.Should().Be("key", "Unexpected project key");
            actualConfig.SonarProjectName.Should().Be("name", "Unexpected project name");
            actualConfig.SonarProjectVersion.Should().Be("1.0", "Unexpected project version");
            actualConfig.AnalyzersSettings.Should().NotBeNull("Analyzer settings should not be null");
            actualConfig.AnalyzersSettings.Should().HaveCount(numAnalyzers);

            AssertExpectedLocalSetting(actualConfig, SonarProperties.HostUrl, "http://host");
            AssertExpectedLocalSetting(actualConfig, "cmd.line1", "cmdline.value.1");
            AssertExpectedServerSetting(actualConfig, "server.key", "server value 1");

            return actualConfig;
        }

        public void AssertAnalysisConfigPathInSonarConfigDirectory()
        {
            var settings = Factory.ReadSettings();
            Directory.Exists(settings.SonarConfigDirectory);
            var actualFileNames = Directory.GetFiles(settings.SonarConfigDirectory).Select(Path.GetFileName);
            actualFileNames.Should().BeEquivalentTo(Path.GetFileName(settings.AnalysisConfigFilePath));
        }

        public void AssertCorrectMethodsCalled(int properties, int allLanguages, int qualityProfile, int rules)
        {
            Factory.TargetsInstaller.Received(1).InstallLoaderTargets(WorkingDir);
            Factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadProperties), properties);
            Factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadAllLanguages), allLanguages);
            Factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadQualityProfile), qualityProfile); // C# and VBNet
            Factory.Server.AssertMethodCalled(nameof(ISonarWebServer.DownloadRules), rules); // C# and VBNet
        }

        public void Dispose() =>
            workingDirectory.Dispose();

        private void AssertConfigFileExists(string filePath)
        {
            File.Exists(filePath).Should().BeTrue("Expecting the analysis config file to exist. Path: {0}", filePath);
            testContext.AddResultFile(filePath);
        }

        private static void AssertDirectoryExists(string path) =>
            Directory.Exists(path).Should().BeTrue("Expected directory does not exist: {0}", path);
    }
}
