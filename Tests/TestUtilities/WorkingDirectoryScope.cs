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

namespace TestUtilities;

/// <summary>
/// Defines a scope inside which the current directory is changed
/// to a specific value. The directory will be reset when the scope is disposed.
/// </summary>
/// <remarks>The location for the temporary analysis directory is based on the working directory.
/// This class provides a simple way to set the directory to a known location for the duration
/// of a test.</remarks>
public sealed class WorkingDirectoryScope : IDisposable
{
    private readonly string originalDirectory;

    public WorkingDirectoryScope(string workingDirectory)
    {
        Directory.Exists(workingDirectory).Should().BeTrue("Test setup error: specified directory should exist - " + workingDirectory);

        originalDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workingDirectory);
    }

    public void Dispose() =>
        Directory.SetCurrentDirectory(originalDirectory);
}
