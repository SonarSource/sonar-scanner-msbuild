/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class ProcessRunnerTests
{
    public TestContext TestContext { get; set; }

    #region Tests

    [TestMethod]
    public void Execute_WhenRunnerArgsIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ProcessRunner(new TestLogger()).Execute(null);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("runnerArgs");
    }

    [TestMethod]
    public void ProcRunner_ExecutionFailed()
    {
        // Arrange
        var exeName = TestUtils.WriteBatchFileForTest(TestContext, "exit -2");

        var logger = new TestLogger();
        var args = new ProcessRunnerArguments(exeName, true);
        var runner = new ProcessRunner(logger);

        // Act
        var success = runner.Execute(args);

        // Assert
        success.Should().BeFalse("Expecting the process to have failed");
        runner.ExitCode.Should().Be(-2, "Unexpected exit code");
    }

    [TestMethod]
    public void ProcRunner_ExecutionSucceeded()
    {
        // Arrange
        var exeName = TestUtils.WriteBatchFileForTest(TestContext,
@"@echo Hello world
xxx yyy
@echo Testing 1,2,3...>&2
");

        var logger = new TestLogger();
        var args = new ProcessRunnerArguments(exeName, true);
        var runner = new ProcessRunner(logger);

        // Act
        var success = runner.Execute(args);

        // Assert
        success.Should().BeTrue("Expecting the process to have succeeded");
        runner.ExitCode.Should().Be(0, "Unexpected exit code");

        logger.AssertInfoLogged("Hello world"); // Check output message are passed to the logger
        logger.AssertErrorLogged("Testing 1,2,3..."); // Check error messages are passed to the logger
    }

    [TestMethod]
    public void ProcRunner_FailsOnTimeout()
    {
        // Arrange

        // Calling TIMEOUT can fail on some OSes (e.g. Windows 7) with the error
        // "Input redirection is not supported, exiting the process immediately."
        // Alternatives such as
        // pinging a non-existent address with a timeout were not reliable.
        var exeName = TestUtils.WriteBatchFileForTest(TestContext,
@"waitfor /t 2 somethingThatNeverHappen
@echo Hello world
");

        var logger = new TestLogger();
        var args = new ProcessRunnerArguments(exeName, true)
        {
            TimeoutInMilliseconds = 100
        };
        var runner = new ProcessRunner(logger);

        var timer = Stopwatch.StartNew();

        // Act
        var success = runner.Execute(args);

        // Assert
        timer.Stop(); // Sanity check that the process actually timed out
        logger.LogInfo("Test output: test ran for {0}ms", timer.ElapsedMilliseconds);
        // TODO: the following line throws regularly on the CI machines (elapsed time is around 97ms)
        // timer.ElapsedMilliseconds >= 100.Should().BeTrue("Test error: batch process exited too early. Elapsed time(ms): {0}", timer.ElapsedMilliseconds)

        success.Should().BeFalse("Expecting the process to have failed");
        runner.ExitCode.Should().Be(ProcessRunner.ErrorCode, "Unexpected exit code");
        logger.AssertMessageNotLogged("Hello world");
        logger.AssertWarningsLogged(1); // expecting a warning about the timeout
        logger.Warnings.Single().Contains("has been terminated").Should().BeTrue();
    }

    [TestMethod]
    public void ProcRunner_PassesEnvVariables()
    {
        // Arrange
        var logger = new TestLogger();
        var runner = new ProcessRunner(logger);

        var exeName = TestUtils.WriteBatchFileForTest(TestContext,
@"echo %PROCESS_VAR%
@echo %PROCESS_VAR2%
@echo %PROCESS_VAR3%
");
        var envVariables = new Dictionary<string, string>() {
            { "PROCESS_VAR", "PROCESS_VAR value" },
            { "PROCESS_VAR2", "PROCESS_VAR2 value" },
            { "PROCESS_VAR3", "PROCESS_VAR3 value" } };

        var args = new ProcessRunnerArguments(exeName, true)
        {
            EnvironmentVariables = envVariables
        };

        // Act
        var success = runner.Execute(args);

        // Assert
        success.Should().BeTrue("Expecting the process to have succeeded");
        runner.ExitCode.Should().Be(0, "Unexpected exit code");

        logger.AssertInfoLogged("PROCESS_VAR value");
        logger.AssertInfoLogged("PROCESS_VAR2 value");
        logger.AssertInfoLogged("PROCESS_VAR3 value");
    }

    [TestMethod]
    public void ProcRunner_PassesEnvVariables_OverrideExisting()
    {
        // Tests that existing environment variables will be overwritten successfully

        // Arrange
        var logger = new TestLogger();
        var runner = new ProcessRunner(logger);

        try
        {
            // It's possible the user won't be have permissions to set machine level variables
            // (e.g. when running on a build agent). Carry on with testing the other variables.
            SafeSetEnvironmentVariable("proc.runner.test.machine", "existing machine value", EnvironmentVariableTarget.Machine, logger);
            Environment.SetEnvironmentVariable("proc.runner.test.process", "existing process value", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("proc.runner.test.user", "existing user value", EnvironmentVariableTarget.User);

            var exeName = TestUtils.WriteBatchFileForTest(TestContext,
@"@echo file: %proc.runner.test.machine%
@echo file: %proc.runner.test.process%
@echo file: %proc.runner.test.user%
");

            var envVariables = new Dictionary<string, string>() {
                { "proc.runner.test.machine", "machine override" },
                { "proc.runner.test.process", "process override" },
                { "proc.runner.test.user", "user override" } };

            var args = new ProcessRunnerArguments(exeName, true)
            {
                EnvironmentVariables = envVariables
            };

            // Act
            var success = runner.Execute(args);

            // Assert
            success.Should().BeTrue("Expecting the process to have succeeded");
            runner.ExitCode.Should().Be(0, "Unexpected exit code");
        }
        finally
        {
            SafeSetEnvironmentVariable("proc.runner.test.machine", null, EnvironmentVariableTarget.Machine, logger);
            Environment.SetEnvironmentVariable("proc.runner.test.process", null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("proc.runner.test.user", null, EnvironmentVariableTarget.User);
        }

        // Check the child process used expected values
        logger.AssertInfoLogged("file: machine override");
        logger.AssertInfoLogged("file: process override");
        logger.AssertInfoLogged("file: user override");

        // Check the runner reported it was overwriting existing variables
        // Note: the existing non-process values won't be visible to the child process
        // unless they were set *before* the test host launched, which won't be the case.
        logger.AssertSingleDebugMessageExists("proc.runner.test.process", "existing process value", "process override");
    }

    [TestMethod]
    public void ProcRunner_MissingExe()
    {
        // Tests attempting to launch a non-existent exe

        // Arrange
        var logger = new TestLogger();
        var args = new ProcessRunnerArguments("missingExe.foo", false);
        var runner = new ProcessRunner(logger);

        // Act
        var success = runner.Execute(args);

        // Assert
        success.Should().BeFalse("Expecting the process to have failed");
        runner.ExitCode.Should().Be(ProcessRunner.ErrorCode, "Unexpected exit code");
        logger.AssertSingleErrorExists("missingExe.foo");
    }

    [TestMethod]
    public void ProcRunner_ArgumentQuoting()
    {
        // Checks arguments passed to the child process are correctly quoted
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var runner = new ProcessRunner(new TestLogger());
        var expected = new[] {
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
        var args = new ProcessRunnerArguments(LogArgsPath(), false) { CmdLineArgs = expected, WorkingDirectory = testDir };
        var success = runner.Execute(args);

        success.Should().BeTrue("Expecting the process to have succeeded");
        runner.ExitCode.Should().Be(0, "Unexpected exit code");
        // Check that the public and private arguments are passed to the child process
        AssertExpectedLogContents(testDir, expected);
    }

    [TestMethod]
    public void ProcRunner_ArgumentQuotingForwardedByBatchScript()
    {
        // Checks arguments passed to a batch script which itself passes them on are correctly escaped
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var batchName = TestUtils.WriteBatchFileForTest(TestContext, "\"" + LogArgsPath() + "\" %*");
        var runner = new ProcessRunner(new TestLogger());
        var expected = new[] {
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
        var args = new ProcessRunnerArguments(batchName, true) { CmdLineArgs = expected, WorkingDirectory = testDir };
        var success = runner.Execute(args);

        success.Should().BeTrue("Expecting the process to have succeeded");
        runner.ExitCode.Should().Be(0, "Unexpected exit code");
        // Check that the public and private arguments are passed to the child process
        AssertExpectedLogContents(testDir, expected);
    }

    [TestMethod]
    [WorkItem(1706)] // https://github.com/SonarSource/sonar-scanner-msbuild/issues/1706
    public void ProcRunner_ArgumentQuotingScanner()
    {
        // Checks arguments passed to a batch script which itself passes them on are correctly escaped
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var batchName = TestUtils.WriteBatchFileForTest(TestContext,
@"@echo off
REM The sonar-scanner.bat uses %* to pass the argument to javac.exe
echo %*
REM Because of the escaping, the single arguments are somewhat broken on echo. A workaround is to add some new lines for some reason. 
echo %1


echo %2


echo %3


echo %4


");
        var logger = new TestLogger();
        var runner = new ProcessRunner(logger);
        var expected = new[]
        {
            @"-Dsonar.scanAllFiles=true",
            @"-Dproject.settings=D:\DevLibTest\ClassLibraryTest.sonarqube\out\sonar-project.properties",
            @"--from=ScannerMSBuild/5.13.1",
            @"--debug"
        };
        var args = new ProcessRunnerArguments(batchName, true) { CmdLineArgs = expected, WorkingDirectory = testDir };
        var success = runner.Execute(args);

        success.Should().BeTrue("Expecting the process to have succeeded");
        runner.ExitCode.Should().Be(0, "Unexpected exit code");
        // Check that the public and private arguments are passed to the child process
        logger.InfoMessages.Should().BeEquivalentTo(
            @"""-Dsonar.scanAllFiles=true"" ""-Dproject.settings=D:\DevLibTest\ClassLibraryTest.sonarqube\out\sonar-project.properties"" ""--from=ScannerMSBuild/5.13.1"" ""--debug""",
            @"""-Dsonar.scanAllFiles=true""",
            string.Empty,
            @"""-Dproject.settings=D:\DevLibTest\ClassLibraryTest.sonarqube\out\sonar-project.properties""",
            string.Empty,
            @"""--from=ScannerMSBuild/5.13.1""",
            string.Empty,
            @"""--debug""");
    }

    [TestMethod]
    [WorkItem(126)] // Exclude secrets from log data: http://jira.sonarsource.com/browse/SONARMSBRU-126
    public void ProcRunner_DoNotLogSensitiveData()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var logger = new TestLogger();
        var runner = new ProcessRunner(logger);
        // Public args - should appear in the log
        var publicArgs = new[]
        {
            "public1",
            "public2",
            "/d:sonar.projectKey=my.key"
        };
        var sensitiveArgs = new[] {
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
        var runnerArgs = new ProcessRunnerArguments(LogArgsPath(), false) { CmdLineArgs = allArgs, WorkingDirectory = testDir };
        var success = runner.Execute(runnerArgs);

        success.Should().BeTrue("Expecting the process to have succeeded");
        runner.ExitCode.Should().Be(0, "Unexpected exit code");
        // Check public arguments are logged but private ones are not
        foreach (var arg in publicArgs)
        {
            logger.AssertSingleDebugMessageExists(arg);
        }
        logger.AssertSingleDebugMessageExists("<sensitive data removed>");
        AssertTextDoesNotAppearInLog("secret", logger);
        // Check that the public and private arguments are passed to the child process
        AssertExpectedLogContents(testDir, allArgs);
    }

    #endregion Tests

    #region Private methods

    private static void SafeSetEnvironmentVariable(string key, string value, EnvironmentVariableTarget target, ILogger logger)
    {
        try
        {
            Environment.SetEnvironmentVariable(key, value, target);
        }
        catch (System.Security.SecurityException)
        {
            logger.LogWarning("Test setup error: user running the test doesn't have the permissions to set the environment variable. Key: {0}, value: {1}, target: {2}",
                key, value, target);
        }
    }

    private static string LogArgsPath() =>
        // Replace to change this project directory to LogArgs project directory while keeping the same build configuration (Debug/Release)
        Path.Combine(Path.GetDirectoryName(typeof(ProcessRunnerTests).Assembly.Location).Replace("SonarScanner.MSBuild.Common.Test", "LogArgs"), "LogArgs.exe");

    private void AssertExpectedLogContents(string logDir, params string[] expected)
    {
        var logFile = Path.Combine(logDir, "LogArgs.log");
        File.Exists(logFile).Should().BeTrue("Expecting the argument log file to exist. File: {0}", logFile);
        TestContext.AddResultFile(logFile);
        var actualArgs = File.ReadAllLines(logFile);
        actualArgs.Should().BeEquivalentTo(expected, "Log file does not have the expected content");
    }

    private static void AssertTextDoesNotAppearInLog(string text, TestLogger logger)
    {
        AssertTextDoesNotAppearInLog(text, logger.InfoMessages);
        AssertTextDoesNotAppearInLog(text, logger.Errors);
        AssertTextDoesNotAppearInLog(text, logger.Warnings);
    }

    private static void AssertTextDoesNotAppearInLog(string text, IList<string> logEntries)
    {
        logEntries.Should().NotContain(e => e.IndexOf(text, StringComparison.OrdinalIgnoreCase) > -1,
            "Specified text should not appear anywhere in the log file: {0}", text);
    }

    #endregion Private methods
}
