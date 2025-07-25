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
public class TrxFileReaderTests
{
    private TestLogger logger;
    private IFileWrapper fileMock;
    private IDirectoryWrapper directoryMock;
    private TrxFileReader trxReader;

    public TestContext TestContext { get; set; }

    /// <summary>
    /// The directory where test results are supposed to be stored. It could be anything, because
    /// we don't create actual files, but regardless we still use test-specific folders.
    /// </summary>
    private string RootDirectory => TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

    [TestInitialize]
    public void TestInitialize()
    {
        fileMock = Substitute.For<IFileWrapper>();
        fileMock.Exists(Arg.Any<string>()).Returns(false);

        directoryMock = Substitute.For<IDirectoryWrapper>();
        directoryMock.Exists(Arg.Any<string>()).Returns(false);
        directoryMock.Exists(RootDirectory).Returns(true);

        logger = new TestLogger();
        trxReader = new TrxFileReader(logger, fileMock, directoryMock);
    }

    [TestMethod]
    public void TrxReader_TestsResultsDirectoryMissing() =>
        // With no call to CreateDirectories, Directory.GetDirectories(RootDirectory) will return an empty array.
        AssertFindCodeCoverageFiles();

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void TrxReader_InvalidTrxFile()
    {
        CreateDirectory(RootDirectory, "dummy.trx", "this is not a trx file");
        trxReader.FindCodeCoverageFiles(RootDirectory).Should().BeEmpty();
        logger.AssertSingleInfoMessageExists("No code coverage attachments were found from the trx files.");
        logger.Warnings.Should().ContainSingle().Which.Should()
            .Match(@"Located trx file is not a valid xml file. File: *\TestResults\dummy.trx. File load error: Data at the root level is invalid. Line 1, position 1.");
        logger.AssertErrorsLogged(0); // should be a warning, not an error
    }

    [TestMethod]
    public void TrxReader_MultipleTrxFiles()
    {
        var testResults = CreateDirectory(RootDirectory);
        var file1 = CreateFile(testResults, "mytrx1.trx", "<TestRun />");
        var file2 = CreateFile(testResults, "mytrx2.trx", "<TestRun />");
        directoryMock.GetFiles(Arg.Is<string>(x => testResults.Equals(x, StringComparison.InvariantCultureIgnoreCase)), Arg.Any<string>())
            .Returns([file1, file2]);

        AssertFindCodeCoverageFiles();
        logger.DebugMessages.Should().BeEmpty();
        logger.AssertSingleInfoMessageExists("No code coverage attachments were found from the trx files.");
    }

    [TestMethod]
    public void TrxReader_SingleTrxFileInSubfolder()
    {
        var testResults = CreateDirectory(Path.Combine(RootDirectory, "Dummy"), "no_attachments.trx", TrxContent());
        directoryMock
            .GetDirectories(Arg.Is<string>(x => RootDirectory.Equals(x, StringComparison.InvariantCultureIgnoreCase)), "TestResults", SearchOption.AllDirectories)
            .Returns([testResults]);

        AssertFindCodeCoverageFiles();
        logger.AssertSingleInfoMessageExists("No code coverage attachments were found from the trx files.");
    }

