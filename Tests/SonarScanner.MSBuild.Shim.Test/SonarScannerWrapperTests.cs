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
        using var scope = new EnvironmentVariableScope().SetVariable(EnvironmentVariables.SonarScannerHomeVariableName, null);
        var wrapper = new SonarScannerWrapperTestRunner();

        var result = new SonarScannerWrapperTestRunner().ExecuteJavaRunnerIgnoringAsserts();

        result.VerifyProcessRunOutcome("C:\\working\\dir", true);
        result.Logger.AssertMessageNotLogged(Resources.MSG_SonarScannerHomeIsSet);
    }

    [TestMethod]
    public void SonarScannerHome_MessageLoggedIfAlreadySet()
    {
        using var scope = new EnvironmentVariableScope().SetVariable(EnvironmentVariables.SonarScannerHomeVariableName, "some_path");
        var wrapper = new SonarScannerWrapperTestRunner();

        var result = new SonarScannerWrapperTestRunner().ExecuteJavaRunnerIgnoringAsserts();

        result.VerifyProcessRunOutcome("C:\\working\\dir", true);
        result.Logger.AssertInfoMessageExists(Resources.MSG_SonarScannerHomeIsSet);
    }

    [TestMethod]
    public void SonarScanner_StandardAdditionalArgumentsPassed()
    {
        var wrapper = new SonarScannerWrapperTestRunner();
        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

new SonarScannerWrapperTestRunner().ExecuteJavaRunnerIgnoringAsserts().VerifyProcessRunOutcome("C:\\working\\dir", true)
    }

    [TestMethod]
    public void SonarScanner_CmdLineArgsArePassedThroughToTheWrapperAndAppearFirst()
    {
        var wrapper = new SonarScannerWrapperTestRunner()
        {
            Config = { SonarScannerWorkingDirectory = "D:\\dummyWorkingDirectory" },
            UserCmdLineArguments =
            {
                { "sonar.login", "me" },
                { "sonar.password", "my.pwd" },
                { "sonar.token", "token" }
            },
            ExeFileName = "c:\\dummy.exe",
            PropertiesFileName = "c:\\foo.properties",
        };
        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

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
            "-Dxxx=yyy",
            "-Dsonar.password=cmdline.password",                          // sensitive value from cmd line: overrides file value
            "-Dsonar.clientcert.password=file.clientCertificatePassword", // sensitive value from file
            "-Dsonar.login=file.username",
            "-Dsonar.token=file.token",
            "-Dproject.settings=c:\\foo.props",
            $"--from=ScannerMSBuild/{Utilities.ScannerVersion}",
            "--debug",
            "-Dsonar.scanAllFiles=true");

        var clientCertPwdIndex = result.CheckArgExists("-Dsonar.clientcert.password=file.clientCertificatePassword"); // sensitive value from file
        var userPwdIndex = result.CheckArgExists("-Dsonar.password=cmdline.password"); // sensitive value from cmd line: overrides file value

        var propertiesFileIndex = result.CheckArgExists(SonarScannerWrapper.ProjectSettingsFileArgName);

        propertiesFileIndex.Should().BeGreaterThan(clientCertPwdIndex, "User arguments should appear first");
        propertiesFileIndex.Should().BeGreaterThan(userPwdIndex, "User arguments should appear first");
    }

    [TestMethod]
    public void SonarScanner_NoUserSpecifiedEnvVars_SONARSCANNEROPTSIsNotPassed()
    {
        var wrapper = new SonarScannerWrapperTestRunner();
        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.VerifyProcessRunOutcome("C:\\working\\dir", true);

        result.SuppliedArguments.EnvironmentVariables.Should().ContainSingle();

        // #656: Check that the JVM size is not set by default
        // https://github.com/SonarSource/sonar-scanner-msbuild/issues/656
        wrapper.Logger.InfoMessages.Should().NotContain(x => x.Contains("SONAR_SCANNER_OPTS"));
    }

    [TestMethod]
    public void SonarScanner_UserSpecifiedEnvVars_OnlySONARSCANNEROPTSIsPassed()
    {
        var wrapper = new SonarScannerWrapperTestRunner();
        using var scope = new EnvironmentVariableScope();
        // the SONAR_SCANNER_OPTS variable should be passed through explicitly,
        // but not other variables
        scope.SetVariable("Foo", "xxx");
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Xmx2048m");
        scope.SetVariable("Bar", "yyy");

        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.VerifyProcessRunOutcome("C:\\working\\dir", true);
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Xmx2048m -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("changeit")}");
        result.SuppliedArguments.EnvironmentVariables.Should().ContainSingle();
        result.Logger.InfoMessages.Should().Contain("Using the supplied value for SONAR_SCANNER_OPTS. Value: -Xmx2048m");
    }

    [TestMethod]
    public void SonarScanner_TrustStorePasswordInScannerOptsEnd_ShouldBeRedacted()
    {
        var wrapper = new SonarScannerWrapperTestRunner();
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Xmx2048m -Djavax.net.ssl.trustStorePassword=\"changeit\"");

        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();
        result.VerifyProcessRunOutcome("C:\\working\\dir", true);
        result.SuppliedArguments.EnvironmentVariables.Should().ContainSingle();
        result.Logger.InfoMessages.Should().Contain(x => x.Contains("SONAR_SCANNER_OPTS"));
        result.Logger.InfoMessages.Should().Contain(x => x.Contains("-Xmx2048m"));
        result.Logger.InfoMessages.Should().Contain(x => x.Contains("-D<sensitive data removed>"));
        result.Logger.InfoMessages.Should().NotContain(x => x.Contains("-Djavax.net.ssl.trustStorePassword=\"changeit\""));
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [DataTestMethod]
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
    [DataTestMethod]
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

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("\t")]
    public void SonarScanner_WhenJavaExePathIsNullOrWhitespace(string path)
    {
        using var scope = new EnvironmentVariableScope();
        var wrapper = new SonarScannerWrapperTestRunner
        {
            Config = { JavaExePath = path },
        };
        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();
        result.Success.Should().BeTrue();
        result.Logger.DebugMessages.Should().BeEmpty();
        result.Logger.Warnings.Should().BeEmpty();
        result.Logger.Errors.Should().BeEmpty();
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
        using var scope = new EnvironmentVariableScope();
        var wrapper = new SonarScannerWrapperTestRunner { Config = { JavaExePath = path } };
        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.Logger.Warnings.Single().Should().StartWith($"Setting the JAVA_HOME for the scanner cli failed. `sonar.scanner.javaExePath` is `{path}`. {errorMessage}");
        result.Logger.DebugMessages.Should().BeEmpty();
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
        var wrapper = new SonarScannerWrapperTestRunner
        {
            Config = { ScannerOptsSettings = { new Property("some.property", "value") } },
            UserCmdLineArguments = { { SonarProperties.TruststorePassword, "password" } },
        };
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Dsome.property=value -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("password")}");
    }

    [TestMethod]
    public void SonarScanner_TruststorePasswordLinux_ShouldBeInEnv()
    {
        var osProvider = Substitute.For<IOperatingSystemProvider>();
        osProvider.IsUnix().Returns(true);
        var wrapper = new SonarScannerWrapperTestRunner
        {
            Config = { ScannerOptsSettings = { new Property("some.property", "value") } },
            UserCmdLineArguments = { { SonarProperties.TruststorePassword, "password" } },
            OsProvider = osProvider,
        };
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Dsome.property=value -Djavax.net.ssl.trustStorePassword=password");
    }

    [TestMethod]
    public void SonarScanner_CmdTruststorePasswordAndInEnv_CmdShouldBeLatest()
    {
        var wrapper = new SonarScannerWrapperTestRunner
        {
            UserCmdLineArguments = { { SonarProperties.TruststorePassword, "password" } },
        };
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another");

        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Djavax.net.ssl.trustStorePassword=another -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("password")}");
    }

    [TestMethod]
    public void SonarScanner_NoCmdTruststorePasswordAndInEnv_NoAddition()
    {
        var wrapper = new SonarScannerWrapperTestRunner();
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another");

        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another");
    }

    [DataTestMethod]
    [DataRow("changeit")]
    [DataRow("sonar")]
    public void SonarScanner_NoCmdTruststorePasswordAndProvidedTruststore_UseDefaultPassword(string defaultPassword)
    {
        using var truststoreFile = new TempFile("pfx");
        CertificateBuilder.CreateWebServerCertificate().ToPfx(truststoreFile.FileName, defaultPassword);
        var wrapper = new SonarScannerWrapperTestRunner
        {
            Config = { ScannerOptsSettings = { new Property("javax.net.ssl.trustStore", truststoreFile.FileName) } },
        };
        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Djavax.net.ssl.trustStore={truststoreFile.FileName} -Djavax.net.ssl.trustStorePassword={SurroundByQuotes(defaultPassword)}");
    }

    [TestMethod]
    public void SonarScanner_NoCmdTruststorePasswordAndNotInEnv_UseDefault()
    {
        var wrapper = new SonarScannerWrapperTestRunner();
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("changeit")}");
    }

    [TestMethod]
    public void SonarScanner_CmdTruststorePasswordAndInEnv_ShouldUseCmd()
    {
        var wrapper = new SonarScannerWrapperTestRunner
        {
            Config = { ScannerOptsSettings = { new Property("some.property", "value") } },
            UserCmdLineArguments = { { SonarProperties.TruststorePassword, "password" } },
        };
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStorePassword=another");

        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Djavax.net.ssl.trustStorePassword=another -Dsome.property=value -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("password")}");
    }

    [TestMethod]
    public void SonarScanner_ScannerOptsSettingsAndTruststorePasswordSonarScannerOptsNotEmpty_ShouldBeInEnv()
    {
        var wrapper = new SonarScannerWrapperTestRunner
        {
            Config = { ScannerOptsSettings = { new Property("some.property", "value") } },
            UserCmdLineArguments = { { SonarProperties.TruststorePassword, "password" } },
        };
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", "-Dsonar.anything.config=existing");

        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.CheckEnvVarExists("SONAR_SCANNER_OPTS", $"-Dsonar.anything.config=existing -Dsome.property=value -Djavax.net.ssl.trustStorePassword={QuoteEnvironmentValue("password")}");
    }

    [TestMethod]
    public void SonarScanner_NothingSupplied_ScanAllShouldBeSet()
    {
        var wrapper = new SonarScannerWrapperTestRunner();
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_SCANNER_OPTS", null);

        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();

        result.Success.Should().BeTrue();
        result.SuppliedArguments.CmdLineArgs.Should().ContainSingle(x => x == "-Dsonar.scanAllFiles=true");
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
    public void FindScannerExe_ReturnsScannerCliBat_Windows() =>
        new SonarScannerWrapper(new TestLogger(), new OperatingSystemProvider(Substitute.For<IFileWrapper>(), new TestLogger()))
            .FindScannerExe()
            .Should()
            .EndWith(@"\bin\sonar-scanner.bat");

    [TestCategory(TestCategories.NoWindows)]
    [TestMethod]
    public void FindScannerExe_ReturnsScannerCliBat_Unix() =>
        new SonarScannerWrapper(new TestLogger(), new OperatingSystemProvider(Substitute.For<IFileWrapper>(), new TestLogger()))
            .FindScannerExe()
            .Should()
            .EndWith(@"/bin/sonar-scanner");

    [TestMethod]
    public void FindScannerExe_WhenNonWindows_ReturnsNoExtension()
    {
        var scannerCliScriptPath = new SonarScannerWrapper(new TestLogger(), new UnixTestOperatingSystemProvider()).FindScannerExe();

        Path.GetExtension(scannerCliScriptPath).Should().BeNullOrEmpty();
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
        var wrapper = new SonarScannerWrapperTestRunner
        {
            Config = { JavaExePath = path },
        };

        using var scope = new EnvironmentVariableScope();
        var result = wrapper.ExecuteJavaRunnerIgnoringAsserts();
        result.Success.Should().BeTrue();

        result.CheckEnvVarExists("JAVA_HOME", expected);
        result.Logger.DebugMessages.Should().Contain(x => x.Contains($@"Setting the JAVA_HOME for the scanner cli to {expected}."));
    }

    private static string QuoteEnvironmentValue(string value) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @$"""{value}""" : value;

    private sealed class UnixTestOperatingSystemProvider : IOperatingSystemProvider
    {
        public PlatformOS OperatingSystem() => PlatformOS.Linux;

        public string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option) => throw new NotSupportedException();

        public bool DirectoryExists(string path) => throw new NotSupportedException();

        public bool IsUnix() => throw new NotImplementedException();
    }

    private sealed class SonarScannerWrapperTestRunner
    {
        public AnalysisConfig Config { get; set; } = new() { SonarScannerWorkingDirectory = "C:\\working\\dir" };
        public ListPropertiesProvider UserCmdLineArguments { get; set; } = new();
        public TestLogger Logger { get; } = new();
        public string ExeFileName { get; set; } = "c:\\foo.exe";
        public string PropertiesFileName { get; set; } = "c:\\foo.props";
        public MockProcessRunner Runner { get; set; } = new MockProcessRunner(executeResult: true);
        public IOperatingSystemProvider OsProvider { get; set; }

        public SonarScannerWrapperTestRunner()
        {
            OsProvider = new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Logger);
        }

        public ExecuteJavaRunnerResult ExecuteJavaRunnerIgnoringAsserts()
        {
            using (new AssertIgnoreScope())
            {
                var wrapper = new SonarScannerWrapper(Logger, OsProvider);
                var result = wrapper.ExecuteJavaRunner(Config, UserCmdLineArguments, ExeFileName, PropertiesFileName, Runner);
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
                testRunner.Logger.AssertInfoMessageExists(Resources.MSG_SonarScannerCompleted);
            }
            else
            {
                testRunner.Logger.AssertErrorsLogged();
                testRunner.Logger.AssertErrorLogged(Resources.ERR_SonarScannerExecutionFailed);
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
            var allArgs = testRunner.Runner.SuppliedArguments.CmdLineArgs;
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
