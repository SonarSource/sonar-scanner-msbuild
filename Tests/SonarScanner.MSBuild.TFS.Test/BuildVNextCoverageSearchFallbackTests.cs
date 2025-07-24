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
    public void Fallback_AgentDirectory_CalculatedCorrectly()
    {
        var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
        var rootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var envDir = Path.Combine(rootDir, "DirSpecifiedInEnvDir");

        using var envVars = new EnvironmentVariableScope();
        // 1) env var not specified -> null
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AGENT_TEMP_DIRECTORY, null);
        testSubject.GetAgentTempDirectory().Should().BeNull();

        // 2) Env var set but dir does not exist -> null
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AGENT_TEMP_DIRECTORY, envDir);
        testSubject.GetAgentTempDirectory().Should().Be(null);

        // 3) Env var set and dir exists -> dir returned
        Directory.CreateDirectory(envDir);
        testSubject.GetAgentTempDirectory().Should().Be(envDir);
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void Fallback_FilesLocatedCorrectly_Windows()
    {
        var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "TestResults");
        var subDir = Path.Combine(dir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);

        TestUtils.CreateTextFile(dir, "foo.coverageXXX", "1");
        TestUtils.CreateTextFile(dir, "abc.trx", "2");
        var expected1 = TestUtils.CreateTextFile(dir, "foo.coverage", "3");
        var expected2 = TestUtils.CreateTextFile(dir, "DUPLICATE.coverage", "4");

        TestUtils.CreateTextFile(dir, "BAR.coverage.XXX", string.Empty);
        TestUtils.CreateTextFile(dir, "Duplicate.coverage", "4"); // appears in both places - only one should be returned
        var expected3 = TestUtils.CreateTextFile(subDir, "BAR.COVERAGE", "5"); // should be found

        using var envVars = new EnvironmentVariableScope();
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AGENT_TEMP_DIRECTORY, dir);
        testSubject.FindCoverageFiles().Should().BeEquivalentTo(expected1, expected2, expected3);
    }

    [TestCategory(TestCategories.NoWindows)]
    [TestMethod]
    public void Fallback_FilesLocatedCorrectly_Unix()
    {
        var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "TestResults");
        var subDir = Path.Combine(dir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);

        TestUtils.CreateTextFile(dir, "foo.coverageXXX", "1");
        TestUtils.CreateTextFile(dir, "abc.trx", "2");
        var expected1 = TestUtils.CreateTextFile(dir, "foo.coverage", "3");
        var expected2 = TestUtils.CreateTextFile(dir, "DUPLICATE.coverage", "4");

        TestUtils.CreateTextFile(dir, "BAR.coverage.XXX", string.Empty);
        TestUtils.CreateTextFile(dir, "Duplicate.coverage", "4"); // appears in both places - only one should be returned
        var expected3 = TestUtils.CreateTextFile(subDir, "BAR.COVERAGE", "5"); // should be found

        using var envVars = new EnvironmentVariableScope();
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AGENT_TEMP_DIRECTORY, dir);
        testSubject.FindCoverageFiles().Should().BeEquivalentTo(expected1, expected2);
    }

    [TestMethod]
    public void Fallback_CalculatesAndDeDupesOnContentCorrectly()
    {
        var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "TestResults");
        var subDir = Path.Combine(dir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);

        var file1 = "fileName1.coverage";
        var file2 = "fileName2.coverage";
        var file3 = "fileName3.coverage";
        var file1Duplicate = "file1Duplicate.coverage";
        var filePath1 = TestUtils.CreateTextFile(dir, file1, file1);
        var filePath2 = TestUtils.CreateTextFile(dir, file2, file2);
        var filePath3 = TestUtils.CreateTextFile(dir, file3, file3);
        var filePath1Duplicate = TestUtils.CreateTextFile(dir, file1Duplicate, file1);
        var filePath1SubDir = TestUtils.CreateTextFile(subDir, file1, file1);

        using var envVars = new EnvironmentVariableScope();
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AGENT_TEMP_DIRECTORY, dir);
        testSubject.FindCoverageFiles().Should().Satisfy(
            x => x == filePath1 || x == filePath1Duplicate || x == filePath1SubDir,
            x => x == filePath2,
            x => x == filePath3);
    }

    [TestMethod]
    public void Fallback_FileHashComparer_SimpleComparisons()
    {
        var testSubject = new BuildVNextCoverageSearchFallback.FileHashComparer();

        // Identical content hash, identical file name -> same
        testSubject.Equals(
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path1.txt", [1, 2]),
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path1.txt", [1, 2]))
            .Should().BeTrue();

        // Identical content hash, different file name -> same
        testSubject.Equals(
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path1.txt", [1, 2]),
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path2.txt", [1, 2]))
            .Should().BeTrue();

        // Different content hash, identical file name -> different
        testSubject.Equals(
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path1.txt", [1, 2]),
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path2.txt", [1, 3]))
            .Should().BeFalse();
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
