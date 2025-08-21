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

using SonarScanner.MSBuild.Common.TFS;
using SonarScanner.MSBuild.Shim;
using SonarScanner.MSBuild.TFS.Test.Infrastructure;

namespace SonarScanner.MSBuild.TFS.Test;

[TestClass]
public class SummaryReportBuilderTests
{
    private ILogger testLogger;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Initialize() =>
        testLogger = new TestLogger();

    [TestMethod]
    public void Ctor_FactoryIsNull_Throws()
    {
        FluentActions.Invoking(() => new SummaryReportBuilder(null, testLogger)).Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("legacyTeamBuildFactory");
        FluentActions.Invoking(() => new SummaryReportBuilder(Substitute.For<ILegacyTeamBuildFactory>(), null)).Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void SummaryReportData_ConfigIsNull_Throws()
    {
        var result = new ProjectInfoAnalysisResult([]) { RanToCompletion = false };
        FluentActions.Invoking(() => new SummaryReportBuilder.SummaryReportData(null, result, testLogger)).Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
    }

    [TestMethod]
    public void SummaryReportData_ResultIsNull_Throws()
    {
        var config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = "http://foo" };
        FluentActions.Invoking(() => new SummaryReportBuilder.SummaryReportData(config, null, testLogger)).Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("result");
    }

    [TestMethod]
    public void SummaryReport_NoProjects()
    {
        var hostUrl = "http://mySonarQube:9000";
        var result = new ProjectInfoAnalysisResult([]) { RanToCompletion = false };
        var config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = hostUrl };
        var summaryReportData = new SummaryReportBuilder.SummaryReportData(config, result, testLogger);

