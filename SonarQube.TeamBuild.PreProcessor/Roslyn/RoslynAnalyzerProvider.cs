/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

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

        private const string SONARANALYZER_PARTIAL_REPO_KEY = "sonaranalyzer-{0}";
        private const string ROSLYN_REPOSITORY_PREFIX = "roslyn.";

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

        /// <summary>
        /// Generates several files related to rulesets and roslyn analyzer assemblies.
        /// Even if a non-empty set of active rules is provided, depending on the server settings of repo keys, we might have no rules in the ruleset.
        /// In that case, this method returns null.
        /// </summary>
        private AnalyzerSettings ConfigureAnalyzer(string language, IEnumerable<ActiveRule> activeRules, IEnumerable<string> inactiveRules)
        {
            RoslynRuleSetGenerator ruleSetGenerator = new RoslynRuleSetGenerator(sqServerSettings);
            RuleSet ruleSet = ruleSetGenerator.Generate(activeRules, inactiveRules, language);
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

        /// <summary>
        /// Write ruleset to a file.
        /// Nothing will be written and null with be returned if the ruleset contains no rules
        /// </summary>
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
                content = RoslynSonarLint.GenerateXml(activeRules, sqServerSettings, language, "csharpsquid");
            }
            else
            {
                content = RoslynSonarLint.GenerateXml(activeRules, sqServerSettings, language, "vbnet");
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
            ICollection<string> repoKeys = ActiveRulesPartialRepoKey(activeRules, language);
            IList<Plugin> plugins = new List<Plugin>();

            foreach (string repoKey in repoKeys)
            {
                string pluginkey;
                string pluginVersion;
                string staticResourceName;
                if (!sqServerSettings.TryGetValue(PluginKeyPropertyKey(repoKey), out pluginkey)
                    || !sqServerSettings.TryGetValue(PluginVersionPropertyKey(repoKey), out pluginVersion)
                    || !sqServerSettings.TryGetValue(StaticResourceNamePropertyKey(repoKey), out staticResourceName))
                {
                    this.logger.LogInfo(Resources.RAP_NoAssembliesForRepo, repoKey, language);
                    continue;
                }

                plugins.Add(new Plugin(pluginkey, pluginVersion, staticResourceName));
            }

            IEnumerable<string> analyzerAssemblyPaths = null;
            if (plugins.Count == 0)
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

        private static string PluginKeyPropertyKey(string partialRepoKey)
        {
            return partialRepoKey + ".pluginKey";
        }

        private static string PluginVersionPropertyKey(string partialRepoKey)
        {
            return partialRepoKey + ".pluginVersion";
        }

        private static string StaticResourceNamePropertyKey(string partialRepoKey)
        {
            return partialRepoKey + ".staticResourceName";
        }

        private static ICollection<string> ActiveRulesPartialRepoKey(IEnumerable<ActiveRule> activeRules, string language)
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
