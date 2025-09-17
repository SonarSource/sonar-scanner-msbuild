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

using NSubstitute.ExceptionExtensions;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public partial class PreProcessorTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        FluentActions.Invoking(() => new PreProcessor(null, new TestRuntime())).Should().Throw<ArgumentNullException>().WithParameterName("factory");
        FluentActions.Invoking(() => new PreProcessor(Substitute.For<IPreprocessorObjectFactory>(), null)).Should().Throw<ArgumentNullException>().WithParameterName("runtime");
    }

    [TestMethod]
    public void Execute_NullArguments_ThrowsArgumentNullException()
    {
        var factory = new MockObjectFactory();
        new PreProcessor(factory, factory.Runtime).Invoking(async x => await x.Execute(null)).Should().ThrowExactlyAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task Execute_InvalidArguments_ReturnsFalseAndLogsError()
    {
        var factory = new MockObjectFactory();

        (await new PreProcessor(factory, factory.Runtime).Execute(["invalid args"])).Should().Be(false);
        factory.Runtime.Logger.Should().HaveErrors("""
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
        using var context = new Context(TestContext);
        var configDirectory = Path.Combine(context.WorkingDir, "conf");
        Directory.CreateDirectory(configDirectory);
        using var lockedFile = new FileStream(Path.Combine(configDirectory, "LockedFile.txt"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);

        (await context.Execute()).Should().BeFalse();
        context.Factory.Runtime.Logger.Errors.Should().ContainMatch(
            $"Failed to create an empty directory '{configDirectory}'. Please check that there are no open or read-only files in the directory and that you have the necessary read/write permissions.*  Detailed error message: The process cannot access the file 'LockedFile.txt' because it is being used by another process.");
    }

    [TestMethod]
    public async Task Execute_InvalidLicense_ReturnsFalse()
    {
        using var context = new Context(TestContext);
        context.Factory.Server.IsServerVersionSupported().Returns(false);

        var result = await context.Execute();

        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task Execute_LicenseCheckThrows_ReturnsFalseAndLogsError()
    {
        using var context = new Context(TestContext);
        context.Factory.Server.IsServerLicenseValid().ThrowsAsync(new InvalidOperationException("Some error was thrown during license check."));

        (await context.Execute()).Should().BeFalse();
        context.Factory.Runtime.Logger.Should().HaveErrors("Some error was thrown during license check.");
    }

    [TestMethod]
    public async Task Execute_TargetsNotInstalled_ReturnsFalseAndLogsDebugMessage()
    {
        using var context = new Context(TestContext);
        (await context.Execute(CreateArgs().Append("/install:false"))).Should().BeTrue();
        context.Factory.Runtime.Logger.Should().HaveDebugs("Skipping installing the ImportsBefore targets file.");
    }

    [TestMethod]
    public async Task Execute_FetchArgumentsAndRuleSets_ConnectionIssue_ReturnsFalseAndLogsError()
    {
        using var context = new Context(TestContext);
        context.Factory.Server.DownloadQualityProfile(null, null, null).ThrowsAsyncForAnyArgs(new WebException("Could not connect to remote server", WebExceptionStatus.ConnectFailure));

        (await context.Execute()).Should().BeFalse();
        context.Factory.Runtime.Logger.Should().HaveErrors("Could not connect to the SonarQube server. Check that the URL is correct and that the server is available. URL: http://host");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Execute_ExplicitScanAllParameter_ReturnsTrue(bool scanAll)
    {
        using var context = new Context(TestContext);
        context.Factory.Server.ServerVersion.Returns(new Version(9, 10, 1, 2));
        var args = new List<string>(CreateArgs())
        {
            $"/d:sonar.scanner.scanAll={scanAll}",
        };

        (await context.Execute(args)).Should().BeTrue();

        context.Factory.Runtime.Logger.Should().HaveNoWarnings();
        context.Factory.Runtime.Logger.AssertNoUIWarningsLogged();
    }

    [TestMethod]
    public async Task Execute_ServerNotAvailable_ReturnsFalse()
    {
        using var context = new Context(TestContext);
        context.Factory.Server = null;

        var result = await context.Execute();

        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task Execute_FetchArgumentsAndRuleSets_ServerReturnsUnexpectedStatus()
    {
        using var context = new Context(TestContext);
        context.Factory.Server.DownloadQualityProfile(null, null, null).ThrowsAsyncForAnyArgs(new WebException("Something else went wrong"));

        await context.PreProcessor.Invoking(async x => await x.Execute(CreateArgs())).Should().ThrowAsync<WebException>().WithMessage("Something else went wrong");
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
        using var context = new Context(TestContext);
        context.Factory.Server.ServerVersion.Returns(new Version(9, 10, 1, 2));

        (await context.Execute()).Should().BeTrue();

        context.AssertDirectoriesCreated();
        context.AssertDownloadMethodsCalled(properties: 1, allLanguages: 1, qualityProfile: 2, rules: 2);

        context.Factory.Runtime.Logger.Should().HaveInfos("Cache data is empty. A full analysis will be performed.")
            .And.HaveDebugs("Processing analysis cache");

        var config = context.AssertAnalysisConfig(2);
        config.SonarQubeVersion.Should().Be("9.10.1.2");
        config.GetConfigValue(SonarProperties.PullRequestCacheBasePath, null).Should().Be(Path.GetDirectoryName(context.WorkingDir));
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
        using var context = new Context(TestContext);
        context.Factory.Server.ServerVersion.Returns(new Version(9, 10, 1, 2));

        var tmpCachePath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).FullName, ".temp-cache");
        var args = new List<string>(CreateArgs())
        {
            $"/d:sonar.plugin.cache.directory={tmpCachePath}",
        };

        (await context.Execute(args)).Should().BeTrue();

        context.AssertDirectoriesCreated();
        context.AssertDownloadMethodsCalled(properties: 1, allLanguages: 1, qualityProfile: 2, rules: 2);

        context.Factory.AssertMethodCalled(nameof(context.Factory.CreateRoslynAnalyzerProvider), 2); // C# and VBNet
        context.Factory.PluginCachePath.Should().Be(tmpCachePath);
        context.Factory.Runtime.Logger.Should().HaveInfos("Cache data is empty. A full analysis will be performed.")
            .And.HaveDebugs("Processing analysis cache");

        var config = context.AssertAnalysisConfig(2);
        config.SonarQubeVersion.Should().Be("9.10.1.2");
        config.GetConfigValue(SonarProperties.PullRequestCacheBasePath, null).Should().Be(Path.GetDirectoryName(context.WorkingDir));
    }

    [TestMethod]
    public async Task Execute_EndToEnd_SuccessCase_NoActiveRule()
    {
        using var context = new Context(TestContext);
        context.Factory.Server.DownloadRules("qp1").Returns([]);

        (await context.Execute()).Should().BeTrue();

        context.AssertDirectoriesCreated();
        context.AssertDownloadMethodsCalled(properties: 1, allLanguages: 1, qualityProfile: 2, rules: 2);
        context.AssertAnalysisConfig(2);
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
        using var context = new Context(TestContext, new MockObjectFactory(organization: "organization"));

        (await context.Execute(CreateArgs("organization"))).Should().BeTrue();

        context.AssertDirectoriesCreated();
        context.AssertDownloadMethodsCalled(properties: 1, allLanguages: 1, qualityProfile: 2, rules: 2);
        context.AssertAnalysisConfig(2);
    }

    [TestMethod]
    public async Task Execute_EndToEnd_UseCli_SuccessCase()
    {
        using var context = new Context(TestContext);
        var args = new List<string>(CreateArgs()) { "/d:sonar.scanner.useSonarScannerCLI=true" };
        (await context.Execute(args)).Should().BeTrue();

        context.AssertDirectoriesCreated();
        context.AssertDownloadMethodsCalled(properties: 1, allLanguages: 1, qualityProfile: 2, rules: 2);
        context.AssertAnalysisConfig(2);
        await context.Factory.EngineResolver.DidNotReceiveWithAnyArgs().ResolvePath(null);
    }

    [TestMethod]
    public async Task Execute_NoPlugin_ReturnsFalseAndLogsError()
    {
        using var context = new Context(TestContext);
        context.Factory.Server.DownloadAllLanguages().Returns(["invalid_plugin"]);

        (await context.Execute()).Should().BeFalse();

        context.Factory.Runtime.Logger.Should().HaveErrors("Could not find any dotnet analyzer plugin on the server (SonarQube/SonarCloud)!");
    }

    [TestMethod]
    public async Task Execute_NoProject_ReturnsTrue()
    {
        using var context = new Context(TestContext, new MockObjectFactory(false));
        context.Factory.Server.DownloadQualityProfile(null, null, null).ReturnsForAnyArgs((string)null);

        (await context.Execute()).Should().BeTrue();

        context.AssertDirectoriesCreated();
        context.AssertDownloadMethodsCalled(properties: 1, allLanguages: 1, qualityProfile: 2, rules: 0);   // no quality profile assigned to project
        context.AssertAnalysisConfig(0);
        // only contains SonarQubeAnalysisConfig (no rulesets or additional files)
        context.AssertAnalysisConfigPathInSonarConfigDirectory();
    }

    [TestMethod]
    public async Task Execute_HandleAnalysisException_ReturnsFalse()
    {
        // Checks end-to-end behavior when AnalysisException is thrown inside FetchArgumentsAndRulesets
        using var context = new Context(TestContext);
        context.Factory.Server.DownloadQualityProfile(null, null, null).ThrowsAsyncForAnyArgs(new AnalysisException("This message and stacktrace should not propagate to the users"));

        (await context.Execute(CreateArgs("InvalidOrganization"))).Should().BeFalse();    // Should not throw

        await context.Factory.Server.ReceivedWithAnyArgs(1).DownloadQualityProfile(null, null, null);
    }

    [TestMethod]
    // Regression test for https://github.com/SonarSource/sonar-scanner-msbuild/issues/699
    public async Task Execute_EndToEnd_Success_LocalSettingsAreUsedInSonarLintXML()
    {
        // Checks that local settings are used when creating the SonarLint.xml file, overriding
        using var context = new Context(TestContext);
        context.Factory.JreResolver
            .ResolvePath(Arg.Any<ProcessedArgs>())
            .Returns("some/path/bin/java.exe");
        context.Factory.EngineResolver
            .ResolvePath(Arg.Any<ProcessedArgs>())
            .Returns("some/path/to/engine.jar");

        context.Factory.Server.DownloadProperties(null, null)
            .ReturnsForAnyArgs(new Dictionary<string, string> { { "server.key", "server value 1" }, { "shared.key1", "server shared value 1" }, { "shared.CASING", "server upper case value" } });
        // Local settings that should override matching server settings
        var args = new List<string>(CreateArgs())
        {
            "/d:local.key=local value 1",
            "/d:shared.key1=local shared value 1 - should override server value",
            "/d:shared.casing=local lower case value",
            "/d:sonar.userHome=homeSweetHome"
        };

        (await context.Execute(args)).Should().BeTrue();

        // Check the settings used when creating the SonarLint file - local and server settings should be merged
        context.Factory.AnalyzerProvider.SuppliedSonarProperties.Should().NotBeNull();
        context.Factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("server.key", "server value 1");
        context.Factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("local.key", "local value 1");
        context.Factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("shared.key1", "local shared value 1 - should override server value");
        // Keys are case-sensitive so differently cased values should be preserved
        context.Factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("shared.CASING", "server upper case value");
        context.Factory.AnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("shared.casing", "local lower case value");

        // Check the settings used when creating the config file - settings should be separate
        var actualConfig = context.AssertAnalysisConfig(2);
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
        found.Should().BeTrue();
        actualProperty.Value.Should().Be(expectedValue);
    }

    private static void AssertExpectedServerSetting(AnalysisConfig actualConfig, string key, string expectedValue)
    {
        var found = Property.TryGetProperty(key, actualConfig.ServerSettings, out var actualProperty);
        found.Should().BeTrue();
        actualProperty.Value.Should().Be(expectedValue);
    }

    private sealed class Context : IDisposable
    {
        public readonly string WorkingDir;
        public readonly MockObjectFactory Factory;
        public readonly PreProcessor PreProcessor;

        private readonly WorkingDirectoryScope workingDirectory;
        private readonly TestContext testContext;

        public Context(TestContext testContext, MockObjectFactory factory = null)
        {
            this.testContext = testContext;
            WorkingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
            workingDirectory = new WorkingDirectoryScope(WorkingDir);
            Factory = factory ?? new MockObjectFactory();
            PreProcessor = new PreProcessor(Factory, Factory.Runtime);
            Factory.Runtime.OperatingSystem.FolderPath(default, default).ReturnsForAnyArgs("some folder");
            Factory.Runtime.File.Exists(Path.Combine(Path.GetDirectoryName(typeof(ArgumentProcessor).Assembly.Location), "Targets", FileConstants.ImportBeforeTargetsName)).Returns(true);
            Factory.Runtime.File.Exists(Path.Combine(Path.GetDirectoryName(typeof(ArgumentProcessor).Assembly.Location), "Targets", FileConstants.IntegrationTargetsName)).Returns(true);
        }

        public void AssertDirectoriesCreated()
        {
            var settings = Factory.ReadSettings();
            AssertDirectoryExists(settings.AnalysisBaseDirectory);
            AssertDirectoryExists(settings.SonarConfigDirectory);
            AssertDirectoryExists(settings.SonarOutputDirectory);
            // We do not assert SonarBinDirectory as it is created in BootstrapperClass.CopyDlls()
            // https://github.com/SonarSource/sonar-scanner-msbuild/blob/b2cc4de9b0dbf916f3956e59c21d2d730af3d26b/src/SonarScanner.MSBuild/BootstrapperClass.cs#L173
        }

        public async Task<bool> Execute(IEnumerable<string> args = null) =>
            await PreProcessor.Execute(args ?? CreateArgs());

        public AnalysisConfig AssertAnalysisConfig(int numAnalyzers)
        {
            var filePath = Factory.ReadSettings().AnalysisConfigFilePath;
            Factory.Runtime.Logger.Should().HaveNoErrors();
            Factory.Runtime.Logger.AssertVerbosity(LoggerVerbosity.Debug);

            AssertConfigFileExists(filePath);
            var actualConfig = AnalysisConfig.Load(filePath);
            actualConfig.SonarProjectKey.Should().Be("key");
            actualConfig.SonarProjectName.Should().Be("name");
            actualConfig.SonarProjectVersion.Should().Be("1.0");
            actualConfig.AnalyzersSettings.Should().NotBeNull();
            actualConfig.AnalyzersSettings.Should().HaveCount(numAnalyzers);

            AssertExpectedLocalSetting(actualConfig, SonarProperties.HostUrl, "http://host");
            AssertExpectedLocalSetting(actualConfig, "cmd.line1", "cmdline.value.1");
            AssertExpectedServerSetting(actualConfig, "server.key", "server value 1");

            return actualConfig;
        }

        public void AssertAnalysisConfigPathInSonarConfigDirectory() =>
            Directory.GetFiles(Factory.ReadSettings().SonarConfigDirectory).Select(Path.GetFileName)
                .Should().BeEquivalentTo("SonarQubeAnalysisConfig.xml");

        public void AssertDownloadMethodsCalled(int properties, int allLanguages, int qualityProfile, int rules)
        {
            Factory.Runtime.Logger.Should().HaveInfos("Updating build integration targets...");             // TargetsInstaller was called
            Factory.Server.ReceivedWithAnyArgs(properties).DownloadProperties(null, null);
            Factory.Server.ReceivedWithAnyArgs(allLanguages).DownloadAllLanguages();
            Factory.Server.ReceivedWithAnyArgs(qualityProfile).DownloadQualityProfile(null, null, null);    // C# and VBNet
            Factory.Server.ReceivedWithAnyArgs(rules).DownloadRules(null);                                  // C# and VBNet
        }

        public void Dispose() =>
            workingDirectory.Dispose();

        private void AssertConfigFileExists(string filePath)
        {
            File.Exists(filePath).Should().BeTrue();
            testContext.AddResultFile(filePath);
        }

        private static void AssertDirectoryExists(string path) =>
            Directory.Exists(path).Should().BeTrue();
    }
}
