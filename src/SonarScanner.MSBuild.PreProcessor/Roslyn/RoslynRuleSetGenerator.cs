/*
 * SonarScanner for MSBuild
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
using SonarQube.Client.Models;
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
        public RuleSet Generate(IEnumerable<SonarQubeRule> activeRules, IEnumerable<SonarQubeRule> inactiveRules, string language)
        {
            if (activeRules == null)
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

            var allRulesByPartialRepoKey = RoslynRulesByPartialRepoKey(activeRules, language)
                .Concat(RoslynRulesByPartialRepoKey(inactiveRules, language))
                .GroupBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.SelectMany(y => y.Value));

            var ruleSet = new RuleSet
            {
                Name = "Rules for SonarQube",
                Description = "This rule set was automatically generated from SonarQube",
                ToolsVersion = "14.0"
            };

            foreach (var entry in allRulesByPartialRepoKey)
            {
                var rules = new Rules
                {
                    AnalyzerId = MandatoryPropertyValue(AnalyzerIdPropertyKey(entry.Key)),
                    RuleNamespace = MandatoryPropertyValue(RuleNamespacePropertyKey(entry.Key)),
                    RuleList = entry.Value.Select(r => new Rule(r.Key, r.IsActive ? "Warning" : "None")).ToList()
                };

                ruleSet.Rules.Add(rules);
            }

            return ruleSet;
        }

        private static Dictionary<string, List<SonarQubeRule>> RoslynRulesByPartialRepoKey(IEnumerable<SonarQubeRule> rules,
            string language)
        {
            var rulesByPartialRepoKey = new Dictionary<string, List<SonarQubeRule>>();

            foreach (var rule in rules)
            {
                if (rule.RepositoryKey.StartsWith(ROSLYN_REPOSITORY_PREFIX))
                {
                    var pluginKey = rule.RepositoryKey.Substring(ROSLYN_REPOSITORY_PREFIX.Length);
                    AddDict(rulesByPartialRepoKey, pluginKey, rule);
                }
                else if ("csharpsquid".Equals(rule.RepositoryKey) || "vbnet".Equals(rule.RepositoryKey))
                {
                    AddDict(rulesByPartialRepoKey, string.Format(SONARANALYZER_PARTIAL_REPO_KEY, language), rule);
                }
            }

            return rulesByPartialRepoKey;
        }

        private static void AddDict<T>(Dictionary<string, List<T>> dict, string key, T value)
        {
            if (!dict.TryGetValue(key, out var list))
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
            if (!this.serverSettings.ContainsKey(propertyKey))
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
            return this.serverSettings[propertyKey];
        }
    }
}
