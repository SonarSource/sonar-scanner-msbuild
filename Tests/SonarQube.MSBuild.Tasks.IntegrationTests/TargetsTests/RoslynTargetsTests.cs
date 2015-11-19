//-----------------------------------------------------------------------
// <copyright file="RoslynTargetsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class RoslynTargetsTests
    {
        private const string RoslynAnalysisResultsSettingName = "sonar.cs.roslyn.reportFilePath";

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

            // Check the intermediate working properties have the expected values
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynRulesetExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynAssemblyExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRunRoslynCodeAnalysis", "true");

            // Check the error log and ruleset properties are set
            string targetDir = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.TargetDir);
            string expectedErrorLog = Path.Combine(targetDir, "SonarQube.Roslyn.ErrorLog.json");
            AssertExpectedAnalysisProperties(result, expectedErrorLog, GetDummyRulesetFilePath());
            AssertExpectedAnalyzersExists(result, GetDummySonarLintFilePath());
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
            AssertExpectedAnalysisProperties(result, "pre-existing.log", "pre-existing.ruleset"); // existing properties should not be changed
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
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRunRoslynCodeAnalysis", "false");

            AssertExpectedAnalysisProperties(result, "pre-existing.log", "pre-existing.ruleset");
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
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRunRoslynCodeAnalysis", "true");

            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.ErrorLog, "already.set.txt");
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

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            // Check the intermediate working properties have the expected values
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynRulesetExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRunRoslynCodeAnalysis", "true");

            // Check the analyzer properties are set as expected
            AssertExpectedAnalyzersExists(result,
                "analyzer1.dll",
                "c:\\myfolder\\analyzer2.dll",
                "sonarLint.dll.xxx",
                GetDummySonarLintFilePath());
        }

        [TestMethod]
        [Description("Checks the analyzer is not added if the assembly is not found")]
        public void Roslyn_Settings_Analyzer_AssemblyDoesNotExist()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();
            ProjectRootElement projectRoot = CreateValidProjectSetup(null);

            this.DeleteDummySonarLintFile();

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
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRunRoslynCodeAnalysis", "false");

            // Check the analyzer properties are set as expected
            AssertExpectedAnalyzersExists(result,
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

        private string GetDummySonarLintFilePath()
        {
            return Path.Combine(this.GetRootDirectory(), "SonarLint.dll");
        }

        private string CreateDummyRuleset()
        {
            string rulesetFilePath = GetDummyRulesetFilePath();

            Directory.CreateDirectory(Path.GetDirectoryName(rulesetFilePath));
            File.WriteAllText(rulesetFilePath, "dummy ruleset");
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

        private string CreateDummySonarLintFile()
        {
            string dummyFilePath = GetDummySonarLintFilePath();
            File.WriteAllText(dummyFilePath, "dummy sonarlint assembly");
            return dummyFilePath;
        }

        private void DeleteDummySonarLintFile()
        {
            string dummyFilePath = GetDummySonarLintFilePath();
            if (!File.Exists(dummyFilePath))
            {
                Assert.Inconclusive("Test error: expecting the dummy sonarlint file to exist: {0}", dummyFilePath);
            }
            File.Delete(dummyFilePath);
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
            AssertExpectedAnalysisProperties(result, string.Empty, string.Empty);
        }

        private static void AssertExpectedAnalysisProperties(BuildResult result, string expectedErrorLog, string expectedResolvedRuleset)
        {
            // Check the ruleset and error log are not set
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.ErrorLog, expectedErrorLog);
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.ResolvedCodeAnalysisRuleset, expectedResolvedRuleset);
        }

        private static void AssertExpectedAnalyzersExists(BuildResult result, params string[] analyzerFilePaths)
        {
            foreach (string filePath in analyzerFilePaths)
            {
                BuildAssertions.AssertSingleItemExists(result, TargetProperties.AnalyzerItemType, filePath);
            }
            BuildAssertions.AssertExpectedItemGroupCount(result, TargetProperties.AnalyzerItemType, analyzerFilePaths.Length);
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
            CreateDummySonarLintFile();
            CreateDummyRuleset();

            WellKnownProjectProperties projectProperties = properties ?? new WellKnownProjectProperties();
            string projectFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Project");

            projectProperties.SonarQubeTempPath = sqTempFolder;
            projectProperties[TargetProperties.Language] = "C#";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, projectFolder, projectProperties);
            return projectRoot;
        }

        #endregion

    }
}
