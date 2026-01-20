/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

using System.Net;
using SonarScanner.MSBuild.PreProcessor.AnalysisConfigProcessing;

namespace SonarScanner.MSBuild.PreProcessor;

public class PreProcessor
{
    private const string CSharpLanguage = "cs";
    private const string VBNetLanguage = "vbnet";

    private static readonly string[] Languages = [CSharpLanguage, VBNetLanguage];

    private readonly IPreprocessorObjectFactory factory;
    private readonly IRuntime runtime;

    public PreProcessor(IPreprocessorObjectFactory factory, IRuntime runtime)
    {
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public virtual async Task<bool> Execute(IEnumerable<string> args)
    {
        runtime.Logger.SuspendOutput(); // Wait for the correct verbosity to be calculated
        var buildSettings = BuildSettings.GetSettingsFromEnvironment();
        var processedArgs = ArgumentProcessor.TryProcessArgs(args, buildSettings, runtime);

        if (processedArgs is null)
        {
            runtime.Logger.ResumeOutput();
            runtime.LogError(Resources.ERROR_InvalidCommandLineArgs);
            return false;
        }
        else
        {
            return await DoExecute(buildSettings, processedArgs);
        }
    }

    private async Task<bool> DoExecute(BuildSettings buildSettings, ProcessedArgs localSettings)
    {
        Debug.Assert(localSettings is not null, "Not expecting the process arguments to be null");
        runtime.Logger.Verbosity = VerbosityCalculator.ComputeVerbosity(localSettings.AggregateProperties, runtime.Logger);
        runtime.Logger.ResumeOutput();
        InstallLoaderTargets(localSettings);

        // Create the directories
        runtime.LogDebug(Resources.MSG_CreatingFolders);
        if (!Utilities.TryEnsureEmptyDirectories(runtime.Logger, buildSettings.SonarConfigDirectory, buildSettings.SonarOutputDirectory))
        {
            return false;
        }

        using var server = await factory.CreateSonarWebServer(localSettings);
        try
        {
            if (server is null
                || !server.IsServerVersionSupported()
                || !await server.IsServerLicenseValid())
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            runtime.LogError(ex.Message);
            runtime.LogDebug(ex.StackTrace);
            return false;
        }
        runtime.Telemetry[TelemetryKeys.ServerInfoVersion] = server.ServerVersion.ToString();

        var jreResolver = factory.CreateJreResolver(server, localSettings.UserHome);
        var resolvedJavaExePath = await jreResolver.ResolvePath(localSettings);

        var scannerEngineJarPath = localSettings.UseSonarScannerCli ? null : await factory.CreateEngineResolver(server, localSettings.UserHome).ResolvePath(localSettings);

        var scannerCliPath = scannerEngineJarPath is null ? await factory.CreateScannerCliResolver(server, localSettings.UserHome).ResolvePath(localSettings) : null;
        if (scannerEngineJarPath is null && scannerCliPath is null)
        {
            runtime.LogError(Resources.ERR_ScannerCliDownloadFailed);
            return false;
        }

        var argumentsAndRuleSets = await FetchArgumentsAndRuleSets(server, localSettings, buildSettings);
        if (!argumentsAndRuleSets.IsSuccess)
        {
            return false;
        }
        Debug.Assert(argumentsAndRuleSets.AnalyzersSettings is not null, "Not expecting the analyzers settings to be null");

        using var cache = new CacheProcessor(server, localSettings, buildSettings, runtime.Logger);
        await cache.Execute();
        var additionalSettings = new Dictionary<string, string>
        {
            { nameof(cache.UnchangedFilesPath), cache.UnchangedFilesPath },
            { SonarProperties.PullRequestCacheBasePath, cache.PullRequestCacheBasePath }
        };
        AnalysisConfigGenerator.GenerateFile(
            localSettings,
            buildSettings,
            additionalSettings,
            argumentsAndRuleSets.ServerSettings,
            argumentsAndRuleSets.AnalyzersSettings,
            server.ServerVersion.ToString(),
            resolvedJavaExePath,
            scannerEngineJarPath,
            scannerCliPath,
            runtime);

        runtime.AnalysisWarnings.Write(buildSettings.SonarOutputDirectory); // Create the analysis warnings file to be picked up the plugin
        TelemetryUtils.AddCIEnvironmentTelemetry(runtime.Telemetry);
        runtime.Telemetry.Write(buildSettings.SonarOutputDirectory);
        return true;
    }

    private void InstallLoaderTargets(ProcessedArgs args)
    {
        if (args.InstallLoaderTargets)
        {
            new TargetsInstaller(runtime).InstallLoaderTargets(runtime.Directory.GetCurrentDirectory());
        }
        else
        {
            runtime.LogDebug(Resources.MSG_NotCopyingTargets);
        }
    }

    private async Task<ArgumentsAndRuleSets> FetchArgumentsAndRuleSets(ISonarWebServer server, ProcessedArgs args, BuildSettings settings)
    {
        var argumentsAndRuleSets = new ArgumentsAndRuleSets();

        try
        {
            runtime.LogInfo(Resources.MSG_FetchingAnalysisConfiguration);

            args.TryGetSetting(SonarProperties.ProjectBranch, out var projectBranch);
            argumentsAndRuleSets.ServerSettings = await server.DownloadProperties(args.ProjectKey, projectBranch);
            var availableLanguages = await server.DownloadAllLanguages();
            var knownLanguages = Languages.Where(availableLanguages.Contains).ToList();
            if (knownLanguages.Count == 0)
            {
                runtime.LogError(Resources.ERR_DotNetAnalyzersNotFound);
                argumentsAndRuleSets.IsSuccess = false;
                return argumentsAndRuleSets;
            }

            foreach (var language in knownLanguages)
            {
                var qualityProfile = await server.DownloadQualityProfile(args.ProjectKey, projectBranch, language);
                if (qualityProfile is null)
                {
                    runtime.LogDebug(Resources.RAP_NoQualityProfile, language, args.ProjectKey);
                    continue;
                }

                var rules = await server.DownloadRules(qualityProfile);
                if (!rules.Any(x => x.IsActive))
                {
                    runtime.LogDebug(Resources.RAP_NoActiveRules, language);
                }

                // Generate Roslyn analyzers settings and rulesets
                // It is null if the processing of server settings and active rules resulted in an empty ruleset
                var localCacheTempPath = args.SettingOrDefault(SonarProperties.PluginCacheDirectory, string.Empty);

                // Use the aggregate of local and server properties when generating the analyzer configuration
                // See bug 699: https://github.com/SonarSource/sonar-scanner-msbuild/issues/699
                var serverProperties = new ListPropertiesProvider(argumentsAndRuleSets.ServerSettings);
                var allProperties = new AggregatePropertiesProvider(args.AggregateProperties, serverProperties);

                var analyzerProvider = factory.CreateRoslynAnalyzerProvider(server, localCacheTempPath, settings, allProperties, rules, language);
                if (analyzerProvider.SetupAnalyzer() is { } analyzerSettings)
                {
                    argumentsAndRuleSets.AnalyzersSettings.Add(analyzerSettings);
                }
            }
        }
        catch (AnalysisException)
        {
            argumentsAndRuleSets.IsSuccess = false;
            return argumentsAndRuleSets;
        }
        catch (WebException ex)
        {
            if (Utilities.HandleHostUrlWebException(ex, args.ServerInfo.ServerUrl, runtime.Logger))
            {
                argumentsAndRuleSets.IsSuccess = false;
                return argumentsAndRuleSets;
            }

            throw;
        }

        argumentsAndRuleSets.IsSuccess = true;
        return argumentsAndRuleSets;
    }

    private sealed class ArgumentsAndRuleSets
    {
        public bool IsSuccess { get; set; }
        public IDictionary<string, string> ServerSettings { get; set; }
        public List<AnalyzerSettings> AnalyzersSettings { get; } = [];
    }
}
