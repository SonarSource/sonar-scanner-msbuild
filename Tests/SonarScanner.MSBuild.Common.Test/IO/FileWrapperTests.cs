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
public class FileWrapperTests
{
    [TestMethod]
    public void AppendAllText()
    {
        var sut = FileWrapper.Instance;
        using var tempFile = new TempFile();
        sut.AppendAllText(tempFile.FileName, "Hello");
        var content = File.ReadAllText(tempFile.FileName);
        content.Should().Be("Hello");

        sut.AppendAllText(tempFile.FileName, "World");
        content = File.ReadAllText(tempFile.FileName);
        content.Should().Be("HelloWorld");
    }

    [TestMethod]
    public void AppendAllLines_AppendsLinesToFile()
    {
        var sut = FileWrapper.Instance;
        using var tempFile = new TempFile();
        var initialLines = new[] { "Line1", "Line2" };
        var appendLines = new[] { "Line3", "Line4" };

        sut.AppendAllLines(tempFile.FileName, initialLines, Encoding.UTF8);
        var content = File.ReadAllLines(tempFile.FileName);
        content.Should().BeEquivalentTo(initialLines, x => x.WithStrictOrdering());

        sut.AppendAllLines(tempFile.FileName, appendLines, Encoding.UTF8);
        content = File.ReadAllLines(tempFile.FileName);
        content.Should().BeEquivalentTo(["Line1", "Line2", "Line3", "Line4"], x => x.WithStrictOrdering());
    }

    [TestMethod]
    public void ReadAllText()
    {
        var sut = FileWrapper.Instance;
        using var tempFile = new TempFile();
        File.WriteAllText(tempFile.FileName, "World");
        var content = sut.ReadAllText(tempFile.FileName);
        content.Should().Be("World");
    }

    [TestMethod]
    public void WriteAllText()
    {
        var sut = FileWrapper.Instance;
        using var tempFile = new TempFile();
        sut.WriteAllText(tempFile.FileName, "Hello");
        var content = File.ReadAllText(tempFile.FileName);
        content.Should().Be("Hello");

        sut.WriteAllText(tempFile.FileName, "World");
        content = File.ReadAllText(tempFile.FileName);
        content.Should().Be("World");
    }

    [TestMethod]
    public void Exists_ReturnsTrueIfFileExists()
    {
        var sut = FileWrapper.Instance;
        using var tempFile = new TempFile();
        using var stream = File.Create(tempFile.FileName);
        var exists = sut.Exists(tempFile.FileName);
        exists.Should().BeTrue();
    }

    [TestMethod]
    public void Exists_ReturnsFalseIfFileDoesNotExist()
    {
        var sut = FileWrapper.Instance;
        var exists = sut.Exists("nonexistent.txt");
        exists.Should().BeFalse();
    }

    [TestMethod]
    public void Delete_RemovesFile()
    {
        var sut = FileWrapper.Instance;
        var tempFile = new TempFile();
        using (var stream = File.Create(tempFile.FileName))
        {
            // Just to ensure the file exists
        }
        File.Exists(tempFile.FileName).Should().BeTrue();
        sut.Delete(tempFile.FileName);
        File.Exists(tempFile.FileName).Should().BeFalse();
    }

    [TestMethod]
    public void Copy_CopiesFile()
    {
        var sut = FileWrapper.Instance;
        using var sourceFile = new TempFile();
        using var destFile = new TempFile();
        sut.WriteAllText(sourceFile.FileName, "CopyMe");
        sut.Copy(sourceFile.FileName, destFile.FileName, overwrite: true);
        File.ReadAllText(destFile.FileName).Should().Be("CopyMe");
    }

    [TestMethod]
    public void Move_MovesFile()
    {
        var sut = FileWrapper.Instance;
        using var sourceFile = new TempFile();
        using var destFile = new TempFile();
        sut.WriteAllText(sourceFile.FileName, "MoveMe");
        sut.Move(sourceFile.FileName, destFile.FileName);
        File.Exists(sourceFile.FileName).Should().BeFalse();
        File.ReadAllText(destFile.FileName).Should().Be("MoveMe");
    }

    [TestMethod]
    public void Open_ReturnsFileStream()
    {
        var sut = FileWrapper.Instance;
        using var tempFile = new TempFile();
        File.WriteAllText(tempFile.FileName, "StreamContent");

        using var stream = sut.Open(tempFile.FileName);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        content.Should().Be("StreamContent");
    }

