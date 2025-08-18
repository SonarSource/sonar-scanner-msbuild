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

using SonarScanner.MSBuild.Shim;
using static FluentAssertions.FluentActions;

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
    private readonly BuildSettings settings;
    private readonly SonarScannerWrapper scanner;
    private readonly TestLogger logger;
    private readonly TfsProcessorWrapper tfsProcessor;
    private readonly SonarProjectPropertiesValidator sonarProjectPropertiesValidator;

    public PostProcessorTests(TestContext testContext)
    {
        this.testContext = testContext;
        config = new()
        {
            SonarOutputDir = Environment.CurrentDirectory,
            SonarConfigDir = Environment.CurrentDirectory,
        };
        settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(TestUtils.CreateTestSpecificFolderWithSubPaths(testContext));
        logger = new();
        tfsProcessor = Substitute.For<TfsProcessorWrapper>(logger, Substitute.For<IOperatingSystemProvider>());
        tfsProcessor.Execute(null, null, null).ReturnsForAnyArgs(true);
        scanner = Substitute.For<SonarScannerWrapper>(logger, Substitute.For<IOperatingSystemProvider>());
        scanner.Execute(null, null, null).ReturnsForAnyArgs(true);
        targetsUninstaller = Substitute.For<TargetsUninstaller>(logger);
        sonarProjectPropertiesValidator = Substitute.For<SonarProjectPropertiesValidator>();
        sut = new PostProcessor(
            scanner,
            logger,
            targetsUninstaller,
            tfsProcessor,
            sonarProjectPropertiesValidator);
    }

    [TestMethod]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        Invoking(() => new PostProcessor(null, null, null, null, null)).Should()
            .Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("sonarScanner");

        Invoking(() => new PostProcessor(scanner, null, null, null, null)).Should()
            .Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("logger");

        Invoking(() => new PostProcessor(scanner, logger, null, null, null)).Should()
            .Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("targetUninstaller");

        Invoking(() => new PostProcessor(scanner, logger, targetsUninstaller, null, null)).Should()
            .Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("tfsProcessor");

        Invoking(() => new PostProcessor(scanner, logger, targetsUninstaller, tfsProcessor, null)).Should()
            .Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("sonarProjectPropertiesValidator");
    }

    [TestMethod]
    public void PostProc_NoProjectsToAnalyze_NoExecutionTriggered()
    {
        Execute(withProject: false).Should().BeFalse("Expecting post-processor to have failed");
        tfsProcessor.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        scanner.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        logger.AssertNoErrorsLogged();
        logger.AssertNoWarningsLogged();
        logger.AssertNoUIWarningsLogged();
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_ExecutionSucceedsWithErrorLogs()
    {
        scanner.WhenForAnyArgs(x => x.Execute(null, null, null)).Do(x => logger.LogError("Errors"));

        Execute().Should().BeTrue("Expecting post-processor to have succeeded");
        AssertTfsProcesserCalledIfNetFramework();
        scanner.Received().Execute(
            config,
            Arg.Is<IAnalysisPropertyProvider>(x => !x.GetAllProperties().Any()),
            Arg.Any<string>());
        logger.AssertErrorsLogged(1);
        logger.AssertWarningsLogged(0);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_FailsOnInvalidArgs()
    {
        Execute("/d:sonar.foo=bar").Should().BeFalse("Expecting post-processor to have failed");
        tfsProcessor.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        scanner.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        logger.AssertErrorsLogged(1);
        logger.AssertWarningsLogged(0);
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
        AssertTfsProcesserCalledIfNetFramework();
        scanner.Received().Execute(
            config,
            Arg.Is<IAnalysisPropertyProvider>(x => x.GetAllProperties().Select(x => x.AsSonarScannerArg()).SequenceEqual(expectedArgs)),
            Arg.Any<string>());
        logger.AssertErrorsLogged(0);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_WhenSettingInFileButNoCommandLineArg_Fail()
    {
        config.HasBeginStepCommandLineCredentials = true;

        Execute().Should().BeFalse();
        logger.AssertErrorLogged(CredentialsErrorMessage);
        tfsProcessor.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        scanner.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_TruststorePasswordNotProvided_Fail()
    {
        config.HasBeginStepCommandLineTruststorePassword = true;

        Execute().Should().BeFalse();
        logger.AssertErrorLogged(TruststorePasswordErrorMessage);
        tfsProcessor.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        scanner.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_TruststorePasswordProvided_DoesNotFail()
    {
        config.HasBeginStepCommandLineTruststorePassword = true;

        Execute("/d:sonar.scanner.truststorePassword=foo").Should().BeTrue();
        logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
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
        logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    public void PostProc_PasswordNotProvidedDuringBeginStepAndTruststorePasswordProvidedThroughEnvDuringEndStep_DoesNotFail()
    {
        config.HasBeginStepCommandLineTruststorePassword = false;
        using var env = new EnvironmentVariableScope();
        env.SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, "-Djavax.net.ssl.trustStorePassword=foo");

        Execute().Should().BeTrue();
        logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    public void PostProc_PasswordNotProvidedDuringBeginStepAndTruststorePasswordProvidedEndStep_DoesNotFail()
    {
        config.HasBeginStepCommandLineTruststorePassword = false;
        using var env = new EnvironmentVariableScope();
        env.SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, null);

        Execute("/d:sonar.scanner.truststorePassword=foo").Should().BeTrue();
        logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    [DataRow("sonar.scanner.truststorePassword=")]
    [DataRow("sonar.scanner.truststorePassword")]
    public void PostProc_InvalidTruststorePasswordProvided_Fail(string truststorePasswordProperty)
    {
        config.HasBeginStepCommandLineTruststorePassword = true;

        Execute($"/d:{truststorePasswordProperty}").Should().BeFalse();
        logger.AssertErrorLogged($"The format of the analysis property {truststorePasswordProperty} is invalid");
        tfsProcessor.DidNotReceiveWithAnyArgs().Execute(null, null, null);
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
        tfsProcessor.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        scanner.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_WhenNoSettingInFileAndCommandLineArg_Fail()
    {
        Execute("/d:sonar.token=foo").Should().BeFalse();
        logger.AssertErrorLogged(CredentialsErrorMessage);
        tfsProcessor.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        scanner.DidNotReceiveWithAnyArgs().Execute(null, null, null);
        VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_WhenNoSettingInFileAndNoCommandLineArg_DoesNotFail()
    {
        Execute().Should().BeTrue();
        logger.AssertNoErrorsLogged(CredentialsErrorMessage);
        logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    public void PostProc_WhenSettingInFileAndCommandLineArg_DoesNotFail()
    {
        config.HasBeginStepCommandLineCredentials = true;

        Execute("/d:sonar.token=foo").Should().BeTrue();
        logger.AssertNoErrorsLogged(CredentialsErrorMessage);
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

    private bool Execute(string arg) =>
        Execute([arg]);

    private bool Execute(string[] args = null, bool withProject = true)
    {
        args ??= [];
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
        var projectInfo = TestUtils.CreateProjectWithFiles(testContext, "withFiles1", testDir);
        var listOfProjects = new List<ProjectData>
        {
            new(ProjectInfo.Load(projectInfo))
        };

        var propertiesFileGenerator = Substitute.For<PropertiesFileGenerator>(config, logger);
        propertiesFileGenerator
            .TryWriteProperties(Arg.Any<PropertiesWriter>(), out _)
            .Returns(true);

        var projectInfoAnalysisResult = new ProjectInfoAnalysisResult();
        projectInfoAnalysisResult.Projects.AddRange(listOfProjects);
        projectInfoAnalysisResult.RanToCompletion = true;
        projectInfoAnalysisResult.FullPropertiesFilePath = withProject ? Path.Combine(testDir, "sonar-project.properties") : null;

        propertiesFileGenerator.GenerateFile().Returns(projectInfoAnalysisResult);
        sut.SetPropertiesFileGenerator(propertiesFileGenerator);
        var success = sut.Execute(args, config, settings);
        return success;
    }

    private void AssertTfsProcesserCalledIfNetFramework() =>
#if NETFRAMEWORK
        tfsProcessor.ReceivedWithAnyArgs().Execute(null, null, null);
#else
        tfsProcessor.DidNotReceiveWithAnyArgs().Execute(null, null, null);
#endif

    private void VerifyTargetsUninstaller() =>
        targetsUninstaller.Received(1).UninstallTargets(Arg.Any<string>());
}
