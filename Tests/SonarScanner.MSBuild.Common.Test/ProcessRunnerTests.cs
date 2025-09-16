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
using System.Runtime.InteropServices;
using System.Security;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class ProcessRunnerTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_NullLogger_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => _ = new ProcessRunner(null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("logger");

    [TestMethod]
    public void Execute_WhenRunnerArgsIsNull_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => new ProcessRunner(new TestLogger()).Execute(null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("runnerArgs");

    [TestMethod]
    public void ProcRunner_ExecutionFailed() =>
        new ProcessRunnerContext(TestContext, "exit 9") { ExpectedExitCode = 9 }.ExecuteAndAssert();

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

        context.Logger.Should().HaveInfos("Hello world")
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

        context.Logger.Should().HaveWarnings("WARN: Hello world");
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

        context.Logger.Should().NotHaveInfo("Hello world")
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
        context.Logger.DebugMessages.Should().SatisfyRespectively(
            x => x.Should().StartWith("Executing file "),
            x => x.Should().Be("Process returned exit code 0"));
        switch (logLevel)
        {
            case LogLevel.None:
                context.Logger.InfoMessages.Should().BeEmpty();
                context.Logger.Should().HaveNoWarnings();
                context.Logger.Should().HaveNoErrors();
                break;
            case LogLevel.Info:
                context.Logger.Should().HaveInfos("Hello World");
                context.Logger.Should().HaveNoWarnings();
                context.Logger.Should().HaveNoErrors();
                break;
            case LogLevel.Warning:
                context.Logger.Should().HaveWarnings("Hello World");
                context.Logger.Should().HaveNoErrors();
                context.Logger.InfoMessages.Should().BeEmpty();
                break;
            case LogLevel.Error:
                context.Logger.Should().HaveErrors("Hello World");
                context.Logger.Should().HaveNoWarnings();
                context.Logger.InfoMessages.Should().BeEmpty();
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
        context.Logger.DebugMessages.Should().SatisfyRespectively(
            x => x.Should().StartWith("Executing file "),
            x => x.Should().Be("Process returned exit code 0"));
        switch (logLevel)
        {
            case LogLevel.None:
                context.Logger.InfoMessages.Should().BeEmpty();
                context.Logger.Should().HaveNoWarnings();
                context.Logger.Should().HaveNoErrors();
                break;
            case LogLevel.Info:
                context.Logger.Should().HaveInfos("Hello World");
                context.Logger.Should().HaveNoWarnings();
                context.Logger.Should().HaveNoErrors();
                break;
            case LogLevel.Warning:
                context.Logger.Should().HaveWarnings("Hello World");
                context.Logger.Should().HaveNoErrors();
                context.Logger.InfoMessages.Should().BeEmpty();
                break;
            case LogLevel.Error:
                context.Logger.Should().HaveErrors("Hello World");
                context.Logger.Should().HaveNoWarnings();
                context.Logger.InfoMessages.Should().BeEmpty();
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
    public void ProcRunner_FailsOnTimeout()
    {
        var content = $"""
            {(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "powershell -Command \"Start-Sleep -Seconds 2\"" : "sleep 2")}
            {EchoCommand("Hello world")}
            """;
        var context = new ProcessRunnerContext(TestContext, content)
        {
            ExpectedExitCode = ProcessRunner.ErrorCode
        };

        context.ProcessArgs.TimeoutInMilliseconds = 250;

        var timer = Stopwatch.StartNew();
        context.Execute();
        timer.Stop(); // Sanity check that the process actually timed out
        context.Logger.LogInfo("Test output: test ran for {0}ms", timer.ElapsedMilliseconds);
        // TODO: the following line throws regularly on the CI machines (elapsed time is around 97ms)
        // timer.ElapsedMilliseconds >= 100.Should().BeTrue("Test error: batch process exited too early. Elapsed time(ms): {0}", timer.ElapsedMilliseconds)
        context.AssertExpected();
        context.Logger.Should().NotHaveInfo("Hello world")
            .And.HaveWarnings(1);   // expecting a warning about the timeout
        context.Logger.Warnings.Single().Contains("has been terminated").Should().BeTrue();
    }

    [TestMethod]
    public void ProcRunner_PassesEnvVariables()
    {
        var content = $"""
            {EchoEnvVar("PROCESS_VAR")}
            {EchoEnvVar("PROCESS_VAR2")}
            {EchoEnvVar("PROCESS_VAR3")}
            """;
        var context = new ProcessRunnerContext(TestContext, content);
        context.ProcessArgs.EnvironmentVariables = new Dictionary<string, string>
        {
            { "PROCESS_VAR", "PROCESS_VAR value" },
            { "PROCESS_VAR2", "PROCESS_VAR2 value" },
            { "PROCESS_VAR3", "PROCESS_VAR3 value" }
        };

        context.ExecuteAndAssert();
        context.Logger.Should().HaveInfos(
            "PROCESS_VAR value",
            "PROCESS_VAR2 value",
            "PROCESS_VAR3 value");
    }

    [TestMethod]
    public void ProcRunner_PassesEnvVariables_OverrideExisting()
    {
        var content = $"""
            {EchoEnvVar("proc_runner_test_machine")}
            {EchoEnvVar("proc_runner_test_process")}
            {EchoEnvVar("proc_runner_test_user")}
            """;
        var context = new ProcessRunnerContext(TestContext, content);
        try
        {
            // It's possible the user won't be have permissions to set machine level variables
            // (e.g. when running on a build agent). Carry on with testing the other variables.
            SafeSetEnvironmentVariable("proc_runner_test_machine", "existing machine value", EnvironmentVariableTarget.Machine, context.Logger);
            Environment.SetEnvironmentVariable("proc_runner_test_process", "existing process value", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("proc_runner_test_user", "existing user value", EnvironmentVariableTarget.User);
            context.ProcessArgs.EnvironmentVariables = new Dictionary<string, string>
            {
                { "proc_runner_test_machine", "machine override" },
                { "proc_runner_test_process", "process override" },
                { "proc_runner_test_user", "user override" }
            };

            context.ExecuteAndAssert();
        }
        finally
        {
            SafeSetEnvironmentVariable("proc_runner_test_machine", null, EnvironmentVariableTarget.Machine, context.Logger);
            Environment.SetEnvironmentVariable("proc_runner_test_process", null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("proc_runner_test_user", null, EnvironmentVariableTarget.User);
        }

        // Check the child process used expected values
        context.Logger.Should().HaveInfos(
            "machine override",
            "process override",
            "user override");

        // Check the runner reported it was overwriting existing variables
        // Note: the existing non-process values won't be visible to the child process
        // unless they were set *before* the test host launched, which won't be the case.
        context.Logger.Should().HaveDebugOnce("Overwriting the value of environment variable 'proc_runner_test_process'. Old value: existing process value, new value: process override");
    }

    [TestMethod]
    public void ProcRunner_MissingExe_ExeMustExists_True()
    {
        var context = new ProcessRunnerContext(TestContext, string.Empty)
        {
            ExpectedExitCode = ProcessRunner.ErrorCode,
            ProcessArgs = new ProcessRunnerArguments("missingExe.foo", false)
        };

        context.ExecuteAndAssert();
        context.Logger.Should().HaveErrorOnce("Execution failed. The specified executable does not exist: missingExe.foo");
    }

    [TestMethod]
    public void ProcRunner_MissingExe_ExeMustExists_False()
    {
        var context = new ProcessRunnerContext(TestContext, string.Empty)
        {
            ProcessArgs = new ProcessRunnerArguments("missingExe.foo", false) { ExeMustExists = false }
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
            "unquoted",
            "\"quoted\"",
            "\"quoted with spaces\"",
            "/test:\"quoted arg\"",
            "unquoted with spaces",
            "quote in \"the middle",
            "quotes \"& ampersands",
            "\"multiple \"\"\"      quotes \" ",
            "trailing backslash \\",
            "all special chars: \\ / : * ? \" < > | %",
            "injection \" > foo.txt",
            "injection \" & echo haha",
            "double escaping \\\" > foo.txt"
        };

        var context = new ProcessRunnerContext(TestContext)
        {
            ProcessArgs = new ProcessRunnerArguments(LogArgsPath(), false)
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
            ProcessArgs = new ProcessRunnerArguments(LogArgsPath(), false)
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
            ProcessArgs = new ProcessRunnerArguments(LogArgsPath(), false)
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
    public void ProcRunner_ArgumentQuotingForwardedByBatchScript()
    {
        var expected = new ProcessRunnerArguments.Argument[]
        {
            "unquoted",
            "\"quoted\"",
            "\"quoted with spaces\"",
            "/test:\"quoted arg\"",
            "unquoted with spaces",
            "quote in \"the middle",
            "quotes \"& ampersands",
            "\"multiple \"\"\"      quotes \" ",
            "trailing backslash \\",
            "all special chars: \\ / : * ? \" < > | %",
            "injection \" > foo.txt",
            "injection \" & echo haha",
            "double escaping \\\" > foo.txt"
        };

        var listArgs = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "%*" : "\"$@\"";
        var context = new ProcessRunnerContext(TestContext, "\"" + LogArgsPath() + "\" " + listArgs);
        context.ProcessArgs.CmdLineArgs = expected;

        context.ExecuteAndAssert();
        context.AssertExpectedLogContents(expected);
    }

    [TestMethod]
    public void ProcRunner_ArgumentQuotingScanner()
    {
        var expected = new ProcessRunnerArguments.Argument[]
        {
            @"-Dsonar.scanAllFiles=true",
            @"-Dproject.settings=D:\DevLibTest\ClassLibraryTest.sonarqube\out\sonar-project.properties",
            @"--from=ScannerMSBuild/5.13.1",
            @"--debug"
        };

        // The sonar-scanner.bat uses %* to pass the argument to javac.exe
        // Because of the escaping, the single arguments are somewhat broken on echo. A workaround is to add some new lines for some reason.
        var content = $"""
            {ScriptInit()}
            {EchoCommand("%*")}
            {EchoCommand("%1")}


            {EchoCommand("%2")}


            {EchoCommand("%3")}


            {EchoCommand("%4")}


            """;

        var context = new ProcessRunnerContext(TestContext, content);
        context.ProcessArgs.CmdLineArgs = expected;

        context.ExecuteAndAssert();
        // Check that the public and private arguments are passed to the child process
        var expectedLogMessages = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[]
            {
                @"""-Dsonar.scanAllFiles=true"" ""-Dproject.settings=D:\DevLibTest\ClassLibraryTest.sonarqube\out\sonar-project.properties"" ""--from=ScannerMSBuild/5.13.1"" ""--debug""",
                @"""-Dsonar.scanAllFiles=true""",
                string.Empty,
                @"""-Dproject.settings=D:\DevLibTest\ClassLibraryTest.sonarqube\out\sonar-project.properties""",
                string.Empty,
                @"""--from=ScannerMSBuild/5.13.1""",
                string.Empty,
                @"""--debug"""
            }
            : [
            @"-Dsonar.scanAllFiles=true -Dproject.settings=D:\DevLibTest\ClassLibraryTest.sonarqube\out\sonar-project.properties --from=ScannerMSBuild/5.13.1 --debug",
            @"-Dsonar.scanAllFiles=true",
            @"-Dproject.settings=D:\DevLibTest\ClassLibraryTest.sonarqube\out\sonar-project.properties",
            @"--from=ScannerMSBuild/5.13.1",
            @"--debug"
            ];

        context.Logger.InfoMessages.Should().BeEquivalentTo(expectedLogMessages);
    }

    [TestMethod]
    public void ProcRunner_DoNotLogSensitiveData()
    {
        // Public args - should appear in the log
        var publicArgs = new ProcessRunnerArguments.Argument[]
        {
            "public1",
            "public2",
            "/d:sonar.projectKey=my.key"
        };
        var sensitiveArgs = new ProcessRunnerArguments.Argument[]
        {
            // Public args - should appear in the log
            "public1", "public2", "/dmy.key=value",

            // Sensitive args - should not appear in the log
            "/d:sonar.password=secret data password",
            "/d:sonar.login=secret data login",
            "/d:sonar.token=secret data token",

            // Sensitive args - different cases -> exclude to be on the safe side
            "/d:sonar.PASSWORD=secret data password upper",

            // Sensitive args - parameter format is slightly incorrect -> exclude to be on the safe side
            "/dsonar.login =secret data login typo",
            "sonar.password=secret data password typo",
            "/dsonar.token =secret data token typo",
        };
        var allArgs = sensitiveArgs.Union(publicArgs).ToArray();
        var context = new ProcessRunnerContext(TestContext)
        {
            ProcessArgs = new ProcessRunnerArguments(LogArgsPath(), false)
            {
                CmdLineArgs = allArgs,
                EnvironmentVariables = new Dictionary<string, string>
                {
                    { "SENSITIVE_DATA", "-Djavax.net.ssl.trustStorePassword=changeit" },
                    { "OVERWRITING_DATA", "-Djavax.net.ssl.trustStorePassword=changeit" },
                    { "EXISTING_SENSITIVE_DATA", "-Djavax.net.ssl.trustStorePassword=changeit" },
                    { "NOT_SENSITIVE", "Something" },
                    { "MIXED_DATA", "-DBefore=true -Djavax.net.ssl.trustStorePassword=changeit -DAfter=false" }
                },
                WorkingDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext)
            }
        };

        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("OVERWRITING_DATA", "Not sensitive");
        scope.SetVariable("EXISTING_SENSITIVE_DATA", "-Djavax.net.ssl.trustStorePassword=password");

        context.ExecuteAndAssert();
        // Check public arguments are logged but private ones are not
        foreach (var arg in publicArgs)
        {
            context.Logger.DebugMessages.Should().ContainSingle(x => x.Contains(arg.Value));
        }
        context.Logger.Should().HaveDebugs(
            "Setting environment variable 'SENSITIVE_DATA'. Value: -D<sensitive data removed>",
            "Setting environment variable 'NOT_SENSITIVE'. Value: Something",
            "Setting environment variable 'MIXED_DATA'. Value: -DBefore=true -D<sensitive data removed>",
            "Overwriting the value of environment variable 'OVERWRITING_DATA'. Old value: Not sensitive, new value: -D<sensitive data removed>");
        context.Logger.DebugMessages.Should()
            .ContainSingle(x => x.Contains("Overwriting the value of environment variable 'EXISTING_SENSITIVE_DATA'. Old value: -D<sensitive data removed>, new value: -D<sensitive data removed>"))
            .And.ContainSingle(x => x.Contains("Args: public1 public2 /dmy.key=value /d:sonar.projectKey=my.key <sensitive data removed>"));
        context.AssertTextDoesNotAppearInLog("secret");
        // Check that the public and private arguments are passed to the child process
        context.AssertExpectedLogContents(allArgs);
    }

    private static void SafeSetEnvironmentVariable(string key, string value, EnvironmentVariableTarget target, TestLogger logger)
    {
        try
        {
            Environment.SetEnvironmentVariable(key, value, target);
        }
        catch (SecurityException)
        {
            logger.LogWarning(
                "Test setup error: user running the test doesn't have the permissions to set the environment variable. Key: {0}, value: {1}, target: {2}",
                key,
                value,
                target);
        }
    }

    private static string EnvVar(string text) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"%{text}%" : $"${text}";

    private static string EchoEnvVar(string text) =>
        EchoCommand(EnvVar(text));

    private static string EchoCommand(string text) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"@echo {text}" : $"echo \"{text.Replace('%', '$')}\"";

    private static string ReadCommand(string variableName = "var1") =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"set /P {variableName}=" : $"read {variableName}";

    private static string ScriptInit() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "@echo off" : "#!/bin/sh";

    private static string LogArgsPath()
    {
        var basePath = Path.GetDirectoryName(typeof(ProcessRunnerTests).Assembly.Location).Replace("SonarScanner.MSBuild.Common.Test", "LogArgs");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(basePath, "LogArgs.exe");
        }
        // See also 'Build test pre-requisites' in templates/unix-qa-stage.yml
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Path.Combine(basePath.Replace("Debug", "Release"), "linux-x64", "LogArgs");
        }
        else // MacOs
        {
            return Path.Combine(basePath.Replace("Debug", "Release"), "osx-x64", "LogArgs");
        }
    }

    private class ProcessRunnerContext
    {
        private readonly ProcessRunner runner;
        private readonly string testDir;
        private ProcessResult result;

        public TestLogger Logger { get; }
        public string ExePath { get; }
        public int ExpectedExitCode { get; init; }
        public ProcessRunnerArguments ProcessArgs { get; init; }

        public ProcessRunnerContext(TestContext testContext, string commands = null)
        {
            commands = $"""
                {ScriptInit()}
                {commands}
                """;
            testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
            ExePath = TestUtils.WriteExecutableScriptForTest(testContext, commands);
            Logger = new TestLogger();
            runner = new ProcessRunner(Logger);
            ProcessArgs = new ProcessRunnerArguments(ExePath, RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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

        public void AssertExpected()
        {
            result.Succeeded.Should().Be(ExpectedExitCode == 0, $"Expecting the process to have {(ExpectedExitCode == 0 ? "succeeded" : "failed")}");
            runner.ExitCode.Should().Be(ExpectedExitCode, "Unexpected exit code");
        }

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
            Logger.InfoMessages
                .Concat(Logger.Errors)
                .Concat(Logger.Warnings)
                .Should()
                .NotContain(
                    x => x.IndexOf(text, StringComparison.OrdinalIgnoreCase) > -1,
                    "Specified text should not appear anywhere in the log file: {0}",
                    text);
    }
}
