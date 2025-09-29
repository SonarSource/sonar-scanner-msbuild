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
using SonarScanner.MSBuild.Shim;
using SonarScanner.MSBuild.TFS;

namespace SonarScanner.MSBuild.PostProcessor;

public class PostProcessor
{
    private readonly SonarScannerWrapper sonarScanner;
    private readonly SonarEngineWrapper sonarEngine;
    private readonly IRuntime runtime;
    private readonly TargetsUninstaller targetUninstaller;
    private readonly SonarProjectPropertiesValidator sonarProjectPropertiesValidator;
    private readonly TfsProcessorWrapper tfsProcessor;
    private readonly BuildVNextCoverageReportProcessor coverageReportProcessor;

    private ScannerEngineInputGenerator scannerEngineInputGenerator;

    public PostProcessor(SonarScannerWrapper sonarScanner,
                         SonarEngineWrapper sonarEngine,
                         IRuntime runtime,
                         TargetsUninstaller targetUninstaller,
                         TfsProcessorWrapper tfsProcessor,
                         SonarProjectPropertiesValidator sonarProjectPropertiesValidator,
                         BuildVNextCoverageReportProcessor coverageReportProcessor)
    {
        this.sonarScanner = sonarScanner ?? throw new ArgumentNullException(nameof(sonarScanner));
        this.sonarEngine = sonarEngine ?? throw new ArgumentNullException(nameof(sonarEngine));
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.targetUninstaller = targetUninstaller ?? throw new ArgumentNullException(nameof(targetUninstaller));
        this.tfsProcessor = tfsProcessor ?? throw new ArgumentNullException(nameof(tfsProcessor));
        this.sonarProjectPropertiesValidator = sonarProjectPropertiesValidator ?? throw new ArgumentNullException(nameof(sonarProjectPropertiesValidator));
        this.coverageReportProcessor = coverageReportProcessor ?? throw new ArgumentNullException(nameof(coverageReportProcessor));
    }

    public virtual bool Execute(string[] args, AnalysisConfig config, IBuildSettings settings)
    {
        _ = args ?? throw new ArgumentNullException(nameof(args));
        _ = config ?? throw new ArgumentNullException(nameof(config));
        _ = settings ?? throw new ArgumentNullException(nameof(settings));
        runtime.Logger.SuspendOutput(); // Wait for the correct verbosity to be calculated
        targetUninstaller.UninstallTargets(config.SonarBinDir);
        if (!ArgumentProcessor.TryProcessArgs(args, runtime.Logger, out var cmdLineArgs))
        {
            runtime.Logger.ResumeOutput();
            return false;   // logging already done
        }
        runtime.Logger.Verbosity = VerbosityCalculator.ComputeVerbosity(config.AnalysisSettings(true, runtime.Logger), runtime.Logger);
        runtime.Logger.ResumeOutput();
        LogStartupSettings(config, settings);
        if (!CheckCredentialsInCommandLineArgs(config, cmdLineArgs) || !CheckEnvironmentConsistency(config, settings))
        {
            return false;   // logging already done
        }

        var analysisResult = CreateAnalysisResult(config, cmdLineArgs);
        if (analysisResult.FullPropertiesFilePath is null)
        {
            return false;
        }
        else
        {
            ProcessCoverageReport(config, settings, analysisResult);
            var result = false;
            if (analysisResult.RanToCompletion)
            {
                var engineInputDumpPath = Path.Combine(settings.SonarOutputDirectory, "ScannerEngineInput.json");   // For customer troubleshooting only
                runtime.File.WriteAllText(engineInputDumpPath, analysisResult.ScannerEngineInput.CloneWithoutSensitiveData().ToString());
                // This is the last moment where we can set telemetry, because telemetry needs to be written before the scanner/engine invocation.
                runtime.Telemetry[TelemetryKeys.EndstepLegacyTFS] = IsTfsProcessorCalled(settings);
                runtime.Telemetry.Write(settings.SonarOutputDirectory);
                result = config.UseSonarScannerCli || config.EngineJarPath is null
                    ? InvokeSonarScanner(cmdLineArgs, config, analysisResult.FullPropertiesFilePath)
                    : InvokeScannerEngine(cmdLineArgs, config, analysisResult.ScannerEngineInput);
            }
            ProcessSummaryReportBuilder(config, settings, analysisResult, result);
            return result;
        }
    }

