/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using TestUtilities;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class UtilitiesTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void VersionDisplayString()
    {
        CheckVersionString("1.2.0.0", "1.2");
        CheckVersionString("1.0.0.0", "1.0");
        CheckVersionString("0.0.0.0", "0.0");
        CheckVersionString("1.2.3.0", "1.2.3");

        CheckVersionString("1.2.0.4", "1.2.0.4");
        CheckVersionString("1.2.3.4", "1.2.3.4");
        CheckVersionString("0.2.3.4", "0.2.3.4");
        CheckVersionString("0.0.3.4", "0.0.3.4");
    }

    private static void CheckVersionString(string version, string expectedDisplayString)
    {
        var actualVersion = new Version(version);
        var actualVersionString = actualVersion.ToDisplayString();

         actualVersionString.Should().Be(expectedDisplayString);
    }

    [TestMethod]
    public void Retry_WhenTimeoutInMillisecondsIsLessThan1_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        Action action = () => Utilities.Retry(0, 1, new TestLogger(), () => true);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentOutOfRangeException>().And.ParamName.Should().Be("timeoutInMilliseconds");
    }

    [TestMethod]
    public void Retry_WhenPauseBetweenTriesInMillisecondsIsLessThan1_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        Action action = () => Utilities.Retry(1, 0, new TestLogger(), () => true);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentOutOfRangeException>().And.ParamName.Should().Be("pauseBetweenTriesInMilliseconds");
    }

    [TestMethod]
    public void Retry_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Utilities.Retry(1, 1, null, () => true);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void Retry_WhenOperationIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Utilities.Retry(1, 1, new TestLogger(), null);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("op");
    }

    [TestMethod]
    public void EnsureDirectoryExists_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Utilities.EnsureDirectoryExists("directory", null);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void EnsureDirectoryExists_WhenDirectoryIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Utilities.EnsureDirectoryExists(null, new TestLogger());

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("directory");
    }

    [TestMethod]
    public void EnsureDirectoryExists_WhenDirectoryIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Utilities.EnsureDirectoryExists("", new TestLogger());

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("directory");
    }

    [TestMethod]
    public void EnsureDirectoryExists_WhenDirectoryIsWhitespaces_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Utilities.EnsureDirectoryExists("   ", new TestLogger());

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("directory");
    }

    [TestMethod]
    public void EnsureDirectoryExists_WhenDirectoryMissing_IsCreated()
    {
        // Arrange
        var baseDir =TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var newDir = Path.Combine(baseDir, "newDir");
        var logger = new TestLogger();

        // Act
        Utilities.EnsureDirectoryExists(newDir, logger);

        // Assert
        Directory.Exists(newDir).Should().BeTrue();
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void EnsureDirectoryExists_WhenDirectoryExists_IsNoOp()
    {
        // Arrange
        var baseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var logger = new TestLogger();

        // Act
        Utilities.EnsureDirectoryExists(baseDir, logger);

        // Assert
        Directory.Exists(baseDir).Should().BeTrue();
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void EnsureEmptyDirectory_WhenDirectoryIsInvalid_ThrowsArgumentNullException()
    {
        // 1. Null
        Action action = () => Utilities.EnsureEmptyDirectory(null, new TestLogger());
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("directory");

        // 2. Empty
        action = () => Utilities.EnsureDirectoryExists("", new TestLogger());
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("directory");

        // 3. Whitespace
        action = () => Utilities.EnsureDirectoryExists("   ", new TestLogger());
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("directory");
    }

    [TestMethod]
    public void EnsureEmptyDirectory_WhenLoggerIsInvalid_ThrowsArgumentNullException()
    {
        // 1. Null
        Action action = () => Utilities.EnsureEmptyDirectory("c:\\foo", null);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");

        // 2. Empty
        action = () => Utilities.EnsureDirectoryExists("c:\\foo", null);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");

        // 3. Whitespace
        action = () => Utilities.EnsureDirectoryExists("c:\\foo", null);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void EnsureEmptyDirectory_WhenDirectoryMissing_IsCreated()
    {
        // Arrange
        var baseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var newDir = Path.Combine(baseDir, "newDir");
        var logger = new TestLogger();

        // Act
        Utilities.EnsureDirectoryExists(newDir, logger);

        // Assert
        Directory.Exists(newDir).Should().BeTrue();
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void EnsureEmptyDirectory_WhenDirectoryExistsAndHasFiles_FilesAreDeleted()
    {
        // Arrange
        var baseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        File.WriteAllText(Path.Combine(baseDir, "file1.txt"), "xxx");
        File.WriteAllText(Path.Combine(baseDir, "file2.txt"), "xxx");
        Directory.CreateDirectory(Path.Combine(baseDir, "subdir1"));
        var logger = new TestLogger();

        // Act
        Utilities.EnsureEmptyDirectory(baseDir, logger);

        // Assert
        Directory.Exists(baseDir).Should().BeTrue();
        Directory.GetFiles(baseDir).Should().BeEmpty();
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void TryEnsureEmptyDirectory_WhenLoggerIsInvalid_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Utilities.TryEnsureEmptyDirectories(null, "c:\\foo");

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void TryEnsureEmptyDirectories_WhenDirectoriesExistsAndHaveFiles_FilesAreDeleted()
    {
        // Arrange
        // Directory with file
        var baseDir1 = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "baseDir1");
        File.WriteAllText(Path.Combine(baseDir1, "file1.txt"), "xxx");

        // Directory with file and sub-directory
        var baseDir2 = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "baseDir2");
        File.WriteAllText(Path.Combine(baseDir2, "file2.txt"), "xxx");
        Directory.CreateDirectory(Path.Combine(baseDir2, "subdir1"));

        // Empty directory
        var baseDir3 = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "baseDir3");

        var logger = new TestLogger();

        // Act
        var result = Utilities.TryEnsureEmptyDirectories(logger, baseDir1, baseDir2, baseDir3);

        // Assert
        result.Should().BeTrue();

        Directory.Exists(baseDir1).Should().BeTrue();
        Directory.GetFiles(baseDir1).Should().BeEmpty();

        Directory.Exists(baseDir2).Should().BeTrue();
        Directory.GetFiles(baseDir2).Should().BeEmpty();

        Directory.Exists(baseDir3).Should().BeTrue();
        Directory.GetFiles(baseDir3).Should().BeEmpty();

        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void TryEnsureEmptyDirectories_WhenIOException_ReturnsFalse()
    {
        // Arrange
        // Directory with file
        var baseDir1 = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "baseDir1");
        var filePath = Path.Combine(baseDir1, "file1.txt");
        File.WriteAllText(filePath, "xxx");

        // Directory with file
        var baseDir2 = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "baseDir2");
        File.WriteAllText(Path.Combine(baseDir2, "file2.txt"), "xxx");

        var logger = new TestLogger();

        bool result;
        using (File.OpenRead(filePath)) // lock the file to cause an IO error
        {
            // Act
            result = Utilities.TryEnsureEmptyDirectories(logger, baseDir1, baseDir2);
        }

        // Assert
        result.Should().BeFalse();

        Directory.Exists(baseDir1).Should().BeTrue();
        Directory.GetFiles(baseDir1).Should().HaveCount(1);

        Directory.Exists(baseDir2).Should().BeTrue();
        Directory.GetFiles(baseDir2).Should().HaveCount(1);

        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().HaveCount(1);
        logger.AssertSingleErrorExists(baseDir1); // expecting the directory name to be in the message
    }

    [TestMethod]
    public void LogAssemblyVersion_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Utilities.LogAssemblyVersion(null, "foo");

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void LogAssemblyVersion_WhenDescriptionIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Utilities.LogAssemblyVersion(new TestLogger(), null);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("description");
    }

    [TestMethod]
    public void LogAssemblyVersion_WhenDescriptionIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Utilities.LogAssemblyVersion(new TestLogger(), "");

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("description");
    }

    [TestMethod]
    public void LogAssemblyVersion_WhenDescriptionIsWhitespaces_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Utilities.LogAssemblyVersion(new TestLogger(), "   ");

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("description");
    }
}
