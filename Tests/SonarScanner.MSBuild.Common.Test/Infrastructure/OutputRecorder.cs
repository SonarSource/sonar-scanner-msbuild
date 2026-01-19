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

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;

namespace SonarScanner.MSBuild.Common.Test;

/// <summary>
/// Test implementation of <see cref="IOutputWriter"/> that records the output messages
/// </summary>
internal class OutputRecorder : IOutputWriter
{
    private readonly List<OutputMessage> outputMessages = [];

    #region Checks

    public void AssertNoOutput() =>
        outputMessages.Should().BeEmpty("Not expecting any output to have been written to the console");

    public void AssertExpectedLastOutput(string regex, ConsoleColor textColor, bool isError)
    {
        outputMessages.Should().NotBeEmpty("Expecting some output to have been written to the console");

        var lastMessage = outputMessages[outputMessages.Count - 1];
        lastMessage.Message.Should().MatchRegex(regex, "Unexpected message content");
        lastMessage.TextColor.Should().Be(textColor, "Unexpected text color");
        lastMessage.IsError.Should().Be(isError, "Unexpected output stream");
    }

    public void AssertExpectedOutputText(params string[] regexes)
    {
        outputMessages.Should().HaveCount(regexes.Length);

        foreach (var pair in outputMessages.Zip(regexes, (message, regex) => (message, regex)))
        {
            pair.message.Message.Should().MatchRegex(pair.regex, "Unexpected output message");
        }
    }

    #endregion Checks

    #region IOutputWriter methods

    public void WriteLine(string message, ConsoleColor color, bool isError)
    {
        outputMessages.Add(new OutputMessage(message, color, isError));

        // Dump to the console to assist debugging
        Console.WriteLine("IsError: {0}, TextColor: {1}, Message: {2}", isError, color.ToString(), message);
    }

    #endregion IOutputWriter methods

    private class OutputMessage
    {
        public OutputMessage(string message, ConsoleColor textColor, bool isError)
        {
            Message = message;
            TextColor = textColor;
            IsError = isError;
        }

        public string Message { get; }
        public ConsoleColor TextColor { get; }
        public bool IsError { get; }
    }
}
