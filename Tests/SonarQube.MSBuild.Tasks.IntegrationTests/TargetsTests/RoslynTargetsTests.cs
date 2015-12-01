//-----------------------------------------------------------------------
// <copyright file="RoslynTargetsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class RoslynTargetsTests
    {
        private const string RoslynAnalysisResultsSettingName = "sonar.cs.roslyn.reportFilePath";
        private const string ErrorLogFileName = "SonarQube.Roslyn.ErrorLog.json";

        private static readonly string[] SonarLintAnalyzerFileNames = { "SonarLint.dll", "SonarLint.CSharp.dll" };

        public TestContext TestContext { get; set; }

        #region SetRoslynSettingsTarget tests

        [TestMethod]
        [Description("Checks the happy path i.e. ruleset exists, analyzer assembly exists, no reason to exclude the project")]
        public void Roslyn_Settings_ValidSetup()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();
            ProjectRootElement projectRoot = CreateValidProjectSetup(null);
            
            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            logger.AssertTaskNotExecuted(TargetConstants.MergeResultSetsTask);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            // Check the intermediate working properties have the expected values
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynRulesetExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynAssemblyExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarLintFound", "true");

            // Check the error log and ruleset properties are set
            string targetDir = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.TargetDir);
            string expectedErrorLog = Path.Combine(targetDir, ErrorLogFileName);
            AssertExpectedAnalysisProperties(result, expectedErrorLog, GetDummyRulesetFilePath(), GetDummySonarLintXmlFilePath());
            AssertExpectedItemValuesExists(result, TargetProperties.AnalyzerItemType, GetSonarLintAnalyzerFilePaths());
        }

        [TestMethod]
        [Description("Checks the ruleset and analyzers list are correctly merged with existing settings")]
        public void Roslyn_Settings_ValidSetup_MergeWithExistingSettings_AbsolutePath()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();

            // Set the ruleset property
            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.ResolvedCodeAnalysisRuleset = "c:\\existing.ruleset";

            // Add some existing analyzer settings
            ProjectRootElement projectRoot = CreateValidProjectSetup(properties);
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "Analyzer1.dll");
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "c:\\Analyzer2.dll");

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            logger.AssertTaskExecuted(TargetConstants.MergeResultSetsTask);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            // Check the error log and ruleset properties are set
            string finalRulesetFilePath = GetDummyRulesetFilePath();
            this.TestContext.AddResultFile(finalRulesetFilePath);

            string targetDir = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.TargetDir);
            string expectedErrorLog = Path.Combine(targetDir, ErrorLogFileName);
            AssertExpectedAnalysisProperties(result, expectedErrorLog, finalRulesetFilePath, GetDummySonarLintXmlFilePath());

            RuleSetAssertions.AssertExpectedIncludeFiles(finalRulesetFilePath, "c:\\existing.ruleset");
            RuleSetAssertions.AssertExpectedIncludeAction(finalRulesetFilePath, "c:\\existing.ruleset", RuleSetAssertions.DefaultActionValue);
        }

        [TestMethod]
        [Description("Checks the ruleset and analyzers list are correctly merged with existing settings")]
        public void Roslyn_Settings_ValidSetup_MergeWithExistingSettings_RelativePath()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();

            // Add some existing analyzer settings
            ProjectRootElement projectRoot = CreateValidProjectSetup(null);

            // Create a ruleset file in a location relative to the project file
            string projectFilePath = projectRoot.ProjectFileLocation.File;
            string rulesetDir = Path.Combine(Path.GetDirectoryName(projectFilePath), "subdir");
            Directory.CreateDirectory(rulesetDir);
            string rulesetFilePath = TestUtils.CreateTextFile(rulesetDir, "existing.ruleset", "dummy ruleset");

            projectRoot.AddProperty(TargetProperties.ResolvedCodeAnalysisRuleset, "subdir\\existing.ruleset");

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            logger.AssertTaskExecuted(TargetConstants.MergeResultSetsTask);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            // Check the error log and ruleset properties are set
            string finalRulesetFilePath = GetDummyRulesetFilePath();
            this.TestContext.AddResultFile(finalRulesetFilePath);

            string targetDir = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.TargetDir);
            string expectedErrorLog = Path.Combine(targetDir, ErrorLogFileName);
            AssertExpectedAnalysisProperties(result, expectedErrorLog, finalRulesetFilePath, GetDummySonarLintXmlFilePath());

            RuleSetAssertions.AssertExpectedIncludeFiles(finalRulesetFilePath, rulesetFilePath);
            RuleSetAssertions.AssertExpectedIncludeAction(finalRulesetFilePath, rulesetFilePath, RuleSetAssertions.DefaultActionValue);
        }

        [TestMethod]
        [Description("Checks the target is not executed if the temp folder is not set")]
        public void Roslyn_Settings_TempFolderIsNotSet()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.ErrorLog = "pre-existing.log";
            properties.ResolvedCodeAnalysisRuleset = "pre-existing.ruleset";

            ProjectRootElement projectRoot = CreateValidProjectSetup(properties);
            projectRoot.AddProperty(TargetProperties.SonarQubeTempPath, string.Empty); // needs to overwritten once the valid project has been created

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetNotExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetNotExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            AssertExpectedAnalysisProperties(result, "pre-existing.log", "pre-existing.ruleset", string.Empty); // existing properties should not be changed
        }

        [TestMethod]
        [Description("Checks the settings are not set if the ruleset does not exist")]
        public void Roslyn_Settings_RuleSetDoesNotExist()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();
            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.ResolvedCodeAnalysisRuleset = "pre-existing.ruleset";
            properties.ErrorLog = "pre-existing.log";

            ProjectRootElement projectRoot = CreateValidProjectSetup(properties);
            this.DeleteDummyRulesetFile();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            // Check the intermediate working properties have the expected values
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynRulesetExists", "False");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynAssemblyExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarLintFound", "false");

            AssertExpectedAnalysisProperties(result, "pre-existing.log", "pre-existing.ruleset", string.Empty);
        }

        [TestMethod]
        [Description("Checks an existing errorLog value is used if set")]
        public void Roslyn_Settings_ErrorLogAlreadySet()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();
            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties[TargetProperties.ErrorLog] = "already.set.txt";

            ProjectRootElement projectRoot = CreateValidProjectSetup(properties);

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            // Check the intermediate working properties have the expected values
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynRulesetExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynAssemblyExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarLintFound", "true");

            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.ErrorLog, "already.set.txt");
        }

        [TestMethod]
        [Description("Checks that the SonarLint additional file is appended to the existing one")]
        public void Roslyn_Settings_AdditionalFiles_Appended()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();
            WellKnownProjectProperties properties = new WellKnownProjectProperties();

            ProjectRootElement projectRoot = CreateValidProjectSetup(properties);

            projectRoot.AddItem(TargetProperties.AdditionalFilesItemType, "foo.txt");
            projectRoot.AddItem(TargetProperties.AdditionalFilesItemType, "c:\\bar.txt");

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            // Check the intermediate working properties have the expected values
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynRulesetExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarLintFound", "true");

            // Check the analyzer properties are set as expected
            List<string> expectedAnalyzers = new List<string>();
            expectedAnalyzers.Add("foo.txt");
            expectedAnalyzers.Add("c:\\bar.txt");
            expectedAnalyzers.Add(GetDummySonarLintXmlFilePath());

            AssertExpectedItemValuesExists(result, TargetProperties.AdditionalFilesItemType, expectedAnalyzers.ToArray());
        }

        [TestMethod]
        [Description("Checks the analyzer assembly is added if the analyzer assembly exists")]
        public void Roslyn_Settings_Analyzer_AssemblyExists()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();
            WellKnownProjectProperties properties = new WellKnownProjectProperties();

            ProjectRootElement projectRoot = CreateValidProjectSetup(properties);

            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "analyzer1.dll"); // additional -> preserve
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "c:\\myfolder\\analyzer2.dll"); // additional -> preserve
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "sonarlint.dll"); // relative path -> remove
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "c:\\myfolder\\sonarlint.dll"); // absolute path -> remove
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "XXSONARLINT.dll"); // case-sensitivity -> remove
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "sonarLint.dll.xxx"); // doesn't match -> preserve
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "c:\\sonarLint\\my.dll"); // doesn't match -> preserve

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            // Check the intermediate working properties have the expected values
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynRulesetExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarLintFound", "true");

            // Check the analyzer properties are set as expected
            List<string> expectedAnalyzers = new List<string>(GetSonarLintAnalyzerFilePaths());
            expectedAnalyzers.Add("analyzer1.dll");
            expectedAnalyzers.Add("c:\\myfolder\\analyzer2.dll");
            expectedAnalyzers.Add("sonarLint.dll.xxx");
            expectedAnalyzers.Add("c:\\sonarLint\\my.dll");

            AssertExpectedItemValuesExists(result, TargetProperties.AnalyzerItemType, expectedAnalyzers.ToArray());
        }

        [TestMethod]
        [Description("Checks the analyzer is not added if the assembly is not found")]
        public void Roslyn_Settings_Analyzer_AssemblyDoesNotExist()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();
            ProjectRootElement projectRoot = CreateValidProjectSetup(null);

            this.DeleteDummySonarLintFiles();

            // The downloaded SonarLint.dll could not be found so any existing settings should be preserved
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "analyzer1.dll");
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "c:\\myfolder\\analyzer2.dll");
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "c:\\myfolder\\sonarlint.dll");

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            // Check the intermediate working properties have the expected values
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynRulesetExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarLintFound", "false");

            // Check the analyzer properties are set as expected
            AssertExpectedItemValuesExists(result,
                TargetProperties.AnalyzerItemType,
                "analyzer1.dll",
                "c:\\myfolder\\analyzer2.dll",
                "c:\\myfolder\\sonarlint.dll");
        }

        [TestMethod]
        [Description("Checks the code analysis properties are cleared for test projects")]
        public void Roslyn_Settings_NotRunForTestProject()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();
            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.ProjectTypeGuids = TargetConstants.MsTestProjectTypeGuid; // mark the project as a test project

            ProjectRootElement projectRoot = CreateValidProjectSetup(properties);

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetNotExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            AssertCodeAnalysisIsDisabled(result);
        }

        [TestMethod]
        [Description("Checks the code analysis properties are cleared for excludedprojects")]
        public void Roslyn_Settings_NotRunForExcludedProject()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();
            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeExclude = "TRUE"; // mark the project as excluded

            ProjectRootElement projectRoot = CreateValidProjectSetup(properties);

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetNotExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            AssertCodeAnalysisIsDisabled(result);
        }

        #endregion

        #region AddAnalysisResults tests

        [TestMethod]
        [Description("Checks the target is not executed if the temp folder is not set")]
        public void Roslyn_SetResults_TempFolderIsNotSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = "";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.SetRoslynResultsTarget);

            // Assert
            logger.AssertTargetNotExecuted(TargetConstants.SetRoslynResultsTarget);
        }

        [TestMethod]
        [Description("Checks the analysis setting is not set if the results file does not exist")]
        public void Roslyn_SetResults_ResultsFileDoesNotExist()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.SetRoslynResultsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.SetRoslynResultsTarget);
            BuildAssertions.AssertAnalysisSettingDoesNotExist(result, RoslynAnalysisResultsSettingName);
        }

        [TestMethod]
        [Description("Checks the analysis setting is set if the result file exists")]
        public void Roslyn_SetResults_ResultsFileExists()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            string resultsFile = TestUtils.CreateTextFile(rootInputFolder, "error.report.txt", "dummy report content");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties[TargetProperties.ErrorLog] = resultsFile;

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.SetRoslynResultsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.SetRoslynResultsTarget);
            BuildAssertions.AssertExpectedAnalysisSetting(result, RoslynAnalysisResultsSettingName, resultsFile);
        }

        #endregion

        #region Combined tests

        [TestMethod]
        [Description("Checks the targets are executed in the expected order")]
        public void Roslyn_TargetExecutionOrder()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.SonarQubeOutputPath = rootInputFolder;
            properties.SonarQubeConfigPath = rootOutputFolder;

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);


            // Add some settings we expect to be ignored
            AddAnalysisSetting("sonar.other.setting", "other value", projectRoot);
            projectRoot.Save();

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.DefaultBuildTarget);

            // Assert
            // Checks that should succeed irrespective of the MSBuild version
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);

            // Note: use VS2013 to run this test using MSBuild 12.0.
            // Use VS2015 to run this test using MSBuild 14.0.
            if (result.ProjectStateAfterBuild.ToolsVersion.CompareTo("14.0") < 0)
            {
                logger.AssertTargetNotExecuted(TargetConstants.ResolveCodeAnalysisRuleSet); // sanity-check: should only be in MSBuild 14.0+
                Assert.Inconclusive("This test requires MSBuild 14.0 to be installed. Version used: {0}", result.ProjectStateAfterBuild.ToolsVersion);
            }
            else
            {
                // MSBuild 14.0+ checks
                logger.AssertExpectedTargetOrdering(
                    TargetConstants.ResolveCodeAnalysisRuleSet,
                    TargetConstants.CategoriseProjectTarget,
                    TargetConstants.OverrideRoslynAnalysisTarget,
                    TargetConstants.SetRoslynAnalysisPropertiesTarget,
                    TargetConstants.CoreCompile,
                    TargetConstants.DefaultBuildTarget,
                    TargetConstants.SetRoslynResultsTarget,
                    TargetConstants.WriteProjectDataTarget);
            }
        }

        #endregion

        #region Private methods

        private string GetRootDirectory()
        {
            return TestUtils.GetTestSpecificFolderName(this.TestContext);
        }

        private string GetDummyRulesetFilePath()
        {
            return Path.Combine(this.GetRootDirectory(), "conf", "SonarQubeRoslyn-cs.ruleset");
        }

        private string GetDummySonarLintXmlFilePath()
        {
            return Path.Combine(this.GetRootDirectory(), "conf", "SonarLint.xml");
        }

        private string[] GetSonarLintAnalyzerFilePaths()
        {
            string rootDir = this.GetRootDirectory();
            List<string> fullPaths = new List<string>();

            foreach(string file in SonarLintAnalyzerFileNames)
            {
                fullPaths.Add(Path.Combine(rootDir, file));
            }
            return fullPaths.ToArray();
        }

        private string CreateDummyRuleset()
        {
            string rulesetFilePath = GetDummyRulesetFilePath();

            Directory.CreateDirectory(Path.GetDirectoryName(rulesetFilePath));
            File.WriteAllText(rulesetFilePath,
@"<?xml version='1.0' encoding='utf-8'?>
<RuleSet Name='DummyRuleSet' ToolsVersion='14.0'>
  <Rules AnalyzerId='My.Analyzer' RuleNamespace='My.Analyzers'>
    <Rule Id='Rule001' Action='Error' />
  </Rules>
</RuleSet>");
            return rulesetFilePath;
        }

        private void DeleteDummyRulesetFile()
        {
            string dummyFilePath = this.GetDummyRulesetFilePath();
            if (!File.Exists(dummyFilePath))
            {
                Assert.Inconclusive("Test error: expecting the dummy ruleset file to exist: {0}", dummyFilePath);
            }
            File.Delete(dummyFilePath);
        }

        private void CreateDummySonarLintFiles()
        {
            foreach (string dummyFilePath in this.GetSonarLintAnalyzerFilePaths())
            {
                File.WriteAllText(dummyFilePath, "dummy sonarlint assembly");
            }
        }

        private void DeleteDummySonarLintFiles()
        {
            foreach (string dummyFilePath in this.GetSonarLintAnalyzerFilePaths())
            {
                if (!File.Exists(dummyFilePath))
                {
                    Assert.Inconclusive("Test error: expecting the dummy sonarlint file to exist: {0}", dummyFilePath);
                }
                File.Delete(dummyFilePath);
            }
        }

        private static void AddAnalysisSetting(string name, string value, ProjectRootElement project)
        {
            ProjectItemElement element = project.AddItem(BuildTaskConstants.SettingItemName, name);
            element.AddMetadata(BuildTaskConstants.SettingValueMetadataName, value);
        }

        #endregion

        #region Checks

        private static void AssertCodeAnalysisIsDisabled(BuildResult result)
        {
            // Check the ruleset and error log are not set
            AssertExpectedAnalysisProperties(result, string.Empty, string.Empty, string.Empty);
        }

        private static void AssertExpectedAnalysisProperties(BuildResult result, string expectedErrorLog, string expectedResolvedRuleset, string expectedAdditionalFilesElement)
        {
            // Check the ruleset and error log are not set
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.ErrorLog, expectedErrorLog);
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.ResolvedCodeAnalysisRuleset, expectedResolvedRuleset);

            if (!string.IsNullOrEmpty(expectedAdditionalFilesElement))
            {
                // Check that the additional files element is set to the expected value
                BuildAssertions.AssertSingleItemExists(result, TargetProperties.AdditionalFilesItemType, expectedAdditionalFilesElement);
                BuildAssertions.AssertExpectedItemGroupCount(result, TargetProperties.AdditionalFilesItemType, 1);
            }
            else
            {
                // Check that the additional files element is not set
                BuildAssertions.AssertExpectedItemGroupCount(result, TargetProperties.AdditionalFilesItemType, 0);
            }
        }

        private void AssertExpectedItemValuesExists(BuildResult result, string itemType, params string[] expectedValues)
        {
            this.DumpLists(result, itemType, expectedValues);
            foreach (string expectedValue in expectedValues)
            {
                BuildAssertions.AssertSingleItemExists(result, itemType, expectedValue);
            }
            BuildAssertions.AssertExpectedItemGroupCount(result, itemType, expectedValues.Length);
        }


        private void DumpLists(BuildResult actualResult, string itemType, string[] expected)
        {
            this.TestContext.WriteLine("");
            this.TestContext.WriteLine("Dumping <" + itemType + "> list: expected");
            foreach (string item in expected)
            {
                this.TestContext.WriteLine("\t{0}", item);
            }
            this.TestContext.WriteLine("");

            this.TestContext.WriteLine("");
            this.TestContext.WriteLine("Dumping <" + itemType + "> list: actual");
            foreach (ProjectItemInstance item in actualResult.ProjectStateAfterBuild.GetItems(itemType))
            {
                this.TestContext.WriteLine("\t{0}", item.EvaluatedInclude);
            }
            this.TestContext.WriteLine("");
        }

        #endregion

        #region Setup

        /// <summary>
        /// Creates a valid project with the necessary ruleset and assembly files on disc
        /// to successfully run the "OverrideRoslynCodeAnalysisProperties" target
        /// </summary>
        private ProjectRootElement CreateValidProjectSetup(WellKnownProjectProperties properties)
        {
            string sqTempFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            CreateDummySonarLintFiles();
            CreateDummyRuleset();

            WellKnownProjectProperties projectProperties = properties ?? new WellKnownProjectProperties();
            string projectFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Project");

            projectProperties.SonarQubeTempPath = sqTempFolder;
            projectProperties[TargetProperties.Language] = "C#";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, projectFolder, projectProperties);

            string projectFilePath = Path.Combine(projectFolder, "valid.project.proj");
            projectRoot.Save(projectFilePath);
            return projectRoot;
        }

        #endregion

    }
}
