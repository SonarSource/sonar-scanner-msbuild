/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.TFS;

namespace SonarScanner.MSBuild.PreProcessor
{
    public class TeamBuildPreProcessor : ITeamBuildPreProcessor
    {
        public const string CSharpLanguage = "cs";
        public const string CSharpPluginKey = "csharp";

        public const string VBNetLanguage = "vbnet";
        public const string VBNetPluginKey = "vbnet";

        private static readonly PluginDefinition csharp = new PluginDefinition(CSharpLanguage, CSharpPluginKey);
        private static readonly PluginDefinition vbnet = new PluginDefinition(VBNetLanguage, VBNetPluginKey);

        private static readonly List<PluginDefinition> plugins = new List<PluginDefinition>
        {
            csharp,
            vbnet
        };

        private readonly IPreprocessorObjectFactory factory;
        private readonly ILogger logger;

        #region Constructor(s)

        public TeamBuildPreProcessor(IPreprocessorObjectFactory factory, ILogger logger)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion Constructor(s)

        #region Inner class

        private class PluginDefinition
        {
            public string Language { get; private set; }
            public string PluginKey { get; private set; }

            public PluginDefinition(string language, string pluginKey)
            {
                Language = language;
                PluginKey = pluginKey;
            }
        }

        #endregion Inner class

        #region Public methods

        public async Task<bool> Execute(string[] args)
        {
            this.logger.SuspendOutput();
            var processedArgs = ArgumentProcessor.TryProcessArgs(args, this.logger);

            if (processedArgs == null)
            {
                this.logger.ResumeOutput();
                this.logger.LogError(Resources.ERROR_InvalidCommandLineArgs);
                return false;
            }
            else
            {
                return await DoExecute(processedArgs);
            }
        }

        private async Task<bool> DoExecute(ProcessedArgs localSettings)
        {
            Debug.Assert(localSettings != null, "Not expecting the process arguments to be null");

            this.logger.Verbosity = VerbosityCalculator.ComputeVerbosity(localSettings.AggregateProperties, this.logger);
            this.logger.ResumeOutput();

            InstallLoaderTargets(localSettings);

            var teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(this.logger);

            // We're checking the args and environment variables so we can report all config errors to the user at once
            if (teamBuildSettings == null)
            {
                this.logger.LogError(Resources.ERROR_CannotPerformProcessing);
                return false;
            }

            // Create the directories
            this.logger.LogDebug(Resources.MSG_CreatingFolders);
            if (!Utilities.TryEnsureEmptyDirectories(this.logger,
                teamBuildSettings.SonarConfigDirectory,
                teamBuildSettings.SonarOutputDirectory))
            {
                return false;
            }

            var server = this.factory.CreateSonarQubeServer(localSettings);
            var argumentsAndRuleSets = await FetchArgumentsAndRulesets(server, localSettings, teamBuildSettings);
            if (!argumentsAndRuleSets.IsSuccess)
            {
                return false;
            }
            Debug.Assert(argumentsAndRuleSets.AnalyzersSettings != null, "Not expecting the analyzers settings to be null");

            // analyzerSettings can be empty
            AnalysisConfigGenerator.GenerateFile(localSettings, teamBuildSettings, argumentsAndRuleSets.ServerSettings, argumentsAndRuleSets.AnalyzersSettings, server, this.logger);

            return true;
        }

        #endregion Public methods

        #region Private methods

        private void InstallLoaderTargets(ProcessedArgs args)
        {
            if (args.InstallLoaderTargets)
            {
                var installer = this.factory.CreateTargetInstaller();
                Debug.Assert(installer != null, "Factory should not return null");
                installer.InstallLoaderTargets(Directory.GetCurrentDirectory());
            }
            else
            {
                this.logger.LogDebug(Resources.MSG_NotCopyingTargets);
            }
        }

        private async Task<ArgumentsAndRuleSets> FetchArgumentsAndRulesets(ISonarQubeServer server, ProcessedArgs args, TeamBuildSettings settings)
        {
            ArgumentsAndRuleSets argumentsAndRuleSets = new ArgumentsAndRuleSets();

            try
            {
                this.logger.LogInfo(Resources.MSG_FetchingAnalysisConfiguration);

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
                        this.logger.LogDebug(Resources.RAP_NoQualityProfile, plugin.Language, args.ProjectKey);
                        continue;
                    }

                    // Fetch rules (active and not active)
                    var activeRules = await server.GetActiveRules(qualityProfile.Item2);

                    if (!activeRules.Any())
                    {
                        this.logger.LogDebug(Resources.RAP_NoActiveRules, plugin.Language);
                    }

                    var inactiveRules = await server.GetInactiveRules(qualityProfile.Item2, plugin.Language);

                    // Generate Roslyn analyzers settings and rulesets
                    var analyzerProvider = this.factory.CreateRoslynAnalyzerProvider();
                    Debug.Assert(analyzerProvider != null, "Factory should not return null");

                    // Will be null if the processing of server settings and active rules resulted in an empty ruleset

                    // Use the aggregate of local and server properties when generating the analyzer configuration
                    // See bug 699: https://github.com/SonarSource/sonar-scanner-msbuild/issues/699
                    var serverProperties = new ListPropertiesProvider(argumentsAndRuleSets.ServerSettings);
                    var allProperties = new AggregatePropertiesProvider(args.AggregateProperties, serverProperties);
                    var analyzer = analyzerProvider.SetupAnalyzer(settings, allProperties, activeRules, inactiveRules, plugin.Language);

                    if (analyzer != null)
                    {
                        argumentsAndRuleSets.AnalyzersSettings.Add(analyzer);
                    }
                }
            }
            catch (WebException ex)
            {
                if (Utilities.HandleHostUrlWebException(ex, args.SonarQubeUrl, this.logger))
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

        #endregion Private methods

        public class ArgumentsAndRuleSets
        {
            public ArgumentsAndRuleSets()
            {
                AnalyzersSettings = new List<AnalyzerSettings>();
            }

            public bool IsSuccess { get; set; }

            public IDictionary<string, string> ServerSettings { get; set; }

            public List<AnalyzerSettings> AnalyzersSettings { get; set; }
        }
    }
}
