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
public class DirectoryInfoExtensionsTests
{
    [TestMethod]
    public void WithTrailingSeparator_Null() =>
        ((Action)(() => DirectoryInfoExtensions.WithTrailingDirectorySeparator(null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("directory");

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    [DataRow(@"C:\SomeDirectory", @"C:\SomeDirectory\")]
    [DataRow(@"C:\SomeDirectory\", @"C:\SomeDirectory\")]
    [DataRow(@"C:\SomeDirectory/", @"C:\SomeDirectory\")]
    public void WithTrailingSeparator_EndsWithBackslash_Windows(string directory, string expected) =>
        new DirectoryInfo(directory).WithTrailingDirectorySeparator().Should().Be(expected);

    [TestCategory(TestCategories.NoWindows)]
    [TestMethod]
    [DataRow(@"/mnt/c/SomeDirectory", @"/mnt/c/SomeDirectory/")]
    [DataRow(@"/mnt/c/SomeDirectory/", @"/mnt/c/SomeDirectory/")]
    [DataRow(@"/mnt/c/SomeDirectory\", @"/mnt/c/SomeDirectory\/")]
    public void WithTrailingSeparator_EndsWithBackslash_Unix(string directory, string expected) =>
        new DirectoryInfo(directory).WithTrailingDirectorySeparator().Should().Be(expected);

    [TestMethod]
    public void WithTrailingSeparator_DoesNotEndWithSeparatorAndContainsDirectorySeparatorChar()
    {
        var directory = new DirectoryInfo("C:" + Path.DirectorySeparatorChar + "SomeDirectory" + Path.DirectorySeparatorChar + "Foo");
        var result = DirectoryInfoExtensions.WithTrailingDirectorySeparator(directory);

        result.Should().Be(directory.FullName + Path.DirectorySeparatorChar);
    }

    [TestMethod]
    public void WithTrailingSeparator_DoesNotEndWithSeparatorAndContainsAltDirectorySeparatorChar()
    {
        var directory = new DirectoryInfo("C:" + Path.AltDirectorySeparatorChar + "SomeDirectory" + Path.AltDirectorySeparatorChar + "Foo");
        var result = DirectoryInfoExtensions.WithTrailingDirectorySeparator(directory);

        result.Should().Be(directory.FullName + Path.DirectorySeparatorChar);
    }

    [TestMethod]
    public void WithTrailingSeparator_DoesNotEndWithSeparatorAndContainsMixedSeparators()
    {
        var directory = new DirectoryInfo("C:" + Path.DirectorySeparatorChar + "SomeDirectory" + Path.AltDirectorySeparatorChar + "Foo");
        var result = DirectoryInfoExtensions.WithTrailingDirectorySeparator(directory);

        result.Should().Be(directory.FullName + Path.DirectorySeparatorChar);
    }

    [TestMethod]
    public void WithTrailingSeparator_DoesNotEndWithSeparatorAndContainsNoSeparator()
    {
        var directory = new DirectoryInfo("SomeDirectory");
        var result = DirectoryInfoExtensions.WithTrailingDirectorySeparator(directory);

        result.Should().Be(directory.FullName + Path.DirectorySeparatorChar);
    }

    [TestMethod]
    public void Parts_Null() =>
        ((Action)(() => DirectoryInfoExtensions.Parts(null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("directory");

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    [DataRow(@"C:\", @"C:\")]
    [DataRow(@"C:\Foo\Bar", @"C:\", "Foo", "Bar")]
    [DataRow(@"C:\Foo\Bar\File.cs", @"C:\", "Foo", "Bar", "File.cs")]
    public void Parts_Windows(string directory, params string[] parts) =>
        new DirectoryInfo(directory).Parts().Should().BeEquivalentTo(parts);

    [TestCategory(TestCategories.NoWindows)]
    [TestMethod]
    [DataRow("/mnt/c/", "/", "mnt", "c")]
    [DataRow("/mnt/c/Foo/Bar", "/", "mnt", "c", "Foo", "Bar")]
    [DataRow("/mnt/c/Foo/Bar/File.cs", "/", "mnt", "c", "Foo", "Bar", "File.cs")]
    public void Parts_Unix(string directory, params string[] parts) =>
        new DirectoryInfo(directory).Parts().Should().BeEquivalentTo(parts);
}
