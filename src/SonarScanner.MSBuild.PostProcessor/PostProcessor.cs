/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using System.IO;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.Common.TFS;
using SonarScanner.MSBuild.PostProcessor.Interfaces;
using SonarScanner.MSBuild.Shim;
using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.PostProcessor
{
    public class PostProcessor : IPostProcessor
    {
        private const string scanAllFiles = "-Dsonar.scanAllFiles=true";

        private readonly ISonarScanner sonarScanner;
        private readonly ILogger logger;
        private readonly ITargetsUninstaller targetUninstaller;
        private readonly ISonarProjectPropertiesValidator sonarProjectPropertiesValidator;
        private readonly ITfsProcessor tfsProcessor;
        private readonly IFileWrapper fileWrapper;

        private IPropertiesFileGenerator propertiesFileGenerator;

        public PostProcessor(
            ISonarScanner sonarScanner,
            ILogger logger,
            ITargetsUninstaller targetUninstaller,
            ITfsProcessor tfsProcessor,
            ISonarProjectPropertiesValidator sonarProjectPropertiesValidator,
            IFileWrapper fileWrapper)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.sonarScanner = sonarScanner ?? throw new ArgumentNullException(nameof(sonarScanner));
            this.targetUninstaller = targetUninstaller ?? throw new ArgumentNullException(nameof(targetUninstaller));
            this.sonarProjectPropertiesValidator = sonarProjectPropertiesValidator ?? throw new ArgumentNullException(nameof(sonarProjectPropertiesValidator));
            this.tfsProcessor = tfsProcessor ?? throw new ArgumentNullException(nameof(tfsProcessor));
            this.fileWrapper = fileWrapper ?? throw new ArgumentNullException(nameof(fileWrapper));
        }

        public void /* for testing purposes */ SetPropertiesFileGenerator(IPropertiesFileGenerator propertiesFileGenerator) =>
            this.propertiesFileGenerator = propertiesFileGenerator;

        public bool Execute(string[] args, AnalysisConfig config, IBuildSettings settings)
        {
            _ = args ?? throw new ArgumentNullException(nameof(args));
            _ = config ?? throw new ArgumentNullException(nameof(config));
            _ = settings ?? throw new ArgumentNullException(nameof(settings));
            logger.SuspendOutput();

            targetUninstaller.UninstallTargets(config.SonarBinDir);

            if (!ArgumentProcessor.TryProcessArgs(args, logger, out IAnalysisPropertyProvider provider))
            {
                logger.ResumeOutput();
                // logging already done
                return false;
            }

            logger.Verbosity = VerbosityCalculator.ComputeVerbosity(config.GetAnalysisSettings(true, logger), logger);
            logger.ResumeOutput();
            LogStartupSettings(config, settings);

            if (!CheckCredentialsInCommandLineArgs(config, provider) || !CheckEnvironmentConsistency(config, settings))
            {
                // logging already done
                return false;
            }

            var propertyResult = GenerateAndValidatePropertiesFile(config);
            if (propertyResult.FullPropertiesFilePath != null)
            {
#if NETFRAMEWORK
                ProcessCoverageReport(config, Path.Combine(config.SonarConfigDir, FileConstants.ConfigFileName), propertyResult.FullPropertiesFilePath);
#endif
                var result = false;
                if (propertyResult.RanToCompletion)
                {
                    result = InvokeSonarScanner(provider, config, propertyResult.FullPropertiesFilePath);
                }
#if NETFRAMEWORK
                if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild)
                {
                    ProcessSummaryReportBuilder(config, result, Path.Combine(config.SonarConfigDir, FileConstants.ConfigFileName), propertyResult.FullPropertiesFilePath);
                }
#endif
                return result;
            }

            LogUIWarnings(config, settings);
            return false;
        }

        private ProjectInfoAnalysisResult GenerateAndValidatePropertiesFile(AnalysisConfig config)
        {
            if (this.propertiesFileGenerator == null)
            {
                this.propertiesFileGenerator = new PropertiesFileGenerator(config, logger);
            }

            var result = this.propertiesFileGenerator.GenerateFile();

            if (this.sonarProjectPropertiesValidator.AreExistingSonarPropertiesFilesPresent(config.SonarScannerWorkingDirectory, result.Projects, out var invalidFolders))
            {
                logger.LogError(Resources.ERR_ConflictingSonarProjectProperties, string.Join(", ", invalidFolders));
                result.RanToCompletion = false;
            }
            else
            {
                ProjectInfoReportBuilder.WriteSummaryReport(config, result, logger);
                result.RanToCompletion = true;
            }

            return result;
        }

        private void LogStartupSettings(AnalysisConfig config, IBuildSettings settings)
        {
            var configFileName = config == null ? string.Empty : config.FileName;
            logger.LogDebug(Resources.MSG_LoadingConfig, configFileName, config != null ? SonarProduct.GetSonarProductToLog(config.SonarQubeHostUrl) : "Sonar");

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
        private bool CheckEnvironmentConsistency(AnalysisConfig config, IBuildSettings settings)
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
            if (string.Equals(configUri, environmentUi, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else
            {
                logger.LogError(Resources.ERROR_BuildUrisDontMatch, environmentUi, configUri, settings.AnalysisConfigFilePath);
                return false;
            }
        }

        /// <summary>
        /// Credentials must be passed to both begin and end step (or not passed at all). If the credentials are passed to only
        /// one of the steps the analysis will fail so let's fail-fast with an explicit message.
        /// </summary>
        private bool CheckCredentialsInCommandLineArgs(AnalysisConfig config, IAnalysisPropertyProvider provider)
        {
            var hasCredentialsInBeginStep = config.HasBeginStepCommandLineCredentials;
            var hasCredentialsInEndStep = provider.HasProperty(SonarProperties.SonarToken) || provider.HasProperty(SonarProperties.SonarUserName);
            if (hasCredentialsInBeginStep ^ hasCredentialsInEndStep)
            {
                logger.LogError(Resources.ERROR_CredentialsNotSpecified);
                return false;
            }

            return true;
        }

#if NETFRAMEWORK

        private void ProcessSummaryReportBuilder(AnalysisConfig config, bool ranToCompletion, String sonarAnalysisConfigFilePath, string propertiesFilePath)
        {
            IList<string> args = new List<string>();
            args.Add("SummaryReportBuilder");
            args.Add(sonarAnalysisConfigFilePath);
            args.Add(propertiesFilePath);
            args.Add(ranToCompletion.ToString());

            logger.IncludeTimestamp = false;
            this.tfsProcessor.Execute(config, args, propertiesFilePath);
            logger.IncludeTimestamp = true;
        }

        private void ProcessCoverageReport(AnalysisConfig config, String sonarAnalysisConfigFilePath, String propertiesFilePath)
        {
            IList<string> args = new List<string>();
            args.Add("ConvertCoverage");
            args.Add(sonarAnalysisConfigFilePath);
            args.Add(propertiesFilePath);

            logger.IncludeTimestamp = false;
            this.tfsProcessor.Execute(config, args, propertiesFilePath);
            logger.IncludeTimestamp = true;
        }

#endif

        private bool InvokeSonarScanner(IAnalysisPropertyProvider cmdLineArgs, AnalysisConfig config, String propertiesFilePath)
        {
            var args = GetSonarScannerArgs(cmdLineArgs);

            logger.IncludeTimestamp = false;
            var result = sonarScanner.Execute(config, args, propertiesFilePath);
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

        // see https://github.com/SonarSource/sonar-dotnet-autoscan/blob/e6c57158bc8842b0aa495180f98819a16d0cbe54/AutoScan.NET/Program.cs#L46
        private void LogUIWarnings(AnalysisConfig config, IBuildSettings settings)
        {
            var warningsFile = Path.Combine(settings.SonarOutputDirectory, "AnalysisWarnings.S4NET.json");
            if (config.MultiFileAnalysis)
            {
                AnalysisWarningProcessor.Process([Resources.WARN_UI_MultifileAnalysisEnabled], warningsFile, fileWrapper, logger);
            }
        }
    }
}