    internal void SetScannerEngineInputGenerator(ScannerEngineInputGenerator scannerEngineInputGenerator) =>
        this.scannerEngineInputGenerator = scannerEngineInputGenerator;

    private AnalysisResult CreateAnalysisResult(AnalysisConfig config, IAnalysisPropertyProvider cmdLineArgs)
    {
        scannerEngineInputGenerator ??= new ScannerEngineInputGenerator(config, cmdLineArgs, runtime);
        var result = scannerEngineInputGenerator.GenerateResult();
        if (sonarProjectPropertiesValidator.AreExistingSonarPropertiesFilesPresent(config.SonarScannerWorkingDirectory, result.Projects, out var invalidFolders))
        {
            runtime.LogError(Resources.ERR_ConflictingSonarProjectProperties, string.Join(", ", invalidFolders));
            result.RanToCompletion = false;
        }
        else
        {
            ProjectInfoReportBuilder.WriteSummaryReport(config, result, runtime.Logger);
            result.RanToCompletion = true;
        }
        return result;
    }

    private static string IsTfsProcessorCalled(IBuildSettings settings) =>
        // We need to know IsTfsProcessorCalled? before we call the scanner/engine because telemetry needs to be complete before that call.
        // tfsProcessor.Execute is called in ProcessSummaryReportBuilder (called after the scanner/engine invocation) if NETFRAMEWORK and BuildEnvironment.LegacyTeamBuild and also in
        // ProcessCoverageReport (before the scanner/engine invocation and only if !BuildSettings.SkipLegacyCodeCoverageProcessing).
        // We are interested if either of the calls happened and therefore we assume ProcessSummaryReportBuilder will happen after the scanner/engine invocation
        // and BuildSettings.SkipLegacyCodeCoverageProcessing is ignored for telemetry.
#if NETFRAMEWORK
        settings.BuildEnvironment is BuildEnvironment.LegacyTeamBuild ? TelemetryValues.EndstepLegacyTFS.Called : TelemetryValues.EndstepLegacyTFS.NotCalled;
#else
        TelemetryValues.EndstepLegacyTFS.NotCalled;
#endif

    private void LogStartupSettings(AnalysisConfig config, IBuildSettings settings)
    {
        var environmentMessage = settings.BuildEnvironment switch
        {
            BuildEnvironment.LegacyTeamBuild => Resources.SETTINGS_InLegacyTeamBuild,
            BuildEnvironment.TeamBuild => Resources.SETTINGS_InTeamBuild,
            BuildEnvironment.NotTeamBuild => Resources.SETTINGS_NotInTeamBuild,
            _ => throw new InvalidOperationException($"Unexpected BuildEnvironment: {settings.BuildEnvironment}")
        };
        runtime.LogDebug(Resources.MSG_LoadingConfig, config.FileName);
        runtime.LogDebug(environmentMessage);
        runtime.LogDebug(
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
            runtime.LogError(Resources.ERROR_BuildUrisDontMatch, environmentUri, configUri, settings.AnalysisConfigFilePath);
            return false;
        }
    }

