//-----------------------------------------------------------------------
// <copyright file="MSBuildPostProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.Integration.Interfaces;
using SonarQube.TeamBuild.PostProcessor.Interfaces;
using SonarScanner.Shim;
using System;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.PostProcessor
{
    public class MSBuildPostProcessor : IMSBuildPostProcessor
    {
        private const string scanAllFiles = "-Dsonar.scanAllFiles=true";

        private readonly ICoverageReportProcessor codeCoverageProcessor;
        private readonly ISummaryReportBuilder reportBuilder;
        private readonly ISonarScanner sonarScanner;
        private readonly ILogger logger;
        private readonly ITargetsUninstaller targetUninstaller;

        public MSBuildPostProcessor(ICoverageReportProcessor codeCoverageProcessor, ISonarScanner scanner, ISummaryReportBuilder reportBuilder, ILogger logger,
            ITargetsUninstaller targetUninstaller)
        {
            if (codeCoverageProcessor == null)
            {
                throw new ArgumentNullException(nameof(codeCoverageProcessor));
            }
            if (scanner == null)
            {
                throw new ArgumentNullException(nameof(scanner));
            }
            if (reportBuilder == null)
            {
                throw new ArgumentNullException(nameof(reportBuilder));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            if (targetUninstaller == null)
            {
                throw new ArgumentNullException(nameof(targetUninstaller));
            }

            this.logger = logger;
            this.codeCoverageProcessor = codeCoverageProcessor;
            this.sonarScanner = scanner;
            this.reportBuilder = reportBuilder;
            this.targetUninstaller = targetUninstaller;
        }

        public bool Execute(string[] args, AnalysisConfig config, ITeamBuildSettings settings)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            this.targetUninstaller.UninstallTargets(logger);

            IAnalysisPropertyProvider provider;
            if (!ArgumentProcessor.TryProcessArgs(args, logger, out provider))
            {
                // logging already done
                return false;
            }

            logger.Verbosity = VerbosityCalculator.ComputeVerbosity(config.GetAnalysisSettings(true), logger);
            LogStartupSettings(config, settings);

            if (!CheckEnvironmentConsistency(config, settings))
            {
                // logging already done
                return false;
            }

            // if initialisation fails a warning will have been logged at the source of the failure
            bool initialised = this.codeCoverageProcessor.Initialise(config, settings, logger);

            if (initialised && !this.codeCoverageProcessor.ProcessCoverageReports())
            {
                // if processing fails, stop the workflow
                return false;
            }

            ProjectInfoAnalysisResult result = InvokeSonarScanner(provider, config);
            this.reportBuilder.GenerateReports(settings, config, result, logger);
            return result.RanToCompletion;
        }

        private void LogStartupSettings(AnalysisConfig config, ITeamBuildSettings settings)
        {
            string configFileName = config == null ? string.Empty : config.FileName;
            logger.LogDebug(Resources.MSG_LoadingConfig, configFileName);

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
        private bool CheckEnvironmentConsistency(AnalysisConfig config, ITeamBuildSettings settings)
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

        private ProjectInfoAnalysisResult InvokeSonarScanner(IAnalysisPropertyProvider cmdLineArgs, AnalysisConfig config)
        {
            IEnumerable<string> args = GetSonarScannerArgs(cmdLineArgs);

            logger.IncludeTimestamp = false;
            ProjectInfoAnalysisResult result = this.sonarScanner.Execute(config, args, logger);
            logger.IncludeTimestamp = true;
            return result;
        }

        private static IEnumerable<string> GetSonarScannerArgs(IAnalysisPropertyProvider provider)
        {
            IList<string> args = new List<string>();

            if (provider != null)
            {
                foreach (Property property in provider.GetAllProperties())
                {
                    args.Add(property.AsSonarScannerArg());
                }
            }

            if (!args.Contains(scanAllFiles))
            {
                args.Add(scanAllFiles);
            }

            return args;
        }
    }
}