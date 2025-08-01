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

using SonarScanner.MSBuild.Shim;
using SonarScanner.MSBuild.Shim.Interfaces;
using static FluentAssertions.FluentActions;

namespace SonarScanner.MSBuild.PostProcessor.Test;

[TestClass]
public class PostProcessorTests
{
    private const string CredentialsErrorMessage = "Credentials must be passed in both begin and end steps or not at all";
    private const string TruststorePasswordErrorMessage = "'sonar.scanner.truststorePassword' must be specified in the end step when specified during the begin step.";

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        var scanner = Substitute.For<ISonarScanner>();
        var logger = Substitute.For<ILogger>();
        var targets = Substitute.For<ITargetsUninstaller>();
        var tfs = Substitute.For<ITfsProcessor>();

        Invoking(() => new PostProcessor(null, null, null, null, null)).Should()
            .Throw<ArgumentNullException>().And.ParamName.Should().Be("sonarScanner");

        Invoking(() => new PostProcessor(scanner, null, null, null, null)).Should()
            .Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");

        Invoking(() => new PostProcessor(scanner, logger, null, null, null)).Should()
            .Throw<ArgumentNullException>().And.ParamName.Should().Be("targetUninstaller");

        Invoking(() => new PostProcessor(scanner, logger, targets, null, null)).Should()
            .Throw<ArgumentNullException>().And.ParamName.Should().Be("tfsProcessor");

