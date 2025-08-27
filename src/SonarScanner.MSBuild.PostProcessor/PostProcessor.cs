/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.Common.TFS;
using SonarScanner.MSBuild.PostProcessor.Interfaces;
using SonarScanner.MSBuild.Shim;
using SonarScanner.MSBuild.TFS;

namespace SonarScanner.MSBuild.PostProcessor;

public class PostProcessor : IPostProcessor
{
    private readonly SonarScannerWrapper sonarScanner;
    private readonly ILogger logger;
    private readonly TargetsUninstaller targetUninstaller;
    private readonly SonarProjectPropertiesValidator sonarProjectPropertiesValidator;
    private readonly TfsProcessorWrapper tfsProcessor;
    private readonly BuildVNextCoverageReportProcessor coverageReportProcessor;
    private readonly IFileWrapper fileWrapper;

    private ScannerEngineInputGenerator scannerEngineInputGenerator;

    public PostProcessor(SonarScannerWrapper sonarScanner,
                         ILogger logger,
                         TargetsUninstaller targetUninstaller,
                         TfsProcessorWrapper tfsProcessor,
                         SonarProjectPropertiesValidator sonarProjectPropertiesValidator,
                         BuildVNextCoverageReportProcessor coverageReportProcessor,
                         IFileWrapper fileWrapper = null)
    {
        this.sonarScanner = sonarScanner ?? throw new ArgumentNullException(nameof(sonarScanner));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.targetUninstaller = targetUninstaller ?? throw new ArgumentNullException(nameof(targetUninstaller));
        this.tfsProcessor = tfsProcessor ?? throw new ArgumentNullException(nameof(tfsProcessor));
        this.sonarProjectPropertiesValidator = sonarProjectPropertiesValidator ?? throw new ArgumentNullException(nameof(sonarProjectPropertiesValidator));
        this.coverageReportProcessor = coverageReportProcessor ?? throw new ArgumentNullException(nameof(coverageReportProcessor));
        this.fileWrapper = fileWrapper ?? FileWrapper.Instance;
    }

