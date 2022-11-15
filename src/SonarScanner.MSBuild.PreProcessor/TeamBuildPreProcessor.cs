﻿/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor
{
    public class TeamBuildPreProcessor : ITeamBuildPreProcessor
    {
        public const string CSharpLanguage = "cs";
        private const string CSharpPluginKey = "csharp";

        public const string VBNetLanguage = "vbnet";
        private const string VBNetPluginKey = "vbnet";

        private static readonly PluginDefinition csharp = new(CSharpLanguage, CSharpPluginKey);
        private static readonly PluginDefinition vbnet = new(VBNetLanguage, VBNetPluginKey);

        private static readonly List<PluginDefinition> plugins = new() { csharp, vbnet };

        private readonly IPreprocessorObjectFactory factory;
        private readonly ILogger logger;

        public TeamBuildPreProcessor(IPreprocessorObjectFactory factory, ILogger logger)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private class PluginDefinition
        {
            public string Language { get; }
            public string PluginKey { get; }

            public PluginDefinition(string language, string pluginKey)
            {
                Language = language;
                PluginKey = pluginKey;
            }
        }

        public async Task<bool> Execute(string[] args)
        {
            logger.SuspendOutput();
            var processedArgs = ArgumentProcessor.TryProcessArgs(args, logger);

            if (processedArgs == null)
            {
                logger.ResumeOutput();
                logger.LogError(Resources.ERROR_InvalidCommandLineArgs);
                return false;
            }
            else
            {
                return await DoExecute(processedArgs);
            }
        }

        private async Task<bool> DoExecute(ProcessedArgs settings)
        {
            Debug.Assert(settings != null, "Not expecting the process arguments to be null");

            this.logger.Verbosity = VerbosityCalculator.ComputeVerbosity(settings.AggregateProperties, this.logger);
            logger.ResumeOutput();

            InstallLoaderTargets(settings);

            var teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(logger);

            // We're checking the args and environment variables so we can report all config errors to the user at once
            if (teamBuildSettings == null)
            {
                logger.LogError(Resources.ERROR_CannotPerformProcessing);
                return false;
            }

            // Create the directories
            logger.LogDebug(Resources.MSG_CreatingFolders);
            if (!Utilities.TryEnsureEmptyDirectories(logger,
                teamBuildSettings.SonarConfigDirectory,
                teamBuildSettings.SonarOutputDirectory))
            {
                return false;
            }

            var server = factory.CreateSonarQubeServer(settings);

            //TODO: fail fast after release of S4NET 6.0
            //Deprecation notice for SQ < 7.9
            await server.WarnIfSonarQubeVersionIsDeprecated();

            try
            {
                if (!await server.IsServerLicenseValid())
                {
                    logger.LogError(Resources.ERR_UnlicensedServer, settings.SonarQubeUrl);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                return false;
            }

            var argumentsAndRuleSets = await FetchArgumentsAndRulesets(server, settings, teamBuildSettings);
            if (!argumentsAndRuleSets.IsSuccess)
            {
                return false;
            }
            Debug.Assert(argumentsAndRuleSets.AnalyzersSettings != null, "Not expecting the analyzers settings to be null");

            if (PullRequestBaseBranch(settings) is { } baseBranch)
            {
                logger.LogInfo(Resources.MSG_Processing_PullRequest_Branch, baseBranch);
            }
            else
            {
                logger.LogDebug(Resources.MSG_Processing_PullRequest_NoBranch);
            }

            // analyzerSettings can be empty
            AnalysisConfigGenerator.GenerateFile(settings, teamBuildSettings, argumentsAndRuleSets.ServerSettings, argumentsAndRuleSets.AnalyzersSettings, server, logger);

            return true;
        }

        private void InstallLoaderTargets(ProcessedArgs args)
        {
            if (args.InstallLoaderTargets)
            {
                var installer = factory.CreateTargetInstaller();
                Debug.Assert(installer != null, "Factory should not return null");
                installer.InstallLoaderTargets(Directory.GetCurrentDirectory());
            }
            else
            {
                logger.LogDebug(Resources.MSG_NotCopyingTargets);
            }
        }

        private async Task<ArgumentsAndRuleSets> FetchArgumentsAndRulesets(ISonarQubeServer server, ProcessedArgs args, TeamBuildSettings settings)
        {
            ArgumentsAndRuleSets argumentsAndRuleSets = new ArgumentsAndRuleSets();

            try
            {
                logger.LogInfo(Resources.MSG_FetchingAnalysisConfiguration);

                // Respect sonar.branch setting if set
                args.TryGetSetting(SonarProperties.ProjectBranch, out var projectBranch);

                // Fetch the SonarQube project properties
                argumentsAndRuleSets.ServerSettings = await server.GetProperties(args.ProjectKey, projectBranch);

                // Fetch installed plugins
                var availableLanguages = await server.GetAllLanguages();

                foreach (var plugin in plugins)
                {
                    if (!availableLanguages.Contains(plugin.Language))
                    {
                        continue;
                    }

                    var qualityProfile = await server.TryGetQualityProfile(args.ProjectKey, projectBranch, args.Organization, plugin.Language);

                    // Fetch project quality profile
                    if (!qualityProfile.Item1)
                    {
                        logger.LogDebug(Resources.RAP_NoQualityProfile, plugin.Language, args.ProjectKey);
                        continue;
                    }

                    // Fetch rules
                    var rules = await server.GetRules(qualityProfile.Item2);
                    if (!rules.Any(x => x.IsActive))
                    {
                        logger.LogDebug(Resources.RAP_NoActiveRules, plugin.Language);
                    }

                    // Generate Roslyn analyzers settings and rulesets
                    var analyzerProvider = factory.CreateRoslynAnalyzerProvider();
                    Debug.Assert(analyzerProvider != null, "Factory should not return null");

                    // Will be null if the processing of server settings and active rules resulted in an empty ruleset

                    // Use the aggregate of local and server properties when generating the analyzer configuration
                    // See bug 699: https://github.com/SonarSource/sonar-scanner-msbuild/issues/699
                    var serverProperties = new ListPropertiesProvider(argumentsAndRuleSets.ServerSettings);
                    var allProperties = new AggregatePropertiesProvider(args.AggregateProperties, serverProperties);
                    var analyzer = analyzerProvider.SetupAnalyzer(settings, allProperties, rules, plugin.Language);

                    if (analyzer != null)
                    {
                        argumentsAndRuleSets.AnalyzersSettings.Add(analyzer);
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
                if (Utilities.HandleHostUrlWebException(ex, args.SonarQubeUrl, logger))
                {
                    argumentsAndRuleSets.IsSuccess = false;
                    return argumentsAndRuleSets;
                }

                throw;
            }
            finally
            {
                Utilities.SafeDispose(server);
            }

            argumentsAndRuleSets.IsSuccess = true;
            return argumentsAndRuleSets;
        }

        private static string PullRequestBaseBranch(ProcessedArgs localSettings) =>
            localSettings.AggregateProperties.TryGetProperty(SonarProperties.PullRequestBase, out var baseBranchProperty)
                ? baseBranchProperty.Value
                : null;

        public class ArgumentsAndRuleSets
        {
            public ArgumentsAndRuleSets()
            {
                AnalyzersSettings = new List<AnalyzerSettings>();
            }

            public bool IsSuccess { get; set; }

            public IDictionary<string, string> ServerSettings { get; set; }

            public List<AnalyzerSettings> AnalyzersSettings { get; }
        }
    }
}
