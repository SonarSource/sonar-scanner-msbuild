/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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
using System.Runtime.InteropServices;
using System.Security;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class ProcessRunnerTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_NullLogger_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => _ = new ProcessRunner(null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("runtime");

    [TestMethod]
    public void Execute_WhenRunnerArgsIsNull_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => new ProcessRunner(new TestRuntime()).Execute(null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("runnerArgs");

    [TestMethod]
    public void ProcRunner_ExecutionFailed() =>
        new ProcessRunnerContext(TestContext, "exit 9") { ExpectedSucceeded = false }.ExecuteAndAssert();

    [TestMethod]
    public void ProcRunner_ExecutionSucceeded()
    {
        var content = $"""
            {EchoCommand("Hello world")}
            xxx yyy
            {EchoCommand("Testing 1,2,3...")}>&2
            """;

        var context = new ProcessRunnerContext(
            TestContext,
            content);

        context.ExecuteAndAssert();

        var expected = string.Empty;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            expected = $"'xxx' is not recognized as an internal or external command,{Environment.NewLine}operable program or batch file.{Environment.NewLine}Testing 1,2,3...{Environment.NewLine}";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            expected = $"{context.ExePath}: line 3: xxx: command not found{Environment.NewLine}Testing 1,2,3...{Environment.NewLine}";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            expected = $"{context.ExePath}: 3: xxx: not found{Environment.NewLine}Testing 1,2,3...{Environment.NewLine}";
        }

        context.Runtime.Logger.Should().HaveInfos("Hello world")
            .And.HaveErrors("Testing 1,2,3...");
        context.ResultStandardOutputShouldBe("Hello world" + Environment.NewLine);
        context.ResultErrorOutputShouldBe(expected);
    }

    [TestMethod]
    public void ProcRunner_ErrorAsWarningMessage_LogAsWarning()
    {
        var content = $"""
            {EchoCommand("WARN: Hello world")}>&2
            """;
        var context = new ProcessRunnerContext(TestContext, content);

        context.ExecuteAndAssert();

        context.Runtime.Logger.Should().HaveWarnings("WARN: Hello world");
        context.ResultStandardOutputShouldBe(string.Empty);
        context.ResultErrorOutputShouldBe("WARN: Hello world" + Environment.NewLine);
    }

    [TestMethod]
    public void ProcRunner_LogOutputFalse_ExecutionSucceeded()
    {
        var content = $"""
            {EchoCommand("Hello world")}
            xxx yyy
            {EchoCommand("Testing 1,2,3...")}>&2
            """;
        var context = new ProcessRunnerContext(TestContext, content);
        context.ProcessArgs.LogOutput = false;

        context.ExecuteAndAssert();

        context.Runtime.Logger.Should().NotHaveInfo("Hello world")
            .And.NotHaveError("Testing 1,2,3...");
        context.ResultStandardOutputShouldBe("Hello world" + Environment.NewLine);

        var expected = string.Empty;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            expected = $"'xxx' is not recognized as an internal or external command,{Environment.NewLine}operable program or batch file.{Environment.NewLine}Testing 1,2,3...{Environment.NewLine}";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            expected = $"{context.ExePath}: line 3: xxx: command not found{Environment.NewLine}Testing 1,2,3...{Environment.NewLine}";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            expected = $"{context.ExePath}: 3: xxx: not found{Environment.NewLine}Testing 1,2,3...{Environment.NewLine}";
        }
        context.ResultErrorOutputShouldBe(expected);
    }

    [TestMethod]
    [DataRow(LogLevel.None)]
    [DataRow(LogLevel.Info)]
    [DataRow(LogLevel.Warning)]
    [DataRow(LogLevel.Error)]
    [DataRow((LogLevel)int.MinValue)]
    public void ProcRunner_OutputToLogMessage_LogLevel_StdOut(LogLevel logLevel)
    {
        var context = new ProcessRunnerContext(TestContext, EchoCommand("Hello World"))
        {
            ProcessArgs = { OutputToLogMessage = (_, message) => new(logLevel, message) }
        };
        context.ExecuteAndAssert();
        context.ResultStandardOutputShouldBe("Hello World" + Environment.NewLine);
        context.ResultErrorOutputShouldBe(string.Empty);
        context.Runtime.Logger.DebugMessages.Should().SatisfyRespectively(
            x => x.Should().StartWith("Executing file "),
            x => x.Should().Be("Process returned exit code 0"));
        switch (logLevel)
        {
            case LogLevel.None:
                context.Runtime.Logger.InfoMessages.Should().BeEmpty();
                context.Runtime.Logger.Should().HaveNoWarnings();
                context.Runtime.Logger.Should().HaveNoErrors();
                break;
            case LogLevel.Info:
                context.Runtime.Logger.Should().HaveInfos("Hello World");
                context.Runtime.Logger.Should().HaveNoWarnings();
                context.Runtime.Logger.Should().HaveNoErrors();
                break;
            case LogLevel.Warning:
                context.Runtime.Logger.Should().HaveWarnings("Hello World");
                context.Runtime.Logger.Should().HaveNoErrors();
                context.Runtime.Logger.InfoMessages.Should().BeEmpty();
                break;
            case LogLevel.Error:
                context.Runtime.Logger.Should().HaveErrors("Hello World");
                context.Runtime.Logger.Should().HaveNoWarnings();
                context.Runtime.Logger.InfoMessages.Should().BeEmpty();
                break;
        }
    }

    [TestMethod]
    [DataRow(LogLevel.None)]
    [DataRow(LogLevel.Info)]
    [DataRow(LogLevel.Warning)]
    [DataRow(LogLevel.Error)]
    [DataRow((LogLevel)int.MinValue)]
    public void ProcRunner_OutputToLogMessage_LogLevel_ErrorOut(LogLevel logLevel)
    {
        var context = new ProcessRunnerContext(TestContext, $"""{EchoCommand("Hello World")}>&2""")
        {
            ProcessArgs = { OutputToLogMessage = (_, message) => new(logLevel, message) }
        };
        context.ExecuteAndAssert();
        context.ResultErrorOutputShouldBe("Hello World" + Environment.NewLine);
        context.ResultStandardOutputShouldBe(string.Empty);
        context.Runtime.Logger.DebugMessages.Should().SatisfyRespectively(
            x => x.Should().StartWith("Executing file "),
            x => x.Should().Be("Process returned exit code 0"));
        switch (logLevel)
        {
            case LogLevel.None:
                context.Runtime.Logger.InfoMessages.Should().BeEmpty();
                context.Runtime.Logger.Should().HaveNoWarnings();
                context.Runtime.Logger.Should().HaveNoErrors();
                break;
            case LogLevel.Info:
                context.Runtime.Logger.Should().HaveInfos("Hello World");
                context.Runtime.Logger.Should().HaveNoWarnings();
                context.Runtime.Logger.Should().HaveNoErrors();
                break;
            case LogLevel.Warning:
                context.Runtime.Logger.Should().HaveWarnings("Hello World");
                context.Runtime.Logger.Should().HaveNoErrors();
                context.Runtime.Logger.InfoMessages.Should().BeEmpty();
                break;
            case LogLevel.Error:
                context.Runtime.Logger.Should().HaveErrors("Hello World");
                context.Runtime.Logger.Should().HaveNoWarnings();
                context.Runtime.Logger.InfoMessages.Should().BeEmpty();
                break;
        }
    }

    [TestMethod]
    public void ProcRunner_StandardInput()
    {
        // The console reads input via code page https://en.wikipedia.org/wiki/Code_page_437 and we send UTF-8 but for ASCII characters, both encodings are identical
        var context = new ProcessRunnerContext(TestContext, $"""
            {ReadCommand("var1")}
            {EchoCommand($"You entered: {EnvVar("var1")}")}
            """)
        {
            ProcessArgs = { StandardInput = "Hello World" }
        };

        context.ExecuteAndAssert();
        context.ResultStandardOutputShouldBe("You entered: Hello World" + Environment.NewLine);
    }

    [TestMethod]
    [TestCategory(TestCategories.NoMacOS)]
    [TestCategory(TestCategories.NoLinux)]
    public void ProcRunner_StandardInput_BatchfileWithCodePageSetTo_UTF8()
    {
        var context = new ProcessRunnerContext(TestContext, $"""
            chcp 65001
            {ReadCommand("var1")}
            {EchoCommand($"You entered: {EnvVar("var1")}")}
            """)
        {
            ProcessArgs = { StandardInput = "Hello World 😊" } // 😊 = F09F 988A in UTF-8
        };

        context.ExecuteAndAssert();
        // F09F 988A is ≡ƒÿè in Codepage DOS Latin-US CP437
        // https://planetcalc.com/9043/?encoding=cp437_DOSLatinUS
        context.ResultStandardOutputShouldBe("""
                Active code page: 65001
                You entered: Hello World ≡ƒÿè

                """
            .ToEnvironmentLineEndings());
    }

    [TestMethod]
    public void ProcRunner_MissingExe_ExeMustExists_True()
    {
        var context = new ProcessRunnerContext(TestContext, string.Empty)
        {
            ExpectedSucceeded = false,
            ProcessArgs = new ProcessRunnerArguments("missingExe.foo")
        };

        context.ExecuteAndAssert();
        context.Runtime.Logger.Should().HaveErrorOnce("Execution failed. The specified executable does not exist: missingExe.foo");
    }

    [TestMethod]
    public void ProcRunner_MissingExe_ExeMustExists_False()
    {
        var context = new ProcessRunnerContext(TestContext, string.Empty)
        {
            ProcessArgs = new ProcessRunnerArguments("missingExe.foo") { ExeMustExists = false }
        };

        FluentActions.Invoking(context.Execute).Should().Throw<Win32Exception>().Which.Message.Should().BeOneOf(
            "The system cannot find the file specified",
            $"An error occurred trying to start process 'missingExe.foo' with working directory '{Environment.CurrentDirectory}'. The system cannot find the file specified.",
            $"An error occurred trying to start process 'missingExe.foo' with working directory '{Environment.CurrentDirectory}'. No such file or directory");
    }

    [TestMethod]
    public void ProcRunner_ArgumentQuoting()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var expected = new ProcessRunnerArguments.Argument[]
        {
            new("unquoted"),
            new("\"quoted\""),
            new("\"quoted with spaces\""),
            new("/test:\"quoted arg\""),
            new("unquoted with spaces"),
            new("quote in \"the middle"),
            new("quotes \"& ampersands"),
            new("\"multiple \"\"\"      quotes \" "),
            new("trailing backslash \\"),
            new("all special chars: \\ / : * ? \" < > | %"),
            new("injection \" > foo.txt"),
            new("injection \" & echo haha"),
            new("double escaping \\\" > foo.txt")
        };

        var context = new ProcessRunnerContext(TestContext)
        {
            ProcessArgs = new ProcessRunnerArguments(LogArgsPath())
            {
                CmdLineArgs = expected,
                WorkingDirectory = testDir
            }
        };

        context.ExecuteAndAssert();

        // Check that the public and private arguments are passed to the child process
        context.AssertExpectedLogContents(expected);
    }

    [TestMethod]
    public void ProcRunner_DoesNotEscapeEscapedArgs()
    {
        var context = new ProcessRunnerContext(TestContext)
        {
            ProcessArgs = new ProcessRunnerArguments(LogArgsPath())
            {
                CmdLineArgs = [
                    new("arg1", true),
                    new("\"arg2\"", true),
                    new("\"arg with spaces\"", true)],
                WorkingDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext)
            }
        };

        context.ExecuteAndAssert();
        context.AssertExpectedLogContents("arg1", "arg2", "arg with spaces");
    }

    [TestMethod]
    public void ProcRunner_EscapesUnescapedArgs()
    {
        var context = new ProcessRunnerContext(TestContext)
        {
            ProcessArgs = new ProcessRunnerArguments(LogArgsPath())
            {
                CmdLineArgs = [
                    new("arg1", false),
                    new("\"arg2\"", false),
                    new("\"arg with spaces\"", false)],
                WorkingDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext)
            }
        };

        context.ExecuteAndAssert();
        context.AssertExpectedLogContents("arg1", "\"arg2\"", "\"arg with spaces\"");
    }

    [TestMethod]
    public void ProcRunner_DoNotLogSensitiveData()
    {
        // Public args - should appear in the log
        var publicArgs = new ProcessRunnerArguments.Argument[]
        {
            new("public1"),
            new("public2"),
            new("/d:sonar.projectKey=my.key")
        };
        var sensitiveArgs = new ProcessRunnerArguments.Argument[]
        {
            // Public args - should appear in the log
            new("public1"), new("public2"), new("/dmy.key=value"),

            // Sensitive args - should not appear in the log
            new("/d:sonar.password=secret data password"),
            new("/d:sonar.login=secret data login"),
            new("/d:sonar.token=secret data token"),

            // Sensitive args - different cases -> exclude to be on the safe side
            new("/d:sonar.PASSWORD=secret data password upper"),

            // Sensitive args - parameter format is slightly incorrect -> exclude to be on the safe side
            new("/dsonar.login =secret data login typo"),
            new("sonar.password=secret data password typo"),
            new("/dsonar.token =secret data token typo"),
        };
        var allArgs = sensitiveArgs.Union(publicArgs).ToArray();
        var context = new ProcessRunnerContext(TestContext)
        {
            ProcessArgs = new ProcessRunnerArguments(LogArgsPath())
            {
                CmdLineArgs = allArgs,
                WorkingDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext)
            }
        };

        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SENSITIVE_DATA", "-Djavax.net.ssl.trustStorePassword=secret");
        scope.SetVariable("NOT_SENSITIVE", "Something");
        scope.SetVariable("MIXED_DATA", "-DBefore=true -Djavax.net.ssl.trustStorePassword=secret -DAfter=false");

        context.ExecuteAndAssert();
        // Check public arguments are logged but private ones are not
        foreach (var arg in publicArgs)
        {
            context.Runtime.Logger.DebugMessages.Should().ContainSingle(x => x.Contains(arg.Value));
        }
        context.Runtime.Logger.DebugMessages.Should().ContainSingle(x => x.Contains("Args: public1 public2 /dmy.key=value /d:sonar.projectKey=my.key <sensitive data removed>"));
        context.AssertTextDoesNotAppearInLog("secret");
        // Check that the public and private arguments are passed to the child process
        context.AssertExpectedLogContents(allArgs);
    }

    private static string EnvVar(string text) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"%{text}%" : $"${text}";

    private static string EchoCommand(string text) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"@echo {text}" : $"echo \"{text.Replace('%', '$')}\"";

    private static string ReadCommand(string variableName = "var1") =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"set /P {variableName}=" : $"read {variableName}";

    private static string ScriptInit() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "@echo off" : "#!/bin/sh";

    private static string LogArgsPath()
    {
        var basePath = Path.GetDirectoryName(typeof(ProcessRunnerTests).Assembly.Location)
            .Replace("__Instrumented_SonarScanner.MSBuild.Common.Test", null)  // AltCover adds one more folder
            .Replace("SonarScanner.MSBuild.Common.Test", "LogArgs");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(basePath, "LogArgs.exe");
        }
        // See also 'Build test pre-requisites' in .github/actions/qa-unix/action.yml
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Path.Combine(basePath.Replace("Debug", "Release"), "linux-x64", "LogArgs");
        }
        else // MacOs
        {
            return Path.Combine(basePath.Replace("Debug", "Release"), "osx-arm64", "LogArgs");
        }
    }

    private class ProcessRunnerContext
    {
        private readonly ProcessRunner runner;
        private readonly string testDir;
        private ProcessResult result;

        public TestRuntime Runtime { get; }
        public string ExePath { get; }
        public bool ExpectedSucceeded { get; init; } = true;
        public ProcessRunnerArguments ProcessArgs { get; init; }

        public ProcessRunnerContext(TestContext testContext, string commands = null)
        {
            commands = $"""
                {ScriptInit()}
                {commands}
                """;
            testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
            ExePath = TestUtils.WriteExecutableScriptForTest(testContext, commands);
            Runtime = new TestRuntime();
            Runtime.File.ShortName(Arg.Any<PlatformOS>(), Arg.Any<string>()).Returns(x => x[1]);
            runner = new ProcessRunner(Runtime);
            ProcessArgs = new ProcessRunnerArguments(ExePath)
            {
                WorkingDirectory = testDir
            };
        }

        public void ExecuteAndAssert()
        {
            Execute();
            AssertExpected();
        }

        public void Execute() =>
            result = runner.Execute(ProcessArgs);

        public void AssertExpected() =>
            result.Succeeded.Should().Be(ExpectedSucceeded);

        public void ResultStandardOutputShouldBe(string expected)
        {
            if (string.IsNullOrEmpty(expected))
            {
                result.StandardOutput.Should().BeEmpty("Expected standard output to be empty");
                return;
            }
            result.StandardOutput.Should().Be(expected, "Unexpected standard output");
        }

        public void ResultErrorOutputShouldBe(string expected) =>
            result.ErrorOutput.Should().Be(expected, "Unexpected error output");

        public void AssertExpectedLogContents(params ProcessRunnerArguments.Argument[] expected) =>
            AssertExpectedLogContents(expected.Select(x => x.Value).ToArray());

        public void AssertExpectedLogContents(params string[] expected)
        {
            var logFile = Path.Combine(testDir, "LogArgs.log");
            File.Exists(logFile).Should().BeTrue("Expecting the log file to exist. File: {0}", logFile);
            File.ReadAllLines(logFile).Should().BeEquivalentTo(expected, "Log file does not have the expected content");
        }

        public void AssertTextDoesNotAppearInLog(string text) =>
            Runtime.Logger.InfoMessages
                .Concat(Runtime.Logger.Errors)
                .Concat(Runtime.Logger.Warnings)
                .Should()
                .NotContain(
                    x => x.IndexOf(text, StringComparison.OrdinalIgnoreCase) > -1,
                    "Specified text should not appear anywhere in the log file: {0}",
                    text);
    }
}