    public bool Execute(string[] args, AnalysisConfig config, IBuildSettings settings)
    {
        _ = args ?? throw new ArgumentNullException(nameof(args));
        _ = config ?? throw new ArgumentNullException(nameof(config));
        _ = settings ?? throw new ArgumentNullException(nameof(settings));
        logger.SuspendOutput();
        targetUninstaller.UninstallTargets(config.SonarBinDir);
        if (!ArgumentProcessor.TryProcessArgs(args, logger, out var provider))
        {
            logger.ResumeOutput();
            return false;   // logging already done
        }
        logger.Verbosity = VerbosityCalculator.ComputeVerbosity(config.AnalysisSettings(true, logger), logger);
        logger.ResumeOutput();
        LogStartupSettings(config, settings);
        if (!CheckCredentialsInCommandLineArgs(config, provider) || !CheckEnvironmentConsistency(config, settings))
        {
            return false;   // logging already done
        }

        var analysisResult = CreateAnalysisResult(config);
        if (analysisResult.FullPropertiesFilePath is null)
        {
            return false;
        }
        else
        {
#if NETFRAMEWORK
            ProcessCoverageReport(config, settings, Path.Combine(config.SonarConfigDir, FileConstants.ConfigFileName), analysisResult);
#endif
            var result = false;
            if (analysisResult.RanToCompletion)
            {
                var engineInputDumpPath = Path.Combine(settings.SonarOutputDirectory, "ScannerEngineInput.json");   // For customer troubleshooting only
                fileWrapper.WriteAllText(engineInputDumpPath, analysisResult.ScannerEngineInput.CloneWithoutSensitiveData().ToString());
                result = InvokeSonarScanner(provider, config, analysisResult.FullPropertiesFilePath);
            }
#if NETFRAMEWORK
            if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild)
            {
                ProcessSummaryReportBuilder(config, result, Path.Combine(config.SonarConfigDir, FileConstants.ConfigFileName), analysisResult.FullPropertiesFilePath);
            }
#endif
            return result;
        }
    }

    internal void SetScannerEngineInputGenerator(ScannerEngineInputGenerator scannerEngineInputGenerator) =>
        this.scannerEngineInputGenerator = scannerEngineInputGenerator;

    private AnalysisResult CreateAnalysisResult(AnalysisConfig config)
    {
        scannerEngineInputGenerator ??= new ScannerEngineInputGenerator(config, logger);
        var result = scannerEngineInputGenerator.GenerateResult();
        if (sonarProjectPropertiesValidator.AreExistingSonarPropertiesFilesPresent(config.SonarScannerWorkingDirectory, result.Projects, out var invalidFolders))
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
        var environmentMessage = settings.BuildEnvironment switch
        {
            BuildEnvironment.LegacyTeamBuild => Resources.SETTINGS_InLegacyTeamBuild,
            BuildEnvironment.TeamBuild => Resources.SETTINGS_InTeamBuild,
            BuildEnvironment.NotTeamBuild => Resources.SETTINGS_NotInTeamBuild,
            _ => throw new InvalidOperationException($"Unexpected BuildEnvironment: {settings.BuildEnvironment}")
        };
        logger.LogDebug(Resources.MSG_LoadingConfig, config.FileName);
        logger.LogDebug(environmentMessage);
        logger.LogDebug(
            Resources.SETTING_DumpSettings,
            settings.AnalysisBaseDirectory,
            settings.BuildDirectory,
            settings.SonarBinDirectory,
            settings.SonarConfigDirectory,
            settings.SonarOutputDirectory,
            settings.AnalysisConfigFilePath);
    }

    /// <summary>
    /// Returns a boolean indicating whether the information in the environment variables matches that in the analysis config file to detect invalid Agent setup.
    /// </summary>
    private bool CheckEnvironmentConsistency(AnalysisConfig config, IBuildSettings settings)
    {
        // Currently we're only checking that the build Uris match as this is the most likely error - it probably means that an old analysis config file has been left behind somehow.
        // e.g. a build definition used to include analysis but has changed so that it is no longer an analysis build, but there is still an old analysis config on disc.
        if (settings.BuildEnvironment == BuildEnvironment.NotTeamBuild)
        {
            return true;
        }

        var configUri = config.GetBuildUri();
        var environmentUri = settings.BuildUri;
        if (string.Equals(configUri, environmentUri, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        else
        {
            logger.LogError(Resources.ERROR_BuildUrisDontMatch, environmentUri, configUri, settings.AnalysisConfigFilePath);
            return false;
        }
    }

    /// <summary>
    /// Credentials must be passed to both begin and end step (or not passed at all). If the credentials are passed to only
    /// one of the steps the analysis will fail so let's fail-fast with an explicit message.
    /// </summary>
    private bool CheckCredentialsInCommandLineArgs(AnalysisConfig config, IAnalysisPropertyProvider provider)
    {
        var hasCredentialsInEndStep = provider.HasProperty(SonarProperties.SonarToken) || provider.HasProperty(SonarProperties.SonarUserName);
        if (config.HasBeginStepCommandLineCredentials ^ hasCredentialsInEndStep)
        {
            logger.LogError(Resources.ERROR_CredentialsNotSpecified);
            return false;
        }

        var sonarScannerOpts = Environment.GetEnvironmentVariable(EnvironmentVariables.SonarScannerOptsVariableName);
        var hasTruststorePasswordInEndStep = provider.HasProperty(SonarProperties.TruststorePassword)
            || (!string.IsNullOrWhiteSpace(sonarScannerOpts) && sonarScannerOpts.Contains("-Djavax.net.ssl.trustStorePassword="));
        // Truststore password must be passed to the end step when it was passed to the begin step
        // However, it is not mandatory to pass it to the begin step to pass it to the end step
        if (config.HasBeginStepCommandLineTruststorePassword && !hasTruststorePasswordInEndStep)
        {
            logger.LogError(Resources.ERROR_TruststorePasswordNotSpecified);
            return false;
        }

        return true;
    }

#if NETFRAMEWORK

    private void ProcessSummaryReportBuilder(AnalysisConfig config, bool ranToCompletion, string sonarAnalysisConfigFilePath, string propertiesFilePath)
    {
        logger.IncludeTimestamp = false;
        tfsProcessor.Execute(config, ["SummaryReportBuilder", sonarAnalysisConfigFilePath, propertiesFilePath, ranToCompletion.ToString()]);
        logger.IncludeTimestamp = true;
    }

    private void ProcessCoverageReport(AnalysisConfig config, IBuildSettings settings, string sonarAnalysisConfigFilePath, AnalysisResult analysisResult)
    {
        if (settings.BuildEnvironment is BuildEnvironment.TeamBuild)
        {
            logger.LogInfo(Resources.MSG_ConvertingCoverageReports);
            var additionalProperties = coverageReportProcessor.ProcessCoverageReports(config, settings);
            WriteProperty(analysisResult.FullPropertiesFilePath, SonarProperties.VsTestReportsPaths, additionalProperties.VsTestReportsPaths);
            WriteProperty(analysisResult.FullPropertiesFilePath, SonarProperties.VsCoverageXmlReportsPaths, additionalProperties.VsCoverageXmlReportsPaths);
            analysisResult.ScannerEngineInput.AddVsTestReportPaths(additionalProperties.VsTestReportsPaths);
            analysisResult.ScannerEngineInput.AddVsXmlCoverageReportPaths(additionalProperties.VsCoverageXmlReportsPaths);
        }
        else if (settings.BuildEnvironment is BuildEnvironment.LegacyTeamBuild && !BuildSettings.SkipLegacyCodeCoverageProcessing)
        {
            logger.LogInfo(Resources.MSG_TFSLegacyProcessorCalled);
            logger.IncludeTimestamp = false;
            tfsProcessor.Execute(config, ["ConvertCoverage", sonarAnalysisConfigFilePath, analysisResult.FullPropertiesFilePath]);
            logger.IncludeTimestamp = true;
        }
    }

    private void WriteProperty(string propertiesFilePath, string property, string[] paths)
    {
        if (paths is not null)
        {
            fileWrapper.AppendAllText(propertiesFilePath, $"{Environment.NewLine}{property}={string.Join(",", paths.Select(x => x.Replace(@"\", @"\\")))}");
        }
    }

#endif

    private bool InvokeSonarScanner(IAnalysisPropertyProvider cmdLineArgs, AnalysisConfig config, string propertiesFilePath)
    {
        logger.IncludeTimestamp = false;
        var result = sonarScanner.Execute(config, cmdLineArgs, propertiesFilePath);
        logger.IncludeTimestamp = true;
        return result;
    }
}
