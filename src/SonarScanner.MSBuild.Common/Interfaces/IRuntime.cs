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

using System.Diagnostics.CodeAnalysis;

namespace SonarScanner.MSBuild.Common;

[ExcludeFromCodeCoverage]   // container class without any logic
public class Runtime : IRuntime
{
    public OperatingSystemProvider OperatingSystem { get; }
    public ILogger Logger { get; }
    public IFileWrapper File { get; }
    public IDirectoryWrapper Directory { get; }

    public Runtime(ILogger logger, IFileWrapper fileWrapper, IDirectoryWrapper directoryWrapper, OperatingSystemProvider operatingSystem)
    {
        Logger = logger;
        File = fileWrapper;
        Directory = directoryWrapper;
        OperatingSystem = operatingSystem;
    }
}
