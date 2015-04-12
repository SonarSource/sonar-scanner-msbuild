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

        static int Main(string[] args)
        {
            ILogger logger = new ConsoleLogger(includeTimestamp: true);

            // TODO: consider using command line arguments if supplied
            AnalysisConfig config = GetAnalysisConfig(logger);
            if (config == null)
            {
                logger.LogError(Resources.ERROR_MissingSettings);
                return ErrorCode;
            }

            TeamBuildSettings settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);

            if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                // Handle code coverage reports
                CoverageReportProcessor coverageProcessor = new CoverageReportProcessor();
                if (!coverageProcessor.ProcessCoverageReports(config, logger))
                {
                    return ErrorCode;
                }
            }

            ProjectInfoAnalysisResult result = InvokeSonarRunner(config, logger);
            
            // Write summary report
            if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                WriteTeamBuildSummaryReport(config, result, logger);
            }

            return result.RanToCompletion ? SuccessCode : ErrorCode;
        }

        /// <summary>
        /// Attempts to load the analysis config file. The location of the file is
        /// calculated from TeamBuild-specific environment variables.
        /// Returns null if the required environment variables are not available.
        /// </summary>
        private static AnalysisConfig GetAnalysisConfig(ILogger logger)
        {
            AnalysisConfig config = null;

            TeamBuildSettings teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
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

        private static ProjectInfoAnalysisResult InvokeSonarRunner(AnalysisConfig config, ILogger logger)
        {
            ISonarRunner runner = new SonarRunnerWrapper();
            ProjectInfoAnalysisResult result = runner.Execute(config, logger);
            return result;
        }

        private static void WriteTeamBuildSummaryReport(AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
        {
            int skippedProjectCount = GetProjectsByStatus(result, ProjectInfoValidity.NoFilesToAnalyze).Count();
            int invalidProjectCount = GetProjectsByStatus(result, ProjectInfoValidity.InvalidGuid).Count();
            invalidProjectCount += GetProjectsByStatus(result, ProjectInfoValidity.DuplicateGuid).Count();

            int excludedProjectCount = GetProjectsByStatus(result, ProjectInfoValidity.ExcludeFlagSet).Count();

            IEnumerable<ProjectInfo> validProjects = GetProjectsByStatus(result, ProjectInfoValidity.Valid);
            int productProjectCount = validProjects.Count(p => p.ProjectType == ProjectType.Product);
            int testProjectCount = validProjects.Count(p => p.ProjectType == ProjectType.Test);

            using (BuildSummaryLogger summaryLogger = new BuildSummaryLogger(config.GetTfsUri(), config.GetBuildUri()))
            {
                summaryLogger.WriteMessage(Resources.Report_ProjectInfoSummary, config.SonarProjectName, config.SonarProjectKey, config.SonarProjectVersion);
                summaryLogger.WriteMessage(Resources.Report_ProductAndTestMessage, productProjectCount, testProjectCount);
                summaryLogger.WriteMessage(Resources.Report_InvalidSkippedAndExcludedMessage, invalidProjectCount, skippedProjectCount, excludedProjectCount);

                // Add a link to SonarQube dashboard if analysis succeeded
                Debug.Assert(config.SonarRunnerPropertiesPath != null, "Not expecting the sonar-runner properties path to be null");
                if (config.SonarRunnerPropertiesPath != null && result.RanToCompletion)
                {
                    ISonarPropertyProvider propertyProvider = new FilePropertiesProvider(config.SonarRunnerPropertiesPath);
                    string hostUrl = propertyProvider.GetProperty(SonarProperties.HostUrl).TrimEnd('/');

                    string sonarUrl = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0}/dashboard/index/{1}", hostUrl, config.SonarProjectKey);

                    summaryLogger.WriteMessage(Resources.Report_AnalysisSucceeded, sonarUrl);
                }

                if (!result.RanToCompletion)
                {
                    summaryLogger.WriteMessage(Resources.Report_AnalysisFailed);
                }                
            }

        }

        private static IEnumerable<ProjectInfo> GetProjectsByStatus(ProjectInfoAnalysisResult result, ProjectInfoValidity status)
        {
            return result.Projects.Where(p => p.Value == status).Select(p => p.Key);
        }
    }
}
