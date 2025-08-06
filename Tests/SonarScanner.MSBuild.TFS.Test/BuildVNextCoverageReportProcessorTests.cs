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

    private readonly MockSearchFallback mockSearchFallback = new();
    private readonly AnalysisConfig analysisConfig = new() { LocalSettings = [] };
    private readonly TestLogger testLogger = new();
    private readonly MockReportConverter converter = new();

    private string testDir;
    private string testResultsDir;
    private string coverageDir;
    private MockBuildSettings settings;
    private BuildVNextCoverageReportProcessor sut;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Initialize()
    {
        testDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        testResultsDir = Directory.CreateDirectory(Path.Combine(testDir, "TestResults")).FullName;
        coverageDir = Directory.CreateDirectory(Path.Combine(testResultsDir, "dummy", "In")).FullName;
        settings = new MockBuildSettings
        {
            BuildDirectory = testDir
        };
        sut = new BuildVNextCoverageReportProcessor(converter, testLogger, mockSearchFallback);
        sut.Initialize(analysisConfig, settings, testDir + Path.DirectorySeparatorChar + "sonar-project.properties");
    }

    [TestMethod]
    public void ProcessCoverageReports_NoTrxFilesFound_CallsSearchFallback()
    {
        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        mockSearchFallback.FallbackCalled.Should().BeTrue();
    }

    [TestMethod]
    public void ProcessCoverageReports_TrxFilesFound_DoesNotCallSearchFallback()
    {
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        mockSearchFallback.FallbackCalled.Should().BeFalse();
    }

    [TestMethod]
    public void ProcessCoverageReports_VsCoverageXmlPathProvided_NotCoverageXmlFileAlreadyPresent_TriesConverting()
    {
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", string.Empty);
        var coveragePathValue = "ThisIsADummyPath";
        analysisConfig.LocalSettings.Add(new Property(SonarProperties.VsCoverageXmlReportsPaths, coveragePathValue));
        analysisConfig.LocalSettings.Add(new Property(SonarProperties.VsTestReportsPaths, null));

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
        coveragePathValue.Should().Be(analysisConfig.GetSettingOrDefault(SonarProperties.VsCoverageXmlReportsPaths, true, null, testLogger));
    }

    [TestMethod]
    public void ProcessCoverageReports_VsTestReportsPathsProvided_SkipsSearching()
    {
        analysisConfig.LocalSettings.Add(new Property(SonarProperties.VsTestReportsPaths, "not null"));

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        sut.TrxFilesLocated.Should().BeFalse();
    }

    [TestMethod]
    public void ProcessCoverageReports_VsCoverageXmlPathProvided_CoverageXmlFileAlreadyPresent_DoesNotTryConverting()
    {
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", string.Empty);
        TestUtils.CreateTextFile(coverageDir, "dummy.coveragexml", string.Empty);
        analysisConfig.LocalSettings.Add(new Property(SonarProperties.VsCoverageXmlReportsPaths, "not null"));

        try
        {
            sut.ProcessCoverageReports(testLogger).Should().BeTrue();
            converter.AssertConvertNotCalled();
            testLogger.AssertWarningsLogged(0);
        }
        finally
        {
            TestUtils.DeleteTextFile(coverageDir, "dummy.coveragexml");
        }
    }

    [TestMethod]
    public void ProcessCoverageReports_NotVsCoverageXmlPathProvided_CoverageXmlFileAlreadyPresent_DoesNotTryConverting()
    {
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", string.Empty);
        TestUtils.CreateTextFile(coverageDir, "dummy.coveragexml", string.Empty);

        try
        {
            using (new AssertIgnoreScope())
            {
                sut.ProcessCoverageReports(testLogger).Should().BeTrue();
            }
            converter.AssertConvertNotCalled();
        }
        finally
        {
            TestUtils.DeleteTextFile(coverageDir, "dummy.coveragexml");
        }
    }

    [TestMethod]
    public void ProcessCoverageReports_NotVsCoverageXmlPathProvided_NotCoverageXmlFileAlreadyPresent_TriesConverting()
    {
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", string.Empty);

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertConvertCalledAtLeastOnce();
    }

    [TestMethod]
    public void ProcessCoverageReports_NotVsCoverageXmlPathProvided_NotCoverageXmlFileAlreadyPresent_ConversionFails()
    {
        TestUtils.CreateTextFile(testResultsDir, "dummy.trx", TrxPayload);
        TestUtils.CreateTextFile(coverageDir, "dummy.coverage", string.Empty);
        converter.ShouldNotFailConversion = false;

        sut.ProcessCoverageReports(testLogger).Should().BeTrue();
        converter.AssertExpectedNumberOfConversions(1);
    }
}
