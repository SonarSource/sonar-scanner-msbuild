//-----------------------------------------------------------------------
// <copyright file="MergeRuleSetsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            MergeRuleSets task = CreateTask(dummyEngine,projectDir, "missing.ruleset");

            // Act and Assert
            FileNotFoundException ex = AssertException.Expects<FileNotFoundException>(() => task.Execute());
            Assert.AreEqual("missing.ruleset", ex.FileName);
        }

        [TestMethod]
        public void MergeRulesets_IncludeRulesets_AbsolutePaths()
        {
            // Arrange
            string projectDir = this.TestContext.TestDeploymentDir;

            // 1. No included rulesets 
            string primaryRuleset = this.CreateValidRuleset("no-added-includes.ruleset.txt");
            ExecuteAndCheckSuccess(projectDir, primaryRuleset);
            RuleSetAssertions.AssertExpectedIncludeFiles(primaryRuleset /* no included files*/);

            // 2. Adding includes
            primaryRuleset = this.CreateValidRuleset("added-includes1.ruleset.txt");
            ExecuteAndCheckSuccess(projectDir, primaryRuleset, "c:\\include1.ruleset");
            RuleSetAssertions.AssertExpectedIncludeFiles(primaryRuleset, "c:\\include1.ruleset");
            RuleSetAssertions.AssertExpectedIncludeAction(primaryRuleset, "c:\\include1.ruleset", RuleSetAssertions.DefaultActionValue);

            // 3. Adding an include that already exists
            primaryRuleset = this.CreateValidRuleset("added-includes.existing.ruleset.txt");
            ExecuteAndCheckSuccess(projectDir, primaryRuleset, "c:\\include1.ruleset", "c:\\include2.ruleset"); // create a file with incldues
            ExecuteAndCheckSuccess(projectDir, primaryRuleset, "c:\\include1.ruleset", "c:\\INCLUDE2.RULESET", "c:\\include3.ruleset"); // add the same includes again with one extra
            RuleSetAssertions.AssertExpectedIncludeFiles(primaryRuleset, "c:\\include1.ruleset", "c:\\include2.ruleset", "c:\\include3.ruleset");

            RuleSetAssertions.AssertExpectedIncludeAction(primaryRuleset, "c:\\include1.ruleset", RuleSetAssertions.DefaultActionValue);
            RuleSetAssertions.AssertExpectedIncludeAction(primaryRuleset, "c:\\include2.ruleset", RuleSetAssertions.DefaultActionValue);
            RuleSetAssertions.AssertExpectedIncludeAction(primaryRuleset, "c:\\include3.ruleset", RuleSetAssertions.DefaultActionValue);
        }


        [TestMethod]
        public void MergeRulesets_IncludeRulesets_RelativePaths()
        {
            // Arrange
            string absoluteRulesetPath = this.CreateValidRuleset("relative.ruleset");
            string projectDir = Path.GetDirectoryName(absoluteRulesetPath);

            // 1. Relative ruleset path that can be resolved -> included
            string primaryRuleset = this.CreateValidRuleset("found.relative.ruleset.txt");
            ExecuteAndCheckSuccess(projectDir, primaryRuleset, "relative.ruleset");
            RuleSetAssertions.AssertExpectedIncludeFiles(primaryRuleset, absoluteRulesetPath);

            // 1. Relative ruleset path that can be resolved -> included
            primaryRuleset = this.CreateValidRuleset("found.relative2.ruleset.txt");
            ExecuteAndCheckSuccess(projectDir, primaryRuleset,
                ".\\relative.ruleset", // should be resolved correctly...
                ".\\.\\relative.ruleset"); // ... but only added once.

            RuleSetAssertions.AssertExpectedIncludeFiles(primaryRuleset, absoluteRulesetPath);

            // 2. Relative ruleset path that cannot be resolved -> not included
            primaryRuleset = this.CreateValidRuleset("not.found.relative.ruleset.txt");
            ExecuteAndCheckSuccess(projectDir, primaryRuleset, "not.found\\relative.ruleset");
            RuleSetAssertions.AssertExpectedIncludeFiles(primaryRuleset /* ruleset should not have been resolved */);
        }

        [TestMethod]
        public void MergeRulesets_EmptyRuleset()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string primaryRuleset = TestUtils.CreateTextFile(testDir, "empty.ruleset.txt",
@"<RuleSet Name = 'RulesetName' ToolsVersion = '14.0' />");

            // Act
            ExecuteAndCheckSuccess(testDir, primaryRuleset, "c:\\foo\\added.ruleset");

            // Assert
            RuleSetAssertions.AssertExpectedIncludeFiles(primaryRuleset, "c:\\foo\\added.ruleset");
        }


        [TestMethod]
        public void MergeRulesets_ExistingInclude()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string primaryRuleset = TestUtils.CreateTextFile(testDir, "existing.ruleset.txt",
@"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""RulesetName"" ToolsVersion=""14.0"">
  <Include Path=""d:\include1.ruleset"" Action=""Error"" />
  <Rules AnalyzerId=""My.Analyzer"" RuleNamespace=""My.Analyzers"">
    <Rule Id=""Rule002"" Action=""Error"" />
  </Rules>
</RuleSet>
");
            // Act
            ExecuteAndCheckSuccess(testDir, primaryRuleset, "d:\\include1.ruleset");

            // Assert
            RuleSetAssertions.AssertExpectedIncludeFiles(primaryRuleset, "d:\\include1.ruleset");
            RuleSetAssertions.AssertExpectedIncludeAction(primaryRuleset, "d:\\include1.ruleset", ErrorActionValue); // action value should still be Error
        }

        #endregion

        #region Checks

        private void ExecuteAndCheckSuccess(string projectDirectory, string primaryRuleset, params string[] rulesetsToInclude)
        {
            this.TestContext.AddResultFile(primaryRuleset);

            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            MergeRuleSets task = CreateTask(dummyEngine, projectDirectory, primaryRuleset, rulesetsToInclude);

            bool taskSucess = task.Execute();
            Assert.IsTrue(taskSucess, "Expecting the task to succeed");
            dummyEngine.AssertNoErrors();
            dummyEngine.AssertNoWarnings();
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

        private static MergeRuleSets CreateTask(DummyBuildEngine buildEngine, string projectDir, string primaryRuleset, params string[] rulesetsToInclude)
        {
            MergeRuleSets task = new MergeRuleSets();
            task.BuildEngine = buildEngine;
            task.ProjectDirectoryPath = projectDir;
            task.PrimaryRulesetFilePath = primaryRuleset;
            task.IncludedRulesetFilePaths = rulesetsToInclude;

            return task;
        }

        #endregion
    }
}
