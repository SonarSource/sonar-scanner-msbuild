/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.TFS.Tests
{
    [TestClass]
    public class BuildVNextCoverageSeachFallbackTests
    {
        public TestContext TestContext { get; set; }
        private static List<string> filesToDelete = new List<string>();

        [TestMethod]
        public void Fallback_AgentDirectory_CalculatedCorrectly()
        {
            // Arrange
            var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
            var rootDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
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

        [TestMethod]
        public void Fallback_FilesLocatedCorrectly()
        {
            // Arrange
            var testSubject = new BuildVNextCoverageSearchFallback(new TestLogger());
            var dir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            var subDir = Path.Combine(dir, "subDir", "subDir2");
            Directory.CreateDirectory(subDir);

            TestUtils.CreateTextFile(dir, "foo.coverageXXX", "");
            TestUtils.CreateTextFile(dir, "abc.trx", "");
            var expected1 = TestUtils.CreateTextFile(dir, "foo.coverage", "");
            var expected2 = TestUtils.CreateTextFile(dir, "DUPLICATE.coverage", "");

            TestUtils.CreateTextFile(dir, "BAR.coverage.XXX", "");
            TestUtils.CreateTextFile(dir, "Duplicate.coverage", ""); // appears in both places - only one should be returned
            var expected3 = TestUtils.CreateTextFile(subDir, "BAR.COVERAGE", ""); // should be found

            filesToDelete.AddRange(new List<string>()
            {
                Path.Combine(dir, "foo.coverageXXX"),
                Path.Combine(dir, "abc.trx"),
                Path.Combine(dir, "foo.coverage"),
                Path.Combine(dir, "DUPLICATE.coverage"),
                Path.Combine(dir, "BAR.coverage.XXX"),
                Path.Combine(dir, "Duplicate.coverage"),
                Path.Combine(subDir, "BAR.COVERAGE")
            });

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
        public void Fallback_FileNameComparer_SimpleComparisons()
        {
            var testSubject = new BuildVNextCoverageSearchFallback.FileNameComparer();

            // Identical string -> same
            testSubject.Equals("c:\\File1.txt", "c:\\File1.txt").Should().BeTrue();

            // Case only -> same
            testSubject.Equals("c:\\File1.txt", "c:\\FILE1.txt").Should().BeTrue();

            // Different folders -> same
            testSubject.Equals("c:\\File1.txt", "c:\\aaa\\File1.txt").Should().BeTrue();
            testSubject.Equals("c:\\aaa\\bbb\\File1.txt", "c:\\aaa\\File1.txt").Should().BeTrue();

            // Different folders and path separators -> same
            testSubject.Equals("c:/File1.txt", "c:/aaa/File1.txt").Should().BeTrue();
            testSubject.Equals("c:/aaa/bbb/File1.txt", "c:/aaa/File1.txt").Should().BeTrue();

            // Diferent name -> different
            testSubject.Equals("c:\\File1.txt", "c:\\File2.txt").Should().BeFalse();
        }

        [TestMethod]
        public void Fallback_FileNameComparer_CorrectlyDeDupesList()
        {
            // Arrange
            var comparer = new BuildVNextCoverageSearchFallback.FileNameComparer();
            string[] input = {
                "c:\\File1.txt",
                "c:\\File1.txt",
                "c:\\aaa\\FILE1.txt",
                "c:\\aaa\\bbb\\file1.TXT",

                "c:/aaa/FILE2.TXT",
                "d:\\FILE2.txt",

                "file3.txt"
            };

            // Act
            var actual = input.Distinct(comparer).ToArray();

            // Assert
            actual.Should().BeEquivalentTo(
                "c:\\File1.txt",
                "c:/aaa/FILE2.TXT",
                "file3.txt"
                );
        }

        [ClassCleanup]
        public static void AddFilesToDeleteToEnv()
        {
            filesToDelete = filesToDelete.Distinct().ToList();

            var currentEnvValue = Environment.GetEnvironmentVariable("TEST_FILE_TO_DELETE", EnvironmentVariableTarget.User) ?? String.Empty;
            if (!string.IsNullOrEmpty(currentEnvValue))
            {
                currentEnvValue += ";;";
            }

            Environment.SetEnvironmentVariable("TEST_FILE_TO_DELETE", currentEnvValue + string.Join(";;",
                filesToDelete), EnvironmentVariableTarget.User);
        }
    }
}
