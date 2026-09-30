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

    [TestMethod]
    public void BestCommonPrefix_ParametersAreNull()
    {
        DirectoryInfoExtensions.BestCommonPrefix(null, StringComparer.Ordinal).Should().BeNull();
        DirectoryInfoExtensions.BestCommonPrefix([], null).Should().BeNull();
        DirectoryInfoExtensions.BestCommonPrefix(null, null).Should().BeNull();
    }

    [TestMethod]
    public void BestCommonPrefix_Empty() =>
        DirectoryInfoExtensions.BestCommonPrefix([], StringComparer.Ordinal).Should().BeNull();

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    [DataRow(null, @"C:\", @"D:\Dir")]
    [DataRow(
        null,
        @"C:\",
        @"C:\Dir",
        @"D:\DirA",
        @"D:\DirB\SubDir",
        @"Z:\",
        @"Z:\Dir")]
    [DataRow(
        null,
        @"C:\Temp",
        @"D:\ThreeTimes\A",
        @"D:\ThreeTimes\B",
        @"D:\ThreeTimes\C",
        @"E:\AlsoThreeTimes\A",
        @"E:\AlsoThreeTimes\B",
        @"E:\AlsoThreeTimes\C")]
    [DataRow(
        @"D:\WorkDir",
        @"C:\Temp",
        @"D:\WorkDir\Project",
        @"D:\WorkDir\Project.Tests")]
    [DataRow(
        @"D:\ThreeTimes",
        @"C:\Temp",
        @"D:\ThreeTimes\A",
        @"D:\ThreeTimes\B",
        @"D:\ThreeTimes\C",
        @"E:\Two\A",
        @"E:\Two\B")]
    [DataRow(
        @"C:\Common",
        @"C:\Common",
        @"C:\Common\SubDirA",
        @"C:\Common\SomethingElse")]
    [DataRow(
        @"C:\",
        @"C:\InRoot.cs",
        @"C:\SubDir\A.cs",
        @"C:\SubDir\B.cs")]
    public void BestCommonPrefix_Windows(string commonPrefix, params string[] paths) =>
        paths.Select(x => new DirectoryInfo(x)).BestCommonPrefix(StringComparer.Ordinal).Should().BeEquivalentTo(commonPrefix is null ? null : new { FullName = commonPrefix });

    [TestCategory(TestCategories.NoWindows)]
    [TestMethod]
    [DataRow("/", @"/C/", @"/D/Dir")]
    [DataRow(
        @"/mnt",
        @"/mnt/C/",
        @"/mnt/C/Dir",
        @"/mnt/D/DirA",
        @"/mnt/D/DirB/SubDir",
        @"/mnt/Z/",
        @"/mnt/Z/Dir")]
    [DataRow(
        @"/mnt",
        @"/mnt/C/Temp",
        @"/mnt/D/ThreeTimes/A",
        @"/mnt/D/ThreeTimes/B",
        @"/mnt/D/ThreeTimes/C",
        @"/mnt/E/AlsoThreeTimes/A",
        @"/mnt/E/AlsoThreeTimes/B",
        @"/mnt/E/AlsoThreeTimes/C")]
    [DataRow(
        @"/", // Different from Windows, but okay. "/" is the common root. On Windows there is no common root for c: and d:.
        @"/C/Temp",
        @"/D/WorkDir/Project",
        @"/D/WorkDir/Project.Tests")]
    [DataRow(
        @"/", // Different from Windows, but okay. See above.
        @"/C/Temp",
        @"/D/ThreeTimes/A",
        @"/D/ThreeTimes/B",
        @"/D/ThreeTimes/C",
        @"/E/Two/A",
        @"/E/Two/B")]
    [DataRow(
        @"/mnt/C/Common",
        @"/mnt/C/Common",
        @"/mnt/C/Common/SubDirA",
        @"/mnt/C/Common/SomethingElse")]
    [DataRow(
        @"/mnt/C",
        @"/mnt/C/InRoot.cs",
        @"/mnt/C/SubDir/A.cs",
        @"/mnt/C/SubDir/B.cs")]
    public void BestCommonPrefix_Unix(string commonPrefix, params string[] paths) =>
        paths.Select(x => new DirectoryInfo(x)).BestCommonPrefix(StringComparer.Ordinal).Should().BeEquivalentTo(commonPrefix is null ? null : new { FullName = commonPrefix });

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    [DynamicData(nameof(CommonPrefixCasing_Windows))]
    public void BestCommonPrefix_Comparer_Windows(string[] paths, StringComparer comparer, string expected) =>
        paths.Select(x => new DirectoryInfo(x)).BestCommonPrefix(comparer).Should().BeEquivalentTo(expected is null ? null : new { FullName = expected });

    public static IEnumerable<object[]> CommonPrefixCasing_Windows() =>
        [
            [new[] { @"c:\InRoot.cs", @"C:\SubDir\A.cs" }, StringComparer.OrdinalIgnoreCase, @"c:\"],
            [new[] { @"c:\InRoot.cs", @"C:\SubDir\A.cs" }, StringComparer.Ordinal, null],
            [new[] { @"c:\InRoot.cs", @"C:\SubDir\A.cs" }, StringComparer.InvariantCultureIgnoreCase, @"c:\"],
            [new[] { @"c:\InRoot.cs", @"C:\SubDir\A.cs" }, StringComparer.InvariantCulture, null]
        ];

    [TestCategory(TestCategories.NoWindows)]
    [TestMethod]
    [DynamicData(nameof(CommonPrefixCasing_Unix))]
    public void BestCommonPrefix_Comparer_Unix(string[] paths, StringComparer comparer, string expected) =>
        paths.Select(x => new DirectoryInfo(x)).BestCommonPrefix(comparer).Should().BeEquivalentTo(expected is null ? null : new { FullName = expected });

    public static IEnumerable<object[]> CommonPrefixCasing_Unix() =>
        [
            [new[] { @"/mnt/c/InRoot.cs", @"/mnt/C/SubDir/A.cs" }, StringComparer.OrdinalIgnoreCase, @"/mnt/c"],
            [new[] { @"/mnt/c/InRoot.cs", @"/mnt/C/SubDir/A.cs" }, StringComparer.Ordinal, "/mnt"],
            [new[] { @"/mnt/c/InRoot.cs", @"/mnt/C/SubDir/A.cs" }, StringComparer.InvariantCultureIgnoreCase, @"/mnt/c"],
            [new[] { @"/mnt/c/InRoot.cs", @"/mnt/C/SubDir/A.cs" }, StringComparer.InvariantCulture, "/mnt"]
        ];
}
