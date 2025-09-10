﻿/*
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

namespace SonarScanner.MSBuild.TFS.Test;

[TestClass]
public class TrxFileReaderTests
{
    private readonly TestRuntime runtime = new();
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
        runtime.File.Exists(Arg.Any<string>()).Returns(false);

        runtime.Directory.Exists(Arg.Any<string>()).Returns(false);
        runtime.Directory.Exists(RootDirectory).Returns(true);

        trxReader = new TrxFileReader(runtime);
    }

    [TestMethod]
    public void Constructor_RuntimeNull_Throws() =>
        FluentActions.Invoking(() => new TrxFileReader(null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("runtime");

    [TestMethod]
    public void TrxReader_TestsResultsDirectoryMissing() =>
        // With no call to CreateDirectories, Directory.GetDirectories(RootDirectory) will return an empty array.
        AssertFindCodeCoverageFiles();

    [TestMethod]
    public void TrxReader_InvalidTrxFile()
    {
        CreateDirectory(RootDirectory, "dummy.trx", "this is not a trx file");
        var trxFiles = trxReader.FindTrxFiles(RootDirectory);
        trxReader.FindCodeCoverageFiles(trxFiles).Should().BeEmpty();
        runtime.Logger.Should().HaveInfoOnce("No code coverage attachments were found from the trx files.");
        runtime.Logger.Warnings.Should().ContainSingle().Which.Should()
            .Match($"Located trx file is not a valid xml file. File: *{Path.Combine("TestResults", "dummy.trx")}. File load error: Data at the root level is invalid. Line 1, position 1.");
        runtime.Logger.Should().HaveNoErrors(); // should be a warning, not an error
    }

    [TestMethod]
    public void TrxReader_MultipleTrxFiles()
    {
        var testResults = CreateDirectory(RootDirectory);
        var file1 = CreateFile(testResults, "mytrx1.trx", "<TestRun />");
        var file2 = CreateFile(testResults, "mytrx2.trx", "<TestRun />");
        runtime.Directory.GetFiles(Arg.Is<string>(x => testResults.Equals(x, StringComparison.InvariantCultureIgnoreCase)), Arg.Any<string>())
            .Returns([file1, file2]);

        AssertFindCodeCoverageFiles();
        runtime.Logger.DebugMessages.Should().BeEmpty();
        runtime.Logger.Should().HaveInfoOnce("No code coverage attachments were found from the trx files.");
    }

    [TestMethod]
    public void TrxReader_SingleTrxFileInSubfolder()
    {
        var testResults = CreateDirectory(Path.Combine(RootDirectory, "Dummy"), "no_attachments.trx", TrxContent());
        runtime.Directory
            .GetDirectories(Arg.Is<string>(x => RootDirectory.Equals(x, StringComparison.InvariantCultureIgnoreCase)), "TestResults", SearchOption.AllDirectories)
            .Returns([testResults]);

        AssertFindCodeCoverageFiles();
        runtime.Logger.Should().HaveInfoOnce("No code coverage attachments were found from the trx files.");
    }

    [TestMethod]
    public void TrxReader_TrxWithNoAttachments()
    {
        CreateDirectory(RootDirectory, "no_attachments.trx", TrxContent());
        AssertFindCodeCoverageFiles();
        runtime.Logger.Should().HaveInfoOnce("No code coverage attachments were found from the trx files.");
    }

    [TestMethod]
    [Description("Tests handling of a trx file that contains information about multiple code coverage runs (i.e. an error case, as we're not expecting this)")]
    public void TrxReader_TrxWithMultipleAttachments()
    {
        CreateDirectory(RootDirectory, "multiple_attachments.trx", TrxContent("MACHINENAME\\AAA.coverage", "XXX.coverage"));
        var trxFiles = trxReader.FindTrxFiles(RootDirectory);
        trxReader.FindCodeCoverageFiles(trxFiles).Should().BeEmpty();
        runtime.Logger.Warnings.OrderBy(x => x).Should().SatisfyRespectively(
            x => x.Should().Match(
                    "None of the following coverage attachments could be found: "
                    + @"MACHINENAME\AAA.coverage, "
                    + $"*{Path.Combine("TestResults", "multiple_attachments", "In", @"MACHINENAME\AAA.coverage")}, "
                    + $"*{Path.Combine("TestResults", "multiple_attachments", "In", @"MACHINENAME\AAA.coverage")}. "
                    + $"Trx file: *{Path.Combine("TestResults", "multiple_attachments.trx")}"),
            x => x.Should().Match(
                    "None of the following coverage attachments could be found: "
                    + "XXX.coverage, "
                    + $"*{Path.Combine("TestResults", "multiple_attachments", "In", "XXX.coverage")}, "
                    + $"*{Path.Combine("TestResults", "multiple_attachments", "In", "XXX.coverage")}. "
                    + $"Trx file: *{Path.Combine("TestResults", "multiple_attachments.trx")}"));
        runtime.Logger.Should().HaveNoErrors();
    }

    [TestMethod]
    [Description("Tests handling of a trx file that contains a single code coverage attachment with a non existing file")]
    public void TrxReader_SingleAttachment_PathDoesNotExist()
    {
        CreateDirectory(RootDirectory, "single_attachment.trx", TrxContent("MACHINENAME\\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage"));
        var trxFiles = trxReader.FindTrxFiles(RootDirectory);
        trxReader.FindCodeCoverageFiles(trxFiles).Should().BeEmpty();
        runtime.Logger.Warnings.Should().ContainSingle().Which.Should().Match(
            "None of the following coverage attachments could be found: "
            + @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage, "
            + $"*{Path.Combine("TestResults", "single_attachment", "In", @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage")}, "
            + $"*{Path.Combine("TestResults", "single_attachment", "In", @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage")}. "
            + $"Trx file: *{Path.Combine("TestResults", "single_attachment.trx")}");
    }

    [TestMethod]
    [Description("Tests handling of a trx file that contains a single code coverage attachment with a non-rooted path")]
    public void TrxReader_SingleAttachment_Path1()
    {
        var relativeCoveragePath = "MACHINENAME\\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";
        var resultsDir = CreateDirectory(RootDirectory, "single attachment.trx", TrxContent(relativeCoveragePath));
        var fullCoveragePath = Path.Combine(resultsDir, "single attachment", "In", relativeCoveragePath);
        CreateFile(Path.GetDirectoryName(fullCoveragePath), Path.GetFileName(fullCoveragePath));

        var trxFiles = trxReader.FindTrxFiles(RootDirectory);
        trxReader.FindCodeCoverageFiles(trxFiles).Should().BeEquivalentTo(fullCoveragePath);
        runtime.Logger.Should().HaveDebugs("Absolute path to coverage file: " + fullCoveragePath);
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

        var trxFiles = trxReader.FindTrxFiles(RootDirectory);
        trxReader.FindCodeCoverageFiles(trxFiles).Should().BeEquivalentTo(fullCoveragePath);
        runtime.Logger.Should().HaveDebugs("Absolute path to coverage file: " + fullCoveragePath);
    }

    [TestMethod]
    [Description("Tests handling of a trx file that contains a single code coverage attachment with a rooted path")]
    public void TrxReader_SingleAttachment_AbsolutePath()
    {
        var coverageResults = CreateDirectory(@"x:\dir1");
        var coverageFileName = CreateFile(coverageResults, "xxx.coverage");
        CreateDirectory(RootDirectory, "single_attachment.trx", TrxContent(coverageFileName));

        var trxFiles = trxReader.FindTrxFiles(RootDirectory);
        trxReader.FindCodeCoverageFiles(trxFiles).Should().BeEquivalentTo(coverageFileName);
        runtime.Logger.Should().HaveDebugs($"Absolute path to coverage file: {Path.Combine(@"x:\dir1", "TestResults", "xxx.coverage")}");
    }

    [TestMethod]
    [Description("Tests handling of a trx file that contain a single code coverage attachment with a path specified by the runDeploymentRoot attribute")]
    public void TrxReader_RunDeploymentRoot_Valid()
    {
        var relativeCoveragePath = @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";
        var resultsDir = CreateDirectory(RootDirectory, "single_attachment.trx", TrxContentWithDeploymentRoot("pathFromDeploymentRoot", relativeCoveragePath));
        var coverageFileName = Path.Combine(resultsDir, "pathFromDeploymentRoot", "In", relativeCoveragePath);
        CreateFile(Path.GetDirectoryName(coverageFileName), Path.GetFileName(coverageFileName));

        var trxFiles = trxReader.FindTrxFiles(RootDirectory);
        trxReader.FindCodeCoverageFiles(trxFiles).Should().ContainSingle()
            .Which.Should().EndWith($"{Path.Combine("TestResults", "pathFromDeploymentRoot", "In", @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage")}")
            .And.Be(coverageFileName);
        runtime.Logger.Should().HaveDebugs($"Absolute path to coverage file: {coverageFileName}");
    }

    [TestMethod]
    [Description("Tests handling of a trx file that contain a single code coverage attachment with an invalid path specified by the runDeploymentRoot attribute")]
    public void TrxReader_RunDeploymentRoot_Invalid()
    {
        var relativeCoveragePath = @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";
        var resultsDir = CreateDirectory(RootDirectory, "single_attachment.trx", TrxContentWithDeploymentRoot("invalidRoot", relativeCoveragePath));
        var coverageFileName = Path.Combine(resultsDir, "pathFromDeploymentRoot", "In", relativeCoveragePath);
        CreateFile(Path.GetDirectoryName(coverageFileName), Path.GetFileName(coverageFileName));

        var trxFiles = trxReader.FindTrxFiles(RootDirectory);
        trxReader.FindCodeCoverageFiles(trxFiles).Should().BeEmpty();
        runtime.Logger.Should().HaveWarnings(
            "None of the following coverage attachments could be found: "
            + @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage, "
            + $"{Path.Combine(resultsDir, "single_attachment", "In", @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage")}, "
            + $"{Path.Combine(resultsDir, "single_attachment", "In", @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage")}, "
            + $"{Path.Combine(resultsDir, "invalidRoot", "In", @"MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage")}. "
            + $"Trx file: {Path.Combine(resultsDir, "single_attachment.trx")}");
    }

    private string CreateDirectory(string path, string fileName = null, string fileContent = "")
    {
        var subdir = Path.Combine(path, "TestResults");
        runtime.Directory.Exists(Arg.Is<string>(x => subdir.Equals(x, StringComparison.InvariantCultureIgnoreCase))).Returns(true);
        runtime.Directory.GetDirectories(Arg.Is<string>(x => path.Equals(x, StringComparison.InvariantCultureIgnoreCase)), "TestResults", Arg.Any<SearchOption>())
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
        runtime.File.Exists(Arg.Is<string>(x => filePath.Equals(x, StringComparison.InvariantCultureIgnoreCase)))
            .Returns(true);
        // File can be opened, making sure the check is case insensitive
        runtime.File.Open(Arg.Is<string>(x => filePath.Equals(x, StringComparison.InvariantCultureIgnoreCase)))
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));
        runtime.Directory.GetFiles(Arg.Is<string>(x => path.Equals(x, StringComparison.InvariantCultureIgnoreCase)), Arg.Any<string>())
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
        var trxFiles = trxReader.FindTrxFiles(RootDirectory);
        trxReader.FindCodeCoverageFiles(trxFiles).Should().BeEmpty();
        runtime.Logger.Should().HaveNoErrors()
            .And.HaveNoWarnings();
    }
}
