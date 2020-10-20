/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2020 SonarSource SA
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

        private readonly IAnalysisPropertyProvider sonarProperties;
        private readonly string inactiveRuleActionText = GetActionText(RuleAction.None);

        private string activeRuleActionText = GetActionText(RuleAction.Warning);
        private RuleAction activeRuleAction = RuleAction.Warning;

        public RoslynRuleSetGenerator(IAnalysisPropertyProvider sonarProperties)
        {
            this.sonarProperties = sonarProperties ?? throw new ArgumentNullException(nameof(sonarProperties));
        }

        public RuleAction ActiveRuleAction
        {
            get
            {
                return activeRuleAction;
            }
            set
            {
                activeRuleAction = value;
                activeRuleActionText = GetActionText(value);
            }
        }

        /// <summary>
        /// Generates a RuleSet that is serializable (XML).
        /// The ruleset can be empty if there are no active rules belonging to the repo keys "vbnet", "csharpsquid" or "roslyn.*".
        /// </summary>
        /// <exception cref="AnalysisException">if required properties that should be associated with the repo key are missing.</exception>
        public RuleSet Generate(string language, IEnumerable<SonarRule> activeRules, IEnumerable<SonarRule> inactiveRules)
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

            var rulesElements = activeRules.Concat(inactiveRules)
                .GroupBy(
                    rule => GetPartialRepoKey(rule, language),
                    rule => rule)
                .Where(IsSupportedRuleRepo)
                .Select(CreateRulesElement);

            var ruleSet = new RuleSet
            {
                Name = "Rules for SonarQube",
                Description = "This rule set was automatically generated from SonarQube",
                ToolsVersion = "14.0"
            };

            ruleSet.Rules.AddRange(rulesElements);

            return ruleSet;
        }

        private static bool IsSupportedRuleRepo(IGrouping<string, SonarRule> analyzerRules)
        {
            var partialRepoKey = analyzerRules.Key;
            return !string.IsNullOrEmpty(partialRepoKey);
        }

        private Rules CreateRulesElement(IGrouping<string, SonarRule> analyzerRules)
        {
            var partialRepoKey = analyzerRules.Key;
            return new Rules
            {
                AnalyzerId = GetRequiredPropertyValue($"{partialRepoKey}.analyzerId"),
                RuleNamespace = GetRequiredPropertyValue($"{partialRepoKey}.ruleNamespace"),
                RuleList = analyzerRules.Select(CreateRuleElement).ToList()
            };
        }

        private Rule CreateRuleElement(SonarRule sonarRule) =>
            new Rule(sonarRule.RuleKey, sonarRule.IsActive ? activeRuleActionText : inactiveRuleActionText);

        private static string GetActionText(RuleAction ruleAction)
        {
            switch (ruleAction)
            {
                case RuleAction.None:
                    return "None";
                case RuleAction.Info:
                    return "Info";
                case RuleAction.Warning:
                    return "Warning";
                case RuleAction.Error:
                    return "Error";
                case RuleAction.Hidden:
                    return "Hidden";
                default:
                    throw new NotSupportedException($"{ruleAction} is not a supported RuleAction.");
            }
        }

        private static string GetPartialRepoKey(SonarRule rule, string language)
        {
            if (rule.RepoKey.StartsWith(ROSLYN_REPOSITORY_PREFIX))
            {
                return rule.RepoKey.Substring(ROSLYN_REPOSITORY_PREFIX.Length);
            }
            else if ("csharpsquid".Equals(rule.RepoKey) || "vbnet".Equals(rule.RepoKey))
            {
                return string.Format(SONARANALYZER_PARTIAL_REPO_KEY, language);
            }
            else
            {
                return null;
            }
        }

        private string GetRequiredPropertyValue(string propertyKey)
        {
            if (!this.sonarProperties.TryGetValue(propertyKey, out var propertyValue))
            {
                var message = $"Property does not exist: {propertyKey}. This property should be set by the plugin in SonarQube.";

                if (propertyKey.StartsWith(string.Format(SONARANALYZER_PARTIAL_REPO_KEY, "vbnet")))
                {
                    message = message + " Possible cause: this Scanner is not compatible with SonarVB 2.X. If necessary, upgrade SonarVB latest in SonarQube.";
                }

                throw new AnalysisException(message);
            }

            return propertyValue;
        }
    }
}
