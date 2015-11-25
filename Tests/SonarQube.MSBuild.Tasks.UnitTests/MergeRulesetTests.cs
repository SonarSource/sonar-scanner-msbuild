//-----------------------------------------------------------------------
// <copyright file="MergeRulesetTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.UnitTests
{
    [TestClass]
    public class MergeRulesetTests
    {
        private const string DefaultActionValue = "Default";
        private const string ErrorActionValue = "Error";
        private static readonly XName IncludeElementName = "Include";
        private static readonly XName PathAttrName = "Path";
        private static readonly XName ActionAttrName = "Action";


        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void MergeRulesets_PrimaryRulesetDoesNotExist()
        {
            // Arrange
            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            MergeRulesets task = CreateTask(dummyEngine, "missing.ruleset");

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
            AssertExpectedIncludeFiles(primaryRuleset /* no included files*/);

            // 2. Adding includes
            primaryRuleset = this.CreateValidRuleset("added-includes1.ruleset.txt");
            ExecuteAndCheckSuccess(primaryRuleset, "include1.ruleset");
            AssertExpectedIncludeFiles(primaryRuleset, "include1.ruleset");
            AssertExpectedAction(primaryRuleset, "include1.ruleset", DefaultActionValue);

            // 3. Adding an include that already exists
            primaryRuleset = this.CreateValidRuleset("added-includes.existing.ruleset.txt");
            ExecuteAndCheckSuccess(primaryRuleset, "include1.ruleset", "include2.ruleset"); // create a file with incldues
            ExecuteAndCheckSuccess(primaryRuleset, "include1.ruleset", "INCLUDE2.RULESET", "include3.ruleset"); // add the same includes again with one extra
            AssertExpectedIncludeFiles(primaryRuleset, "include1.ruleset", "include2.ruleset", "include3.ruleset");

            AssertExpectedAction(primaryRuleset, "include1.ruleset", DefaultActionValue);
            AssertExpectedAction(primaryRuleset, "include2.ruleset", DefaultActionValue);
            AssertExpectedAction(primaryRuleset, "include3.ruleset", DefaultActionValue);
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
            AssertExpectedIncludeFiles(primaryRuleset, "c:\\foo\\added.ruleset");
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
            AssertExpectedIncludeFiles(primaryRuleset, "d:\\include1.ruleset");
            AssertExpectedAction(primaryRuleset, "d:\\include1.ruleset", ErrorActionValue); // action value should still be Error
        }

        #endregion

        #region Checks

        private void ExecuteAndCheckSuccess(string primaryRuleset, params string[] rulesetsToInclude)
        {
            this.TestContext.AddResultFile(primaryRuleset);

            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            MergeRulesets task = CreateTask(dummyEngine, primaryRuleset, rulesetsToInclude);

            bool taskSucess = task.Execute();
            Assert.IsTrue(taskSucess, "Expecting the task to succeed");
            dummyEngine.AssertNoErrors();
            dummyEngine.AssertNoWarnings();
        }
       
        private static void AssertExpectedIncludeFiles(string primaryRulesetFilePath, params string[] expectedRulesets)
        {
            XDocument doc = XDocument.Load(primaryRulesetFilePath);
            IEnumerable<XElement> includeElements = doc.Descendants(IncludeElementName);
            foreach (string expected in expectedRulesets)
            {
                AssertSingleIncludeExists(includeElements, expected);
            }

            Assert.AreEqual(expectedRulesets.Length, includeElements.Count(), "Unexpected number of Includes");
        }

        private static void AssertExpectedAction(string primaryRulesetFilePath, string includePath, string expectedAction)
        {
            XDocument doc = XDocument.Load(primaryRulesetFilePath);
            IEnumerable<XElement> includeElements = doc.Descendants(IncludeElementName);
            XElement includeElement = AssertSingleIncludeExists(includeElements, includePath);

            XAttribute actionAttr = includeElement.Attribute(ActionAttrName);

            Assert.IsNotNull(actionAttr, "Include element does not have an Action attribute: {0}", includeElement);
            Assert.AreEqual(expectedAction, actionAttr.Value, "Unexpected Action value");
        }

        private static XElement AssertSingleIncludeExists(IEnumerable<XElement> includeElements, string expectedPath)
        {
            IEnumerable<XElement> matches = includeElements.Where(i => HasIncludePath(i, expectedPath));
            Assert.AreEqual(1, matches.Count(), "Expecting one and only Include with Path '{0}'", expectedPath);
            return matches.First();
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

        private static MergeRulesets CreateTask(DummyBuildEngine buildEngine, string primaryRuleset, params string[] rulesetsToInclude)
        {
            MergeRulesets task = new MergeRulesets();
            task.BuildEngine = buildEngine;
            task.PrimaryRulesetFilePath = primaryRuleset;
            task.IncludedRulesetFilePaths = rulesetsToInclude;

            return task;
        }

        private static bool HasIncludePath(XElement includeElement, string includePath)
        {
            XAttribute attr;
            attr = includeElement.Attributes(PathAttrName).Single();

            return attr != null && string.Equals(attr.Value, includePath, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
