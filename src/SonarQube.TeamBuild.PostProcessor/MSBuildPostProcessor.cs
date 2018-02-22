/*
 * SonarQube Scanner for MSBuild
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
using System.Collections.Generic;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.Integration.Interfaces;
using SonarQube.TeamBuild.PostProcessor.Interfaces;
using SonarScanner.Shim;

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

        public MSBuildPostProcessor(ICoverageReportProcessor codeCoverageProcessor, ISonarScanner scanner,
            ISummaryReportBuilder reportBuilder, ILogger logger, ITargetsUninstaller targetUninstaller)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.codeCoverageProcessor = codeCoverageProcessor ?? throw new ArgumentNullException(nameof(codeCoverageProcessor));
            sonarScanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
            this.reportBuilder = reportBuilder ?? throw new ArgumentNullException(nameof(reportBuilder));
            this.targetUninstaller = targetUninstaller ?? throw new ArgumentNullException(nameof(targetUninstaller));
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

            targetUninstaller.UninstallTargets(logger);

            logger.SuspendOutput();

            if (!ArgumentProcessor.TryProcessArgs(args, logger, out IAnalysisPropertyProvider provider))
            {
                logger.ResumeOutput();
                // logging already done
                return false;
            }

            logger.Verbosity = VerbosityCalculator.ComputeVerbosity(config.GetAnalysisSettings(true), logger);
            logger.ResumeOutput();
            LogStartupSettings(config, settings);

            if (!CheckEnvironmentConsistency(config, settings))
            {
                // logging already done
                return false;
            }

            // if initialization fails a warning will have been logged at the source of the failure
            var initialised = codeCoverageProcessor.Initialise(config, settings, logger);

            if (initialised && !codeCoverageProcessor.ProcessCoverageReports())
            {
                // if processing fails, stop the workflow
                return false;
            }

            var result = InvokeSonarScanner(provider, config);
            reportBuilder.GenerateReports(settings, config, result, logger);
            return result.RanToCompletion;
        }

        private void LogStartupSettings(AnalysisConfig config, ITeamBuildSettings settings)
        {
            var configFileName = config == null ? string.Empty : config.FileName;
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

            var configUri = config.GetBuildUri();
            var environmentUi = settings.BuildUri;

            if (!string.Equals(configUri, environmentUi, System.StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError(Resources.ERROR_BuildUrisDontMatch, environmentUi, configUri, settings.AnalysisConfigFilePath);
                return false;
            }

            return true;
        }

        private ProjectInfoAnalysisResult InvokeSonarScanner(IAnalysisPropertyProvider cmdLineArgs, AnalysisConfig config)
        {
            var args = GetSonarScannerArgs(cmdLineArgs);

            logger.IncludeTimestamp = false;
            var result = sonarScanner.Execute(config, args, logger);
            logger.IncludeTimestamp = true;
            return result;
        }

        private static IEnumerable<string> GetSonarScannerArgs(IAnalysisPropertyProvider provider)
        {
            IList<string> args = new List<string>();

            if (provider != null)
            {
                foreach (var property in provider.GetAllProperties())
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
