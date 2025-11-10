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

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Level of detail for the log messages.
/// </summary>
/// <remarks>
/// Does not cover warnings and errors.
/// The levels are in step with the SonarQube verbosity levels (http://docs.sonarqube.org/display/SONAR/Server+Log+Management):
/// Info, Debug (for advanced logs), Trace (for advanced logs and logs that might have a perf impact).
/// </remarks>
public enum LoggerVerbosity
{
    /// <summary>
    /// Important messages that always get logged.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Advanced information messages that help in debugging scenarios.
    /// </summary>
    Debug = 1
}

public interface ILogger
{
    /// <summary>
    /// Gets or sets the level of detail to show in the log.
    /// </summary>
    LoggerVerbosity Verbosity { get; set; }

    /// <summary>
    /// Gets or sets whether log entries are prefixed with timestamps.
    /// </summary>
    bool IncludeTimestamp { get; set; }

    void LogDebug(string message, params object[] args);

    void LogInfo(string message, params object[] args);

    void LogWarning(string message, params object[] args);

    void LogError(string message, params object[] args);

    /// <summary>
    /// Suspends writing output to the console. Any messages will be recorded but
    /// not output unless and until <see cref="ResumeOutput"/> is called.
    /// </summary>
    void SuspendOutput();

    /// <summary>
    /// Writes out any messages that were recorded as a result of
    /// <see cref="SuspendOutput"/> having been called.
    /// Any subsequent messages will be output immediately.
    /// </summary>
    void ResumeOutput();
}
