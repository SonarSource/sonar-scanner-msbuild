/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
 
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.Integration.Interfaces;
using SonarScanner.Shim;
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
    public class SummaryReportBuilder : ISummaryReportBuilder
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

        public /* for test purposes */ const string DashboardUrlFormat = "{0}/dashboard/index/{1}";
        public /* for test purposes */ const string DashboardUrlFormatWithBranch = "{0}/dashboard/index/{1}:{2}";
        public /* for test purposes */ const string SummaryMdFilename = "summary.md";

        private AnalysisConfig config;
        private ILogger logger;
        private ProjectInfoAnalysisResult result;
        private ITeamBuildSettings settings;

        #region IReportBuilder interface methods

        /// <summary>
        /// Generates summary reports for LegacyTeamBuild and for Build Vnext
        /// </summary>
        public void GenerateReports(ITeamBuildSettings settings, AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            this.settings = settings;
            this.config = config;
            this.result = result;
            this.logger = logger;

            this.GenerateReports();
        }

        #endregion

        private void GenerateReports()
        {
            SummaryReportData summaryData = CreateSummaryData(this.config, this.result);

            if (this.settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                UpdateLegacyTeamBuildSummary(summaryData);
            }

            CreateSummaryMdFile(summaryData);

            // Write the dashboard link to the output. The sonar-scanner will have written it out earlier,
            // but writing it again here puts it very close to the end of the output - easier to find,
            // especially when running from the command line.
            if (this.result.RanToCompletion)
            {
                this.logger.LogInfo(Resources.Report_LinkToDashboard, summaryData.DashboardUrl);
            }
        }

        public /* for test purposes */ static SummaryReportData CreateSummaryData(
            AnalysisConfig config,
            ProjectInfoAnalysisResult result)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }


            SummaryReportData summaryData = new SummaryReportData();

            summaryData.SkippedProjects = GetProjectsByStatus(result, ProjectInfoValidity.NoFilesToAnalyze).Count();
            summaryData.InvalidProjects = GetProjectsByStatus(result, ProjectInfoValidity.InvalidGuid).Count();
            summaryData.InvalidProjects += GetProjectsByStatus(result, ProjectInfoValidity.DuplicateGuid).Count();

            summaryData.ExcludedProjects = GetProjectsByStatus(result, ProjectInfoValidity.ExcludeFlagSet).Count();
            IEnumerable<ProjectInfo> validProjects = GetProjectsByStatus(result, ProjectInfoValidity.Valid);
            summaryData.ProductProjects = validProjects.Count(p => p.ProjectType == ProjectType.Product);
            summaryData.TestProjects = validProjects.Count(p => p.ProjectType == ProjectType.Test);

            summaryData.Succeeded = result.RanToCompletion;

            summaryData.DashboardUrl = GetSonarDashboadUrl(config);
            summaryData.ProjectDescription = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                Resources.Report_SonarQubeProjectDescription, config.SonarProjectName, config.SonarProjectKey, config.SonarProjectVersion);
            return summaryData;

        }

        private static string GetSonarDashboadUrl(AnalysisConfig config)
        {
            string hostUrl = config.SonarQubeHostUrl.TrimEnd('/');
            string branch = FindBranch(config);

            string sonarUrl;

            if (string.IsNullOrWhiteSpace(branch))
            {
                sonarUrl = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    DashboardUrlFormat,
                    hostUrl,
                    config.SonarProjectKey);
            }
            else
            {
                sonarUrl = string.Format(
                   System.Globalization.CultureInfo.InvariantCulture,
                   DashboardUrlFormatWithBranch,
                   hostUrl,
                   config.SonarProjectKey,
                   branch);
            }

            return sonarUrl;
        }

        private static string FindBranch(AnalysisConfig config)
        {
            IAnalysisPropertyProvider localSettings = config.GetAnalysisSettings(includeServerSettings: false);
            Debug.Assert(localSettings != null);

            string branch;
            localSettings.TryGetValue(SonarProperties.ProjectBranch, out branch);

            return branch;
        }

        private static IEnumerable<ProjectInfo> GetProjectsByStatus(ProjectInfoAnalysisResult result, ProjectInfoValidity status)
        {
            return result.Projects.Where(p => p.Value == status).Select(p => p.Key);
        }

        private void CreateSummaryMdFile(SummaryReportData summaryData)
        {
            this.logger.LogInfo(Resources.Report_CreatingSummaryMarkdown);

            Debug.Assert(!string.IsNullOrEmpty(this.config.SonarOutputDir), "Could not find the output directory");
            string summaryMdPath = Path.Combine(this.config.SonarOutputDir, SummaryMdFilename);

            using (StreamWriter sw = new StreamWriter(summaryMdPath, append: false))
            {
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
            this.logger.LogInfo(Resources.Report_UpdatingTeamBuildSummary);

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