    [TestMethod]
    public void Create_CreatesNewFile()
    {
        var sut = FileWrapper.Instance;
        using var tempFile = new TempFile();
        File.Delete(tempFile.FileName); // Ensure file does not exist

        using (var stream = sut.Create(tempFile.FileName))
        {
            stream.Should().NotBeNull();
            stream.CanWrite.Should().BeTrue();
            stream.WriteByte(65); // Write 'A'
        }

        File.Exists(tempFile.FileName).Should().BeTrue();
        File.ReadAllText(tempFile.FileName).Should().Be("A");
    }

    [TestMethod]
    public void CreateNewAllLines_CreatesFileWithLines()
    {
        var sut = FileWrapper.Instance;
        using var tempFile = new TempFile();
        File.Delete(tempFile.FileName); // Ensure file does not exist

        var lines = new[] { "Alpha", "Beta", "Gamma" };
        sut.CreateNewAllLines(tempFile.FileName, lines, Encoding.UTF8);

        File.Exists(tempFile.FileName).Should().BeTrue();
        var content = File.ReadAllLines(tempFile.FileName);
        content.Should().BeEquivalentTo(lines, x => x.WithStrictOrdering());

        FluentActions.Invoking(
            () => sut.CreateNewAllLines(tempFile.FileName, lines, Encoding.UTF8)).Should().Throw<IOException>().WithMessage("The file * already exists.");
    }

    [TestMethod]
    [CombinatorialData]
    public void ShortName_ReturnsFilename_WhenSmall(PlatformOS platform)
    {
        var sut = FileWrapper.Instance;
        using var tempFile = new TempFile();
        var shortName = sut.ShortName(platform, tempFile.FileName);
        shortName.Should().Be(tempFile.FileName);
    }

    [TestMethod]
    [TestCategory(TestCategories.NoWindows)]
    public void ShortName_ReturnsLongNameOnNix()
    {
        var sut = FileWrapper.Instance;
        var longFilename = new string('a', 300) + ".txt";
        var shortName = sut.ShortName(new OperatingSystemProvider(FileWrapper.Instance, new TestLogger()).OperatingSystem(), longFilename);
        shortName.Should().Be(longFilename);
    }

    [TestMethod]
    [TestCategory(TestCategories.NoMacOS)]
    [TestCategory(TestCategories.NoLinux)]
    [DataRow("")]
    [DataRow(@"\\?\")]
    public void ShortName_WithFileNameLongerThanMaxPath_ReturnsShortName(string extendedPath)
    {
        var sut = FileWrapper.Instance;
        // Windows MAX_PATH is 260, but .NET can handle longer paths with \\?\ prefix.
        var longDirectory = new string('d', 200);
        var longFileName = new string('ö', 100) + ".txt";
        var tempDir = Path.GetTempPath();
        var longPath = Path.Combine(tempDir, longDirectory);
        Directory.CreateDirectory(longPath);
        var longFile = $"{longPath}{Path.AltDirectorySeparatorChar}{longFileName}"; // Use '/' as separator because the GetShortPathName API fails with it and it needs to be replaced before the call
        File.Create(longFile).Dispose(); // Create the file to ensure it exists
        try
        {
            var shortName = sut.ShortName(PlatformOS.Windows, extendedPath + longFile);
            shortName.Should().NotBeNull().And.EndWith(@"\ACB3~1.TXT");
            shortName.Length.Should().BeLessThan(longFile.Length);
        }
        finally
        {
            File.Delete(longFile);
            Directory.Delete(longPath);
        }
    }

    [TestMethod]
    [TestCategory(TestCategories.NoMacOS)]
    [TestCategory(TestCategories.NoLinux)]
    public void ShortName_WithFileNameLongerThanMaxPath_Nonexisting()
    {
        var sut = FileWrapper.Instance;
        // Windows MAX_PATH is 260, but .NET can handle longer paths with \\?\ prefix.
        var longDirectory = new string('d', 200);
        var longFileName = new string('b', 100) + ".txt";
        var longPath = Path.Combine(Path.GetTempPath(), longDirectory, longFileName);
        var shortName = sut.ShortName(PlatformOS.Windows, longPath);
        shortName.Should().Be(longPath);
    }
}