    /// <summary>
    /// Credentials must be passed to both begin and end step (or not passed at all). If the credentials are passed to only
    /// one of the steps the analysis will fail so let's fail-fast with an explicit message.
    /// </summary>
    private bool CheckCredentialsInCommandLineArgs(AnalysisConfig config, IAnalysisPropertyProvider cmdLineArgs)
    {
        var hasCredentialsInEndStep = cmdLineArgs.HasProperty(SonarProperties.SonarToken) || cmdLineArgs.HasProperty(SonarProperties.SonarUserName);
        if (config.HasBeginStepCommandLineCredentials ^ hasCredentialsInEndStep)
        {
            runtime.LogError(Resources.ERROR_CredentialsNotSpecified);
            return false;
        }

        var sonarScannerOpts = Environment.GetEnvironmentVariable(EnvironmentVariables.SonarScannerOptsVariableName);
        var hasTruststorePasswordInEndStep = cmdLineArgs.HasProperty(SonarProperties.TruststorePassword)
            || (!string.IsNullOrWhiteSpace(sonarScannerOpts) && sonarScannerOpts.Contains("-Djavax.net.ssl.trustStorePassword="));
        // Truststore password must be passed to the end step when it was passed to the begin step
        // However, it is not mandatory to pass it to the begin step to pass it to the end step
        if (config.HasBeginStepCommandLineTruststorePassword && !hasTruststorePasswordInEndStep)
        {
            runtime.LogError(Resources.ERROR_TruststorePasswordNotSpecified);
            return false;
        }

        return true;
    }

    private void ProcessSummaryReportBuilder(AnalysisConfig config, IBuildSettings settings, AnalysisResult analysisResult, bool ranToCompletion)
    {
#if NETFRAMEWORK
        if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild)
        {
            runtime.Logger.IncludeTimestamp = false;
            tfsProcessor.Execute(
                config,
                ["SummaryReportBuilder", Path.Combine(config.SonarConfigDir, FileConstants.ConfigFileName), analysisResult.FullPropertiesFilePath, ranToCompletion.ToString()]);
            runtime.Logger.IncludeTimestamp = true;
        }
#endif
    }

    private void ProcessCoverageReport(AnalysisConfig config, IBuildSettings settings, AnalysisResult analysisResult)
    {
#if NETFRAMEWORK
        if (settings.BuildEnvironment is BuildEnvironment.TeamBuild)
        {
            runtime.LogInfo(Resources.MSG_ConvertingCoverageReports);
            var additionalProperties = coverageReportProcessor.ProcessCoverageReports(config, settings);
            WriteProperty(analysisResult.FullPropertiesFilePath, SonarProperties.VsTestReportsPaths, additionalProperties.VsTestReportsPaths);
            WriteProperty(analysisResult.FullPropertiesFilePath, SonarProperties.VsCoverageXmlReportsPaths, additionalProperties.VsCoverageXmlReportsPaths);
            analysisResult.ScannerEngineInput.AddVsTestReportPaths(additionalProperties.VsTestReportsPaths);
            analysisResult.ScannerEngineInput.AddVsXmlCoverageReportPaths(additionalProperties.VsCoverageXmlReportsPaths);
        }
        else if (settings.BuildEnvironment is BuildEnvironment.LegacyTeamBuild && !BuildSettings.SkipLegacyCodeCoverageProcessing)
        {
            runtime.LogInfo(Resources.MSG_TFSLegacyProcessorCalled);
            runtime.Logger.IncludeTimestamp = false;
            tfsProcessor.Execute(config, ["ConvertCoverage", Path.Combine(config.SonarConfigDir, FileConstants.ConfigFileName), analysisResult.FullPropertiesFilePath]);
            runtime.Logger.IncludeTimestamp = true;
        }
#endif
    }

#if NETFRAMEWORK

    private void WriteProperty(string propertiesFilePath, string property, string[] paths)
    {
        if (paths is not null)
        {
            runtime.File.AppendAllText(propertiesFilePath, $"{Environment.NewLine}{property}={string.Join(",", paths.Select(x => x.Replace(@"\", @"\\")))}");
        }
    }

#endif

    private bool InvokeSonarScanner(IAnalysisPropertyProvider cmdLineArgs, AnalysisConfig config, string propertiesFilePath)
    {
        runtime.Logger.IncludeTimestamp = false;
        var result = sonarScanner.Execute(config, cmdLineArgs, propertiesFilePath);
        runtime.Logger.IncludeTimestamp = true;
        return result;
    }

    private bool InvokeScannerEngine(IAnalysisPropertyProvider cmdLineArgs, AnalysisConfig config, ScannerEngineInput input) =>
        sonarEngine.Execute(config, input.ToString(), cmdLineArgs);
}
