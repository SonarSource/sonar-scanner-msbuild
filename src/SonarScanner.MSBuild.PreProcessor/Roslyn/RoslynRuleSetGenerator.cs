/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.Linq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

public class RoslynRuleSetGenerator
{
    private const string SONARANALYZER_PARTIAL_REPO_KEY = "sonaranalyzer-{0}";
    private const string ROSLYN_REPOSITORY_PREFIX = "roslyn.";
    private const string activeRuleText = "Warning";
    private const string inactiveRuleText = "None";

    private readonly IAnalysisPropertyProvider sonarProperties;
    private readonly bool deactivateAll;

    public RoslynRuleSetGenerator(IAnalysisPropertyProvider sonarProperties, bool deactivateAll = false)
    {
        this.sonarProperties = sonarProperties ?? throw new ArgumentNullException(nameof(sonarProperties));
        this.deactivateAll = deactivateAll;
    }

    /// <summary>
    /// Generates a RuleSet that is serializable (XML).
    /// The ruleset can be empty if there are no active rules belonging to the repo keys "vbnet", "csharpsquid" or "roslyn.*".
    /// </summary>
    /// <exception cref="AnalysisException">if required properties that should be associated with the repo key are missing.</exception>
    public RuleSet Generate(string language, IEnumerable<SonarRule> rules)
    {
        _ = language ?? throw new ArgumentNullException(nameof(language));
        _ = rules ?? throw new ArgumentNullException(nameof(rules));

        var ruleSet = new RuleSet
        {
            Name = "Rules for SonarQube",
            Description = "This rule set was automatically generated from SonarQube",
            ToolsVersion = "14.0"
        };

        var rulesElements = rules
            .GroupBy(rule => GetPartialRepoKey(rule, language))
            .Where(IsSupportedRuleRepo)
            .Select(CreateRulesElement);
        ruleSet.Rules.AddRange(rulesElements);

        return ruleSet;
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

    private static bool IsSupportedRuleRepo(IGrouping<string, SonarRule> analyzerRules) =>
        !string.IsNullOrEmpty(analyzerRules.Key);

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
        new Rule(sonarRule.RuleKey, sonarRule.IsActive && !this.deactivateAll ? activeRuleText : inactiveRuleText);

    private string GetRequiredPropertyValue(string propertyKey)
    {
        if (!this.sonarProperties.TryGetValue(propertyKey, out var propertyValue))
        {
            throw new AnalysisException($"Property does not exist: {propertyKey}. This property should be set by the plugin in SonarQube.");
        }
        return propertyValue;
    }
}
