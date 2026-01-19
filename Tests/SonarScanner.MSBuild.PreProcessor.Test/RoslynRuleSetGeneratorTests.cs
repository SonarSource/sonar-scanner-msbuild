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

using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class RoslynRuleSetGeneratorTests
{
    [TestMethod]
    public void Generate_Null() =>
        ((Func<RuleSet>)(() => new RoslynRuleSetGenerator(false).Generate(null))).Should().ThrowExactly<ArgumentNullException>();

    [TestMethod]
    public void Generate_Empty()
    {
        var context = new Context([]);
        context.RuleSet.Rules.Single().RuleList.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_ActiveRule_OverrideToNone()
    {
        var context = new Context([
                new("csharpsquid", "rule1", true),
                new("csharpsquid", "rule2", true),
            ], true);
        context.ValidateRuleSet(
            new("rule1", "None"),
            new("rule2", "None"));
    }

    [TestMethod]
    public void Generate_ActiveRuleAction_Warning()
    {
        var context = new Context([
                new("csharpsquid", "rule1", true),
                new("csharpsquid", "rule2", true),
            ]);
        context.ValidateRuleSet(
            new("rule1", "Warning"),
            new("rule2", "Warning"));
    }

    [TestMethod]
    public void Generate_InactiveRules_None()
    {
        var context = new Context([
            new("csharpsquid", "rule1", false),
            new("csharpsquid", "rule2", false),
        ]);
        context.ValidateRuleSet(
            new("rule1", "None"),
            new("rule2", "None"));
    }

    [TestMethod]
    public void Generate_MixedActivation()
    {
        var context = new Context([
            new("csharpsquid", "rule1", false),
            new("csharpsquid", "rule2", true),
        ]);
        context.ValidateRuleSet(
            new("rule1", "None"),
            new("rule2", "Warning"));
    }

    [TestMethod]
    public void Generate_OtherRules()
    {
        var context = new Context([
            new("other.repo", "other.rule1", isActive: true),
            new("other.repo", "other.rule2", isActive: false),
        ]);
        context.ValidateRuleSet(
            new("other.rule1", "Warning"),
            new("other.rule2", "None"));
    }

    [TestMethod]
    public void Generate_RoslynSDKRules()
    {
        var context = new Context([
            new("roslyn.custom1", "active1", true),
            new("roslyn.custom2", "active2", true),
            new("roslyn.custom1", "inactive1", false),
            new("roslyn.custom2", "inactive2", false),
        ]);
        context.ValidateRuleSet(
            new("active1", "Warning"),
            new("active2", "Warning"),
            new("inactive1", "None"),
            new("inactive2", "None"));
    }

    [TestMethod]
    public void Generate_SonarRules()
    {
        // RoslynAnalyzerProvider receives rules per language, so this will not happen in practice. The RoslynRuleSetGenerator generates all that it receives.
        var context = new Context([
            new("csharpsquid", "CS-active1", true),
            new("vbnet", "VB-active2", true),
            new("csharpsquid", "CS-inactive1", false),
            new("vbnet", "VB-inactive2", false),
            ]);
        context.ValidateRuleSet(
            new("CS-active1", "Warning"),
            new("VB-active2", "Warning"),
            new("CS-inactive1", "None"),
            new("VB-inactive2", "None"));
    }

    private sealed class Context
    {
        public RuleSet RuleSet { get; set; }

        public Context(List<SonarRule> rules, bool deactivateAll = false)
        {
            var sut = new RoslynRuleSetGenerator(deactivateAll);
            RuleSet = sut.Generate(rules);
            ValidateCommonParameters();
        }

        public void ValidateRuleSet(params Rule[] expected) =>
            RuleSet.Rules.Single().RuleList.Should().BeEquivalentTo(expected);

        private void ValidateCommonParameters()
        {
            RuleSet.Description.Should().Be("This rule set was automatically generated from SonarQube");
            RuleSet.ToolsVersion.Should().Be("14.0");
            RuleSet.Name.Should().Be("Rules for SonarQube");
            var rules = RuleSet.Rules.Should().ContainSingle().Subject;
            rules.AnalyzerId.Should().Be("SonarScannerFor.NET");
            rules.RuleNamespace.Should().Be("SonarScannerFor.NET");
        }
    }
}
