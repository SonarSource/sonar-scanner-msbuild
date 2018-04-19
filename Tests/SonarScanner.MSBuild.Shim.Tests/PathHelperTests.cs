/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
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

namespace SonarScanner.MSBuild.Shim.Tests
{
    [TestClass]
    public class PathHelperTests
    {
        [TestMethod]
        public void WithTrailingSeparator_WhenNull_ThrowsArgumentNullException()
        {
            // Arrange & Act
            Action act = () => PathHelper.WithTrailingDirectorySeparator(null);

            // Assert
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("directory");
        }

        [TestMethod]
        public void WithTrailingSeparator_WhenEndsWithBackslash_ReturnsDirectoryFullName()
        {
            // Arrange
            var path = "C:\\SomeDirectory\\";
            var directory = new DirectoryInfo(path);

            // Act
            var result = PathHelper.WithTrailingDirectorySeparator(directory);

            // Assert
            result.Should().Be(directory.FullName);
        }

        [TestMethod]
        public void WithTrailingSeparator_WhenEndsWithSlash_ReturnsDirectoryFullName()
        {
            // Arrange
            var path = "C:/SomeDirectory/";
            var directory = new DirectoryInfo(path);

            // Act
            var result = PathHelper.WithTrailingDirectorySeparator(directory);

            // Assert
            result.Should().Be(directory.FullName);
        }

        [TestMethod]
        public void WithTrailingSeparator_WhenDoesNotEndWithSeparatorAndContainsDirectorySeparatorChar_ReturnsStringWithRightEnd()
        {
            // Arrange
            var path = "C:" + Path.DirectorySeparatorChar + "SomeDirectory" + Path.DirectorySeparatorChar + "Foo";
            var directory = new DirectoryInfo(path);

            // Act
            var result = PathHelper.WithTrailingDirectorySeparator(directory);

            // Assert
            result.Should().Be(directory.FullName + Path.DirectorySeparatorChar);
        }

        [TestMethod]
        public void WithTrailingSeparator_WhenDoesNotEndWithSeparatorAndContainsAltDirectorySeparatorChar_ReturnsStringWithRightEnd()
        {
            // Arrange
            var path = "C:" + Path.AltDirectorySeparatorChar + "SomeDirectory" + Path.AltDirectorySeparatorChar + "Foo";
            var directory = new DirectoryInfo(path);

            // Act
            var result = PathHelper.WithTrailingDirectorySeparator(directory);

            // Assert
            result.Should().Be(directory.FullName + Path.DirectorySeparatorChar);
        }

        [TestMethod]
        public void WithTrailingSeparator_WhenDoesNotEndWithSeparatorAndContainsNoSeparator_ReturnsStringWithDirectorySeparatorChar()
        {
            // Arrange
            var path = "SomeDirectory";
            var directory = new DirectoryInfo(path);

            // Act
            var result = PathHelper.WithTrailingDirectorySeparator(directory);

            // Assert
            result.Should().Be(directory.FullName + Path.DirectorySeparatorChar);
        }

        [TestMethod]
        public void IsInDirectory_ReturnsTheExpectedResults()
        {
            PathHelper.IsInDirectory(new FileInfo("C:\\Src\\File.cs"), new DirectoryInfo("C:\\Src\\")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo("C:\\Src\\File.cs"), new DirectoryInfo("C:\\Src")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo("C:\\Src\\File.cs"), new DirectoryInfo("C:\\")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo("C:\\SRC\\FILE.CS"), new DirectoryInfo("C:\\src")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo("C:/Src/File.cs"), new DirectoryInfo("C:/Src")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo("C:\\Src\\File.cs"), new DirectoryInfo("C:/Src")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo("~Foo\\File.cs"), new DirectoryInfo("~Foo")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo("C:\\Src\\Bar\\..\\File.cs"), new DirectoryInfo("C:\\Src")).Should().BeTrue();
            PathHelper.IsInDirectory(new FileInfo("C:\\Src\\File.cs"), new DirectoryInfo("C:\\Src\\Bar\\..")).Should().BeTrue();

            PathHelper.IsInDirectory(new FileInfo("C:\\SrcFile.cs"), new DirectoryInfo("C:\\Src")).Should().BeFalse();
            PathHelper.IsInDirectory(new FileInfo("C:\\Src\\File.cs"), new DirectoryInfo("C:\\Src\\Bar")).Should().BeFalse();
        }

        [TestMethod]
        public void GetCommonRoot_WhenNull_ReturnsNull()
        {
            // Arrange
            // Act
            var result = PathHelper.GetCommonRoot(null);

            // Assert
            result.Should().BeNull();
        }

        [TestMethod]
        public void GetCommonRoot_WhenNoCommonPath_ReturnsNull()
        {
            PathHelper.GetCommonRoot(new[]
            {
                new DirectoryInfo("C:\\"),
                new DirectoryInfo("D:\\Foo"),
            }).Should().BeNull();
        }

        [TestMethod]
        public void GetCommonRoot_WhenCommonPath_ReturnsTheLongestCommonPart()
        {
            PathHelper.GetCommonRoot(new[]
            {
                new DirectoryInfo("C:\\Foo"),
                new DirectoryInfo("C:\\Foo\\Bar"),
                new DirectoryInfo("C:\\Foo\\FooBar"),
            }).FullName.Should().Be("C:\\Foo");
        }

        [TestMethod]
        public void GetCommonRoot_WhenCommonPathOfFiles_ReturnsTheLongestCommonPart()
        {
            PathHelper.GetCommonRoot(new[]
            {
                new DirectoryInfo("C:\\Foo.cs"),
                new DirectoryInfo("C:\\Foo\\Bar.cs"),
                new DirectoryInfo("C:\\Foo\\FooBar.cs"),
            }).FullName.Should().Be("C:\\");
        }

        [TestMethod]
        public void GetParts_WhenNull_ThrowsArgumentNullException()
        {
            // Arrange
            // Act
            Action act = () => PathHelper.GetParts(null);

            // Assert
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("directoryInfo");
        }

        [TestMethod]
        public void GetParts_ReturnsTheExpectedValues()
        {
            PathHelper.GetParts(new DirectoryInfo("C:\\")).Should().BeEquivalentTo("C:\\");
            PathHelper.GetParts(new DirectoryInfo("C:\\Foo\\Bar")).Should().BeEquivalentTo("C:\\", "Foo", "Bar");

            // Also work with a file path given as DirectoryInfo
            PathHelper.GetParts(new DirectoryInfo("C:\\Foo\\Bar\\File.cs")).Should().BeEquivalentTo("C:\\", "Foo", "Bar", "File.cs");
        }
    }
}
