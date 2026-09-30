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
public class PathHelperTests
{
    [TestMethod]
    public void BestCommonPrefix_WhenParametersAreNull_ReturnsNull()
    {
        PathHelper.BestCommonPrefix(null, StringComparer.Ordinal).Should().BeNull();
        PathHelper.BestCommonPrefix(new DirectoryInfo[] { }, null).Should().BeNull();
        PathHelper.BestCommonPrefix(null, null).Should().BeNull();
    }

    [TestMethod]
    public void BestCommonPrefix_WhenEmpty_ReturnsNull() =>
        PathHelper.BestCommonPrefix([], StringComparer.Ordinal).Should().BeNull();

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
        PathHelper
            .BestCommonPrefix(paths.Select(x => new DirectoryInfo(x)), StringComparer.Ordinal)
            .Should()
            .BeEquivalentTo(commonPrefix is null ? null : new { FullName = commonPrefix });

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
        PathHelper
            .BestCommonPrefix(paths.Select(x => new DirectoryInfo(x)), StringComparer.Ordinal)
            .Should()
            .BeEquivalentTo(commonPrefix is null ? null : new { FullName = commonPrefix });

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    [DynamicData(nameof(CommonPrefixCasing_Windows))]
    public void BestCommonPrefix_Comparer_Windows(string[] paths, StringComparer comparer, string expected) =>
        PathHelper
            .BestCommonPrefix(paths.Select(x => new DirectoryInfo(x)), comparer)
            .Should()
            .BeEquivalentTo(expected is null ? null : new { FullName = expected });

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
        PathHelper
            .BestCommonPrefix(paths.Select(x => new DirectoryInfo(x)), comparer)
            .Should()
            .BeEquivalentTo(expected is null ? null : new { FullName = expected });

    public static IEnumerable<object[]> CommonPrefixCasing_Unix() =>
        [
            [new[] { @"/mnt/c/InRoot.cs", @"/mnt/C/SubDir/A.cs" }, StringComparer.OrdinalIgnoreCase, @"/mnt/c"],
            [new[] { @"/mnt/c/InRoot.cs", @"/mnt/C/SubDir/A.cs" }, StringComparer.Ordinal, "/mnt"],
            [new[] { @"/mnt/c/InRoot.cs", @"/mnt/C/SubDir/A.cs" }, StringComparer.InvariantCultureIgnoreCase, @"/mnt/c"],
            [new[] { @"/mnt/c/InRoot.cs", @"/mnt/C/SubDir/A.cs" }, StringComparer.InvariantCulture, "/mnt"]
        ];
}
