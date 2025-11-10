/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

namespace SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

public class RoslynRuleSetGenerator(bool deactivateAll)
{
    private const string LegacyAttributesValue = "SonarScannerFor.NET";    // Legacy, unused, but mandatory attribute
    private const string ActiveRuleText = "Warning";
    private const string InactiveRuleText = "None";

    private readonly bool deactivateAll = deactivateAll;

    public RuleSet Generate(IEnumerable<SonarRule> rules)
    {
        _ = rules ?? throw new ArgumentNullException(nameof(rules));
        return new RuleSet
        {
            Name = "Rules for SonarQube",
            Description = "This rule set was automatically generated from SonarQube",
            ToolsVersion = "14.0",
            Rules = [CreateRules(rules)]
        };
    }

    private Rules CreateRules(IEnumerable<SonarRule> analyzerRules) =>
        new()
        {
            AnalyzerId = LegacyAttributesValue,
            RuleNamespace = LegacyAttributesValue,
            RuleList = analyzerRules.Select(CreateRule).ToList()
        };

    private Rule CreateRule(SonarRule sonarRule) =>
        new(sonarRule.RuleKey, sonarRule.IsActive && !deactivateAll ? ActiveRuleText : InactiveRuleText);
}
