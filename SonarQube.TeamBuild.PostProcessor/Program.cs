//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarRunner.Shim;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SonarQube.TeamBuild.PostProcessor
{
    internal class Program
    {
        private const int SuccessCode = 0;
        private const int ErrorCode = 1;

        static int Main()
        {
            ILogger logger = new ConsoleLogger(includeTimestamp: true);

            TeamBuildSettings settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            Debug.Assert(settings != null, "Settings should not be null");

            AnalysisConfig config = GetAnalysisConfig(settings, logger);
            if (config == null)
            {
                logger.LogError(Resources.ERROR_MissingSettings);
                return ErrorCode;
            }

            if (!CheckEnvironmentConsistency(config, settings, logger))
            {
                return ErrorCode;
            }

            ICoverageReportProcessor coverageReportProcessor = TryCreateCoverageReportProcessor(settings);

            // Handle code coverage reports
            if (!coverageReportProcessor.ProcessCoverageReports(config, settings, logger))
            {
                return ErrorCode;
            }
            ProjectInfoAnalysisResult result = InvokeSonarRunner(config, logger);

            // Write summary report
            if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                UpdateTeamBuildSummary(config, result, logger);
            }

            return result.RanToCompletion ? SuccessCode : ErrorCode;
        }

        /// <summary>
        /// Attempts to load the analysis config file. The location of the file is
        /// calculated from TeamBuild-specific environment variables.
        /// Returns null if the required environment variables are not available.
        /// </summary>
        private static AnalysisConfig GetAnalysisConfig(TeamBuildSettings teamBuildSettings, ILogger logger)
        {
            AnalysisConfig config = null;

            if (teamBuildSettings != null)
            {
                string configFilePath = teamBuildSettings.AnalysisConfigFilePath;
                Debug.Assert(!string.IsNullOrWhiteSpace(configFilePath), "Expecting the analysis config file path to be set");

                if (File.Exists(configFilePath))
                {
                    logger.LogMessage(Resources.DIAG_LoadingConfig, configFilePath);
                    config = AnalysisConfig.Load(teamBuildSettings.AnalysisConfigFilePath);
                }
                else
                {
                    logger.LogError(Resources.ERROR_ConfigFileNotFound, configFilePath);
                }
            }
            return config;
        }

        /// <summary>
        /// Returns a boolean indicating whether the information in the environment variables
        /// matches that in the analysis config file.
        /// Used to detect invalid setups on the build agent.
        /// </summary>
        private static bool CheckEnvironmentConsistency(AnalysisConfig config, TeamBuildSettings settings, ILogger logger)
        {
            // Currently we're only checking that the build uris match as this is the most likely error
            // - it probably means that an old analysis config file has been left behind somehow 
            // e.g. a build definition used to include analysis but has changed so that it is no 
            // longer an analysis build, but there is still an old analysis config on disc.

            if (settings.BuildEnvironment == BuildEnvironment.NotTeamBuild)
            {
                return true;
            }

            string configUri = config.GetBuildUri();
            string environmentUi = settings.BuildUri;

            if (!string.Equals(configUri, environmentUi, System.StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError(Resources.ERROR_BuildUrisDontMatch, environmentUi, configUri, settings.AnalysisConfigFilePath);
                return false;
            }

            return true;
        }

        private static ProjectInfoAnalysisResult InvokeSonarRunner(AnalysisConfig config, ILogger logger)
        {
            ISonarRunner runner = new SonarRunnerWrapper();
            ProjectInfoAnalysisResult result = runner.Execute(config, logger);
            return result;
        }

        private static void UpdateTeamBuildSummary(AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
        {
            logger.LogMessage(Resources.Report_UpdatingTeamBuildSummary);

            int skippedProjectCount = GetProjectsByStatus(result, ProjectInfoValidity.NoFilesToAnalyze).Count();
            int invalidProjectCount = GetProjectsByStatus(result, ProjectInfoValidity.InvalidGuid).Count();
            invalidProjectCount += GetProjectsByStatus(result, ProjectInfoValidity.DuplicateGuid).Count();

            int excludedProjectCount = GetProjectsByStatus(result, ProjectInfoValidity.ExcludeFlagSet).Count();

            IEnumerable<ProjectInfo> validProjects = GetProjectsByStatus(result, ProjectInfoValidity.Valid);
            int productProjectCount = validProjects.Count(p => p.ProjectType == ProjectType.Product);
            int testProjectCount = validProjects.Count(p => p.ProjectType == ProjectType.Test);

            using (BuildSummaryLogger summaryLogger = new BuildSummaryLogger(config.GetTfsUri(), config.GetBuildUri()))
            {
                string projectDescription = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    Resources.Report_SonarQubeProjectDescription, config.SonarProjectName, config.SonarProjectKey, config.SonarProjectVersion);

                // Add a link to SonarQube dashboard if analysis succeeded
                Debug.Assert(config.SonarRunnerPropertiesPath != null, "Not expecting the sonar-runner properties path to be null");
                if (config.SonarRunnerPropertiesPath != null && result.RanToCompletion)
                {
                    ISonarPropertyProvider propertyProvider = new FilePropertiesProvider(config.SonarRunnerPropertiesPath);
                    string hostUrl = propertyProvider.GetProperty(SonarProperties.HostUrl).TrimEnd('/');

                    string sonarUrl = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0}/dashboard/index/{1}", hostUrl, config.SonarProjectKey);

                    summaryLogger.WriteMessage(Resources.Report_AnalysisSucceeded, projectDescription, sonarUrl);
                }

                if (!result.RanToCompletion)
                {
                    summaryLogger.WriteMessage(Resources.Report_AnalysisFailed, projectDescription);
                }

                summaryLogger.WriteMessage(Resources.Report_ProductAndTestMessage, productProjectCount, testProjectCount);
                summaryLogger.WriteMessage(Resources.Report_InvalidSkippedAndExcludedMessage, invalidProjectCount, skippedProjectCount, excludedProjectCount);
            }

        }

        private static IEnumerable<ProjectInfo> GetProjectsByStatus(ProjectInfoAnalysisResult result, ProjectInfoValidity status)
        {
            return result.Projects.Where(p => p.Value == status).Select(p => p.Key);
        }

        /// <summary>
        /// Factory method to create a coverage report processor for the current build environment.
        /// TODO: replace with a general purpose pre- and post- processing extension mechanism.
        /// </summary>
        private static ICoverageReportProcessor TryCreateCoverageReportProcessor(TeamBuildSettings settings)
        {
            ICoverageReportProcessor processor = null;

            if (settings.BuildEnvironment == BuildEnvironment.TeamBuild)
            {
                processor = new BuildVNextCoverageReportProcessor();
            }
            else if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                processor = new TfsLegacyCoverageReportProcessor();
            }
            return processor;
        }
    }
}
