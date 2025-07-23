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

using System.Runtime.InteropServices;
using NSubstitute.Extensions;
using TestUtilities.Certificates;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class SonarScannerWrapperTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Execute_WhenConfigIsNull_Throws()
    {
        var testSubject = new SonarScannerWrapper(new TestLogger(), Substitute.For<IOperatingSystemProvider>());
        Action act = () => testSubject.Execute(null, EmptyPropertyProvider.Instance, string.Empty);

        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
    }

    [TestMethod]
    public void Execute_WhenUserCmdLineArgumentsIsNull_Throws()
    {
        var testSubject = new SonarScannerWrapper(new TestLogger(), Substitute.For<IOperatingSystemProvider>());
        Action act = () => testSubject.Execute(new AnalysisConfig(), null, string.Empty);

        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("userCmdLineArguments");
    }

    [TestMethod]
    public void Execute_WhenFullPropertiesFilePathIsNull_ReturnsFalse()
    {
        var testSubject = new SonarScannerWrapper(new TestLogger(), Substitute.For<IOperatingSystemProvider>());
        var result = testSubject.Execute(new AnalysisConfig(), EmptyPropertyProvider.Instance, null);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void Execute_ReturnTrue()
    {
        var testSubject = Substitute.ForPartsOf<SonarScannerWrapper>(new TestLogger(), Substitute.For<IOperatingSystemProvider>());
        testSubject
            .Configure()
            .ExecuteJavaRunner(Arg.Any<AnalysisConfig>(), Arg.Any<IAnalysisPropertyProvider>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProcessRunner>())
            .Returns(true);
        var result = testSubject.Execute(new AnalysisConfig(), EmptyPropertyProvider.Instance, "some/path");

        result.Should().BeTrue();
    }

    [TestMethod]
    public void Ctor_WhenLoggerIsNull_Throws()
    {
        Action act = () => _ = new SonarScannerWrapper(null, Substitute.For<IOperatingSystemProvider>());

        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void Ctor_WhenOperatingSystemProviderIsNull_Throws()
    {
        Action act = () => _ = new SonarScannerWrapper(new TestLogger(), null);

        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("operatingSystemProvider");
    }

    [TestMethod]
    public void SonarScannerHome_NoMessageIfNotAlreadySet()
    {
        var testLogger = new TestLogger();

        using var scope = new EnvironmentVariableScope().SetVariable(EnvironmentVariables.SonarScannerHomeVariableName, null);
        var config = new AnalysisConfig { SonarScannerWorkingDirectory = "C:\\working\\dir" };
        var mockRunner = new MockProcessRunner(executeResult: true);

        var success = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, testLogger, "c:\\file.exe", "d:\\properties.prop", mockRunner);

        VerifyProcessRunOutcome(mockRunner, testLogger, "C:\\working\\dir", success, true);
        testLogger.AssertMessageNotLogged(Resources.MSG_SonarScannerHomeIsSet);
    }

    [TestMethod]
    public void SonarScannerHome_MessageLoggedIfAlreadySet()
    {
        using var scope = new EnvironmentVariableScope().SetVariable(EnvironmentVariables.SonarScannerHomeVariableName, "some_path");
        var testLogger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig { SonarScannerWorkingDirectory = "c:\\workingDir" };

        var success = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, testLogger, "c:\\exePath", "f:\\props.txt", mockRunner);

        VerifyProcessRunOutcome(mockRunner, testLogger, "c:\\workingDir", success, true);
        testLogger.AssertInfoMessageExists(Resources.MSG_SonarScannerHomeIsSet);
    }

    [TestMethod]
    public void SonarScanner_StandardAdditionalArgumentsPassed()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig { SonarScannerWorkingDirectory = "c:\\work" };

        var success = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "c:\\exe.Path", "d:\\propertiesFile.Path", mockRunner);

        VerifyProcessRunOutcome(mockRunner, logger, "c:\\work", success, true);
    }

    [TestMethod]
    public void SonarScanner_CmdLineArgsArePassedThroughToTheWrapperAndAppearFirst()
    {
        var logger = new TestLogger();
        var userArgs = new ListPropertiesProvider();
        userArgs.AddProperty("sonar.login", "me");
        userArgs.AddProperty("sonar.password", "my.pwd");
        userArgs.AddProperty("sonar.token", "token");
        var mockRunner = new MockProcessRunner(executeResult: true);

        var success = ExecuteJavaRunnerIgnoringAsserts(
            new AnalysisConfig { SonarScannerWorkingDirectory = "D:\\dummyWorkingDirectory" },
            userArgs,
            logger,
            "c:\\dummy.exe",
            "c:\\foo.properties",
            mockRunner);

        VerifyProcessRunOutcome(mockRunner, logger, "D:\\dummyWorkingDirectory", success, true);
        CheckStandardArgsPassed(mockRunner, "c:\\foo.properties");
        var loginIndex = CheckArgExists("-Dsonar.login=me", mockRunner);
        var passwordIndex = CheckArgExists("-Dsonar.password=my.pwd", mockRunner);
        var tokenIndex = CheckArgExists("-Dsonar.token=token", mockRunner);
        var propertiesFileIndex = CheckArgExists(SonarScannerWrapper.ProjectSettingsFileArgName, mockRunner);
        propertiesFileIndex.Should().BeGreaterThan(loginIndex, "User arguments should appear first");
        propertiesFileIndex.Should().BeGreaterThan(passwordIndex, "User arguments should appear first");
        propertiesFileIndex.Should().BeGreaterThan(tokenIndex, "User arguments should appear first");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void SonarScanner_SensitiveArgsPassedOnCommandLine()
    {
        // Check that sensitive arguments from the config are passed on the command line
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var userArgs = new ListPropertiesProvider();
        userArgs.AddProperty("xxx", "yyy");
        userArgs.AddProperty("sonar.password", "cmdline.password");

        // Create a config file containing sensitive arguments
        var fileSettings = new AnalysisProperties
        {
            new(SonarProperties.ClientCertPassword, "client certificate password"),
            new(SonarProperties.SonarPassword, "file.password - should not be returned"),
            new(SonarProperties.SonarUserName, "file.username - should not be returned"),
            new(SonarProperties.SonarToken, "token - should not be returned"),
            new("file.not.sensitive.key", "not sensitive value")
        };

        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settingsFilePath = Path.Combine(testDir, "fileSettings.txt");
        fileSettings.Save(settingsFilePath);

        var config = new AnalysisConfig { SonarScannerWorkingDirectory = testDir };
        config.SetSettingsFilePath(settingsFilePath);

        // Act
        var success = ExecuteJavaRunnerIgnoringAsserts(config, userArgs, logger, "c:\\foo.exe", "c:\\foo.props", mockRunner);

        // Assert
        VerifyProcessRunOutcome(mockRunner, logger, testDir, success, true);

        CheckStandardArgsPassed(mockRunner, "c:\\foo.props");

        // Non-sensitive values from the file should not be passed on the command line
        CheckArgDoesNotExist("file.not.sensitive.key", mockRunner);
        CheckArgDoesNotExist(SonarProperties.SonarUserName, mockRunner);
        CheckArgDoesNotExist(SonarProperties.SonarPassword, mockRunner);
        CheckArgDoesNotExist(SonarProperties.ClientCertPassword, mockRunner);
        CheckArgDoesNotExist(SonarProperties.SonarToken, mockRunner);

        var clientCertPwdIndex = CheckArgExists("-Dsonar.clientcert.password=client certificate password", mockRunner); // sensitive value from file
        var userPwdIndex = CheckArgExists("-Dsonar.password=cmdline.password", mockRunner); // sensitive value from cmd line: overrides file value

        var propertiesFileIndex = CheckArgExists(SonarScannerWrapper.ProjectSettingsFileArgName, mockRunner);

        propertiesFileIndex.Should().BeGreaterThan(clientCertPwdIndex, "User arguments should appear first");
        propertiesFileIndex.Should().BeGreaterThan(userPwdIndex, "User arguments should appear first");
    }

    [TestMethod]
    public void SonarScanner_NoUserSpecifiedEnvVars_SONARSCANNEROPTSIsNotPassed()
    {
        // Arrange
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig { SonarScannerWorkingDirectory = "c:\\work" };

        // Act
        var success = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "c:\\exe.Path", "d:\\propertiesFile.Path", mockRunner);

        // Assert
        VerifyProcessRunOutcome(mockRunner, logger, "c:\\work", success, true);

        mockRunner.SuppliedArguments.EnvironmentVariables.Should().ContainSingle();

        // #656: Check that the JVM size is not set by default
        // https://github.com/SonarSource/sonar-scanner-msbuild/issues/656
        logger.InfoMessages.Should().NotContain(x => x.Contains("SONAR_SCANNER_OPTS"));
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void SonarScanner_UserSpecifiedEnvVars_OnlySONARSCANNEROPTSIsPassed()
    {
        // Arrange
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig { SonarScannerWorkingDirectory = "c:\\work" };

        using (var scope = new EnvironmentVariableScope())
        {
            // the SONAR_SCANNER_OPTS variable should be passed through explicitly,
            // but not other variables
            scope.SetVariable("Foo", "xxx");
            scope.SetVariable("SONAR_SCANNER_OPTS", "-Xmx2048m");
            scope.SetVariable("Bar", "yyy");

            // Act
            var success = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "c:\\exe.Path", "d:\\propertiesFile.Path", mockRunner);

            // Assert
            VerifyProcessRunOutcome(mockRunner, logger, "c:\\work", success, true);
        }

        CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Xmx2048m -Djavax.net.ssl.trustStorePassword=\"changeit\"", mockRunner);
        mockRunner.SuppliedArguments.EnvironmentVariables.Should().ContainSingle();
        logger.InfoMessages.Should().Contain(x => x.Contains("SONAR_SCANNER_OPTS"));
        logger.InfoMessages.Should().Contain(x => x.Contains("-Xmx2048m"));
    }

    [TestMethod]
    public void SonarScanner_TrustStorePasswordInScannerOptsEnd_ShouldBeRedacted()
    {
        // Arrange
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig { SonarScannerWorkingDirectory = "c:\\work" };

        using (var scope = new EnvironmentVariableScope())
        {
            scope.SetVariable("SONAR_SCANNER_OPTS", "-Xmx2048m -Djavax.net.ssl.trustStorePassword=\"changeit\"");

            // Act
            var success = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "c:\\exe.Path", "d:\\propertiesFile.Path", mockRunner);

            // Assert
            VerifyProcessRunOutcome(mockRunner, logger, "c:\\work", success, true);
        }

        mockRunner.SuppliedArguments.EnvironmentVariables.Count.Should().Be(1);
        logger.InfoMessages.Should().Contain(x => x.Contains("SONAR_SCANNER_OPTS"));
        logger.InfoMessages.Should().Contain(x => x.Contains("-Xmx2048m"));
        logger.InfoMessages.Should().Contain(x => x.Contains("-D<sensitive data removed>"));
        logger.InfoMessages.Should().NotContain(x => x.Contains("-Djavax.net.ssl.trustStorePassword=\"changeit\""));
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [DataTestMethod]
    [DataRow(@"C:\Program Files\Java\jdk-17\bin\java.exe", @"C:\Program Files\Java\jdk-17")]
    [DataRow(@"C:\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\" +
             @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\" +
             @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\" +
             @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\" +
             @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\" +
             @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\" +
             @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\bin\java.exe",
             @"C:\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\" +
             @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\" +
             @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\" +
             @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\" +
             @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\" +
             @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\" +
             @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path")]
    public void SonarScanner_WhenJavaExePathIsSet_JavaHomeIsSet(string path, string expected)
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig { JavaExePath = path };

        using (new EnvironmentVariableScope())
        {
            var result = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "exe file path", "properties file path", mockRunner);
            result.Should().BeTrue();
        }

        CheckEnvVarExists("JAVA_HOME", expected, mockRunner);
        logger.DebugMessages.Should().Contain(x => x.Contains($@"Setting the JAVA_HOME for the scanner cli to {expected}."));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("\t")]
    public void SonarScanner_WhenJavaExePathIsNullOrWhitespace(string path)
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig { JavaExePath = path };

        using (new EnvironmentVariableScope())
        {
            var result = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "exe file path", "properties file path", mockRunner);
            result.Should().BeTrue();
        }

        logger.DebugMessages.Should().BeEmpty();
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }

    [DataTestMethod]