        Invoking(() => new PostProcessor(scanner, logger, targets, tfs, null)).Should()
            .Throw<ArgumentNullException>().And.ParamName.Should().Be("sonarProjectPropertiesValidator");
    }

    [TestMethod]
    public void PostProc_NoProjectsToAnalyze_NoExecutionTriggered()
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.SonarOutputDir = Environment.CurrentDirectory;
        context.Config.SonarConfigDir = Environment.CurrentDirectory;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";
        context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
        context.Config.ScanAllAnalysis = true;
        context.Scanner.ValueToReturn = true;
        context.TfsProcessor.ValueToReturn = true;

        // Act
        var success = Execute_WithNoProject(context, true);

        // Assert
        success.Should().BeFalse("Expecting post-processor to have failed");
        context.TfsProcessor.AssertNotExecuted();
        context.Scanner.AssertNotExecuted();
        context.Logger.AssertNoErrorsLogged();
        context.Logger.AssertNoWarningsLogged();
        context.Logger.AssertNoUIWarningsLogged();
        context.VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_ExecutionSucceedsWithErrorLogs()
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.SonarOutputDir = Environment.CurrentDirectory;
        context.Config.SonarConfigDir = Environment.CurrentDirectory;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";
        context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
        context.Scanner.ValueToReturn = true;
        context.TfsProcessor.ValueToReturn = true;
        context.Scanner.ErrorToLog = "Errors";

        // Act
        var success = Execute(context, true);

        // Assert
        success.Should().BeTrue("Expecting post-processor to have succeeded");

        context.Scanner.AssertExecuted();
        context.Scanner.SuppliedCommandLineArgs.Should().BeEmpty();
        context.Logger.AssertErrorsLogged(1);
        context.Logger.AssertWarningsLogged(0);
        context.VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_FailsOnInvalidArgs()
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.SonarOutputDir = Environment.CurrentDirectory;
        context.TfsProcessor.ValueToReturn = false;
        context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;

        // Act
        var success = Execute(context, true, "/d:sonar.foo=bar");

        // Assert
        success.Should().BeFalse("Expecting post-processor to have failed");

        context.TfsProcessor.AssertNotExecuted();
        context.Scanner.AssertNotExecuted();
        context.Logger.AssertErrorsLogged(1);
        context.Logger.AssertWarningsLogged(0);
        context.VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_ValidArgsPassedThrough()
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.HasBeginStepCommandLineCredentials = true;
        context.Config.SonarOutputDir = Environment.CurrentDirectory;
        context.Config.SonarConfigDir = Environment.CurrentDirectory;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";
        context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
        context.Config.ScanAllAnalysis = true;
        context.Scanner.ValueToReturn = true;
        context.TfsProcessor.ValueToReturn = true;

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

        // Act
        var success = Execute(context, true, suppliedArgs);

        // Assert
        success.Should().BeTrue("Expecting post-processor to have succeeded");

        context.Scanner.AssertExecuted();
        context.Scanner.SuppliedCommandLineArgs.Should().Equal(expectedArgs, "Unexpected command line args passed to the sonar-scanner");
        context.Logger.AssertErrorsLogged(0);
        context.VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_WhenSettingInFileButNoCommandLineArg_Fail()
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.HasBeginStepCommandLineCredentials = true;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";
        context.TfsProcessor.ValueToReturn = false;

        // Act
        var success = Execute(context, true, args: []);

        // Assert
        success.Should().BeFalse();
        context.Logger.AssertErrorLogged(CredentialsErrorMessage);
        context.TfsProcessor.AssertNotExecuted();
        context.Scanner.AssertNotExecuted();
        context.VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_TruststorePasswordNotProvided_Fail()
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.HasBeginStepCommandLineTruststorePassword = true;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";
        context.TfsProcessor.ValueToReturn = false;

        // Act
        var success = Execute(context, true, args: []);

        // Assert
        success.Should().BeFalse();
        context.Logger.AssertErrorLogged(TruststorePasswordErrorMessage);
        context.TfsProcessor.AssertNotExecuted();
        context.Scanner.AssertNotExecuted();
        context.VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_TruststorePasswordProvided_DoesNotFail()
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.SonarOutputDir = Environment.CurrentDirectory;
        context.Config.SonarConfigDir = Environment.CurrentDirectory;
        context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
        context.Config.AdditionalConfig = [];
        context.Scanner.ValueToReturn = true;
        context.Config.HasBeginStepCommandLineTruststorePassword = true;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";

        // Act
        var success = Execute(context, true, args: "/d:sonar.scanner.truststorePassword=foo");

        // Assert
        success.Should().BeTrue();
        context.Logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    [DataRow("javax.net.ssl.trustStorePassword=foo")]
    [DataRow("javax.net.ssl.trustStorePassword=\"foo\"")]
    [DataRow("javax.net.ssl.trustStorePassword=")]
    public void PostProc_TruststorePasswordProvidedThroughEnv_DoesNotFail(string truststorePasswordProp)
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.SonarOutputDir = Environment.CurrentDirectory;
        context.Config.SonarConfigDir = Environment.CurrentDirectory;
        context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
        context.Config.AdditionalConfig = [];
        context.Scanner.ValueToReturn = true;
        context.Config.HasBeginStepCommandLineTruststorePassword = true;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";
        using var env = new EnvironmentVariableScope();
        env.SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, $"-D{truststorePasswordProp}");

        // Act
        var success = Execute(context, true);

        // Assert
        success.Should().BeTrue();
        context.Logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    public void PostProc_PasswordNotProvidedDuringBeginStepAndTruststorePasswordProvidedThroughEnvDuringEndStep_DoesNotFail()
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.SonarOutputDir = Environment.CurrentDirectory;
        context.Config.SonarConfigDir = Environment.CurrentDirectory;
        context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
        context.Config.AdditionalConfig = [];
        context.Scanner.ValueToReturn = true;
        context.Config.HasBeginStepCommandLineTruststorePassword = false;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";
        using var env = new EnvironmentVariableScope();
        env.SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, "-Djavax.net.ssl.trustStorePassword=foo");

        // Act
        var success = Execute(context, true);

        // Assert
        success.Should().BeTrue();
        context.Logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    public void PostProc_PasswordNotProvidedDuringBeginStepAndTruststorePasswordProvidedEndStep_DoesNotFail()
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.SonarOutputDir = Environment.CurrentDirectory;
        context.Config.SonarConfigDir = Environment.CurrentDirectory;
        context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
        context.Config.AdditionalConfig = [];
        context.Scanner.ValueToReturn = true;
        context.Config.HasBeginStepCommandLineTruststorePassword = false;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";
        using var env = new EnvironmentVariableScope();
        env.SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, null);

        // Act
        var success = Execute(context, true, args: "/d:sonar.scanner.truststorePassword=foo");

        // Assert
        success.Should().BeTrue();
        context.Logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    [DataRow("sonar.scanner.truststorePassword=")]
    [DataRow("sonar.scanner.truststorePassword")]
    public void PostProc_InvalidTruststorePasswordProvided_Fail(string truststorePasswordProperty)
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.HasBeginStepCommandLineTruststorePassword = true;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";
        context.TfsProcessor.ValueToReturn = false;

        // Act
        var success = Execute(context, true, $"/d:{truststorePasswordProperty}");

        // Assert
        success.Should().BeFalse();
        context.Logger.AssertErrorLogged($"The format of the analysis property {truststorePasswordProperty} is invalid");
        context.TfsProcessor.AssertNotExecuted();
        context.Scanner.AssertNotExecuted();
        context.VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_InvalidTruststorePasswordProvidedInEnv_Fail()
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.HasBeginStepCommandLineTruststorePassword = true;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";
        context.TfsProcessor.ValueToReturn = false;
        using var env = new EnvironmentVariableScope();
        env.SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, "-Dsonar.scanner.truststorePassword");

        // Act
        var success = Execute(context, true);

        // Assert
        success.Should().BeFalse();
        context.TfsProcessor.AssertNotExecuted();
        context.Scanner.AssertNotExecuted();
        context.VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_WhenNoSettingInFileAndCommandLineArg_Fail()
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.SonarOutputDir = Environment.CurrentDirectory;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";
        context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
        context.Config.AdditionalConfig = [];
        context.Scanner.ValueToReturn = true;
        context.TfsProcessor.ValueToReturn = false;

        // Act
        var success = Execute(context, true, args: "/d:sonar.token=foo");

        // Assert
        success.Should().BeFalse();
        context.Logger.AssertErrorLogged(CredentialsErrorMessage);
        context.TfsProcessor.AssertNotExecuted();
        context.Scanner.AssertNotExecuted();
        context.VerifyTargetsUninstaller();
    }

    [TestMethod]
    public void PostProc_WhenNoSettingInFileAndNoCommandLineArg_DoesNotFail()
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.SonarOutputDir = Environment.CurrentDirectory;
        context.Config.SonarConfigDir = Environment.CurrentDirectory;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";
        context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
        context.Config.AdditionalConfig = [];
        context.Scanner.ValueToReturn = true;

        // Act
        var success = Execute(context, true, args: []);

        // Assert
        success.Should().BeTrue();
        context.Logger.AssertNoErrorsLogged(CredentialsErrorMessage);
        context.Logger.AssertNoErrorsLogged(TruststorePasswordErrorMessage);
    }

    [TestMethod]
    public void PostProc_WhenSettingInFileAndCommandLineArg_DoesNotFail()
    {
        // Arrange
        var context = new PostProcTestContext(TestContext);
        context.Config.HasBeginStepCommandLineCredentials = true;
        context.Config.SonarConfigDir = Environment.CurrentDirectory;
        context.Config.SonarQubeHostUrl = "http://sonarqube.com";
        context.Config.SonarOutputDir = Environment.CurrentDirectory;
        context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
        context.Scanner.ValueToReturn = true;

        // Act
        var success = Execute(context, true, args: "/d:sonar.token=foo");

        // Assert
        success.Should().BeTrue();
        context.Logger.AssertNoErrorsLogged(CredentialsErrorMessage);
    }

    [TestMethod]
    public void Execute_NullArgs_Throws()
    {
        Action action = () => DummyPostProcessorExecute(null, new AnalysisConfig(), new MockBuildSettings());
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("args");
    }

    [TestMethod]
    public void Execute_NullAnalysisConfig_Throws()
    {
        Action action = () => DummyPostProcessorExecute([], null, new MockBuildSettings());
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
    }

    [TestMethod]
    public void Execute_NullTeamBuildSettings_Throws()
    {
        Action action = () => DummyPostProcessorExecute([], new AnalysisConfig(), null);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("settings");
    }

    private bool Execute_WithNoProject(PostProcTestContext context, bool propertyWriteSucceeded, params string[] args)
    {
        var sonarProjectPropertiesValidator = Substitute.For<ISonarProjectPropertiesValidator>();
        sonarProjectPropertiesValidator
            .AreExistingSonarPropertiesFilesPresent(Arg.Any<string>(), Arg.Any<ICollection<ProjectData>>(), out var _)
            .Returns(false);

        var proc = new PostProcessor(
            context.Scanner,
            context.Logger,
            context.TargetsUninstaller,
            context.TfsProcessor,
            sonarProjectPropertiesValidator);

        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var projectInfo = TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);

        var listOfProjects = new List<ProjectData>
        {
            new(ProjectInfo.Load(projectInfo))
        };

        var propertiesFileGenerator = Substitute.For<IPropertiesFileGenerator>();
        propertiesFileGenerator
            .TryWriteProperties(Arg.Any<PropertiesWriter>(), out _)
            .Returns(propertyWriteSucceeded);

        var projectInfoAnalysisResult = new ProjectInfoAnalysisResult();
        projectInfoAnalysisResult.Projects.AddRange(listOfProjects);
        projectInfoAnalysisResult.RanToCompletion = true;
        projectInfoAnalysisResult.FullPropertiesFilePath = null;

        propertiesFileGenerator.GenerateFile().Returns(projectInfoAnalysisResult);
        proc.SetPropertiesFileGenerator(propertiesFileGenerator);
        var success = proc.Execute(args, context.Config, context.Settings);
        return success;
    }

    private bool Execute(PostProcTestContext context, bool propertyWriteSucceeded, params string[] args)
    {
        var sonarProjectPropertiesValidator = Substitute.For<ISonarProjectPropertiesValidator>();
        sonarProjectPropertiesValidator
            .AreExistingSonarPropertiesFilesPresent(Arg.Any<string>(), Arg.Any<ICollection<ProjectData>>(), out var _)
            .Returns(false);

        var proc = new PostProcessor(
            context.Scanner,
            context.Logger,
            context.TargetsUninstaller,
            context.TfsProcessor,
            sonarProjectPropertiesValidator);

        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, Guid.NewGuid().ToString());

        var projectInfo = TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);

        var listOfProjects = new List<ProjectData>
        {
            new(ProjectInfo.Load(projectInfo))
        };

        var propertiesFileGenerator = Substitute.For<IPropertiesFileGenerator>();
        propertiesFileGenerator
            .TryWriteProperties(Arg.Any<PropertiesWriter>(), out _)
            .Returns(propertyWriteSucceeded);

        var projectInfoAnalysisResult = new ProjectInfoAnalysisResult();
        projectInfoAnalysisResult.Projects.AddRange(listOfProjects);
        projectInfoAnalysisResult.RanToCompletion = true;
        projectInfoAnalysisResult.FullPropertiesFilePath = Path.Combine(testDir, "sonar-project.properties");

        propertiesFileGenerator.GenerateFile().Returns(projectInfoAnalysisResult);
        proc.SetPropertiesFileGenerator(propertiesFileGenerator);
        var success = proc.Execute(args, context.Config, context.Settings);
        return success;
    }

    private void DummyPostProcessorExecute(string[] args, AnalysisConfig config, IBuildSettings settings)
    {
        var context = new PostProcTestContext(TestContext);
        var sonarProjectPropertiesValidator = Substitute.For<ISonarProjectPropertiesValidator>();

        var proc = new PostProcessor(
            context.Scanner,
            context.Logger,
            context.TargetsUninstaller,
            context.TfsProcessor,
            sonarProjectPropertiesValidator);
        proc.Execute(args, config, settings);
    }

    /// <summary>
    /// Helper class that creates all of the necessary mocks.
    /// </summary>
    private class PostProcTestContext
    {
        public ITargetsUninstaller TargetsUninstaller { get; }
        public AnalysisConfig Config { get; set; }
        public BuildSettings Settings { get; }
        public MockSonarScanner Scanner { get; }
        public TestLogger Logger { get; }
        public MockTfsProcessor TfsProcessor { get; }

        public PostProcTestContext(TestContext testContext)
        {
            Config = new AnalysisConfig();
            Settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(TestUtils.CreateTestSpecificFolderWithSubPaths(testContext));
            Logger = new TestLogger();
            TfsProcessor = new MockTfsProcessor(Logger);
            Scanner = new MockSonarScanner(Logger);
            TargetsUninstaller = Substitute.For<ITargetsUninstaller>();
        }

        public void VerifyTargetsUninstaller() =>
            TargetsUninstaller.Received(1).UninstallTargets(Arg.Any<string>());
    }
}
