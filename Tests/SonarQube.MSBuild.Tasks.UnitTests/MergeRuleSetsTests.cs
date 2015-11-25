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
            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            MergeRuleSets task = CreateTask(dummyEngine, "missing.ruleset");

            // Act and Assert
            FileNotFoundException ex = AssertException.Expects<FileNotFoundException>(() => task.Execute());
            Assert.AreEqual("missing.ruleset", ex.FileName);
        }

        [TestMethod]
        public void MergeRulesets_IncludeRulesets()
        {
            // 1. No included rulesets 
            string primaryRuleset = this.CreateValidRuleset("no-added-includes.ruleset.txt");
            ExecuteAndCheckSuccess(primaryRuleset);
            RuleSetAssertions.AssertExpectedIncludeFiles(primaryRuleset /* no included files*/);

            // 2. Adding includes
            primaryRuleset = this.CreateValidRuleset("added-includes1.ruleset.txt");
            ExecuteAndCheckSuccess(primaryRuleset, "include1.ruleset");
            RuleSetAssertions.AssertExpectedIncludeFiles(primaryRuleset, "include1.ruleset");
            RuleSetAssertions.AssertExpectedIncludeAction(primaryRuleset, "include1.ruleset", RuleSetAssertions.DefaultActionValue);

            // 3. Adding an include that already exists
            primaryRuleset = this.CreateValidRuleset("added-includes.existing.ruleset.txt");
            ExecuteAndCheckSuccess(primaryRuleset, "include1.ruleset", "include2.ruleset"); // create a file with incldues
            ExecuteAndCheckSuccess(primaryRuleset, "include1.ruleset", "INCLUDE2.RULESET", "include3.ruleset"); // add the same includes again with one extra
            RuleSetAssertions.AssertExpectedIncludeFiles(primaryRuleset, "include1.ruleset", "include2.ruleset", "include3.ruleset");

            RuleSetAssertions.AssertExpectedIncludeAction(primaryRuleset, "include1.ruleset", RuleSetAssertions.DefaultActionValue);
            RuleSetAssertions.AssertExpectedIncludeAction(primaryRuleset, "include2.ruleset", RuleSetAssertions.DefaultActionValue);
            RuleSetAssertions.AssertExpectedIncludeAction(primaryRuleset, "include3.ruleset", RuleSetAssertions.DefaultActionValue);
        }

        [TestMethod]
        public void MergeRulesets_EmptyRuleset()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string primaryRuleset = TestUtils.CreateTextFile(testDir, "empty.ruleset.txt",
@"<RuleSet Name = 'RulesetName' ToolsVersion = '14.0' />");

            // Act
            ExecuteAndCheckSuccess(primaryRuleset, "c:\\foo\\added.ruleset");

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
            ExecuteAndCheckSuccess(primaryRuleset, "d:\\include1.ruleset");

            // Assert
            RuleSetAssertions.AssertExpectedIncludeFiles(primaryRuleset, "d:\\include1.ruleset");
            RuleSetAssertions.AssertExpectedIncludeAction(primaryRuleset, "d:\\include1.ruleset", ErrorActionValue); // action value should still be Error
        }

        #endregion

        #region Checks

        private void ExecuteAndCheckSuccess(string primaryRuleset, params string[] rulesetsToInclude)
        {
            this.TestContext.AddResultFile(primaryRuleset);

            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            MergeRuleSets task = CreateTask(dummyEngine, primaryRuleset, rulesetsToInclude);

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

        private static MergeRuleSets CreateTask(DummyBuildEngine buildEngine, string primaryRuleset, params string[] rulesetsToInclude)
        {
            MergeRuleSets task = new MergeRuleSets();
            task.BuildEngine = buildEngine;
            task.PrimaryRulesetFilePath = primaryRuleset;
            task.IncludedRulesetFilePaths = rulesetsToInclude;

            return task;
        }

        #endregion
    }
}
