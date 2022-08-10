/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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

using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Shim.Test
{
    [TestClass]
    public class PathHelperTests
    {
        [TestMethod]
        public void WithTrailingSeparator_WhenNull_ThrowsArgumentNullException() =>
            ((Action)(() => PathHelper.WithTrailingDirectorySeparator(null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("directory");

        [TestMethod]
        public void WithTrailingSeparator_WhenEndsWithBackslash_ReturnsDirectoryFullName()
        {
            var directory = new DirectoryInfo(@"C:\SomeDirectory\");
            var result = PathHelper.WithTrailingDirectorySeparator(directory);

            result.Should().Be(directory.FullName);
        }

        [TestMethod]
        public void WithTrailingSeparator_WhenEndsWithSlash_ReturnsDirectoryFullName()
        {
            var directory = new DirectoryInfo("C:/SomeDirectory/");
            var result = PathHelper.WithTrailingDirectorySeparator(directory);

            result.Should().Be(directory.FullName);
        }

        [TestMethod]
        public void WithTrailingSeparator_WhenDoesNotEndWithSeparatorAndContainsDirectorySeparatorChar_ReturnsStringWithRightEnd()
        {
            var directory = new DirectoryInfo("C:" + Path.DirectorySeparatorChar + "SomeDirectory" + Path.DirectorySeparatorChar + "Foo");
            var result = PathHelper.WithTrailingDirectorySeparator(directory);

            result.Should().Be(directory.FullName + Path.DirectorySeparatorChar);
        }

        [TestMethod]
        public void WithTrailingSeparator_WhenDoesNotEndWithSeparatorAndContainsAltDirectorySeparatorChar_ReturnsStringWithRightEnd()
        {
            var directory = new DirectoryInfo("C:" + Path.AltDirectorySeparatorChar + "SomeDirectory" + Path.AltDirectorySeparatorChar + "Foo");
            var result = PathHelper.WithTrailingDirectorySeparator(directory);

            result.Should().Be(directory.FullName + Path.DirectorySeparatorChar);
        }

        [TestMethod]
        public void WithTrailingSeparator_WhenDoesNotEndWithSeparatorAndContainsNoSeparator_ReturnsStringWithDirectorySeparatorChar()
        {
            var directory = new DirectoryInfo("SomeDirectory");
            var result = PathHelper.WithTrailingDirectorySeparator(directory);

            result.Should().Be(directory.FullName + Path.DirectorySeparatorChar);
        }

        [TestMethod]
        public void IsInDirectory_ReturnsTheExpectedResults()
        {
            PathHelper.IsInDirectory(new FileInfo(@"C:\Src\File.cs"), new DirectoryInfo(@"C:\Src\")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo(@"C:\Src\File.cs"), new DirectoryInfo(@"C:\Src")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo(@"C:\Src\File.cs"), new DirectoryInfo(@"C:\")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo(@"C:\SRC\FILE.CS"), new DirectoryInfo(@"C:\src")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo(@"C:/Src/File.cs"), new DirectoryInfo(@"C:/Src")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo(@"C:\Src\File.cs"), new DirectoryInfo(@"C:/Src")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo(@"~Foo\File.cs"), new DirectoryInfo("~Foo")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo(@"C:\Src\Bar\..\File.cs"), new DirectoryInfo(@"C:\Src")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo(@"C:\Src\File.cs"), new DirectoryInfo(@"C:\Src\Bar\..")).Should().BeTrue();
            // False
            PathHelper.IsInDirectory(new FileInfo(@"C:\SrcFile.cs"), new DirectoryInfo(@"C:\Src")).Should().BeFalse();
            PathHelper.IsInDirectory(new FileInfo(@"C:\Src\File.cs"), new DirectoryInfo(@"C:\Src\Bar")).Should().BeFalse();
        }

        [TestMethod]
        public void BestCommonRoot_WhenNull_ReturnsNull() =>
            PathHelper.BestCommonRoot(null).Should().BeNull();

        [TestMethod]
        public void BestCommonRoot_WhenNoCommonPath_ReturnsNull() =>
            PathHelper.BestCommonRoot(new[]
            {
                new DirectoryInfo(@"C:\"),
                new DirectoryInfo(@"D:\Dir"),
            }).Should().BeNull();

        [TestMethod]
        public void BestCommonRoot_WhenNoCommonPath_SameCountInEachGroup_ReturnsNull() =>
            PathHelper.BestCommonRoot(new[]
            {
                new DirectoryInfo(@"C:\"),
                new DirectoryInfo(@"C:\Dir"),
                new DirectoryInfo(@"D:\DirA"),
                new DirectoryInfo(@"D:\DirB\SubDir"),
                new DirectoryInfo(@"Z:\"),
                new DirectoryInfo(@"Z:\Dir"),
            }).Should().BeNull();

        [TestMethod]
        public void BestCommonRoot_WhenNoCommonPath_SameCountInMostCommonGroup_ReturnsNull() =>
            PathHelper.BestCommonRoot(new[]
            {
                new DirectoryInfo(@"C:\Temp"),
                new DirectoryInfo(@"D:\ThreeTimes\A"),
                new DirectoryInfo(@"D:\ThreeTimes\B"),
                new DirectoryInfo(@"D:\ThreeTimes\C"),
                new DirectoryInfo(@"E:\AlsoThreeTimes\A"),
                new DirectoryInfo(@"E:\AlsoThreeTimes\B"),
                new DirectoryInfo(@"E:\AlsoThreeTimes\C"),
            }).Should().BeNull();

        [TestMethod]
        public void BestCommonRoot_WhenNoCommonPath_ReturnsMostCommonOne_Simple() =>
            PathHelper.BestCommonRoot(new[]
            {
                new DirectoryInfo(@"C:\Temp"),
                new DirectoryInfo(@"D:\WorkDir\Project"),
                new DirectoryInfo(@"D:\WorkDir\Project.Tests"),
            }).FullName.Should().Be(@"D:\WorkDir");

        [TestMethod]
        public void BestCommonRoot_WhenNoCommonPath_ReturnsMostCommonOne_Complex() =>
            PathHelper.BestCommonRoot(new[]
            {
                new DirectoryInfo(@"C:\Temp"),
                new DirectoryInfo(@"D:\ThreeTimes\A"),
                new DirectoryInfo(@"D:\ThreeTimes\B"),
                new DirectoryInfo(@"D:\ThreeTimes\C"),
                new DirectoryInfo(@"E:\Two\A"),
                new DirectoryInfo(@"E:\Two\B"),
            }).FullName.Should().Be(@"D:\ThreeTimes");

        [TestMethod]
        public void BestCommonRoot_WhenCommonPath_ReturnsTheLongestCommonPart() =>
            PathHelper.BestCommonRoot(new[]
            {
                new DirectoryInfo(@"C:\Common"),
                new DirectoryInfo(@"C:\Common\SubDirA"),
                new DirectoryInfo(@"C:\Common\SomethingElse"),
            }).FullName.Should().Be(@"C:\Common");

        [TestMethod]
        public void BestCommonRoot_WhenCommonPathOfFiles_ReturnsTheLongestCommonPart() =>
            PathHelper.BestCommonRoot(new[]
            {
                new DirectoryInfo(@"C:\InRoot.cs"),
                new DirectoryInfo(@"C:\SubDir\A.cs"),
                new DirectoryInfo(@"C:\SubDir\B.cs"),
            }).FullName.Should().Be(@"C:\");

        [TestMethod]
        public void GetParts_WhenNull_ThrowsArgumentNullException() =>
            ((Action)(() => PathHelper.GetParts(null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("directory");

        [TestMethod]
        public void GetParts_ReturnsTheExpectedValues()
        {
            PathHelper.GetParts(new DirectoryInfo(@"C:\")).Should().BeEquivalentTo(@"C:\");
            PathHelper.GetParts(new DirectoryInfo(@"C:\Foo\Bar")).Should().BeEquivalentTo(@"C:\", "Foo", "Bar");
            // Also work with a file path given as DirectoryInfo
            PathHelper.GetParts(new DirectoryInfo(@"C:\Foo\Bar\File.cs")).Should().BeEquivalentTo(@"C:\", "Foo", "Bar", "File.cs");
        }
    }
}
