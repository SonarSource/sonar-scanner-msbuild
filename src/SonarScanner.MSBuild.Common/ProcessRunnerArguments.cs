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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace SonarScanner.MSBuild.Common;

public enum LogLevel
{
    Info,
    Warning,
    Error
}

public readonly record struct LogMessage(LogLevel Level, string Message);

public delegate LogMessage? OutputToLogMessage(bool stdOut, string outputLine);

/// <summary>
/// Data class containing parameters required to execute a new process
/// </summary>
public class ProcessRunnerArguments
{
    public string ExeName { get; }

    /// <summary>
    /// Non-sensitive command line arguments (i.e. ones that can safely be logged). Optional.
    /// </summary>
    public IEnumerable<string> CmdLineArgs { get; set; }

    public string WorkingDirectory { get; set; }

    public int TimeoutInMilliseconds { get; set; }

    public bool LogOutput { get; set; } = true;

    public string EscapedArguments
    {
        get
        {
            if (CmdLineArgs is null)
            {
                return null;
            }

            var result = string.Join(" ", CmdLineArgs.Select(EscapeArgument));

            if (IsBatchScript)
            {
                result = ShellEscape(result);
            }

            return result;
        }
    }

    /// <summary>
    /// Additional environments variables that should be set/overridden for the process. Can be null.
    /// </summary>
    public IDictionary<string, string> EnvironmentVariables { get; set; }

    public OutputToLogMessage OutputToLogMessage { get; set; }

    private bool IsBatchScript { get; set; }

    public ProcessRunnerArguments(string exeName, bool isBatchScript)
    {
        if (string.IsNullOrWhiteSpace(exeName))
        {
            throw new ArgumentNullException(nameof(exeName));
        }

        ExeName = exeName;
        IsBatchScript = isBatchScript;

        TimeoutInMilliseconds = Timeout.Infinite;
        OutputToLogMessage = (stdOut, outputLine) =>
        {
            if (stdOut)
            {
                // It's important to log this as an important message because
                // this the log redirection pipeline of the child process
                return new(LogLevel.Info, outputLine);
            }
            else
            {
                return outputLine.StartsWith("WARN") ? new(LogLevel.Warning, outputLine) : new(LogLevel.Error, outputLine);
            }
        };
    }

    /// <summary>
    /// Returns the string that should be used when logging command line arguments
    /// (sensitive data will have been removed)
    /// </summary>
    public string AsLogText()
    {
        if (CmdLineArgs is null)
        {
            return null;
        }

        var hasSensitiveData = false;

        var sb = new StringBuilder();

        foreach (var arg in CmdLineArgs)
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
    /// should not be logged/written to file
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

    /// <summary>
    /// The CreateProcess Win32 API call only takes 1 string for all arguments.
    /// Ultimately, it is the responsibility of each program to decide how to split this string into multiple arguments.
    ///
    /// See:
    /// https://blogs.msdn.microsoft.com/oldnewthing/20100917-00/?p=12833/
    /// https://blogs.msdn.microsoft.com/twistylittlepassagesallalike/2011/04/23/everyone-quotes-command-line-arguments-the-wrong-way/
    /// http://www.daviddeley.com/autohotkey/parameters/parameters.htm
    /// </summary>
    private static string EscapeArgument(string arg)
    {
        Debug.Assert(arg is not null, "Not expecting an argument to be null");

        var sb = new StringBuilder();

        sb.Append("\"");
        for (var i = 0; i < arg.Length; i++)
        {
            var numberOfBackslashes = 0;
            for (; i < arg.Length && arg[i] == '\\'; i++)
            {
                numberOfBackslashes++;
            }

            if (i == arg.Length)
            {
                // Escape all backslashes, but let the terminating
                // double quotation mark we add below be interpreted
                // as a meta-character.
                sb.Append('\\', numberOfBackslashes * 2);
            }
            else if (arg[i] == '"')
            {
                // Escape all backslashes and the following
                // double quotation mark.
                sb.Append('\\', numberOfBackslashes * 2 + 1);
                sb.Append(arg[i]);
            }
            else
            {
                // Backslashes aren't special here.
                sb.Append('\\', numberOfBackslashes);
                sb.Append(arg[i]);
            }
        }
        sb.Append("\"");

        return sb.ToString();
    }

    /// <summary>
    /// Batch scripts are evil.
    /// The escape character in batch is '^'.
    ///
    /// Example:
    /// script.bat : echo %*
    /// cmd.exe: script.bat foo^>out.txt
    ///
    /// This passes the argument "foo >out.txt" to script.bat.
    /// Variable expansion happen before execution (i.e. it is preprocessing), so the script becomes:
    ///
    /// echo foo>out.txt
    ///
    /// which will write "foo" into the file "out.txt"
    ///
    /// To avoid this, one must call:
    /// cmd.exe: script.bat foo^^^>out.txt
    ///
    /// which gets rewritten into: echo foo^>out.txt
    /// and then executed.
    ///
    /// Note: Delayed expansion is not available for %*, %1
    /// set foo=%* and set foo="%*" with echo !foo!
    /// will only move the command injection away from the "echo" to the "set" itself.
    /// </summary>
    private static string ShellEscape(string argLine)
    {
        var sb = new StringBuilder();
        foreach (var c in argLine)
        {
            // This escape is required after %* is expanded to prevent command injections
            sb.Append('^');
            sb.Append('^');

            // This escape is required only to pass the argument line to the batch script
            sb.Append('^');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
