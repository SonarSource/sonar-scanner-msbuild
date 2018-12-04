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

using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.TFS.Tests
{
    [TestClass]
    public class TrxFileReaderTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod, TestCategory("CodeCoverage")]
        public void TrxReader_TestsResultsDirectoryMissing()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var logger = new TestLogger();

            // Act
            var coverageFilePaths = new TrxFileReader(logger).FindCodeCoverageFiles(testDir);

            // Assert
            coverageFilePaths.Should().BeEmpty();

            // Not expecting errors or warnings: we assume it means that tests have not been executed
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod, TestCategory("CodeCoverage")]
        public void TrxReader_InvalidTrxFile()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var resultsDir = TestUtils.CreateTestSpecificFolder(TestContext, "TestResults");
            TestUtils.CreateTextFile(resultsDir, "dummy.trx", "this is not a trx file");
            var logger = new TestLogger();

            // Act
            var coverageFilePaths = new TrxFileReader(logger).FindCodeCoverageFiles(testDir);

            // Assert
            coverageFilePaths.Should().BeEmpty();

            logger.AssertSingleInfoMessageExists("No code coverage attachments were found from the trx files.");
            logger.Warnings.Should().HaveCount(1);
            logger.Warnings[0].Should().Match(@"Located trx file is not a valid xml file. File: *\Out\TrxReader_InvalidTrxFile\TestResults\dummy.trx. File load error: Data at the root level is invalid. Line 1, position 1."); // expecting a warning about the invalid file
            logger.AssertErrorsLogged(0); // should be a warning, not an error
        }

        [TestMethod, TestCategory("CodeCoverage")]
        public void TrxReader_MultipleTrxFiles()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var resultsDir = TestUtils.CreateTestSpecificFolder(TestContext, "TestResults");
            var trx1 = TestUtils.CreateTextFile(resultsDir, "mytrx1.trx", "<TestRun />");
            var trx2 = TestUtils.CreateTextFile(resultsDir, "mytrx2.trx", "<TestRun />");
            var logger = new TestLogger();

            // Act
            var coverageFilePaths = new TrxFileReader(logger).FindCodeCoverageFiles(testDir);

            // Assert
            coverageFilePaths.Should().BeEmpty();

            logger.DebugMessages.Should().HaveCount(0);
            logger.AssertSingleInfoMessageExists("No code coverage attachments were found from the trx files.");
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);
        }

        [TestMethod, TestCategory("CodeCoverage")]
        public void TrxReader_SingleTrxFileInSubfolder()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var resultsDir = TestUtils.CreateTestSpecificFolder(TestContext, "Dummy", "TestResults");
            var trxFile = TestUtils.CreateTextFile(resultsDir, "no_attachments.trx",
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestRun id=""eb906034-f363-4bf0-ac6a-29fa47645f67""
    name=""LOCAL SERVICE@MACHINENAME 2015-05-06 08:38:39"" runUser=""NT AUTHORITY\LOCAL SERVICE"" xmlns=""http://microsoft.com/schemas/VisualStudio/TeamTest/2010"">
  <TestSettings name=""default"" id=""bf0f0911-87a2-4413-aa12-36e177a9c5b3"" />
  <ResultSummary outcome=""Completed"">
    <Counters total=""123"" executed=""123"" passed=""123"" failed=""0"" error=""0"" timeout=""0"" aborted=""0"" inconclusive=""0"" passedButRunAborted=""0"" notRunnable=""0"" notExecuted=""0"" disconnected=""0"" warning=""0"" completed=""0"" inProgress=""0"" pending=""0"" />
    <RunInfos />
    <CollectorDataEntries />
  </ResultSummary>
</TestRun>
");
            var logger = new TestLogger();

            // Act
            var coverageFilePaths = new TrxFileReader(logger).FindCodeCoverageFiles(testDir);

            // Assert
            coverageFilePaths.Should().BeEmpty();

            // Not finding attachment info in the file shouldn't cause a warning/error
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
            logger.AssertSingleInfoMessageExists("No code coverage attachments were found from the trx files.");
        }

        [TestMethod, TestCategory("CodeCoverage")]
        public void TrxReader_TrxWithNoAttachments()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var resultsDir = TestUtils.CreateTestSpecificFolder(TestContext, "TestResults");
            var trxFile = TestUtils.CreateTextFile(resultsDir, "no_attachments.trx",
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestRun id=""eb906034-f363-4bf0-ac6a-29fa47645f67""
    name=""LOCAL SERVICE@MACHINENAME 2015-05-06 08:38:39"" runUser=""NT AUTHORITY\LOCAL SERVICE"" xmlns=""http://microsoft.com/schemas/VisualStudio/TeamTest/2010"">
  <TestSettings name=""default"" id=""bf0f0911-87a2-4413-aa12-36e177a9c5b3"" />
  <ResultSummary outcome=""Completed"">
    <Counters total=""123"" executed=""123"" passed=""123"" failed=""0"" error=""0"" timeout=""0"" aborted=""0"" inconclusive=""0"" passedButRunAborted=""0"" notRunnable=""0"" notExecuted=""0"" disconnected=""0"" warning=""0"" completed=""0"" inProgress=""0"" pending=""0"" />
    <RunInfos />
    <CollectorDataEntries />
  </ResultSummary>
</TestRun>
");
            var logger = new TestLogger();

            // Act
            var coverageFilePaths = new TrxFileReader(logger).FindCodeCoverageFiles(testDir);

            // Assert
            coverageFilePaths.Should().BeEmpty();

            // Not finding attachment info in the file shouldn't cause a warning/error
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
            logger.AssertSingleInfoMessageExists("No code coverage attachments were found from the trx files.");
        }

        [TestMethod, TestCategory("CodeCoverage")]
        [Description("Tests handling of a trx file that contains information about multiple code coverage runs (i.e. an error case, as we're not expecting this)")]
        public void TrxReader_TrxWithMultipleAttachments()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var resultsDir = TestUtils.CreateTestSpecificFolder(TestContext, "TestResults");

            TestUtils.CreateTextFile(resultsDir, "multiple_attachments.trx",
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestRun id=""eb906034-f363-4bf0-ac6a-29fa47645f67""
    name=""LOCAL SERVICE@MACHINENAME 2015-05-06 08:38:39"" runUser=""NT AUTHORITY\LOCAL SERVICE""
    xmlns=""http://microsoft.com/schemas/VisualStudio/TeamTest/2010"">
  <ResultSummary outcome=""Completed"">
    <Counters total=""123"" executed=""123"" passed=""123"" failed=""0"" error=""0"" timeout=""0"" aborted=""0"" inconclusive=""0"" passedButRunAborted=""0"" notRunnable=""0"" notExecuted=""0"" disconnected=""0"" warning=""0"" completed=""0"" inProgress=""0"" pending=""0"" />
    <RunInfos />
    <CollectorDataEntries>
      <Collector agentName=""MACHINENAME"" uri=""datacollector://microsoft/CodeCoverage/2.0"" collectorDisplayName=""Code Coverage"">
        <UriAttachments>
          <UriAttachment>
            <A href=""MACHINENAME\AAA.coverage"">
            </A>
          </UriAttachment>
        </UriAttachments>
      </Collector>
      <Collector agentName=""MACHINENAME"" uri=""datacollector://microsoft/CodeCoverage/2.0"" collectorDisplayName=""Code Coverage"">
        <UriAttachments>
          <UriAttachment>
            <A href=""XXX.coverage"">
            </A>
          </UriAttachment>
        </UriAttachments>
      </Collector>
    </CollectorDataEntries>
  </ResultSummary>
</TestRun>
");
            var logger = new TestLogger();

            // Act
            var coverageFilePaths = new TrxFileReader(logger).FindCodeCoverageFiles(testDir);

            // Assert
            coverageFilePaths.Should().BeEmpty();

            logger.Warnings.Should().HaveCount(2);
            logger.Warnings[0].Should().Match(@"None of the following coverage attachments could be found: MACHINENAME\AAA.coverage, *\Out\TrxReader_TrxWithMultipleAttachments\TestResults\multiple_attachments\In\MACHINENAME\AAA.coverage, *\Out\TrxReader_TrxWithMultipleAttachments\TestResults\multiple_attachments\In\MACHINENAME\AAA.coverage. Trx file: *\Out\TrxReader_TrxWithMultipleAttachments\TestResults\multiple_attachments.trx");
            logger.Warnings[1].Should().Match(@"None of the following coverage attachments could be found: XXX.coverage, *\Out\TrxReader_TrxWithMultipleAttachments\TestResults\multiple_attachments\In\XXX.coverage, *\Out\TrxReader_TrxWithMultipleAttachments\TestResults\multiple_attachments\In\XXX.coverage. Trx file: *\Out\TrxReader_TrxWithMultipleAttachments\TestResults\multiple_attachments.trx");
            logger.AssertErrorsLogged(0);
        }

        [TestMethod, TestCategory("CodeCoverage")]
        [Description("Tests handling of a trx file that contains a single code coverage attachment with a non existing file")]
        public void TrxReader_SingleAttachment_PathDoesNotExist()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var resultsDir = TestUtils.CreateTestSpecificFolder(TestContext, "TestResults");
            var coverageFileName = "MACHINENAME\\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";

            TestUtils.CreateTextFile(resultsDir, "single_attachment.trx",
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestRun id=""eb906034-f363-4bf0-ac6a-29fa47645f67""
    name=""LOCAL SERVICE@MACHINENAME 2015-05-06 08:38:39"" runUser=""NT AUTHORITY\LOCAL SERVICE""
    xmlns=""http://microsoft.com/schemas/VisualStudio/TeamTest/2010"">
  <ResultSummary outcome=""Completed"">
    <Counters total=""123"" executed=""123"" passed=""123"" failed=""0"" error=""0"" timeout=""0"" aborted=""0"" inconclusive=""0"" passedButRunAborted=""0"" notRunnable=""0"" notExecuted=""0"" disconnected=""0"" warning=""0"" completed=""0"" inProgress=""0"" pending=""0"" />
    <RunInfos />
    <CollectorDataEntries>
      <Collector agentName=""MACHINENAME"" uri=""datacollector://microsoft/CodeCoverage/2.0"" collectorDisplayName=""Code Coverage"">
        <UriAttachments>
          <UriAttachment>
            <A href=""{0}"">
            </A>
          </UriAttachment>
        </UriAttachments>
      </Collector>
    </CollectorDataEntries>
  </ResultSummary>
</TestRun>",
           coverageFileName);

            var logger = new TestLogger();

            // Act
            var coverageFilePaths = new TrxFileReader(logger).FindCodeCoverageFiles(testDir);

            // Assert
            coverageFilePaths.Should().BeEmpty();

            logger.Warnings.Should().HaveCount(1);
            logger.Warnings[0].Should().Match(@"None of the following coverage attachments could be found: MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage, *\Out\TrxReader_SingleAttachment_PathDoesNotExist\TestResults\single_attachment\In\MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage, *\Out\TrxReader_SingleAttachment_PathDoesNotExist\TestResults\single_attachment\In\MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage. Trx file: *\Out\TrxReader_SingleAttachment_PathDoesNotExist\TestResults\single_attachment.trx");
        }

        [TestMethod, TestCategory("CodeCoverage")]
        [Description("Tests handling of a trx file that contains a single code coverage attachment with a non-rooted path")]
        public void TrxReader_SingleAttachment_Path1()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var resultsDir = TestUtils.CreateTestSpecificFolder(TestContext, "TestResults");
            var coverageFileName = "MACHINENAME\\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";

            TestUtils.CreateTextFile(resultsDir, "single attachment.trx",
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestRun id=""eb906034-f363-4bf0-ac6a-29fa47645f67""
    name=""LOCAL SERVICE@MACHINENAME 2015-05-06 08:38:39"" runUser=""NT AUTHORITY\LOCAL SERVICE""
    xmlns=""http://microsoft.com/schemas/VisualStudio/TeamTest/2010"">
  <ResultSummary outcome=""Completed"">
    <Counters total=""123"" executed=""123"" passed=""123"" failed=""0"" error=""0"" timeout=""0"" aborted=""0"" inconclusive=""0"" passedButRunAborted=""0"" notRunnable=""0"" notExecuted=""0"" disconnected=""0"" warning=""0"" completed=""0"" inProgress=""0"" pending=""0"" />
    <RunInfos />
    <CollectorDataEntries>
      <Collector agentName=""MACHINENAME"" uri=""datacollector://microsoft/CodeCoverage/2.0"" collectorDisplayName=""Code Coverage"">
        <UriAttachments>
          <UriAttachment>
            <A href=""{0}"">
            </A>
          </UriAttachment>
        </UriAttachments>
      </Collector>
    </CollectorDataEntries>
  </ResultSummary>
</TestRun>",
           coverageFileName);

            var logger = new TestLogger();

            var expectedFilePath = Path.Combine(resultsDir, "single attachment", "In", coverageFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(expectedFilePath));
            File.Create(expectedFilePath);

            // Act
            var coverageFilePaths = new TrxFileReader(logger).FindCodeCoverageFiles(testDir);

            // Assert
            coverageFilePaths.Should().BeEquivalentTo(expectedFilePath);

            logger.AssertDebugMessageExists(coverageFileName);
        }

        [TestMethod, TestCategory("CodeCoverage")]
        [Description("Tests handling of a trx file that contains a single code coverage attachment with a non-rooted path")]
        public void TrxReader_SingleAttachment_Path2()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var resultsDir = TestUtils.CreateTestSpecificFolder(TestContext, "TestResults");
            var coverageFileName = "MACHINENAME\\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";

            TestUtils.CreateTextFile(resultsDir, "single_attachment.trx",
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestRun id=""eb906034-f363-4bf0-ac6a-29fa47645f67""
    name=""LOCAL SERVICE@MACHINENAME 2015-05-06 08:38:39"" runUser=""NT AUTHORITY\LOCAL SERVICE""
    xmlns=""http://microsoft.com/schemas/VisualStudio/TeamTest/2010"">
  <ResultSummary outcome=""Completed"">
    <Counters total=""123"" executed=""123"" passed=""123"" failed=""0"" error=""0"" timeout=""0"" aborted=""0"" inconclusive=""0"" passedButRunAborted=""0"" notRunnable=""0"" notExecuted=""0"" disconnected=""0"" warning=""0"" completed=""0"" inProgress=""0"" pending=""0"" />
    <RunInfos />
    <CollectorDataEntries>
      <Collector agentName=""MACHINENAME"" uri=""datacollector://microsoft/CodeCoverage/2.0"" collectorDisplayName=""Code Coverage"">
        <UriAttachments>
          <UriAttachment>
            <A href=""{0}"">
            </A>
          </UriAttachment>
        </UriAttachments>
      </Collector>
    </CollectorDataEntries>
  </ResultSummary>
</TestRun>",
           coverageFileName);

            var logger = new TestLogger();

            var expectedFilePath = Path.Combine(resultsDir, "single_attachment", "In", coverageFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(expectedFilePath));
            File.Create(expectedFilePath);

            // Act
            var coverageFilePaths = new TrxFileReader(logger).FindCodeCoverageFiles(testDir);

            // Assert
            coverageFilePaths.Should().BeEquivalentTo(expectedFilePath);

            logger.AssertDebugMessageExists(coverageFileName);
        }

        [TestMethod, TestCategory("CodeCoverage")]
        [Description("Tests handling of a trx file that contains a single code coverage attachment with a rooted path")]
        public void TrxReader_SingleAttachment_AbsolutePath()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var resultsDir = TestUtils.CreateTestSpecificFolder(TestContext, "TestResults");
            var coverageFileName = "x:\\dir1\\dir2\\xxx.coverage";

            TestUtils.CreateTextFile(resultsDir, "single_attachment.trx",
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestRun id=""eb906034-f363-4bf0-ac6a-29fa47645f67""
    name=""LOCAL SERVICE@MACHINENAME 2015-05-06 08:38:39"" runUser=""NT AUTHORITY\LOCAL SERVICE""
    xmlns=""http://microsoft.com/schemas/VisualStudio/TeamTest/2010"">
  <ResultSummary outcome=""Completed"">
    <Counters total=""123"" executed=""123"" passed=""123"" failed=""0"" error=""0"" timeout=""0"" aborted=""0"" inconclusive=""0"" passedButRunAborted=""0"" notRunnable=""0"" notExecuted=""0"" disconnected=""0"" warning=""0"" completed=""0"" inProgress=""0"" pending=""0"" />
    <RunInfos />
    <CollectorDataEntries>
      <Collector agentName=""MACHINENAME"" uri=""datacollector://microsoft/CodeCoverage/2.0"" collectorDisplayName=""Code Coverage"">
        <UriAttachments>
          <UriAttachment>
            <A href=""{0}"">
            </A>
          </UriAttachment>
        </UriAttachments>
      </Collector>
    </CollectorDataEntries>
  </ResultSummary>
</TestRun>",
           coverageFileName);

            var logger = new TestLogger();

            // Act
            var coverageFilePaths = new TrxFileReader(logger, x => true).FindCodeCoverageFiles(testDir);

            // Assert
            coverageFilePaths.Should().BeEquivalentTo(coverageFilePaths);
            logger.AssertDebugMessageExists(@"Absolute path to coverage file: x:\dir1\dir2\xxx.coverage");
        }

        #endregion Tests
    }
}
