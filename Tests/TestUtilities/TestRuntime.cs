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

using NSubstitute;

namespace TestUtilities;

/// <summary>
/// Mock for IRuntime that contains configurable mocks for its properties.
/// </summary>
public record TestRuntime : IRuntime
{
    /// <summary>
    /// A Substitute for OperatingSystemProvider.
    /// </summary>
    public OperatingSystemProvider OperatingSystem { get; init; }

    /// <summary>
    /// A Substitute for IDirectoryWrapper.
    /// </summary>
    public IDirectoryWrapper Directory { get; init; } = Substitute.For<IDirectoryWrapper>();

    /// <summary>
    /// A Substitute for IFileWrapper.
    /// </summary>
    public IFileWrapper File { get; init; } = Substitute.For<IFileWrapper>();

    public TestLogger Logger { get; init; } = new();

    public TestTelemetry Telemetry { get; init; } = new();

    ILogger IRuntime.Logger => Logger;

    ITelemetry IRuntime.Telemetry => Telemetry;

    public TestRuntime() =>
        OperatingSystem = Substitute.For<OperatingSystemProvider>(File, Logger);

    public void ConfigureOS(PlatformOS os) =>
        OperatingSystem.OperatingSystem().Returns(os);

    public void LogDebug(string message, params object[] args) =>
        Logger.LogDebug(message, args);

    public void LogInfo(string message, params object[] args) =>
        Logger.LogInfo(message, args);

    public void LogWarning(string message, params object[] args) =>
        Logger.LogWarning(message, args);

    public void LogError(string message, params object[] args) =>
        Logger.LogError(message, args);
}