#if NETFRAMEWORK
    [DataRow("java.exe", "Path cannot be the empty string or all whitespace.")]
#else
    [DataRow("java.exe", "The value cannot be an empty string.")]
#endif
    [DataRow("/", "Value cannot be null.")]
    public void SonarScanner_WhenSettingJavaHomePathFails_AWarningIsLogged(string path, string errorMessage)
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig { JavaExePath = path };

        using (new EnvironmentVariableScope())
        {
            var result = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "exe file path", "properties file path", mockRunner);
            result.Should().BeTrue();
        }

        logger.Warnings.Single().Should().StartWith($"Setting the JAVA_HOME for the scanner cli failed. `sonar.scanner.javaExePath` is `{path}`. {errorMessage}");
        logger.DebugMessages.Should().BeEmpty();
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void SonarScanner_ScannerOptsSettingSonarScannerOptsEmpty()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig();
        config.ScannerOptsSettings.Add(new Property("some.property", "value"));

        using (new EnvironmentVariableScope())
        {
            var result = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "exe file path", "properties file path", mockRunner);
            result.Should().BeTrue();
        }

        CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Dsome.property=value -Djavax.net.ssl.trustStorePassword=\"changeit\"", mockRunner);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void SonarScanner_ScannerOptsSettingSonarScannerOptsEmpty_Multiple()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig();
        config.ScannerOptsSettings.Add(new Property("some.property", "value"));
        config.ScannerOptsSettings.Add(new Property("some.other.property", "\"another value with #%\\/?*\""));

        using (new EnvironmentVariableScope())
        {
            var result = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "exe file path", "properties file path", mockRunner);
            result.Should().BeTrue();
        }

        CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Dsome.property=value -Dsome.other.property=\"another value with #%\\/?*\" -Djavax.net.ssl.trustStorePassword=\"changeit\"", mockRunner);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void SonarScanner_ScannerOptsSettingSonarScannerOptsNotEmpty()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig();
        config.ScannerOptsSettings.Add(new Property("some.property", "value"));

        using (var scope = new EnvironmentVariableScope())
        {
            scope.SetVariable("SONAR_SCANNER_OPTS", "-Dsonar.anything.config=existing");
            var result = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "exe file path", "properties file path", mockRunner);
            result.Should().BeTrue();
        }

        CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Dsonar.anything.config=existing -Dsome.property=value -Djavax.net.ssl.trustStorePassword=\"changeit\"", mockRunner);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void SonarScanner_ScannerOptsSettingSonarScannerOptsNotEmpty_PropertyAlreadySet()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig();
        config.ScannerOptsSettings.Add(new Property("some.property", "new"));

        using (var scope = new EnvironmentVariableScope())
        {
            scope.SetVariable("SONAR_SCANNER_OPTS", "-Dsome.property=existing");
            var result = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "exe file path", "properties file path", mockRunner);
            result.Should().BeTrue();
        }

        CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Dsome.property=existing -Dsome.property=new -Djavax.net.ssl.trustStorePassword=\"changeit\"", mockRunner);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void SonarScanner_ScannerOptsSettingSonarScannerOptsEmptyWithTruststorePassword_ShouldBeInEnv()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var userArgs = new ListPropertiesProvider();
        userArgs.AddProperty(SonarProperties.TruststorePassword, "password");
        var config = new AnalysisConfig();
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = ExecuteJavaRunnerIgnoringAsserts(config, userArgs, logger, "exe file path", "properties file path", mockRunner);

        result.Should().BeTrue();
        CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=\"password\"", mockRunner);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void SonarScanner_TruststorePassword_ShouldBeInEnv()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var userArgs = new ListPropertiesProvider();
        userArgs.AddProperty(SonarProperties.TruststorePassword, "password");
        var config = new AnalysisConfig();
        config.ScannerOptsSettings.Add(new Property("some.property", "value"));
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = ExecuteJavaRunnerIgnoringAsserts(config, userArgs, logger, "exe file path", "properties file path", mockRunner);

        result.Should().BeTrue();
        CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Dsome.property=value -Djavax.net.ssl.trustStorePassword=\"password\"", mockRunner);
    }

    [TestMethod]
    public void SonarScanner_TruststorePasswordLinux_ShouldBeInEnv()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var userArgs = new ListPropertiesProvider();
        userArgs.AddProperty(SonarProperties.TruststorePassword, "password");
        var config = new AnalysisConfig();
        config.ScannerOptsSettings.Add(new Property("some.property", "value"));
        var osProvider = Substitute.For<IOperatingSystemProvider>();
        osProvider.IsUnix().Returns(true);
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = ExecuteJavaRunnerIgnoringAsserts(config, userArgs, logger, "exe file path", "properties file path", mockRunner, osProvider);

        result.Should().BeTrue();
        CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Dsome.property=value -Djavax.net.ssl.trustStorePassword=password", mockRunner);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void SonarScanner_CmdTruststorePasswordAndInEnv_CmdShouldBeLatest()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var userArgs = new ListPropertiesProvider();
        userArgs.AddProperty(SonarProperties.TruststorePassword, "password");
        var config = new AnalysisConfig();
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another");

        var result = ExecuteJavaRunnerIgnoringAsserts(config, userArgs, logger, "exe file path", "properties file path", mockRunner);

        result.Should().BeTrue();
        CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another -Djavax.net.ssl.trustStorePassword=\"password\"", mockRunner);
    }

    [TestMethod]
    public void SonarScanner_NoCmdTruststorePasswordAndInEnv_NoAddition()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig();
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another");

        var result = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "exe file path", "properties file path", mockRunner);

        result.Should().BeTrue();
        CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another", mockRunner);
    }

    [DataTestMethod]
    [DataRow("changeit")]
    [DataRow("sonar")]
    public void SonarScanner_NoCmdTruststorePasswordAndProvidedTruststore_UseDefaultPassword(string defaultPassword)
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig();
        using var truststoreFile = new TempFile("pfx");
        config.ScannerOptsSettings.Add(new Property("javax.net.ssl.trustStore", truststoreFile.FileName));
        CertificateBuilder.CreateWebServerCertificate().ToPfx(truststoreFile.FileName, defaultPassword);

        var result = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "exe file path", "properties file path", mockRunner);

        result.Should().BeTrue();
        CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Djavax.net.ssl.trustStore={truststoreFile.FileName} -Djavax.net.ssl.trustStorePassword={SurroundByQuotes(defaultPassword)}", mockRunner);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void SonarScanner_NoCmdTruststorePasswordAndNotInEnv_UseDefault()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig();
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "exe file path", "properties file path", mockRunner);

        result.Should().BeTrue();
        CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=\"changeit\"", mockRunner);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void SonarScanner_CmdTruststorePasswordAndInEnv_ShouldUseCmd()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var userArgs = new ListPropertiesProvider();
        userArgs.AddProperty(SonarProperties.TruststorePassword, "password");
        var config = new AnalysisConfig();
        config.ScannerOptsSettings.Add(new Property("some.property", "value"));
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another");

        var result = ExecuteJavaRunnerIgnoringAsserts(config, userArgs, logger, "exe file path", "properties file path", mockRunner);

        result.Should().BeTrue();
        CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another -Dsome.property=value -Djavax.net.ssl.trustStorePassword=\"password\"", mockRunner);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void SonarScanner_ScannerOptsSettingsAndTruststorePasswordSonarScannerOptsNotEmpty_ShouldBeInEnv()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var userArgs = new ListPropertiesProvider();
        userArgs.AddProperty(SonarProperties.TruststorePassword, "password");
        var config = new AnalysisConfig();
        config.ScannerOptsSettings.Add(new Property("some.property", "value"));
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Dsonar.anything.config=existing");

        var result = ExecuteJavaRunnerIgnoringAsserts(config, userArgs, logger, "exe file path", "properties file path", mockRunner);

        result.Should().BeTrue();
        CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Dsonar.anything.config=existing -Dsome.property=value -Djavax.net.ssl.trustStorePassword=\"password\"", mockRunner);
    }

    [TestMethod]
    public void SonarScanner_NothingSupplied_ScanAllShouldBeSet()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig();
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "exe file path", "properties file path", mockRunner);

        result.Should().BeTrue();
        mockRunner.SuppliedArguments.CmdLineArgs.Should().ContainSingle(x => x == "-Dsonar.scanAllFiles=true");
    }

    [TestMethod]
    public void WrapperError_Success_NoStdErr() =>
        TestWrapperErrorHandling(executeResult: true, addMessageToStdErr: false, expectedOutcome: true);

    [TestMethod]
    [WorkItem(202)]
    public void WrapperError_Success_StdErr() =>
        TestWrapperErrorHandling(executeResult: true, addMessageToStdErr: true, expectedOutcome: true);

    [TestMethod]
    public void WrapperError_Fail_NoStdErr() =>
        TestWrapperErrorHandling(executeResult: false, addMessageToStdErr: false, expectedOutcome: false);

    [TestMethod]
    public void WrapperError_Fail_StdErr() =>
        TestWrapperErrorHandling(executeResult: false, addMessageToStdErr: true, expectedOutcome: false);

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void FindScannerExe_ReturnsScannerCliBat()
    {
        var scannerCliScriptPath = new SonarScannerWrapper(new TestLogger(), new OperatingSystemProvider(Substitute.For<IFileWrapper>(), new TestLogger())).FindScannerExe();

        scannerCliScriptPath.Should().EndWithEquivalentOf(@"\bin\sonar-scanner.bat");
    }

    [TestMethod]
    public void FindScannerExe_WhenNonWindows_ReturnsNoExtension()
    {
        var scannerCliScriptPath = new SonarScannerWrapper(new TestLogger(), new UnixTestOperatingSystemProvider()).FindScannerExe();

        Path.GetExtension(scannerCliScriptPath).Should().BeNullOrEmpty();
    }

    private static bool ExecuteJavaRunnerIgnoringAsserts(AnalysisConfig config,
        IAnalysisPropertyProvider userCmdLineArguments,
        ILogger logger,
        string exeFileName,
        string propertiesFileName,
        IProcessRunner runner,
        IOperatingSystemProvider osProvider = null)
    {
        using (new AssertIgnoreScope())
        {
            var wrapper = new SonarScannerWrapper(logger, osProvider ?? new OperatingSystemProvider(Substitute.For<IFileWrapper>(), logger));
            return wrapper.ExecuteJavaRunner(config, userCmdLineArguments, exeFileName, propertiesFileName, runner);
        }
    }

    private static void TestWrapperErrorHandling(bool executeResult, bool addMessageToStdErr, bool expectedOutcome)
    {
        // Arrange
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult);

        var config = new AnalysisConfig { SonarScannerWorkingDirectory = "C:\\working" };

        if (addMessageToStdErr)
        {
            logger.LogError("Dummy error");
        }

        var success = ExecuteJavaRunnerIgnoringAsserts(config, EmptyPropertyProvider.Instance, logger, "c:\\bar.exe", "c:\\props.xml", mockRunner);

        VerifyProcessRunOutcome(mockRunner, logger, "C:\\working", success, expectedOutcome);
    }

    private static void VerifyProcessRunOutcome(MockProcessRunner mockRunner, TestLogger testLogger, string expectedWorkingDir, bool actualOutcome, bool expectedOutcome)
    {
        actualOutcome.Should().Be(expectedOutcome);

        mockRunner.SuppliedArguments.WorkingDirectory.Should().Be(expectedWorkingDir);

        if (actualOutcome)
        {
            // Errors can still be logged when the process completes successfully, so
            // we don't check the error log in this case
            testLogger.AssertInfoMessageExists(Resources.MSG_SonarScannerCompleted);
        }
        else
        {
            testLogger.AssertErrorsLogged();
            testLogger.AssertErrorLogged(Resources.ERR_SonarScannerExecutionFailed);
        }
    }

    /// <summary>
    /// Checks that the argument exists, and returns the start position of the argument in the list of
    /// concatenated arguments so we can check that the arguments are passed in the correct order.
    /// </summary>
    private static int CheckArgExists(string expectedArg, MockProcessRunner mockRunner)
    {
        var allArgs = string.Join(" ", mockRunner.SuppliedArguments.CmdLineArgs);
        var index = allArgs.IndexOf(expectedArg, StringComparison.Ordinal);
        index.Should().BeGreaterThan(-1, "Expected argument was not found. Arg: '{0}', all args: '{1}'", expectedArg, allArgs);
        return index;
    }

    private static void CheckStandardArgsPassed(MockProcessRunner mockRunner, string expectedPropertiesFilePath) =>
        CheckArgExists("-Dproject.settings=" + expectedPropertiesFilePath, mockRunner); // should always be passing the properties file

    private static void CheckArgDoesNotExist(string argToCheck, MockProcessRunner mockRunner)
    {
        var allArgs = mockRunner.SuppliedArguments.EscapedArguments;
        var index = allArgs.IndexOf(argToCheck, StringComparison.Ordinal);
        index.Should().Be(-1, "Not expecting to find the argument. Arg: '{0}', all args: '{1}'", argToCheck, allArgs);
    }

    private static void CheckEnvVarExists(string varName, string expectedValue, MockProcessRunner mockRunner) =>
        mockRunner.SuppliedArguments.EnvironmentVariables.Should().ContainKey(varName)
            .WhoseValue.Should().Be(expectedValue);

    private static string SurroundByQuotes(string value) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"\"{value}\""
            : value;

    private sealed class UnixTestOperatingSystemProvider : IOperatingSystemProvider
    {
        public PlatformOS OperatingSystem() => PlatformOS.Linux;

        public string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option) => throw new NotSupportedException();

        public bool DirectoryExists(string path) => throw new NotSupportedException();

        public bool IsUnix() => throw new NotImplementedException();
    }
}
