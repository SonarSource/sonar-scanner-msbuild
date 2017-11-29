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

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.UnitTests
{
    [TestClass]
    public class MergeRuleSetsTests
    {
        private const string ErrorActionValue = "Error";

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void MergeRulesets_PrimaryRulesetDoesNotExist()
        {
            // Arrange
            var projectDir = TestContext.TestDeploymentDir;
            var targetRulesetFilePath = Path.Combine(projectDir, "merged.ruleset.txt");

            var dummyEngine = new DummyBuildEngine();
            var task = CreateTask(dummyEngine,projectDir, "missing.ruleset", targetRulesetFilePath);

            // Act and Assert
            var ex = AssertException.Expects<FileNotFoundException>(() => task.Execute());
            Assert.AreEqual("missing.ruleset", ex.FileName);
        }

        [TestMethod]
        public void MergeRulesets_MergedRulesetAlreadyExists()
        {
            // Arrange
            var projectDir = TestContext.TestDeploymentDir;
            var primaryRuleset = CreateValidRuleset("valid.ruleset.txt");
            var targetRulesetFilePath = Path.Combine(projectDir, "merged.ruleset.txt");
            File.WriteAllText(targetRulesetFilePath, "dummy existing ruleset");

            var dummyEngine = new DummyBuildEngine();
            var task = CreateTask(dummyEngine, projectDir, primaryRuleset, targetRulesetFilePath);

            // Act and Assert
            var ex = AssertException.Expects<InvalidOperationException>(() => task.Execute());
            Assert.IsTrue(ex.Message.Contains(targetRulesetFilePath));
        }

        [TestMethod]
        public void MergeRulesets_IncludeRulesets_AbsolutePaths()
        {
            // Arrange
            var projectDir = TestContext.TestDeploymentDir;
            string mergedRuleset;

            // 1. No included rulesets
            var primaryRuleset = CreateValidRuleset("no-added-includes.ruleset.txt");
            mergedRuleset = ExecuteAndCheckSuccess(projectDir, primaryRuleset);
            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset /* no included files*/);

            // 2. Adding includes
            primaryRuleset = CreateValidRuleset("added-includes1.ruleset.txt");
            mergedRuleset = ExecuteAndCheckSuccess(projectDir, primaryRuleset, "c:\\include1.ruleset");
            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset, "c:\\include1.ruleset");

            // 3. Adding an include that already exists
            primaryRuleset = CreateValidRuleset("added-includes.existing.ruleset.txt");
            var rulesetWithExistingInclude = ExecuteAndCheckSuccess(projectDir, primaryRuleset, "c:\\include1.ruleset", "c:\\include2.ruleset"); // create a file with includes

            mergedRuleset = ExecuteAndCheckSuccess(projectDir, rulesetWithExistingInclude, "c:\\include1.ruleset", "c:\\INCLUDE2.RULESET", "c:\\include3.ruleset"); // add the same includes again with one extra
            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset, "c:\\include1.ruleset", "c:\\include2.ruleset", "c:\\include3.ruleset");
        }

        [TestMethod]
        public void MergeRulesets_IncludeRulesets_RelativePaths()
        {
            // Arrange
            var absoluteRulesetPath = CreateValidRuleset("relative.ruleset");
            var projectDir = Path.GetDirectoryName(absoluteRulesetPath);
            string mergedRuleset;

            // 1. Relative ruleset path that can be resolved -> included
            var primaryRuleset = CreateValidRuleset("found.relative.ruleset.txt");
            mergedRuleset = ExecuteAndCheckSuccess(projectDir, primaryRuleset, "relative.ruleset");
            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset, absoluteRulesetPath);

            // 1. Relative ruleset path that can be resolved -> included
            primaryRuleset = CreateValidRuleset("found.relative2.ruleset.txt");
            mergedRuleset = ExecuteAndCheckSuccess(projectDir, primaryRuleset,
                ".\\relative.ruleset", // should be resolved correctly...
                ".\\.\\relative.ruleset"); // ... but only added once.

            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset, absoluteRulesetPath);

            // 2. Relative ruleset path that cannot be resolved -> not included
            primaryRuleset = CreateValidRuleset("not.found.relative.ruleset.txt");
            mergedRuleset = ExecuteAndCheckSuccess(projectDir, primaryRuleset, "not.found\\relative.ruleset");
            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset /* ruleset should not have been resolved */);
        }

        [TestMethod]
        public void MergeRulesets_EmptyRuleset()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var primaryRuleset = TestUtils.CreateTextFile(testDir, "empty.ruleset.txt",
@"<RuleSet Name = 'RulesetName' ToolsVersion = '14.0' />");

            // Act
            var mergedRuleset = ExecuteAndCheckSuccess(testDir, primaryRuleset, "c:\\foo\\added.ruleset");

            // Assert
            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset, "c:\\foo\\added.ruleset");
        }

        [TestMethod]
        public void MergeRulesets_ExistingInclude()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var primaryRuleset = TestUtils.CreateTextFile(testDir, "existing.ruleset.txt",
@"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""RulesetName"" ToolsVersion=""14.0"">
  <Include Path=""d:\error.include.ruleset"" Action=""Error"" />
  <Include Path=""d:\info.include.ruleset"" Action=""Info"" />
  <Include Path=""d:\warning.include.ruleset"" Action=""Warning"" />
<Include Path=""d:\default.include.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""My.Analyzer"" RuleNamespace=""My.Analyzers"">
    <Rule Id=""Rule002"" Action=""Error"" />
  </Rules>
</RuleSet>
");
            // Act
            var mergedRuleset = ExecuteAndCheckSuccess(testDir, primaryRuleset,
                "d:\\error.include.ruleset",
                "d:\\info.include.ruleset",
                "d:\\warning.include.ruleset",
                "d:\\default.include.ruleset");

            // Assert
            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset,
                // Action value should be "Warning" for all included rulesets
                "d:\\error.include.ruleset",
                "d:\\info.include.ruleset",
                "d:\\warning.include.ruleset",
                "d:\\default.include.ruleset");
        }

        #endregion Tests

        #region Checks

        private string ExecuteAndCheckSuccess(string projectDirectory, string primaryRuleset, params string[] rulesetsToInclude)
        {
            var mergedRulesetFileName = primaryRuleset + ".merged.txt";

            var dummyEngine = new DummyBuildEngine();
            var task = CreateTask(dummyEngine, projectDirectory, primaryRuleset, mergedRulesetFileName, rulesetsToInclude);

            var taskSucess = task.Execute();
            Assert.IsTrue(taskSucess, "Expecting the task to succeed");
            dummyEngine.AssertNoErrors();
            dummyEngine.AssertNoWarnings();

            Assert.IsTrue(File.Exists(mergedRulesetFileName), "Expecting the merged ruleset to have been created: {0}", mergedRulesetFileName);
            TestContext.AddResultFile(primaryRuleset);
            TestContext.AddResultFile(mergedRulesetFileName);

            return mergedRulesetFileName;
        }

        #endregion Checks

        #region Private methods

        private string CreateValidRuleset(string fileName)
        {
            var testDir = TestUtils.EnsureTestSpecificFolder(TestContext);
            var rulesetPath = TestUtils.CreateTextFile(testDir, fileName,
@"<?xml version='1.0' encoding='utf-8'?>
<RuleSet Name='RulesetName' ToolsVersion='14.0'>
  <Rules AnalyzerId='My.Analyzer' RuleNamespace='My.Analyzers'>
    <Rule Id='Rule001' Action='None' />
    <Rule Id='Rule002' Action='Error' />
  </Rules>
</RuleSet>
");
            return rulesetPath;
        }

        private static MergeRuleSets CreateTask(DummyBuildEngine buildEngine,
            string projectDir,
            string primaryRuleset,
            string targetRulesetFilePath,
            params string[] rulesetsToInclude)
        {
            var task = new MergeRuleSets
            {
                BuildEngine = buildEngine,
                ProjectDirectoryPath = projectDir,
                PrimaryRulesetFilePath = primaryRuleset,
                MergedRuleSetFilePath = targetRulesetFilePath,
                IncludedRulesetFilePaths = rulesetsToInclude
            };

            return task;
        }

        #endregion Private methods
    }
}
