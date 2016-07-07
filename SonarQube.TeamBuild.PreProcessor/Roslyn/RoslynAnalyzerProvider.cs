//-----------------------------------------------------------------------
// <copyright file="RoslynAnalyzerProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    public class RoslynAnalyzerProvider : IAnalyzerProvider
    {
        public const string RoslynFormatNamePrefix = "roslyn-{0}";
        public const string RoslynRulesetFileName = "SonarQubeRoslyn-{0}.ruleset";

        private static readonly string SONARANALYZER_PARTIAL_REPO_KEY = "sonaranalyzer-{0}";
        private static readonly string ROSLYN_REPOSITORY_PREFIX = "roslyn.";

        public const string CSharpLanguage = "cs";
        public const string CSharpPluginKey = "csharp";
        public const string CSharpRepositoryKey = "csharp";

        public const string VBNetLanguage = "vbnet";
        public const string VBNetPluginKey = "vbnet";
        public const string VBNetRepositoryKey = "vbnet";

        private readonly IAnalyzerInstaller analyzerInstaller;
        private readonly ILogger logger;
        private TeamBuildSettings sqSettings;
        private IDictionary<string, string> sqServerSettings;

        #region Public methods

        public RoslynAnalyzerProvider(IAnalyzerInstaller analyzerInstaller, ILogger logger)
        {
            if (analyzerInstaller == null)
            {
                throw new ArgumentNullException(nameof(analyzerInstaller));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            this.analyzerInstaller = analyzerInstaller;
            this.logger = logger;
        }

        public AnalyzerSettings SetupAnalyzer(TeamBuildSettings settings, IDictionary<string, string> serverSettings,
            IEnumerable<ActiveRule> activeRules, IEnumerable<string> inactiveRules, string language)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            if (language == null)
            {
                throw new ArgumentNullException(nameof(language));
            }
            if (serverSettings == null)
            {
                throw new ArgumentNullException(nameof(serverSettings));
            }
            if (inactiveRules == null)
            {
                throw new ArgumentNullException(nameof(inactiveRules));
            }
            if (activeRules == null)
            {
                throw new ArgumentNullException(nameof(activeRules));
            }
            if (!activeRules.Any())
            {
                return null;
            }

            this.sqSettings = settings;
            this.sqServerSettings = serverSettings;

            AnalyzerSettings analyzer = ConfigureAnalyzer(language, activeRules, inactiveRules);
            if (analyzer == null)
            {
                logger.LogDebug(Resources.RAP_NoPluginInstalled);
            }

            return analyzer;
        }

        public static string GetRoslynFormatName(string language)
        {
            return string.Format(RoslynFormatNamePrefix, language);
        }

        public static string GetRoslynRulesetFileName(string language)
        {
            return string.Format(RoslynRulesetFileName, language);
        }
        #endregion

        #region Private methods

        private AnalyzerSettings ConfigureAnalyzer(string language, IEnumerable<ActiveRule> activeRules, IEnumerable<string> inactiveRules)
        {
            RoslynRuleSetGenerator ruleSetGenerator = new RoslynRuleSetGenerator(sqServerSettings, logger);
            RuleSet ruleSet = ruleSetGenerator.generate(activeRules, inactiveRules, language);
            string rulesetFilePath = this.WriteRuleset(ruleSet, language);
            if (rulesetFilePath == null)
            {
                return null;
            }

            IEnumerable<string> additionalFiles = this.WriteAdditionalFiles(language, activeRules);
            IEnumerable<string> analyzersAssemblies = this.FetchAnalyzerAssemblies(activeRules, language);

            AnalyzerSettings compilerConfig = new AnalyzerSettings(language, rulesetFilePath,
                analyzersAssemblies ?? Enumerable.Empty<string>(),
                additionalFiles ?? Enumerable.Empty<string>());
            return compilerConfig;
        }

        public string WriteRuleset(RuleSet ruleSet, string language)
        {
            string rulesetFilePath = null;
            if (ruleSet == null || ruleSet.Rules == null || !ruleSet.Rules.Any())
            {
                this.logger.LogDebug(Resources.RAP_ProfileDoesNotContainRuleset);
            }
            else
            {
                rulesetFilePath = GetRulesetFilePath(this.sqSettings, language);
                this.logger.LogDebug(Resources.RAP_UnpackingRuleset, rulesetFilePath);

                ruleSet.Save(rulesetFilePath);
            }
            return rulesetFilePath;
        }

        private static string GetRulesetFilePath(TeamBuildSettings settings, string language)
        {
            return Path.Combine(settings.SonarConfigDirectory, GetRoslynRulesetFileName(language));
        }

        private IEnumerable<string> WriteAdditionalFiles(string language, IEnumerable<ActiveRule> activeRules)
        {
            Debug.Assert(activeRules != null, "Supplied active rules should not be null");

            List<string> additionalFiles = new List<string>();
            string filePath = WriteSonarLintXmlFile(language, activeRules);
            if (filePath != null)
            {
                Debug.Assert(File.Exists(filePath), "Expecting the additional file to exist: {0}", filePath);
                additionalFiles.Add(filePath);
            }

            return additionalFiles;
        }

        private string WriteSonarLintXmlFile(string language, IEnumerable<ActiveRule> activeRules)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                this.logger.LogDebug(Resources.RAP_AdditionalFileNameMustBeSpecified);
                return null;
            }

            string content;
            if (language.Equals(CSharpLanguage))
            {
                content = RoslynSonarLint.generateXml(activeRules, "csharpsquid");
            }
            else
            {
                content = RoslynSonarLint.generateXml(activeRules, "vbnet");
            }

            string langDir = Path.Combine(this.sqSettings.SonarConfigDirectory, language);
            Directory.CreateDirectory(langDir);

            string fullPath = Path.Combine(langDir, "SonarLint.xml");
            if (File.Exists(fullPath))
            {
                this.logger.LogDebug(Resources.RAP_AdditionalFileAlreadyExists, language, fullPath);
                return null;
            }

            this.logger.LogDebug(Resources.RAP_WritingAdditionalFile, fullPath);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        public IEnumerable<string> FetchAnalyzerAssemblies(IEnumerable<ActiveRule> activeRules, string language)
        {
            ICollection<string> repoKeys = activeRulesPartialRepoKey(activeRules, language);
            IList<Plugin> plugins = new List<Plugin>();

            foreach (string repoKey in repoKeys)
            {
                string pluginkey;
                string pluginVersion;
                string staticResourceName;
                if (!sqServerSettings.TryGetValue(pluginKeyPropertyKey(repoKey), out pluginkey)
                    || !sqServerSettings.TryGetValue(pluginVersionPropertyKey(repoKey), out pluginVersion)
                    || !sqServerSettings.TryGetValue(staticResourceNamePropertyKey(repoKey), out staticResourceName)) {
                    this.logger.LogInfo(Resources.RAP_NoAssembliesForRepo, repoKey, language);
                    continue;
                }

                plugins.Add(new Plugin(pluginkey, pluginVersion, staticResourceName));
            }

            IEnumerable<string> analyzerAssemblyPaths = null;
            if (plugins == null || plugins.Count == 0)
            {
                this.logger.LogInfo(Resources.RAP_NoAnalyzerPluginsSpecified, language);
            }
            else
            {
                this.logger.LogInfo(Resources.RAP_ProvisioningAnalyzerAssemblies, language);
                analyzerAssemblyPaths = this.analyzerInstaller.InstallAssemblies(plugins);
            }
            return analyzerAssemblyPaths;
        }

        private static String pluginKeyPropertyKey(String partialRepoKey)
        {
            return partialRepoKey + ".pluginKey";
        }

        private static String pluginVersionPropertyKey(String partialRepoKey)
        {
            return partialRepoKey + ".pluginVersion";
        }

        private static String staticResourceNamePropertyKey(String partialRepoKey)
        {
            return partialRepoKey + ".staticResourceName";
        }

        private static ICollection<string> activeRulesPartialRepoKey(IEnumerable<ActiveRule> activeRules, string language)
        {
            ISet<string> list = new HashSet<string>();

            foreach (ActiveRule activeRule in activeRules)
            {
                if (activeRule.RepoKey.StartsWith(ROSLYN_REPOSITORY_PREFIX))
                {
                    list.Add(activeRule.RepoKey.Substring(ROSLYN_REPOSITORY_PREFIX.Length));
                }
                else if ("csharpsquid".Equals(activeRule.RepoKey) || "vbnet".Equals(activeRule.RepoKey))
                {
                    list.Add(string.Format(SONARANALYZER_PARTIAL_REPO_KEY, language));
                }
            }

            return list;
        }

        #endregion
    }
}
