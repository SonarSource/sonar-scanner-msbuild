//-----------------------------------------------------------------------
// <copyright file="RulesetGeneratorTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class RulesetGeneratorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void RulesetGet_Simple()
        {
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            ServerDataModel model = new ServerDataModel();

            model.InstalledPlugins.Add("real.plugin1");
            model.InstalledPlugins.Add("unused.plugin1");

            // Set up the rule repositories
            model.AddRepository("empty.repo", "aaa");

            model.AddRepository("repo1", "languageAAA")
                .AddRule("repo1.aaa.r1", "repo1.aaa.r1.internal")
                .AddRule("repo1.aaa.r2", "repo1.aaa.r2.internal");

            model.AddRepository("repo1", "languageBBB")
                .AddRule("repo1.bbb.r1", "repo1.xxx.r1.internal")
                .AddRule("repo1.bbb.r2", "repo1.xxx.r2.internal")
                .AddRule("repo1.bbb.r3", "repo1.xxx.r3.internal");


            // Set up the quality profiles
            model.AddQualityProfile("profile 1", "languageAAA")
                .AddProject("unused.project")
                .AddProject("project1");

            model.AddQualityProfile("profile 2", "languageBBB")
                .AddProject("project1")
                .AddProject("project2");

            // Add rules to the quality profiles
            model.AddRuleToProfile("repo1.aaa.r1", "profile 1"); // Only one rule in the repo

            model.AddRuleToProfile("repo1.bbb.r1", "profile 2");
            model.AddRuleToProfile("repo1.bbb.r2", "profile 2");
            model.AddRuleToProfile("repo1.bbb.r3", "profile 2");

            MockSonarQubeServer server = new MockSonarQubeServer();
            server.Data = model;

            // 1. Plugin not installed
            string rulesetFilePath = Path.Combine(testDir, "r1.txt");
            RulesetGenerator.Generate(server, "missing.plugin", "languageAAA", "repo1", "project1", rulesetFilePath);
            AssertFileDoesNotExist(rulesetFilePath);

            // 2. Language not handled
            RulesetGenerator.Generate(server, "real.plugin1", "unhandled.language", "repo1", "project1", rulesetFilePath);
            AssertFileDoesNotExist(rulesetFilePath);

            // 3. Missing project
            RulesetGenerator.Generate(server, "real.plugin1", "languageBBB", "repo1", "missing.project", rulesetFilePath);
            AssertFileDoesNotExist(rulesetFilePath);

            // 4. Valid, aaa
            string aaaRulesetFilePath = Path.Combine(testDir, "aaa_ruleset.txt");
            RulesetGenerator.Generate(server, "real.plugin1", "languageAAA", "repo1", "project1", aaaRulesetFilePath);
            AssertRuleSetContainsRules(aaaRulesetFilePath, "repo1.aaa.r1");

            // 5. Valid, bbb
            string bbbRulesetFilePath = Path.Combine(testDir, "bbb_ruleset.txt");
            RulesetGenerator.Generate(server, "real.plugin1", "languageBBB", "repo1", "project1", bbbRulesetFilePath);
            AssertRuleSetContainsRules(bbbRulesetFilePath,
                "repo1.bbb.r1", "repo1.bbb.r2", "repo1.bbb.r3");
        }

        #endregion

        #region Checks

        private static void AssertFileDoesNotExist(string filePath)
        {
            Assert.IsFalse(File.Exists(filePath), "Not expecting file to exist: {0}", filePath);
        }

        private void AssertRuleSetContainsRules(string filePath, params string[] expectedRuleIds)
        {
            Assert.IsTrue(File.Exists(filePath), "Expected ruleset file does not exist: {0}", filePath);
            this.TestContext.AddResultFile(filePath);

            XDocument doc = XDocument.Load(filePath);

            foreach (string ruleId in expectedRuleIds)
            {
                AssertRuleIdExists(doc, ruleId);
            }

            AssertExpectedRuleCount(doc, expectedRuleIds.Length);
        }

        private static void AssertRuleIdExists(XDocument doc, string ruleId)
        {
            XElement element = doc.Descendants().Single(e => e.Name == "Rule" && HasRuleIdAttribute(e, ruleId));
            Assert.IsNotNull(element, "Could not find ruleId with expected id: {0}", ruleId);
        }

        private static bool HasRuleIdAttribute(XElement element, string ruleId)
        {
            return element.Attributes().Any(a => a.Name == "Id" && a.Value == ruleId);
        }

        private static void AssertExpectedRuleCount(XDocument doc, int expectedCount)
        {
            IEnumerable<XElement> rules = doc.Descendants().Where(e => e.Name == "Rule");
            Assert.AreEqual(expectedCount, rules.Count(), "Unexpected number of rules");
        }

        #endregion
    }
}
