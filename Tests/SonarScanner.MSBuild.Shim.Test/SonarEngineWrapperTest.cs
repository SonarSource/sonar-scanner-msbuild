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

        context.Runtime.Logger.AssertErrorLogged("The scanner engine did not complete successfully");
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
        context.Runtime.Logger.AssertInfoLogs(
            "The scanner engine has finished successfully",
            $"Using Java found in Analysis Config: {context.ResolvedJavaExe}");
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void FindJavaExe_ConfiguredPath_DoesNotExist(bool isUnix)
    {
        var context = new Context(isUnix);
        context.Runtime.File.Exists(context.ResolvedJavaExe).Returns(false);

        context.Execute().Should().BeTrue();

        context.Runner.SuppliedArguments.ExeName.Should().Be(context.JavaFileName);
        context.Runtime.Logger.AssertInfoLogs(
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
        context.Runtime.Logger.AssertInfoLogs(
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
        context.Runtime.Logger.AssertInfoLogs(
            $"Could not find Java in Analysis Config: {context.ResolvedJavaExe}",
            $"Found 'JAVA_HOME': {context.JavaHome}",
            $"Could not find Java in JAVA_HOME: {context.JavaHomeExePath}",
            $"Could not find Java, falling back to using PATH: {context.JavaFileName}");
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

        public Context(bool isUnix = false, bool processSucceeds = true)
        {
            Runner = new MockProcessRunner(processSucceeds);
            Runtime.OperatingSystem.IsUnix().Returns(isUnix);
            Engine = new SonarEngineWrapper(Runtime, Runner);
            Runtime.File.Exists(ResolvedJavaExe).Returns(true);
        }

        public bool Execute() =>
            Engine.Execute(
                new AnalysisConfig { JavaExePath = ResolvedJavaExe, EngineJarPath = "engine.jar" },
                SampleInput);
    }
}
