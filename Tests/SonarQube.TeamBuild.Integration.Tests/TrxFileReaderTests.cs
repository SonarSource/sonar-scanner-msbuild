//-----------------------------------------------------------------------
// <copyright file="TrxFileReaderTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.TeamBuild.Integration.Tests
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
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            TestLogger logger = new TestLogger();

            // Act
            string coverageFilePath = TrxFileReader.LocateCodeCoverageFile(testDir, logger);

            // Assert
            Assert.AreEqual(null, coverageFilePath);

            // Not expecting errors or warnings: we assume it means that tests have not been executed
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod, TestCategory("CodeCoverage")]
        public void TrxReader_InvalidTrxFile()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string resultsDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "TestResults");
            string textFile = CreateTextFile(resultsDir, "dummy.trx", "this is not a trx file");
            TestLogger logger = new TestLogger();

            // Act
            string coverageFilePath = TrxFileReader.LocateCodeCoverageFile(testDir, logger);

            // Assert
            Assert.AreEqual(null, coverageFilePath);

            logger.AssertSingleWarningExists("dummy.trx"); // expecting a warning about the invalid file
            logger.AssertErrorsLogged(0); // should be a warning, not an error
        }

        [TestMethod, TestCategory("CodeCoverage")]
        public void TrxReader_MultipleTrxFiles()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string resultsDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "TestResults");
            string trx1 = CreateTextFile(resultsDir, "mytrx1.trx", "<TestRun />");
            string trx2 = CreateTextFile(resultsDir, "mytrx2.trx", "<TestRun />");
            TestLogger logger = new TestLogger();

            // Act
            string coverageFilePath = TrxFileReader.LocateCodeCoverageFile(testDir, logger);

            // Assert
            Assert.AreEqual(null, coverageFilePath);

            logger.AssertSingleWarningExists(trx1, trx2); // expecting a warning referring the log files
            logger.AssertErrorsLogged(0);
        }

        [TestMethod, TestCategory("CodeCoverage")]
        public void TrxReader_TrxWithNoAttachments()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string resultsDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "TestResults");
            string trxFile = CreateTextFile(resultsDir, "no_attachments.trx",
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
            TestLogger logger = new TestLogger();

            // Act
            string coverageFilePath = TrxFileReader.LocateCodeCoverageFile(testDir, logger);

            // Assert
            Assert.AreEqual(null, coverageFilePath);

            // Not finding attachment info in the file shouldn't cause a warning/error
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
            logger.AssertMessageExists(trxFile); // should be a message referring to the trx
        }

        [TestMethod, TestCategory("CodeCoverage")]
        [Description("Tests handling of a trx file that contains information about multiple code coverage runs (i.e. an error case, as we're not expecting this)")]
        public void TrxReader_TrxWithMultipleAttachments()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string resultsDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "TestResults");
            string trxFile
                = CreateTextFile(resultsDir, "multiple_attachments.trx",
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
            TestLogger logger = new TestLogger();

            // Act
            string coverageFilePath = TrxFileReader.LocateCodeCoverageFile(testDir, logger);

            // Assert
            Assert.AreEqual(null, coverageFilePath);
            
            logger.AssertSingleWarningExists(@"MACHINENAME\AAA.coverage", @"XXX.coverage"); // the warning should refer to both of the coverage files
            logger.AssertErrorsLogged(0);
        }

        [TestMethod, TestCategory("CodeCoverage")]
        [Description("Tests handling of a trx file that contains a single code coverage attachment with a non-rooted path")]
        public void TrxReader_SingleAttachment_RelativePath()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string resultsDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "TestResults");
            string coverageFileName = "MACHINENAME\\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";

            string textFile = CreateTextFile(resultsDir, "single_attachment.trx",
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

            TestLogger logger = new TestLogger();

            // Act
            string coverageFilePath = TrxFileReader.LocateCodeCoverageFile(testDir, logger);

            // Assert
            string expected = Path.Combine(resultsDir, "single_attachment", "In", coverageFileName);

            Assert.AreEqual(expected, coverageFilePath);

            logger.AssertMessageExists(coverageFileName);
        }

        [TestMethod, TestCategory("CodeCoverage")]
        [Description("Tests handling of a trx file that contains a single code coverage attachment with a rooted path")]
        public void TrxReader_SingleAttachment_AbsolutePath()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string resultsDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "TestResults");
            string coverageFileName = "x:\\dir1\\dir2\\xxx.coverage";

            string textFile = CreateTextFile(resultsDir, "single_attachment.trx",
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

            TestLogger logger = new TestLogger();

            // Act
            string coverageFilePath = TrxFileReader.LocateCodeCoverageFile(testDir, logger);

            // Assert
            Assert.AreEqual(coverageFilePath, coverageFilePath);
            logger.AssertMessageExists(coverageFileName);
        }

        #endregion

        #region Private methods

        private static string CreateTextFile(string parentDir, string fileName, string content, params string[] args)
        {
            Assert.IsTrue(Directory.Exists(parentDir), "Test setup error: expecting the parent directory to exist: {0}", parentDir);
            string fullPath = Path.Combine(parentDir, fileName);

            string formattedContent = content;
            if (args != null && args.Any())
            {
                formattedContent = string.Format(System.Globalization.CultureInfo.InvariantCulture, content, args);
            }

            File.WriteAllText(fullPath, formattedContent);
            return fullPath;
        }

        #endregion

    }
}
