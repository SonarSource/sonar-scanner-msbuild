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

namespace SonarScanner.MSBuild.PreProcessor.JreResolution.Test;

[TestClass]
public class LocalJreTruststoreResolverTests
{
    private static EnvironmentVariableScope globalEnvScope;

    [ClassInitialize]
    public static void Setup(TestContext context)
    {
        List<string> paths = ["/usr/local/sbin", "/usr/local/bin", "/usr/sbin", "/usr/bin", "/sbin", "/bin"];
        globalEnvScope = new EnvironmentVariableScope();
        globalEnvScope.SetVariable("PATH", string.Join($"{Path.PathSeparator}", paths));
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void Cleanup() =>
        globalEnvScope?.Dispose();

    [TestMethod]
    public void UnixTruststorePath_NullArgs_Throws() =>
        FluentActions.Invoking(() => new LocalJreTruststoreResolver(Substitute.For<IProcessRunner>(), new TestRuntime()).UnixTruststorePath(null))
            .Should().Throw<ArgumentException>()
            .WithParameterName("args");

    [TestMethod]
    public void UnixTruststorePath_SystemPathNull_ShouldBeNull()
    {
        var runtime = new TestRuntime();
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Execute(Arg.Any<ProcessRunnerArguments>()).Returns(new ProcessResult(true, string.Empty, string.Empty));
        var processedArgs = CreateProcessedArgs(runtime);
        var sut = new LocalJreTruststoreResolver(processRunner, runtime);
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", null);
        envScope.SetVariable("PATH", null);

        var result = sut.UnixTruststorePath(processedArgs);

        result.Should().BeNull();
        runtime.Logger.DebugMessages.Should().HaveCount(3);
        AssertDebugLogged(runtime.Logger, "JAVA_HOME environment variable not set. Try to infer Java home from Java executable.");
        AssertDebugLogged(runtime.Logger, "Could not infer bourne shell executable from PATH.");
        AssertDebugLogged(runtime.Logger, "Could not infer Java Home from the java executable.");
    }

    [TestMethod]
    public void UnixTruststorePath_BourneShellNotFound_ShouldBeNull()
    {
        var runtime = new TestRuntime();
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Execute(Arg.Any<ProcessRunnerArguments>()).Returns(new ProcessResult(true, string.Empty, string.Empty));
        var processedArgs = CreateProcessedArgs(runtime);
        var sut = new LocalJreTruststoreResolver(processRunner, runtime);
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", null);

        var result = sut.UnixTruststorePath(processedArgs);

        result.Should().BeNull();
        runtime.Logger.DebugMessages.Should().HaveCount(3);
        AssertDebugLogged(runtime.Logger, "JAVA_HOME environment variable not set. Try to infer Java home from Java executable.");
        AssertDebugLogged(runtime.Logger, "Could not infer bourne shell executable from PATH.");
        AssertDebugLogged(runtime.Logger, "Could not infer Java Home from the java executable.");
    }

    [TestMethod]
    public void UnixTruststorePath_NothingSetNoJava_ShouldBeNull()
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Is<string>(x => ToUnixPath(x) == "/bin/sh")).Returns(true);
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Execute(Arg.Any<ProcessRunnerArguments>()).Returns(new ProcessResult(false, string.Empty, string.Empty));
        var processedArgs = CreateProcessedArgs(runtime);
        var sut = new LocalJreTruststoreResolver(processRunner, runtime);
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", null);

        var result = sut.UnixTruststorePath(processedArgs);

