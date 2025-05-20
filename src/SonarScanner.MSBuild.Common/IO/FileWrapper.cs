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
using System.IO;

namespace SonarScanner.MSBuild.Common;

[ExcludeFromCodeCoverage]
public class FileWrapper : IFileWrapper
{
    public static IFileWrapper Instance { get; } = new FileWrapper();

    private FileWrapper() { }

    public void Copy(string sourceFileName, string destFileName, bool overwrite) =>
        File.Copy(sourceFileName, destFileName, overwrite);

    public bool Exists(string path) =>
        File.Exists(path);

    public string ReadAllText(string path) =>
        File.ReadAllText(path);

    public void WriteAllText(string path, string contents) =>
        File.WriteAllText(path, contents);

    public void AppendAllText(string path, string contents) =>
        File.AppendAllText(path, contents);

    public Stream Open(string path) =>
        File.OpenRead(path);

    public Stream Create(string path) =>
        File.Create(path);

    public void Move(string sourceFileName, string destFileName) =>
        File.Move(sourceFileName, destFileName);

    public void Delete(string file) =>
        File.Delete(file);

    public void AppendAllLines(string file, IEnumerable<string> enumerable, Encoding encoding) =>
        File.AppendAllLines(file, enumerable, encoding);
}
