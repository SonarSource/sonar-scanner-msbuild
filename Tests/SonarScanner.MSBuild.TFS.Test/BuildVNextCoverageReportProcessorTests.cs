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

using SonarScanner.MSBuild.TFS.Tests.Infrastructure;

namespace SonarScanner.MSBuild.TFS.Test;

[TestClass]
public class BuildVNextCoverageReportProcessorTests
{
    public enum Settings
    {
        TestAndCoverageXmlReportsPathsNull,
        TestReportsPathsNotNull,
        CoverageXmlReportsPathsNotNull,
        TestAndCoverageXmlReportsPathsNotNull
    }

    private readonly AnalysisConfig analysisConfig = new();
    private readonly TestLogger testLogger = new();
    private readonly MockReportConverter converter = new();
    private readonly MockBuildSettings settings = new();
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
        testResultsDir = Directory.CreateDirectory(Path.Combine(testDir, "TestResults")).FullName;
        coverageDir = Directory.CreateDirectory(Path.Combine(testResultsDir, "dummy", "In")).FullName;
        alternateCoverageDir = Directory.CreateDirectory(Path.Combine(testResultsDir, "alternate", "In")).FullName;
        propertiesFilePath = testDir + Path.DirectorySeparatorChar + "sonar-project.properties";
        settings.BuildDirectory = testDir;
        sut = new BuildVNextCoverageReportProcessor(converter, testLogger);
        sut.Initialize(analysisConfig, settings, propertiesFilePath);
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
    public void Initializer_ConfigIsNull_ThrowsNullArgumentException()
    {
        var action = () => new BuildVNextCoverageReportProcessor(converter, testLogger).Initialize(null, settings, "properties file path");
        action.Should().ThrowExactly<ArgumentNullException>().WithParameterName("config");
    }

    [TestMethod]
    public void Initializer_SettingsAreNull_ThrowsNullArgumentException()
    {
        var action = () => new BuildVNextCoverageReportProcessor(converter, testLogger).Initialize(analysisConfig, null, "properties file path");
        action.Should().ThrowExactly<ArgumentNullException>().WithParameterName("settings");
    }

    [TestMethod]
    public void Initializer_PropertiesFilePathIsNull_ThrowsNullArgumentException()
    {
        var action = () => new BuildVNextCoverageReportProcessor(converter, testLogger).Initialize(analysisConfig, settings, null);
        action.Should().ThrowExactly<ArgumentNullException>().WithParameterName("propertiesFilePath");
    }

    [TestMethod]
    public void ProcessCoverageReports_Uninitialized_InvalidOperationException()
    {
        sut = new BuildVNextCoverageReportProcessor(converter, testLogger);
        var action = () => sut.ProcessCoverageReports(testLogger);
        action.Should().ThrowExactly<InvalidOperationException>().WithMessage("The Coverage Report Processor was not initialized before use.");
    }

