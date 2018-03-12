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
using System.Linq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.Roslyn.Model
{
    public class RoslynRuleSetGenerator
    {
        private const string SONARANALYZER_PARTIAL_REPO_KEY = "sonaranalyzer-{0}";
        private const string ROSLYN_REPOSITORY_PREFIX = "roslyn.";
        private readonly IDictionary<string, string> serverSettings;

        public RoslynRuleSetGenerator(IDictionary<string, string> serverSettings)
        {
            this.serverSettings = serverSettings ?? throw new ArgumentNullException(nameof(serverSettings));
        }

        /// <summary>
        /// Generates a RuleSet that is serializable (XML).
        /// The ruleset can be empty if there are no active rules belonging to the repo keys "vbnet", "csharpsquid" or "roslyn.*".
        /// </summary>
        /// <exception cref="AnalysisException">if mandatory properties that should be associated with the repo key are missing.</exception>
        public RuleSet Generate(IEnumerable<ActiveRule> activeRules, IEnumerable<string> inactiveRules, string language)
        {
            if (activeRules == null || !activeRules.Any())
            {
                throw new ArgumentNullException(nameof(activeRules));
            }
            if (inactiveRules == null)
            {
                throw new ArgumentNullException(nameof(inactiveRules));
            }
            if (language == null)
            {
                throw new ArgumentNullException(nameof(language));
            }

            var activeRulesByPartialRepoKey = ActiveRoslynRulesByPartialRepoKey(activeRules, language);
            var inactiveRulesByRepoKey = GetInactiveRulesByRepoKey(inactiveRules);

            var ruleSet = new RuleSet
            {
                Name = "Rules for SonarQube",
                Description = "This rule set was automatically generated from SonarQube",
                ToolsVersion = "14.0"
            };

            foreach (var entry in activeRulesByPartialRepoKey)
            {
                var rules = new Rules();
                var repoKey = entry.Value.First().RepoKey;
                rules.AnalyzerId = MandatoryPropertyValue(AnalyzerIdPropertyKey(entry.Key));
                rules.RuleNamespace = MandatoryPropertyValue(RuleNamespacePropertyKey(entry.Key));

                // add active rules
                rules.RuleList = entry.Value.Select(r => new Rule(r.RuleKey, "Warning")).ToList();

                // add other
                if (inactiveRulesByRepoKey.TryGetValue(repoKey, out List<string> otherRules))
                {
                    rules.RuleList.AddRange(otherRules.Select(r => new Rule(r, "None")).ToList());
                }
                ruleSet.Rules.Add(rules);
            }

            return ruleSet;
        }

        private static Dictionary<string, List<string>> GetInactiveRulesByRepoKey(IEnumerable<string> inactiveRules)
        {
            var dict = new Dictionary<string, List<string>>();
            foreach (var r in inactiveRules)
            {
                ParseRuleKey(r, out string repo, out string key);
                AddDict(dict, repo, key);
            }
            return dict;
        }

        private static void ParseRuleKey(string keyWithRepo, out string repo, out string key)
        {
            var pos = keyWithRepo.IndexOf(':');
            repo = keyWithRepo.Substring(0, pos);
            key = keyWithRepo.Substring(pos + 1);
        }

        private static Dictionary<string, List<ActiveRule>> ActiveRoslynRulesByPartialRepoKey(IEnumerable<ActiveRule> activeRules,
            string language)
        {
            var rulesByPartialRepoKey = new Dictionary<string, List<ActiveRule>>();

            foreach (var activeRule in activeRules)
            {
                if (activeRule.RepoKey.StartsWith(ROSLYN_REPOSITORY_PREFIX))
                {
                    var pluginKey = activeRule.RepoKey.Substring(ROSLYN_REPOSITORY_PREFIX.Length);
                    AddDict(rulesByPartialRepoKey, pluginKey, activeRule);
                }
                else if ("csharpsquid".Equals(activeRule.RepoKey) || "vbnet".Equals(activeRule.RepoKey))
                {
                    AddDict(rulesByPartialRepoKey, string.Format(SONARANALYZER_PARTIAL_REPO_KEY, language), activeRule);
                }
            }

            return rulesByPartialRepoKey;
        }

        private static void AddDict<T>(Dictionary<string, List<T>> dict, string key, T value)
        {
            if (!dict.TryGetValue(key, out List<T> list))
            {
                list = new List<T>();
                dict.Add(key, list);
            }
            list.Add(value);
        }

        private static string AnalyzerIdPropertyKey(string partialRepoKey)
        {
            return partialRepoKey + ".analyzerId";
        }

        private static string RuleNamespacePropertyKey(string partialRepoKey)
        {
            return partialRepoKey + ".ruleNamespace";
        }

        private string MandatoryPropertyValue(string propertyKey)
        {
            if (propertyKey == null)
            {
                throw new ArgumentNullException(nameof(propertyKey));
            }
            if (!serverSettings.ContainsKey(propertyKey))
            {
                if (propertyKey.StartsWith(string.Format(SONARANALYZER_PARTIAL_REPO_KEY, "vbnet")))
                {
                    throw new AnalysisException("Property doesn't exist: " + propertyKey
                        + ". Possible cause: this Scanner is not compatible with SonarVB 2.X. If necessary, upgrade SonarVB to 3.0+ in SonarQube.");
                }
                else
                {
                    throw new AnalysisException("Key doesn't exist: " + propertyKey +". This property should be set by the plugin in SonarQube.");
                }
            }
            return serverSettings[propertyKey];
        }
    }
}
