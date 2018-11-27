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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.Tests
{
    [TestClass]
    public class RoslynRuleSetGeneratorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void RoslynRuleSet_ConstructorArgumentChecks()
        {
            Action act = () => new RoslynRuleSetGenerator(null);
            act.Should().ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        public void RoslynRuleSet_GeneratorArgumentChecks()
        {
            IDictionary<string, string> dict = new Dictionary<string, string>();
            var generator = new RoslynRuleSetGenerator(dict);
            IEnumerable<SonarRule> activeRules = new List<SonarRule>();
            IEnumerable<SonarRule> inactiveRules = new List<SonarRule>();
            var language = "cs";

            Action act1 = () => generator.Generate(activeRules, inactiveRules, null);
            act1.Should().ThrowExactly<ArgumentNullException>();

            Action act2 = () => generator.Generate(activeRules, null, language);
            act2.Should().ThrowExactly<ArgumentNullException>();

            Action act3 = () => generator.Generate(null, inactiveRules, language);
            act3.Should().ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        public void RoslynRuleSet_Empty()
        {
            IDictionary<string, string> dict = new Dictionary<string, string>();
            var generator = new RoslynRuleSetGenerator(dict);
            var activeRules = new List<SonarRule>();
            IEnumerable<SonarRule> inactiveRules = new List<SonarRule>();
            var language = "cs";
            activeRules.Add(new SonarRule("repo", "key"));

            var ruleSet = generator.Generate(activeRules, inactiveRules, language);
            // No analyzer
            ruleSet.Rules.Any().Should().BeFalse();

            ruleSet.Description.Should().Be("This rule set was automatically generated from SonarQube");
            ruleSet.ToolsVersion.Should().Be("14.0");
            ruleSet.Name.Should().Be("Rules for SonarQube");
        }

        [TestMethod]
        public void RoslynRuleSet_CSharp()
        {
            IDictionary<string, string> dict = new Dictionary<string, string>
            {
                { "sonaranalyzer-cs.analyzerId", "SonarAnalyzer.CSharp" },
                { "sonaranalyzer-cs.ruleNamespace", "SonarAnalyzer.CSharp" },
                { "sonaranalyzer-cs.pluginKey", "csharp" },
                { "sonaranalyzer-cs.staticResourceName", "SonarAnalyzer.zip" },
                { "sonaranalyzer-cs.nuget.packageId", "SonarAnalyzer.CSharp" },
                { "sonaranalyzer-cs.pluginVersion", "1.13.0" },
                { "sonaranalyzer-cs.nuget.packageVersion", "1.13.0" },

                { "custom.analyzerId", "SonarAnalyzer.Custom" },
                { "custom.ruleNamespace", "SonarAnalyzer.Custom" }
            };

            var generator = new RoslynRuleSetGenerator(dict);
            var activeRules = new List<SonarRule>();
            var inactiveRules = new List<SonarRule>();
            var language = "cs";

            activeRules.Add(new SonarRule("csharpsquid", "S1000", true));
            activeRules.Add(new SonarRule("csharpsquid", "S1001", true));
            activeRules.Add(new SonarRule("roslyn.custom", "custom", "custom.internal", true));
            activeRules.Add(new SonarRule("other.repo", "other.rule", true));

            inactiveRules.Add(new SonarRule("csharpsquid", "S1002", false));
            inactiveRules.Add(new SonarRule("roslyn.custom", "S1005", false));

            var ruleSet = generator.Generate(activeRules, inactiveRules, language);
            string[] activatedCSharp = { "S1000", "S1001" };
            string[] activatedCustom = { "custom" };

            string[] inactivatedCSharp = { "S1002" };
            string[] inactivatedCustom = { "S1005" };

            ruleSet.Rules.Should().HaveCount(2);
            AssertAnalyzerRules(ruleSet, "SonarAnalyzer.Custom", activatedCustom, inactivatedCustom);
            AssertAnalyzerRules(ruleSet, "SonarAnalyzer.CSharp", activatedCSharp, inactivatedCSharp);

            ruleSet.Description.Should().Be("This rule set was automatically generated from SonarQube");
            ruleSet.ToolsVersion.Should().Be("14.0");
            ruleSet.Name.Should().Be("Rules for SonarQube");
        }

        #endregion Tests

        #region Checks

        private void AssertAnalyzerRules(RuleSet ruleSet, string analyzerId, string[] activatedRuleIds, string[] inactivatedRuleIds)
        {
            var rules = ruleSet.Rules.First(r => r.AnalyzerId.Equals(analyzerId));
            rules.RuleNamespace.Should().Be(analyzerId);
            rules.RuleList.Should().HaveCount(activatedRuleIds.Count() + inactivatedRuleIds.Count());

            // No repeated ids
            rules.RuleList.GroupBy(x => x.Id).Any(g => g.Count() > 1).Should().BeFalse();

            // Active correspond to Warning, inactive to None
            foreach (var id in activatedRuleIds)
            {
                rules.RuleList.Exists(r => r.Id.Equals(id) && r.Action.Equals("Warning")).Should().BeTrue();
            }

            foreach (var id in inactivatedRuleIds)
            {
                rules.RuleList.Exists(r => r.Id.Equals(id) && r.Action.Equals("None")).Should().BeTrue();
            }
        }

        #endregion Checks
    }
}
