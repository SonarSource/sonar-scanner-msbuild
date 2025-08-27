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

using Newtonsoft.Json;

namespace SonarScanner.MSBuild.Shim;

public class SonarEngineOutput
{
    private enum EngineLevel
    {
        TRACE,
        DEBUG,
        INFO,
        WARN,
        ERROR
    }

    public static LogMessage? OutputToLogMessage(bool stdOut, string outputLine)
    {
        if (stdOut)
        {
            try
            {
                var engineOutput = JsonConvert.DeserializeObject<EngineOutput>(outputLine);
                var logLevel = engineOutput.Level switch
                {
                    EngineLevel.WARN => LogLevel.Warning,
                    EngineLevel.ERROR => LogLevel.Error,
                    _ => LogLevel.Info
                };
                var message = engineOutput.Message;
                if (!string.IsNullOrWhiteSpace(engineOutput.Stacktrace))
                {
                    message += Environment.NewLine + engineOutput.Stacktrace;
                }
                return new(logLevel, message);
            }
            catch (JsonException)
            {
                return new LogMessage(LogLevel.Info, outputLine);
            }
        }
        return new LogMessage(LogLevel.Error, outputLine);
    }

    // https://xtranet-sonarsource.atlassian.net/wiki/spaces/CodeOrches/pages/3155001372/Scanner+Bootstrapping#Scanner-Engine-contract
    private sealed record EngineOutput
    {
        [JsonProperty(Required = Required.Always)]
        public string Message { get; set; }

        [JsonProperty(Required = Required.Always)]
        public EngineLevel Level { get; set; }

        [JsonProperty(Required = Required.Default)]
        public string Stacktrace { get; set; }
    }
}
