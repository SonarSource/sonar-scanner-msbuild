//-----------------------------------------------------------------------
// <copyright file="RoslynRuleSetGeneratorTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
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
            RoslynRuleSetGenerator generator = new RoslynRuleSetGenerator(dict);
            IEnumerable<ActiveRule> activeRules = new List<ActiveRule>();
            IEnumerable<string> inactiveRules = new List<string>();
            string language = "cs";

            AssertException.Expects<ArgumentNullException>(() => generator.generate(activeRules, inactiveRules, null));
            AssertException.Expects<ArgumentNullException>(() => generator.generate(activeRules, null, language));
            AssertException.Expects<ArgumentNullException>(() => generator.generate(null, inactiveRules, language));
        }

        [TestMethod]
        public void RoslynRuleSet_Empty()
        {
            IDictionary<string, string> dict = new Dictionary<string, string>();
            RoslynRuleSetGenerator generator = new RoslynRuleSetGenerator(dict);
            List<ActiveRule> activeRules = new List<ActiveRule>();
            IEnumerable<string> inactiveRules = new List<string>();
            string language = "cs";
            activeRules.Add(new ActiveRule("repo", "key"));

            RuleSet ruleSet = generator.generate(activeRules, inactiveRules, language);
            // No analyzer
            Assert.IsFalse(ruleSet.Rules.Any());

            Assert.AreEqual(ruleSet.Description, "This rule set was automatically generated from SonarQube");
            Assert.AreEqual(ruleSet.ToolsVersion, "14.0");
            Assert.AreEqual(ruleSet.Name, "Rules for SonarQube");
        }

        [TestMethod]
        public void RoslynRuleSet_CSharp()
        {
            IDictionary<string, string> dict = new Dictionary<string, string>();
            dict.Add("sonaranalyzer-cs.analyzerId", "SonarAnalyzer.CSharp");
            dict.Add("sonaranalyzer-cs.ruleNamespace", "SonarAnalyzer.CSharp");
            dict.Add("sonaranalyzer-cs.pluginKey", "csharp");
            dict.Add("sonaranalyzer-cs.staticResourceName", "SonarAnalyzer.zip");
            dict.Add("sonaranalyzer-cs.nuget.packageId", "SonarAnalyzer.CSharp");
            dict.Add("sonaranalyzer-cs.pluginVersion", "1.13.0");
            dict.Add("sonaranalyzer-cs.nuget.packageVersion", "1.13.0");

            dict.Add("custom.analyzerId", "SonarAnalyzer.Custom");
            dict.Add("custom.ruleNamespace", "SonarAnalyzer.Custom");

            RoslynRuleSetGenerator generator = new RoslynRuleSetGenerator(dict);
            List<ActiveRule> activeRules = new List<ActiveRule>();
            List<string> inactiveRules = new List<string>();
            string language = "cs";

            activeRules.Add(new ActiveRule("csharpsquid", "S1000"));
            activeRules.Add(new ActiveRule("csharpsquid", "S1001"));
            activeRules.Add(new ActiveRule("roslyn.custom", "custom", "custom.internal"));
            activeRules.Add(new ActiveRule("other.repo", "other.rule"));

            inactiveRules.Add("csharpsquid:S1002");
            inactiveRules.Add("roslyn.custom:S1005");

            RuleSet ruleSet = generator.generate(activeRules, inactiveRules, language);
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

        #endregion

        #region Checks

        private void AssertAnalyzerRules(RuleSet ruleSet, string analyzerId, string[] activatedRuleIds, string[] inactivatedRuleIds)
        {
            Rules rules = ruleSet.Rules.First(r => r.AnalyzerId.Equals(analyzerId));
            Assert.AreEqual(analyzerId, rules.RuleNamespace);
            Assert.AreEqual(rules.RuleList.Count, activatedRuleIds.Count() + inactivatedRuleIds.Count());

            // No repeated ids
            Assert.IsFalse(rules.RuleList.GroupBy(x => x.Id).Any(g => g.Count() > 1));

            // Active correspond to Warning, inactive to None
            foreach (string id in activatedRuleIds)
            {
                Assert.IsTrue(rules.RuleList.Exists(r => r.Id.Equals(id) && r.Action.Equals("Warning")));
            }

            foreach (string id in inactivatedRuleIds)
            {
                Assert.IsTrue(rules.RuleList.Exists(r => r.Id.Equals(id) && r.Action.Equals("None")));
            }
        }

        #endregion

    }
}
