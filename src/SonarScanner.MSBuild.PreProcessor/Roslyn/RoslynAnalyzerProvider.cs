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
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;
using SonarScanner.MSBuild.TFS;

namespace SonarScanner.MSBuild.PreProcessor.Roslyn
{
    public class RoslynAnalyzerProvider : IAnalyzerProvider
    {
        public const string RoslynRulesetFileName = "SonarQubeRoslyn-{0}.ruleset";

        private const string SONARANALYZER_PARTIAL_REPO_KEY_PREFIX = "sonaranalyzer-";
        private const string SONARANALYZER_PARTIAL_REPO_KEY = SONARANALYZER_PARTIAL_REPO_KEY_PREFIX + "{0}";
        private const string ROSLYN_REPOSITORY_PREFIX = "roslyn.";

        public const string CSharpLanguage = "cs";
        public const string CSharpPluginKey = "csharp";
        public const string CSharpRepositoryKey = "csharp";

        public const string VBNetLanguage = "vbnet";
        public const string VBNetPluginKey = "vbnet";
        public const string VBNetRepositoryKey = "vbnet";

        private readonly IAnalyzerInstaller analyzerInstaller;
        private readonly ILogger logger;
        private TeamBuildSettings teamBuildSettings;
        private IAnalysisPropertyProvider sonarProperties;

