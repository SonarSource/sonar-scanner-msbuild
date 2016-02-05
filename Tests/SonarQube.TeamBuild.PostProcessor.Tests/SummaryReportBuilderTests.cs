//-----------------------------------------------------------------------
// <copyright file="SummaryReportBuilderTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.PostProcessor;
using SonarRunner.Shim;
using SonarQube.Common;
using TestUtilities;
using SonarQube.TeamBuild.Integration;
using System.IO;

namespace SonarQube.TeamBuild.PostProcessorTests
{
    [TestClass]
    public class SummaryReportBuilderTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void SummaryReport_NoProjects()
        {
            // Arrange
            string hostUrl = "http://mySonarQube:9000";
            ProjectInfoAnalysisResult result = new ProjectInfoAnalysisResult { RanToCompletion = false };
            AnalysisConfig config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = hostUrl };

            // Act
            var summaryReportData = SummaryReportBuilder.CreateSummaryData(config, result);

            // Assert
            VerifySummaryReportData(summaryReportData, result, hostUrl, config);
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
            string hostUrl = "http://mySonarQube:9000";
            ProjectInfoAnalysisResult result = new ProjectInfoAnalysisResult { RanToCompletion = false };
            AnalysisConfig config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = hostUrl };
            config.LocalSettings = new AnalysisProperties();
            config.LocalSettings.Add(new Property() { Id = SonarProperties.ProjectBranch, Value = "master" });
            AddProjectInfoToResult(result, ProjectInfoValidity.Valid, type: ProjectType.Product, count: 4);

            // Act
            var summaryReportData = SummaryReportBuilder.CreateSummaryData(config, result);

            // Assert
            VerifySummaryReportData(summaryReportData, result, hostUrl, config);
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
            string hostUrl = "http://mySonarQube:9000";
            ProjectInfoAnalysisResult result = new ProjectInfoAnalysisResult() { RanToCompletion = true };
            AnalysisConfig config = new AnalysisConfig() { SonarProjectKey = "", SonarQubeHostUrl = hostUrl };

            AddProjectInfoToResult(result, ProjectInfoValidity.DuplicateGuid, count: 3);
            AddProjectInfoToResult(result, ProjectInfoValidity.ExcludeFlagSet, type: ProjectType.Product, count: 4);
            AddProjectInfoToResult(result, ProjectInfoValidity.ExcludeFlagSet, type: ProjectType.Test, count: 1);
            AddProjectInfoToResult(result, ProjectInfoValidity.InvalidGuid, type: ProjectType.Product, count: 7);
            AddProjectInfoToResult(result, ProjectInfoValidity.InvalidGuid, type: ProjectType.Test, count: 8);
            AddProjectInfoToResult(result, ProjectInfoValidity.NoFilesToAnalyze, count: 11);
            AddProjectInfoToResult(result, ProjectInfoValidity.Valid, type: ProjectType.Product, count: 13);
            AddProjectInfoToResult(result, ProjectInfoValidity.Valid, type: ProjectType.Test, count: 17);

            // Act
            var summaryReportData = SummaryReportBuilder.CreateSummaryData(config, result);

            // Assert
            VerifySummaryReportData(summaryReportData, result, hostUrl, config);
            VerifySummaryProjectCounts(
                summaryReportData,
                expectedExcludedProjects: 5, // ExcludeFlagSet
                expectedInvalidProjects: 18, // InvalidGuid + DuplicateGuid
                expectedSkippedProjects: 11, // No files to analyse
                expectedProductProjects: 13,
                expectedTestProjects: 17);
        }

        [TestMethod]
        public void SummaryReport_ReportIsGenerated()
        {
            // Arrange
            string hostUrl = "http://mySonarQube:9000";
            ProjectInfoAnalysisResult result = new ProjectInfoAnalysisResult();
            AnalysisConfig config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = hostUrl };

            TeamBuildSettings settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(this.TestContext.DeploymentDirectory);
            config.SonarOutputDir = TestContext.TestDeploymentDir; // this will be cleaned up by VS when there are too many results
            string expectedReportPath = Path.Combine(TestContext.TestDeploymentDir, SummaryReportBuilder.SummaryMdFilename);

            // Act
            SummaryReportBuilder builder = new SummaryReportBuilder();
            builder.GenerateReports(settings, config, result, new TestLogger());

            // Assert
            Assert.IsTrue(File.Exists(expectedReportPath) && (new FileInfo(expectedReportPath)).Length > 0, "The report file cannot be found or is empty");
        }

        private static void VerifySummaryReportData(
            SummaryReportBuilder.SummaryReportData summaryReportData,
            ProjectInfoAnalysisResult analysisResult,
            string expectedHostUrl,
            AnalysisConfig config)
        {
            string expectedUrl;
            string branch;

            config.GetAnalysisSettings(false).TryGetValue("sonar.branch", out branch);

            if (String.IsNullOrEmpty(branch))
            {
                expectedUrl = String.Format(
                    SummaryReportBuilder.DashboardUrlFormat, 
                    expectedHostUrl, 
                    config.SonarProjectKey);
            }
            else
            {
                expectedUrl = String.Format(
                    SummaryReportBuilder.DashboardUrlFormatWithBranch, 
                    expectedHostUrl, 
                    config.SonarProjectKey, 
                    branch);
            }

            Assert.AreEqual(expectedUrl, summaryReportData.DashboardUrl, "Invalid dashboard url");
            Assert.AreEqual(analysisResult.RanToCompletion, summaryReportData.Succeeded, "Invalid outcome");

        }

        private static void VerifySummaryProjectCounts(
            SummaryReportBuilder.SummaryReportData summaryReportData,
            int expectedInvalidProjects,
            int expectedProductProjects,
            int expectedSkippedProjects,
            int expectedTestProjects,
            int expectedExcludedProjects)
        {
            Assert.AreEqual(expectedInvalidProjects, summaryReportData.InvalidProjects);
            Assert.AreEqual(expectedProductProjects, summaryReportData.ProductProjects);
            Assert.AreEqual(expectedSkippedProjects, summaryReportData.SkippedProjects);
            Assert.AreEqual(expectedTestProjects, summaryReportData.TestProjects);
            Assert.AreEqual(expectedExcludedProjects, summaryReportData.ExcludedProjects);
        }

        private static void AddProjectInfoToResult(ProjectInfoAnalysisResult result, ProjectInfoValidity validity, ProjectType type = ProjectType.Product, uint count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                ProjectInfo pi = new ProjectInfo() { ProjectType = type };
                result.Projects[pi] = validity;
            }
        }

    }
}