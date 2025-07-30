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

namespace SonarScanner.MSBuild.TFS.Tests;

[TestClass]
public class BuildVNextCoverageSearchFallbackTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Fallback_AgentDirectory_CalculatedCorrectly_Null()
    {
        var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
        using var envVars = new EnvironmentVariableScope();
        // env var not specified -> null
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AGENT_TEMP_DIRECTORY, null);
        testSubject.GetAgentTempDirectory().Should().BeNull();
    }

    [TestMethod]
    public void Fallback_AgentDirectory_CalculatedCorrectly_NonExisting()
    {
        var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
        var rootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var envDir = Path.Combine(rootDir, "DirSpecifiedInEnvDir");

        using var envVars = new EnvironmentVariableScope();
        // Env var set but dir does not exist -> null
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AGENT_TEMP_DIRECTORY, envDir);
        testSubject.GetAgentTempDirectory().Should().BeNull();
    }

    [TestMethod]
    public void Fallback_AgentDirectory_CalculatedCorrectly_Existing()
    {
        var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
        var rootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var envDir = Path.Combine(rootDir, "DirSpecifiedInEnvDir");

        using var envVars = new EnvironmentVariableScope();
        // Env var set and dir exists -> dir returned
        Directory.CreateDirectory(envDir);
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AGENT_TEMP_DIRECTORY, envDir);
        testSubject.GetAgentTempDirectory().Should().Be(envDir);
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestMethod]
    public void Fallback_FilesLocatedCorrectly_Windows_Mac()
    {
        var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "TestResults");
        var subDir = Path.Combine(dir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);

        TestUtils.CreateTextFile(dir, "foo.coverageXXX", "1");              // wrong file extension
        TestUtils.CreateTextFile(dir, "abc.trx", "2");                      // wrong file extension
        TestUtils.CreateTextFile(dir, "BAR.coverage.XXX", string.Empty);    // wrong file extension
        var lowerCasePath = TestUtils.CreateTextFile(dir, "foo.coverage", "3");
        var upperCasePath = TestUtils.CreateTextFile(subDir, "BAR.COVERAGE", "5");
        var duplicate1FilePath = TestUtils.CreateTextFile(dir, "DUPLICATE.coverage", "4");
        var duplicate2FilePath = TestUtils.CreateTextFile(dir, "Duplicate.coverage", "4");

        using var envVars = new EnvironmentVariableScope();
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AGENT_TEMP_DIRECTORY, dir);
        testSubject.FindCoverageFiles().Should().Satisfy(
            x => x == lowerCasePath,
            x => x == upperCasePath,
            x => x == duplicate1FilePath || x == duplicate2FilePath);
    }

    [TestCategory(TestCategories.NoWindows)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void Fallback_FilesLocatedCorrectly_Linux()
    {
        var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "TestResults");
        var subDir = Path.Combine(dir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);

        TestUtils.CreateTextFile(dir, "foo.coverageXXX", "1");              // wrong file extension
        TestUtils.CreateTextFile(dir, "abc.trx", "2");                      // wrong file extension
        TestUtils.CreateTextFile(dir, "BAR.coverage.XXX", string.Empty);    // wrong file extension
        var lowerCasePath = TestUtils.CreateTextFile(dir, "foo.coverage", "3");
        var upperCasePath = TestUtils.CreateTextFile(subDir, "BAR.COVERAGE", "5");
        var duplicate1FilePath = TestUtils.CreateTextFile(dir, "DUPLICATE.coverage", "4");
        var duplicate2FilePath = TestUtils.CreateTextFile(dir, "Duplicate.coverage", "4");

        using var envVars = new EnvironmentVariableScope();
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AGENT_TEMP_DIRECTORY, dir);
        testSubject.FindCoverageFiles().Should().Satisfy(
            x => x == lowerCasePath,    // should also find upperCasePath but does not due to case-sensitivity
            x => x == duplicate1FilePath || x == duplicate2FilePath);
    }

    [TestMethod]
    public void Fallback_CalculatesAndDeDupesOnContentCorrectly()
    {
        var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "TestResults");
        var subDir = Path.Combine(dir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);

        var file1 = "file1.coverage";
        var file2 = "file2.coverage";
        var file3 = "file3.coverage";
        var file1Duplicate = "file1Duplicate.coverage";
        var filePath1 = TestUtils.CreateTextFile(dir, file1, file1);
        var filePath2 = TestUtils.CreateTextFile(dir, file2, file2);
        var filePath3 = TestUtils.CreateTextFile(dir, file3, file3);
        var filePath1Duplicate = TestUtils.CreateTextFile(dir, file1Duplicate, file1);
        var filePath1SubDir = TestUtils.CreateTextFile(subDir, file1, file1);

        using var envVars = new EnvironmentVariableScope();
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AGENT_TEMP_DIRECTORY, dir);
        var result = testSubject.FindCoverageFiles().ToList();
        result.Should().HaveCount(3, "the 5 files should be de-duped based on content hash.");
        result.Should().Satisfy(
            x => x == filePath1 || x == filePath1Duplicate || x == filePath1SubDir,
            x => x == filePath2,
            x => x == filePath3);
    }

    [TestMethod]
    [DataRow(new byte[] { 1, 2 }, new byte[] { 1, 2 }, true)]
    [DataRow(new byte[] { 1, 2, 3 }, new byte[] { 1, 2 }, false)]
    [DataRow(new byte[] { 1, 2 }, new byte[] { 1, 2, 3 }, false)]
    [DataRow(new byte[] { 1, 2 }, new byte[] { 1, 3 }, false)]
    [DataRow(new byte[] { }, new byte[] { 1 }, false)]
    public void Fallback_FileHashComparer_SimpleComparisons_DifferentHashes(byte[] hash1, byte[] hash2, bool expected)
    {
        var testSubject = new BuildVNextCoverageSearchFallback.FileHashComparer();
        testSubject.Equals(
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path1.txt", hash1),
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path2.txt", hash2))
            .Should().Be(expected);
    }

    [TestMethod]
    [DataRow("File.txt", "File.txt")]
    [DataRow("File.txt", "FileOther.txt")]
    [DataRow("FileOther.txt", "File.txt")]
    [DataRow("File.txt", null)]
    [DataRow("File.txt", "")]
    [DataRow("", "File.txt")]
    [DataRow(null, "File.txt")]
    public void Fallback_FileHashComparer_SimpleComparisons_SameHash_Filenames(string fileName1, string fileName2)
    {
        var testSubject = new BuildVNextCoverageSearchFallback.FileHashComparer();

        // file name is not considered by the FileHashComparer, only the hash
        testSubject.Equals(
            new BuildVNextCoverageSearchFallback.FileWithContentHash(fileName1, [1, 2]),
            new BuildVNextCoverageSearchFallback.FileWithContentHash(fileName2, [1, 2]))
            .Should().BeTrue();
    }

    [TestMethod]
    public void Fallback_FileHashComparer_CorrectlyDeDupesList()
    {
        var comparer = new BuildVNextCoverageSearchFallback.FileHashComparer();
        BuildVNextCoverageSearchFallback.FileWithContentHash[] input =
        [
            new BuildVNextCoverageSearchFallback.FileWithContentHash(string.Empty, [1]),
            new BuildVNextCoverageSearchFallback.FileWithContentHash(string.Empty, [1]),
            new BuildVNextCoverageSearchFallback.FileWithContentHash(string.Empty, [1, 2]),
            new BuildVNextCoverageSearchFallback.FileWithContentHash(string.Empty, [1, 2]),
            new BuildVNextCoverageSearchFallback.FileWithContentHash(string.Empty, [1, 2, 3]),
            new BuildVNextCoverageSearchFallback.FileWithContentHash(string.Empty, [1, 2, 3])
        ];

        input.Distinct(comparer).Should().BeEquivalentTo([
            new BuildVNextCoverageSearchFallback.FileWithContentHash(string.Empty, [1]),
            new BuildVNextCoverageSearchFallback.FileWithContentHash(string.Empty, [1, 2]),
            new BuildVNextCoverageSearchFallback.FileWithContentHash(string.Empty, [1, 2, 3])]);
    }
}
