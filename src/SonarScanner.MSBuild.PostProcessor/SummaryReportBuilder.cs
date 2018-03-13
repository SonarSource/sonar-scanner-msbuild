/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.TFS;
using SonarScanner.MSBuild.TFS.Interfaces;
using SonarScanner.MSBuild.Shim;

namespace SonarScanner.MSBuild.PostProcessor
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

        private readonly ILegacyTeamBuildFactory legacyTeamBuildFactory;

        public SummaryReportBuilder(ILegacyTeamBuildFactory legacyTeamBuildFactory)
        {
            this.legacyTeamBuildFactory
                = legacyTeamBuildFactory ?? throw new ArgumentNullException(nameof(legacyTeamBuildFactory));
        }

        #region IReportBuilder interface methods

        /// <summary>
        /// Generates summary reports for LegacyTeamBuild and for Build Vnext
        /// </summary>
        public void GenerateReports(ITeamBuildSettings settings, AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.result = result ?? throw new ArgumentNullException(nameof(result));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            GenerateReports();
        }

        #endregion IReportBuilder interface methods

        private void GenerateReports()
        {
            var summaryData = CreateSummaryData(config, result);

            if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                UpdateLegacyTeamBuildSummary(summaryData);
            }

            CreateSummaryMdFile(summaryData);

            // Write the dashboard link to the output. The sonar-scanner will have written it out earlier,
            // but writing it again here puts it very close to the end of the output - easier to find,
            // especially when running from the command line.
            if (result.RanToCompletion)
            {
                logger.LogInfo(Resources.Report_LinkToDashboard, summaryData.DashboardUrl);
            }
        }

        public /* for test purposes */ static SummaryReportData CreateSummaryData(
            AnalysisConfig config,
            ProjectInfoAnalysisResult result)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var validProjects = result.GetProjectsByStatus(ProjectInfoValidity.Valid);

            var summaryData = new SummaryReportData
            {
                SkippedProjects = result.Projects.Count(p => p.Status == ProjectInfoValidity.NoFilesToAnalyze),
                InvalidProjects = result.Projects.Count(p => p.Status == ProjectInfoValidity.InvalidGuid || p.Status == ProjectInfoValidity.DuplicateGuid),
                ExcludedProjects = result.Projects.Count(p => p.Status == ProjectInfoValidity.ExcludeFlagSet),
                ProductProjects = validProjects.Count(p => p.ProjectType == ProjectType.Product),
                TestProjects = validProjects.Count(p => p.ProjectType == ProjectType.Test),
                Succeeded = result.RanToCompletion,
                DashboardUrl = GetSonarDashboadUrl(config),
                ProjectDescription = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    Resources.Report_SonarQubeProjectDescription, config.SonarProjectName,
                    config.SonarProjectKey, config.SonarProjectVersion)
            };
            return summaryData;
        }

        private static string GetSonarDashboadUrl(AnalysisConfig config)
        {
            var hostUrl = config.SonarQubeHostUrl.TrimEnd('/');
            var branch = FindBranch(config);

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
            var localSettings = config.GetAnalysisSettings(includeServerSettings: false);
            Debug.Assert(localSettings != null);

            localSettings.TryGetValue(SonarProperties.ProjectBranch, out string branch);

            return branch;
        }

        private void CreateSummaryMdFile(SummaryReportData summaryData)
        {
            logger.LogInfo(Resources.Report_CreatingSummaryMarkdown);

            Debug.Assert(!string.IsNullOrEmpty(config.SonarOutputDir), "Could not find the output directory");
            var summaryMdPath = Path.Combine(config.SonarOutputDir, SummaryMdFilename);

            using (var sw = new StreamWriter(summaryMdPath, append: false))
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
            logger.LogInfo(Resources.Report_UpdatingTeamBuildSummary);

            using (var summaryLogger = legacyTeamBuildFactory.BuildLegacyBuildSummaryLogger(config.GetTfsUri(), config.GetBuildUri()))
            {
                summaryLogger.WriteMessage(Resources.WARN_XamlBuildDeprecated);

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