        result.Should().BeNull();
        runtime.Logger.DebugMessages.Should().HaveCount(3);
        AssertDebugLogged(runtime.Logger, "JAVA_HOME environment variable not set. Try to infer Java home from Java executable.");
        AssertDebugLogged(runtime.Logger, "Unable to locate Java executable. Reason: ''.");
        AssertDebugLogged(runtime.Logger, "Could not infer Java Home from the java executable.");
    }

    [TestMethod]
    public void UnixTruststorePath_NothingSetErrorOnReadLink_ShouldBeNull()
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Is<string>(x => ToUnixPath(x) == "/bin/sh")).Returns(true);
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Execute(Arg.Is<ProcessRunnerArguments>(x => x.CmdLineArgs.Contains("command -v java"))).Returns(new ProcessResult(true, "/usr/bin/java", string.Empty));
        var processedArgs = CreateProcessedArgs(runtime);
        var sut = new LocalJreTruststoreResolver(processRunner, runtime);
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", null);

        var result = sut.UnixTruststorePath(processedArgs);

        result.Should().BeNull();
        runtime.Logger.DebugMessages.Should().HaveCount(4);
        AssertDebugLogged(runtime.Logger, "JAVA_HOME environment variable not set. Try to infer Java home from Java executable.");
        AssertDebugLogged(runtime.Logger, "Java executable located at: '/usr/bin/java'.");
        AssertDebugLogged(runtime.Logger, "Unable to follow potential symbolic link of '/usr/bin/java'. Reason: ''");
        AssertDebugLogged(runtime.Logger, "Could not infer Java Home from the java executable.");
    }

    [TestMethod]
    public void UnixTruststorePath_NothingSetJavaHomeDirectoryDoesNotExist_ShouldBeNull()
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Is<string>(x => ToUnixPath(x) == "/bin/sh")).Returns(true);
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Execute(Arg.Is<ProcessRunnerArguments>(x => x.CmdLineArgs.Contains("command -v java") || x.CmdLineArgs.Contains("readlink -f /usr/bin/java")))
            .Returns(new ProcessResult(true, "/usr/bin/java", string.Empty), new ProcessResult(true, "/usr/lib/jvm/java-17-openjdk-amd64/bin/java", string.Empty));
        var logger = new TestLogger();
        var processedArgs = CreateProcessedArgs(runtime);
        var sut = new LocalJreTruststoreResolver(processRunner, runtime);
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", null);

        var result = sut.UnixTruststorePath(processedArgs);

        result.Should().BeNull();
        runtime.Logger.DebugMessages.Should().HaveCount(4);
        AssertDebugLogged(runtime.Logger, "JAVA_HOME environment variable not set. Try to infer Java home from Java executable.");
        AssertDebugLogged(runtime.Logger, "Java executable located at: '/usr/bin/java'.");
        AssertDebugLogged(runtime.Logger, "Java executable symbolic link resolved to: '/usr/lib/jvm/java-17-openjdk-amd64/bin/java'.");
        AssertDebugLogged(runtime.Logger, "Java home '/usr/lib/jvm/java-17-openjdk-amd64' does not exist.");
    }

    [TestMethod]
    [DataRow("/", "")]
    [DataRow("/java", "")]
    public void UnixTruststorePath_NothingSetJavaPathTooShort_ShouldBeNull(string resolvedPath, string expectedHomePath)
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Is<string>(x => ToUnixPath(x) == "/bin/sh")).Returns(true);
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Execute(Arg.Is<ProcessRunnerArguments>(x => x.CmdLineArgs.Contains("command -v java") || x.CmdLineArgs.Contains("readlink -f /usr/bin/java")))
            .Returns(new ProcessResult(true, "/usr/bin/java", string.Empty), new ProcessResult(true, resolvedPath, string.Empty));
        var processedArgs = CreateProcessedArgs(runtime);
        var sut = new LocalJreTruststoreResolver(processRunner, runtime);
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", null);

        var result = sut.UnixTruststorePath(processedArgs);

        result.Should().BeNull();
        runtime.Logger.DebugMessages.Should().HaveCount(4);
        AssertDebugLogged(runtime.Logger, "JAVA_HOME environment variable not set. Try to infer Java home from Java executable.");
        AssertDebugLogged(runtime.Logger, "Java executable located at: '/usr/bin/java'.");
        AssertDebugLogged(runtime.Logger, $"Java executable symbolic link resolved to: '{resolvedPath}'.");
        AssertDebugLogged(runtime.Logger, $"Java home '{expectedHomePath}' does not exist.");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("  ")]
    public void UnixTruststorePath_NothingSetJavaResolvedPathEmpty_ShouldBeNull(string resolvedPath)
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Is<string>(x => ToUnixPath(x) == "/bin/sh")).Returns(true);
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Execute(Arg.Is<ProcessRunnerArguments>(x => x.CmdLineArgs.Contains("command -v java") || x.CmdLineArgs.Contains("readlink -f /usr/bin/java")))
            .Returns(new ProcessResult(true, "/usr/bin/java", string.Empty), new ProcessResult(true, resolvedPath, string.Empty));
        var processedArgs = CreateProcessedArgs(runtime);
        var sut = new LocalJreTruststoreResolver(processRunner, runtime);
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", null);

        var result = sut.UnixTruststorePath(processedArgs);

        result.Should().BeNull();
        runtime.Logger.DebugMessages.Should().HaveCount(4);
        AssertDebugLogged(runtime.Logger, "JAVA_HOME environment variable not set. Try to infer Java home from Java executable.");
        AssertDebugLogged(runtime.Logger, "Java executable located at: '/usr/bin/java'.");
        AssertDebugLogged(runtime.Logger, $"Java executable symbolic link resolved to: '{resolvedPath}'.");
        AssertDebugLogged(runtime.Logger, "Could not infer Java Home from the java executable.");
    }

    [TestMethod]
    public void UnixTruststorePath_NothingSetCacertsDoesNotExist_ShouldBeNull()
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Is<string>(x => ToUnixPath(x) == "/bin/sh")).Returns(true);
        runtime.Directory.Exists(Arg.Any<string>()).Returns(true);
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Execute(Arg.Is<ProcessRunnerArguments>(x => x.CmdLineArgs.Contains("command -v java") || x.CmdLineArgs.Contains("readlink -f /usr/bin/java")))
            .Returns(new ProcessResult(true, "/usr/bin/java", string.Empty), new ProcessResult(true, "/usr/lib/jvm/java-17-openjdk-amd64/bin/java", string.Empty));
        var processedArgs = CreateProcessedArgs(runtime);
        var sut = new LocalJreTruststoreResolver(processRunner, runtime);
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", null);

        var result = sut.UnixTruststorePath(processedArgs);

        result.Should().BeNull();
        runtime.Logger.DebugMessages.Should().HaveCount(4);
        AssertDebugLogged(runtime.Logger, "JAVA_HOME environment variable not set. Try to infer Java home from Java executable.");
        AssertDebugLogged(runtime.Logger, "Java executable located at: '/usr/bin/java'.");
        AssertDebugLogged(runtime.Logger, "Java executable symbolic link resolved to: '/usr/lib/jvm/java-17-openjdk-amd64/bin/java'.");
        AssertDebugLogged(runtime.Logger, "Unable to find Java Keystore file. Lookup path: '/usr/lib/jvm/java-17-openjdk-amd64/lib/security/cacerts'.");
    }

    [TestMethod]
    public void UnixTruststorePath_NothingSetCacertsExist()
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Any<string>()).Returns(true);
        runtime.Directory.Exists(Arg.Any<string>()).Returns(true);
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Execute(Arg.Is<ProcessRunnerArguments>(x => x.CmdLineArgs.Contains("command -v java") || x.CmdLineArgs.Contains("readlink -f /usr/bin/java")))
            .Returns(new ProcessResult(true, "/usr/bin/java", string.Empty), new ProcessResult(true, "/usr/lib/jvm/java-17-openjdk-amd64/bin/java", string.Empty));
        var processedArgs = CreateProcessedArgs(runtime);
        var sut = new LocalJreTruststoreResolver(processRunner, runtime);
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", null);

        var result = sut.UnixTruststorePath(processedArgs);

        AssertPath(result, "/usr/lib/jvm/java-17-openjdk-amd64/lib/security/cacerts");
        runtime.Logger.DebugMessages.Should().HaveCount(4);
        AssertDebugLogged(runtime.Logger, "JAVA_HOME environment variable not set. Try to infer Java home from Java executable.");
        AssertDebugLogged(runtime.Logger, "Java executable located at: '/usr/bin/java'.");
        AssertDebugLogged(runtime.Logger, "Java executable symbolic link resolved to: '/usr/lib/jvm/java-17-openjdk-amd64/bin/java'.");
        AssertDebugLogged(runtime.Logger, "Java Keystore file found: '/usr/lib/jvm/java-17-openjdk-amd64/lib/security/cacerts'.");
    }

    [TestMethod]
    public void UnixTruststorePath_JavaExeProvided()
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Any<string>()).Returns(true);
        runtime.Directory.Exists(Arg.Any<string>()).Returns(true);
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Execute(Arg.Is<ProcessRunnerArguments>(x => x.CmdLineArgs.Contains("readlink -f /usr/bin/java")))
            .Returns(new ProcessResult(true, "/usr/lib/jvm/java-17-openjdk-amd64/bin/java", string.Empty));
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.JavaExePath, "/usr/bin/java");
        var processedArgs = CreateProcessedArgs(runtime, cmdLineArgs);
        var sut = new LocalJreTruststoreResolver(processRunner, runtime);
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", null);

        var result = sut.UnixTruststorePath(processedArgs);

        AssertPath(result, "/usr/lib/jvm/java-17-openjdk-amd64/lib/security/cacerts");
        runtime.Logger.DebugMessages.Should().HaveCount(5);
        AssertDebugLogged(runtime.Logger, "JAVA_HOME environment variable not set. Try to infer Java home from Java executable.");
        AssertDebugLogged(runtime.Logger, "The argument 'sonar.scanner.javaExePath' is set.");
        AssertDebugLogged(runtime.Logger, "Java executable located at: '/usr/bin/java'.");
        AssertDebugLogged(runtime.Logger, "Java executable symbolic link resolved to: '/usr/lib/jvm/java-17-openjdk-amd64/bin/java'.");
        AssertDebugLogged(runtime.Logger, "Java Keystore file found: '/usr/lib/jvm/java-17-openjdk-amd64/lib/security/cacerts'.");
    }

    [TestMethod]
    public void UnixTruststorePath_JavaHomeAndJavaExePathSet_JavaHomePathUsed()
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Any<string>()).Returns(true);
        runtime.Directory.Exists(Arg.Any<string>()).Returns(true);
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Execute(Arg.Is<ProcessRunnerArguments>(x => x.CmdLineArgs.Contains("command -v java"))).Returns(new ProcessResult(true, "/usr/lib/jvm/java-11/bin/java", string.Empty));
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.JavaExePath, "/usr/bin/java");
        var processedArgs = CreateProcessedArgs(runtime, cmdLineArgs);
        var sut = new LocalJreTruststoreResolver(processRunner, runtime);
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", "/usr/lib/jvm/java-17-openjdk-amd64");

        var result = sut.UnixTruststorePath(processedArgs);

        AssertPath(result, "/usr/lib/jvm/java-17-openjdk-amd64/lib/security/cacerts");
        runtime.Logger.DebugMessages.Should().ContainSingle();
        AssertDebugLogged(runtime.Logger, "Java Keystore file found: '/usr/lib/jvm/java-17-openjdk-amd64/lib/security/cacerts'.");
    }

    private static void AssertPath(string actual, string expected) =>
        ToUnixPath(actual).Should().Be(ToUnixPath(expected));

    private static void AssertDebugLogged(TestLogger logger, string message) =>
        logger.DebugMessages.Should().Contain(x => ToUnixPath(x) == message);

    private static string ToUnixPath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static ProcessedArgs CreateProcessedArgs(TestRuntime runtime, IAnalysisPropertyProvider cmdLineProvider = null) =>
        new(
            "valid.key",
            "valid.name",
            "1.0",
            "organization",
            false,
            cmdLineProvider ?? EmptyPropertyProvider.Instance,
            Substitute.For<IAnalysisPropertyProvider>(),
            EmptyPropertyProvider.Instance,
            runtime with { Logger = new() }); // new logger to avoid log pollution
}
