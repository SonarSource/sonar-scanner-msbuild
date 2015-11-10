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
        [Description("Checks the target is not executed if the temp folder is not set")]
        public void Roslyn_Settings_TempFolderIsNotSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = "";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynSettingsTarget);

            // Assert
            logger.AssertTargetNotExecuted(TargetConstants.OverrideRoslynSettingsTarget);
        }

        [TestMethod]
        [Description("Checks the settings are not set if the ruleset does not exist")]
        public void Roslyn_Settings_RuleSetDoesNotExist()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.CodeAnalysisRuleset = "ruleset.set.by.the.project.that.should.be.overridden";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynSettingsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynSettingsTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynSettingsTarget);

            AssertCodeAnalysisIsDisabled(result);
        }

        [TestMethod]
        [Description("Checks the properties are set correctly if the ruleset exists")]
        public void Roslyn_Settings_RuleSetExists()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rulesetFilePath = CreateDummyRuleset(rootInputFolder);

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.CodeAnalysisRuleset = "should.be.overwritten";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynSettingsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynSettingsTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynSettingsTarget);

            // Check the intermediate working properties have the expected values
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynRulesetExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRunRoslynCodeAnalysis", "True");

            // Check the error log and ruleset properties are set
            string targetDir = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.TargetDir);
            string expectedErrorLog = Path.Combine(targetDir, "SonarQube.Roslyn.ErrorLog.xml");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.ErrorLog, expectedErrorLog);
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.CodeAnalysisRuleset, rulesetFilePath);
        }

        [TestMethod]
        [Description("Checks an existing errorLog value is used if set")]
        public void Roslyn_Settings_ErrorLogAlreadySet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            // The ruleset must exist for properties to be set
            CreateDummyRuleset(rootInputFolder);

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;

            properties[TargetProperties.ErrorLog] = "already.set.txt";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynSettingsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynSettingsTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynSettingsTarget);

            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.ErrorLog, "already.set.txt");
        }

        [TestMethod]
        [Description("Checks the code analysis properties are cleared for test projects")]
        public void Roslyn_Settings_NotRunForTestProject()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            
            // The ruleset must exist for properties to be set
            CreateDummyRuleset(rootInputFolder);

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.ProjectTypeGuids = TargetConstants.MsTestProjectTypeGuid; // mark the project as a test project

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynSettingsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynSettingsTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynSettingsTarget);

            // Check the intermediate working properties have the expected values
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynRulesetExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRunRoslynCodeAnalysis", "false");

            AssertCodeAnalysisIsDisabled(result);
        }

        [TestMethod]
        [Description("Checks the code analysis properties are cleared for excludedprojects")]
        public void Roslyn_Settings_NotRunForExcludedProject()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            // The ruleset must exist for properties to be set
            CreateDummyRuleset(rootInputFolder);

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.SonarQubeExclude = "true"; // mark the project as excluded

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynSettingsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynSettingsTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynSettingsTarget);

            // Check the intermediate working properties have the expected values
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRoslynRulesetExists", "True");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, "SonarQubeRunRoslynCodeAnalysis", "false");

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
            BuildResult actualResult = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.SetRoslynResultsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.SetRoslynResultsTarget);
            BuildAssertions.AssertAnalysisSettingDoesNotExist(actualResult, RoslynAnalysisResultsSettingName);
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
            BuildResult actualResult = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.SetRoslynResultsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.SetRoslynResultsTarget);
            BuildAssertions.AssertExpectedAnalysisSetting(actualResult, RoslynAnalysisResultsSettingName, resultsFile);
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
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynSettingsTarget);
            logger.AssertExpectedTargetOrdering(
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.OverrideRoslynSettingsTarget,
                "ResolveCodeAnalysisRuleSet",
                TargetConstants.CoreCompile,
                TargetConstants.DefaultBuildTarget,
                TargetConstants.SetRoslynResultsTarget,
                TargetConstants.WriteProjectDataTarget);
        }

        #endregion

        #region Private methods

        private static string CreateDummyRuleset(string rootFolderPath)
        {
            string configFolder = Path.Combine(rootFolderPath, "conf");
            if (!Directory.Exists(configFolder))
            {
                Directory.CreateDirectory(configFolder);
            }
            string rulesetFilePath = TestUtils.CreateTextFile(configFolder, "SonarQubeRoslyn-cs.ruleset", "dummy rules");
            return rulesetFilePath;
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
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.ErrorLog, string.Empty);
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.CodeAnalysisRuleset, string.Empty);
        }

        #endregion
    }
}
