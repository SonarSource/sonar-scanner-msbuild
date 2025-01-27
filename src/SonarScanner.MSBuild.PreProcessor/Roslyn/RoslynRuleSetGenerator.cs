/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

public class RoslynRuleSetGenerator(IAnalysisPropertyProvider sonarProperties, bool deactivateAll = false)
{
    private const string SonarAnalyzerPartialRepoKey = "sonaranalyzer-{0}";
    private const string RoslynRepoPrefix = "roslyn.";
    private const string ActiveRuleText = "Warning";
    private const string InactiveRuleText = "None";

    private readonly IAnalysisPropertyProvider sonarProperties = sonarProperties ?? throw new ArgumentNullException(nameof(sonarProperties));
    private readonly bool deactivateAll = deactivateAll;

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
            .GroupBy(x => GetPartialRepoKey(x, language))
            .Where(IsSupportedRuleRepo)
            .Select(CreateRulesElement);
        ruleSet.Rules.AddRange(rulesElements);

        return ruleSet;
    }

    private static string GetPartialRepoKey(SonarRule rule, string language)
    {
        if (rule.RepoKey.StartsWith(RoslynRepoPrefix))
        {
            return rule.RepoKey.Substring(RoslynRepoPrefix.Length);
        }
        else if ("csharpsquid".Equals(rule.RepoKey) || "vbnet".Equals(rule.RepoKey))
        {
            return string.Format(SonarAnalyzerPartialRepoKey, language);
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
        new(sonarRule.RuleKey, sonarRule.IsActive && !deactivateAll ? ActiveRuleText : InactiveRuleText);

    private string GetRequiredPropertyValue(string propertyKey)
    {
        if (!sonarProperties.TryGetValue(propertyKey, out var propertyValue))
        {
            throw new AnalysisException($"Property does not exist: {propertyKey}. This property should be set by the plugin in SonarQube.");
        }
        return propertyValue;
    }
}
