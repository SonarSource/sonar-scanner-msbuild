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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;
using TestUtilities;

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
            AssertException.Expects<ArgumentNullException>(() => new RoslynRuleSetGenerator(null));
        }

        [TestMethod]
        public void RoslynRuleSet_GeneratorArgumentChecks()
        {
            IDictionary<string, string> dict = new Dictionary<string, string>();
            var generator = new RoslynRuleSetGenerator(dict);
            IEnumerable<ActiveRule> activeRules = new List<ActiveRule>();
            IEnumerable<string> inactiveRules = new List<string>();
            var language = "cs";

            AssertException.Expects<ArgumentNullException>(() => generator.Generate(activeRules, inactiveRules, null));
            AssertException.Expects<ArgumentNullException>(() => generator.Generate(activeRules, null, language));
            AssertException.Expects<ArgumentNullException>(() => generator.Generate(null, inactiveRules, language));
        }

        [TestMethod]
        public void RoslynRuleSet_Empty()
        {
            IDictionary<string, string> dict = new Dictionary<string, string>();
            var generator = new RoslynRuleSetGenerator(dict);
            var activeRules = new List<ActiveRule>();
            IEnumerable<string> inactiveRules = new List<string>();
            var language = "cs";
            activeRules.Add(new ActiveRule("repo", "key"));

            var ruleSet = generator.Generate(activeRules, inactiveRules, language);
            // No analyzer
            Assert.IsFalse(ruleSet.Rules.Any());

            Assert.AreEqual(ruleSet.Description, "This rule set was automatically generated from SonarQube");
            Assert.AreEqual(ruleSet.ToolsVersion, "14.0");
            Assert.AreEqual(ruleSet.Name, "Rules for SonarQube");
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
            var activeRules = new List<ActiveRule>();
            var inactiveRules = new List<string>();
            var language = "cs";

            activeRules.Add(new ActiveRule("csharpsquid", "S1000"));
            activeRules.Add(new ActiveRule("csharpsquid", "S1001"));
            activeRules.Add(new ActiveRule("roslyn.custom", "custom", "custom.internal"));
            activeRules.Add(new ActiveRule("other.repo", "other.rule"));

            inactiveRules.Add("csharpsquid:S1002");
            inactiveRules.Add("roslyn.custom:S1005");

            var ruleSet = generator.Generate(activeRules, inactiveRules, language);
            string[] activatedCSharp = { "S1000", "S1001" };
            string[] activatedCustom = { "custom" };

            string[] inactivatedCSharp = { "S1002" };
            string[] inactivatedCustom = { "S1005" };

            Assert.AreEqual(2, ruleSet.Rules.Count());
            AssertAnalyzerRules(ruleSet, "SonarAnalyzer.Custom", activatedCustom, inactivatedCustom);
            AssertAnalyzerRules(ruleSet, "SonarAnalyzer.CSharp", activatedCSharp, inactivatedCSharp);

            Assert.AreEqual(ruleSet.Description, "This rule set was automatically generated from SonarQube");
            Assert.AreEqual(ruleSet.ToolsVersion, "14.0");
            Assert.AreEqual(ruleSet.Name, "Rules for SonarQube");
        }

        #endregion Tests

        #region Checks

        private void AssertAnalyzerRules(RuleSet ruleSet, string analyzerId, string[] activatedRuleIds, string[] inactivatedRuleIds)
        {
            var rules = ruleSet.Rules.First(r => r.AnalyzerId.Equals(analyzerId));
            Assert.AreEqual(analyzerId, rules.RuleNamespace);
            Assert.AreEqual(rules.RuleList.Count, activatedRuleIds.Count() + inactivatedRuleIds.Count());

            // No repeated ids
            Assert.IsFalse(rules.RuleList.GroupBy(x => x.Id).Any(g => g.Count() > 1));

            // Active correspond to Warning, inactive to None
            foreach (var id in activatedRuleIds)
            {
                Assert.IsTrue(rules.RuleList.Exists(r => r.Id.Equals(id) && r.Action.Equals("Warning")));
            }

            foreach (var id in inactivatedRuleIds)
            {
                Assert.IsTrue(rules.RuleList.Exists(r => r.Id.Equals(id) && r.Action.Equals("None")));
            }
        }

        #endregion Checks
    }
}
