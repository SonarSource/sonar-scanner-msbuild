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
public class BuildVNextCoverageReportProcessorTests
{
    public enum Properties
    {
        TestAndCoverageXmlReportsPathsNull,
        TestReportsPathsNotNull,
        CoverageXmlReportsPathsNotNull,
        TestAndCoverageXmlReportsPathsNotNull
    }

    private readonly AnalysisConfig analysisConfig = new();
    private readonly TestRuntime runtime = new();
    private readonly ICoverageReportConverter converter = Substitute.For<ICoverageReportConverter>();
    private readonly BuildSettings buildSettings;
    private readonly string testDir;
    private readonly string testResultsDir;
    private readonly string coverageDir;
    private readonly string alternateCoverageDir;
    private readonly EnvironmentVariableScope environmentVariableScope = new();

    private BuildVNextCoverageReportProcessor sut;

    public TestContext TestContext { get; set; }

    public BuildVNextCoverageReportProcessorTests(TestContext testContext)
    {
        testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
        runtime.Directory.Exists(testDir).Returns(true);
        testResultsDir = Path.Combine(testDir, "TestResults");
        runtime.Directory.GetDirectories(testDir, "TestResults", Arg.Any<SearchOption>()).Returns([testResultsDir]);
        coverageDir = Path.Combine(testResultsDir, "dummy", "In");
        alternateCoverageDir = Path.Combine(testResultsDir, "alternate", "In");
        runtime.Directory.Exists(alternateCoverageDir).Returns(true);
        buildSettings = BuildSettings.CreateForTesting(null, true, testDir);
        sut = new BuildVNextCoverageReportProcessor(converter, runtime);
        environmentVariableScope.SetVariable(EnvironmentVariables.AgentTempDirectory, alternateCoverageDir);  // setup search fallback
    }

    [TestCleanup]
    public void Cleanup() =>
        environmentVariableScope.Dispose();

