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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class RoslynRuleSetGeneratorTests
{
    [TestMethod]
    public void RoslynRuleSet_ConstructorArgumentChecks()
    {
        Action act = () => new RoslynRuleSetGenerator(null);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    public void RoslynRuleSet_GeneratorArgumentChecks()
    {
        var generator = new RoslynRuleSetGenerator(new ListPropertiesProvider());
        var rules = Enumerable.Empty<SonarRule>();
        var language = "cs";

        Action act1 = () => generator.Generate(null, rules);
        act1.Should().ThrowExactly<ArgumentNullException>();

        Action act2 = () => generator.Generate(language, null);
        act2.Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    public void RoslynRuleSet_Empty()
    {
        var context = new Context([new("repo", "key")], []);
        context.RuleSet.Rules.Should().BeEmpty(); // No analyzers
        context.ValidateCommonParameters();
    }

    [TestMethod]
    public void RoslynRuleSet_ActiveRuleAction_OverrideToNone()
    {
        var context = new Context([
                new("csharpsquid", "rule1", true),
                new("csharpsquid", "rule2", true),
            ],
            null,
            true);
        context.ValidateSingleRuleList(["None", "None"]);
    }

    [TestMethod]
    public void RoslynRuleSet_ActiveRuleAction_Warning()
    {
        var context = new Context([
                new("csharpsquid", "rule1", true),
                new("csharpsquid", "rule2", true),
            ]);
        context.ValidateSingleRuleList(["Warning", "Warning"]);
    }

    [TestMethod]
    public void RoslynRuleSet_InactiveRules_None()
    {
        var context = new Context(
        [
            new("csharpsquid", "rule1", isActive: false),
            new("csharpsquid", "rule2", isActive: false),
        ]);
        context.ValidateSingleRuleList(["None", "None"]);
    }

    [TestMethod]
    public void RoslynRuleSet_Unsupported_Rules_Ignored()
    {
        var context = new Context(
        [
            new("other.repo", "other.rule1", isActive: true),
            new("other.repo", "other.rule2", isActive: false),
        ]);
        context.RuleSet.Rules.Should().BeEmpty();
    }

    [TestMethod]
    public void RoslynRuleSet_RoslynSDK_Rules_Added()
    {
        var context = new Context(
        [
            new("roslyn.custom1", "active1", true),
            new("roslyn.custom2", "active2", true),
            new("roslyn.custom1", "inactive1", false),
            new("roslyn.custom2", "inactive2", false),
        ],
        new()
        {
            ["custom1.analyzerId"] = "CustomAnalyzer1",
            ["custom1.ruleNamespace"] = "CustomNamespace1",
            ["custom2.analyzerId"] = "CustomAnalyzer2",
            ["custom2.ruleNamespace"] = "CustomNamespace2",
        });

        context.ValidateRuleSet(["CustomNamespace1", "CustomNamespace2"],
                                ["CustomAnalyzer1", "CustomAnalyzer2"],
                                [["active1", "inactive1"], ["active2", "inactive2"]],
                                [["Warning", "None"], ["Warning", "None"]]);
    }

    [TestMethod]
    public void RoslynRuleSet_Sonar_Rules_Added()
    {
        var context = new Context(
            [
                new("csharpsquid", "active1", true),
                // Even though this rule is for VB it will be added as C#, see NOTE below
                new("vbnet", "active2", true),
                new("csharpsquid", "inactive1", false),
                // Even though this rule is for VB it will be added as C#, see NOTE below
                new("vbnet", "inactive2", false),
            ]);

        // NOTE: The RuleNamespace and AnalyzerId are taken from the language parameter of the
        // Generate method. The FetchArgumentsAndRulesets method will retrieve active/inactive
        // rules from SonarQube per language/quality profile and mixture of VB-C# rules is not
        // expected.
        context.ValidateRuleSet(["SonarAnalyzer.CSharp"],
                                ["SonarAnalyzer.CSharp"],
                                [["active1", "active2", "inactive1", "inactive2"]],
                                [["Warning", "Warning", "None", "None"]]);
    }

    [TestMethod]
    public void RoslynRuleSet_Common_Parameters()
    {
        var context = new Context([], []);
        context.ValidateCommonParameters();
    }

    [TestMethod]
    public void RoslynRuleSet_AnalyzerId_Proprety_Missing()
    {
        new Action(() => new Context(
            [new("csharpsquid", "active1", true)],
            new() { { "sonaranalyzer-cs.ruleNamespace", "SonarAnalyzer.CSharp" } }))
            .Should().ThrowExactly<AnalysisException>()
            .WithMessage("Property does not exist: sonaranalyzer-cs.analyzerId. This property should be set by the plugin in SonarQube.");
    }

    [TestMethod]
    public void RoslynRuleSet_RuleNamespace_Proprety_Missing()
    {
        new Action(() => new Context(
            [new("csharpsquid", "active1", true)],
            new() { { "sonaranalyzer-cs.analyzerId", "SonarAnalyzer.CSharp" } }))
            .Should().ThrowExactly<AnalysisException>()
            .WithMessage("Property does not exist: sonaranalyzer-cs.ruleNamespace. This property should be set by the plugin in SonarQube.");
    }

    [TestMethod]
    public void RoslynRuleSet_PropertyName_IsCaseSensitive()
    {
        new Action(() => new Context(
            [new("csharpsquid", "active1", true)],
            new() { { "sonaranalyzer-cs.ANALYZERId", "SonarAnalyzer.CSharp" }, { "sonaranalyzer-cs.ruleNamespace", "SonarAnalyzer.CSharp" } }))
            .Should().ThrowExactly<AnalysisException>()
            .WithMessage("Property does not exist: sonaranalyzer-cs.analyzerId. This property should be set by the plugin in SonarQube.");
    }

    private class Context
    {
        private readonly RoslynRuleSetGenerator ruleSetGenerator;
        private readonly List<SonarRule> sonarRules;
        private readonly string language;

        public RuleSet RuleSet { get; set; }

        public Context(List<SonarRule> rules, Dictionary<string, string> properties = null, bool deactivateAll = false)
        {
            ruleSetGenerator =  new RoslynRuleSetGenerator(new ListPropertiesProvider(
                properties ?? new Dictionary<string, string>
            {
                { "sonaranalyzer-cs.analyzerId", "SonarAnalyzer.CSharp" },
                { "sonaranalyzer-cs.ruleNamespace", "SonarAnalyzer.CSharp" },
            }), deactivateAll);
            sonarRules = rules;
            language = "cs";
            RuleSet = ruleSetGenerator.Generate(language, sonarRules);
        }

        public void ValidateSingleRuleList(List<string> rule)
        {
            RuleSet.Rules.Should().ContainSingle();
            RuleSet.Rules[0].RuleList.Select(x => x.Action).Should().BeEquivalentTo(rule);
        }

        public void ValidateRuleSet(List<string> nameSpaces, List<string> analyzerIds, List<List<string>> rulesIDs, List<List<string>> rulesWarnings)
        {
            for (var i = 0; i < nameSpaces.Count; i++)
            {
                RuleSet.Rules[i].RuleNamespace.Should().Be(nameSpaces[i]);
                RuleSet.Rules[i].AnalyzerId.Should().Be(analyzerIds[i]);
                RuleSet.Rules[i].RuleList.Should().HaveCount(rulesIDs[i].Count);
                RuleSet.Rules[i].RuleList.Select(x => x.Id).Should().BeEquivalentTo(rulesIDs[i]);
                RuleSet.Rules[i].RuleList.Select(x => x.Action).Should().BeEquivalentTo(rulesWarnings[i]);
            }
        }

        public void ValidateCommonParameters()
        {
            RuleSet.Description.Should().Be("This rule set was automatically generated from SonarQube");
            RuleSet.ToolsVersion.Should().Be("14.0");
            RuleSet.Name.Should().Be("Rules for SonarQube");
        }
    }
}
