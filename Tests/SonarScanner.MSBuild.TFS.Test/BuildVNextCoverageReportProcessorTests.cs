/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using static SonarScanner.MSBuild.TFS.BuildVNextCoverageReportProcessor;

namespace SonarScanner.MSBuild.TFS.Test;

[TestClass]
[DoNotParallelize]
public class BuildVNextCoverageReportProcessorTests
{
    private readonly AnalysisConfig analysisConfig = new();
    private readonly TestRuntime runtime = new();
    private readonly BuildSettings buildSettings;
    private readonly string testDir;
    private readonly string testResultsDir;
    private readonly string coverageDir;
    private readonly string agentTempDir;
    private readonly EnvironmentVariableScope environmentVariableScope = new();

    private BuildVNextCoverageReportProcessor sut;

    public TestContext TestContext { get; set; }

    public BuildVNextCoverageReportProcessorTests(TestContext testContext)
    {
        testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
        runtime.Directory.Exists(testDir).Returns(true);
        testResultsDir = Path.Combine(testDir, "TestResults");
        runtime.Directory.GetDirectories(testDir, "TestResults", Arg.Any<SearchOption>()).Returns([testResultsDir]);
        coverageDir = Path.Combine(testResultsDir, "TestResults", "In");
        agentTempDir = Path.Combine(testResultsDir, "agentTemp", "In");
        runtime.Directory.Exists(agentTempDir).Returns(true);
        buildSettings = BuildSettings.CreateForTesting(null, true, testDir);
        sut = new BuildVNextCoverageReportProcessor(runtime);
        environmentVariableScope.SetVariable(EnvironmentVariables.AgentTempDirectory, agentTempDir);  // setup search fallback
    }

    [TestCleanup]
    public void Cleanup() =>
        environmentVariableScope.Dispose();

