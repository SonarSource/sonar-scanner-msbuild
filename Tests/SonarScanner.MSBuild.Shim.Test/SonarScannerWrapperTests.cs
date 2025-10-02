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
using Combinatorial.MSTest;
using NSubstitute.Extensions;
using TestUtilities.Certificates;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class SonarScannerWrapperTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Execute_WhenConfigIsNull_Throws() =>
        new SonarScannerWrapper(new TestRuntime()).Invoking(x => x.Execute(null, EmptyPropertyProvider.Instance, string.Empty))
            .Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("config");

    [TestMethod]
    public void Execute_WhenUserCmdLineArgumentsIsNull_Throws() =>
        new SonarScannerWrapper(new TestRuntime()).Invoking(x => x.Execute(new AnalysisConfig(), null, string.Empty))
            .Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("userCmdLineArguments");

    [TestMethod]
    public void Execute_WhenFullPropertiesFilePathIsNull_ReturnsFalse() =>
        new SonarScannerWrapper(new TestRuntime()).Execute(new AnalysisConfig(), EmptyPropertyProvider.Instance, null).Should().BeFalse();

    [TestMethod]
    [CombinatorialData]
    public void Execute_Success_ReturnTrue(PlatformOS os)
    {
        var runtime = new TestRuntime();
        runtime.ConfigureOS(os);
        runtime.File.Exists(Arg.Is<string>(x => x == "/SonarScannerCli/sonar-scanner" || x == "/SonarScannerCli/sonar-scanner.bat")).Returns(true);
        var testSubject = Substitute.ForPartsOf<SonarScannerWrapper>(runtime);
        testSubject
            .Configure()
            .ExecuteJavaRunner(Arg.Any<AnalysisConfig>(), Arg.Any<IAnalysisPropertyProvider>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProcessRunner>())
            .Returns(true);
        testSubject.Execute(new AnalysisConfig { SonarScannerCliPath = "/SonarScannerCli/sonar-scanner" }, EmptyPropertyProvider.Instance, "some/path").Should().BeTrue();
    }

    [TestMethod]
    public void Execute_ScannerCliNotSpecified_ReturnsFalse()
    {
        var runtime = new TestRuntime();
        var testSubject = new SonarScannerWrapper(runtime);
        testSubject.Execute(new AnalysisConfig { SonarScannerCliPath = null }, EmptyPropertyProvider.Instance, "some/path").Should().BeFalse();
        runtime.Logger.Should().HaveErrorOnce("The SonarScanner CLI is needed to finish the analysis, but could not be found. The path to the SonarScanner CLI wasn't set. "
            + "Please specify /d:sonar.scanner.useSonarScannerCLI=true in the begin step.");
    }

    [TestMethod]
    public void Execute_ScannerCliNotFound_ReturnsFalse_Windows()
    {
        var runtime = new TestRuntime();
        runtime.ConfigureOS(PlatformOS.Windows);
        runtime.File.Exists(@"c:\SonarScannerCli\sonar-scanner.bat").Returns(false);
        var testSubject = new SonarScannerWrapper(runtime);
        testSubject.Execute(new AnalysisConfig { SonarScannerCliPath = @"c:\SonarScannerCli\sonar-scanner" }, EmptyPropertyProvider.Instance, "some/path").Should().BeFalse();
        runtime.Logger.Should().HaveErrorOnce("The SonarScanner CLI is needed to finish the analysis, but could not be found. "
            + @"The path 'c:\SonarScannerCli\sonar-scanner.bat' to the SonarScanner CLI is invalid. The file does not exists.");
    }

    [TestMethod]
    [DataRow(PlatformOS.Linux)]
    [DataRow(PlatformOS.MacOSX)]
    [DataRow(PlatformOS.Alpine)]
    public void Execute_ReturnsFalse_ScannerCliNotFound_Unix(PlatformOS os)
    {
        var runtime = new TestRuntime();
        runtime.ConfigureOS(os);
        runtime.File.Exists("/SonarScannerCli/sonar-scanner").Returns(false);
        var testSubject = new SonarScannerWrapper(runtime);
        testSubject.Execute(new AnalysisConfig { SonarScannerCliPath = "/SonarScannerCli/sonar-scanner" }, EmptyPropertyProvider.Instance, "some/path").Should().BeFalse();
        runtime.Logger.Should().HaveErrorOnce("The SonarScanner CLI is needed to finish the analysis, but could not be found. "
            + "The path '/SonarScannerCli/sonar-scanner' to the SonarScanner CLI is invalid. The file does not exists.");
    }

    [TestMethod]
    public void SonarScannerHome_NoMessageIfNotAlreadySet()
    {
        using var scope = new EnvironmentVariableScope().SetVariable(EnvironmentVariables.SonarScannerHomeVariableName, null);

        var result = new SonarScannerWrapperTestRunner().ExecuteJavaRunnerIgnoringAsserts();

        result.VerifyProcessRunOutcome("C:\\working\\dir", true);
        result.Logger.Should().NotHaveInfo(Resources.MSG_SonarScannerHomeIsSet);
    }

    [TestMethod]
    public void SonarScannerHome_MessageLoggedIfAlreadySet()
    {
        using var scope = new EnvironmentVariableScope().SetVariable(EnvironmentVariables.SonarScannerHomeVariableName, "some_path");

        var result = new SonarScannerWrapperTestRunner().ExecuteJavaRunnerIgnoringAsserts();

        result.VerifyProcessRunOutcome("C:\\working\\dir", true);
        result.Logger.Should().HaveInfos(Resources.MSG_SonarScannerHomeIsSet);
    }

    [TestMethod]
    public void SonarScanner_StandardAdditionalArgumentsPassed() =>
        new SonarScannerWrapperTestRunner()
            .ExecuteJavaRunnerIgnoringAsserts()
            .VerifyProcessRunOutcome("C:\\working\\dir", true);

    [TestMethod]
    public void SonarScanner_CmdLineArgsArePassedThroughToTheWrapperAndAppearFirst()
    {
        var result = new SonarScannerWrapperTestRunner()
        {
            Config = { SonarScannerWorkingDirectory = "D:\\dummyWorkingDirectory" },
            UserCmdLineArguments =
            {
                { "sonar.login", "me" },
                { "sonar.password", "my.pwd" },
                { "sonar.token", "token" }
            },
            PropertiesFileName = "c:\\foo.properties",
        }.ExecuteJavaRunnerIgnoringAsserts();

        result.VerifyProcessRunOutcome("D:\\dummyWorkingDirectory", true);
        result.CheckStandardArgsPassed("c:\\foo.properties");
        var loginIndex = result.CheckArgExists("-Dsonar.login=me");
        var passwordIndex = result.CheckArgExists("-Dsonar.password=my.pwd");
        var tokenIndex = result.CheckArgExists("-Dsonar.token=token");
        var propertiesFileIndex = result.CheckArgExists(SonarScannerWrapper.ProjectSettingsFileArgName);
        propertiesFileIndex.Should().BeGreaterThan(loginIndex, "User arguments should appear first");
        propertiesFileIndex.Should().BeGreaterThan(passwordIndex, "User arguments should appear first");
        propertiesFileIndex.Should().BeGreaterThan(tokenIndex, "User arguments should appear first");
    }

    [TestMethod]
    public void SonarScanner_SensitiveArgsPassedOnCommandLine()
    {
        // Check that sensitive arguments from the config are passed on the command line

        // Create a config file containing sensitive arguments
        var fileSettings = new AnalysisProperties
        {
            // Sensitive values are not expected to be in this file. We test what would happen if. See SonarProperties.SensitivePropertyKeys
            new(SonarProperties.ClientCertPassword, "file.clientCertificatePassword"), // Sensitive
            new(SonarProperties.SonarPassword, "file.password"),                       // Sensitive
            new(SonarProperties.SonarUserName, "file.username"),                       // Sensitive
            new(SonarProperties.SonarToken, "file.token"),                             // Sensitive
            new("file.not.sensitive.key", "not sensitive value")
        };

        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var settingsFilePath = Path.Combine(testDir, "fileSettings.txt");
        fileSettings.Save(settingsFilePath);

        var wrapper = new SonarScannerWrapperTestRunner()
        {
            Config = { SonarScannerWorkingDirectory = testDir },
            UserCmdLineArguments =
            {
                { "xxx", "yyy" },
                { "sonar.password", "cmdline.password" },
            },
        };
        wrapper.Config.SetSettingsFilePath(settingsFilePath);

        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.VerifyProcessRunOutcome(testDir, true);

        result.CheckStandardArgsPassed("c:\\foo.props");

        // Non-sensitive values from the file should not be passed on the command line
        result.CheckArgDoesNotExist("file.not.sensitive.key");
        result.SuppliedArguments.CmdLineArgs.Should().BeEquivalentTo(
            new ProcessRunnerArguments.Argument[]
            {
                new("-Dxxx=yyy"),
                new("-Dsonar.password=cmdline.password"),                          // sensitive value from cmd line: overrides file value
                new("-Dsonar.clientcert.password=file.clientCertificatePassword"), // sensitive value from file
                new("-Dsonar.login=file.username"),
                new("-Dsonar.token=file.token"),
                new("-Dproject.settings=c:\\foo.props"),
                new($"--from=ScannerMSBuild/{Utilities.ScannerVersion}"),
                new("--debug"),
                new("-Dsonar.scanAllFiles=true")
            });

        var clientCertPwdIndex = result.CheckArgExists("-Dsonar.clientcert.password=file.clientCertificatePassword"); // sensitive value from file
        var userPwdIndex = result.CheckArgExists("-Dsonar.password=cmdline.password"); // sensitive value from cmd line: overrides file value

        var propertiesFileIndex = result.CheckArgExists(SonarScannerWrapper.ProjectSettingsFileArgName);

        propertiesFileIndex.Should().BeGreaterThan(clientCertPwdIndex, "User arguments should appear first");
        propertiesFileIndex.Should().BeGreaterThan(userPwdIndex, "User arguments should appear first");
    }

    [TestMethod]
    public void SonarScanner_NoUserSpecifiedEnvVars_SONARSCANNEROPTSIsNotPassed()
    {
        var result = new SonarScannerWrapperTestRunner().ExecuteJavaRunnerIgnoringAsserts();

        result.VerifyProcessRunOutcome("C:\\working\\dir", true);

        result.SuppliedArguments.EnvironmentVariables.Should().ContainSingle();

        // #656: Check that the JVM size is not set by default
        // https://github.com/SonarSource/sonar-scanner-msbuild/issues/656
        new SonarScannerWrapperTestRunner().Logger.InfoMessages.Should().NotContain(x => x.Contains("SONAR_SCANNER_OPTS"));
    }

    [TestMethod]
    public void SonarScanner_UserSpecifiedEnvVars_OnlySONARSCANNEROPTSIsPassed()
    {
        using var scope = new EnvironmentVariableScope();
        // the SONAR_SCANNER_OPTS variable should be passed through explicitly,
        // but not other variables
        scope.SetVariable("Foo", "xxx");
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Xmx2048m");
        scope.SetVariable("Bar", "yyy");

        var result = new SonarScannerWrapperTestRunner().ExecuteJavaRunnerIgnoringAsserts();

        result.VerifyProcessRunOutcome("C:\\working\\dir", true);
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Xmx2048m -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("changeit")}");
        result.SuppliedArguments.EnvironmentVariables.Should().ContainSingle();
        result.Logger.InfoMessages.Should().Contain("Using the supplied value for SONAR_SCANNER_OPTS. Value: -Xmx2048m");
    }

    [TestMethod]
    public void SonarScanner_TrustStorePasswordInScannerOptsEnd_ShouldBeRedacted()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Xmx2048m -Djavax.net.ssl.trustStorePassword=\"changeit\"");

        var result = new SonarScannerWrapperTestRunner().ExecuteJavaRunnerIgnoringAsserts();
        result.VerifyProcessRunOutcome("C:\\working\\dir", true);
        result.SuppliedArguments.EnvironmentVariables.Should().ContainSingle();
        result.Logger.InfoMessages.Should().Contain(x => x.Contains("SONAR_SCANNER_OPTS"));
        result.Logger.InfoMessages.Should().Contain(x => x.Contains("-Xmx2048m"));
        result.Logger.InfoMessages.Should().Contain(x => x.Contains("-D<sensitive data removed>"));
        result.Logger.InfoMessages.Should().NotContain(x => x.Contains("-Djavax.net.ssl.trustStorePassword=\"changeit\""));
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    [DataRow(@"C:\Program Files\Java\jdk-17\bin\java.exe", @"C:\Program Files\Java\jdk-17")]
    [DataRow(@"C:\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\"
             + @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\"
             + @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\"
             + @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\"
             + @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\"
             + @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\"
             + @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\bin\java.exe",
             @"C:\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\"
             + @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\"
             + @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\"
             + @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\"
             + @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\"
             + @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\very\long\path\"
             + @"very\long\path\very\long\path\very\long\path\very\long\path\very\long\path")]
    public void SonarScanner_WhenJavaExePathIsSet_JavaHomeIsSet_Windows(string path, string expected) =>
        SonarScanner_WhenJavaExePathIsSet_JavaHomeIsSet(path, expected);

    [TestCategory(TestCategories.NoWindows)]
    [TestMethod]
    [DataRow(@"/usr/bin/java", @"/usr")] // e.g. a symbolic link to /etc/alternatives/java which is a symlink to the actual Java executable /usr/lib/jvm/java-21-openjdk-amd64/bin/java
                                         // We assume the symbolic links are already resolved here.
    [DataRow(@"/usr/lib/jvm/java-21-openjdk-amd64/bin/java", @"/usr/lib/jvm/java-21-openjdk-amd64")]
    [DataRow(@"/very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/"
             + @"very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/"
             + @"very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/"
             + @"very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/"
             + @"very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/"
             + @"very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/"
             + @"very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/bin/java",
             @"/very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/"
             + @"very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/"
             + @"very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/"
             + @"very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/"
             + @"very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/"
             + @"very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/very/long/path/"
             + @"very/long/path/very/long/path/very/long/path/very/long/path/very/long/path")]
    public void SonarScanner_WhenJavaExePathIsSet_JavaHomeIsSet_Unix(string path, string expected) =>
        SonarScanner_WhenJavaExePathIsSet_JavaHomeIsSet(path, expected);

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("\t")]
    public void SonarScanner_WhenJavaExePathIsNullOrWhitespace(string path)
    {
        using var scope = new EnvironmentVariableScope();
        var result = new SonarScannerWrapperTestRunner
        {
            Config = { JavaExePath = path },
        }.ExecuteJavaRunnerIgnoringAsserts();
        result.Success.Should().BeTrue();
        result.Logger.Should().HaveNoDebugs();
        result.Logger.Warnings.Should().BeEmpty();
        result.Logger.Errors.Should().BeEmpty();
    }

    [TestMethod]
