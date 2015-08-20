//-----------------------------------------------------------------------
// <copyright file="MSBuildPostProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarRunner.Shim;
using System;
using System.Linq;

namespace SonarQube.TeamBuild.PostProcessor
{
    public class MSBuildPostProcessor
    {
        private readonly ICoverageReportProcessor codeCoverageProcessor;
        private readonly ISummaryReportBuilder reportBuilder;
        private readonly ISonarRunner sonarRunner;

        public MSBuildPostProcessor(ICoverageReportProcessor codeCoverageProcessor, ISonarRunner runner, ISummaryReportBuilder reportBuilder)
        {
            if (codeCoverageProcessor == null)
            {
                throw new ArgumentNullException("codeCoverageProcessor");
            }
            if (runner == null)
            {
                throw new ArgumentNullException("param");
            }
            if (reportBuilder == null)
            {
                throw new ArgumentNullException("reportBuilder");
            }

            this.codeCoverageProcessor = codeCoverageProcessor;
            this.sonarRunner = runner;
            this.reportBuilder = reportBuilder;
        }

        public bool Execute(AnalysisConfig config, TeamBuildSettings settings, ILogger logger)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            if (config == null)
            {
                LogStartupSettings(config, settings, logger);
                logger.LogError(Resources.ERROR_MissingSettings);
                return false;
            }

            logger.Verbosity = VerbosityCalculator.ComputeVerbosity(config.GetAnalysisSettings(true), logger);
            LogStartupSettings(config, settings, logger);

            if (!CheckEnvironmentConsistency(config, settings, logger))
            {
                return false;
            }

            // Handle code coverage reports
            if (!this.codeCoverageProcessor.ProcessCoverageReports(config, settings, logger))
            {
                return false;
            }

            ProjectInfoAnalysisResult result = InvokeSonarRunner(config, logger);

            this.reportBuilder.GenerateReports(settings, config, result, logger);

            return result.RanToCompletion;
        }

        private static void LogStartupSettings(AnalysisConfig config, TeamBuildSettings settings, ILogger logger)
        {
            logger.LogDebug(Resources.MSG_LoadingConfig, config.FileName);

            switch (settings.BuildEnvironment)
            {
                case BuildEnvironment.LegacyTeamBuild:
                    logger.LogDebug(Resources.SETTINGS_InLegacyTeamBuild);

                    break;
                case BuildEnvironment.TeamBuild:
                    logger.LogDebug(Resources.SETTINGS_InTeamBuild);
                    break;
                case BuildEnvironment.NotTeamBuild:
                    logger.LogDebug(Resources.SETTINGS_NotInTeamBuild);
                    break;
                default:
                    break;
            }

            logger.LogDebug(Resources.SETTING_DumpSettings,
                settings.AnalysisBaseDirectory,
                settings.BuildDirectory,
                settings.SonarBinDirectory,
                settings.SonarConfigDirectory,
                settings.SonarOutputDirectory,
                settings.AnalysisConfigFilePath);
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

        private ProjectInfoAnalysisResult InvokeSonarRunner(AnalysisConfig config, ILogger logger)
        {
            logger.IncludeTimestamp = false;
            ProjectInfoAnalysisResult result = this.sonarRunner.Execute(config, Enumerable.Empty<string>() /* todo */, logger);
            logger.IncludeTimestamp = true;
            return result;
        }
    }
}
