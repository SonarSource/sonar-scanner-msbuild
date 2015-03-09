//-----------------------------------------------------------------------
// <copyright file="E2EFxCopTests.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sonar.Common;
using System.IO;
using TestUtilities;

namespace SonarMSBuild.Tasks.IntegrationTests.E2E
{

    [TestClass]
    [DeploymentItem("LinkedFiles\\Sonar.Integration.v0.1.targets")]
    public class E2EFxCopTests
    {
        private const string ExpectedCompileListFileName = "CompileList.txt";

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [Description("If the output folder is not set our custom targets should not be executed")]
        public void E2E_FxCop_OutputFolderNotSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            // Don't set the output folder
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarAnalysis = "true";
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            BuildLogger logger = new BuildLogger();

            // 1. No code analysis properties
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertTargetNotExecuted(TargetConstants.SonarOverrideFxCopSettingsTarget);
            logger.AssertTargetNotExecuted(TargetConstants.SonarSetFxCopResultsTarget);

            AssertFxCopNotExecuted(logger);

            ProjectInfoAssertions.AssertNoProjectInfoFilesExists(rootOutputFolder);
        }

        [TestMethod]
        [Description("If the RunSonarAnalysis is not set our custom targets should not be executed")]
        public void E2E_FxCop_RunSonarAnalysisNotSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarOutputPath = rootOutputFolder;

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            BuildLogger logger = new BuildLogger();

            // 1. No code analysis properties
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertTargetNotExecuted(TargetConstants.SonarOverrideFxCopSettingsTarget);
            logger.AssertTargetNotExecuted(TargetConstants.SonarSetFxCopResultsTarget);

            AssertFxCopNotExecuted(logger);

            ProjectInfoAssertions.AssertNoProjectInfoFilesExists(rootOutputFolder);
        }

        [TestMethod]
        [Description("FxCop analysis should not be run if the output folder is set but a custom ruleset isn't specified")]
        public void E2E_FxCop_OutputFolderSet_SonarRulesetNotSpecified()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            // Set the output folder but not the config folder
            string fxCopLogFile = Path.Combine(rootInputFolder, "FxCopResults.xml");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarAnalysis = "true";
            preImportProperties.SonarOutputPath = rootOutputFolder;
            preImportProperties.RunCodeAnalysis = "TRUE";
            preImportProperties.CodeAnalysisLogFile = fxCopLogFile;
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertTargetExecuted(TargetConstants.SonarOverrideFxCopSettingsTarget); // output folder is set so this should be executed
            logger.AssertTargetNotExecuted(TargetConstants.SonarSetFxCopResultsTarget);

            AssertFxCopNotExecuted(logger);
            Assert.IsFalse(File.Exists(fxCopLogFile), "FxCop log file should not have been produced");

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);
            ProjectInfoAssertions.AssertAnalysisResultDoesNotExists(projectInfo, AnalysisType.FxCop.ToString());
        }

        [TestMethod]
        [Description("FxCop analysis should not be run if the output folder is set but the custom ruleset couldn't be found")]
        public void E2E_FxCop_OutputFolderSet_SonarRulesetNotFound()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            // Set the output folder and config path
            // Don't create a ruleset file on disc
            string fxCopLogFile = Path.Combine(rootInputFolder, "FxCopResults.xml");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarAnalysis = "true";
            preImportProperties.SonarOutputPath = rootOutputFolder;
            preImportProperties.RunCodeAnalysis = "true"; // our targets should override this value
            preImportProperties.CodeAnalysisLogFile = fxCopLogFile;
            preImportProperties.SonarConfigPath = rootInputFolder;
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertTargetExecuted(TargetConstants.SonarOverrideFxCopSettingsTarget);  // output folder is set so this should be executed
            logger.AssertTargetNotExecuted(TargetConstants.SonarSetFxCopResultsTarget);

            // We expect the core FxCop *target* to have been started, but it should then be skipped
            // executing the FxCop *task* because the condition on the target is false
            // -> the FxCop output file should not be produced
            AssertFxCopNotExecuted(logger);

            Assert.IsFalse(File.Exists(fxCopLogFile), "FxCop log file should not have been produced");

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);
            ProjectInfoAssertions.AssertAnalysisResultDoesNotExists(projectInfo, AnalysisType.FxCop.ToString());
        }

        [TestMethod]
        [Description("FxCop analysis should be run if the output folder is set and the ruleset can be found")]
        public void E2E_FxCop_AllConditionsMet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            string fxCopLogFile = Path.Combine(rootInputFolder, "FxCopResults.xml");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarAnalysis = "true";
            preImportProperties.SonarOutputPath = rootOutputFolder;
            preImportProperties.RunCodeAnalysis = "false";
            preImportProperties.CodeAnalysisLogFile = fxCopLogFile;
            preImportProperties.CodeAnalysisRuleset = "specifiedInProject.ruleset";

            preImportProperties["SonarConfigPath"] = rootInputFolder;
            CreateValidFxCopRuleset(rootInputFolder, "SonarAnalysis.ruleset");

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            AssertAllFxCopTargetsExecuted(logger);
            Assert.IsTrue(File.Exists(fxCopLogFile), "FxCop log file should have been produced");

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);
            ProjectInfoAssertions.AssertAnalysisResultExists(projectInfo, AnalysisType.FxCop.ToString(), fxCopLogFile);
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Creates a valid FxCop ruleset in the specified location.
        /// The contents of the ruleset are not important for the tests; the only
        /// requirement is that it should allow the FxCop targets to execute correctly.
        /// </summary>
        private void CreateValidFxCopRuleset(string rootInputFolder, string fileName)
        {
            string fullPath = Path.Combine(rootInputFolder, fileName);

            string content = @"
<?xml version='1.0' encoding='utf-8'?>
<RuleSet Name='Empty ruleset' Description='Valid empty ruleset' ToolsVersion='12.0'>
<!--
  <Include Path='minimumrecommendedrules.ruleset' Action='Default' />

  <Rules AnalyzerId='Microsoft.Analyzers.ManagedCodeAnalysis' RuleNamespace='Microsoft.Rules.Managed'>
    <Rule Id='CA1008' Action='Warning' />
  </Rules>
-->
</RuleSet>";

            File.WriteAllText(fullPath, content);
            this.TestContext.AddResultFile(fullPath);
        }

        #endregion

        #region Assertions methods

        private void AssertAllFxCopTargetsExecuted(BuildLogger logger)
        {
            logger.AssertTargetExecuted(TargetConstants.SonarOverrideFxCopSettingsTarget);
            logger.AssertTargetExecuted(TargetConstants.SonarSetFxCopResultsTarget);

            // If the sonar FxCop targets are executed then we expect the FxCop
            // target and task to be executed too
            AssertFxCopExecuted(logger);
        }

        private void AssertFxCopExecuted(BuildLogger logger)
        {
            logger.AssertTargetExecuted(TargetConstants.FxCopTarget);
            logger.AssertTaskExecuted(TargetConstants.FxCopTask);
        }

        private void AssertFxCopNotExecuted(BuildLogger logger)
        {
            // FxCop has a "RunCodeAnalysis" target and a "CodeAnalysis" task: the target executes the task.
            // We are interested in whether the task is executed or not as that is what will actually produce
            // the output file (it's possible that the target will be executed, but that it will decide
            // to skip the task because the required conditions are not met).
            logger.AssertTaskNotExecuted(TargetConstants.FxCopTask);
        }

        #endregion
    }
}
