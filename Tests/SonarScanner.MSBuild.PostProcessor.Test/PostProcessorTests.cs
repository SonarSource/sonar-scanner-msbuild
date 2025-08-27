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

using SonarScanner.MSBuild.Common.TFS;
using SonarScanner.MSBuild.Shim;
using SonarScanner.MSBuild.TFS;
using static FluentAssertions.FluentActions;
using static SonarScanner.MSBuild.TFS.BuildVNextCoverageReportProcessor;

namespace SonarScanner.MSBuild.PostProcessor.Test;

[TestClass]
public class PostProcessorTests
{
    private const string CredentialsErrorMessage = "Credentials must be passed in both begin and end steps or not at all";
    private const string TruststorePasswordErrorMessage = "'sonar.scanner.truststorePassword' must be specified in the end step when specified during the begin step.";

    private readonly PostProcessor sut;
    private readonly TestContext testContext;
    private readonly TargetsUninstaller targetsUninstaller;
    private readonly AnalysisConfig config;
    private readonly SonarScannerWrapper scanner;
    private readonly TfsProcessorWrapper tfsProcessor;
    private readonly BuildVNextCoverageReportProcessor coverageReportProcessor;
    private readonly SonarProjectPropertiesValidator sonarProjectPropertiesValidator;
    private readonly ScannerEngineInput scannerEngineInput;
    private readonly TestRuntime runtime;
    private IBuildSettings settings;

    public PostProcessorTests(TestContext testContext)
    {
        this.testContext = testContext;
        config = new()
        {
            SonarOutputDir = Environment.CurrentDirectory,
            SonarConfigDir = Environment.CurrentDirectory,
        };
        config.SetBuildUri("http://test-build-uri");
        settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(TestUtils.CreateTestSpecificFolderWithSubPaths(testContext));
        runtime = new();
        tfsProcessor = Substitute.For<TfsProcessorWrapper>(runtime);
        tfsProcessor.Execute(null, null).ReturnsForAnyArgs(true);
        scanner = Substitute.For<SonarScannerWrapper>(runtime);
        scanner.Execute(null, null, null).ReturnsForAnyArgs(true);
        targetsUninstaller = Substitute.For<TargetsUninstaller>(runtime.Logger);
        sonarProjectPropertiesValidator = Substitute.For<SonarProjectPropertiesValidator>();
        coverageReportProcessor = Substitute
            .For<BuildVNextCoverageReportProcessor>(Substitute.For<ICoverageReportConverter>(), runtime);
        coverageReportProcessor.ProcessCoverageReports(null, null).ReturnsForAnyArgs(new AdditionalProperties([@"VS\Test\Path"], [@"VS\XML\Coverage\Path"]));
        scannerEngineInput = new ScannerEngineInput(config);
        sut = new PostProcessor(
            scanner,
            runtime.Logger,
            targetsUninstaller,
            tfsProcessor,
            sonarProjectPropertiesValidator,
            coverageReportProcessor,
            runtime.File);
    }

    [TestMethod]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        Invoking(() => new PostProcessor(null, null, null, null, null, null)).Should()
            .Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("sonarScanner");

        Invoking(() => new PostProcessor(scanner, null, null, null, null, null)).Should()
            .Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("logger");

        Invoking(() => new PostProcessor(scanner, runtime.Logger, null, null, null, null)).Should()
            .Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("targetUninstaller");

        Invoking(() => new PostProcessor(scanner, runtime.Logger, targetsUninstaller, null, null, null)).Should()
            .Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("tfsProcessor");

        Invoking(() => new PostProcessor(scanner, runtime.Logger, targetsUninstaller, tfsProcessor, null, null)).Should()
            .Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("sonarProjectPropertiesValidator");