        public RoslynAnalyzerProvider(IAnalyzerInstaller analyzerInstaller, ILogger logger)
        {
            this.analyzerInstaller = analyzerInstaller ?? throw new ArgumentNullException(nameof(analyzerInstaller));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Generates several files related to rulesets and roslyn analyzer assemblies.
        /// Active rules should never be empty, but depending on the server settings of repo keys, we might have no rules in the ruleset.
        /// In that case, this method returns null.
        /// </summary>
        public AnalyzerSettings SetupAnalyzer(TeamBuildSettings teamBuildSettings, IAnalysisPropertyProvider sonarProperties,
            IEnumerable<SonarRule> activeRules, IEnumerable<SonarRule> inactiveRules, string language)
        {
            this.teamBuildSettings = teamBuildSettings ?? throw new ArgumentNullException(nameof(teamBuildSettings));
            this.sonarProperties = sonarProperties ?? throw new ArgumentNullException(nameof(sonarProperties));

            if (language == null)
            {
                throw new ArgumentNullException(nameof(language));
            }

            if (inactiveRules == null)
            {
                throw new ArgumentNullException(nameof(inactiveRules));
            }

            if (activeRules == null)
            {
                throw new ArgumentNullException(nameof(activeRules));
            }

            var rulesetFilePath = CreateRuleSet(language, activeRules, inactiveRules, ProjectType.Product);
            var testProjectRuleSetFilePath = CreateRuleSet(language, activeRules, inactiveRules, ProjectType.Test);
            var analyzerPlugins = FetchAnalyzerPlugins(language, activeRules);
            var additionalFiles = WriteAdditionalFiles(language, activeRules);

            return new AnalyzerSettings(language, rulesetFilePath, testProjectRuleSetFilePath, analyzerPlugins, additionalFiles);
        }

        public static string GetRoslynRulesetFileName(string language, ProjectType projectType)
        {
            var testSuffix = projectType == ProjectType.Test ? "-test" : string.Empty;
            return string.Format(RoslynRulesetFileName, $"{language}{testSuffix}");
        }

        private string CreateRuleSet(string language, IEnumerable<SonarRule> activeRules, IEnumerable<SonarRule> inactiveRules, ProjectType projectType)
        {
            var ruleSetGenerator = new RoslynRuleSetGenerator(this.sonarProperties);
            if (projectType == ProjectType.Test)
            {
                ruleSetGenerator.ActiveRuleAction = RuleAction.None;
            }

            var ruleSet = ruleSetGenerator.Generate(language, activeRules, inactiveRules);

            Debug.Assert(ruleSet != null, "Expecting the RuleSet to be created.");
            Debug.Assert(ruleSet.Rules != null, "Expecting the RuleSet.Rules to be initialized.");

            var rulesetFilePath = Path.Combine(
                this.teamBuildSettings.SonarConfigDirectory,
                GetRoslynRulesetFileName(language, projectType));

            this.logger.LogDebug(Resources.RAP_UnpackingRuleset, rulesetFilePath);

            ruleSet.Save(rulesetFilePath);

            return rulesetFilePath;
        }

        private IEnumerable<string> WriteAdditionalFiles(string language, IEnumerable<SonarRule> activeRules)
        {
            Debug.Assert(activeRules != null, "Supplied active rules should not be null");

            if (TryWriteSonarLintXmlFile(language, activeRules, out var filePath))
            {
                Debug.Assert(File.Exists(filePath), "Expecting the additional file to exist: {0}", filePath);
                return new[] { filePath };
            }

            return Enumerable.Empty<string>();
        }

        private bool TryWriteSonarLintXmlFile(string language, IEnumerable<SonarRule> activeRules, out string sonarLintXmlPath)
        {
            sonarLintXmlPath = null;

            var langDir = Path.Combine(this.teamBuildSettings.SonarConfigDirectory, language);
            Directory.CreateDirectory(langDir);

            sonarLintXmlPath = Path.Combine(langDir, "SonarLint.xml");
            if (File.Exists(sonarLintXmlPath))
            {
                this.logger.LogDebug(Resources.RAP_AdditionalFileAlreadyExists, language, sonarLintXmlPath);
                return false;
            }

            var content = RoslynSonarLint.GenerateXml(activeRules, sonarProperties, language);

            this.logger.LogDebug(Resources.RAP_WritingAdditionalFile, sonarLintXmlPath);

            File.WriteAllText(sonarLintXmlPath, content);

            return true;
        }

        private IEnumerable<AnalyzerPlugin> FetchAnalyzerPlugins(string language, IEnumerable<SonarRule> activeRules)
        {
            var partialRepoKeys = ActiveRulesPartialRepoKeys(activeRules);
            IList<Plugin> plugins = new List<Plugin>();

            foreach (var partialRepoKey in partialRepoKeys)
            {
                if (!sonarProperties.TryGetValue($"{partialRepoKey}.pluginKey", out var pluginkey) ||
                    !sonarProperties.TryGetValue($"{partialRepoKey}.pluginVersion", out var pluginVersion) ||
                    !sonarProperties.TryGetValue($"{partialRepoKey}.staticResourceName", out var staticResourceName))
                {
                    if (!partialRepoKey.StartsWith(SONARANALYZER_PARTIAL_REPO_KEY_PREFIX))
                    {
                        this.logger.LogInfo(Resources.RAP_NoAssembliesForRepo, partialRepoKey, language);
                    }
                    continue;
                }

                plugins.Add(new Plugin(pluginkey, pluginVersion, staticResourceName));
            }

            if (plugins.Count == 0)
            {
                this.logger.LogInfo(Resources.RAP_NoAnalyzerPluginsSpecified, language);
                return Enumerable.Empty<AnalyzerPlugin>();
            }
            else
            {
                this.logger.LogInfo(Resources.RAP_ProvisioningAnalyzerAssemblies, language);
                return this.analyzerInstaller.InstallAssemblies(plugins);
            }
        }

        private static ICollection<string> ActiveRulesPartialRepoKeys(IEnumerable<SonarRule> rules)
        {
            var partialRepoKeys = new HashSet<string>
            {
                // Always add SonarC# and SonarVB to have at least tokens...
                string.Format(SONARANALYZER_PARTIAL_REPO_KEY, "cs"),
                string.Format(SONARANALYZER_PARTIAL_REPO_KEY, "vbnet")
            };

            // Add the Roslyn SDK rules' partial repo keys, if any
            partialRepoKeys.UnionWith(
                rules
                    .Where(rule => rule.RepoKey.StartsWith(ROSLYN_REPOSITORY_PREFIX))
                    .Select(rule => rule.RepoKey.Substring(ROSLYN_REPOSITORY_PREFIX.Length)));

            return partialRepoKeys;
        }
    }
}
