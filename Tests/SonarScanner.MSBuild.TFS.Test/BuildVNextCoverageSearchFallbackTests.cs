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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.TFS.Tests;

[TestClass]
public class BuildVNextCoverageSearchFallbackTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Fallback_AgentDirectory_CalculatedCorrectly()
    {
        // Arrange
        var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
        var rootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(this.TestContext);
        var envDir = Path.Combine(rootDir, "DirSpecifiedInEnvDir");

        using (var envVars = new EnvironmentVariableScope())
        {
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
    }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void Fallback_FilesLocatedCorrectly()
    {
        // Arrange
        var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "TestResults");
        var subDir = Path.Combine(dir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);

        TestUtils.CreateTextFile(dir, "foo.coverageXXX", "1");
        TestUtils.CreateTextFile(dir, "abc.trx", "2");
        var expected1 = TestUtils.CreateTextFile(dir, "foo.coverage", "3");
        var expected2 = TestUtils.CreateTextFile(dir, "DUPLICATE.coverage", "4");

        TestUtils.CreateTextFile(dir, "BAR.coverage.XXX", "");
        TestUtils.CreateTextFile(dir, "Duplicate.coverage", "4"); // appears in both places - only one should be returned
        var expected3 = TestUtils.CreateTextFile(subDir, "BAR.COVERAGE", "5"); // should be found

        using (var envVars = new EnvironmentVariableScope())
        {
            envVars.SetVariable(BuildVNextCoverageSearchFallback.AGENT_TEMP_DIRECTORY, dir);

            // Act
            var actual = testSubject.FindCoverageFiles();

            // Assert
            actual.Should().BeEquivalentTo(expected1, expected2, expected3);
        }
    }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void Fallback_CalculatesAndDeDupesOnContentCorrectly()
    {
        // Arrange
        var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "TestResults");
        var subDir = Path.Combine(dir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);

        var fileOne = "fileNameOne.coverage";
        var fileTwo = "fileNameTwo.coverage";
        var fileThree = "fileNameThree.coverage";
        var fileOneDuplicate = "fileNameOneDuplicate.coverage";
        var expected1 = TestUtils.CreateTextFile(dir, fileOne, fileOne);
        var expected2 = TestUtils.CreateTextFile(dir, fileTwo, fileTwo);
        var expected3 = TestUtils.CreateTextFile(dir, fileThree, fileThree);
        TestUtils.CreateTextFile(dir, fileOneDuplicate, fileOne); // Same content as fileOne, should not be expected
        TestUtils.CreateTextFile(subDir, fileOne, fileOne); // Same content and filename, but in other dir, as fileOne, should not be expected

        using (var envVars = new EnvironmentVariableScope())
        {
            envVars.SetVariable(BuildVNextCoverageSearchFallback.AGENT_TEMP_DIRECTORY, dir);

            // Act
            var actual = testSubject.FindCoverageFiles();

            // Assert
            actual.Should().BeEquivalentTo(expected1, expected2, expected3);
        }
    }


    [TestMethod]
    public void Fallback_FileHashComparer_SimpleComparisons()
    {
        var testSubject = new BuildVNextCoverageSearchFallback.FileHashComparer();

        // Identical content hash, identical file name -> same
        testSubject.Equals(
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path1.txt", new byte[] { 1, 2 }),
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path1.txt", new byte[] { 1, 2 })
        ).Should().BeTrue();

        // Identical content hash, different file name -> same
        testSubject.Equals(
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path1.txt", new byte[] { 1, 2 }),
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path2.txt", new byte[] { 1, 2 })
        ).Should().BeTrue();

        // Different content hash, identical file name -> different
        testSubject.Equals(
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path1.txt", new byte[] { 1, 2 }),
            new BuildVNextCoverageSearchFallback.FileWithContentHash("c:\\path2.txt", new byte[] { 1, 3 })
        ).Should().BeFalse();
    }

    [TestMethod]
    public void Fallback_FileHashComparer_CorrectlyDeDupesList()
    {
        // Arrange
        var comparer = new BuildVNextCoverageSearchFallback.FileHashComparer();
        BuildVNextCoverageSearchFallback.FileWithContentHash[] input = {
            new BuildVNextCoverageSearchFallback.FileWithContentHash("", new byte[]{ 1 }),
            new BuildVNextCoverageSearchFallback.FileWithContentHash("", new byte[]{ 1 }),
            new BuildVNextCoverageSearchFallback.FileWithContentHash("", new byte[]{ 1, 2 }),
            new BuildVNextCoverageSearchFallback.FileWithContentHash("", new byte[]{ 1, 2 }),
            new BuildVNextCoverageSearchFallback.FileWithContentHash("", new byte[]{ 1, 2, 3 }),
            new BuildVNextCoverageSearchFallback.FileWithContentHash("", new byte[]{ 1, 2, 3 })
        };

        // Act
        var actual = input.Distinct(comparer).ToArray();

        // Assert
        actual.Should().BeEquivalentTo(
            new[]
            {
                new BuildVNextCoverageSearchFallback.FileWithContentHash("", new byte[]{ 1 }),
                new BuildVNextCoverageSearchFallback.FileWithContentHash("", new byte[]{ 1, 2 }),
                new BuildVNextCoverageSearchFallback.FileWithContentHash("", new byte[]{ 1, 2, 3 })
            }
        );
    }
}
