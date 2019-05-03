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


using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.TFS.Interfaces;
using SonarScanner.MSBuild.TFS.Tests.Infrastructure;
using TestUtilities;

namespace SonarScanner.MSBuild.TFS.Tests
{
    [TestClass]
    public class BuildVNextCoverageReportProcessorTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void SearchFallbackShouldBeCalled_IfNoTrxFilesFound()
        {
            var mockSearchFallback = new MockSearchFallback();
            mockSearchFallback.SetReturnedFiles("file1.txt", "file2.txt");
            var testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            var testSubject = new BuildVNextCoverageReportProcessor(new MockReportConverter(), new TestLogger(), mockSearchFallback);

            var settings = new MockBuildSettings
            {
                BuildDirectory = testDir
            };

            IEnumerable<string> trxFilePaths = null;
            bool result = testSubject.TryGetTrxFilesAccessor(new Common.AnalysisConfig(), settings, out trxFilePaths);

            // Assert
            result.Should().BeTrue(); // expecting true i.e. carry on even if nothing found
            testSubject.TrxFilesLocated.Should().BeFalse();

            IEnumerable<string> binaryFilePaths = null;
            result = testSubject.TryGetVsCoverageFilesAccessor(new Common.AnalysisConfig(), settings, out binaryFilePaths);
            result.Should().BeTrue();
            binaryFilePaths.Should().BeEquivalentTo("file1.txt", "file2.txt");

            mockSearchFallback.FallbackCalled.Should().BeTrue();
        }

        [TestMethod]
        public void SearchFallbackNotShouldBeCalled_IfTrxFilesFound()
        {
            var mockSearchFallback = new MockSearchFallback();
            var testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            var testResultsDir = Path.Combine(testDir, "TestResults");
            Directory.CreateDirectory(testResultsDir);
            TestUtils.CreateTextFile(testResultsDir, "dummy.trx", "");

            var testSubject = new BuildVNextCoverageReportProcessor(new MockReportConverter(), new TestLogger(), mockSearchFallback);

            var settings = new MockBuildSettings
            {
                BuildDirectory = testDir
            };

            IEnumerable<string> trxFilePaths = null;
            bool result = testSubject.TryGetTrxFilesAccessor(new Common.AnalysisConfig(), settings, out trxFilePaths);

            // 1) Search for TRX files -> results found
            result.Should().BeTrue();
            testSubject.TrxFilesLocated.Should().BeTrue();

            // 2) Now search for .coverage files
            IEnumerable<string> binaryFilePaths = null;
            result = testSubject.TryGetVsCoverageFilesAccessor(new Common.AnalysisConfig(), settings, out binaryFilePaths);
            result.Should().BeTrue();
            binaryFilePaths.Should().BeEmpty();

            mockSearchFallback.FallbackCalled.Should().BeFalse();
        }
    }
}