    [TestMethod]
    public void Constructor_Null() =>
        FluentActions.Invoking(() => new BuildVNextCoverageReportProcessor(null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("runtime");

    [TestMethod]
    public void ProcessCoverageReports()
    {
        var result = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        result.VsTestReportsPaths.Should().BeNull();
        result.VsCoverageXmlReportsPaths.Should().BeNull();
        result.CoverageConversionPerformed.Should().BeFalse();
        runtime.Logger.Should().HaveNoErrors().And.HaveInfos(
            "Fetching code coverage report information from TFS...",
            "Attempting to locate a test results (.trx) file...",
            "No test results files found",
            "Looking for binary coverage files to convert to XML format.",
            "Falling back on locating coverage files in the agent temp directory.");
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxFileFound()
    {
        var trxFile = CreateTrxFile();

        var result = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        result.VsTestReportsPaths.Should().ContainSingle().Which.Should().Be(trxFile);
        result.VsCoverageXmlReportsPaths.Should().BeNull();
        result.CoverageConversionPerformed.Should().BeFalse();
        runtime.Logger.Should().HaveNoErrors()
            .And.HaveInfos(
                "Fetching code coverage report information from TFS...",
                "Attempting to locate a test results (.trx) file...",
                $"The following test results files were found: {trxFile}",
                "Looking for binary coverage files to convert to XML format.")
            .And.HaveDebugs("Not using the fallback mechanism to detect binary coverage files.");
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxFileFound_TestReportsPathsGiven()
    {
        var trxFile = CreateTrxFile();
        analysisConfig.LocalSettings = [new Property(SonarProperties.VsTestReportsPaths, "not null")];

        var result = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        result.VsTestReportsPaths.Should().BeNull();
        result.VsCoverageXmlReportsPaths.Should().BeNull();
        result.CoverageConversionPerformed.Should().BeFalse();
        runtime.Logger.Should().HaveNoErrors().And.HaveInfos(
            $"The following test results files were found: {trxFile}",
            "Property 'sonar.cs.vstest.reportsPaths' provided, skipping the search for TRX files in default folders...",
            "Looking for binary coverage files to convert to XML format.",
            "Falling back on locating coverage files in the agent temp directory.");
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    public void ProcessCoverageReports_TrxFileFound_CoverageFileFound_TestReportsPathsGiven()
    {
        var trxFile = CreateTrxFile();
        var coverageFile = CopySampleCoverageFile(coverageDir, "sample.coverage");
        analysisConfig.LocalSettings = [new Property(SonarProperties.VsTestReportsPaths, "not null")];

        var result = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        result.VsTestReportsPaths.Should().BeNull();
        result.VsCoverageXmlReportsPaths.Should().ContainSingle().Which.Should().Be(Path.Combine(coverageDir, "sample.coveragexml"));
        result.CoverageConversionPerformed.Should().BeTrue();
        runtime.Logger.Should().HaveNoErrors()
            .And.HaveInfos(
                $"The following test results files were found: {trxFile}",
                "Property 'sonar.cs.vstest.reportsPaths' provided, skipping the search for TRX files in default folders...",
                "Looking for binary coverage files to convert to XML format.")
            .And.HaveDebugs(
                "Not using the fallback mechanism to detect binary coverage files.",
                $"Converting coverage file '{coverageFile}' to '{Path.Combine(coverageDir, "sample.coveragexml")}'.");
    }

    [TestMethod]
    public void ProcessCoverageReports_CoverageFileExistsWithoutTrxFile()
    {
        CreateFile(coverageDir, "sample.coverage");

        var result = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        result.VsTestReportsPaths.Should().BeNull();
        result.VsCoverageXmlReportsPaths.Should().BeNull();
        result.CoverageConversionPerformed.Should().BeFalse();
        runtime.Logger.Should().HaveNoErrors().And.HaveInfos(
            "No test results files found",
            "Looking for binary coverage files to convert to XML format.",
            "Falling back on locating coverage files in the agent temp directory.");
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    public void ProcessCoverageReports_TrxFileFound_CoverageFileFound()
    {
        var trxFile = CreateTrxFile();
        var coverageFile = CopySampleCoverageFile(coverageDir, "sample.coverage");
        // nonbinary coverage files, like from cobertura, are ignored
        var coberturaTrxFile = CreateTrxFile(fileName: "cobertura.trx", coverageFileName: "notbinary.xml");
        CreateFile(Path.Combine(testResultsDir, "cobertura", "In"), "notbinary.xml");
        runtime.Directory.GetFiles(testResultsDir, "*.trx").Returns([trxFile, coberturaTrxFile]);

        var result = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        result.VsTestReportsPaths.Should().BeEquivalentTo([trxFile, coberturaTrxFile]);
        result.VsCoverageXmlReportsPaths.Should().ContainSingle().Which.Should().Be(Path.Combine(coverageDir, "sample.coveragexml"));
        result.CoverageConversionPerformed.Should().BeTrue();
        runtime.Logger.Should().HaveNoErrors()
            .And.HaveInfos(
                $"The following test results files were found: {trxFile}, {coberturaTrxFile}",
                "Looking for binary coverage files to convert to XML format.")
            .And.HaveDebugs(
                "Not using the fallback mechanism to detect binary coverage files.",
                $"Converting coverage file '{coverageFile}' to '{Path.Combine(coverageDir, "sample.coveragexml")}'.");
    }

    [TestMethod]
    public void ProcessCoverageReports_CoverageXmlReportsPathsGiven()
    {
        analysisConfig.ServerSettings = [new Property(SonarProperties.VsCoverageXmlReportsPaths, "not null")];

        var result = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        result.VsTestReportsPaths.Should().BeNull();
        result.VsCoverageXmlReportsPaths.Should().BeNull();
        result.CoverageConversionPerformed.Should().BeFalse();
        runtime.Logger.Should().HaveNoErrors()
            .And.HaveInfos("No test results files found")
            .And.NotHaveInfo(Resources.CONV_DIAG_LookingForBinaries);
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxFileFound_CoverageXmlReportsPathsGiven()
    {
        var trxFile = CreateTrxFile();
        analysisConfig.LocalSettings = [new Property(SonarProperties.VsCoverageXmlReportsPaths, "not null")];

        var result = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        result.VsTestReportsPaths.Should().ContainSingle().Which.Should().Be(trxFile);
        result.VsCoverageXmlReportsPaths.Should().BeNull();
        result.CoverageConversionPerformed.Should().BeFalse();
        runtime.Logger.Should().HaveNoErrors()
            .And.HaveInfos($"The following test results files were found: {trxFile}")
            .And.NotHaveInfo(Resources.CONV_DIAG_LookingForBinaries);
    }

    [TestMethod]
    public void ProcessCoverageReports_ConvertedFileAlreadyExists()
    {
        var trxFile = CreateTrxFile();
        var coverageFile = CreateFile(coverageDir, "sample.coverage");
        var coverageXmlFile = CreateFile(coverageDir, "sample.coveragexml");

        var result = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        result.VsTestReportsPaths.Should().ContainSingle().Which.Should().Be(trxFile);
        result.VsCoverageXmlReportsPaths.Should().ContainSingle().Which.Should().Be(coverageXmlFile);
        result.CoverageConversionPerformed.Should().BeFalse();
        runtime.Logger.Should().HaveNoErrors()
            .And.HaveInfos($"Found corresponding Binary-to-XML conversion output file for {coverageFile}, no conversion will be attempted.");
    }

    [TestMethod]
    public void ProcessCoverageReports_InvalidConversionFile()
    {
        var trxFile = CreateTrxFile();
        var coverageFile = CreateFile(coverageDir, "sample.coverage");
        Directory.CreateDirectory(coverageDir);
        File.WriteAllText(Path.Combine(coverageDir, "sample.coverage"), "invalid");

        var result = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        result.VsTestReportsPaths.Should().ContainSingle().Which.Should().Be(trxFile);
        result.VsCoverageXmlReportsPaths.Should().BeNull();
        result.CoverageConversionPerformed.Should().BeFalse();
        runtime.Logger.Should().HaveErrors($"""
            Failed to convert the binary code coverage reports to XML. No code coverage information will be uploaded to SonarQube.
            Check that the downloaded code coverage file ({coverageFile}) is valid by opening it in Visual Studio. If it is not, check that the internet security settings on the build machine allow files to be downloaded from the Team Foundation Server machine.
            """);
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    public void ProcessCoverageReports_CoverageFileInAgentTempDir()
    {
        var coverageFile = CopySampleCoverageFile(agentTempDir, "fallback.coverage");
        var copyOfCoverageFile = CopySampleCoverageFile(agentTempDir, "copyOfFallback.coverage");
        runtime.Directory.GetFiles(agentTempDir, "*.coverage", Arg.Any<SearchOption>()).Returns([coverageFile, copyOfCoverageFile]);

        var result = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        result.VsTestReportsPaths.Should().BeNull();
        result.VsCoverageXmlReportsPaths.Should().ContainSingle().Which.Should().Be(Path.Combine(agentTempDir, "fallback.coveragexml"));
        result.CoverageConversionPerformed.Should().BeTrue();
        runtime.Logger.Should().HaveNoErrors().And.HaveInfos(
            "Looking for binary coverage files to convert to XML format.",
            "Falling back on locating coverage files in the agent temp directory.")
            .And.HaveDebugs($"Converting coverage file '{coverageFile}' to '{Path.Combine(agentTempDir, "fallback.coveragexml")}'.");
        runtime.Logger.DebugMessages.Should().ContainInConsecutiveOrder(
            "All matching files: count=2",
            $"\t{coverageFile}",
            $"\t{copyOfCoverageFile}",
            "Unique coverage files: count=1",
            $"\t{coverageFile}");
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    public void ProcessCoverageReports_CoverageFileInAgentTempDir_TrxFileFound_TestReportsPathsGiven()
    {
        var trxFile = CreateTrxFile();
        var coverageFile = CopySampleCoverageFile(agentTempDir, "fallback.coverage");
        var copyOfCoverageFile = CopySampleCoverageFile(agentTempDir, "copyOfFallback.coverage");
        runtime.Directory.GetFiles(agentTempDir, "*.coverage", Arg.Any<SearchOption>()).Returns([coverageFile, copyOfCoverageFile]);
        analysisConfig.LocalSettings = [new Property(SonarProperties.VsTestReportsPaths, "not null")];

        var result = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        result.VsTestReportsPaths.Should().BeNull();
        result.VsCoverageXmlReportsPaths.Should().ContainSingle().Which.Should().Be(Path.Combine(agentTempDir, "fallback.coveragexml"));
        result.CoverageConversionPerformed.Should().BeTrue();
        runtime.Logger.Should().HaveNoErrors().And.HaveInfos(
            $"The following test results files were found: {trxFile}",
            "Property 'sonar.cs.vstest.reportsPaths' provided, skipping the search for TRX files in default folders...",
            "Looking for binary coverage files to convert to XML format.",
            "Falling back on locating coverage files in the agent temp directory.")
            .And.HaveDebugs($"Converting coverage file '{coverageFile}' to '{Path.Combine(agentTempDir, "fallback.coveragexml")}'.");
        runtime.Logger.DebugMessages.Should().ContainInConsecutiveOrder(
            "All matching files: count=2",
            $"\t{coverageFile}",
            $"\t{copyOfCoverageFile}",
            "Unique coverage files: count=1",
            $"\t{coverageFile}");
    }

    [TestMethod]
    public void CheckAgentTempDirectory_CalculatedCorrectly_Null()
    {
        using var envVars = new EnvironmentVariableScope();
        // env var not specified -> null
        envVars.SetVariable(EnvironmentVariables.AgentTempDirectory, null);

        sut.CheckAgentTempDirectory().Should().BeNull();
    }

    [TestMethod]
    public void CheckAgentTempDirectory_CalculatedCorrectly_NonExisting()
    {
        var envDir = Path.Combine(testDir, "DirSpecifiedInEnvDir");
        using var envVars = new EnvironmentVariableScope();
        // Env var set but dir does not exist -> null
        envVars.SetVariable(EnvironmentVariables.AgentTempDirectory, envDir);

        sut.CheckAgentTempDirectory().Should().BeNull();
    }

    [TestMethod]
    public void CheckAgentTempDirectory_CalculatedCorrectly_Existing()
    {
        var envDir = Path.Combine(testDir, "DirSpecifiedInEnvDir");
        using var envVars = new EnvironmentVariableScope();
        // Env var set and dir exists -> dir returned
        runtime.Directory.Exists(envDir).Returns(true);
        envVars.SetVariable(EnvironmentVariables.AgentTempDirectory, envDir);

        sut.CheckAgentTempDirectory().Should().Be(envDir);
    }

    [TestMethod]
    public void FindFallbackCoverageFiles_NoAgentDirectory_Empty()
    {
        using var envVars = new EnvironmentVariableScope();
        envVars.SetVariable(EnvironmentVariables.AgentTempDirectory, null);

        sut.FindFallbackCoverageFiles().Should().BeEmpty();
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestMethod]
    public void FindFallbackCoverageFiles_FilesLocatedCorrectly_Windows_Mac()
    {
        sut = new BuildVNextCoverageReportProcessor(new TestRuntime { Directory = DirectoryWrapper.Instance, File = FileWrapper.Instance }); // no file mocking, test actual search behavior
        var subDir = Path.Combine(agentTempDir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);
        TestUtils.CreateTextFile(agentTempDir, "foo.coverageXXX", "1");              // wrong file extension
        TestUtils.CreateTextFile(agentTempDir, "abc.trx", "2");                      // wrong file extension
        TestUtils.CreateTextFile(agentTempDir, "BAR.coverage.XXX", "3");             // wrong file extension
        var lowerCasePath = TestUtils.CreateTextFile(agentTempDir, "foo.coverage", "4");
        var upperCasePath = TestUtils.CreateTextFile(subDir, "BAR.COVERAGE", "5");

        sut.FindFallbackCoverageFiles().Should().BeEquivalentTo(lowerCasePath, upperCasePath);
    }

    [TestCategory(TestCategories.NoWindows)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void FindFallbackCoverageFiles_FilesLocatedCorrectly_Linux()
    {
        sut = new BuildVNextCoverageReportProcessor(new TestRuntime { Directory = DirectoryWrapper.Instance, File = FileWrapper.Instance }); // no file mocking, test actual search behavior
        var subDir = Path.Combine(agentTempDir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);
        TestUtils.CreateTextFile(agentTempDir, "foo.coverageXXX", "1");             // wrong file extension
        TestUtils.CreateTextFile(agentTempDir, "abc.trx", "2");                     // wrong file extension
        TestUtils.CreateTextFile(agentTempDir, "BAR.coverage.XXX", "3");            // wrong file extension
        var lowerCasePath = TestUtils.CreateTextFile(agentTempDir, "foo.coverage", "4");
        var upperCasePath = TestUtils.CreateTextFile(subDir, "BAR.COVERAGE", "5");
        var duplicate1FilePath = TestUtils.CreateTextFile(agentTempDir, "DUPLICATE.coverage", "6");
        var duplicate2FilePath = TestUtils.CreateTextFile(agentTempDir, "Duplicate.coverage", "7"); // Unix file system is case-sensitive, so these are two separate files

        sut.FindFallbackCoverageFiles().Should().Satisfy(
            x => x == lowerCasePath,    // should also find upperCasePath but does not due to case-sensitivity on Linux
            x => x == duplicate1FilePath,
            x => x == duplicate2FilePath);
    }

    [TestMethod]
    public void FindFallbackCoverageFiles_CalculatesAndDeDupesOnContentCorrectly()
    {
        sut = new BuildVNextCoverageReportProcessor(new TestRuntime { Directory = DirectoryWrapper.Instance, File = FileWrapper.Instance }); // no file mocking, test actual search behavior
        var subDir = Path.Combine(agentTempDir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);
        var file1 = "file1.coverage";
        var file1Duplicate = "file1Duplicate.coverage";
        var filePath1 = TestUtils.CreateTextFile(agentTempDir, file1, "same content");
        var filePath1Duplicate = TestUtils.CreateTextFile(agentTempDir, file1Duplicate, "same content");
        var filePath1SubDir = TestUtils.CreateTextFile(subDir, file1, "same content");

        sut.FindFallbackCoverageFiles().Should().ContainSingle("the 3 files should be de-duped based on content hash.")
            .Which.Should().BeOneOf(filePath1, filePath1Duplicate, filePath1SubDir);
    }

    [TestMethod]
    [DataRow(new byte[] { 1, 2, 3 }, new byte[] { 1, 2 })]
    [DataRow(new byte[] { 1, 2 }, new byte[] { 1, 2, 3 })]
    [DataRow(new byte[] { 1, 2 }, new byte[] { 1, 3 })]
    [DataRow(new byte[] { 1, 2 }, new byte[] { 2, 1 })]
    [DataRow(new byte[] { }, new byte[] { 1 })]
    public void FileWithContentHash_Equals_DifferentHash_False(byte[] hash1, byte[] hash2) =>
        new FileWithContentHash("c:\\path.txt", hash1)
            .Equals(new FileWithContentHash("c:\\path.txt", hash2))
            .Should().BeFalse();

    [TestMethod]
    [DataRow("File.txt", "File.txt")]
    [DataRow("File.txt", "FileOther.txt")]
    [DataRow("FileOther.txt", "File.txt")]
    [DataRow("File.txt", null)]
    [DataRow("File.txt", "")]
    [DataRow("", "File.txt")]
    [DataRow(null, "File.txt")]
    public void FileWithContentHash_Equals_SameHash_True(string fileName1, string fileName2) =>
        new FileWithContentHash(fileName1, [1, 2])
            .Equals(new FileWithContentHash(fileName2, [1, 2]))
            .Should().BeTrue();

    [TestMethod]
    [DataRow("string")]
    [DataRow(42)]
    [DataRow(null)]
    public void FileWithContentHash_Equals_Other_False(object other) =>
        new FileWithContentHash("c:\\path.txt", [1, 2])
            .Equals(other)
            .Should().BeFalse();

    [TestMethod]
    public void ConvertToXml_ConversionFailure_False()
    {
        var context = new ConverterTestContext(TestContext);
        sut.ConvertToXml(context.InputFilePath, context.OutputFilePath).Should().BeFalse();
        File.Exists(context.OutputFilePath).Should().BeFalse();
        runtime.Logger.Should().HaveErrors($"""
            Failed to convert the binary code coverage reports to XML. No code coverage information will be uploaded to SonarQube.
            Check that the downloaded code coverage file ({context.InputFilePath}) is valid by opening it in Visual Studio. If it is not, check that the internet security settings on the build machine allow files to be downloaded from the Team Foundation Server machine.
            """)
            .And.HaveNoWarnings();
    }

    [TestMethod]
    public void ConvertToXml_InputFileIsLocked_False()
    {
        var context = new ConverterTestContext(TestContext);
        using var fs = new FileStream(context.InputFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); // lock the file with FileShare.None
        // FileShare.None will cause nested inner exceptions: AggregateException -> CoverageFileException -> IOException with messages
        // AggregateException: One or more errors occurred.
        // CoverageFileException: Failed to open coverage file "C:\Fullpath\input.txt".
        // IOException: The process cannot access the file 'C:\Fullpath\input.txt' because it is being used by another process.
        sut.ConvertToXml(context.InputFilePath, context.OutputFilePath).Should().BeFalse();
        runtime.Logger.Should().HaveErrors($"""
            Failed to convert the binary code coverage reports to XML. No code coverage information will be uploaded to SonarQube.
            Check that the downloaded code coverage file ({context.InputFilePath}) is valid by opening it in Visual Studio. If it is not, check that the internet security settings on the build machine allow files to be downloaded from the Team Foundation Server machine.
            """);
        File.Exists(context.OutputFilePath).Should().BeFalse();
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    public void ConvertToXml_ConvertsSampleFile()
    {
        var inputFilePath = Path.Combine(TestContext.DeploymentDirectory, "Sample.coverage");
        var outputFilePath = Path.Combine(TestContext.DeploymentDirectory, $"{nameof(ConvertToXml_ConvertsSampleFile)}.xmlcoverage");
        var expectedOutputFilePath = Path.Combine(TestContext.DeploymentDirectory, "Expected.xmlcoverage");

        File.Exists(inputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeFalse();
        File.Exists(expectedOutputFilePath).Should().BeTrue();
        sut.ConvertToXml(inputFilePath, outputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeTrue();
        // All tags and attributes must appear in actual and expected. Comments, whitespace, ordering, and the like is ignored in the assertion.
        XDocument.Load(outputFilePath).Should().BeEquivalentTo(XDocument.Load(expectedOutputFilePath));
        runtime.Logger.Should().HaveDebugs($"Converting coverage file '{inputFilePath}' to '{outputFilePath}'.");
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    public void ConvertToXml_ProblematicCulture_ConvertsSampleFile()
    {
        var inputFilePath = Path.Combine(TestContext.DeploymentDirectory, "Sample.coverage");
        var outputFilePath = Path.Combine(TestContext.DeploymentDirectory, $"{nameof(ConvertToXml_ProblematicCulture_ConvertsSampleFile)}.xmlcoverage");
        var expectedOutputFilePath = Path.Combine(TestContext.DeploymentDirectory, "Expected.xmlcoverage");

        File.Exists(inputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeFalse();
        File.Exists(expectedOutputFilePath).Should().BeTrue();
        using var scope = new ApplicationCultureInfo(CultureInfo.GetCultureInfo("de-DE")); // Serializes block_coverage="33.33" as block_coverage="33,33"
        sut.ConvertToXml(inputFilePath, outputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeTrue();
        // All tags and attributes must appear in actual and expected. Comments, whitespace, ordering, and the like is ignored in the assertion.
        XDocument.Load(outputFilePath).Should().BeEquivalentTo(XDocument.Load(expectedOutputFilePath));
    }

    private string CreateFile(string path, string fileName, string fileContent = "")
    {
        var filePath = Path.Combine(path, fileName);
        runtime.File.Exists(filePath).Returns(true);
        runtime.File.Open(filePath).Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));
        return filePath;
    }

    private string CreateTrxFile(string fileName = "TestResults.trx", string coverageFileName = "sample.coverage")
    {
        var filePath = CreateFile(testResultsDir, fileName, $"""
            <?xml version="1.0" encoding="utf-8" ?>
            <x:TestRun id="4e4e4073-b17c-4bd0-a8bc-051bbc5a63e4" name="John@JOHN-DOE 2019-05-22 14:26:54:768" runUser="JOHN-DO\John" xmlns:x="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                <x:ResultSummary outcome="Completed">
                <x:CollectorDataEntries>
                    <x:Collector uri="datacollector://microsoft/CodeCoverage/2.0">
                        <x:UriAttachments>
                            <x:UriAttachment>
                                <x:A href="{coverageFileName}">{coverageFileName}</x:A>
                            </x:UriAttachment>
                        </x:UriAttachments>
                    </x:Collector>
                </x:CollectorDataEntries>
                </x:ResultSummary>
            </x:TestRun>
            """);
        runtime.Directory.GetFiles(testResultsDir, "*.trx").Returns([filePath]);
        return filePath;
    }

    private string CopySampleCoverageFile(string path, string fileName)
    {
        var filePath = CreateFile(path, fileName);
        Directory.CreateDirectory(path);
        File.Copy(Path.Combine(TestContext.DeploymentDirectory, "Sample.coverage"), filePath, overwrite: true);
        return filePath;
    }

    private class ConverterTestContext
    {
        public string InputFilePath { get; }
        public string OutputFilePath { get; }

        public ConverterTestContext(TestContext testContext, string fileContent = "dummy input file", [CallerMemberName] string testMethodName = null)
        {
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
            InputFilePath = Path.Combine(testDir, $"input_{testMethodName}.txt");
            OutputFilePath = Path.Combine(testDir, "output.txt");
            if (fileContent is not null)
            {
                File.WriteAllText(InputFilePath, fileContent);
            }
        }
    }
}
