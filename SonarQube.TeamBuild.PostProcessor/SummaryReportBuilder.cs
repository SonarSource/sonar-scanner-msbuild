//-----------------------------------------------------------------------
// <copyright file="SummaryReportBuilder.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarRunner.Shim;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SonarQube.TeamBuild.PostProcessor
{
    /// <summary>
    /// Generates summary reports for various build systems
    /// </summary>
    public class SummaryReportBuilder
    {
        public class SummaryReportData
        {
            public int ProductProjects { get; set; }
            public int TestProjects { get; set; }
            public int InvalidProjects { get; set; }
            public int SkippedProjects { get; set; }
            public int ExcludedProjects { get; set; }
            public bool Succeeded { get; set; }
            public string DashboardUrl { get; set; }
            public string ProjectDescription { get; set; }
        }

        public /* for test purposes */ const string DashboardUrlFormat= "{0}/dashboard/index/{1}";
        public /* for test purposes */ const string SummaryMdFilename = "summary.md";

        private AnalysisConfig config;
        private ILogger logger;
        private ProjectInfoAnalysisResult result;
        private TeamBuildSettings settings;
        private ISonarPropertyProvider sonarPropertyProvider;

        private SummaryReportBuilder(TeamBuildSettings settings, AnalysisConfig config, ProjectInfoAnalysisResult result, ISonarPropertyProvider sonarPropertyProvider, ILogger logger)
        {
            this.settings = settings;
            this.config = config;
            this.result = result;
            this.logger = logger;
            this.sonarPropertyProvider = sonarPropertyProvider;
        }

        /// <summary>
        /// Generates summary reports for LegacyTeamBuild and for Build Vnext
        /// </summary>
        public static void GenerateReports(TeamBuildSettings settings, AnalysisConfig config, ProjectInfoAnalysisResult result, ISonarPropertyProvider sonarPropertyProvider, ILogger logger)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            if (sonarPropertyProvider == null)
            {
                throw new ArgumentNullException(nameof(sonarPropertyProvider));
            }

            SummaryReportBuilder reportBuilder = new SummaryReportBuilder(settings, config, result, sonarPropertyProvider, logger);
            reportBuilder.GenerateReports();
        }

        private void GenerateReports()
        {
            SummaryReportData summaryData = CreateSummaryData(this.config, this.result, this.sonarPropertyProvider);

            if (this.settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                UpdateLegacyTeamBuildSummary(summaryData);
            }

            CreateSummaryMdFile(summaryData);
        }

        public /* for test purposes */ static SummaryReportData CreateSummaryData(
            AnalysisConfig config,
            ProjectInfoAnalysisResult result, 
            ISonarPropertyProvider sonarPropertyProvider)
        {
            SummaryReportData summaryData = new SummaryReportData();

            summaryData.SkippedProjects = GetProjectsByStatus(result, ProjectInfoValidity.NoFilesToAnalyze).Count();
            summaryData.InvalidProjects = GetProjectsByStatus(result, ProjectInfoValidity.InvalidGuid).Count();
            summaryData.InvalidProjects += GetProjectsByStatus(result, ProjectInfoValidity.DuplicateGuid).Count();

            summaryData.ExcludedProjects = GetProjectsByStatus(result, ProjectInfoValidity.ExcludeFlagSet).Count();
            IEnumerable<ProjectInfo> validProjects = GetProjectsByStatus(result, ProjectInfoValidity.Valid);
            summaryData.ProductProjects = validProjects.Count(p => p.ProjectType == ProjectType.Product);
            summaryData.TestProjects = validProjects.Count(p => p.ProjectType == ProjectType.Test);

            summaryData.Succeeded = result.RanToCompletion;

            summaryData.DashboardUrl = GetSonarDashboadUrl(config, sonarPropertyProvider);
            summaryData.ProjectDescription = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                Resources.Report_SonarQubeProjectDescription, config.SonarProjectName, config.SonarProjectKey, config.SonarProjectVersion);
            return summaryData;

        }

        private static string GetSonarDashboadUrl(AnalysisConfig config, ISonarPropertyProvider propertyProvider)
        {
            string hostUrl = propertyProvider.GetProperty(SonarProperties.HostUrl).TrimEnd('/');

            string sonarUrl = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                DashboardUrlFormat, hostUrl, config.SonarProjectKey);
            return sonarUrl;
        }

        private static IEnumerable<ProjectInfo> GetProjectsByStatus(ProjectInfoAnalysisResult result, ProjectInfoValidity status)
        {
            return result.Projects.Where(p => p.Value == status).Select(p => p.Key);
        }

        private void CreateSummaryMdFile(SummaryReportData summaryData)
        {
            this.logger.LogMessage(Resources.Report_CreatingSummaryMarkdown);

            Debug.Assert(!String.IsNullOrEmpty(this.config.SonarOutputDir), "Could not find the output directory");
            string summaryMdPath = Path.Combine(this.config.SonarOutputDir, SummaryMdFilename);

            using (StreamWriter sw = new StreamWriter(summaryMdPath, append: false))
            {
                sw.WriteLine(Resources.Report_MdSummaryTitle);

                if (summaryData.Succeeded)
                {
                    sw.WriteLine(Resources.Report_MdSummaryAnalysisSucceeded, summaryData.ProjectDescription, summaryData.DashboardUrl);
                }
                else
                {
                    sw.WriteLine(Resources.Report_MdSummaryAnalysisFailed, summaryData.ProjectDescription);
                }

                sw.WriteLine(Resources.Report_MdSummaryProductAndTestMessage, summaryData.ProductProjects, summaryData.TestProjects);
                sw.WriteLine(Resources.Report_MdSummaryInvalidSkippedAndExcludedMessage, summaryData.InvalidProjects, summaryData.SkippedProjects, summaryData.ExcludedProjects);
            }
        }

        private void UpdateLegacyTeamBuildSummary(SummaryReportData summaryData)
        {
            this.logger.LogMessage(Resources.Report_UpdatingTeamBuildSummary);

            using (BuildSummaryLogger summaryLogger = new BuildSummaryLogger(this.config.GetTfsUri(), this.config.GetBuildUri()))
            {
                // Add a link to SonarQube dashboard if analysis succeeded
                if (summaryData.Succeeded)
                {
                    summaryLogger.WriteMessage(Resources.Report_AnalysisSucceeded, summaryData.ProjectDescription, summaryData.DashboardUrl);
                }
                else
                {
                    summaryLogger.WriteMessage(Resources.Report_AnalysisFailed, summaryData.ProjectDescription);
                }

                summaryLogger.WriteMessage(Resources.Report_ProductAndTestMessage, summaryData.ProductProjects, summaryData.TestProjects);
                summaryLogger.WriteMessage(Resources.Report_InvalidSkippedAndExcludedMessage, summaryData.InvalidProjects, summaryData.SkippedProjects, summaryData.ExcludedProjects);
            }
        }

    }
}
