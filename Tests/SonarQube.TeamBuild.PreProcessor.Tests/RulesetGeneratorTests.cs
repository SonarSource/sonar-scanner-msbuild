/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
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
 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;
using System.IO;
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
            string rulesetFilePath = Path.Combine(testDir, "r1.txt");

            List<ActiveRule> activeRules = new List<ActiveRule>();
            activeRules.Add(new ActiveRule("repo1", "repo1.aaa.r1.internal"));
            activeRules.Add(new ActiveRule("repo1", "repo1.aaa.r2.internal"));
            activeRules.Add(new ActiveRule("repo1", "repo1.aaa.r3.internal"));
            activeRules.Add(new ActiveRule("repo2", "repo2.aaa.r4.internal"));

            RulesetGenerator generator = new RulesetGenerator();
            generator.Generate("repo1", activeRules, rulesetFilePath);

            PreProcessAsserts.AssertRuleSetContainsRules(rulesetFilePath,
                "repo1.aaa.r1.internal", "repo1.aaa.r2.internal", "repo1.aaa.r3.internal");
        }

        [TestMethod]
        public void RulesetGet_NoRules()
        {
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string rulesetFilePath = Path.Combine(testDir, "r1.txt");

            List<ActiveRule> activeRules = new List<ActiveRule>();
            RulesetGenerator generator = new RulesetGenerator();
            generator.Generate("repo1", activeRules, rulesetFilePath);

            AssertFileDoesNotExist(rulesetFilePath);
        }

        [TestMethod]
        public void RulesetGet_ValidateArgs()
        {
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string rulesetFilePath = Path.Combine(testDir, "r1.txt");

            List<ActiveRule> activeRules = new List<ActiveRule>();

            RulesetGenerator generator = new RulesetGenerator();
            AssertException.Expects<ArgumentNullException>(() => generator.Generate("repo1", null, rulesetFilePath));
            AssertException.Expects<ArgumentNullException>(() => generator.Generate(null, activeRules, rulesetFilePath));
            AssertException.Expects<ArgumentNullException>(() => generator.Generate("repo1", activeRules, null));
        }

        #endregion

        #region Checks

        private static void AssertFileDoesNotExist(string filePath)
        {
            Assert.IsFalse(File.Exists(filePath), "Not expecting file to exist: {0}", filePath);
        }

        #endregion
    }
}