    [TestMethod]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxFileFound_WritesPropertiesFile(Settings settings)
    {
        SetupSettingsAndFiles(settings, trx: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        AssertUsesFallback(false);
        testLogger.Warnings.Should().ContainSingle().Which.StartsWith("None of the following coverage attachments could be found: dummy.coverage");
        AssertPropertiesFileContainsOnlyTestReportsPaths();
    }

    [TestMethod]
    [DataRow(Settings.TestReportsPathsNotNull)]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxFileFound_TestReportsPathsProvided_DoesNotWritePropertiesFile(Settings settings)
    {
        SetupSettingsAndFiles(settings, trx: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        testLogger.Warnings.Should().ContainSingle().Which.StartsWith("None of the following coverage attachments could be found: dummy.coverage");
        File.Exists(propertiesFilePath).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_DoesNotWritePropertiesFile(Settings settings)
    {
        SetupSettingsAndFiles(settings);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        AssertUsesFallback();
        testLogger.AssertWarningsLogged(0);
        File.Exists(propertiesFilePath).Should().BeFalse();
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_Converts()
    {
        SetupSettingsAndFiles(Settings.TestAndCoverageXmlReportsPathsNull, trx: true, coverage: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should().ContainAll(
            "sonar.cs.vstest.reportsPaths",
            PathCombineWithEscape("TestResults", "dummy.trx"),
            "sonar.cs.vscoveragexml.reportsPaths",
            PathCombineWithEscape("TestResults", "dummy", "In", "dummy.coveragexml"));
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_TestReportsPathsProvided_Converts_DoesNotWriteTestReportsPathsToPropertiesFile()
    {
        SetupSettingsAndFiles(Settings.TestReportsPathsNotNull, trx: true, coverage: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should()
            .ContainAll("sonar.cs.vscoveragexml.reportsPaths", PathCombineWithEscape("TestResults", "dummy", "In", "dummy.coveragexml"))
            .And.NotContainAny(SonarProperties.VsTestReportsPaths, "dummy.trx");
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_CoverageXmlReportsPathsProvided_Converts_DoesNotWriteCoverageXmlReportsPathsToPropertiesFile()
    {
        SetupSettingsAndFiles(Settings.CoverageXmlReportsPathsNotNull, trx: true, coverage: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        AssertPropertiesFileContainsOnlyTestReportsPaths();
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_TestReportsAndCoverageXmlPathsProvided_Converts_DoesNotWritePropertiesFile()
    {
        SetupSettingsAndFiles(Settings.TestAndCoverageXmlReportsPathsNotNull, trx: true, coverage: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        File.Exists(propertiesFilePath).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_CoverageFileFound_DoesNotConvert(Settings settings)
    {
        SetupSettingsAndFiles(settings, coverage: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(0);
        testLogger.AssertWarningsLogged(0);
        File.Exists(propertiesFilePath).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_CoverageXmlFileAlreadyPresent_DoesNotConvert_WritesCoverageXmlReportsPathsToPropertiesFile(Settings settings)
    {
        SetupSettingsAndFiles(settings, trx: true, coverage: true, coverageXml: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertConvertNotCalled();
        testLogger.AssertWarningsLogged(0);
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should()
            .ContainAll("sonar.cs.vscoveragexml.reportsPaths", PathCombineWithEscape("TestResults", "dummy", "In", "dummy.coveragexml"));
    }

    [TestMethod]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_CoverageXmlFileAlreadyPresent_CoverageXmlReportsPathsProvided_DoesNotConvert_DoesNotWriteCoverageXmlReportsPathsToPropertiesFile(Settings settings)
    {
        SetupSettingsAndFiles(settings, trx: true, coverage: true, coverageXml: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertConvertNotCalled();
        testLogger.AssertWarningsLogged(0);
        AssertPropertiesFileDoesNotContainCoverageXmlReportsPaths();
    }

    [TestMethod]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_ConversionFails_ReturnsTrue(Settings settings)
    {
        SetupSettingsAndFiles(settings, trx: true, coverage: true);
        converter.ShouldFailConversion = true;

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        AssertPropertiesFileDoesNotContainCoverageXmlReportsPaths();
    }

    [TestMethod]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_AlternateCoverageFileFound_Converts(Settings settings)
    {
        SetupSettingsAndFiles(settings, alternate: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        AssertUsesFallback();
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should()
            .ContainAll("sonar.cs.vscoveragexml.reportsPaths", PathCombineWithEscape("TestResults", "alternate", "In", "alternate.coveragexml"))
            .And.NotContain(SonarProperties.VsTestReportsPaths);
    }

    [TestMethod]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_AlternateCoverageFilesFound_VsCoverageXmlReportsPathsProvided_Converts_DoesNotWritePropertiesFile(Settings settings)
    {
        SetupSettingsAndFiles(settings, alternate: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        AssertUsesFallback();
        converter.AssertExpectedNumberOfConversions(1);
        File.Exists(propertiesFilePath).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndAlternateCoverageFileFound_DoesNotUseFallback(Settings settings)
    {
        SetupSettingsAndFiles(settings, trx: true, alternate: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(0);
        AssertUsesFallback(false);
        AssertPropertiesFileContainsOnlyTestReportsPaths();
    }

    [TestMethod]
    [DataRow(Settings.TestReportsPathsNotNull)]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndAlternateCoverageFileFound_TestReportsPathsProvided_UsesFallback_Converts(Settings settings)
    {
        SetupSettingsAndFiles(settings, trx: true, alternate: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        AssertUsesFallback();
        AssertPropertiesFileDoesNotContainTestReportsPaths();
    }

    [TestMethod]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndCoverageAndAlternateCoverageFileFound_Converts_DoesNotUseFallback(Settings settings)
    {
        SetupSettingsAndFiles(settings, trx: true, coverage: true, alternate: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        AssertUsesFallback(false);
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should()
            .ContainAll("sonar.cs.vscoveragexml.reportsPaths", PathCombineWithEscape("TestResults", "dummy", "In", "dummy.coveragexml"));
    }

    [TestMethod]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndCoverageAndAlternateCoverageFileFound_CoverageXmlReportsPathsProvided_Converts_DoesNotUseFallback_DoesNotWriteCoverageXmlReportsPathsToPropertiesFile(
        Settings settings)
    {
        SetupSettingsAndFiles(settings, true, coverage: true, alternate: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        AssertUsesFallback(false);
        AssertPropertiesFileDoesNotContainCoverageXmlReportsPaths();
    }

    [TestMethod]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNull)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_AlternateCoverageFileFound_CoverageXmlFileAlreadyPresent_DoesNotConvert_UsesFallback(Settings settings)
    {
        SetupSettingsAndFiles(settings, alternate: true, alternateXml: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertConvertNotCalled();
        AssertUsesFallback();
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should()
            .ContainAll("sonar.cs.vscoveragexml.reportsPaths", PathCombineWithEscape("TestResults", "alternate", "In", "alternate.coveragexml"))
            .And.NotContain(SonarProperties.VsTestReportsPaths);
    }

    [TestMethod]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestAndCoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_AlternateCoverageFileFound_CoverageXmlFileAlreadyPresent_CoverageXmlReportsPathsProvided_DoesNotConvert_UsesFallback_DoesNotWritePropertiesFile(
        Settings settings)
    {
        SetupSettingsAndFiles(settings, alternate: true, alternateXml: true);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertConvertNotCalled();
        AssertUsesFallback();
        File.Exists(propertiesFilePath).Should().BeFalse();
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
        Directory.CreateDirectory(envDir);
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
        var subDir = Path.Combine(testDir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);
        TestUtils.CreateTextFile(testDir, "foo.coverageXXX", "1");              // wrong file extension
        TestUtils.CreateTextFile(testDir, "abc.trx", "2");                      // wrong file extension
        TestUtils.CreateTextFile(testDir, "BAR.coverage.XXX", string.Empty);    // wrong file extension
        var lowerCasePath = TestUtils.CreateTextFile(testDir, "foo.coverage", "3");
        var upperCasePath = TestUtils.CreateTextFile(subDir, "BAR.COVERAGE", "5");
        var duplicate1FilePath = TestUtils.CreateTextFile(testDir, "DUPLICATE.coverage", "4");
        var duplicate2FilePath = TestUtils.CreateTextFile(testDir, "Duplicate.coverage", "4");
        using var envVars = new EnvironmentVariableScope();
        envVars.SetVariable(BuildVNextCoverageReportProcessor.AgentTempDirectory, testDir);

        sut.FindFallbackCoverageFiles().Should().Satisfy(
            x => x == lowerCasePath,
            x => x == upperCasePath,
            x => x == duplicate1FilePath || x == duplicate2FilePath);
    }

    [TestCategory(TestCategories.NoWindows)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void FindFallbackCoverageFiles_FilesLocatedCorrectly_Linux()
    {
        var subDir = Path.Combine(testDir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);
        TestUtils.CreateTextFile(testDir, "foo.coverageXXX", "1");              // wrong file extension
        TestUtils.CreateTextFile(testDir, "abc.trx", "2");                      // wrong file extension
        TestUtils.CreateTextFile(testDir, "BAR.coverage.XXX", string.Empty);    // wrong file extension
        var lowerCasePath = TestUtils.CreateTextFile(testDir, "foo.coverage", "3");
        var upperCasePath = TestUtils.CreateTextFile(subDir, "BAR.COVERAGE", "5");
        var duplicate1FilePath = TestUtils.CreateTextFile(testDir, "DUPLICATE.coverage", "4");
        var duplicate2FilePath = TestUtils.CreateTextFile(testDir, "Duplicate.coverage", "4");
        using var envVars = new EnvironmentVariableScope();
        envVars.SetVariable(BuildVNextCoverageReportProcessor.AgentTempDirectory, testDir);

        sut.FindFallbackCoverageFiles().Should().Satisfy(
            x => x == lowerCasePath,    // should also find upperCasePath but does not due to case-sensitivity on Linux
            x => x == duplicate1FilePath || x == duplicate2FilePath);
    }

    [TestMethod]
    public void FindFallbackCoverageFiles_CalculatesAndDeDupesOnContentCorrectly()
    {
        var subDir = Path.Combine(testDir, "subDir", "subDir2");
        Directory.CreateDirectory(subDir);
        var file1 = "file1.coverage";
        var file2 = "file2.coverage";
        var file3 = "file3.coverage";
        var file1Duplicate = "file1Duplicate.coverage";
        var filePath1 = TestUtils.CreateTextFile(testDir, file1, file1);
        var filePath2 = TestUtils.CreateTextFile(testDir, file2, file2);
        var filePath3 = TestUtils.CreateTextFile(testDir, file3, file3);
        var filePath1Duplicate = TestUtils.CreateTextFile(testDir, file1Duplicate, file1);
        var filePath1SubDir = TestUtils.CreateTextFile(subDir, file1, file1);
        using var envVars = new EnvironmentVariableScope();
        envVars.SetVariable(BuildVNextCoverageReportProcessor.AgentTempDirectory, testDir);

        sut.FindFallbackCoverageFiles().Should()
            .HaveCount(3, "the 5 files should be de-duped based on content hash.")
            .And.Satisfy(
            x => x == filePath1 || x == filePath1Duplicate || x == filePath1SubDir,
            x => x == filePath2,
            x => x == filePath3);
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

    private void SetupSettingsAndFiles(Settings settings, bool trx = false, bool coverage = false, bool coverageXml = false, bool alternate = false, bool alternateXml = false)
    {
        analysisConfig.LocalSettings = settings switch
        {
            Settings.TestAndCoverageXmlReportsPathsNull => [new Property(SonarProperties.VsTestReportsPaths, null), new Property(SonarProperties.VsCoverageXmlReportsPaths, null)],
            Settings.TestReportsPathsNotNull => [new Property(SonarProperties.VsTestReportsPaths, "not null"), new Property(SonarProperties.VsCoverageXmlReportsPaths, null)],
            Settings.CoverageXmlReportsPathsNotNull => [new Property(SonarProperties.VsTestReportsPaths, null), new Property(SonarProperties.VsCoverageXmlReportsPaths, "not null")],
            Settings.TestAndCoverageXmlReportsPathsNotNull => [new Property(SonarProperties.VsTestReportsPaths, "not null"), new Property(SonarProperties.VsCoverageXmlReportsPaths, "not null")],
            _ => throw new NotSupportedException(settings + " is not a supported value.")
        };
        if (trx)
        {
            TestUtils.CreateTextFile(testResultsDir, "dummy.trx", """
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
        }
        if (coverage)
        {
            TestUtils.CreateTextFile(coverageDir, "dummy.coverage", "coverage");
        }
        if (coverageXml)
        {
            TestUtils.CreateTextFile(coverageDir, "dummy.coveragexml", "coveragexml");
        }
        if (alternate)
        {
            TestUtils.CreateTextFile(alternateCoverageDir, "alternate.coverage", "alternate");
        }
        if (alternateXml)
        {
            TestUtils.CreateTextFile(alternateCoverageDir, "alternate.coveragexml", "alternate coveragexml");
        }
    }

    private void AssertPropertiesFileDoesNotContainTestReportsPaths() =>
        (File.Exists(propertiesFilePath) && File.ReadAllText(propertiesFilePath).Contains(SonarProperties.VsTestReportsPaths)).Should().BeFalse();

    private void AssertPropertiesFileDoesNotContainCoverageXmlReportsPaths() =>
        (File.Exists(propertiesFilePath) && File.ReadAllText(propertiesFilePath).Contains(SonarProperties.VsCoverageXmlReportsPaths)).Should().BeFalse();

    private void AssertPropertiesFileContainsOnlyTestReportsPaths()
    {
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should()
            .ContainAll("sonar.cs.vstest.reportsPaths", PathCombineWithEscape("TestResults", "dummy.trx"))
            .And.NotContainAny(SonarProperties.VsCoverageXmlReportsPaths);
    }

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
}