    [TestMethod]
    public void TrxReader_TrxWithNoAttachments()
    {
        CreateDirectory(RootDirectory, "no_attachments.trx", TrxContent());
        AssertFindCodeCoverageFiles();
        logger.AssertSingleInfoMessageExists("No code coverage attachments were found from the trx files.");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Tests handling of a trx file that contains information about multiple code coverage runs (i.e. an error case, as we're not expecting this)")]
    public void TrxReader_TrxWithMultipleAttachments()
    {
        CreateDirectory(RootDirectory, "multiple_attachments.trx", TrxContent("MACHINENAME\\AAA.coverage", "XXX.coverage"));
        trxReader.FindCodeCoverageFiles(RootDirectory).Should().BeEmpty();
        logger.Warnings.Should().SatisfyRespectively(
            x => x.Should().Match(
                    "None of the following coverage attachments could be found: "
                    + @"MACHINENAME\AAA.coverage, "
                    + @"*\TestResults\multiple_attachments\In\MACHINENAME\AAA.coverage, "
                    + @"*\TestResults\multiple_attachments\In\MACHINENAME\AAA.coverage. "
                    + @"Trx file: *\TestResults\multiple_attachments.trx"),
            x => x.Should().Match(
                    "None of the following coverage attachments could be found: "
                    + "XXX.coverage, "
                    + @"*\TestResults\multiple_attachments\In\XXX.coverage, "
                    + @"*\TestResults\multiple_attachments\In\XXX.coverage. "
                    + @"Trx file: *\TestResults\multiple_attachments.trx"));
        logger.AssertErrorsLogged(0);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Tests handling of a trx file that contains a single code coverage attachment with a non existing file")]
    public void TrxReader_SingleAttachment_PathDoesNotExist()
    {
        CreateDirectory(RootDirectory, "single_attachment.trx", TrxContent("MACHINENAME\\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage"));
        trxReader.FindCodeCoverageFiles(RootDirectory).Should().BeEmpty();
        logger.Warnings.Should().ContainSingle().Which.Should().Match(
            "None of the following coverage attachments could be found: "
            + @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage, "
            + @"*\TestResults\single_attachment\In\MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage, "
            + @"*\TestResults\single_attachment\In\MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage. "
            + @"Trx file: *\TestResults\single_attachment.trx");
    }

    [TestMethod]
    [Description("Tests handling of a trx file that contains a single code coverage attachment with a non-rooted path")]
    public void TrxReader_SingleAttachment_Path1()
    {
        var relativeCoveragePath = "MACHINENAME\\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";
        var resultsDir = CreateDirectory(RootDirectory, "single attachment.trx", TrxContent(relativeCoveragePath));
        var fullCoveragePath = Path.Combine(resultsDir, "single attachment", "In", relativeCoveragePath);
        CreateFile(Path.GetDirectoryName(fullCoveragePath), Path.GetFileName(fullCoveragePath));

        trxReader.FindCodeCoverageFiles(RootDirectory).Should().BeEquivalentTo(fullCoveragePath);
        logger.AssertDebugMessageExists(relativeCoveragePath);
    }

    [TestMethod]
    [Description("Tests handling of a trx file that contains a single code coverage attachment with a non-rooted path")]
    public void TrxReader_SingleAttachment_Path2()
    {
        var relativeCoveragePath = "MACHINENAME\\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";
        var resultsDir = CreateDirectory(RootDirectory, "single attachment.trx", TrxContent(relativeCoveragePath));
        // With VSTest task the coverage file name uses underscore instead of spaces.
        var fullCoveragePath = Path.Combine(resultsDir, "single_attachment", "In", relativeCoveragePath);
        CreateFile(Path.GetDirectoryName(fullCoveragePath), Path.GetFileName(fullCoveragePath));

        trxReader.FindCodeCoverageFiles(RootDirectory).Should().BeEquivalentTo(fullCoveragePath);
        logger.AssertDebugMessageExists(relativeCoveragePath);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Tests handling of a trx file that contains a single code coverage attachment with a rooted path")]
    public void TrxReader_SingleAttachment_AbsolutePath()
    {
        var coverageResults = CreateDirectory("x:\\dir1");
        var coverageFileName = CreateFile(coverageResults, "xxx.coverage");
        CreateDirectory(RootDirectory, "single_attachment.trx", TrxContent(coverageFileName));

        trxReader.FindCodeCoverageFiles(RootDirectory).Should().BeEquivalentTo(coverageFileName);
        logger.AssertDebugMessageExists(@"Absolute path to coverage file: x:\dir1\TestResults\xxx.coverage");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Tests handling of a trx file that contain a single code coverage attachment with a path specified by the runDeploymentRoot attribute")]
    public void TrxReader_RunDeploymentRoot_Valid()
    {
        var relativeCoveragePath = @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";
        var resultsDir = CreateDirectory(RootDirectory, "single_attachment.trx", TrxContentWithDeploymentRoot("pathFromDeploymentRoot", relativeCoveragePath));
        var coverageFileName = Path.Combine(resultsDir, "pathFromDeploymentRoot", "In", relativeCoveragePath);
        CreateFile(Path.GetDirectoryName(coverageFileName), Path.GetFileName(coverageFileName));

        trxReader.FindCodeCoverageFiles(RootDirectory).Should().ContainSingle()
            .Which.Should().EndWith(@"TestResults\pathFromDeploymentRoot\In\MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage")
            .And.Be(coverageFileName);
        logger.AssertDebugLogged($@"Absolute path to coverage file: {coverageFileName}");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Tests handling of a trx file that contain a single code coverage attachment with an invalid path specified by the runDeploymentRoot attribute")]
    public void TrxReader_RunDeploymentRoot_Invalid()
    {
        var relativeCoveragePath = @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";
        var resultsDir = CreateDirectory(RootDirectory, "single_attachment.trx", TrxContentWithDeploymentRoot("invalidRoot", relativeCoveragePath));
        var coverageFileName = Path.Combine(resultsDir, "pathFromDeploymentRoot", "In", relativeCoveragePath);
        CreateFile(Path.GetDirectoryName(coverageFileName), Path.GetFileName(coverageFileName));

        trxReader.FindCodeCoverageFiles(RootDirectory).Should().BeEmpty();
        logger.AssertWarningLogged(
            "None of the following coverage attachments could be found: "
            + @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage, "
            + $@"{resultsDir}\single_attachment\In\MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage, "
            + $@"{resultsDir}\single_attachment\In\MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage, "
            + $@"{resultsDir}\invalidRoot\In\MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage. "
            + $@"Trx file: {resultsDir}\single_attachment.trx");
    }

    private string CreateDirectory(string path, string fileName = null, string fileContent = "")
    {
        var subdir = Path.Combine(path, "TestResults");
        directoryMock.Exists(Arg.Is<string>(x => subdir.Equals(x, StringComparison.InvariantCultureIgnoreCase))).Returns(true);
        directoryMock.GetDirectories(Arg.Is<string>(x => path.Equals(x, StringComparison.InvariantCultureIgnoreCase)), "TestResults", Arg.Any<SearchOption>())
            .Returns([subdir]);
        if (fileName is not null)
        {
            CreateFile(subdir, fileName, fileContent);
        }
        return subdir;
    }

    private string CreateFile(string path, string fileName, string fileContent = "")
    {
        var filePath = Path.Combine(path, fileName);
        // File can be checked for existence, making sure the check is case insensitive
        fileMock.Exists(Arg.Is<string>(x => filePath.Equals(x, StringComparison.InvariantCultureIgnoreCase)))
            .Returns(true);
        // File can be opened, making sure the check is case insensitive
        fileMock.Open(Arg.Is<string>(x => filePath.Equals(x, StringComparison.InvariantCultureIgnoreCase)))
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));
        directoryMock.GetFiles(Arg.Is<string>(x => path.Equals(x, StringComparison.InvariantCultureIgnoreCase)), Arg.Any<string>())
            .Returns([filePath]);
        return filePath;
    }

    private static string TrxContent(params string[] attachmentUris) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <TestRun id="eb906034-f363-4bf0-ac6a-29fa47645f67"
            name="LOCAL SERVICE@MACHINENAME 2015-05-06 08:38:39" runUser="NT AUTHORITY\LOCAL SERVICE"
            xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
            <ResultSummary outcome="Completed">
                <Counters total="123" executed="123" passed="123" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" />
                <RunInfos />
                <CollectorDataEntries>
                    {string.Join(Environment.NewLine, attachmentUris.Select(FormatCollectorElement))}
                </CollectorDataEntries>
            </ResultSummary>
        </TestRun>
        """;

    private static string TrxContentWithDeploymentRoot(string deploymentRoot, params string[] attachmentUris) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
            id="eb906034-f363-4bf0-ac6a-29fa47645f67" name="LOCAL SERVICE@MACHINENAME 2015-05-06 08:38:39" runUser="NT AUTHORITY\LOCAL SERVICE">
            <TestSettings name="default" >
              <Deployment runDeploymentRoot="{deploymentRoot}" />
            </TestSettings>
            <ResultSummary outcome="Completed">
                <RunInfos />
                <CollectorDataEntries>
                    {string.Join(Environment.NewLine, attachmentUris.Select(FormatCollectorElement))}
                </CollectorDataEntries>
            </ResultSummary>
        </TestRun>
        """;

    private static string FormatCollectorElement(string uri) =>
        $"""
        <Collector agentName="MACHINENAME" uri="datacollector://microsoft/CodeCoverage/2.0" collectorDisplayName="Code Coverage">
            <UriAttachments>
                <UriAttachment>
                    <A href="{uri}">
                    </A>
                </UriAttachment>
            </UriAttachments>
        </Collector>
        """;

    private void AssertFindCodeCoverageFiles()
    {
        trxReader.FindCodeCoverageFiles(RootDirectory).Should().BeEmpty();
        logger.AssertErrorsLogged(0);
        logger.AssertWarningsLogged(0);
    }
}
