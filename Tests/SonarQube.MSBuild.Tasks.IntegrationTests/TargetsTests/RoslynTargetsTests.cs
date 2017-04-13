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
 
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class RoslynTargetsTests
    {
        private const string RoslynAnalysisResultsSettingName = "sonar.cs.roslyn.reportFilePath";
        private const string AnalyzerWorkDirectoryResultsSettingName = "sonar.cs.analyzer.projectOutPath";
        private const string ErrorLogFilePattern = "{0}.RoslynCA.json";

        public TestContext TestContext { get; set; }

        #region SetRoslynSettingsTarget tests

        [TestMethod]
        [Description("Checks the happy path i.e. settings exist in config, no reason to exclude the project")]
        public void Roslyn_Settings_ValidSetup()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();

            // Set the config directory so the targets know where to look for the analysis config file
            string confDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "config");

            // Create a valid config file containing analyzer settings
            string[] expectedAssemblies = new string[] { "c:\\data\\config.analyzer1.dll", "c:\\config2.dll" };
            string[] analyzerAdditionalFiles = new string[] { "c:\\config.1.txt", "c:\\config.2.txt" };

            AnalysisConfig config = new AnalysisConfig();

            AnalyzerSettings analyzerSettings = new AnalyzerSettings();
            analyzerSettings.Language = "cs";
            analyzerSettings.RuleSetFilePath = "d:\\my.ruleset";
            analyzerSettings.AnalyzerAssemblyPaths = expectedAssemblies.ToList();
            analyzerSettings.AdditionalFilePaths = analyzerAdditionalFiles.ToList();
            config.AnalyzersSettings = new List<AnalyzerSettings>();
            config.AnalyzersSettings.Add(analyzerSettings);

            string configFilePath = Path.Combine(confDir, FileConstants.ConfigFileName);
            config.Save(configFilePath);

            // Create the project
            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeConfigPath = confDir;
            properties.ResolvedCodeAnalysisRuleset = "c:\\should.be.overridden.ruleset";

            ProjectRootElement projectRoot = CreateValidProjectSetup(properties);

            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "should.be.removed.analyzer1.dll");
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "c:\\should.be.removed.analyzer2.dll");

            string[] notRemovedAdditionalFiles = new string[] { "should.not.be.removed.additional1.txt", "should.not.be.removed.additional2.txt" };

            foreach (var notRemovedAdditionalFile in notRemovedAdditionalFiles)
            {
                projectRoot.AddItem(TargetProperties.AdditionalFilesItemType, notRemovedAdditionalFile);
            }

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            string projectSpecificConfFilePath = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.ProjectConfFilePath);
            string[] expectedRoslynAdditionalFiles = new string[] { projectSpecificConfFilePath }
                .Concat(analyzerAdditionalFiles)
                .Concat(notRemovedAdditionalFiles)
                .ToArray();

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            // Check the error log and ruleset properties are set
            AssertErrorLogIsSetBySonarQubeTargets(result);
            AssertExpectedResolvedRuleset(result, "d:\\my.ruleset");
            AssertExpectedItemValuesExists(result, TargetProperties.AdditionalFilesItemType, expectedRoslynAdditionalFiles);
            AssertExpectedItemValuesExists(result, TargetProperties.AnalyzerItemType, expectedAssemblies);

            BuildAssertions.AssertWarningsAreNotTreatedAsErrorsNorIgnored(result);
        }

        [TestMethod]
        [Description("Checks the happy path i.e. settings exist in config, no reason to exclude the project")]
        public void Roslyn_Settings_ValidSetup_Override_AdditionalFiles()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();

            // Set the config directory so the targets know where to look for the analysis config file
            string confDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "config");

            // Create a valid config file containing analyzer settings
            string[] expectedAssemblies = new string[] { "c:\\data\\config.analyzer1.dll", "c:\\config2.dll" };
            string analyzer1ExpectedFileName = "config.1.txt";
            string[] analyzerAdditionalFiles = new string[] { "c:\\" + analyzer1ExpectedFileName, "c:\\config.2.txt" };

            AnalysisConfig config = new AnalysisConfig();

            AnalyzerSettings analyzerSettings = new AnalyzerSettings();
            analyzerSettings.Language = "cs";
            analyzerSettings.RuleSetFilePath = "d:\\my.ruleset";
            analyzerSettings.AnalyzerAssemblyPaths = expectedAssemblies.ToList();
            analyzerSettings.AdditionalFilePaths = analyzerAdditionalFiles.ToList();
            config.AnalyzersSettings = new List<AnalyzerSettings>();
            config.AnalyzersSettings.Add(analyzerSettings);

            string configFilePath = Path.Combine(confDir, FileConstants.ConfigFileName);
            config.Save(configFilePath);

            // Create the project
            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeConfigPath = confDir;
            properties.ResolvedCodeAnalysisRuleset = "c:\\should.be.overridden.ruleset";

            ProjectRootElement projectRoot = CreateValidProjectSetup(properties);

            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "should.be.removed.analyzer1.dll");
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "c:\\should.be.removed.analyzer2.dll");

            string[] notRemovedAdditionalFiles = new string[] { "should.not.be.removed.additional1.txt", "should.not.be.removed.additional2.txt" };

            foreach (var notRemovedAdditionalFile in notRemovedAdditionalFiles)
            {
                projectRoot.AddItem(TargetProperties.AdditionalFilesItemType, notRemovedAdditionalFile);
            }

            projectRoot.AddItem(TargetProperties.AdditionalFilesItemType, "DummyPath\\" + analyzer1ExpectedFileName.ToUpperInvariant());

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            string projectSpecificConfFilePath = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.ProjectConfFilePath);
            string[] expectedRoslynAdditionalFiles = new string[] { projectSpecificConfFilePath }
                .Concat(analyzerAdditionalFiles)
                .Concat(notRemovedAdditionalFiles)
                .ToArray();

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            // Check the error log and ruleset properties are set
            AssertErrorLogIsSetBySonarQubeTargets(result);
            AssertExpectedResolvedRuleset(result, "d:\\my.ruleset");
            AssertExpectedItemValuesExists(result, TargetProperties.AdditionalFilesItemType, expectedRoslynAdditionalFiles);
            AssertExpectedItemValuesExists(result, TargetProperties.AnalyzerItemType, expectedAssemblies);

            BuildAssertions.AssertWarningsAreNotTreatedAsErrorsNorIgnored(result);
        }

        [TestMethod]
        [Description("Checks that a config file with no analyzer settings does not cause an issue")]
        public void Roslyn_Settings_SettingsMissing_NoError()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();

            // Set the config directory so the targets know where to look for the analysis config file
            string confDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "config");

            // Create a valid config file that does not contain analyzer settings
            AnalysisConfig config = new AnalysisConfig();
            string configFilePath = Path.Combine(confDir, FileConstants.ConfigFileName);
            config.Save(configFilePath);

            // Create the project
            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeConfigPath = confDir;
            properties.ResolvedCodeAnalysisRuleset = "c:\\should.be.overridden.ruleset";

            ProjectRootElement projectRoot = CreateValidProjectSetup(properties);

            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "should.be.removed.analyzer1.dll");
            const string additionalFileName = "should.not.be.removed.additional1.txt";
            projectRoot.AddItem(TargetProperties.AdditionalFilesItemType, additionalFileName);

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            string projectSpecificConfFilePath = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.ProjectConfFilePath);

            string[] expectedRoslynAdditionalFiles = new string[] {
                projectSpecificConfFilePath,
                additionalFileName /* additional files are not removed any longer */
            };

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            // Check the error log and ruleset properties are set
            AssertErrorLogIsSetBySonarQubeTargets(result);
            AssertExpectedResolvedRuleset(result, string.Empty);
            BuildAssertions.AssertExpectedItemGroupCount(result, TargetProperties.AnalyzerItemType, 0);
            AssertExpectedItemValuesExists(result, TargetProperties.AdditionalFilesItemType, expectedRoslynAdditionalFiles);
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
            properties.WarningsAsErrors = "CS101";
            properties.TreatWarningsAsErrors = "true";

            ProjectRootElement projectRoot = CreateValidProjectSetup(properties);
            projectRoot.AddProperty(TargetProperties.SonarQubeTempPath, string.Empty); // needs to overwritten once the valid project has been created

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetNotExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetNotExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);

            // Existing properties should not be changed
            AssertExpectedErrorLog(result, "pre-existing.log");
            AssertExpectedResolvedRuleset(result, "pre-existing.ruleset");
            BuildAssertions.AssertExpectedItemGroupCount(result, TargetProperties.AnalyzerItemType, 0);
            BuildAssertions.AssertExpectedItemGroupCount(result, TargetProperties.AdditionalFilesItemType, 0);

            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.TreatWarningsAsErrors, "true");
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.WarningsAsErrors, "CS101");
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

            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.ErrorLog, "already.set.txt");
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

            AssertCodeAnalysisIsDisabled(result, null);
        }

        [TestMethod]
        [Description("Checks the code analysis properties are cleared for excludedprojects")]
        public void Roslyn_Settings_NotRunForExcludedProject()
        {
            // Arrange
            BuildLogger logger = new BuildLogger();
            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeExclude = "TRUE"; // mark the project as excluded
            properties.ResolvedCodeAnalysisRuleset = "Dummy value";

            ProjectRootElement projectRoot = CreateValidProjectSetup(properties);

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetNotExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            AssertCodeAnalysisIsDisabled(result, properties.ResolvedCodeAnalysisRuleset);
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
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.CreateProjectSpecificDirs, TargetConstants.SetRoslynResultsTarget);

            string projectSpecificOutDir = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.ProjectSpecificOutDir);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.CreateProjectSpecificDirs);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynResultsTarget);
            BuildAssertions.AssertExpectedAnalysisSetting(result, RoslynAnalysisResultsSettingName, resultsFile);
            BuildAssertions.AssertExpectedAnalysisSetting(result, AnalyzerWorkDirectoryResultsSettingName, projectSpecificOutDir);
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

        private static void AddAnalysisSetting(string name, string value, ProjectRootElement project)
        {
            ProjectItemElement element = project.AddItem(BuildTaskConstants.SettingItemName, name);
            element.AddMetadata(BuildTaskConstants.SettingValueMetadataName, value);
        }

        #endregion

        #region Checks

        private static void AssertCodeAnalysisIsDisabled(BuildResult result, string expectedResolvedCodeAnalysisRuleset)
        {
            // Check the ruleset and error log are not set
            AssertExpectedErrorLog(result, string.Empty);
            AssertExpectedResolvedRuleset(result, expectedResolvedCodeAnalysisRuleset);
        }

        /// <summary>
        /// Checks the error log property has been set to the value supplied in the targets file
        /// </summary>
        private static void AssertErrorLogIsSetBySonarQubeTargets(BuildResult result)
        {
            string targetDir = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.TargetDir);
            string targetFileName = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.TargetFileName);

            string expectedErrorLog = Path.Combine(targetDir, String.Format(CultureInfo.InvariantCulture, ErrorLogFilePattern, targetFileName));


            AssertExpectedErrorLog(result, expectedErrorLog);
        }

        private static void AssertExpectedErrorLog(BuildResult result, string expectedErrorLog)
        {
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.ErrorLog, expectedErrorLog);
        }

        private static void AssertExpectedResolvedRuleset(BuildResult result, string expectedResolvedRuleset)
        {
            BuildAssertions.AssertExpectedPropertyValue(result.ProjectStateAfterBuild, TargetProperties.ResolvedCodeAnalysisRuleset, expectedResolvedRuleset);
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
            string sqTempFolder = TestUtils.EnsureTestSpecificFolder(this.TestContext);

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