    [TestMethod]
    public void Constructor_ConverterIsNull_ThrowsNullArgumentException() =>
        FluentActions.Invoking(() => new BuildVNextCoverageReportProcessor(null, runtime)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("converter");

    [TestMethod]
    public void Constructor_LoggerIsNull_ThrowsNullArgumentException() =>
        FluentActions.Invoking(() => new BuildVNextCoverageReportProcessor(converter, null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("runtime");

    [TestMethod]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxFileFound_WritesPropertiesFile(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true);

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        AssertUsesFallback(false);
        runtime.Logger.Warnings.Should().ContainSingle().Which.StartsWith("None of the following coverage attachments could be found: dummy.coverage");
        AssertPropertiesFileContainsTestReportsPaths(additionalProperties);
        AssertPropertiesFileContainsCoverageXmlReportsPaths(additionalProperties, false);
    }

    [TestMethod]
    [DataRow(Properties.TestReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxFileFound_TestReportsPathsProvided_DoesNotWritePropertiesFile(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings);
        runtime.Logger.Warnings.Should().ContainSingle().Which.StartsWith("None of the following coverage attachments could be found: dummy.coverage");
        runtime.File.DidNotReceiveWithAnyArgs().AppendAllText(null, null);
    }

    [TestMethod]
    [CombinatorialData]
    public void ProcessCoverageReports_NoTrxFilesFound_DoesNotWritePropertiesFile(Properties properties)
    {
        SetupPropertiesAndFiles(properties);

        sut.ProcessCoverageReports(analysisConfig, buildSettings);
        AssertUsesFallback();
        runtime.Logger.Should().HaveNoWarnings();
        runtime.File.DidNotReceiveWithAnyArgs().AppendAllText(null, null);
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_Converts()
    {
        SetupPropertiesAndFiles(Properties.TestAndCoverageXmlReportsPathsNull, trx: true, coverage: true);
        CopySampleCoverageFile(coverageDir, "dummy.coverage");

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        runtime.Logger.Should().HaveNoWarnings();
        AssertPropertiesFileContainsTestReportsPaths(additionalProperties);
        AssertPropertiesFileContainsCoverageXmlReportsPaths(additionalProperties);
        additionalProperties.CoverageConversionPerformed.Should().BeTrue();
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_TestReportsPathsProvided_Converts_DoesNotWriteTestReportsPathsToPropertiesFile()
    {
        SetupPropertiesAndFiles(Properties.TestReportsPathsNotNull, trx: true, coverage: true);
        CopySampleCoverageFile(coverageDir, "dummy.coverage");

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        runtime.Logger.Should().HaveNoWarnings();
        AssertPropertiesFileContainsCoverageXmlReportsPaths(additionalProperties);
        AssertPropertiesFileContainsTestReportsPaths(additionalProperties, false);
        additionalProperties.CoverageConversionPerformed.Should().BeTrue();
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_CoverageXmlReportsPathsProvided_Converts_DoesNotWriteCoverageXmlReportsPathsToPropertiesFile()
    {
        SetupPropertiesAndFiles(Properties.CoverageXmlReportsPathsNotNull, trx: true, coverage: true);
        CopySampleCoverageFile(coverageDir, "dummy.coverage");

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        runtime.Logger.Should().HaveNoWarnings();
        AssertPropertiesFileContainsTestReportsPaths(additionalProperties);
        AssertPropertiesFileContainsCoverageXmlReportsPaths(additionalProperties, false);
        additionalProperties.CoverageConversionPerformed.Should().BeTrue();
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_TestReportsAndCoverageXmlPathsProvided_Converts_DoesNotWritePropertiesFile()
    {
        SetupPropertiesAndFiles(Properties.TestAndCoverageXmlReportsPathsNotNull, trx: true, coverage: true);
        CopySampleCoverageFile(coverageDir, "dummy.coverage");

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        runtime.Logger.Should().HaveNoWarnings();
        runtime.File.DidNotReceiveWithAnyArgs().AppendAllText(null, null);
        additionalProperties.CoverageConversionPerformed.Should().BeTrue();
    }

    [TestMethod]
    [CombinatorialData]
    public void ProcessCoverageReports_NoTrxFilesFound_CoverageFileFound_DoesNotConvert(Properties properties)
    {
        SetupPropertiesAndFiles(properties, coverage: true);

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        runtime.Logger.Should().HaveNoWarnings();
        runtime.File.DidNotReceiveWithAnyArgs().AppendAllText(null, null);
        additionalProperties.CoverageConversionPerformed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_CoverageXmlFileAlreadyPresent_DoesNotConvert_WritesCoverageXmlReportsPathsToPropertiesFile(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true, coverage: true, coverageXml: true);

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        runtime.Logger.Should().HaveNoWarnings();
        AssertPropertiesFileContainsCoverageXmlReportsPaths(additionalProperties);
        additionalProperties.CoverageConversionPerformed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_CoverageXmlFileAlreadyPresent_CoverageXmlReportsPathsProvided_DoesNotConvert_DoesNotWriteCoverageXmlReportsPathsToPropertiesFile(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true, coverage: true, coverageXml: true);

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        runtime.Logger.Should().HaveNoWarnings();
        AssertPropertiesFileContainsCoverageXmlReportsPaths(additionalProperties, false);
        additionalProperties.CoverageConversionPerformed.Should().BeFalse();
    }

    [TestMethod]
    [CombinatorialData]
    public void ProcessCoverageReports_ConversionFails_ReturnsTrue(Properties properties)
    {
        // Conversion will fail automatically, if we don't copy a real file.
        SetupPropertiesAndFiles(properties, trx: true, coverage: true);

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        runtime.Logger.Should().HaveNoWarnings();
        AssertPropertiesFileContainsCoverageXmlReportsPaths(additionalProperties, false);
        additionalProperties.CoverageConversionPerformed.Should().BeFalse();
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_AlternateCoverageFileFound_Converts(Properties properties)
    {
        SetupPropertiesAndFiles(properties, alternate: true);
        CopySampleCoverageFile(alternateCoverageDir, "alternate.coverage");

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        AssertUsesFallback();
        AssertPropertiesFileContainsAlternateCoverageXmlReportsPaths(additionalProperties);
        AssertPropertiesFileContainsTestReportsPaths(additionalProperties, false);
        additionalProperties.CoverageConversionPerformed.Should().BeTrue();
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_AlternateCoverageFilesFound_VsCoverageXmlReportsPathsProvided_Converts_DoesNotWritePropertiesFile(Properties properties)
    {
        SetupPropertiesAndFiles(properties, alternate: true);
        CopySampleCoverageFile(alternateCoverageDir, "alternate.coverage");

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        AssertUsesFallback();
        runtime.File.DidNotReceiveWithAnyArgs().AppendAllText(null, null);
        additionalProperties.CoverageConversionPerformed.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndAlternateCoverageFileFound_DoesNotUseFallback(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true, alternate: true);

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        AssertUsesFallback(false);
        AssertPropertiesFileContainsTestReportsPaths(additionalProperties);
        AssertPropertiesFileContainsCoverageXmlReportsPaths(additionalProperties, false);
        additionalProperties.CoverageConversionPerformed.Should().BeFalse();
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    [DataRow(Properties.TestReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndAlternateCoverageFileFound_TestReportsPathsProvided_UsesFallback_Converts(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true, alternate: true);
        CopySampleCoverageFile(alternateCoverageDir, "alternate.coverage");

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        AssertUsesFallback();
        AssertPropertiesFileContainsTestReportsPaths(additionalProperties, false);
        additionalProperties.CoverageConversionPerformed.Should().BeTrue();
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndCoverageAndAlternateCoverageFileFound_Converts_DoesNotUseFallback(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true, coverage: true, alternate: true);
        CopySampleCoverageFile(coverageDir, "dummy.coverage");

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        AssertUsesFallback(false);
        AssertPropertiesFileContainsCoverageXmlReportsPaths(additionalProperties);
        additionalProperties.CoverageConversionPerformed.Should().BeTrue();
    }

    [TestMethod]
    [DeploymentItem(@"Resources")]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndCoverageAndAlternateCoverageFileFound_CoverageXmlReportsPathsProvided_Converts_DoesNotUseFallback_DoesNotWriteCoverageXmlReportsPathsToPropertiesFile(
        Properties properties)
    {
        SetupPropertiesAndFiles(properties, true, coverage: true, alternate: true);
        CopySampleCoverageFile(coverageDir, "dummy.coverage");

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        AssertUsesFallback(false);
        AssertPropertiesFileContainsCoverageXmlReportsPaths(additionalProperties, false);
        additionalProperties.CoverageConversionPerformed.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_AlternateCoverageFileFound_CoverageXmlFileAlreadyPresent_DoesNotConvert_UsesFallback(Properties properties)
    {
        SetupPropertiesAndFiles(properties, alternate: true, alternateXml: true);

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        AssertUsesFallback();
        AssertPropertiesFileContainsAlternateCoverageXmlReportsPaths(additionalProperties);
        AssertPropertiesFileContainsTestReportsPaths(additionalProperties, false);
        additionalProperties.CoverageConversionPerformed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_AlternateCoverageFileFound_CoverageXmlFileAlreadyPresent_CoverageXmlReportsPathsProvided_DoesNotConvert_UsesFallback_DoesNotWritePropertiesFile(
        Properties properties)
    {
        SetupPropertiesAndFiles(properties, alternate: true, alternateXml: true);

        var additionalProperties = sut.ProcessCoverageReports(analysisConfig, buildSettings);
        AssertUsesFallback();
        runtime.File.DidNotReceiveWithAnyArgs().AppendAllText(null, null);
        additionalProperties.CoverageConversionPerformed.Should().BeFalse();
    }

    [TestMethod]
    public void AgentDirectory_CalculatedCorrectly_Null()
    {
        using var envVars = new EnvironmentVariableScope();
        // env var not specified -> null
        envVars.SetVariable(EnvironmentVariables.AgentTempDirectory, null);

        sut.CheckAgentTempDirectory().Should().BeNull();
    }

    [TestMethod]
    public void AgentDirectory_CalculatedCorrectly_NonExisting()
    {
        var envDir = Path.Combine(testDir, "DirSpecifiedInEnvDir");
        using var envVars = new EnvironmentVariableScope();
        // Env var set but dir does not exist -> null
        envVars.SetVariable(EnvironmentVariables.AgentTempDirectory, envDir);

        sut.CheckAgentTempDirectory().Should().BeNull();
    }

    [TestMethod]
    public void AgentDirectory_CalculatedCorrectly_Existing()
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
        sut = new BuildVNextCoverageReportProcessor(converter, new TestRuntime { Directory = DirectoryWrapper.Instance, File = FileWrapper.Instance }); // no file mocking, test actual search behavior
        var subDir = Path.Combine(alternateCoverageDir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);
        TestUtils.CreateTextFile(alternateCoverageDir, "foo.coverageXXX", "1");              // wrong file extension
        TestUtils.CreateTextFile(alternateCoverageDir, "abc.trx", "2");                      // wrong file extension
        TestUtils.CreateTextFile(alternateCoverageDir, "BAR.coverage.XXX", "3");             // wrong file extension
        var lowerCasePath = TestUtils.CreateTextFile(alternateCoverageDir, "foo.coverage", "4");
        var upperCasePath = TestUtils.CreateTextFile(subDir, "BAR.COVERAGE", "5");

        sut.FindFallbackCoverageFiles().Should().BeEquivalentTo(lowerCasePath, upperCasePath);
    }

    [TestCategory(TestCategories.NoWindows)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void FindFallbackCoverageFiles_FilesLocatedCorrectly_Linux()
    {
        sut = new BuildVNextCoverageReportProcessor(converter, new TestRuntime { Directory = DirectoryWrapper.Instance, File = FileWrapper.Instance }); // no file mocking, test actual search behavior
        var subDir = Path.Combine(alternateCoverageDir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);
        TestUtils.CreateTextFile(alternateCoverageDir, "foo.coverageXXX", "1");             // wrong file extension
        TestUtils.CreateTextFile(alternateCoverageDir, "abc.trx", "2");                     // wrong file extension
        TestUtils.CreateTextFile(alternateCoverageDir, "BAR.coverage.XXX", "3");            // wrong file extension
        var lowerCasePath = TestUtils.CreateTextFile(alternateCoverageDir, "foo.coverage", "4");
        var upperCasePath = TestUtils.CreateTextFile(subDir, "BAR.COVERAGE", "5");
        var duplicate1FilePath = TestUtils.CreateTextFile(alternateCoverageDir, "DUPLICATE.coverage", "6");
        var duplicate2FilePath = TestUtils.CreateTextFile(alternateCoverageDir, "Duplicate.coverage", "7"); // Unix file system is case-sensitive, so these are two separate files

        sut.FindFallbackCoverageFiles().Should().Satisfy(
            x => x == lowerCasePath,    // should also find upperCasePath but does not due to case-sensitivity on Linux
            x => x == duplicate1FilePath,
            x => x == duplicate2FilePath);
    }

    [TestMethod]
    public void FindFallbackCoverageFiles_CalculatesAndDeDupesOnContentCorrectly()
    {
        sut = new BuildVNextCoverageReportProcessor(converter, new TestRuntime { Directory = DirectoryWrapper.Instance, File = FileWrapper.Instance }); // no file mocking, test actual search behavior
        var subDir = Path.Combine(alternateCoverageDir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);
        var file1 = "file1.coverage";
        var file1Duplicate = "file1Duplicate.coverage";
        var filePath1 = TestUtils.CreateTextFile(alternateCoverageDir, file1, "same content");
        var filePath1Duplicate = TestUtils.CreateTextFile(alternateCoverageDir, file1Duplicate, "same content");
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
    public void BinaryToXmlCoverageReportConverter_InvalidArgs_Throws() =>
        FluentActions.Invoking(() => _ = new BinaryToXmlCoverageReportConverter(null)).Should().Throw<ArgumentNullException>().WithParameterName("logger");

    [TestMethod]
    public void ConvertToXml_InvalidArgs_Throws()
    {
        var testSubject = new BinaryToXmlCoverageReportConverter(Substitute.For<ILogger>());

        // 1. Null input path
        testSubject.Invoking(x => x.ConvertToXml(null, "dummypath")).Should().Throw<ArgumentNullException>().WithParameterName("inputFilePath");
        testSubject.Invoking(x => x.ConvertToXml("\t\n", "dummypath")).Should().Throw<ArgumentNullException>().WithParameterName("inputFilePath");

        // 2. Null output path
        testSubject.Invoking(x => x.ConvertToXml("dummypath", null)).Should().Throw<ArgumentNullException>().WithParameterName("outputFilePath");
        testSubject.Invoking(x => x.ConvertToXml("dummypath", "   ")).Should().Throw<ArgumentNullException>().WithParameterName("outputFilePath");
    }

    [TestMethod]
    public void ConvertToXml_ConversionFailure_SuccessFalseAndErrorLogged()
    {
        var context = new ConverterTestContext(TestContext);
        new BinaryToXmlCoverageReportConverter(context.Logger).ConvertToXml(context.InputFilePath, context.OutputFilePath).Should().BeFalse();
        File.Exists(context.OutputFilePath).Should().BeFalse("Conversion failed");
        context.Logger.Should().HaveErrors($"""
            Failed to convert the binary code coverage reports to XML. No code coverage information will be uploaded to the server (SonarQube/SonarCloud).
            Check that the downloaded code coverage file ({context.InputFilePath}) is valid by opening it in Visual Studio. If it is not, check that the internet security settings on the build machine allow files to be downloaded from the Team Foundation Server machine.
            """)
            .And.HaveNoWarnings();
    }

    [TestMethod]
    public void ConvertToXml_FileConverterReturnsAnErrorCode_Fails()
    {
        var context = new ConverterTestContext(TestContext);
        new BinaryToXmlCoverageReportConverter(context.Logger).ConvertToXml(context.InputFilePath, context.OutputFilePath).Should().BeFalse("Expecting the process to fail");
        context.Logger.Should().HaveErrorOnce($"""
            Failed to convert the binary code coverage reports to XML. No code coverage information will be uploaded to the server (SonarQube/SonarCloud).
            Check that the downloaded code coverage file ({context.InputFilePath}) is valid by opening it in Visual Studio. If it is not, check that the internet security settings on the build machine allow files to be downloaded from the Team Foundation Server machine.
            """);
        File.Exists(context.OutputFilePath).Should().BeFalse("Not expecting the output file to exist");
    }

    [TestMethod]
    public void ConvertToXml_InputFileDoesNotExists_Fails()
    {
        var context = new ConverterTestContext(TestContext, fileContent: null);
        new BinaryToXmlCoverageReportConverter(context.Logger).ConvertToXml(context.InputFilePath, context.OutputFilePath).Should().BeFalse("Expecting the process to fail");
        context.Logger.Errors.Should().ContainSingle().Which.Should()
            .Be($"The binary coverage file {context.InputFilePath} could not be found. No coverage information will be uploaded to the Sonar server.");
        File.Exists(context.OutputFilePath).Should().BeFalse("Not expecting the output file to exist");
    }

    [TestMethod]
    public void ConvertToXml_InputFileIsLocked_Fails()
    {
        var context = new ConverterTestContext(TestContext);
        try
        {
            using var fs = new FileStream(context.InputFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); // lock the file with FileShare.None
            // FileShare.None will cause nested inner exceptions: AggregateException -> CoverageFileException -> IOException with messages
            // AggregateException: One or more errors occurred.
            // CoverageFileException: Failed to open coverage file "C:\Fullpath\input.txt".
            // IOException: The process cannot access the file 'C:\Fullpath\input.txt' because it is being used by another process.
            new BinaryToXmlCoverageReportConverter(context.Logger).ConvertToXml(context.InputFilePath, context.OutputFilePath).Should().BeFalse("Expecting the process to fail");
            context.Logger.Errors.Should().ContainSingle().Which.Should().Match($"Failed to convert the binary code coverage reports to XML. No code coverage information will be uploaded to the server (SonarQube/SonarCloud).*Check that the downloaded code coverage file ({context.InputFilePath}) is valid by opening it in Visual Studio. If it is not, check that the internet security settings on the build machine allow files to be downloaded from the Team Foundation Server machine.");
            File.Exists(context.OutputFilePath).Should().BeFalse("Not expecting the output file to exist");
        }
        finally
        {
            File.Delete(context.InputFilePath);
        }
    }

    [TestMethod]
    // DeploymentItem does not work on Linux for relative files: https://github.com/microsoft/testfx/issues/1460
    [DeploymentItem(@"Resources")] // Copy whole directory. Contains: Sample.coverage and Expected.xmlcoverage
    public void ConvertToXml_ConvertsSampleFile()
    {
        var logger = new TestLogger();
        var inputFilePath = Path.Combine(Environment.CurrentDirectory, "Sample.coverage");
        var outputFilePath = Path.Combine(Environment.CurrentDirectory, $"{nameof(ConvertToXml_ConvertsSampleFile)}.xmlcoverage");
        var expectedOutputFilePath = Path.Combine(Environment.CurrentDirectory, "Expected.xmlcoverage");

        File.Exists(inputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeFalse();
        File.Exists(expectedOutputFilePath).Should().BeTrue();
        new BinaryToXmlCoverageReportConverter(logger).ConvertToXml(inputFilePath, outputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeTrue();
        // All tags and attributes must appear in actual and expected. Comments, whitespace, ordering, and the like is ignored in the assertion.
        XDocument.Load(outputFilePath).Should().BeEquivalentTo(XDocument.Load(expectedOutputFilePath));
        logger.DebugMessages.Should().ContainSingle().Which.Should().Match($"Converting coverage file '*Sample.coverage' to '*{nameof(ConvertToXml_ConvertsSampleFile)}.xmlcoverage'.");
    }

    [TestMethod]
    // DeploymentItem does not work on Linux for relative files: https://github.com/microsoft/testfx/issues/1460
    [DeploymentItem(@"Resources")] // Copy whole directory. Contains: Sample.coverage and Expected.xmlcoverage
    public void ConvertToXml_ProblematicCulture_ConvertsSampleFile()
    {
        var logger = new TestLogger();
        var inputFilePath = Path.Combine(Environment.CurrentDirectory, "Sample.coverage");
        var outputFilePath = Path.Combine(Environment.CurrentDirectory, $"{nameof(ConvertToXml_ProblematicCulture_ConvertsSampleFile)}.xmlcoverage");
        var expectedOutputFilePath = Path.Combine(Environment.CurrentDirectory, "Expected.xmlcoverage");

        File.Exists(inputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeFalse();
        File.Exists(expectedOutputFilePath).Should().BeTrue();
        using var _ = new ApplicationCultureInfo(CultureInfo.GetCultureInfo("de-DE")); // Serializes block_coverage="33.33" as block_coverage="33,33"
        new BinaryToXmlCoverageReportConverter(logger).ConvertToXml(inputFilePath, outputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeTrue();
        // All tags and attributes must appear in actual and expected. Comments, whitespace, ordering, and the like is ignored in the assertion.
        XDocument.Load(outputFilePath).Should().BeEquivalentTo(XDocument.Load(expectedOutputFilePath));
    }

    private void SetupPropertiesAndFiles(Properties settings, bool trx = false, bool coverage = false, bool coverageXml = false, bool alternate = false, bool alternateXml = false)
    {
        analysisConfig.LocalSettings = settings switch
        {
            Properties.TestAndCoverageXmlReportsPathsNull => [new Property(SonarProperties.VsTestReportsPaths, null), new Property(SonarProperties.VsCoverageXmlReportsPaths, null)],
            Properties.TestReportsPathsNotNull => [new Property(SonarProperties.VsTestReportsPaths, "not null"), new Property(SonarProperties.VsCoverageXmlReportsPaths, null)],
            Properties.CoverageXmlReportsPathsNotNull => [new Property(SonarProperties.VsTestReportsPaths, null), new Property(SonarProperties.VsCoverageXmlReportsPaths, "not null")],
            Properties.TestAndCoverageXmlReportsPathsNotNull => [new Property(SonarProperties.VsTestReportsPaths, "not null"), new Property(SonarProperties.VsCoverageXmlReportsPaths, "not null")],
            _ => throw new NotSupportedException(settings + " is not a supported value.")
        };
        if (trx)
        {
            CreateFile(testResultsDir, "dummy.trx", """
                <?xml version="1.0" encoding="utf-8" ?>
                <x:TestRun id="4e4e4073-b17c-4bd0-a8bc-051bbc5a63e4" name="John@JOHN-DOE 2019-05-22 14:26:54:768" runUser="JOHN-DO\John" xmlns:x="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                    <x:ResultSummary outcome="Completed">
                    <x:CollectorDataEntries>
                        <x:Collector uri="datacollector://microsoft/CodeCoverage/2.0">
                            <x:UriAttachments>
                                <x:UriAttachment>
                                    <x:A href="dummy.coverage">dummy.coverage</x:A>
                                </x:UriAttachment>
                            </x:UriAttachments>
                        </x:Collector>
                    </x:CollectorDataEntries>
                    </x:ResultSummary>
                </x:TestRun>
                """);
            runtime.Directory.GetFiles(testResultsDir, "*.trx").Returns([Path.Combine(testResultsDir, "dummy.trx")]);
        }
        if (coverage)
        {
            CreateFile(coverageDir, "dummy.coverage", "coverage");
        }
        if (coverageXml)
        {
            CreateFile(coverageDir, "dummy.coveragexml", "coveragexml");
        }
        if (alternate)
        {
            CreateFile(alternateCoverageDir, "alternate.coverage", "alternate");
            runtime.Directory.GetFiles(alternateCoverageDir, "*.coverage", Arg.Any<SearchOption>()).Returns([Path.Combine(alternateCoverageDir, "alternate.coverage")]);
        }
        if (alternateXml)
        {
            CreateFile(alternateCoverageDir, "alternate.coveragexml", "alternate coveragexml");
        }
    }

    private static void AssertPropertiesFileContainsTestReportsPaths(AdditionalProperties additionalProperties, bool contains = true)
    {
        if (contains)
        {
            additionalProperties.VsTestReportsPaths.Should().ContainSingle(x => x.EndsWith(Path.Combine("TestResults", "dummy.trx")));
        }
        else
        {
            additionalProperties.VsTestReportsPaths.Should().BeNull();
        }
    }

    private static void AssertPropertiesFileContainsCoverageXmlReportsPaths(AdditionalProperties additionalProperties, bool contains = true)
    {
        if (contains)
        {
            additionalProperties.VsCoverageXmlReportsPaths.Should().ContainSingle(x => x.EndsWith(Path.Combine("TestResults", "dummy", "In", "dummy.coveragexml")));
        }
        else
        {
            additionalProperties.VsCoverageXmlReportsPaths.Should().BeNull();
        }
    }

    private static void AssertPropertiesFileContainsAlternateCoverageXmlReportsPaths(AdditionalProperties additionalProperties) =>
        additionalProperties.VsCoverageXmlReportsPaths.Should().ContainSingle(x => x.EndsWith(Path.Combine("TestResults", "alternate", "In", "alternate.coveragexml")));

    private void AssertUsesFallback(bool isTrue = true)
    {
        if (isTrue)
        {
            runtime.Logger.Should().HaveInfos("Did not find any binary coverage files in the expected location.")
                .And.NotHaveDebug(Resources.TRX_DIAG_NotUsingFallback);
        }
        else
        {
            runtime.Logger.Should().NotHaveInfo(Resources.TRX_DIAG_NoCoverageFilesFound)
                .And.HaveDebugs("Not using the fallback mechanism to detect binary coverage files.");
        }
    }

    private void CreateFile(string path, string fileName, string fileContent = "")
    {
        var filePath = Path.Combine(path, fileName);
        runtime.File.Exists(filePath).Returns(true);
        runtime.File.Open(filePath).Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));
        // some tests need the real file
        Directory.CreateDirectory(path);
        File.WriteAllText(filePath, fileContent);
    }

    private static void CopySampleCoverageFile(string path, string fileName) =>
        File.Copy(Path.Combine(Environment.CurrentDirectory, "Sample.coverage"), Path.Combine(path, fileName), overwrite: true);

    private class ConverterTestContext
    {
        public TestLogger Logger { get; }
        public string InputFilePath { get; }
        public string OutputFilePath { get; }

        public ConverterTestContext(TestContext testContext, string fileContent = "dummy input file", [CallerMemberName] string testMethodName = null)
        {
            Logger = new TestLogger();
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
