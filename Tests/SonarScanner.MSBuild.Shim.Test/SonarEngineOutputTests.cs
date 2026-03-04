/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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
public class SonarEngineOutputTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow("""{"message":"A trace","level":"TRACE"}""", LogLevel.Info, "TRACE: A trace")]
    [DataRow("""{"message":"A debug","level":"DEBUG"}""", LogLevel.Info, "DEBUG: A debug")]
    [DataRow("""{"message":"An info","level":"INFO"}""", LogLevel.Info, "INFO: An info")]
    [DataRow("""{"message":"A warning","level":"WARN"}""", LogLevel.Warning, "WARN: A warning")]
    [DataRow("""{"message":"An error","level":"ERROR"}""", LogLevel.Error, "ERROR: An error")]
    [DataRow("""{"MESSAGE":"An error","Level":"ERROR"}""", LogLevel.Error, "ERROR: An error")]
    public void OutputToLogMessage_ParsesJsonAndMapsLevel(string json, LogLevel expectedLevel, string expectedMessage)
    {
        var result = SonarEngineOutput.OutputToLogMessage(true, json);

        var logMessage = result.Should().NotBeNull().And.BeAssignableTo<LogMessage>().Which;
        logMessage.Level.Should().Be(expectedLevel);
        logMessage.Message.Should().Be(expectedMessage);
    }

    [TestMethod]
    public void OutputToLogMessage_ParsesStacktrace()
    {
        var result = SonarEngineOutput.OutputToLogMessage(true, """{"Message":"Error with stack","Level":"ERROR","Stacktrace":"stacktrace details"}""");

        var logMessage = result.Should().NotBeNull().And.BeAssignableTo<LogMessage>().Which;
        logMessage.Level.Should().Be(LogLevel.Error);
        logMessage.Message.Should().Be("""
            ERROR: Error with stack
            stacktrace details
            """.ToEnvironmentLineEndings());
    }

    [TestMethod]
    [DataRow("""not a json""")]
    [DataRow("""{}""")]
    [DataRow("""{OtherProp: 1}""")]
    [DataRow("""{Message: "Message"}""")]
    [DataRow("""{Level: "WARN"}""")]
    [DataRow("""{Message: "Message", Level: "UNKNOWN"}""")]
    [DataRow("""{Message: null, Level: "WARN"}""")]
    public void OutputToLogMessage_ReturnsInfoOnJsonException(string invalidJson)
    {
        var result = SonarEngineOutput.OutputToLogMessage(true, invalidJson);

        var logMessage = result.Should().NotBeNull().And.BeAssignableTo<LogMessage>().Which;
        logMessage.Level.Should().Be(LogLevel.Info);
        logMessage.Message.Should().Be(invalidJson);
    }

    [TestMethod]
    [DataRow("Just some text")]
    [DataRow("""{"Message":"Error with stack","Level":"ERROR","Stacktrace":"stacktrace details"}""")]
    public void OutputToLogMessage_StdErr_ReturnsErrorDoesNotParseJson(string line)
    {
        var result = SonarEngineOutput.OutputToLogMessage(false, line);

        var logMessage = result.Should().NotBeNull().And.BeAssignableTo<LogMessage>().Which;
        logMessage.Level.Should().Be(LogLevel.Error);
        logMessage.Message.Should().Be(line);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void OutputToLogMessage_NullOutputLine_ReturnsNull(bool stdOut)
    {
        // null is delivered by DataReceivedEventArgs.Data when the process stream closes (EOF signal)
        var result = SonarEngineOutput.OutputToLogMessage(stdOut, null);

        result.Should().BeNull();
    }

    [TestMethod]
    // JsonConvert.DeserializeObject<T>("") and DeserializeObject<T>("null") return null instead of throwing JsonException.
    // This happens e.g. when the scanner jar emits an empty line (observed with logback StatusPrinter output).
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("null")]
    public void OutputToLogMessage_DeserializesToNull_ReturnsInfo(string outputLine)
    {
        var result = SonarEngineOutput.OutputToLogMessage(true, outputLine);

        var logMessage = result.Should().NotBeNull().And.BeAssignableTo<LogMessage>().Which;
        logMessage.Level.Should().Be(LogLevel.Info);
        logMessage.Message.Should().Be(outputLine);
    }

    [TestMethod]
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    public void ProcRunner_SonarEngineOutputDelegate_MixedOutput_HandledGracefully()
    {
        // Verifies that non-JSON and empty lines mixed in with valid JSON log output are all handled
        // gracefully end-to-end: ProcessRunner → SonarEngineOutput.OutputToLogMessage → logged without crashing.
        var missingBrace = @"{""message"":""Missing brace"",""level"":""INFO""";
        var missingProperty = """{"level":"INFO"}""";
        var script = $"""
            @echo off
            @echo {"""{"message":"First message","level":"INFO"}"""}
            @echo.
            @echo {missingBrace}
            @echo(
            @echo {missingProperty}
            @echo null
            @echo {"""{"message":"Last message","level":"WARN"}"""}
            """;
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var exePath = TestUtils.WriteExecutableScriptForTest(TestContext, script);
        var runtime = new TestRuntime();
        runtime.File.ShortName(Arg.Any<PlatformOS>(), Arg.Any<string>()).Returns(x => x[1]);
        var runner = new ProcessRunner(runtime);
        var processArgs = new ProcessRunnerArguments(exePath, isBatchScript: true)
        {
            WorkingDirectory = testDir,
            OutputToLogMessage = SonarEngineOutput.OutputToLogMessage
        };

        runner.Execute(processArgs);

        runtime.Logger.Should()
            .HaveInfos("INFO: First message", string.Empty, missingBrace, missingProperty, "null")
            .And.HaveWarnings("WARN: Last message");
    }
}
