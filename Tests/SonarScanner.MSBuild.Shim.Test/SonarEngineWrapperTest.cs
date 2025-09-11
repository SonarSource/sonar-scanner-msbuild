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
    public void Execute_Config_ThrowsArgumentNullException() =>
        new SonarEngineWrapper(new TestRuntime(), Substitute.For<IProcessRunner>()).Invoking(x => x.Execute(null, "{}")).Should().Throw<ArgumentNullException>().WithParameterName("config");

    [TestMethod]
    public void Execute_Failure()
    {
        var context = new Context(processSucceeds: false);

        context.Execute().Should().BeFalse();

        context.Runtime.Should().HaveErrorsLogged("The scanner engine did not complete successfully");
    }

    [TestMethod]
    public void Execute_Success_ConfiguredPathExists()
    {
        var context = new Context();
        context.Execute().Should().BeTrue();

        context.Runner.SuppliedArguments.Should().BeEquivalentTo(new
        {
            ExeName = context.ResolvedJavaExe,
            CmdLineArgs = (string[])["-jar", "engine.jar"],
            StandardInput = SampleInput,
        });
        context.Runtime.Should().HaveInfosLogged(
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
        context.Runtime.Should().HaveInfosLogged(
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
        context.Runtime.Should().HaveInfosLogged(
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
        context.Runtime.Should().HaveInfosLogged(
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
            CmdLineArgs = (string[])["-DJavaParam=Env", "-jar", "engine.jar"],
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
            CmdLineArgs = (string[])["-DJavaParam=Config", "-jar", "engine.jar"],
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
            CmdLineArgs = (string[])["-jar", "engine.jar"],
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
            CmdLineArgs = (string[])["-DJavaParam=Env", "-DJavaParam=Config", "-jar", "engine.jar"],
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
            CmdLineArgs = (string[])["-DJavaParam=Env -DJavaOtherParam=Value -DSomeOtherParam=AnotherValue", "-DJavaParam=Config", "-DSomeParam=SomeValue", "-DOtherParam=OtherValue", "-jar", "engine.jar"],
            StandardInput = SampleInput,
        });
    }

    [TestMethod]
    public void Execute_JavaNotFoundInPathExceptionIsLogged()
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
    public void Execute_OnlyWin32ExceptionIsCaught()
    public void Execute_JavaNotFoundInPathExceptionIsLogged_OtherUncaught(Type exceptionType)
    {
        var context = new Context(processSucceeds: false, exception: new IOException("The system cannot find the file specified"));
        var config = new AnalysisConfig { JavaExePath = null };
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("JAVA_HOME", null);
        FluentActions.Invoking(() => context.Execute(config)).Should().Throw<IOException>().WithMessage("The system cannot find the file specified");
        context.Runtime.Logger.Should().HaveInfos(
            "Could not find Java in Analysis Config: ",
            "'JAVA_HOME' environment variable not set",
            "Could not find Java, falling back to using PATH: java.exe")
            .And.HaveNoErrors();
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
            Runner = new MockProcessRunner(processSucceeds, exception: exception);
            Runtime.OperatingSystem.OperatingSystem().Returns(isUnix ? PlatformOS.Linux : PlatformOS.Windows);
            Engine = new SonarEngineWrapper(Runtime, Runner);
            Runtime.File.Exists(ResolvedJavaExe).Returns(true);
        }

        public bool Execute(AnalysisConfig analysisConfig = null) =>
            Engine.Execute(
                analysisConfig ?? new AnalysisConfig { JavaExePath = ResolvedJavaExe, EngineJarPath = "engine.jar" },
                SampleInput);
    }
}
