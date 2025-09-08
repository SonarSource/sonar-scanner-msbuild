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
using Newtonsoft.Json.Linq;

namespace SonarScanner.MSBuild.Common;

public class Telemetry : ITelemetry
{
    /// <summary>
    /// List of telemetry messages computed during the begin and end step.
    /// </summary>
    private readonly IList<KeyValuePair<string, object>> telemetryMessages = [];

    private readonly IFileWrapper fileWrapper;
    private readonly ILogger logger;

    public Telemetry(IFileWrapper fileWrapper, ILogger logger)
    {
        this.fileWrapper = fileWrapper;
        this.logger = logger;
    }

    /// <summary>
    /// Saves a telemetry message for later processing.
    /// The <paramref name="value"/> parameter must be a primitive JSON type, like a number, a String, or a Boolean.
    /// </summary>
    public void AddTelemetryMessage(string key, object value) =>
        telemetryMessages.Add(new(key, value));

    public void WriteTelemetry(string outputFolder)
    {
        var telemetryMessagesJson = new StringBuilder();
        foreach (var message in telemetryMessages)
        {
            telemetryMessagesJson.AppendLine(ParseMessage(message));
        }

        var path = Path.Combine(outputFolder, FileConstants.TelemetryFileName);
        var telemetry = telemetryMessagesJson.ToString();
        try
        {
            fileWrapper.AppendAllText(path, telemetry);
        }
        catch (IOException ex)
        {
            logger.LogWarning($"Could not write {FileConstants.TelemetryFileName} in {outputFolder}", ex.Message);
        }

        static string ParseMessage(KeyValuePair<string, object> message)
        {
            var entry = new JObject();
            var value = JToken.FromObject(message.Value);
            if (value is not JValue)
            {
                throw new NotSupportedException($"Unsupported telemetry message value type: {message.Value.GetType()}");
            }
            entry[message.Key] = value;
            return entry.ToString(Formatting.None);
        }
    }
}
