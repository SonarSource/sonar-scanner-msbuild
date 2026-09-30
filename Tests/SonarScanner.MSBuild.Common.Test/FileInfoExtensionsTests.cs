/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class FileInfoExtensionsTests
{
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    [DataRow(@"C:\Src\File.cs", @"C:\Src\")]
    [DataRow(@"C:\Src\File.cs", @"C:\Src")]
    [DataRow(@"C:\Src\File.cs", @"C:\")]
    [DataRow(@"C:\SRC\FILE.CS", @"C:\src")]
    [DataRow(@"C:/Src/File.cs", @"C:/Src")]
    [DataRow(@"C:\Src\File.cs", @"C:/Src")]
    [DataRow(@"~Foo\File.cs", "~Foo")]
    [DataRow(@"C:\Src\Bar\..\File.cs", @"C:\Src")]
    [DataRow(@"C:\Src\File.cs", @"C:\Src\Bar\..")]
    [DataRow(@"C:\äöü\File.cs", @"C:\äöü")]
    [DataRow("C:\\Foo_\u00e4öü\\File.cs", "C:\\Foo_a\u0308öü")] // https://www.compart.com/en/unicode/U+00E4 = ä; https://www.compart.com/en/unicode/U+0308 = ̈ (combining diaeresis)
    [DataRow("C:\\Foo_\u00e4öü\\File.cs", "C:\\Foo_\u0041\u0308öü")] // https://www.compart.com/en/unicode/U+0041 = A
    public void IsInDirectory_True_Windows(string file, string directory) =>
        new FileInfo(file).IsInDirectory(new DirectoryInfo(directory)).Should().BeTrue();

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    [DataRow(@"C:\SrcFile.cs", @"C:\Src")]
    [DataRow(@"C:\Src\File.cs", @"C:\Src\Bar")]
    public void IsInDirectory_False_Windows(string file, string directory) =>
        new FileInfo(file).IsInDirectory(new DirectoryInfo(directory)).Should().BeFalse();

    [TestCategory(TestCategories.NoWindows)]
    [TestMethod]
    [DataRow(@"/mnt/c/Src/File.cs", @"/mnt/c/Src/")]
    [DataRow(@"/mnt/c/Src/File.cs", @"/mnt/c/Src")]
    [DataRow(@"/mnt/c/Src/File.cs", @"/mnt/c/")]
    [DataRow(@"/mnt/c/SRC/FILE.CS", @"/mnt/c/src")]
    [DataRow(@"~Foo/File.cs", "~Foo")]
    [DataRow(@"/mnt/c/Src/Bar/../File.cs", @"/mnt/c/Src")]
    [DataRow(@"/mnt/c/Src/File.cs", @"/mnt/c/Src/Bar/..")]
    public void IsInDirectory_True_Unix(string file, string directory) =>
        new FileInfo(file).IsInDirectory(new DirectoryInfo(directory)).Should().BeTrue();

    [TestCategory(TestCategories.NoWindows)]
    [TestMethod]
    [DataRow(@"/mnt/c/SrcFile.cs", @"/mnt/c/Src")]
    [DataRow(@"/mnt/c/Src/File.cs", @"/mnt/c/Src/Bar")]
    public void IsInDirectory_False_Unix(string file, string directory) =>
        new FileInfo(file).IsInDirectory(new DirectoryInfo(directory)).Should().BeFalse();

    [TestMethod]
    [DataRow("dir", StringComparison.Ordinal, true)]
    [DataRow("dir", StringComparison.OrdinalIgnoreCase, true)]
    [DataRow("DIR", StringComparison.Ordinal, false)]
    [DataRow("DIR", StringComparison.OrdinalIgnoreCase, true)]
    [DataRow("dir\u00AD", StringComparison.Ordinal, false)] // Soft hyphen is ignored by culture-sensitive comparisons
    public void IsInDirectory_WithComparison(string fileDirectoryName, StringComparison comparison, bool expected) =>
        new FileInfo(Path.Combine(fileDirectoryName, "File.cs")).IsInDirectory(new DirectoryInfo("dir"), comparison).Should().Be(expected);
}
