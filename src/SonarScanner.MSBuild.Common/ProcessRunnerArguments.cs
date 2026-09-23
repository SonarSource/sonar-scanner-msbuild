/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

using System.Threading;

namespace SonarScanner.MSBuild.Common;

public enum LogLevel
{
    None,
    Info,
    Warning,
    Error
}

public readonly record struct LogMessage(LogLevel Level, string Message);

public delegate LogMessage? OutputToLogMessage(bool stdOut, string outputLine);

/// <summary>
/// Data class containing parameters required to execute a new process.
/// </summary>
public class ProcessRunnerArguments
{
    public string ExeName { get; }

    /// <summary>
    /// Non-sensitive command line arguments (i.e. ones that can safely be logged). Optional.
    /// </summary>
    public IReadOnlyList<Argument> CmdLineArgs { get; set; }

    public string WorkingDirectory { get; set; }

    public int TimeoutInMilliseconds { get; set; }

    public bool LogOutput { get; set; } = true;

    public string EscapedArguments => CmdLineArgs is null ? null : string.Join(" ", CmdLineArgs.Select(x => x.EscapeArgument()));

    /// <summary>
    /// Additional environments variables that should be set/overridden for the process. Can be null.
    /// </summary>
    public IDictionary<string, string> EnvironmentVariables { get; set; }

    public OutputToLogMessage OutputToLogMessage { get; set; }

    public string StandardInput { get; set; }

    /// <summary>
    /// Specifies that <see cref="ProcessRunner"/> checks whether the <see cref="ExeName"/> file can be found via <see cref="File.Exists(string)"/>.
    /// Turn this off, if the <see cref="ExeName"/> can also be resolved via <c>%PATH%</c> lookups.
    /// See also <seealso href="https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-diagnostics-processstartinfo-useshellexecute#workingdirectory">
    /// ProcessStartInfo remarks about %PATH% lookup.
    /// </seealso>.
    /// </summary>
    /// <remarks>
    /// Default: <see langword="true"/>.
    /// </remarks>
    public bool ExeMustExists { get; set; } = true;

    public ProcessRunnerArguments(string exeName)
    {
        Contract.ThrowIfNullOrWhitespace(exeName, nameof(exeName));
        ExeName = exeName;
        TimeoutInMilliseconds = Timeout.Infinite;
        OutputToLogMessage = (stdOut, outputLine) =>
        {
            var logLevel = stdOut
                ? LogLevel.Info
                : LogLevel.Error;
            logLevel = logLevel == LogLevel.Error && outputLine.StartsWith("WARN")
                ? LogLevel.Warning
                : logLevel;
            return new(logLevel, outputLine);
        };
    }

    /// <summary>
    /// Returns the string that should be used when logging command line arguments
    /// (sensitive data will have been removed).
    /// </summary>
    public string AsLogText()
    {
        if (CmdLineArgs is null)
        {
            return null;
        }

        var hasSensitiveData = false;

        var sb = new StringBuilder();

        foreach (var arg in CmdLineArgs.Select(x => x.Value))
        {
            if (ContainsSensitiveData(arg))
            {
                hasSensitiveData = true;
            }
            else
            {
                sb.Append(arg);
                sb.Append(" ");
            }
        }

        if (hasSensitiveData)
        {
            sb.Append(Resources.MSG_CmdLine_SensitiveCmdLineArgsAlternativeText);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Determines whether the text contains sensitive data that
    /// should not be logged/written to file.
    /// </summary>
    public static bool ContainsSensitiveData(string text)
    {
        Debug.Assert(SonarProperties.SensitivePropertyKeys is not null, "SensitiveDataMarkers array should not be null");

        if (text is null)
        {
            return false;
        }

        return SonarProperties.SensitivePropertyKeys.Any(x => text.IndexOf(x, StringComparison.OrdinalIgnoreCase) > -1);
    }

    public readonly record struct Argument(string Value, bool Escaped = false)
    {
        /// <summary>
        /// The CreateProcess Win32 API call only takes 1 string for all arguments.
        /// Ultimately, it is the responsibility of each program to decide how to split this string into multiple arguments.
        ///
        /// See:
        /// https://blogs.msdn.microsoft.com/oldnewthing/20100917-00/?p=12833/
        /// https://blogs.msdn.microsoft.com/twistylittlepassagesallalike/2011/04/23/everyone-quotes-command-line-arguments-the-wrong-way/
        /// http://www.daviddeley.com/autohotkey/parameters/parameters.htm.
        /// </summary>
        public string EscapeArgument()
        {
            if (Escaped)
            {
                return Value;
            }
            var sb = new StringBuilder(capacity: Value.Length + 2);
            sb.Append("\"");

            for (var i = 0; i < Value.Length; i++)
            {
                var numberOfBackslashes = 0;
                for (; i < Value.Length && Value[i] == '\\'; i++)
                {
                    numberOfBackslashes++;
                }

                if (i == Value.Length)
                {
                    // Escape all backslashes, but let the terminating
                    // double quotation mark we add below be interpreted
                    // as a meta-character.
                    sb.Append('\\', numberOfBackslashes * 2);
                }
                else if (Value[i] == '"')
                {
                    // Escape all backslashes and the following
                    // double quotation mark.
                    sb.Append('\\', numberOfBackslashes * 2 + 1);
                    sb.Append(Value[i]);
                }
                else
                {
                    // Backslashes aren't special here.
                    sb.Append('\\', numberOfBackslashes);
                    sb.Append(Value[i]);
                }
            }
            sb.Append("\"");

            return sb.ToString();
        }
    }
}
