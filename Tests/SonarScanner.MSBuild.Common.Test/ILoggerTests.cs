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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class ILoggerTests
{
    private readonly IFileWrapper fileWrapper = Substitute.For<IFileWrapper>();

    public TestContext TestContext { get; set; }

    [TestMethod]
    [Description("Regression test: checks the logger does not fail on null message")]
    public void ConsoleLogger_NoExceptionOnNullMessage()
    {
        // 1. Logger without timestamps
        var logger = new ConsoleLogger(includeTimestamp: false);

        logger.Invoking(x => x.LogInfo(null)).Should().NotThrow();
        logger.Invoking(x => x.LogInfo(null, null)).Should().NotThrow();
        logger.Invoking(x => x.LogInfo(null, "abc")).Should().NotThrow();

        logger.Invoking(x => x.LogWarning(null)).Should().NotThrow();
        logger.Invoking(x => x.LogWarning(null, null)).Should().NotThrow();
        logger.Invoking(x => x.LogWarning(null, "abc")).Should().NotThrow();

        logger.Invoking(x => x.LogError(null)).Should().NotThrow();
        logger.Invoking(x => x.LogError(null, null)).Should().NotThrow();
        logger.Invoking(x => x.LogError(null, "abc")).Should().NotThrow();

        // 2. Logger without timestamps
        logger = new ConsoleLogger(includeTimestamp: true);

        logger.Invoking(x => x.LogInfo(null)).Should().NotThrow();
        logger.Invoking(x => x.LogInfo(null, null)).Should().NotThrow();
        logger.Invoking(x => x.LogInfo(null, "abc")).Should().NotThrow();

        logger.Invoking(x => x.LogWarning(null)).Should().NotThrow();
        logger.Invoking(x => x.LogWarning(null, null)).Should().NotThrow();
        logger.Invoking(x => x.LogWarning(null, "abc")).Should().NotThrow();

        logger.Invoking(x => x.LogError(null)).Should().NotThrow();
        logger.Invoking(x => x.LogError(null, null)).Should().NotThrow();
        logger.Invoking(x => x.LogError(null, "abc")).Should().NotThrow();
    }

    [TestMethod]
    [Description("Regression test: checks the logger does not fail on null arguments")]
    public void ConsoleLogger_NoExceptionOnNullArgs()
    {
        // 1. Logger without timestamps
        var logger = new ConsoleLogger(includeTimestamp: false);

        logger.Invoking(x => x.LogInfo(null, null)).Should().NotThrow();
        logger.Invoking(x => x.LogInfo("123", null)).Should().NotThrow();

        logger.Invoking(x => x.LogWarning(null, null)).Should().NotThrow();
        logger.Invoking(x => x.LogWarning("123", null)).Should().NotThrow();

        logger.Invoking(x => x.LogError(null, null)).Should().NotThrow();
        logger.Invoking(x => x.LogError("123", null)).Should().NotThrow();

        // 2. Logger without timestamps
        logger = new ConsoleLogger(includeTimestamp: true);

        logger.Invoking(x => x.LogInfo(null, null)).Should().NotThrow();
        logger.Invoking(x => x.LogInfo("123", null)).Should().NotThrow();

        logger.Invoking(x => x.LogWarning(null, null)).Should().NotThrow();
        logger.Invoking(x => x.LogWarning("123", null)).Should().NotThrow();

        logger.Invoking(x => x.LogError(null, null)).Should().NotThrow();
        logger.Invoking(x => x.LogError("123", null)).Should().NotThrow();
    }

    [TestMethod]
    public void ConsoleLogger_ExpectedMessages_Message()
    {
        using (var output = new OutputCaptureScope())
        {
            // 1. Logger without timestamps
            var logger = new ConsoleLogger(includeTimestamp: false);

            logger.LogInfo("message1");
            output.AssertExpectedLastMessage("message1");

            logger.LogInfo("message2", null);
            output.AssertExpectedLastMessage("message2");

            logger.LogInfo("message3 {0}", "xxx");
            output.AssertExpectedLastMessage("message3 xxx");

            // 2. Logger with timestamps
            logger = new ConsoleLogger(includeTimestamp: true);

            logger.LogInfo("message4");
            output.AssertLastMessageEndsWith("message4");

            logger.LogInfo("message5{0}{1}", null, null);
            output.AssertLastMessageEndsWith("message5");

            logger.LogInfo("message6 {0}{1}", "xxx", "yyy", "zzz");
            output.AssertLastMessageEndsWith("message6 xxxyyy");
        }
    }

    [TestMethod]
    public void ConsoleLogger_ExpectedMessages_Warning()
    {
        // NOTE: we expect all warnings to be prefixed with a localized
        // "WARNING" prefix, so we're using "AssertLastMessageEndsWith"
        // even for warnings that do not have timestamps.
        using (var output = new OutputCaptureScope())
        {
            // 1. Logger without timestamps
            var logger = new ConsoleLogger(includeTimestamp: false);

            logger.LogWarning("warn1");
            output.AssertLastMessageEndsWith("warn1");

            logger.LogWarning("warn2", null);
            output.AssertLastMessageEndsWith("warn2");

            logger.LogWarning("warn3 {0}", "xxx");
            output.AssertLastMessageEndsWith("warn3 xxx");

            // 2. Logger with timestamps
            logger = new ConsoleLogger(includeTimestamp: true);

            logger.LogWarning("warn4");
            output.AssertLastMessageEndsWith("warn4");

            logger.LogWarning("warn5{0}{1}", null, null);
            output.AssertLastMessageEndsWith("warn5");

            logger.LogWarning("warn6 {0}{1}", "xxx", "yyy", "zzz");
            output.AssertLastMessageEndsWith("warn6 xxxyyy");
        }
    }

    [TestMethod]
    public void ConsoleLogger_ExpectedMessages_Error()
    {
        using (var output = new OutputCaptureScope())
        {
            // 1. Logger without timestamps
            var logger = new ConsoleLogger(includeTimestamp: false);

            logger.LogError("simple error1");
            output.AssertExpectedLastError("simple error1");

            logger.LogError("simple error2", null);
            output.AssertExpectedLastError("simple error2");

            logger.LogError("simple error3 {0}", "xxx");
            output.AssertExpectedLastError("simple error3 xxx");

            // 2. Logger with timestamps
            logger = new ConsoleLogger(includeTimestamp: true);

            logger.LogError("simple error4");
            output.AssertLastErrorEndsWith("simple error4");

            logger.LogError("simple error5{0}{1}", null, null);
            output.AssertLastErrorEndsWith("simple error5");

            logger.LogError("simple error6 {0}{1}", "xxx", "yyy", "zzz");
            output.AssertLastErrorEndsWith("simple error6 xxxyyy");
        }
    }

    [TestMethod]
    [Description("Checks that formatted strings and special formatting characters are handled correctly")]
    public void ConsoleLogger_FormattedStrings()
    {
        using (var output = new OutputCaptureScope())
        {
            // 1. Logger without timestamps
            var logger = new ConsoleLogger(includeTimestamp: false);

            logger.LogInfo("{ }");
            output.AssertExpectedLastMessage("{ }");

            logger.LogInfo("}{");
            output.AssertExpectedLastMessage("}{");

            logger.LogInfo("a{1}2", null);
            output.AssertExpectedLastMessage("a{1}2");

            logger.LogInfo("{0}", "123");
            output.AssertExpectedLastMessage("123");

            logger.LogInfo("a{0}{{{1}}}", "11", "22");
            output.AssertExpectedLastMessage("a11{22}");

            // 2. Logger with timestamps
            logger = new ConsoleLogger(includeTimestamp: true);

            logger.LogInfo("{ }");
            output.AssertLastMessageEndsWith("{ }");

            logger.LogInfo("}{");
            output.AssertLastMessageEndsWith("}{");

            logger.LogInfo("a{1}2", null);
            output.AssertLastMessageEndsWith("a{1}2");

            logger.LogInfo("{0}", "123");
            output.AssertLastMessageEndsWith("123");

            logger.LogInfo("a{0}{{{1}}}", "11", "22");
            output.AssertLastMessageEndsWith("a11{22}");
        }
    }

    [TestMethod]
    public void ConsoleLogger_Verbosity()
    {
        var logger = new ConsoleLogger(includeTimestamp: false);
        logger.Verbosity.Should().Be(LoggerVerbosity.Debug, "Default verbosity should be Debug");

        using (var output = new OutputCaptureScope())
        {
            logger.Verbosity = LoggerVerbosity.Info;
            logger.LogInfo("info1");
            output.AssertExpectedLastMessage("info1");
            logger.LogInfo("info2");
            output.AssertExpectedLastMessage("info2");
            logger.LogDebug("debug1");
            output.AssertExpectedLastMessage("info2"); // the debug message should not have been logged

            logger.Verbosity = LoggerVerbosity.Debug;
            logger.LogDebug("debug");
            output.AssertExpectedLastMessage("debug");
            logger.LogInfo("info3");
            output.AssertExpectedLastMessage("info3");

            logger.Verbosity = LoggerVerbosity.Info;
            logger.LogInfo("info4");
            output.AssertExpectedLastMessage("info4");
            logger.LogDebug("debug2");
            output.AssertExpectedLastMessage("info4");
        }
    }

    [TestMethod]
    public void ConsoleLogger_SuspendAndResume()
    {
        const string prefixRegex = @"\d{2}:\d{2}:\d{2}(.\d{1,3})?";
        var recorder = new OutputRecorder();
        var logger = ConsoleLogger.CreateLoggerForTesting(true, recorder, fileWrapper);

        // 1. Suspend output - should be able to call this multiple times
        logger.SuspendOutput();
        logger.SuspendOutput();
        logger.SuspendOutput();
        recorder.AssertNoOutput();

        // 2. Resume output when no messages written -> no output, no error
        logger.ResumeOutput();
        recorder.AssertNoOutput();

        // 3. Suspend and log some output
        logger.Verbosity = LoggerVerbosity.Info;
        logger.SuspendOutput();
        logger.LogDebug("debug 1 {0}", "xxx");
        logger.LogWarning("warning 1 {0}", "xxx");
        logger.LogError("error 1 {0}", "xxx");
        logger.LogInfo("info 1 {0}", "xxx");

        recorder.AssertNoOutput();

        // 4. Resume -> check expected output and ordering
        // The verbosity should be checked when ResumeOutput is called, not
        // when the message is originally logged.
        logger.Verbosity = LoggerVerbosity.Debug;
        logger.ResumeOutput();

        recorder.AssertExpectedOutputText(
            $"{prefixRegex}  debug 1 xxx",
            $"{prefixRegex}  WARNING: warning 1 xxx",
            $"{prefixRegex}  error 1 xxx",
            $"{prefixRegex}  info 1 xxx");

        // 5. Log more -> output should be immediate
        logger.LogInfo("info 2");
        recorder.AssertExpectedLastOutput($"{prefixRegex}  info 2", Console.ForegroundColor, false);

        logger.LogError("error 2");
        recorder.AssertExpectedLastOutput($"{prefixRegex}  error 2", ConsoleLogger.ErrorColor, true);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void ConsoleLogger_WriteUIWarnings_GenerateFile()
    {
        const string prefixRegex = @"\d{2}:\d{2}:\d{2}(.\d{1,3})?  WARNING:";
        using var output = new OutputCaptureScope();
        var logger = new ConsoleLogger(includeTimestamp: true, fileWrapper);

        logger.LogUIWarning("uiWarn1");
        output.AssertExpectedLastMessageRegex($"{prefixRegex} uiWarn1");

        logger.LogUIWarning("uiWarn2", null);
        output.AssertExpectedLastMessageRegex($"{prefixRegex} uiWarn2");

        logger.LogUIWarning("uiWarn3 {0}", "xxx");
        output.AssertExpectedLastMessageRegex($"{prefixRegex} uiWarn3 xxx");

        const string outputDir = "outputDir";
        logger.WriteUIWarnings(outputDir); // this should not contain any timestamps.
        fileWrapper.Received(1).WriteAllText(
             Path.Combine(outputDir, FileConstants.UIWarningsFileName),
             """
             [
               {
                 "text": "uiWarn1"
               },
               {
                 "text": "uiWarn2"
               },
               {
                 "text": "uiWarn3 xxx"
               }
             ]
             """);
    }

    [TestMethod]
    public void ILogger_Log_Debug()
    {
        var logger = Substitute.For<ILogger>();
        logger.Log(LoggerVerbosity.Debug, "message1");
        logger.ReceivedCalls().Should().ContainSingle();
        logger.Received(1).LogDebug("message1");
    }

    [TestMethod]
    public void ILogger_Log_Info()
    {
        var logger = Substitute.For<ILogger>();
        logger.Log(LoggerVerbosity.Info, "message1");
        logger.ReceivedCalls().Should().ContainSingle();
        logger.Received(1).LogInfo("message1");
    }

    [TestMethod]
    public void ILogger_Log_InfoAndDebug()
    {
        var logger = Substitute.For<ILogger>();
        logger.Log(LoggerVerbosity.Info, "info message");
        logger.Log(LoggerVerbosity.Debug, "debug message");
        logger.ReceivedCalls().Should().HaveCount(2);
        logger.Received(1).LogInfo("info message");
        logger.Received(1).LogDebug("debug message");
    }

    [TestMethod]
    public void ILogger_Log_UnknownLogVerbosity()
    {
#if NETFRAMEWORK
        var expectedMessage = """
            Unsupported log level.
            Parameter name: level
            Actual value was 100.
            """;
#else
        var expectedMessage = """
            Unsupported log level. (Parameter 'level')
            Actual value was 100.
            """;
#endif
        var logger = Substitute.For<ILogger>();
        logger.Invoking(x => x.Log((LoggerVerbosity)100, "message")).Should().ThrowExactly<ArgumentOutOfRangeException>().WithMessage(expectedMessage);
    }
}
