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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class UtilitiesTests
{
    public TestContext TestContext { get; set; }

    [DataRow("1.2.0.0", "1.2")]
    [DataRow("1.0.0.0", "1.0")]
    [DataRow("0.0.0.0", "0.0")]
    [DataRow("1.2.3.0", "1.2.3")]
    [DataRow("1.2.0.4", "1.2.0.4")]
    [DataRow("1.2.3.4", "1.2.3.4")]
    [DataRow("0.2.3.4", "0.2.3.4")]
    [DataRow("0.0.3.4", "0.0.3.4")]
    [DataTestMethod]
    public void VersionDisplayString(string version, string expectedDisplayString) =>
        new Version(version).ToDisplayString().Should().Be(expectedDisplayString);

    [TestMethod]
    public void EnsureDirectoryExists_WhenDirectoryMissing_IsCreated()
    {
        var newDir = Path.Combine(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext), "newDir");
        var logger = new TestLogger();

        Utilities.EnsureDirectoryExists(newDir, logger);

        Directory.Exists(newDir).Should().BeTrue();
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void EnsureDirectoryExists_WhenDirectoryExists_IsNoOp()
    {
        var baseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var logger = new TestLogger();

        Utilities.EnsureDirectoryExists(baseDir, logger);

        Directory.Exists(baseDir).Should().BeTrue();
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void EnsureEmptyDirectory_WhenDirectoryMissing_IsCreated()
    {
        var newDir = Path.Combine(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext), "newDir");
        var logger = new TestLogger();

        Utilities.EnsureEmptyDirectory(newDir, logger);

        Directory.Exists(newDir).Should().BeTrue();
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void EnsureEmptyDirectory_WhenDirectoryExistsAndHasFiles_FilesAreDeleted()
    {
        var baseDir = CreateDirectoryWithFile("baseDir1");
        File.WriteAllText(Path.Combine(baseDir, "file2.txt"), "xxx");
        Directory.CreateDirectory(Path.Combine(baseDir, "subdir1"));
        var logger = new TestLogger();

        Utilities.EnsureEmptyDirectory(baseDir, logger);

        Directory.Exists(baseDir).Should().BeTrue();
        Directory.GetFiles(baseDir).Should().BeEmpty();
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void TryEnsureEmptyDirectories_WhenDirectoriesExistsAndHaveFiles_FilesAreDeleted()
    {
        var dirWithFile = CreateDirectoryWithFile("baseDir1");
        var dirWithFileAndSubDir = CreateDirectoryWithFile("baseDir2");
        Directory.CreateDirectory(Path.Combine(dirWithFileAndSubDir, "subdir1"));
        var emptyDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "baseDir3");
        var logger = new TestLogger();

        var result = Utilities.TryEnsureEmptyDirectories(logger, dirWithFile, dirWithFileAndSubDir, emptyDir);

        result.Should().BeTrue();
        Directory.Exists(dirWithFile).Should().BeTrue();
        Directory.GetFiles(dirWithFile).Should().BeEmpty();
        Directory.Exists(dirWithFileAndSubDir).Should().BeTrue();
        Directory.GetFiles(dirWithFileAndSubDir).Should().BeEmpty();
        Directory.Exists(emptyDir).Should().BeTrue();
        Directory.GetFiles(emptyDir).Should().BeEmpty();
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }

    // Windows enforces file locks at the OS level resulting in IOException.
    // Unix does not enforce file locks at the OS level, so no exception is thrown.
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void TryEnsureEmptyDirectories_WhenIOException_ReturnsFalse()
    {
        var dirWithFile = CreateDirectoryWithFile("baseDir1");
        var dirWithFile2 = CreateDirectoryWithFile("baseDir2");
        var logger = new TestLogger();
        bool result;

        using (File.OpenRead(Path.Combine(dirWithFile, "file1.txt"))) // lock the file to cause an IO error
        {
            result = Utilities.TryEnsureEmptyDirectories(logger, dirWithFile, dirWithFile2);
        }

        result.Should().BeFalse();
        Directory.Exists(dirWithFile).Should().BeTrue();
        Directory.GetFiles(dirWithFile).Should().ContainSingle();
        Directory.Exists(dirWithFile2).Should().BeTrue();
        Directory.GetFiles(dirWithFile2).Should().ContainSingle();
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().ContainSingle();
        logger.AssertSingleErrorExists(dirWithFile); // expecting the directory name to be in the message
    }

    [DataRow(0, 1, "timeoutInMilliseconds")]
    [DataRow(1, 0, "pauseBetweenTriesInMilliseconds")]
    [DataTestMethod]
    public void Retry_ThrowsArgumentOutOfRangeException(int timeout, int pause, string expected)
    {
        Action action = () => Utilities.Retry(timeout, pause, new TestLogger(), () => true);
        action.Should().ThrowExactly<ArgumentOutOfRangeException>().And.ParamName.Should().Be(expected);
    }

    [TestMethod]
    public void Retry_NullLogger_ThrowsArgumentNullException()
    {
        Action action = () => Utilities.Retry(1, 1,  null, () => true);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void Retry_NullOp_ThrowsArgumentNullException()
    {
        Action action = () => Utilities.Retry(1, 1, new TestLogger(), null);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("op");
    }

    [TestMethod]
    public void EnsureDirectoryExists_NullLogger_ThrowsArgumentNullException()
    {
        Action action = () => Utilities.EnsureDirectoryExists("directory", null);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataTestMethod]
    public void EnsureDirectoryExists_InvalidDirectory_ThrowsArgumentNullException(string directory)
    {
        Action action = () => Utilities.EnsureDirectoryExists(directory, new TestLogger());
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("directory");
    }

    [TestMethod]
    public void EnsureEmptyDirectory_NullLogger_ThrowsArgumentNullException()
    {
        Action action = () => Utilities.EnsureEmptyDirectory("directory", null);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataTestMethod]
    public void EnsureEmptyDirectory_InvalidDirectory_ThrowsArgumentNullException(string directory)
    {
        Action action = () => Utilities.EnsureEmptyDirectory(directory, new TestLogger());
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("directory");
    }

    [TestMethod]
    public void LogAssemblyVersion_NullLogger_ThrowsArgumentNullException()
    {
        Action action = () => Utilities.LogAssemblyVersion(null, "description");
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    [DataTestMethod]
    public void LogAssemblyVersion_InvaliDescription_ThrowsArgumentNullException(string description)
    {
        Action action = () => Utilities.LogAssemblyVersion(new TestLogger(), description);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("description");
    }

    [TestMethod]
    public void TryEnsureEmptyDirectory_WhenLoggerIsInvalid_ThrowsArgumentNullException()
    {
        Action action = () => Utilities.TryEnsureEmptyDirectories(null, "foo");
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    private string CreateDirectoryWithFile(string directoryName)
    {
        var dir = Path.Combine(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext), directoryName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "file1.txt"), "xxx");
        return dir;
    }
}
