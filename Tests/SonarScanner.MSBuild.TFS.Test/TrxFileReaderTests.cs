/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using TestUtilities;

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
    private string RootDirectory =>
        TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

    [TestInitialize]
    public void TestInitialize()
    {
        fileMock = Substitute.For<IFileWrapper>();
        // Any file does not exist unless it is setup afterwards
        fileMock.Exists(Arg.Any<string>()).Returns(false);

        directoryMock = Substitute.For<IDirectoryWrapper>();
        // Any directory does not exist unless it is setup afterwards
        directoryMock.Exists(Arg.Any<string>()).Returns(false);
        // RootDirectory exists
        directoryMock.Exists(RootDirectory).Returns(true);

        logger = new TestLogger();
        trxReader = new TrxFileReader(logger, fileMock, directoryMock);
    }

    [TestMethod]
    public void TrxReader_TestsResultsDirectoryMissing()
    {
        // Arrange
        // No subdirectories, we call CreateDirectories to setup Directory.GetDirectories(RootDirectory)
        // to return empty array and avoid throwing.
        CreateDirectories(RootDirectory);

        // Act
        var coverageFilePaths = trxReader.FindCodeCoverageFiles(RootDirectory);

        // Assert
        coverageFilePaths.Should().BeEmpty();

        // Not expecting errors or warnings: we assume it means that tests have not been executed
        logger.AssertErrorsLogged(0);
        logger.AssertWarningsLogged(0);
    }

    [TestMethod]
    public void TrxReader_InvalidTrxFile()
    {
        // Arrange
        var testResults = CreateDirectories(RootDirectory, "TestResults")[0];
        CreateFiles(testResults, ("dummy.trx", "this is not a trx file"));

        // Act
        var coverageFilePaths = trxReader.FindCodeCoverageFiles(RootDirectory);

        // Assert
        coverageFilePaths.Should().BeEmpty();

        logger.AssertSingleInfoMessageExists("No code coverage attachments were found from the trx files.");
        logger.Warnings.Should().HaveCount(1);
        logger.Warnings[0].Should().Match(@"Located trx file is not a valid xml file. File: *\TestResults\dummy.trx. File load error: Data at the root level is invalid. Line 1, position 1."); // expecting a warning about the invalid file
        logger.AssertErrorsLogged(0); // should be a warning, not an error
    }

    [TestMethod]
    public void TrxReader_MultipleTrxFiles()
    {
        // Arrange
        var testResults = CreateDirectories(RootDirectory, "TestResults")[0];
        var trx1 = CreateFiles(testResults,
            ("mytrx1.trx", "<TestRun />"),
            ("mytrx2.trx", "<TestRun />"));

        // Act
        var coverageFilePaths = trxReader.FindCodeCoverageFiles(RootDirectory);

        // Assert
        coverageFilePaths.Should().BeEmpty();

        logger.DebugMessages.Should().HaveCount(0);
        logger.AssertSingleInfoMessageExists("No code coverage attachments were found from the trx files.");
        logger.AssertWarningsLogged(0);
        logger.AssertErrorsLogged(0);
    }

    [TestMethod]
    public void TrxReader_SingleTrxFileInSubfolder()
    {
        // Arrange
        var testResults = CreateDirectories(RootDirectory, "Dummy\\TestResults")[0];
        CreateFiles(testResults, ("no_attachments.trx", GetTrxContent()));

        // Act
        var coverageFilePaths = trxReader.FindCodeCoverageFiles(RootDirectory);

        // Assert
        coverageFilePaths.Should().BeEmpty();

        // Not finding attachment info in the file shouldn't cause a warning/error
        logger.AssertErrorsLogged(0);
        logger.AssertWarningsLogged(0);
        logger.AssertSingleInfoMessageExists("No code coverage attachments were found from the trx files.");
    }

    [TestMethod]
    public void TrxReader_TrxWithNoAttachments()
    {
        // Arrange
        var resultsDir = CreateDirectories(RootDirectory, "TestResults")[0];
        CreateFiles(resultsDir, ("no_attachments.trx", GetTrxContent()));

        // Act
        var coverageFilePaths = trxReader.FindCodeCoverageFiles(RootDirectory);

        // Assert
        coverageFilePaths.Should().BeEmpty();

        // Not finding attachment info in the file shouldn't cause a warning/error
        logger.AssertErrorsLogged(0);
        logger.AssertWarningsLogged(0);
        logger.AssertSingleInfoMessageExists("No code coverage attachments were found from the trx files.");
    }

    [TestMethod]
    [Description("Tests handling of a trx file that contains information about multiple code coverage runs (i.e. an error case, as we're not expecting this)")]
    public void TrxReader_TrxWithMultipleAttachments()
    {
        // Arrange
        var resultsDir = CreateDirectories(RootDirectory, "TestResults")[0];
        CreateFiles(resultsDir, ("multiple_attachments.trx", GetTrxContent("MACHINENAME\\AAA.coverage", "XXX.coverage")));

        // Act
        var coverageFilePaths = trxReader.FindCodeCoverageFiles(RootDirectory);

        // Assert
        coverageFilePaths.Should().BeEmpty();

        logger.Warnings.Should().HaveCount(2);
        logger.Warnings[0].Should().Match(@"None of the following coverage attachments could be found: MACHINENAME\AAA.coverage, *\TestResults\multiple_attachments\In\MACHINENAME\AAA.coverage, *\TestResults\multiple_attachments\In\MACHINENAME\AAA.coverage. Trx file: *\TestResults\multiple_attachments.trx");
        logger.Warnings[1].Should().Match(@"None of the following coverage attachments could be found: XXX.coverage, *\TestResults\multiple_attachments\In\XXX.coverage, *\TestResults\multiple_attachments\In\XXX.coverage. Trx file: *\TestResults\multiple_attachments.trx");
        logger.AssertErrorsLogged(0);
    }

    [TestMethod]
    [Description("Tests handling of a trx file that contains a single code coverage attachment with a non existing file")]
    public void TrxReader_SingleAttachment_PathDoesNotExist()
    {
        // Arrange
        var resultsDir = CreateDirectories(RootDirectory, "TestResults")[0];
        var coverageFileName = "MACHINENAME\\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";

        CreateFiles(resultsDir, ("single_attachment.trx", GetTrxContent(coverageFileName)));

        // Act
        var coverageFilePaths = trxReader.FindCodeCoverageFiles(RootDirectory);

        // Assert
        coverageFilePaths.Should().BeEmpty();

        logger.Warnings.Should().HaveCount(1);
        logger.Warnings[0].Should().Match(@"None of the following coverage attachments could be found: MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage, *\TestResults\single_attachment\In\MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage, *\TestResults\single_attachment\In\MACHINENAME\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage. Trx file: *\TestResults\single_attachment.trx");
    }

    [TestMethod]
    [Description("Tests handling of a trx file that contains a single code coverage attachment with a non-rooted path")]
    public void TrxReader_SingleAttachment_Path1()
    {
        // Arrange
        var resultsDir = CreateDirectories(RootDirectory, "TestResults")[0];

        var relativeCoveragePath = "MACHINENAME\\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";
        var fullCoveragePath = Path.Combine(resultsDir, "single attachment", "In", relativeCoveragePath);
        CreateFiles(Path.GetDirectoryName(fullCoveragePath), (Path.GetFileName(fullCoveragePath), string.Empty));

        CreateFiles(resultsDir, ("single attachment.trx", GetTrxContent(relativeCoveragePath)));

        // Act
        var coverageFilePaths = trxReader.FindCodeCoverageFiles(RootDirectory);

        // Assert
        coverageFilePaths.Should().BeEquivalentTo(fullCoveragePath);

        logger.AssertDebugMessageExists(relativeCoveragePath);
    }

    [TestMethod]
    [Description("Tests handling of a trx file that contains a single code coverage attachment with a non-rooted path")]
    public void TrxReader_SingleAttachment_Path2()
    {
        // Arrange
        var resultsDir = CreateDirectories(RootDirectory, "TestResults")[0];
        var relativeCoveragePath = "MACHINENAME\\LOCAL SERVICE_MACHINENAME 2015-05-06 08_38_35.coverage";
        // With VSTest task the coverage file name uses underscore instead of spaces.
        var fullCoveragePath = Path.Combine(resultsDir, "single_attachment", "In", relativeCoveragePath);
        CreateFiles(Path.GetDirectoryName(fullCoveragePath), (Path.GetFileName(fullCoveragePath), string.Empty));

        CreateFiles(resultsDir, ("single attachment.trx", GetTrxContent(relativeCoveragePath)));

        // Act
        var coverageFilePaths = trxReader.FindCodeCoverageFiles(RootDirectory);

        // Assert
        coverageFilePaths.Should().BeEquivalentTo(fullCoveragePath);

        logger.AssertDebugMessageExists(relativeCoveragePath);
    }

    [TestMethod]
    [Description("Tests handling of a trx file that contains a single code coverage attachment with a rooted path")]
    public void TrxReader_SingleAttachment_AbsolutePath()
    {
        // Arrange
        var coverageResults = CreateDirectories("x:\\dir1", "dir2")[0];
        var coverageFileName = CreateFiles(coverageResults, ("xxx.coverage", string.Empty))[0];

        var testResults = CreateDirectories(RootDirectory, "TestResults")[0];
        CreateFiles(testResults, ("single_attachment.trx", GetTrxContent(coverageFileName)));

        // Act
        var coverageFilePaths = trxReader.FindCodeCoverageFiles(RootDirectory);

        // Assert
        coverageFilePaths.Should().BeEquivalentTo(coverageFileName);
        logger.AssertDebugMessageExists(@"Absolute path to coverage file: x:\dir1\dir2\xxx.coverage");
    }

    private string[] CreateDirectories(string path, params string[] names)
    {
        var subdirs = names.Select(name => Path.Combine(path, name)).ToArray();

        // Directories can be checked for existence, making sure the check is case insensitive
        Array.ForEach(subdirs, x =>
            directoryMock
                .Exists(Arg.Is<string>(s => x.Equals(s, StringComparison.InvariantCultureIgnoreCase)))
                .Returns(true));

        directoryMock
            .GetDirectories(Arg.Is<string>(x => path.Equals(x, StringComparison.InvariantCultureIgnoreCase)), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(subdirs);

        return subdirs;
    }

    private string[] CreateFiles(string path, params (string Name, string Content)[] files)
    {
        var filePaths = files.Select(f => Path.Combine(path, f.Name)).ToArray();

        for (var i = 0; i < files.Length; i++)
        {
            var filePath = filePaths[i];
            var fileContent = files[i].Content;
            // File can be checked for existence, making sure the check is case insensitive
            fileMock
                .Exists(Arg.Is<string>(x => filePath.Equals(x, StringComparison.InvariantCultureIgnoreCase)))
                .Returns(true);
            // File can be opened, making sure the check is case insensitive
            fileMock
                .Open(Arg.Is<string>(x => filePath.Equals(x, StringComparison.InvariantCultureIgnoreCase)))
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));
        }

        directoryMock
            .GetFiles(Arg.Is<string>(x => path.Equals(x, StringComparison.InvariantCultureIgnoreCase)), Arg.Any<string>())
            .Returns(filePaths);

        return filePaths;
    }

    private static string GetTrxContent(params string[] attachmentUris)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestRun id=""eb906034-f363-4bf0-ac6a-29fa47645f67""
    name=""LOCAL SERVICE@MACHINENAME 2015-05-06 08:38:39"" runUser=""NT AUTHORITY\LOCAL SERVICE""
    xmlns=""http://microsoft.com/schemas/VisualStudio/TeamTest/2010"">
  <ResultSummary outcome=""Completed"">
    <Counters total=""123"" executed=""123"" passed=""123"" failed=""0"" error=""0"" timeout=""0"" aborted=""0"" inconclusive=""0"" passedButRunAborted=""0"" notRunnable=""0"" notExecuted=""0"" disconnected=""0"" warning=""0"" completed=""0"" inProgress=""0"" pending=""0"" />
    <RunInfos />
    <CollectorDataEntries>
      {string.Join(Environment.NewLine, attachmentUris.Select(FormatCollectorElement))}
    </CollectorDataEntries>
  </ResultSummary>
</TestRun>";

        string FormatCollectorElement(string uri) =>
            $@"<Collector agentName=""MACHINENAME"" uri=""datacollector://microsoft/CodeCoverage/2.0"" collectorDisplayName=""Code Coverage"">
        <UriAttachments>
          <UriAttachment>
            <A href=""{uri}"">
            </A>
          </UriAttachment>
        </UriAttachments>
      </Collector>";
    }
}