#if NETFRAMEWORK
    [DataRow("java.exe", "Path cannot be the empty string or all whitespace.")]
#else
    [DataRow("java.exe", "The value cannot be an empty string.")]
#endif
    [DataRow("/", "Value cannot be null.")]
    public void SonarScanner_WhenSettingJavaHomePathFails_AWarningIsLogged(string path, string errorMessage)
    {
        using var scope = new EnvironmentVariableScope();
        var wrapper = new SonarScannerWrapperTestRunner { Config = { JavaExePath = path } };
        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.Logger.Warnings.Single().Should().StartWith($"Setting the JAVA_HOME for the scanner cli failed. `sonar.scanner.javaExePath` is `{path}`. {errorMessage}");
        result.Logger.Should().HaveNoDebugs();
    }

    [TestMethod]
    public void SonarScanner_ScannerOptsSettingSonarScannerOptsEmpty()
    {
        using var scope = new EnvironmentVariableScope();
        var wrapper = new SonarScannerWrapperTestRunner { Config = { ScannerOptsSettings = { new Property("some.property", "value") } } };
        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Dsome.property=value -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("changeit")}");
    }

    [TestMethod]
    public void SonarScanner_ScannerOptsSettingSonarScannerOptsEmpty_Multiple()
    {
        var wrapper = new SonarScannerWrapperTestRunner
        {
            Config = { ScannerOptsSettings = { new Property("some.property", "value"), new Property("some.other.property", "\"another value with #%\\/?*\"") } },
        };

        using var scope = new EnvironmentVariableScope();
        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();
        result.Success.Should().BeTrue();

        result.CheckEnvVarExists(
            "SONAR_SCANNER_OPTS",
            $"-Dsome.property=value -Dsome.other.property=\"another value with #%\\/?*\" -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("changeit")}");
    }

    [TestMethod]
    public void SonarScanner_ScannerOptsSettingSonarScannerOptsNotEmpty()
    {
        var wrapper = new SonarScannerWrapperTestRunner { Config = { ScannerOptsSettings = { new Property("some.property", "value") } } };
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Dsonar.anything.config=existing");
        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();
        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Dsonar.anything.config=existing -Dsome.property=value -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("changeit")}");
    }

    [TestMethod]
    public void SonarScanner_ScannerOptsSettingSonarScannerOptsNotEmpty_PropertyAlreadySet()
    {
        var wrapper = new SonarScannerWrapperTestRunner { Config = { ScannerOptsSettings = { new Property("some.property", "new") } } };
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Dsome.property=existing");
        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();
        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Dsome.property=existing -Dsome.property=new -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("changeit")}");
    }

    [TestMethod]
    public void SonarScanner_ScannerOptsSettingSonarScannerOptsEmptyWithTruststorePassword_ShouldBeInEnv()
    {
        var wrapper = new SonarScannerWrapperTestRunner { UserCmdLineArguments = { { SonarProperties.TruststorePassword, "password" } } };
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("password")}");
    }

    [TestMethod]
    public void SonarScanner_TruststorePassword_ShouldBeInEnv()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = new SonarScannerWrapperTestRunner
        {
            Config = { ScannerOptsSettings = { new Property("some.property", "value") } },
            UserCmdLineArguments = { { SonarProperties.TruststorePassword, "password" } },
        }.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Dsome.property=value -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("password")}");
    }

    [TestMethod]
    public void SonarScanner_TruststorePasswordLinux_ShouldBeInEnv()
    {
        var osProvider = Substitute.For<OperatingSystemProvider>(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>());
        osProvider.OperatingSystem().Returns(PlatformOS.Linux);
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = new SonarScannerWrapperTestRunner
        {
            Config = { ScannerOptsSettings = { new Property("some.property", "value") } },
            UserCmdLineArguments = { { SonarProperties.TruststorePassword, "password" } },
            OsProvider = osProvider,
        }.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Dsome.property=value -Djavax.net.ssl.trustStorePassword=password");
    }

    [TestMethod]
    public void SonarScanner_CmdTruststorePasswordAndInEnv_CmdShouldBeLatest()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another");

        var result = new SonarScannerWrapperTestRunner
        {
            UserCmdLineArguments = { { SonarProperties.TruststorePassword, "password" } },
        }.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Djavax.net.ssl.trustStorePassword=another -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("password")}");
    }

    [TestMethod]
    public void SonarScanner_NoCmdTruststorePasswordAndInEnv_NoAddition()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another");

        var result = new SonarScannerWrapperTestRunner().ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another");
    }

    [TestMethod]
    [DataRow("changeit")]
    [DataRow("sonar")]
    public void SonarScanner_NoCmdTruststorePasswordAndProvidedTruststore_UseDefaultPassword(string defaultPassword)
    {
        using var truststoreFile = new TempFile("pfx");
        CertificateBuilder.CreateWebServerCertificate().ToPfx(truststoreFile.FileName, defaultPassword);
        var result = new SonarScannerWrapperTestRunner
        {
            Config = { ScannerOptsSettings = { new Property("javax.net.ssl.trustStore", truststoreFile.FileName) } },
        }.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Djavax.net.ssl.trustStore={truststoreFile.FileName} -Djavax.net.ssl.trustStorePassword={SurroundByQuotes(defaultPassword)}");
    }

    [TestMethod]
    public void SonarScanner_NoCmdTruststorePasswordAndNotInEnv_UseDefault()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = new SonarScannerWrapperTestRunner().ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("changeit")}");
    }

    [TestMethod]
    public void SonarScanner_CmdTruststorePasswordAndInEnv_ShouldUseCmd()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another");

        var result = new SonarScannerWrapperTestRunner
        {
            Config = { ScannerOptsSettings = { new Property("some.property", "value") } },
            UserCmdLineArguments = { { SonarProperties.TruststorePassword, "password" } },
        }.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Djavax.net.ssl.trustStorePassword=another -Dsome.property=value -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("password")}");
    }

    [TestMethod]
    public void SonarScanner_ScannerOptsSettingsAndTruststorePasswordSonarScannerOptsNotEmpty_ShouldBeInEnv()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Dsonar.anything.config=existing");

        var result = new SonarScannerWrapperTestRunner
        {
            Config = { ScannerOptsSettings = { new Property("some.property", "value") } },
            UserCmdLineArguments = { { SonarProperties.TruststorePassword, "password" } },
        }.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Dsonar.anything.config=existing -Dsome.property=value -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("password")}");
    }

    [TestMethod]
    public void SonarScanner_NothingSupplied_ScanAllShouldBeSet()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = new SonarScannerWrapperTestRunner().ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.SuppliedArguments.CmdLineArgs.Should().ContainSingle(x => x.Value == "-Dsonar.scanAllFiles=true");
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

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void FindScannerExe_ReturnsScannerCliBat_Windows()
    {
        var runtime = new TestRuntime();
        runtime.ConfigureOS(PlatformOS.Windows);
        runtime.File.Exists(@"C:\SonarScannerCliCache\sonar-scanner-5.0.2.4997\bin\sonar-scanner.bat").Returns(true);
        new SonarScannerWrapper(runtime)
            .FindScannerExe(new AnalysisConfig { SonarScannerCliPath = @"C:\SonarScannerCliCache\sonar-scanner-5.0.2.4997\bin\sonar-scanner" })
            .Should()
            .Be(@"C:\SonarScannerCliCache\sonar-scanner-5.0.2.4997\bin\sonar-scanner.bat");
    }

    [TestMethod]
    [DataRow(PlatformOS.Linux)]
    [DataRow(PlatformOS.MacOSX)]
    [DataRow(PlatformOS.Alpine)]
    public void FindScannerExe_ReturnsScannerCliBat_Unix(PlatformOS os)
    {
        var runtime = new TestRuntime();
        runtime.ConfigureOS(os);
        runtime.File.Exists("/SonarScannerCliCache/sonar-scanner-5.0.2.4997/bin/sonar-scanner").Returns(true);
        new SonarScannerWrapper(runtime)
            .FindScannerExe(new AnalysisConfig { SonarScannerCliPath = "/SonarScannerCliCache/sonar-scanner-5.0.2.4997/bin/sonar-scanner" })
            .Should()
            .Be("/SonarScannerCliCache/sonar-scanner-5.0.2.4997/bin/sonar-scanner");
    }

    [TestMethod]
    [CombinatorialData]
    public void FindScannerExe_SonarScannerCliPath_NotSet(PlatformOS os)
    {
        var runtime = new TestRuntime();
        runtime.ConfigureOS(os);
        new SonarScannerWrapper(runtime)
            .FindScannerExe(new AnalysisConfig { SonarScannerCliPath = null })
            .Should()
            .BeNull();
        runtime.Logger.Should().HaveErrorOnce("The SonarScanner CLI is needed to finish the analysis, but could not be found. "
            + "The path to the SonarScanner CLI wasn't set. Please specify /d:sonar.scanner.useSonarScannerCLI=true in the begin step.");
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void FindScannerExe_SonarScannerCliPath_NotFound_Windows()
    {
        var runtime = new TestRuntime();
        runtime.ConfigureOS(PlatformOS.Windows);
        runtime.File.Exists(@"C:\SonarScannerCliCache\sonar-scanner-5.0.2.4997\bin\sonar-scanner.bat").Returns(false);
        new SonarScannerWrapper(runtime)
            .FindScannerExe(new AnalysisConfig { SonarScannerCliPath = @"C:\SonarScannerCliCache\sonar-scanner-5.0.2.4997\bin\sonar-scanner" })
            .Should()
            .BeNull();
        runtime.Logger.Should().HaveErrorOnce("The SonarScanner CLI is needed to finish the analysis, but could not be found. "
            + @"The path 'C:\SonarScannerCliCache\sonar-scanner-5.0.2.4997\bin\sonar-scanner.bat' to the SonarScanner CLI is invalid. The file does not exists.");
    }

    [TestMethod]
    [DataRow(PlatformOS.Linux)]
    [DataRow(PlatformOS.MacOSX)]
    [DataRow(PlatformOS.Alpine)]
    public void FindScannerExe_SonarScannerCliPath_NotFound_Unix(PlatformOS os)
    {
        var runtime = new TestRuntime();
        runtime.ConfigureOS(os);
        runtime.File.Exists(@"/SonarScannerCliCache/sonar-scanner-5.0.2.4997/bin/sonar-scanner").Returns(false);
        new SonarScannerWrapper(runtime)
            .FindScannerExe(new AnalysisConfig { SonarScannerCliPath = @"/SonarScannerCliCache/sonar-scanner-5.0.2.4997/bin/sonar-scanner" })
            .Should()
            .BeNull();
        runtime.Logger.Should().HaveErrorOnce("The SonarScanner CLI is needed to finish the analysis, but could not be found. "
            + @"The path '/SonarScannerCliCache/sonar-scanner-5.0.2.4997/bin/sonar-scanner' to the SonarScanner CLI is invalid. The file does not exists.");
    }

    private static void TestWrapperErrorHandling(bool executeResult, bool addMessageToStdErr, bool expectedOutcome)
    {
        var wrapper = new SonarScannerWrapperTestRunner
        {
            Runner = new(executeResult),
        };

        if (addMessageToStdErr)
        {
            wrapper.Logger.LogError("Dummy error");
        }

        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.VerifyProcessRunOutcome("C:\\working\\dir", expectedOutcome);
    }

    private static string SurroundByQuotes(string value) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"\"{value}\""
            : value;

    private static void SonarScanner_WhenJavaExePathIsSet_JavaHomeIsSet(string path, string expected)
    {
        using var scope = new EnvironmentVariableScope();
        var result = new SonarScannerWrapperTestRunner
        {
            Config = { JavaExePath = path },
        }.ExecuteJavaRunnerIgnoringAsserts();
        result.Success.Should().BeTrue();

        result.CheckEnvVarExists("JAVA_HOME", expected);
        result.Logger.DebugMessages.Should().Contain(x => x.Contains($@"Setting the JAVA_HOME for the scanner cli to {expected}."));
    }

    private static string QuoteEnvironmentValue(string value) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @$"""{value}""" : value;

    private sealed class SonarScannerWrapperTestRunner
    {
        private const string ExeFileName = "c:\\foo.exe";

        public AnalysisConfig Config { get; set; } = new() { SonarScannerWorkingDirectory = "C:\\working\\dir" };
        public ListPropertiesProvider UserCmdLineArguments { get; set; } = new();
        public TestLogger Logger { get; } = new();
        public string PropertiesFileName { get; set; } = "c:\\foo.props";
        public MockProcessRunner Runner { get; set; } = new MockProcessRunner(executeResult: true);
        public OperatingSystemProvider OsProvider { get; set; }

        public SonarScannerWrapperTestRunner()
        {
            OsProvider = new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Logger);
        }

        public ExecuteJavaRunnerResult ExecuteJavaRunnerIgnoringAsserts()
        {
            using (new AssertIgnoreScope())
            {
                var result = new SonarScannerWrapper(new TestRuntime { Logger = Logger, OperatingSystem = OsProvider })
                    .ExecuteJavaRunner(Config, UserCmdLineArguments, ExeFileName, PropertiesFileName, Runner);
                return new(this, result);
            }
        }
    }

    private sealed class ExecuteJavaRunnerResult
    {
        private readonly SonarScannerWrapperTestRunner testRunner;

        public bool Success { get; }

        public TestLogger Logger => testRunner.Logger;
        public ProcessRunnerArguments SuppliedArguments => testRunner.Runner.SuppliedArguments;
        public string ExeName => testRunner.Runner.SuppliedArguments.ExeName;

        public ExecuteJavaRunnerResult(SonarScannerWrapperTestRunner sonarScannerWrapperTestRunner, bool success)
        {
            testRunner = sonarScannerWrapperTestRunner;
            Success = success;
        }

        public void VerifyProcessRunOutcome(string expectedWorkingDir, bool expectedOutcome)
        {
            Success.Should().Be(expectedOutcome);
            testRunner.Runner.SuppliedArguments.WorkingDirectory.Should().Be(expectedWorkingDir);
            if (Success)
            {
                // Errors can still be logged when the process completes successfully, so
                // we don't check the error log in this case
                testRunner.Logger.Should().HaveInfos(Resources.MSG_SonarScannerCompleted);
            }
            else
            {
                testRunner.Logger.Should().HaveErrors(Resources.ERR_SonarScannerExecutionFailed);
            }
        }

        public void CheckStandardArgsPassed(string expectedPropertiesFilePath) =>
            CheckArgExists("-Dproject.settings=" + expectedPropertiesFilePath); // should always be passing the properties file

        /// <summary>
        /// Checks that the argument exists, and returns the start position of the argument in the list of
        /// concatenated arguments so we can check that the arguments are passed in the correct order.
        /// </summary>
        public int CheckArgExists(string expectedArg)
        {
            var allArgs = string.Join(" ", testRunner.Runner.SuppliedArguments.CmdLineArgs);
            var index = allArgs.IndexOf(expectedArg, StringComparison.Ordinal);
            index.Should().BeGreaterThan(-1, "Expected argument was not found. Arg: '{0}', all args: '{1}'", expectedArg, allArgs);
            return index;
        }

        public void CheckArgDoesNotExist(string argToCheck)
        {
            var allArgs = testRunner.Runner.SuppliedArguments.CmdLineArgs.Select(x => x.Value);
            allArgs.Should().NotContainMatch(
                $"*{argToCheck}*",
                "Not expecting to find the argument. Arg: '{0}', all args: '{1}'",
                argToCheck,
                allArgs.Aggregate(new StringBuilder(), (sb, x) => sb.AppendFormat("{0} | ", x), x => x.ToString()));
        }

        public void CheckEnvVarExists(string varName, string expectedValue) =>
            testRunner.Runner.SuppliedArguments.EnvironmentVariables.Should().ContainKey(varName).WhoseValue.Should().Be(expectedValue);
    }
}
