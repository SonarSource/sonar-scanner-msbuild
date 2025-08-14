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

public interface IDirectoryWrapper
{
    /// <inheritdoc cref="Directory.CreateDirectory(string)"/>
    void CreateDirectory(string path);

    /// <inheritdoc cref="Directory.Delete(string, bool)"/>
    void Delete(string path, bool recursive);

    /// <inheritdoc cref="Directory.Exists(string)"/>
    bool Exists(string path);

    /// <inheritdoc cref="Directory.GetDirectories(string, string, SearchOption)"/>
    string[] GetDirectories(string path, string searchPattern, SearchOption searchOption);

    /// <inheritdoc cref="Directory.GetFiles(string, string)"/>
    string[] GetFiles(string path, string searchPattern);

    /// <inheritdoc cref="Directory.GetFiles(string, string, SearchOption)"/>
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);

    /// <inheritdoc cref="Path.GetRandomFileName()"/>
    string GetRandomFileName();

    /// <inheritdoc cref="Directory.Move(string, string)"/>
    void Move(string sourceDirName, string destDirName);

    /// <inheritdoc cref="DirectoryInfo.EnumerateFiles(string, SearchOption)"/>
    IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo path, string searchPattern, SearchOption searchOption);

    /// <inheritdoc cref="DirectoryInfo.EnumerateDirectories(string, SearchOption)"/>
    IEnumerable<DirectoryInfo> EnumerateDirectories(DirectoryInfo path, string searchPattern, SearchOption searchOption);
}
