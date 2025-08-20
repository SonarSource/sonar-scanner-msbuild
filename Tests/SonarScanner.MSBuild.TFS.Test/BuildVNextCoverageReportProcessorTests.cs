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

using SonarScanner.MSBuild.TFS.Tests.Infrastructure;

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
    private readonly TestLogger testLogger = new();
    private readonly MockReportConverter converter = new();
    private readonly MockBuildSettings buildSettings = new();
    private readonly IFileWrapper fileWrapper = Substitute.For<IFileWrapper>();
    private readonly IDirectoryWrapper directoryWrapper = Substitute.For<IDirectoryWrapper>();
    private readonly string testDir;
    private readonly string testResultsDir;
    private readonly string coverageDir;
    private readonly string alternateCoverageDir;
    private readonly string propertiesFilePath;
    private readonly EnvironmentVariableScope environmentVariableScope = new();

    private BuildVNextCoverageReportProcessor sut;

    public BuildVNextCoverageReportProcessorTests(TestContext testContext)
    {
        testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
        directoryWrapper.Exists(testDir).Returns(true);
        testResultsDir = Path.Combine(testDir, "TestResults");
        directoryWrapper.GetDirectories(testDir, "TestResults", Arg.Any<SearchOption>()).Returns([testResultsDir]);
        coverageDir = Path.Combine(testResultsDir, "dummy", "In");
        alternateCoverageDir = Path.Combine(testResultsDir, "alternate", "In");
        directoryWrapper.Exists(alternateCoverageDir).Returns(true);
        propertiesFilePath = testDir + Path.DirectorySeparatorChar + "sonar-project.properties";
        buildSettings.BuildDirectory = testDir;
        sut = new BuildVNextCoverageReportProcessor(converter, testLogger, fileWrapper, directoryWrapper);
        environmentVariableScope.SetVariable(BuildVNextCoverageReportProcessor.AgentTempDirectory, alternateCoverageDir);  // setup search fallback
    }

    [TestCleanup]
    public void Cleanup() =>
        environmentVariableScope.Dispose();

    [TestMethod]
    public void Constructor_ConverterIsNull_ThrowsNullArgumentException()
    {
        var action = () => new BuildVNextCoverageReportProcessor(null, testLogger);
        action.Should().ThrowExactly<ArgumentNullException>().WithParameterName("converter");
    }

    [TestMethod]
    public void Constructor_LoggerIsNull_ThrowsNullArgumentException()
    {
        var action = () => new BuildVNextCoverageReportProcessor(converter, null);
        action.Should().ThrowExactly<ArgumentNullException>().WithParameterName("logger");
    }

    [TestMethod]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxFileFound_WritesPropertiesFile(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        AssertUsesFallback(false);
        testLogger.Warnings.Should().ContainSingle().Which.StartsWith("None of the following coverage attachments could be found: dummy.coverage");
        AssertPropertiesFileContainsTestReportsPaths();
        AssertPropertiesFileContainsCoverageXmlReportsPaths(false);
    }

    [TestMethod]
    [DataRow(Properties.TestReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxFileFound_TestReportsPathsProvided_DoesNotWritePropertiesFile(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        testLogger.Warnings.Should().ContainSingle().Which.StartsWith("None of the following coverage attachments could be found: dummy.coverage");
        fileWrapper.DidNotReceiveWithAnyArgs().AppendAllText(null, null);
    }

    [TestMethod]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.TestReportsPathsNotNull)]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_DoesNotWritePropertiesFile(Properties properties)
    {
        SetupPropertiesAndFiles(properties);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        AssertUsesFallback();
        testLogger.AssertWarningsLogged(0);
        fileWrapper.DidNotReceiveWithAnyArgs().AppendAllText(null, null);
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_Converts()
    {
        SetupPropertiesAndFiles(Properties.TestAndCoverageXmlReportsPathsNull, trx: true, coverage: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        AssertPropertiesFileContainsTestReportsPaths();
        AssertPropertiesFileContainsCoverageXmlReportsPaths();
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_TestReportsPathsProvided_Converts_DoesNotWriteTestReportsPathsToPropertiesFile()
    {
        SetupPropertiesAndFiles(Properties.TestReportsPathsNotNull, trx: true, coverage: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        AssertPropertiesFileContainsCoverageXmlReportsPaths();
        AssertPropertiesFileContainsTestReportsPaths(false);
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_CoverageXmlReportsPathsProvided_Converts_DoesNotWriteCoverageXmlReportsPathsToPropertiesFile()
    {
        SetupPropertiesAndFiles(Properties.CoverageXmlReportsPathsNotNull, trx: true, coverage: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        AssertPropertiesFileContainsTestReportsPaths();
        AssertPropertiesFileContainsCoverageXmlReportsPaths(false);
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_TestReportsAndCoverageXmlPathsProvided_Converts_DoesNotWritePropertiesFile()
    {
        SetupPropertiesAndFiles(Properties.TestAndCoverageXmlReportsPathsNotNull, trx: true, coverage: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        fileWrapper.DidNotReceiveWithAnyArgs().AppendAllText(null, null);
    }

    [TestMethod]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.TestReportsPathsNotNull)]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_CoverageFileFound_DoesNotConvert(Properties properties)
    {
        SetupPropertiesAndFiles(properties, coverage: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertExpectedNumberOfConversions(0);
        testLogger.AssertWarningsLogged(0);
        fileWrapper.DidNotReceiveWithAnyArgs().AppendAllText(null, null);
    }

    [TestMethod]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_CoverageXmlFileAlreadyPresent_DoesNotConvert_WritesCoverageXmlReportsPathsToPropertiesFile(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true, coverage: true, coverageXml: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertConvertNotCalled();
        testLogger.AssertWarningsLogged(0);
        AssertPropertiesFileContainsCoverageXmlReportsPaths();
    }

    [TestMethod]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_CoverageXmlFileAlreadyPresent_CoverageXmlReportsPathsProvided_DoesNotConvert_DoesNotWriteCoverageXmlReportsPathsToPropertiesFile(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true, coverage: true, coverageXml: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertConvertNotCalled();
        testLogger.AssertWarningsLogged(0);
        AssertPropertiesFileContainsCoverageXmlReportsPaths(false);
    }

    [TestMethod]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.TestReportsPathsNotNull)]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_ConversionFails_ReturnsTrue(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true, coverage: true);
        converter.ShouldFailConversion = true;

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        AssertPropertiesFileContainsCoverageXmlReportsPaths(false);
    }

    [TestMethod]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_AlternateCoverageFileFound_Converts(Properties properties)
    {
        SetupPropertiesAndFiles(properties, alternate: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertExpectedNumberOfConversions(1);
        AssertUsesFallback();
        AssertPropertiesFileContainsAlternateCoverageXmlReportsPaths();
        AssertPropertiesFileContainsTestReportsPaths(false);
    }

    [TestMethod]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_AlternateCoverageFilesFound_VsCoverageXmlReportsPathsProvided_Converts_DoesNotWritePropertiesFile(Properties properties)
    {
        SetupPropertiesAndFiles(properties, alternate: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        AssertUsesFallback();
        converter.AssertExpectedNumberOfConversions(1);
        fileWrapper.DidNotReceiveWithAnyArgs().AppendAllText(null, null);
    }

    [TestMethod]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndAlternateCoverageFileFound_DoesNotUseFallback(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true, alternate: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertExpectedNumberOfConversions(0);
        AssertUsesFallback(false);
        AssertPropertiesFileContainsTestReportsPaths();
        AssertPropertiesFileContainsCoverageXmlReportsPaths(false);
    }

    [TestMethod]
    [DataRow(Properties.TestReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndAlternateCoverageFileFound_TestReportsPathsProvided_UsesFallback_Converts(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true, alternate: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertExpectedNumberOfConversions(1);
        AssertUsesFallback();
        AssertPropertiesFileContainsTestReportsPaths(false);
    }

    [TestMethod]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndCoverageAndAlternateCoverageFileFound_Converts_DoesNotUseFallback(Properties properties)
    {
        SetupPropertiesAndFiles(properties, trx: true, coverage: true, alternate: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertExpectedNumberOfConversions(1);
        AssertUsesFallback(false);
        AssertPropertiesFileContainsCoverageXmlReportsPaths();
    }

    [TestMethod]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndCoverageAndAlternateCoverageFileFound_CoverageXmlReportsPathsProvided_Converts_DoesNotUseFallback_DoesNotWriteCoverageXmlReportsPathsToPropertiesFile(
        Properties properties)
    {
        SetupPropertiesAndFiles(properties, true, coverage: true, alternate: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertExpectedNumberOfConversions(1);
        AssertUsesFallback(false);
        AssertPropertiesFileContainsCoverageXmlReportsPaths(false);
    }

    [TestMethod]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Properties.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_AlternateCoverageFileFound_CoverageXmlFileAlreadyPresent_DoesNotConvert_UsesFallback(Properties properties)
    {
        SetupPropertiesAndFiles(properties, alternate: true, alternateXml: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertConvertNotCalled();
        AssertUsesFallback();
        AssertPropertiesFileContainsAlternateCoverageXmlReportsPaths();
        AssertPropertiesFileContainsTestReportsPaths(false);
    }

    [TestMethod]
    [DataRow(Properties.CoverageXmlReportsPathsNotNull)]
    [DataRow(Properties.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_AlternateCoverageFileFound_CoverageXmlFileAlreadyPresent_CoverageXmlReportsPathsProvided_DoesNotConvert_UsesFallback_DoesNotWritePropertiesFile(
        Properties properties)
    {
        SetupPropertiesAndFiles(properties, alternate: true, alternateXml: true);

        sut.ProcessCoverageReports(analysisConfig, buildSettings, propertiesFilePath, testLogger);
        converter.AssertConvertNotCalled();
        AssertUsesFallback();
        fileWrapper.DidNotReceiveWithAnyArgs().AppendAllText(null, null);
    }

    [TestMethod]
    public void AgentDirectory_CalculatedCorrectly_Null()
    {
        using var envVars = new EnvironmentVariableScope();
        // env var not specified -> null
        envVars.SetVariable(BuildVNextCoverageReportProcessor.AgentTempDirectory, null);

        sut.CheckAgentTempDirectory().Should().BeNull();
    }

    [TestMethod]
    public void AgentDirectory_CalculatedCorrectly_NonExisting()
    {
        var envDir = Path.Combine(testDir, "DirSpecifiedInEnvDir");
        using var envVars = new EnvironmentVariableScope();
        // Env var set but dir does not exist -> null
        envVars.SetVariable(BuildVNextCoverageReportProcessor.AgentTempDirectory, envDir);

        sut.CheckAgentTempDirectory().Should().BeNull();
    }

    [TestMethod]
    public void AgentDirectory_CalculatedCorrectly_Existing()
    {
        var envDir = Path.Combine(testDir, "DirSpecifiedInEnvDir");
        using var envVars = new EnvironmentVariableScope();
        // Env var set and dir exists -> dir returned
        directoryWrapper.Exists(envDir).Returns(true);
        envVars.SetVariable(BuildVNextCoverageReportProcessor.AgentTempDirectory, envDir);

        sut.CheckAgentTempDirectory().Should().Be(envDir);
    }

    [TestMethod]
    public void FindFallbackCoverageFiles_NoAgentDirectory_Empty()
    {
        using var envVars = new EnvironmentVariableScope();
        envVars.SetVariable(BuildVNextCoverageReportProcessor.AgentTempDirectory, null);

        sut.FindFallbackCoverageFiles().Should().BeEmpty();
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestMethod]
    public void FindFallbackCoverageFiles_FilesLocatedCorrectly_Windows_Mac()
    {
        sut = new BuildVNextCoverageReportProcessor(converter, testLogger);     // no file mocking, we need to test actual search behavior
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
        sut = new BuildVNextCoverageReportProcessor(converter, testLogger);     // no file mocking, we need to test actual search behavior
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
        sut = new BuildVNextCoverageReportProcessor(converter, testLogger);     // no file mocking, we need to test actual search behavior
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
        new BuildVNextCoverageReportProcessor.FileWithContentHash("c:\\path.txt", hash1)
            .Equals(new BuildVNextCoverageReportProcessor.FileWithContentHash("c:\\path.txt", hash2))
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
        new BuildVNextCoverageReportProcessor.FileWithContentHash(fileName1, [1, 2])
            .Equals(new BuildVNextCoverageReportProcessor.FileWithContentHash(fileName2, [1, 2]))
            .Should().BeTrue();

    [TestMethod]
    [DataRow("string")]
    [DataRow(42)]
    [DataRow(null)]
    public void FileWithContentHash_Equals_Other_False(object other) =>
        new BuildVNextCoverageReportProcessor.FileWithContentHash("c:\\path.txt", [1, 2])
            .Equals(other)
            .Should().BeFalse();

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
            directoryWrapper.GetFiles(testResultsDir, "*.trx").Returns([Path.Combine(testResultsDir, "dummy.trx")]);
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
            directoryWrapper.GetFiles(alternateCoverageDir, "*.coverage", Arg.Any<SearchOption>()).Returns([Path.Combine(alternateCoverageDir, "alternate.coverage")]);
        }
        if (alternateXml)
        {
            CreateFile(alternateCoverageDir, "alternate.coveragexml", "alternate coveragexml");
        }
    }

    private void AssertPropertiesFileContainsTestReportsPaths(bool contains = true)
    {
        if (contains)
        {
            fileWrapper.Received().AppendAllText(
                propertiesFilePath,
                Arg.Is<string>(x => x.Contains("sonar.cs.vstest.reportsPaths") && x.Contains(PathCombineWithEscape("TestResults", "dummy.trx"))));
        }
        else
        {
            fileWrapper.DidNotReceive().AppendAllText(
                propertiesFilePath,
                Arg.Is<string>(x => x.Contains(SonarProperties.VsTestReportsPaths)));
        }
    }

    private void AssertPropertiesFileContainsCoverageXmlReportsPaths(bool contains = true)
    {
        if (contains)
        {
            fileWrapper.Received().AppendAllText(
                propertiesFilePath,
                Arg.Is<string>(x => x.Contains("sonar.cs.vscoveragexml.reportsPaths") && x.Contains(PathCombineWithEscape("TestResults", "dummy", "In", "dummy.coveragexml"))));
        }
        else
        {
            fileWrapper.DidNotReceive().AppendAllText(
                propertiesFilePath,
                Arg.Is<string>(x => x.Contains(SonarProperties.VsCoverageXmlReportsPaths)));
        }
    }

    private void AssertPropertiesFileContainsAlternateCoverageXmlReportsPaths() =>
            fileWrapper.Received().AppendAllText(
                propertiesFilePath,
                Arg.Is<string>(x => x.Contains("sonar.cs.vscoveragexml.reportsPaths") && x.Contains(PathCombineWithEscape("TestResults", "alternate", "In", "alternate.coveragexml"))));

    private void AssertUsesFallback(bool isTrue = true)
    {
        if (isTrue)
        {
            testLogger.AssertInfoLogged("Did not find any binary coverage files in the expected location.");
            testLogger.AssertDebugNotLogged(Resources.TRX_DIAG_NotUsingFallback);
        }
        else
        {
            testLogger.AssertMessageNotLogged(Resources.TRX_DIAG_NoCoverageFilesFound);
            testLogger.AssertDebugLogged("Not using the fallback mechanism to detect binary coverage files.");
        }
    }

    private static string PathCombineWithEscape(params string[] parts)
    {
        var separator = Path.DirectorySeparatorChar.ToString();
        if (separator == @"\")
        {
            separator = @"\\";
        }
        return string.Join(separator, parts);
    }

    private string CreateFile(string path, string fileName, string fileContent = "")
    {
        var filePath = Path.Combine(path, fileName);
        fileWrapper.Exists(filePath).Returns(true);
        fileWrapper.Open(filePath).Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));
        return filePath;
    }
}
