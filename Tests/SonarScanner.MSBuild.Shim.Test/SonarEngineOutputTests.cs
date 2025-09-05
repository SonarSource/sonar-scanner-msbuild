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
public class SonarEngineOutputTests
{
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
}
