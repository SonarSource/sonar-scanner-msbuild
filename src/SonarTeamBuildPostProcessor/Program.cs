//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using Sonar.TeamBuild.Integration;
using System.Diagnostics;
using System.IO;

namespace Sonar.TeamBuild.PostProcessor
{
    class Program
    {
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

            // Handle code coverage reports
            CoverageReportProcessor coverageProcessor = new CoverageReportProcessor();
            bool success = coverageProcessor.ProcessCoverageReports(config, logger);

            SummaryReportBuilder.WriteSummaryReport(config, logger);

            using (BuildSummaryLogger summaryLogger = new BuildSummaryLogger(config.GetTfsUri(), config.GetBuildUri()))
            {
                summaryLogger.WriteMessage("Sonar project: {0} ({1}), version: {2}", config.SonarProjectName, config.SonarProjectKey, config.SonarProjectVersion);

                // Add a link to SonarQube dashboard
                Debug.Assert(config.SonarRunnerPropertiesPath != null, "Not expecting the sonar runner properties path to be null");
                if (config.SonarRunnerPropertiesPath != null)
                {
                    ISonarPropertyProvider propertyProvider = new FilePropertiesProvider(config.SonarRunnerPropertiesPath);
                    string sonarUrl = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0}/dashboard/index/{1}",
                        propertyProvider.GetProperty(SonarProperties.HostUrl),
                        config.SonarProjectKey);

                    summaryLogger.WriteMessage("[Analysis results] ({0})", sonarUrl);
                }
            }

            if (!success)
            {
                return ErrorCode;
            }

            return 0;
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

    }
}
