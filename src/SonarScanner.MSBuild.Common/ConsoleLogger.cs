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
using System.Globalization;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Simple logger implementation that logs output to the console
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

    private readonly IOutputWriter outputWriter;

    private readonly IFileWrapper fileWrapper = FileWrapper.Instance;

    private bool isOutputSuspended = false;

    /// <summary>
    /// List of messages that have not been output to the console.
    /// </summary>
    private IList<Message> suspendedMessages;
    
    /// <summary>
    /// Indicates whether logged messages should be prefixed with timestamps or not.
    /// </summary>
    public bool IncludeTimestamp { get; set; }

    public ConsoleLogger(bool includeTimestamp)
        : this(includeTimestamp, new ConsoleWriter())
    {
    }

    private ConsoleLogger(bool includeTimestamp, IOutputWriter writer)
    {
        IncludeTimestamp = includeTimestamp;
        Verbosity = DefaultVerbosity;
        outputWriter = writer;
    }

    /// <summary>
    /// Use only for testing.
    /// </summary>
    public static ConsoleLogger CreateLoggerForTesting(bool includeTimestamp, IOutputWriter writer) =>
        new(includeTimestamp, writer);

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
        Write(MessageType.Warning, GetFormattedMessage(Resources.Logger_WarningPrefix + message, args));

    public void LogError(string message, params object[] args) =>
        Write(MessageType.Error, message, args);

    public void LogDebug(string message, params object[] args) =>
        Write(MessageType.Debug, message, args);

    public void LogInfo(string message, params object[] args) =>
        Write(MessageType.Info, message, args);

    public void LogUIWarning(string message, params object[] args)
    {
        var formattedMessage = GetFormattedMessage(message, args);
        uiWarnings.Add(formattedMessage);
        LogWarning(formattedMessage);
    }

    public void CreateUIWarningFile(string outputFolder)
    {
        if (uiWarnings.Any())
        {
            var warningsJson = JsonConvert.SerializeObject(uiWarnings.Select(x => new { text = x }).ToArray(), Formatting.Indented);
            fileWrapper.WriteAllText(Path.Combine(outputFolder, FileConstants.UIWarningsFileName), warningsJson);
        }
    }

    public LoggerVerbosity Verbosity
    {
        get; set;
    }

    private string GetFormattedMessage(string message, params object[] args)
    {
        var finalMessage = message;
        if (args is not null && args.Length > 0)
        {
            finalMessage = string.Format(CultureInfo.CurrentCulture, finalMessage ?? string.Empty, args);
        }

        if (IncludeTimestamp)
        {
            finalMessage = string.Format(CultureInfo.CurrentCulture, "{0}  {1}", System.DateTime.Now.ToString("HH:mm:ss.FFF",
                CultureInfo.InvariantCulture), finalMessage);
        }
        return finalMessage;
    }

    private void FlushOutput()
    {
        Debug.Assert(isOutputSuspended, "Not expecting FlushOutput to be called unless output is currently suspended");
        Debug.Assert(suspendedMessages is not null, "suspendedMessages should be non-null if output is suspended");

        isOutputSuspended = false;

        foreach (var message in suspendedMessages)
        {
            Write(message.MessageType, message.FinalMessage);
        }

        suspendedMessages = null;
    }

    /// <summary>
    /// Either writes the message to the output stream, or records it
    /// if output is currently suspended
    /// </summary>
    private void Write(MessageType messageType, string message, params object[] args)
    {
        var finalMessage = GetFormattedMessage(message, args);

        if (isOutputSuspended)
        {
            suspendedMessages.Add(new Message(messageType, finalMessage));
        }
        else
        {
            // We always write out info, warnings and errors, but only write debug message
            // if the verbosity is Debug.
            if (messageType != MessageType.Debug || (Verbosity == LoggerVerbosity.Debug))
            {
                var textColor = GetConsoleColor(messageType);

                Debug.Assert(outputWriter is not null, "OutputWriter should not be null");
                outputWriter.WriteLine(finalMessage, textColor, messageType == MessageType.Error);
            }
        }
    }

    private ConsoleColor GetConsoleColor(MessageType messageType) =>
        messageType switch
        {
            MessageType.Debug => DebugColor,
            MessageType.Warning => WarningColor,
            MessageType.Error => ErrorColor,
            _ => Console.ForegroundColor,
        };

    private class Message
    {
        public MessageType MessageType { get; }
        public string FinalMessage { get; }

        public Message(MessageType messageType, string finalMessage)
        {
            MessageType = messageType;
            FinalMessage = finalMessage;
        }
    }
}
