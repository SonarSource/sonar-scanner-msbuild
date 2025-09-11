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

[ExcludeFromCodeCoverage]
public class DirectoryWrapper : IDirectoryWrapper
{
    public static IDirectoryWrapper Instance { get; } = new DirectoryWrapper();

    private DirectoryWrapper() { }

    public void CreateDirectory(string path) =>
        Directory.CreateDirectory(path);

    public bool Exists(string path) =>
        Directory.Exists(path);

    public string GetCurrentDirectory() =>
        Directory.GetCurrentDirectory();

    public string[] GetDirectories(string path, string searchPattern, SearchOption searchOption) =>
        Directory.GetDirectories(path, searchPattern, searchOption);

    public string[] GetFiles(string path, string searchPattern) =>
        Directory.GetFiles(path, searchPattern);

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.GetFiles(path, searchPattern, searchOption);

    public void Move(string sourceDirName, string destDirName) =>
        Directory.Move(sourceDirName, destDirName);

    public void Delete(string path, bool recursive) =>
        Directory.Delete(path, recursive);

    public string GetRandomFileName() =>
        Path.GetRandomFileName();

    public IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo path, string searchPattern, SearchOption searchOption) =>
        path.EnumerateFiles(searchPattern, searchOption);

    public IEnumerable<DirectoryInfo> EnumerateDirectories(DirectoryInfo path, string searchPattern, SearchOption searchOption) =>
        path.EnumerateDirectories(searchPattern, searchOption);
}
