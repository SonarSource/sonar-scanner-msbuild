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

public class TestRuntime : IRuntime
{
    public OperatingSystemProvider OperatingSystem { get; init; }
    public TestLogger Logger { get; init; }
    public IFileWrapper File { get; init; }
    public IDirectoryWrapper Directory { get; init; }
    ILogger IRuntime.Logger => Logger;

    public TestRuntime()
    {
        Logger = new();
        File = Substitute.For<IFileWrapper>();
        Directory = Substitute.For<IDirectoryWrapper>();
        OperatingSystem = Substitute.For<OperatingSystemProvider>(File, Logger);
    }
}