        Invoking(() => new PostProcessor(scanner, runtime.Logger, targetsUninstaller, tfsProcessor, Substitute.For<SonarProjectPropertiesValidator>(), null)).Should()
            .Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("coverageReportProcessor");
    }

    [TestMethod]
    public void PostProc_NoProjectsToAnalyze_NoExecutionTriggered()
    {
        Execute(withProject: false).Should().BeFalse("Expecting post-processor to have failed");
        scanner.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        runtime.Logger.AssertNoErrorsLogged();
        runtime.Logger.AssertNoWarningsLogged();
        runtime.Logger.AssertNoUIWarningsLogged();
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_ExecutionSucceedsWithErrorLogs()
    {
        scanner.WhenForAnyArgs(x => x.Execute(null, null, null)).Do(x => runtime.Logger.LogError("Errors"));

        Execute().Should().BeTrue("Expecting post-processor to have succeeded");
        scanner.Received().Execute(
            config,
            Arg.Is<IAnalysisPropertyProvider>(x => !x.GetAllProperties().Any()),
            Arg.Any<string>());
        runtime.Logger.AssertErrorsLogged(1);
        runtime.Logger.AssertWarningsLogged(0);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_FailsOnInvalidArgs()
    {
        Execute("/d:sonar.foo=bar").Should().BeFalse("Expecting post-processor to have failed");
        scanner.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        runtime.Logger.AssertErrorsLogged(1);
        runtime.Logger.AssertWarningsLogged(0);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_ValidArgsPassedThrough()
    {
        config.HasBeginStepCommandLineCredentials = true;
        var suppliedArgs = new[]
        {
            "/d:sonar.password=\"my pwd\"",
            "/d:sonar.login=login",
            "/d:sonar.token=token",
        };
        var expectedArgs = new[]
        {
            "-Dsonar.password=\"my pwd\"",
            "-Dsonar.login=login",
            "-Dsonar.token=token"
        };

        Execute(suppliedArgs).Should().BeTrue("Expecting post-processor to have succeeded");
        scanner.Received().Execute(
            config,
            Arg.Is<IAnalysisPropertyProvider>(x => x.GetAllProperties().Select(x => x.AsSonarScannerArg()).SequenceEqual(expectedArgs)),
            Arg.Any<string>());
        runtime.Logger.AssertErrorsLogged(0);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_WhenSettingInFileButNoCommandLineArg_Fail()
    {
        config.HasBeginStepCommandLineCredentials = true;

        Execute().Should().BeFalse();
        runtime.Logger.AssertErrorLogged(CredentialsErrorMessage);
        scanner.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_TruststorePasswordNotProvided_Fail()
    {
        config.HasBeginStepCommandLineTruststorePassword = true;

        Execute().Should().BeFalse();
        runtime.Logger.AssertErrorLogged(TruststorePasswordErrorMessage);
        scanner.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_TruststorePasswordProvided_DoesNotFail()
    {
        config.HasBeginStepCommandLineTruststorePassword = true;

        Execute("/d:sonar.scanner.truststorePassword=foo").Should().BeTrue();
        runtime.Logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    [DataRow("javax.net.ssl.trustStorePassword=foo")]
    [DataRow("javax.net.ssl.trustStorePassword=\"foo\"")]
    [DataRow("javax.net.ssl.trustStorePassword=")]
    public void PostProc_TruststorePasswordProvidedThroughEnv_DoesNotFail(string truststorePasswordProp)
    {
        config.HasBeginStepCommandLineTruststorePassword = true;
        using var env = new EnvironmentVariableScope();
        env.SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, $"-D{truststorePasswordProp}");

        Execute().Should().BeTrue();
        runtime.Logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    public void PostProc_PasswordNotProvidedDuringBeginStepAndTruststorePasswordProvidedThroughEnvDuringEndStep_DoesNotFail()
    {
        config.HasBeginStepCommandLineTruststorePassword = false;
        using var env = new EnvironmentVariableScope();
        env.SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, "-Djavax.net.ssl.trustStorePassword=foo");

        Execute().Should().BeTrue();
        runtime.Logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    public void PostProc_PasswordNotProvidedDuringBeginStepAndTruststorePasswordProvidedEndStep_DoesNotFail()
    {
        config.HasBeginStepCommandLineTruststorePassword = false;
        using var env = new EnvironmentVariableScope();
        env.SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, null);

        Execute("/d:sonar.scanner.truststorePassword=foo").Should().BeTrue();
        runtime.Logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    [DataRow("sonar.scanner.truststorePassword=")]
    [DataRow("sonar.scanner.truststorePassword")]
    public void PostProc_InvalidTruststorePasswordProvided_Fail(string truststorePasswordProperty)
    {
        config.HasBeginStepCommandLineTruststorePassword = true;

        Execute($"/d:{truststorePasswordProperty}").Should().BeFalse();
        runtime.Logger.AssertErrorLogged($"The format of the analysis property {truststorePasswordProperty} is invalid");
        scanner.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_InvalidTruststorePasswordProvidedInEnv_Fail()
    {
        config.HasBeginStepCommandLineTruststorePassword = true;
        using var env = new EnvironmentVariableScope();
        env.SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, "-Dsonar.scanner.truststorePassword");

        Execute().Should().BeFalse();
        scanner.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_WhenNoSettingInFileAndCommandLineArg_Fail()
    {
        Execute("/d:sonar.token=foo").Should().BeFalse();
        runtime.Logger.AssertErrorLogged(CredentialsErrorMessage);
        scanner.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_WhenNoSettingInFileAndNoCommandLineArg_DoesNotFail()
    {
        Execute().Should().BeTrue();
        runtime.Logger.AssertNoErrorsLogged(CredentialsErrorMessage);
        runtime.Logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    public void PostProc_WhenSettingInFileAndCommandLineArg_DoesNotFail()
    {
        config.HasBeginStepCommandLineCredentials = true;

        Execute("/d:sonar.token=foo").Should().BeTrue();
        runtime.Logger.AssertNoErrorsLogged(CredentialsErrorMessage);
    }

    [TestMethod]
    public void Execute_ExistingSonarPropertiesFilesPresent_Fail()
    {
        sonarProjectPropertiesValidator.AreExistingSonarPropertiesFilesPresent(null, null, out var _).ReturnsForAnyArgs(x =>
            {
                x[2] = new[] { "Some Path" };
                return true;
            });
        Execute().Should().BeFalse();
        sonarProjectPropertiesValidator.ReceivedWithAnyArgs().AreExistingSonarPropertiesFilesPresent(null, null, out var _);
        runtime.Logger.AssertErrorLogged("sonar-project.properties files are not understood by the SonarScanner for .NET. Remove those files from the following folders: Some Path");
    }

    [TestMethod]
    public void Execute_NullArgs_Throws() =>
        sut.Invoking(x => x.Execute(null, new AnalysisConfig(), Substitute.For<IBuildSettings>())).Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("args");

    [TestMethod]
    public void Execute_NullAnalysisConfig_Throws() =>
        sut.Invoking(x => x.Execute([], null, Substitute.For<IBuildSettings>())).Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");

    [TestMethod]
    public void Execute_NullTeamBuildSettings_Throws() =>
        sut.Invoking(x => x.Execute([], new AnalysisConfig(), null)).Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("settings");

    [TestMethod]
    public void Execute_NotTeamBuild_NoCoverageProcessorCalled()
    {
        SubstituteSettings(BuildEnvironment.NotTeamBuild);

        Execute().Should().BeTrue();
        AssertTfsProcessorConvertCoverageCalledIfNetFramework(false);
        AssertTfsProcessorSummaryReportBuilderCalledIfNetFramework(false);
        coverageReportProcessor.DidNotReceiveWithAnyArgs().ProcessCoverageReports(null, null);
    }

    [TestMethod]
    public void Execute_TeamBuild_CoverageReportProcessorCalled()
    {
        SubstituteSettings(BuildEnvironment.TeamBuild);

        Execute().Should().BeTrue();
        AssertTfsProcessorConvertCoverageCalledIfNetFramework(false);
        AssertTfsProcessorSummaryReportBuilderCalledIfNetFramework(false);
        AssertProcessCoverageReportsCalledIfNetFramework();

#if NETFRAMEWORK
        runtime.File.Received().AppendAllText(
            Arg.Any<string>(),
            Arg.Is<string>(x => x.Contains("sonar.cs.vstest.reportsPaths") && x.Contains(PathCombineWithEscape("VS", "Test", "Path"))));
        runtime.File.Received().AppendAllText(
            Arg.Any<string>(),
            Arg.Is<string>(x => x.Contains("sonar.cs.vscoveragexml.reportsPaths") && x.Contains(PathCombineWithEscape("VS", "XML", "Coverage", "Path"))));
        var reader = new ScannerEngineInputReader(scannerEngineInput.ToString());
        reader.AssertProperty("sonar.cs.vstest.reportsPaths", Path.Combine("VS", "Test", "Path"));
        reader.AssertProperty("sonar.cs.vscoveragexml.reportsPaths", Path.Combine("VS", "XML", "Coverage", "Path"));
#endif
    }

    [TestMethod]
    public void Execute_LegacyTeamBuild_TfsProcessorCalled()
    {
        SubstituteSettings(BuildEnvironment.LegacyTeamBuild);

        Execute().Should().BeTrue();
        AssertTfsProcessorConvertCoverageCalledIfNetFramework();
        AssertTfsProcessorSummaryReportBuilderCalledIfNetFramework();
        coverageReportProcessor.DidNotReceiveWithAnyArgs().ProcessCoverageReports(null, null);
    }

    [TestMethod]
    public void Execute_LegacyTeamBuild_BuildUrisDoNotMatch_Fail()
    {
        SubstituteSettings(BuildEnvironment.LegacyTeamBuild);
        config.SetBuildUri("http://other-uri");

        Execute().Should().BeFalse();
        AssertTfsProcessorConvertCoverageCalledIfNetFramework(false);
        AssertTfsProcessorSummaryReportBuilderCalledIfNetFramework(false);
        coverageReportProcessor.DidNotReceiveWithAnyArgs().ProcessCoverageReports(null, null);
        runtime.Logger.AssertErrorLogged("""
            Inconsistent build environment settings: the build Uri in the analysis config file does not match the build uri from the environment variable.
            Build Uri from environment: http://test-build-uri
            Build Uri from config: http://other-uri
            Analysis config file: Path-to-SonarQubeAnalysisConfig.xml
            Please delete the analysis config file and try the build again.
            """);
    }

    [TestMethod]
    public void Execute_LegacyTeamBuild_SkipLegacyCodeCoverage_TfsProcessorCalledOnlyForSummaryReportBuilder()
    {
        SubstituteSettings(BuildEnvironment.LegacyTeamBuild);
        using var env = new EnvironmentVariableScope();
        env.SetVariable(EnvironmentVariables.SkipLegacyCodeCoverage, "true");

        Execute().Should().BeTrue();
        AssertTfsProcessorConvertCoverageCalledIfNetFramework(false);
        AssertTfsProcessorSummaryReportBuilderCalledIfNetFramework();
        coverageReportProcessor.DidNotReceiveWithAnyArgs().ProcessCoverageReports(null, null);
    }

    private bool Execute(string arg) =>
        Execute([arg]);

    private bool Execute(string[] args = null, bool withProject = true)
    {
        args ??= [];
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
        var projectInfo = TestUtils.CreateProjectWithFiles(testContext, "withFiles1", testDir);
        var scannerEngineInputGenerator = Substitute.For<ScannerEngineInputGenerator>(config, runtime.Logger);

        var projectInfoAnalysisResult = new ProjectInfoAnalysisResult(
            [new[] { ProjectInfo.Load(projectInfo) }.ToProjectData(true, runtime.Logger).Single()],
            withProject ? scannerEngineInput : null,
            withProject ? Path.Combine(testDir, "sonar-project.properties") : null) { RanToCompletion = true };
        scannerEngineInputGenerator.GenerateResult().Returns(projectInfoAnalysisResult);
        sut.SetScannerEngineInputGenerator(scannerEngineInputGenerator);
        var success = sut.Execute(args, config, settings);
        return success;
    }

    private void AssertProcessCoverageReportsCalledIfNetFramework() =>
#if NETFRAMEWORK
        coverageReportProcessor.ReceivedWithAnyArgs().ProcessCoverageReports(null, null);
#else
        coverageReportProcessor.DidNotReceiveWithAnyArgs().ProcessCoverageReports(null, null);
#endif

    private void AssertTfsProcessorConvertCoverageCalledIfNetFramework(bool shouldBeCalled = true) =>
        AssertTfsProcessorCommandCalledIfNetFramework("ConvertCoverage", shouldBeCalled);

    private void AssertTfsProcessorSummaryReportBuilderCalledIfNetFramework(bool shouldBeCalled = true) =>
        AssertTfsProcessorCommandCalledIfNetFramework("SummaryReportBuilder", shouldBeCalled);

    private void AssertTfsProcessorCommandCalledIfNetFramework(string command, bool shouldBeCalled)
    {
#if NETFRAMEWORK
        if (shouldBeCalled)
        {
            tfsProcessor.Received().Execute(Arg.Any<AnalysisConfig>(), Arg.Is<IEnumerable<string>>(x => x.Contains(command)));
        }
        else
        {
            tfsProcessor.DidNotReceive().Execute(Arg.Any<AnalysisConfig>(), Arg.Is<IEnumerable<string>>(x => x.Contains(command)));
        }
#else
        tfsProcessor.DidNotReceiveWithAnyArgs().Execute(null, null);
#endif
    }

    private void VerifyTargetsUninstaller() =>
        targetsUninstaller.Received(1).UninstallTargets(Arg.Any<string>());

    private void SubstituteSettings(BuildEnvironment environment)
    {
        settings = Substitute.For<IBuildSettings>();
        settings.BuildEnvironment.Returns(environment);
        settings.BuildUri.Returns(config.GetBuildUri());
        settings.AnalysisConfigFilePath.Returns("Path-to-SonarQubeAnalysisConfig.xml");
    }

    private static string PathCombineWithEscape(params string[] parts)
    {
        var separator = Path.DirectorySeparatorChar.ToString();
        if (separator == @"\")
        {
            separator = @"\\";
        }
        return string.Join(separator, parts);
    }
}
