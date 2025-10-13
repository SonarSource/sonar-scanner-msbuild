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
using System.Runtime.InteropServices;

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

    public void CreateNewAllLines(string file, IEnumerable<string> enumerable, Encoding encoding)
    {
        using var fs = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var streamWriter = new StreamWriter(fs, encoding);
        foreach (var line in enumerable)
        {
            streamWriter.WriteLine(line);
        }
    }

    public string ShortName(PlatformOS os, string path)
    {
        if (os is not PlatformOS.Windows)
        {
            // Short names are a Windows concept
            return path;
        }
        const int MAX_PATH = 260;
        const uint BUFFER_SIZE = 256;
        if (path.Length < MAX_PATH)
        {
            return path;
        }

        if (!path.StartsWith(@"\\?\"))
        {
            path = @"\\?\" + path;
        }
        var shortNameBuffer = new StringBuilder((int)BUFFER_SIZE);
        GetShortPathName(path, shortNameBuffer, BUFFER_SIZE);
        return shortNameBuffer.ToString();
    }

    // https://www.pinvoke.net/default.aspx/kernel32.getshortpathname
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern uint GetShortPathName(
        [MarshalAs(UnmanagedType.LPTStr)] string lpszLongPath,
        [MarshalAs(UnmanagedType.LPTStr)]
        StringBuilder lpszShortPath,
        uint cchBuffer);
}
