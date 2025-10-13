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

using System.Runtime.InteropServices;

namespace SonarScanner.MSBuild.Common;

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
        const int maxPath = 260;
        const uint bufferSize = 256;
        const string ExtendedPathLengthSpecifier = @"\\?\"; // https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation
        if (path.Length < maxPath || os is not PlatformOS.Windows)
        {
            return path;
        }
        var longPath = path;
        if (!longPath.StartsWith(ExtendedPathLengthSpecifier))
        {
            longPath = ExtendedPathLengthSpecifier + longPath;
        }
        longPath = longPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar); // Windows API does not like forward slashes
        var shortNameBuffer = new StringBuilder((int)bufferSize);
        uint shortNameLength;
        try
        {
            shortNameLength = GetShortPathName(longPath, shortNameBuffer, bufferSize);
        }
        catch
        {
            return path;
        }
        if (shortNameLength != shortNameBuffer.Length || shortNameLength == 0) // https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getshortpathnamew#return-value
        {
            return path;
        }
        var result = shortNameBuffer.ToString();
        result = result.StartsWith(ExtendedPathLengthSpecifier)
            ? result.Substring(ExtendedPathLengthSpecifier.Length)
            : result;
        return result;
    }

    // https://www.pinvoke.net/default.aspx/kernel32.getshortpathname
    // https://github.com/terrafx/terrafx.interop.windows/blob/61ac1e963e14aa75e224dfa7554e01525ab65e74/sources/Interop/Windows/Windows/um/fileapi/Windows.cs#L281-L284
    [DllImport("KERNEL32.dll", ExactSpelling = true, EntryPoint = "GetShortPathNameW", SetLastError = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetShortPathName(
        [MarshalAs(UnmanagedType.LPTStr)] string lpszLongPath,
        [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpszShortPath,
        uint cchBuffer);
}