        VerifySummaryReportData(summaryReportData, result, hostUrl, config, new TestLogger());
        VerifySummaryProjectCounts(
            summaryReportData,
            expectedExcludedProjects: 0,
            expectedInvalidProjects: 0,
            expectedSkippedProjects: 0,
            expectedProductProjects: 0,
            expectedTestProjects: 0);
    }

    [TestMethod]
    public void SummaryReport_WithBranch()
    {
        var hostUrl = "http://mySonarQube:9000";
        var result = new ProjectInfoAnalysisResult(CreateProjects(ProjectInfoValidity.Valid, ProjectType.Product, 4).ToArray()) { RanToCompletion = false };
        var config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = hostUrl };
        config.LocalSettings = new AnalysisProperties { new Property(SonarProperties.ProjectBranch, "master") };
        var summaryReportData = new SummaryReportBuilder.SummaryReportData(config, result, testLogger);

        VerifySummaryReportData(summaryReportData, result, hostUrl, config, testLogger);
        VerifySummaryProjectCounts(
            summaryReportData,
            expectedExcludedProjects: 0,
            expectedInvalidProjects: 0,
            expectedSkippedProjects: 0,
            expectedProductProjects: 4,
            expectedTestProjects: 0);
    }

    [TestMethod]
    public void SummaryReport_AllTypesOfProjects()
    {
        var hostUrl = "http://mySonarQube:9000";
        var projects = CreateProjects(ProjectInfoValidity.ExcludeFlagSet, ProjectType.Product, 4)
            .Concat(CreateProjects(ProjectInfoValidity.ExcludeFlagSet, ProjectType.Test, 1))
            .Concat(CreateProjects(ProjectInfoValidity.InvalidGuid, ProjectType.Product, 7))
            .Concat(CreateProjects(ProjectInfoValidity.InvalidGuid, ProjectType.Test, 8))
            .Concat(CreateProjects(ProjectInfoValidity.NoFilesToAnalyze, ProjectType.Product, 11))
            .Concat(CreateProjects(ProjectInfoValidity.Valid, ProjectType.Product, 13))
            .Concat(CreateProjects(ProjectInfoValidity.Valid, ProjectType.Test, 17))
            .Concat(CreateProjects(ProjectInfoValidity.DuplicateGuid, ProjectType.Product, 5))
            .Concat(CreateProjects(ProjectInfoValidity.DuplicateGuid, ProjectType.Test, 3));
        var result = new ProjectInfoAnalysisResult(projects.ToArray()) { RanToCompletion = true };
        var config = new AnalysisConfig() { SonarProjectKey = string.Empty, SonarQubeHostUrl = hostUrl };
        var summaryReportData = new SummaryReportBuilder.SummaryReportData(config, result, testLogger);

        VerifySummaryReportData(summaryReportData, result, hostUrl, config, testLogger);
        VerifySummaryProjectCounts(
            summaryReportData,
            expectedExcludedProjects: 5, // ExcludeFlagSet
            expectedInvalidProjects: 23, // InvalidGuid, DuplicateGuid
            expectedSkippedProjects: 11, // No files to analyze
            expectedProductProjects: 13,
            expectedTestProjects: 17);
    }

    [TestMethod]
    public void Deprecated_Warning_Logged_On_XAML_Build()
    {
        var hostUrl = "http://mySonarQube:9000";
        var result = new ProjectInfoAnalysisResult([]);
        var config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = hostUrl };
        var settings = new MockBuildSettings
        {
            BuildEnvironment = BuildEnvironment.LegacyTeamBuild
        };
        var summaryLogger = new MockLegacyBuildSummaryLogger();
        var builder = new SummaryReportBuilder(new MockLegacyTeamBuildFactory(summaryLogger, null), new TestLogger());
        config.SonarOutputDir = TestContext.TestDeploymentDir; // this will be cleaned up by VS when there are too many results
        builder.GenerateReports(settings, config, result.RanToCompletion, result.FullPropertiesFilePath);

        summaryLogger.Messages[0].Should().Be("** WARNING: Support for XAML builds is deprecated since version 4.1 and will be removed in version 5.0 of the Scanner for .NET **");
    }

    private static void VerifySummaryReportData(
        SummaryReportBuilder.SummaryReportData summaryReportData,
        ProjectInfoAnalysisResult analysisResult,
        string expectedHostUrl,
        AnalysisConfig config,
        ILogger logger)
    {
        string expectedUrl;

        config.AnalysisSettings(false, logger).TryGetValue("sonar.branch", out string branch);

        expectedUrl = string.IsNullOrEmpty(branch)
            ? $"{expectedHostUrl}/dashboard/index/{config.SonarProjectKey}"
            : $"{expectedHostUrl}/dashboard/index/{config.SonarProjectKey}:{branch}";

        summaryReportData.DashboardUrl.Should().Be(expectedUrl, "Invalid dashboard url");
        summaryReportData.Succeeded.Should().Be(analysisResult.RanToCompletion, "Invalid outcome");
    }

    private static void VerifySummaryProjectCounts(
        SummaryReportBuilder.SummaryReportData summaryReportData,
        int expectedInvalidProjects,
        int expectedProductProjects,
        int expectedSkippedProjects,
        int expectedTestProjects,
        int expectedExcludedProjects)
    {
        summaryReportData.InvalidProjects.Should().Be(expectedInvalidProjects);
        summaryReportData.ProductProjects.Should().Be(expectedProductProjects);
        summaryReportData.SkippedProjects.Should().Be(expectedSkippedProjects);
        summaryReportData.TestProjects.Should().Be(expectedTestProjects);
        summaryReportData.ExcludedProjects.Should().Be(expectedExcludedProjects);
    }

    private static IEnumerable<ProjectData> CreateProjects(ProjectInfoValidity validity, ProjectType type, uint count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return new ProjectData(new ProjectInfo { ProjectType = type }) { Status = validity };
        }
    }

    private sealed class MockLegacyBuildSummaryLogger : ILegacyBuildSummaryLogger
    {
        public List<string> Messages { get; } = [];

        public void WriteMessage(string message, params object[] args) =>
            Messages.Add(string.Format(message, args));

        public void Dispose() { }            // Not needed for test
    }
}
