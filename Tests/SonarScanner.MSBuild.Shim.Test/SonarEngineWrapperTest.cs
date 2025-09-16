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

using System.ComponentModel;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class SonarEngineWrapperTest
{
    private const string SampleInput = """
        { "scannerProperties":
            [
                {"key": "sonar.scanner.app", "value": "S4NET"}
            ]
        }
        """;

    [TestMethod]
    public void Ctor_Runtime_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => new SonarEngineWrapper(null, Substitute.For<IProcessRunner>())).Should().Throw<ArgumentNullException>().WithParameterName("runtime");

    [TestMethod]
    public void Ctor_ProcessRunner_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => new SonarEngineWrapper(new TestRuntime(), null)).Should().Throw<ArgumentNullException>().WithParameterName("processRunner");

    [TestMethod]
    public void Execute_Config_ThrowsArgumentNullException_Config() =>
        new SonarEngineWrapper(new TestRuntime(), Substitute.For<IProcessRunner>()).Invoking(x => x.Execute(null, "{}", null)).Should().Throw<ArgumentNullException>().WithParameterName("config");

    [TestMethod]
    public void Execute_Config_ThrowsArgumentNullException_UserCmdLineArgs() =>
        new SonarEngineWrapper(new TestRuntime(), Substitute.For<IProcessRunner>()).Invoking(x => x.Execute(new AnalysisConfig(), "{}", null)).Should().Throw<ArgumentNullException>().WithParameterName("userCmdLineArguments");

    [TestMethod]
    public void Execute_Failure()
    {
        var context = new Context(processSucceeds: false);

        context.Execute().Should().BeFalse();

        context.Runtime.Logger.Should().HaveErrors("The scanner engine did not complete successfully");
    }

    [TestMethod]
    public void Execute_Success_ConfiguredPathExists()
    {
        var context = new Context();
        context.Execute().Should().BeTrue();

        context.Runner.SuppliedArguments.Should().BeEquivalentTo(new
        {
            ExeName = context.ResolvedJavaExe,
            CmdLineArgs = new ProcessRunnerArguments.Argument[] { new("-Djavax.net.ssl.trustStorePassword=\"changeit\"", true), "-jar", "engine.jar" },
            StandardInput = SampleInput,
        });
        context.Runtime.Logger.Should().HaveInfos(
            "The scanner engine has finished successfully",
            $"Using Java found in Analysis Config: {context.ResolvedJavaExe}");
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void FindJavaExe_ConfiguredPath_DoesNotExist(bool isUnix)
    {
        using var scope = new EnvironmentVariableScope().SetVariable(EnvironmentVariables.JavaHomeVariableName, null);
        var context = new Context(isUnix);
        context.Runtime.File.Exists(context.ResolvedJavaExe).Returns(false);

        context.Execute().Should().BeTrue();

        context.Runner.SuppliedArguments.ExeName.Should().Be(context.JavaFileName);
        context.Runtime.Logger.Should().HaveInfos(
            $"Could not find Java in Analysis Config: {context.ResolvedJavaExe}",
            "'JAVA_HOME' environment variable not set",
            $"Could not find Java, falling back to using PATH: {context.JavaFileName}");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void FindJavaExe_JavaHomeSet_Exists(bool isUnix)
    {
        var context = new Context(isUnix);
        using var environmentVariableScope = new EnvironmentVariableScope();
        environmentVariableScope.SetVariable(EnvironmentVariables.JavaHomeVariableName, context.JavaHome);
        context.Runtime.File.Exists(context.ResolvedJavaExe).Returns(false);
        context.Runtime.File.Exists(context.JavaHomeExePath).Returns(true);

        context.Execute().Should().BeTrue();

        context.Runner.SuppliedArguments.ExeName.Should().Be(context.JavaHomeExePath);
        context.Runtime.Logger.Should().HaveInfos(
            $"Could not find Java in Analysis Config: {context.ResolvedJavaExe}",
            $"Found 'JAVA_HOME': {context.JavaHome}",
            $"Using Java found in JAVA_HOME: {context.JavaHomeExePath}");
    }

    [TestMethod]
    public void FindJavaExe_JavaHomeSet_DoesNotExist()
    {
        var context = new Context();
        using var environmentVariableScope = new EnvironmentVariableScope();
        environmentVariableScope.SetVariable(EnvironmentVariables.JavaHomeVariableName, context.JavaHome);
        context.Runtime.File.Exists(context.ResolvedJavaExe).Returns(false);
        context.Runtime.File.Exists(context.JavaHomeExePath).Returns(false);

        context.Execute().Should().BeTrue();

        context.Runner.SuppliedArguments.ExeName.Should().Be(context.JavaFileName);
        context.Runtime.Logger.Should().HaveInfos(
            $"Could not find Java in Analysis Config: {context.ResolvedJavaExe}",
            $"Found 'JAVA_HOME': {context.JavaHome}",
            $"Could not find Java in JAVA_HOME: {context.JavaHomeExePath}",
            $"Could not find Java, falling back to using PATH: {context.JavaFileName}");
    }

    [TestMethod]
    public void Execute_ScannerOptsFromEnv_UsedForJavaInvocation()
    {
        using var scope = new EnvironmentVariableScope().SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, "-DJavaParam=Env");
        var context = new Context();

        context.Execute().Should().BeTrue();

        context.Runner.SuppliedArguments.Should().BeEquivalentTo(new
        {
            ExeName = context.ResolvedJavaExe,
            CmdLineArgs = new ProcessRunnerArguments.Argument[] { new("-DJavaParam=Env", true), new("-Djavax.net.ssl.trustStorePassword=\"changeit\"", true), new("-jar"), new("engine.jar") },
            StandardInput = SampleInput,
        });
    }

    [TestMethod]
    public void Execute_ScannerOptsFromConfig_UsedForJavaInvocation()
    {
        var context = new Context();
        var config = new AnalysisConfig
        {
            JavaExePath = context.ResolvedJavaExe,
            EngineJarPath = "engine.jar",
        };
        config.ScannerOptsSettings.Add(new Property("JavaParam", "Config"));

        context.Execute(config).Should().BeTrue();

        context.Runner.SuppliedArguments.Should().BeEquivalentTo(new
        {
            ExeName = context.ResolvedJavaExe,
            CmdLineArgs = new ProcessRunnerArguments.Argument[] { new("-DJavaParam=Config", true), new("-Djavax.net.ssl.trustStorePassword=\"changeit\"", true), "-jar", "engine.jar" },
            StandardInput = SampleInput,
        });
    }

    [TestMethod]
    public void Execute_WorkingDirectoryIsSonarScannerWorkingDirectory()
    {
        var context = new Context();
        var config = new AnalysisConfig
        {
            JavaExePath = context.ResolvedJavaExe,
            EngineJarPath = "engine.jar",
            SonarScannerWorkingDirectory = @"C:\MyWorkingDir"
        };

        context.Execute(config).Should().BeTrue();

        context.Runner.SuppliedArguments.Should().BeEquivalentTo(new
        {
            ExeName = context.ResolvedJavaExe,
            CmdLineArgs = new ProcessRunnerArguments.Argument[] { new("-Djavax.net.ssl.trustStorePassword=\"changeit\"", true), "-jar", "engine.jar" },
            StandardInput = SampleInput,
            WorkingDirectory = @"C:\MyWorkingDir",
        });
    }

    [TestMethod]
    // Java Params that come afterwards overwrite earlier ones
    public void Execute_ScannerOptsFromConfig_ComeAfterScannerOptsFromEnv()
    {
        using var scope = new EnvironmentVariableScope().SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, "-DJavaParam=Env");
        var context = new Context();
        var config = new AnalysisConfig
        {
            JavaExePath = context.ResolvedJavaExe,
            EngineJarPath = "engine.jar",
        };
        config.ScannerOptsSettings.Add(new Property("JavaParam", "Config"));

        context.Execute(config).Should().BeTrue();

        context.Runner.SuppliedArguments.Should().BeEquivalentTo(new
        {
            ExeName = context.ResolvedJavaExe,

            CmdLineArgs = new ProcessRunnerArguments.Argument[] { new("-DJavaParam=Env", true), new("-DJavaParam=Config", true), new("-Djavax.net.ssl.trustStorePassword=\"changeit\"", true), "-jar", "engine.jar" },
            StandardInput = SampleInput,
        });
    }

    [TestMethod]
    // Java Params that come afterwards overwrite earlier ones
    public void Execute_ScannerOpts_MultipleParameters()
    {
        using var scope = new EnvironmentVariableScope().SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, "-DJavaParam=Env -DJavaOtherParam=Value -DSomeOtherParam=AnotherValue");
        var context = new Context();
        var config = new AnalysisConfig
        {
            JavaExePath = context.ResolvedJavaExe,
            EngineJarPath = "engine.jar",
        };
        config.ScannerOptsSettings.Add(new Property("JavaParam", "Config"));
        config.ScannerOptsSettings.Add(new Property("SomeParam", "SomeValue"));
        config.ScannerOptsSettings.Add(new Property("OtherParam", "OtherValue"));

        context.Execute(config).Should().BeTrue();

        context.Runner.SuppliedArguments.Should().BeEquivalentTo(new
        {
            ExeName = context.ResolvedJavaExe,
            CmdLineArgs = new ProcessRunnerArguments.Argument[]
            {
                new("-DJavaParam=Env -DJavaOtherParam=Value -DSomeOtherParam=AnotherValue", true),
                new("-DJavaParam=Config", true),
                new("-DSomeParam=SomeValue", true),
                new("-DOtherParam=OtherValue", true),
                new("-Djavax.net.ssl.trustStorePassword=\"changeit\"", true),
                new("-jar", false),
                new("engine.jar", false)
            },
            StandardInput = SampleInput,
        });
    }

    [TestMethod]
    public void Execute_JavaNotFoundInPath_Win32Exception_Logged()
    {
        var context = new Context(processSucceeds: false, exception: new Win32Exception(2, "The system cannot find the file specified"));
        var config = new AnalysisConfig { JavaExePath = null };
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("JAVA_HOME", null);
        context.Execute(config).Should().BeFalse();
        context.Runtime.Logger.Should().HaveInfos(
            "Could not find Java in Analysis Config: ",
            "'JAVA_HOME' environment variable not set",
            "Could not find Java, falling back to using PATH: java.exe")
            .And.HaveErrors(
            "The scanner engine execution failed with System.ComponentModel.Win32Exception: Error Code = -2147467259. The system cannot find the file specified.");
    }

    [TestMethod]
    public void Execute_JavaNotFoundInPath_PlatformNotSupportedException_Logged()
    {
        var context = new Context(processSucceeds: false, exception: new PlatformNotSupportedException("Some message"));
        var config = new AnalysisConfig { JavaExePath = null };
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("JAVA_HOME", null);
        context.Execute(config).Should().BeFalse();
        context.Runtime.Logger.Should().HaveInfos(
            "Could not find Java in Analysis Config: ",
            "'JAVA_HOME' environment variable not set",
            "Could not find Java, falling back to using PATH: java.exe")
            .And.HaveErrors(
            "The scanner engine execution failed with System.PlatformNotSupportedException: Some message.");
    }

    [TestMethod]
    [DataRow(typeof(InvalidOperationException))] // This exception is an indicator of a bug and should not be caught
    [DataRow(typeof(FileNotFoundException))]     // This exception is only documented for other overloads of Process.Start
    [DataRow(typeof(IOException))]
    public void Execute_JavaNotFoundInPath_UnexpectedException_Thrown(Type exceptionType)
    {
        var context = new Context(processSucceeds: false, exception: (Exception)Activator.CreateInstance(exceptionType, "Some message"));
        var config = new AnalysisConfig { JavaExePath = null };
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("JAVA_HOME", null);
        var thrownException = FluentActions.Invoking(() => context.Execute(config)).Should().Throw<Exception>().Which;
        thrownException.Should().BeOfType(exceptionType);
        thrownException.Message.Should().Be("Some message");
        context.Runtime.Logger.Should().HaveInfos(
            "Could not find Java in Analysis Config: ",
            "'JAVA_HOME' environment variable not set",
            "Could not find Java, falling back to using PATH: java.exe")
            .And.HaveNoErrors();
    }

    [TestMethod]
    public void Execute_TrustStorePasswordUserSuppliedCli_UsedForJavaInvocation()
    {
        var context = new Context();
        var userCmdLineArgs = new ListPropertiesProvider([new(SonarProperties.TruststorePassword, "UserSuppliedPassword")]);

        context.Execute(null, userCmdLineArgs).Should().BeTrue();
        context.Runner.SuppliedArguments.Should().BeEquivalentTo(new
        {
            ExeName = context.ResolvedJavaExe,
            CmdLineArgs = new ProcessRunnerArguments.Argument[] { new("-Djavax.net.ssl.trustStorePassword=\"UserSuppliedPassword\"", true), new("-jar"), new("engine.jar") },
            StandardInput = SampleInput,
        });
    }

    [TestMethod]
    public void Execute_TrustStorePassword_UserSuppliedEnv_UsedForJavaInvocation()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, "-Djavax.net.ssl.trustStorePassword=\"UserSuppliedPassword\"");
        var context = new Context();

        context.Execute().Should().BeTrue();
        context.Runner.SuppliedArguments.Should().BeEquivalentTo(new
        {
            ExeName = context.ResolvedJavaExe,
            CmdLineArgs = new ProcessRunnerArguments.Argument[] { new("-Djavax.net.ssl.trustStorePassword=\"UserSuppliedPassword\"", true), new("-jar"), new("engine.jar") },
            StandardInput = SampleInput,
        });
    }

    [TestMethod]
    // Java Params that come afterwards overwrite earlier ones
    public void Execute_TrustStorePassword_UserSupplied_CliPasswordOverWritesEnvPassword()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, "-Djavax.net.ssl.trustStorePassword=\"EnvPassword\"");

        var userCmdLineArgs = new ListPropertiesProvider([new(SonarProperties.TruststorePassword, "CliPassword")]);
        var context = new Context();

        context.Execute(null, userCmdLineArgs).Should().BeTrue();
        context.Runner.SuppliedArguments.Should().BeEquivalentTo(new
        {
            ExeName = context.ResolvedJavaExe,
            CmdLineArgs = new ProcessRunnerArguments.Argument[]
            {
                new("-Djavax.net.ssl.trustStorePassword=\"EnvPassword\"", true),
                new("-Djavax.net.ssl.trustStorePassword=\"CliPassword\"", true),
                new("-jar"),
                new("engine.jar")
            },
            StandardInput = SampleInput,
        });
    }

    [TestMethod]
    public void Execute_TrustStorePassword_UnixNotWrappedInQuotes()
    {
        var userCmdLineArgs = new ListPropertiesProvider([new(SonarProperties.TruststorePassword, "Password")]);
        var context = new Context(true);

        context.Execute(null, userCmdLineArgs).Should().BeTrue();
        context.Runner.SuppliedArguments.Should().BeEquivalentTo(new
        {
            ExeName = context.ResolvedJavaExe,
            CmdLineArgs = new ProcessRunnerArguments.Argument[] { new("-Djavax.net.ssl.trustStorePassword=Password", true), new("-jar"), new("engine.jar") },
            StandardInput = SampleInput,
        });
    }

    private sealed class Context
    {
        public readonly SonarEngineWrapper Engine;
        public readonly MockProcessRunner Runner;
        public readonly TestRuntime Runtime = new();
        public readonly string ResolvedJavaExe = "resolved-java.exe";
        public readonly string JavaHome = Path.Combine("Java", "Home");

        public string JavaFileName => Runtime.OperatingSystem.IsUnix() ? "java" : "java.exe";
        public string JavaHomeExePath => Path.Combine(JavaHome, "bin", JavaFileName);

        public Context(bool isUnix = false, bool processSucceeds = true, Exception exception = null)
        {
            Runner = new MockProcessRunner(processSucceeds) { Exception = exception };
            Runtime.OperatingSystem.OperatingSystem().Returns(isUnix ? PlatformOS.Linux : PlatformOS.Windows);
            Engine = new SonarEngineWrapper(Runtime, Runner);
            Runtime.File.Exists(ResolvedJavaExe).Returns(true);
        }

        public bool Execute(AnalysisConfig analysisConfig = null, IAnalysisPropertyProvider userCmdLineArgs = null) =>
            Engine.Execute(
                analysisConfig ?? new AnalysisConfig { JavaExePath = ResolvedJavaExe, EngineJarPath = "engine.jar" },
                SampleInput,
                userCmdLineArgs ?? new ListPropertiesProvider());
    }
}
