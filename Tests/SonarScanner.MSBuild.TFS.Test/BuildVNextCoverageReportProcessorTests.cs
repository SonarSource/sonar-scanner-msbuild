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
    private const string TrxPayload = """
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
        """;

    [Flags]
    public enum Settings
    {
        None = 0,
        TestReportsPathsNotNull = 1,
        CoverageXmlReportsPathsNotNull = 2
    }

    private readonly Dictionary<Settings, AnalysisProperties> localSettings = new()
    {
        { Settings.None, [new Property(SonarProperties.VsTestReportsPaths, null), new Property(SonarProperties.VsCoverageXmlReportsPaths, null)] },
        { Settings.TestReportsPathsNotNull, [new Property(SonarProperties.VsTestReportsPaths, "not null"), new Property(SonarProperties.VsCoverageXmlReportsPaths, null)] },
        { Settings.CoverageXmlReportsPathsNotNull, [new Property(SonarProperties.VsTestReportsPaths, null), new Property(SonarProperties.VsCoverageXmlReportsPaths, "not null")] },
        {
            Settings.TestReportsPathsNotNull | Settings.CoverageXmlReportsPathsNotNull,
            [new Property(SonarProperties.VsTestReportsPaths, "not null"), new Property(SonarProperties.VsCoverageXmlReportsPaths, "not null")]
        }
    };
    private readonly AnalysisConfig analysisConfig = new();
    private readonly TestLogger testLogger = new();
    private readonly MockReportConverter converter = new();
    private readonly MockBuildSettings settings = new();
    private readonly string testResultsDir;
    private readonly string coverageDir;
    private readonly string propertiesFilePath;

    private BuildVNextCoverageReportProcessor sut;

    public BuildVNextCoverageReportProcessorTests()
    {
        var testDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        testResultsDir = Directory.CreateDirectory(Path.Combine(testDir, "TestResults")).FullName;
        coverageDir = Directory.CreateDirectory(Path.Combine(testResultsDir, "dummy", "In")).FullName;
        propertiesFilePath = testDir + Path.DirectorySeparatorChar + "sonar-project.properties";
        settings.BuildDirectory = testDir;
        sut = new BuildVNextCoverageReportProcessor(converter, testLogger);
        sut.Initialize(analysisConfig, settings, propertiesFilePath);
    }

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
    [DataRow(Settings.None)]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxFileFound_WritesPropertiesFile(Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        AssertUsesFallback(false);
        testLogger.Warnings.Should().ContainSingle().Which.StartsWith("None of the following coverage attachments could be found: dummy.coverage");
        AssertPropertiesFileContainsOnlyTestReportsPaths();
    }

    [TestMethod]
    [DataRow(Settings.TestReportsPathsNotNull)]
    [DataRow(Settings.TestReportsPathsNotNull | Settings.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxFileFound_TestReportsPathsProvided_DoesNotWritePropertiesFile(Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        testLogger.Warnings.Should().ContainSingle().Which.StartsWith("None of the following coverage attachments could be found: dummy.coverage");
        File.Exists(propertiesFilePath).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(Settings.None)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestReportsPathsNotNull | Settings.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_DoesNotWritePropertiesFile(Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        AssertUsesFallback();
        testLogger.AssertWarningsLogged(0);
        File.Exists(propertiesFilePath).Should().BeFalse();
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_Converts()
    {
        analysisConfig.LocalSettings = localSettings[Settings.None];
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", "coverage");

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should().ContainAll(
            "sonar.cs.vstest.reportsPaths",
            @"\\TestResults\\dummy.trx",
            "sonar.cs.vscoveragexml.reportsPaths",
            @"\\TestResults\\dummy\\In\\dummy.coveragexml");
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_TestReportsPathsProvided_Converts_DoesNotWriteTestReportsPathsToPropertiesFile()
    {
        analysisConfig.LocalSettings = localSettings[Settings.TestReportsPathsNotNull];
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", "coverage");

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should()
            .ContainAll("sonar.cs.vscoveragexml.reportsPaths", @"\\TestResults\\dummy\\In\\dummy.coveragexml")
            .And.NotContainAny(SonarProperties.VsTestReportsPaths, "dummy.trx");
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_CoverageXmlReportsPathsProvided_Converts_DoesNotWriteCoverageXmlReportsPathsToPropertiesFile()
    {
        analysisConfig.LocalSettings = localSettings[Settings.CoverageXmlReportsPathsNotNull];
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", "coverage");

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        AssertPropertiesFileContainsOnlyTestReportsPaths();
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxAndCoverageFileFound_TestReportsAndCoverageXmlPathsProvided_Converts_DoesNotWritePropertiesFile()
    {
        analysisConfig.LocalSettings = localSettings[Settings.TestReportsPathsNotNull | Settings.CoverageXmlReportsPathsNotNull];
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", "coverage");

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        File.Exists(propertiesFilePath).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(Settings.None)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestReportsPathsNotNull | Settings.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_CoverageFileFound_DoesNotConvert(Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", "coverage");

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(0);
        testLogger.AssertWarningsLogged(0);
        File.Exists(propertiesFilePath).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(Settings.None)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_CoverageXmlFileAlreadyPresent_DoesNotConvert_WritesCoverageXmlReportsPathsToPropertiesFile(Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", "coverage");
        TestUtils.CreateTextFile(coverageDir, "dummy.coveragexml", "coveragexml");

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertConvertNotCalled();
        testLogger.AssertWarningsLogged(0);
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should()
            .ContainAll("sonar.cs.vscoveragexml.reportsPaths", @"\\TestResults\\dummy\\In\\dummy.coveragexml");
    }

    [TestMethod]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestReportsPathsNotNull | Settings.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_CoverageXmlFileAlreadyPresent_CoverageXmlReportsPathsProvided_DoesNotConvert_DoesNotWriteCoverageXmlReportsPathsToPropertiesFile(Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", "coverage");
        TestUtils.CreateTextFile(coverageDir, "dummy.coveragexml", "coveragexml");

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertConvertNotCalled();
        testLogger.AssertWarningsLogged(0);
        AssertPropertiesFileDoesNotContainCoverageXmlReportsPaths();
    }

    [TestMethod]
    [DataRow(Settings.None)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestReportsPathsNotNull | Settings.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_ConversionFails_ReturnsTrue(Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", "coverage");
        converter.ShouldFailConversion = true;

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        testLogger.AssertWarningsLogged(0);
        AssertPropertiesFileDoesNotContainCoverageXmlReportsPaths();
    }

    [TestMethod]
    [DataRow(Settings.None)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_AlternateCoverageFileFound_Converts(Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(testResultsDir, "alternate.coverage", "alternate");
        using var envVars = new EnvironmentVariableScope(); // set up search fallback
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AgentTempDirectory, testResultsDir);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        AssertUsesFallback();
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should()
            .ContainAll("sonar.cs.vscoveragexml.reportsPaths", @"\\TestResults\\alternate.coveragexml")
            .And.NotContain(SonarProperties.VsTestReportsPaths);
    }

    [TestMethod]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestReportsPathsNotNull | Settings.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_NoTrxFilesFound_AlternateCoverageFilesFound_VsCoverageXmlReportsPathsProvided_Converts_DoesNotWritePropertiesFile(Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(testResultsDir, "alternate.coverage", "alternate");
        using var envVars = new EnvironmentVariableScope(); // set up search fallback
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AgentTempDirectory, testResultsDir);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        AssertUsesFallback();
        converter.AssertExpectedNumberOfConversions(1);
        File.Exists(propertiesFilePath).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(Settings.None)]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndAlternateCoverageFileFound_DoesNotUseFallback(Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(testResultsDir, "alternate.coverage", "alternate");
        using var envVars = new EnvironmentVariableScope(); // This sets up the search fallback to demonstrate that it's not used.
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AgentTempDirectory, testResultsDir);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(0);
        AssertUsesFallback(false);
        AssertPropertiesFileContainsOnlyTestReportsPaths();
    }

    [TestMethod]
    [DataRow(Settings.TestReportsPathsNotNull)]
    [DataRow(Settings.TestReportsPathsNotNull | Settings.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndAlternateCoverageFileFound_TestReportsPathsProvided_UsesFallback_Converts(Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(testResultsDir, "alternate.coverage", "alternate");
        using var envVars = new EnvironmentVariableScope(); // This sets up the search fallback to demonstrate that it's not used.
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AgentTempDirectory, testResultsDir);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        AssertUsesFallback();
        AssertPropertiesFileDoesNotContainTestReportsPaths();
    }

    [TestMethod]
    [DataRow(Settings.None)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndCoverageAndAlternateCoverageFileFound_Converts_DoesNotUseFallback(Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", "coverage");
        TestUtils.CreateTextFile(testResultsDir, "alternate.coverage", "alternate");
        using var envVars = new EnvironmentVariableScope(); // This sets up the search fallback to demonstrate that it's not used.
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AgentTempDirectory, testResultsDir);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        AssertUsesFallback(false);
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should()
            .ContainAll("sonar.cs.vscoveragexml.reportsPaths", @"\\TestResults\\dummy\\In\\dummy.coveragexml");
    }

    [TestMethod]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestReportsPathsNotNull | Settings.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_TrxAndCoverageAndAlternateCoverageFileFound_CoverageXmlReportsPathsProvided_Converts_DoesNotUseFallback_DoesNotWriteCoverageXmlReportsPathsToPropertiesFile(
        Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", "coverage");
        TestUtils.CreateTextFile(testResultsDir, "alternate.coverage", "alternate");
        using var envVars = new EnvironmentVariableScope(); // This sets up the search fallback to demonstrate that it's not used.
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AgentTempDirectory, testResultsDir);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        AssertUsesFallback(false);
        AssertPropertiesFileDoesNotContainCoverageXmlReportsPaths();
    }

    [TestMethod]
    [DataRow(Settings.None)]
    [DataRow(Settings.TestReportsPathsNotNull)]
    public void ProcessCoverageReports_AlternateCoverageFileFound_CoverageXmlFileAlreadyPresent_DoesNotConvert_UsesFallback(Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(testResultsDir, "alternate.coverage", "alternate");
        TestUtils.CreateTextFile(testResultsDir, "alternate.coveragexml", "alternate coveragexml");
        using var envVars = new EnvironmentVariableScope(); // set up search fallback
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AgentTempDirectory, testResultsDir);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertConvertNotCalled();
        AssertUsesFallback();
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should()
            .ContainAll("sonar.cs.vscoveragexml.reportsPaths", @"\\TestResults\\alternate.coveragexml")
            .And.NotContain(SonarProperties.VsTestReportsPaths);
    }

    [TestMethod]
    [DataRow(Settings.CoverageXmlReportsPathsNotNull)]
    [DataRow(Settings.TestReportsPathsNotNull | Settings.CoverageXmlReportsPathsNotNull)]
    public void ProcessCoverageReports_AlternateCoverageFileFound_CoverageXmlFileAlreadyPresent_CoverageXmlReportsPathsProvided_DoesNotConvert_UsesFallback_DoesNotWritePropertiesFile(
        Settings settings)
    {
        analysisConfig.LocalSettings = localSettings[settings];
        TestUtils.CreateTextFile(testResultsDir, "alternate.coverage", "alternate");
        TestUtils.CreateTextFile(testResultsDir, "alternate.coveragexml", "alternate coveragexml");
        using var envVars = new EnvironmentVariableScope(); // set up search fallback
        envVars.SetVariable(BuildVNextCoverageSearchFallback.AgentTempDirectory, testResultsDir);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertConvertNotCalled();
        AssertUsesFallback();
        File.Exists(propertiesFilePath).Should().BeFalse();
    }

    private void AssertPropertiesFileDoesNotContainTestReportsPaths() =>
        (File.Exists(propertiesFilePath) && File.ReadAllText(propertiesFilePath).Contains(SonarProperties.VsTestReportsPaths)).Should().BeFalse();

    private void AssertPropertiesFileDoesNotContainCoverageXmlReportsPaths() =>
        (File.Exists(propertiesFilePath) && File.ReadAllText(propertiesFilePath).Contains(SonarProperties.VsCoverageXmlReportsPaths)).Should().BeFalse();

    private void AssertPropertiesFileContainsOnlyTestReportsPaths()
    {
        File.Exists(propertiesFilePath).Should().BeTrue();
        File.ReadAllText(propertiesFilePath).Should()
            .ContainAll("sonar.cs.vstest.reportsPaths", @"\\TestResults\\dummy.trx")
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
            testLogger.AssertMessageNotLogged(Resources.TRIX_DIAG_NoCoverageFilesFound);
            testLogger.AssertDebugLogged("Not using the fallback mechanism to detect binary coverage files.");
        }
    }
}
