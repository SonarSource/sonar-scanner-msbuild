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

using System.Globalization;

namespace TestUtilities;

public class TestLogger : ILogger
{
    // All messages are normalized to Unix line endings, because Resx files contains multiline messages with CRLF and we emit mix of LF and CRFL to logs on *nix system
    public List<string> DebugMessages { get; }
    public List<string> InfoMessages { get; }
    public List<string> Warnings { get; }
    public List<string> Errors { get; }
    public LoggerVerbosity Verbosity { get; set; }
    public bool IncludeTimestamp { get; set; }

    public TestLogger()
    {
        // Write out a separator. Many tests create more than one TestLogger.
        // This helps separate the results of the different cases.
        WriteLine(string.Empty);
        WriteLine("------------------------------------------------------------- (new TestLogger created)");
        WriteLine(string.Empty);

        DebugMessages = [];
        InfoMessages = [];
        Warnings = [];
        Errors = [];

        Verbosity = LoggerVerbosity.Debug;
    }

    public void AssertVerbosity(LoggerVerbosity expected) =>
        Verbosity.Should().Be(expected, "Logger verbosity mismatch");

    public void LogInfo(string message, params object[] args)
    {
        InfoMessages.Add(FormatMessage(message, args));
        WriteLine("INFO: " + message, args);
    }

    public void LogWarning(string message, params object[] args)
    {
        Warnings.Add(FormatMessage(message, args));
        WriteLine("WARNING: " + message, args);
    }

    public void LogError(string message, params object[] args)
    {
        Errors.Add(FormatMessage(message, args));
        WriteLine("ERROR: " + message, args);
    }

    public void LogDebug(string message, params object[] args)
    {
        DebugMessages.Add(FormatMessage(message, args));
        WriteLine("DEBUG: " + message, args);
    }

    public void SuspendOutput()
    {
        // no-op
    }

    public void ResumeOutput()
    {
        // no-op
    }

    private static void WriteLine(string message, params object[] args) =>
        Console.WriteLine(FormatMessage(message, args));

    private static string FormatMessage(string message, params object[] args) =>
        args.Any()  // string.Format is not happy about logging json with { } in it
            ? string.Format(CultureInfo.CurrentCulture, message, args).ToUnixLineEndings()
            : message.ToUnixLineEndings();
}
