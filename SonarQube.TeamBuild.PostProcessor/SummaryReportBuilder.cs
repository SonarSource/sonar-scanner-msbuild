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
    internal class SummaryReportBuilder
    {
        private AnalysisConfig config;
        private ILogger logger;
        private ProjectInfoAnalysisResult result;
        private TeamBuildSettings settings;

        private SummaryReportBuilder(TeamBuildSettings settings, AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
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

            this.settings = settings;
            this.config = config;
            this.result = result;
            this.logger = logger;
        }

        /// <summary>
        /// Generates summary reports for LegacyTeamBuild and for Build Vnext
        /// </summary>
        public static void GenerateReports(TeamBuildSettings settings, AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
        {
            SummaryReportBuilder reportBuilder = new SummaryReportBuilder(settings, config, result, logger);
            reportBuilder.GenerateReports();
        }

        private void GenerateReports()
        {
            if (this.settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                UpdateTeamBuildSummary();
            }

            if (this.settings.BuildEnvironment == BuildEnvironment.TeamBuild)
            {
                CreateBuildVnextSummaryMdFile();
            }
        }

        private void CreateBuildVnextSummaryMdFile()
        {
            this.logger.LogMessage(Resources.Report_CreatingSummaryMarkdown);

            int skippedProjectCount, invalidProjectCount, excludedProjectCount, productProjectCount, testProjectCount;
            GetProjectCountByType(out skippedProjectCount, out invalidProjectCount, out excludedProjectCount, out productProjectCount, out testProjectCount);

            Debug.Assert(!String.IsNullOrEmpty(this.config.SonarOutputDir), "Could not find the output directory");
            string summaryMdPath = Path.Combine(this.config.SonarOutputDir, "summary.md");

            using (StreamWriter sw = new StreamWriter(summaryMdPath, append: false))
            {
                string projectDescription = GetProjectDescription();

                sw.WriteLine(Resources.Report_MdSummaryTitle);

                if (this.config.SonarRunnerPropertiesPath != null && this.result.RanToCompletion)
                {
                    string sonarUrl = GetSonarDashboadUrl();
                    sw.WriteLine(Resources.Report_MdSummaryAnalysisSucceeded, projectDescription, sonarUrl);
                }

                if (!this.result.RanToCompletion)
                {
                    sw.WriteLine(Resources.Report_MdSummaryAnalysisFailed, projectDescription);
                }

                sw.WriteLine(Resources.Report_MdSummaryProductAndTestMessage, productProjectCount, testProjectCount);
                sw.WriteLine(Resources.Report_MdSummaryInvalidSkippedAndExcludedMessage, invalidProjectCount, skippedProjectCount, excludedProjectCount);
            }
        }

        private void UpdateTeamBuildSummary()
        {
            this.logger.LogMessage(Resources.Report_UpdatingTeamBuildSummary);

            int skippedProjectCount, invalidProjectCount, excludedProjectCount, productProjectCount, testProjectCount;
            GetProjectCountByType(out skippedProjectCount, out invalidProjectCount, out excludedProjectCount, out productProjectCount, out testProjectCount);

            using (BuildSummaryLogger summaryLogger = new BuildSummaryLogger(this.config.GetTfsUri(), this.config.GetBuildUri()))
            {
                string projectDescription = GetProjectDescription();

                // Add a link to SonarQube dashboard if analysis succeeded
                Debug.Assert(this.config.SonarRunnerPropertiesPath != null, "Not expecting the sonar-runner properties path to be null");
                if (this.config.SonarRunnerPropertiesPath != null && this.result.RanToCompletion)
                {
                    string sonarUrl = GetSonarDashboadUrl();
                    summaryLogger.WriteMessage(Resources.Report_AnalysisSucceeded, projectDescription, sonarUrl);
                }

                if (!this.result.RanToCompletion)
                {
                    summaryLogger.WriteMessage(Resources.Report_AnalysisFailed, projectDescription);
                }

                summaryLogger.WriteMessage(Resources.Report_ProductAndTestMessage, productProjectCount, testProjectCount);
                summaryLogger.WriteMessage(Resources.Report_InvalidSkippedAndExcludedMessage, invalidProjectCount, skippedProjectCount, excludedProjectCount);
            }
        }

        private string GetProjectDescription()
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture,
                Resources.Report_SonarQubeProjectDescription, this.config.SonarProjectName, this.config.SonarProjectKey, this.config.SonarProjectVersion);
        }

        private void GetProjectCountByType(out int skippedProjectCount, out int invalidProjectCount, out int excludedProjectCount, out int productProjectCount, out int testProjectCount)
        {
            skippedProjectCount = GetProjectsByStatus(this.result, ProjectInfoValidity.NoFilesToAnalyze).Count();
            invalidProjectCount = GetProjectsByStatus(this.result, ProjectInfoValidity.InvalidGuid).Count();
            invalidProjectCount += GetProjectsByStatus(this.result, ProjectInfoValidity.DuplicateGuid).Count();

            excludedProjectCount = GetProjectsByStatus(this.result, ProjectInfoValidity.ExcludeFlagSet).Count();
            IEnumerable<ProjectInfo> validProjects = GetProjectsByStatus(this.result, ProjectInfoValidity.Valid);
            productProjectCount = validProjects.Count(p => p.ProjectType == ProjectType.Product);
            testProjectCount = validProjects.Count(p => p.ProjectType == ProjectType.Test);
        }

        private string GetSonarDashboadUrl()
        {
            ISonarPropertyProvider propertyProvider = new FilePropertiesProvider(this.config.SonarRunnerPropertiesPath);
            string hostUrl = propertyProvider.GetProperty(SonarProperties.HostUrl).TrimEnd('/');

            string sonarUrl = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0}/dashboard/index/{1}", hostUrl, config.SonarProjectKey);
            return sonarUrl;
        }

        private static IEnumerable<ProjectInfo> GetProjectsByStatus(ProjectInfoAnalysisResult result, ProjectInfoValidity status)
        {
            return result.Projects.Where(p => p.Value == status).Select(p => p.Key);
        }
    }
}