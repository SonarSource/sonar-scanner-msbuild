//-----------------------------------------------------------------------
// <copyright file="MergeRuleSetsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
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
            string projectDir = this.TestContext.TestDeploymentDir;
            string targetRulesetFilePath = Path.Combine(projectDir, "merged.ruleset.txt");

            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            MergeRuleSets task = CreateTask(dummyEngine,projectDir, "missing.ruleset", targetRulesetFilePath);

            // Act and Assert
            FileNotFoundException ex = AssertException.Expects<FileNotFoundException>(() => task.Execute());
            Assert.AreEqual("missing.ruleset", ex.FileName);
        }

        [TestMethod]
        public void MergeRulesets_MergedRulesetAlreadyExists()
        {
            // Arrange
            string projectDir = this.TestContext.TestDeploymentDir;
            string primaryRuleset = this.CreateValidRuleset("valid.ruleset.txt");
            string targetRulesetFilePath = Path.Combine(projectDir, "merged.ruleset.txt");
            File.WriteAllText(targetRulesetFilePath, "dummy existing ruleset");

            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            MergeRuleSets task = CreateTask(dummyEngine, projectDir, primaryRuleset, targetRulesetFilePath);

            // Act and Assert
            InvalidOperationException ex = AssertException.Expects<InvalidOperationException>(() => task.Execute());
            Assert.IsTrue(ex.Message.Contains(targetRulesetFilePath));
        }

        [TestMethod]
        public void MergeRulesets_IncludeRulesets_AbsolutePaths()
        {
            // Arrange
            string projectDir = this.TestContext.TestDeploymentDir;
            string mergedRuleset;

            // 1. No included rulesets 
            string primaryRuleset = this.CreateValidRuleset("no-added-includes.ruleset.txt");
            mergedRuleset = ExecuteAndCheckSuccess(projectDir, primaryRuleset);
            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset /* no included files*/);

            // 2. Adding includes
            primaryRuleset = this.CreateValidRuleset("added-includes1.ruleset.txt");
            mergedRuleset = ExecuteAndCheckSuccess(projectDir, primaryRuleset, "c:\\include1.ruleset");
            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset, "c:\\include1.ruleset");

            // 3. Adding an include that already exists
            primaryRuleset = this.CreateValidRuleset("added-includes.existing.ruleset.txt");
            string rulesetWithExistingInclude = ExecuteAndCheckSuccess(projectDir, primaryRuleset, "c:\\include1.ruleset", "c:\\include2.ruleset"); // create a file with includes
            
            mergedRuleset = ExecuteAndCheckSuccess(projectDir, rulesetWithExistingInclude, "c:\\include1.ruleset", "c:\\INCLUDE2.RULESET", "c:\\include3.ruleset"); // add the same includes again with one extra
            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset, "c:\\include1.ruleset", "c:\\include2.ruleset", "c:\\include3.ruleset");
        }

        [TestMethod]
        public void MergeRulesets_IncludeRulesets_RelativePaths()
        {
            // Arrange
            string absoluteRulesetPath = this.CreateValidRuleset("relative.ruleset");
            string projectDir = Path.GetDirectoryName(absoluteRulesetPath);
            string mergedRuleset;

            // 1. Relative ruleset path that can be resolved -> included
            string primaryRuleset = this.CreateValidRuleset("found.relative.ruleset.txt");
            mergedRuleset = ExecuteAndCheckSuccess(projectDir, primaryRuleset, "relative.ruleset");
            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset, absoluteRulesetPath);

            // 1. Relative ruleset path that can be resolved -> included
            primaryRuleset = this.CreateValidRuleset("found.relative2.ruleset.txt");
            mergedRuleset = ExecuteAndCheckSuccess(projectDir, primaryRuleset,
                ".\\relative.ruleset", // should be resolved correctly...
                ".\\.\\relative.ruleset"); // ... but only added once.

            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset, absoluteRulesetPath);

            // 2. Relative ruleset path that cannot be resolved -> not included
            primaryRuleset = this.CreateValidRuleset("not.found.relative.ruleset.txt");
            mergedRuleset = ExecuteAndCheckSuccess(projectDir, primaryRuleset, "not.found\\relative.ruleset");
            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset /* ruleset should not have been resolved */);
        }

        [TestMethod]
        public void MergeRulesets_EmptyRuleset()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string primaryRuleset = TestUtils.CreateTextFile(testDir, "empty.ruleset.txt",
@"<RuleSet Name = 'RulesetName' ToolsVersion = '14.0' />");

            // Act
            string mergedRuleset = ExecuteAndCheckSuccess(testDir, primaryRuleset, "c:\\foo\\added.ruleset");

            // Assert
            RuleSetAssertions.AssertExpectedIncludeFilesAndDefaultAction(mergedRuleset, "c:\\foo\\added.ruleset");
        }


        [TestMethod]
        public void MergeRulesets_ExistingInclude()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string primaryRuleset = TestUtils.CreateTextFile(testDir, "existing.ruleset.txt",
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
            string mergedRuleset = ExecuteAndCheckSuccess(testDir, primaryRuleset,
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

        #endregion

        #region Checks

        private string ExecuteAndCheckSuccess(string projectDirectory, string primaryRuleset, params string[] rulesetsToInclude)
        {
            string mergedRulesetFileName = primaryRuleset + ".merged.txt";

            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            MergeRuleSets task = CreateTask(dummyEngine, projectDirectory, primaryRuleset, mergedRulesetFileName, rulesetsToInclude);

            bool taskSucess = task.Execute();
            Assert.IsTrue(taskSucess, "Expecting the task to succeed");
            dummyEngine.AssertNoErrors();
            dummyEngine.AssertNoWarnings();

            Assert.IsTrue(File.Exists(mergedRulesetFileName), "Expecting the merged ruleset to have been created: {0}", mergedRulesetFileName);
            this.TestContext.AddResultFile(primaryRuleset);
            this.TestContext.AddResultFile(mergedRulesetFileName);

            return mergedRulesetFileName;
        }
       
        #endregion


        #region Private methods

        private string CreateValidRuleset(string fileName)
        {
            string testDir = TestUtils.EnsureTestSpecificFolder(this.TestContext);
            string rulesetPath = TestUtils.CreateTextFile(testDir, fileName,
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
            MergeRuleSets task = new MergeRuleSets();
            task.BuildEngine = buildEngine;
            task.ProjectDirectoryPath = projectDir;
            task.PrimaryRulesetFilePath = primaryRuleset;
            task.MergedRuleSetFilePath = targetRulesetFilePath;
            task.IncludedRulesetFilePaths = rulesetsToInclude;

            return task;
        }

        #endregion
    }
}
