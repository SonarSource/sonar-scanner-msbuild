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

using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Simple logger implementation that logs output to the console.
/// </summary>
public class ConsoleLogger : ILogger
{
    public const ConsoleColor DebugColor = ConsoleColor.DarkCyan;
    public const ConsoleColor WarningColor = ConsoleColor.Yellow;
    public const ConsoleColor ErrorColor = ConsoleColor.Red;

    private const LoggerVerbosity DefaultVerbosity = VerbosityCalculator.InitialLoggingVerbosity;

    private enum MessageType
    {
        Info,
        Debug,
        Warning,
        Error
    }

    /// <summary>
    /// List of UI warnings that should be logged.
    /// </summary>
    private readonly IList<string> uiWarnings = [];

    /// <summary>
    /// List of telemetry messages computed during the begin and end step.
    /// </summary>
    private readonly IList<KeyValuePair<string, object>> telemetryMessages = [];

    private readonly IOutputWriter outputWriter;
    private readonly IFileWrapper fileWrapper;

    private bool isOutputSuspended = false;

    /// <summary>
    /// List of messages that have not been output to the console.
    /// </summary>
    private IList<Message> suspendedMessages;

    /// <summary>
    /// Indicates whether logged messages should be prefixed with timestamps or not.
    /// </summary>
    public bool IncludeTimestamp { get; set; }

    public LoggerVerbosity Verbosity { get; set; }

    public ConsoleLogger(bool includeTimestamp)
        : this(includeTimestamp, new ConsoleWriter(), FileWrapper.Instance)
    {
    }

    public ConsoleLogger(bool includeTimestamp, IFileWrapper fileWrapper)
        : this(includeTimestamp, new ConsoleWriter(), fileWrapper)
    {
    }

    private ConsoleLogger(bool includeTimestamp, IOutputWriter writer, IFileWrapper fileWrapper)
    {
        IncludeTimestamp = includeTimestamp;
        Verbosity = DefaultVerbosity;
        outputWriter = writer;
        this.fileWrapper = fileWrapper;
    }

    /// <summary>
    /// Use only for testing.
    /// </summary>
    public static ConsoleLogger CreateLoggerForTesting(bool includeTimestamp, IOutputWriter writer, IFileWrapper fileWrapper) =>
        new(includeTimestamp, writer, fileWrapper);

    public void SuspendOutput()
    {
        if (!isOutputSuspended)
        {
            isOutputSuspended = true;
            suspendedMessages = [];
        }
    }

    public void ResumeOutput()
    {
        if (isOutputSuspended)
        {
            FlushOutput();
        }
    }

    public void LogWarning(string message, params object[] args) =>
        Write(MessageType.Warning, Resources.Logger_WarningPrefix + message, args);

    public void LogError(string message, params object[] args) =>
        Write(MessageType.Error, message, args);

    public void LogDebug(string message, params object[] args) =>
        Write(MessageType.Debug, message, args);

    public void LogInfo(string message, params object[] args) =>
        Write(MessageType.Info, message, args);

    public void LogUIWarning(string message, params object[] args)
    {
        uiWarnings.Add(FormatMessage(message, args));
        LogWarning(message, args);
    }

    public void WriteUIWarnings(string outputFolder)
    {
        if (uiWarnings.Any())
        {
            var warningsJson = JsonConvert.SerializeObject(uiWarnings.Select(x => new { text = x }).ToArray(), Formatting.Indented);
            fileWrapper.WriteAllText(Path.Combine(outputFolder, FileConstants.UIWarningsFileName), warningsJson);
        }
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
        fileWrapper.AppendAllText(path, telemetry);

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

    private void FlushOutput()
    {
        Debug.Assert(isOutputSuspended, "Not expecting FlushOutput to be called unless output is currently suspended");
        Debug.Assert(suspendedMessages is not null, "suspendedMessages should be non-null if output is suspended");

        isOutputSuspended = false;
        foreach (var message in suspendedMessages)
        {
            WriteFormatted(message.MessageType, message.FinalMessage); // Messages have already been formatted before they end up here.
        }
        suspendedMessages = null;
    }

    /// <summary>
    /// Formats a message and writes or records it.
    /// </summary>
    private void Write(MessageType messageType, string message, params object[] args) =>
        WriteFormatted(messageType, FormatAndTimestampMessage(message, args));

    /// <summary>
    /// Either writes the message to the output stream, or records it
    /// if output is currently suspended.
    /// </summary>
    private void WriteFormatted(MessageType messageType, string formatted)
    {
        if (isOutputSuspended)
        {
            suspendedMessages.Add(new Message(messageType, formatted));
        }
        else
        {
            // We always write out info, warnings and errors, but only write debug message
            // if the verbosity is Debug.
            if (messageType != MessageType.Debug || (Verbosity == LoggerVerbosity.Debug))
            {
                var textColor = GetConsoleColor(messageType);

                Debug.Assert(outputWriter is not null, "OutputWriter should not be null");
                outputWriter.WriteLine(formatted, textColor, messageType == MessageType.Error);
            }
        }
    }

    private string FormatAndTimestampMessage(string message, params object[] args)
    {
        var formatted = FormatMessage(message, args);
        if (IncludeTimestamp)
        {
            formatted = string.Format(CultureInfo.CurrentCulture, "{0}  {1}", DateTime.Now.ToString("HH:mm:ss.FFF", CultureInfo.InvariantCulture), formatted);
        }
        return formatted;
    }

    private static string FormatMessage(string message, params object[] args) =>
        args is not null && args.Length > 0
            ? string.Format(CultureInfo.CurrentCulture, message ?? string.Empty, args)
            : message;

    private static ConsoleColor GetConsoleColor(MessageType messageType) =>
        messageType switch
        {
            MessageType.Debug => DebugColor,
            MessageType.Warning => WarningColor,
            MessageType.Error => ErrorColor,
            _ => Console.ForegroundColor,
        };

    private sealed class Message(MessageType messageType, string finalMessage)
    {
        public MessageType MessageType { get; } = messageType;
        public string FinalMessage { get; } = finalMessage;
    }
}
