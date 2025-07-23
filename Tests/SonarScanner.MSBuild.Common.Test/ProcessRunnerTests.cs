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

using System.Security;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class ProcessRunnerTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Action action = () => _ = new ProcessRunner(null);
        action.Should().ThrowExactly<ArgumentNullException>().WithParameterName("logger");
    }

    [TestMethod]
    public void Execute_WhenRunnerArgsIsNull_ThrowsArgumentNullException()
    {
        Action action = () => new ProcessRunner(new TestLogger()).Execute(null);
        action.Should().ThrowExactly<ArgumentNullException>().WithParameterName("runnerArgs");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void ProcRunner_ExecutionFailed() =>
        new ProcessRunnerContext(TestContext, "exit -2") { ExpectedExitCode = -2 }.ExecuteAndAssert();

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void ProcRunner_ExecutionSucceeded()
    {
        var context = new ProcessRunnerContext(
            TestContext,
            """
            @echo off
            @echo Hello world
            xxx yyy
            @echo Testing 1,2,3...>&2
            """);

        context.ExecuteAndAssert();

        context.Logger.AssertInfoLogged("Hello world");
        context.Logger.AssertErrorLogged("Testing 1,2,3...");
        context.ResultStandardOutputShouldBe("Hello world" + Environment.NewLine);
        context.ResultErrorOutputShouldBe("""
            'xxx' is not recognized as an internal or external command,
            operable program or batch file.
            Testing 1,2,3...

            """);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void ProcRunner_ErrorAsWarningMessage_LogAsWarning()
    {
        var context = new ProcessRunnerContext(TestContext, """
            @echo off
            @echo WARN: Hello world>&2
            """);

        context.ExecuteAndAssert();

        context.Logger.AssertWarningLogged("WARN: Hello world");
        context.ResultStandardOutputShouldBe(string.Empty);
        context.ResultErrorOutputShouldBe("""
            WARN: Hello world

            """);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void ProcRunner_LogOutputFalse_ExecutionSucceeded()
    {
        var context = new ProcessRunnerContext(
            TestContext,
            """
            @echo off
            @echo Hello world
            xxx yyy
            @echo Testing 1,2,3...>&2
            """);
        context.ProcessArgs.LogOutput = false;

        context.ExecuteAndAssert();

        context.Logger.AssertMessageNotLogged("Hello world");
        context.Logger.AssertErrorNotLogged("Testing 1,2,3...");
        context.ResultStandardOutputShouldBe("Hello world" + Environment.NewLine);
        context.ResultErrorOutputShouldBe("""
            'xxx' is not recognized as an internal or external command,
            operable program or batch file.
            Testing 1,2,3...

            """);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void ProcRunner_FailsOnTimeout()
    {
        var context = new ProcessRunnerContext(
            TestContext,
            """
            powershell -Command "Start-Sleep -Seconds 2"
            @echo Hello world
            """)
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
        context.Logger.AssertMessageNotLogged("Hello world");
        context.Logger.AssertWarningsLogged(1); // expecting a warning about the timeout
        context.Logger.Warnings.Single().Contains("has been terminated").Should().BeTrue();
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void ProcRunner_PassesEnvVariables()
    {
        var context = new ProcessRunnerContext(
            TestContext,
            """
            echo %PROCESS_VAR%
            @echo %PROCESS_VAR2%
            @echo %PROCESS_VAR3%
            """);
        context.ProcessArgs.EnvironmentVariables = new Dictionary<string, string>
        {
            { "PROCESS_VAR", "PROCESS_VAR value" },
            { "PROCESS_VAR2", "PROCESS_VAR2 value" },
            { "PROCESS_VAR3", "PROCESS_VAR3 value" }
        };

        context.ExecuteAndAssert();
        context.Logger.AssertInfoLogged("PROCESS_VAR value");
        context.Logger.AssertInfoLogged("PROCESS_VAR2 value");
        context.Logger.AssertInfoLogged("PROCESS_VAR3 value");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void ProcRunner_PassesEnvVariables_OverrideExisting()
    {
        var context = new ProcessRunnerContext(
            TestContext,
            """
            @echo file: %proc.runner.test.machine%
            @echo file: %proc.runner.test.process%
            @echo file: %proc.runner.test.user%
            """);
        try
        {
            // It's possible the user won't be have permissions to set machine level variables
            // (e.g. when running on a build agent). Carry on with testing the other variables.
            SafeSetEnvironmentVariable("proc.runner.test.machine", "existing machine value", EnvironmentVariableTarget.Machine, context.Logger);
            Environment.SetEnvironmentVariable("proc.runner.test.process", "existing process value", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("proc.runner.test.user", "existing user value", EnvironmentVariableTarget.User);
            context.ProcessArgs.EnvironmentVariables = new Dictionary<string, string>
            {
                { "proc.runner.test.machine", "machine override" },
                { "proc.runner.test.process", "process override" },
                { "proc.runner.test.user", "user override" }
            };

            context.ExecuteAndAssert();
        }
        finally
        {
            SafeSetEnvironmentVariable("proc.runner.test.machine", null, EnvironmentVariableTarget.Machine, context.Logger);
            Environment.SetEnvironmentVariable("proc.runner.test.process", null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("proc.runner.test.user", null, EnvironmentVariableTarget.User);
        }

        // Check the child process used expected values
        context.Logger.AssertInfoLogged("file: machine override");
        context.Logger.AssertInfoLogged("file: process override");
        context.Logger.AssertInfoLogged("file: user override");

        // Check the runner reported it was overwriting existing variables
        // Note: the existing non-process values won't be visible to the child process
        // unless they were set *before* the test host launched, which won't be the case.
        context.Logger.AssertSingleDebugMessageExists("proc.runner.test.process", "existing process value", "process override");
    }

    [TestMethod]
    public void ProcRunner_MissingExe()
    {
        var context = new ProcessRunnerContext(
            TestContext,
            string.Empty)
        {
            ExpectedExitCode = ProcessRunner.ErrorCode,
            ProcessArgs = new ProcessRunnerArguments("missingExe.foo", false)
        };

        context.ExecuteAndAssert();
        context.Logger.AssertSingleErrorExists("missingExe.foo");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void ProcRunner_ArgumentQuoting()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var expected = new[]
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

        var context = new ProcessRunnerContext(
            TestContext,
            commands: string.Empty)
        {
            ProcessArgs =  new ProcessRunnerArguments(LogArgsPath(), false)
            {
                CmdLineArgs = expected,
                WorkingDirectory = testDir
            }
        };

        context.ExecuteAndAssert();

        // Check that the public and private arguments are passed to the child process
        context.AssertExpectedLogContents(expected);
    }

    // Checks arguments passed to a batch script which itself passes them on are correctly escaped
    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void ProcRunner_ArgumentQuotingForwardedByBatchScript()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var expected = new[]
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
        var context = new ProcessRunnerContext(
            TestContext,
            "\"" + LogArgsPath() + "\" %*");
        context.SetCmdLineArgs(expected);

        context.ExecuteAndAssert();
        context.AssertExpectedLogContents(expected);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [WorkItem(1706)] // https://github.com/SonarSource/sonar-scanner-msbuild/issues/1706
    public void ProcRunner_ArgumentQuotingScanner()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var expected = new[]
        {
            @"-Dsonar.scanAllFiles=true",
            @"-Dproject.settings=D:\DevLibTest\ClassLibraryTest.sonarqube\out\sonar-project.properties",
            @"--from=ScannerMSBuild/5.13.1",
            @"--debug"
        };

        var context = new ProcessRunnerContext(
            TestContext,
            commands: """
            @echo off
            REM The sonar-scanner.bat uses %* to pass the argument to javac.exe
            echo %*
            REM Because of the escaping, the single arguments are somewhat broken on echo. A workaround is to add some new lines for some reason.
            echo %1


            echo %2


            echo %3


            echo %4


            """);
        context.SetCmdLineArgs(expected);

        context.ExecuteAndAssert();
        // Check that the public and private arguments are passed to the child process
        context.Logger.InfoMessages.Should().BeEquivalentTo(
            @"""-Dsonar.scanAllFiles=true"" ""-Dproject.settings=D:\DevLibTest\ClassLibraryTest.sonarqube\out\sonar-project.properties"" ""--from=ScannerMSBuild/5.13.1"" ""--debug""",
            @"""-Dsonar.scanAllFiles=true""",
            string.Empty,
            @"""-Dproject.settings=D:\DevLibTest\ClassLibraryTest.sonarqube\out\sonar-project.properties""",
            string.Empty,
            @"""--from=ScannerMSBuild/5.13.1""",
            string.Empty,
            @"""--debug""");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [WorkItem(126)] // Exclude secrets from log data: http://jira.sonarsource.com/browse/SONARMSBRU-126
    public void ProcRunner_DoNotLogSensitiveData()
    {
        // Public args - should appear in the log
        var publicArgs = new[]
        {
            "public1",
            "public2",
            "/d:sonar.projectKey=my.key"
        };
        var sensitiveArgs = new[]
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
        var context = new ProcessRunnerContext(
            TestContext,
            null)
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
            context.Logger.AssertSingleDebugMessageExists(arg);
        }
        context.Logger.AssertSingleDebugMessageExists("Setting environment variable 'SENSITIVE_DATA'. Value: -D<sensitive data removed>");
        context.Logger.AssertSingleDebugMessageExists("Setting environment variable 'NOT_SENSITIVE'. Value: Something");
        context.Logger.AssertSingleDebugMessageExists("Setting environment variable 'MIXED_DATA'. Value: -DBefore=true -D<sensitive data removed>");
        context.Logger.AssertSingleDebugMessageExists("Overwriting the value of environment variable 'OVERWRITING_DATA'. Old value: Not sensitive, new value: -D<sensitive data removed>");
        context.Logger.AssertSingleDebugMessageExists("Overwriting the value of environment variable 'EXISTING_SENSITIVE_DATA'. Old value: -D<sensitive data removed>, new value: -D<sensitive data removed>");
        context.Logger.AssertSingleDebugMessageExists("Args: public1 public2 /dmy.key=value /d:sonar.projectKey=my.key <sensitive data removed>");
        context.AssertTextDoesNotAppearInLog("secret");
        // Check that the public and private arguments are passed to the child process
        context.AssertExpectedLogContents(allArgs);
    }

    private static void SafeSetEnvironmentVariable(string key, string value, EnvironmentVariableTarget target, ILogger logger)
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

    private static string LogArgsPath() =>
        // Replace to change this project directory to LogArgs project directory while keeping the same build configuration (Debug/Release)
        Path.Combine(Path.GetDirectoryName(typeof(ProcessRunnerTests).Assembly.Location).Replace("SonarScanner.MSBuild.Common.Test", "LogArgs"), "LogArgs.exe");

    private class ProcessRunnerContext
    {
        private readonly ProcessRunner runner;
        private readonly string testDir;
        private readonly string exePath;
        private ProcessResult result;

        public TestLogger Logger { get; private set; }
        public int ExpectedExitCode { get; init; }
        public ProcessRunnerArguments ProcessArgs { get; init; }

        public ProcessRunnerContext(TestContext testContext, string commands)
        {
            testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
            exePath = TestUtils.WriteBatchFileForTest(testContext, commands);
            Logger = new TestLogger();
            runner = new ProcessRunner(Logger);
            ProcessArgs = new ProcessRunnerArguments(exePath, true)
            {
                WorkingDirectory = testDir
            };
        }

        public void SetCmdLineArgs(IEnumerable<string> args) =>
            ProcessArgs.CmdLineArgs = args;

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
