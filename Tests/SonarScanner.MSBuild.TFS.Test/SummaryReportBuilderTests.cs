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

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.TFS;
using SonarScanner.MSBuild.Shim;
using SonarScanner.MSBuild.TFS.Tests.Infrastructure;
using TestUtilities;

namespace SonarScanner.MSBuild.TFS.Tests;

[TestClass]
public class SummaryReportBuilderTests
{
    public TestContext TestContext { get; set; }

    private ILogger testLogger;

    [TestInitialize]
    public void Initialize()
    {
        testLogger = new TestLogger();
    }

    [TestMethod]
    public void Ctor_FactoryIsNull_Throws()
    {
        // Act & Assert
        Action action = () => new SummaryReportBuilder(null, testLogger);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("legacyTeamBuildFactory");

        // Act & Assert
        action = () => new SummaryReportBuilder(Substitute.For<ILegacyTeamBuildFactory>(), null);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void CreateSummaryData_ConfigIsNull_Throws()
    {
        // Arrange
        var result = new ProjectInfoAnalysisResult { RanToCompletion = false };
        Action action = () => SummaryReportBuilder.CreateSummaryData(null, result, testLogger);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
    }

    [TestMethod]
    public void CreateSummaryData_ResultIsNull_Throws()
    {
        // Arrange
        var config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = "http://foo" };
        Action action = () => SummaryReportBuilder.CreateSummaryData(config, null, testLogger);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("result");
    }

    [TestMethod]
    public void SummaryReport_NoProjects()
    {
        // Arrange
        var hostUrl = "http://mySonarQube:9000";
        var result = new ProjectInfoAnalysisResult { RanToCompletion = false };
        var config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = hostUrl };

        // Act
        var summaryReportData = SummaryReportBuilder.CreateSummaryData(config, result, testLogger);

        // Assert
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
        // Arrange
        var hostUrl = "http://mySonarQube:9000";
        var result = new ProjectInfoAnalysisResult { RanToCompletion = false };
        var config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = hostUrl };
        config.LocalSettings = new AnalysisProperties { new Property(SonarProperties.ProjectBranch, "master") };
        AddProjectInfoToResult(result, ProjectInfoValidity.Valid, type: ProjectType.Product, count: 4);

        // Act
        var summaryReportData = SummaryReportBuilder.CreateSummaryData(config, result, testLogger);

        // Assert
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
        // Arrange
        var hostUrl = "http://mySonarQube:9000";
        var result = new ProjectInfoAnalysisResult() { RanToCompletion = true };
        var config = new AnalysisConfig() { SonarProjectKey = "", SonarQubeHostUrl = hostUrl };

        AddProjectInfoToResult(result, ProjectInfoValidity.ExcludeFlagSet, type: ProjectType.Product, count: 4);
        AddProjectInfoToResult(result, ProjectInfoValidity.ExcludeFlagSet, type: ProjectType.Test, count: 1);
        AddProjectInfoToResult(result, ProjectInfoValidity.InvalidGuid, type: ProjectType.Product, count: 7);
        AddProjectInfoToResult(result, ProjectInfoValidity.InvalidGuid, type: ProjectType.Test, count: 8);
        AddProjectInfoToResult(result, ProjectInfoValidity.NoFilesToAnalyze, count: 11);
        AddProjectInfoToResult(result, ProjectInfoValidity.Valid, type: ProjectType.Product, count: 13);
        AddProjectInfoToResult(result, ProjectInfoValidity.Valid, type: ProjectType.Test, count: 17);
        AddProjectInfoToResult(result, ProjectInfoValidity.DuplicateGuid, type: ProjectType.Product, count: 5);
        AddProjectInfoToResult(result, ProjectInfoValidity.DuplicateGuid, type: ProjectType.Test, count: 3);

        // Act
        var summaryReportData = SummaryReportBuilder.CreateSummaryData(config, result, testLogger);

        // Assert
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
        var result = new ProjectInfoAnalysisResult();
        var config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = hostUrl };

        // Arrange
        var settings = new MockBuildSettings
        {
            BuildEnvironment = BuildEnvironment.LegacyTeamBuild
        };

        var summaryLogger = new MockLegacyBuildSummaryLogger();
        var builder = new SummaryReportBuilder(
            new MockLegacyTeamBuildFactory(summaryLogger, null), new TestLogger());

        config.SonarOutputDir = TestContext.TestDeploymentDir; // this will be cleaned up by VS when there are too many results

        // Act
        builder.GenerateReports(settings, config, result.RanToCompletion, result.FullPropertiesFilePath, testLogger);

        // Assert
        summaryLogger.Messages[0].Should().Be("** WARNING: Support for XAML builds is deprecated since version 4.1 and will be removed in version 5.0 of the Scanner for MSBuild **");
    }

    private sealed class MockLegacyBuildSummaryLogger : ILegacyBuildSummaryLogger
    {
        public List<string> Messages { get; } = new List<string>();

        public void WriteMessage(string message, params object[] args)
        {
            Messages.Add(string.Format(message, args));
        }

        public void Dispose()
        {
            // Not needed for test
        }
    }

    private static void VerifySummaryReportData(
        SummaryReportBuilder.SummaryReportData summaryReportData,
        ProjectInfoAnalysisResult analysisResult,
        string expectedHostUrl,
        AnalysisConfig config,
        ILogger logger)
    {
        string expectedUrl;

        config.GetAnalysisSettings(false, logger).TryGetValue("sonar.branch", out string branch);

        if (string.IsNullOrEmpty(branch))
        {
            expectedUrl = string.Format(
                SummaryReportBuilder.DashboardUrlFormat,
                expectedHostUrl,
                config.SonarProjectKey);
        }
        else
        {
            expectedUrl = string.Format(
                SummaryReportBuilder.DashboardUrlFormatWithBranch,
                expectedHostUrl,
                config.SonarProjectKey,
                branch);
        }

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

    private static void AddProjectInfoToResult(ProjectInfoAnalysisResult result, ProjectInfoValidity validity, ProjectType type = ProjectType.Product, uint count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            result.Projects.Add(new ProjectData(new ProjectInfo { ProjectType = type }) { Status = validity });
        }
    }
}
