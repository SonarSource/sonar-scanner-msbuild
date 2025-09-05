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

namespace SonarScanner.MSBuild.Common;

public class Runtime : IRuntime
{
    public OperatingSystemProvider OperatingSystem { get; }
    public IDirectoryWrapper Directory { get; }
    public IFileWrapper File { get; }
    public ILogger Logger { get; }

    public Runtime(OperatingSystemProvider operatingSystem, IDirectoryWrapper directoryWrapper, IFileWrapper fileWrapper, ILogger logger)
    {
        OperatingSystem = operatingSystem ?? throw new ArgumentNullException(nameof(operatingSystem));
        Directory = directoryWrapper ?? throw new ArgumentNullException(nameof(directoryWrapper));
        File = fileWrapper ?? throw new ArgumentNullException(nameof(fileWrapper));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void LogDebug(string message, params object[] args) =>
        Logger.LogDebug(message, args);

    public void LogInfo(string message, params object[] args) =>
        Logger.LogInfo(message, args);

    public void LogWarning(string message, params object[] args) =>
        Logger.LogWarning(message, args);

    public void LogError(string message, params object[] args) =>
        Logger.LogError(message, args);
}
