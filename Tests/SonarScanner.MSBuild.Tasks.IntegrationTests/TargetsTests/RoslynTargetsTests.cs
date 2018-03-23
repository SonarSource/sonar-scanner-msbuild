/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class RoslynTargetsTests
    {
        private const string RoslynAnalysisResultsSettingName = "sonar.cs.roslyn.reportFilePath";
        private const string AnalyzerWorkDirectoryResultsSettingName = "sonar.cs.analyzer.projectOutPath";
        private const string ErrorLogFilePattern = "{0}.RoslynCA.json";

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
        {
            HackForVs2017Update3.Enable();
        }

        #region SetRoslynSettingsTarget tests

        [TestMethod]
        [Description("Checks the happy path i.e. settings exist in config, no reason to exclude the project")]
        public void Roslyn_Settings_ValidSetup()
        {
            // Arrange
            var logger = new BuildLogger();

            // Set the config directory so the targets know where to look for the analysis config file
            var confDir = TestUtils.CreateTestSpecificFolder(TestContext, "config");

            // Create a valid config file containing analyzer settings
            var expectedAssemblies = new string[] { "c:\\data\\config.analyzer1.dll", "c:\\config2.dll" };
            var analyzerAdditionalFiles = new string[] { "c:\\config.1.txt", "c:\\config.2.txt" };

            var config = new AnalysisConfig();

            var analyzerSettings = new AnalyzerSettings
            {
                Language = "cs",
                RuleSetFilePath = "d:\\my.ruleset",
                AnalyzerAssemblyPaths = expectedAssemblies.ToList(),
                AdditionalFilePaths = analyzerAdditionalFiles.ToList()
            };
            config.AnalyzersSettings = new List<AnalyzerSettings>
            {
                analyzerSettings
            };

            var configFilePath = Path.Combine(confDir, FileConstants.ConfigFileName);
            config.Save(configFilePath);

            // Create the project
            var properties = new WellKnownProjectProperties
            {
                SonarQubeConfigPath = confDir,
                ResolvedCodeAnalysisRuleset = "c:\\should.be.overridden.ruleset"
            };

            var projectRoot = CreateValidProjectSetup(properties);

            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "should.be.removed.analyzer1.dll");
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "c:\\should.be.removed.analyzer2.dll");

            var notRemovedAdditionalFiles = new string[] { "should.not.be.removed.additional1.txt", "should.not.be.removed.additional2.txt" };

            foreach (var notRemovedAdditionalFile in notRemovedAdditionalFiles)
            {
                projectRoot.AddItem(TargetProperties.AdditionalFilesItemType, notRemovedAdditionalFile);
            }

            // Act
            var result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            var projectSpecificConfFilePath = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.ProjectConfFilePath);
            var expectedRoslynAdditionalFiles = new string[] { projectSpecificConfFilePath }
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
            var logger = new BuildLogger();

            // Set the config directory so the targets know where to look for the analysis config file
            var confDir = TestUtils.CreateTestSpecificFolder(TestContext, "config");

            // Create a valid config file containing analyzer settings
            var expectedAssemblies = new string[] { "c:\\data\\config.analyzer1.dll", "c:\\config2.dll" };
            var analyzer1ExpectedFileName = "config.1.txt";
            var analyzerAdditionalFiles = new string[] { "c:\\" + analyzer1ExpectedFileName, "c:\\config.2.txt" };

            var config = new AnalysisConfig();

            var analyzerSettings = new AnalyzerSettings
            {
                Language = "cs",
                RuleSetFilePath = "d:\\my.ruleset",
                AnalyzerAssemblyPaths = expectedAssemblies.ToList(),
                AdditionalFilePaths = analyzerAdditionalFiles.ToList()
            };
            config.AnalyzersSettings = new List<AnalyzerSettings>
            {
                analyzerSettings
            };

            var configFilePath = Path.Combine(confDir, FileConstants.ConfigFileName);
            config.Save(configFilePath);

            // Create the project
            var properties = new WellKnownProjectProperties
            {
                SonarQubeConfigPath = confDir,
                ResolvedCodeAnalysisRuleset = "c:\\should.be.overridden.ruleset"
            };

            var projectRoot = CreateValidProjectSetup(properties);

            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "should.be.removed.analyzer1.dll");
            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "c:\\should.be.removed.analyzer2.dll");

            var notRemovedAdditionalFiles = new string[] { "should.not.be.removed.additional1.txt", "should.not.be.removed.additional2.txt" };

            foreach (var notRemovedAdditionalFile in notRemovedAdditionalFiles)
            {
                projectRoot.AddItem(TargetProperties.AdditionalFilesItemType, notRemovedAdditionalFile);
            }

            projectRoot.AddItem(TargetProperties.AdditionalFilesItemType, "DummyPath\\" + analyzer1ExpectedFileName.ToUpperInvariant());

            // Act
            var result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            var projectSpecificConfFilePath = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.ProjectConfFilePath);
            var expectedRoslynAdditionalFiles = new string[] { projectSpecificConfFilePath }
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
            var logger = new BuildLogger();

            // Set the config directory so the targets know where to look for the analysis config file
            var confDir = TestUtils.CreateTestSpecificFolder(TestContext, "config");

            // Create a valid config file that does not contain analyzer settings
            var config = new AnalysisConfig();
            var configFilePath = Path.Combine(confDir, FileConstants.ConfigFileName);
            config.Save(configFilePath);

            // Create the project
            var properties = new WellKnownProjectProperties
            {
                SonarQubeConfigPath = confDir,
                ResolvedCodeAnalysisRuleset = "c:\\should.be.overridden.ruleset"
            };

            var projectRoot = CreateValidProjectSetup(properties);

            projectRoot.AddItem(TargetProperties.AnalyzerItemType, "should.be.removed.analyzer1.dll");
            const string additionalFileName = "should.not.be.removed.additional1.txt";
            projectRoot.AddItem(TargetProperties.AdditionalFilesItemType, additionalFileName);

            // Act
            var result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            var projectSpecificConfFilePath = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.ProjectConfFilePath);

            var expectedRoslynAdditionalFiles = new string[] {
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
            var logger = new BuildLogger();

            var properties = new WellKnownProjectProperties
            {
                ErrorLog = "pre-existing.log",
                ResolvedCodeAnalysisRuleset = "pre-existing.ruleset",
                WarningsAsErrors = "CS101",
                TreatWarningsAsErrors = "true"
            };

            var projectRoot = CreateValidProjectSetup(properties);
            projectRoot.AddProperty(TargetProperties.SonarQubeTempPath, string.Empty); // needs to overwritten once the valid project has been created

            // Act
            var result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

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
            var logger = new BuildLogger();
            var properties = new WellKnownProjectProperties
            {
                [TargetProperties.ErrorLog] = "already.set.txt"
            };

            var projectRoot = CreateValidProjectSetup(properties);

            // Act
            var result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

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
            var logger = new BuildLogger();
            var properties = new WellKnownProjectProperties
            {
                ProjectTypeGuids = TargetConstants.MsTestProjectTypeGuid // mark the project as a test project
            };

            var projectRoot = CreateValidProjectSetup(properties);

            // Act
            var result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

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
            var logger = new BuildLogger();
            var properties = new WellKnownProjectProperties
            {
                SonarQubeExclude = "TRUE", // mark the project as excluded
                ResolvedCodeAnalysisRuleset = "Dummy value"
            };

            var projectRoot = CreateValidProjectSetup(properties);

            // Act
            var result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            logger.AssertTargetNotExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.OverrideRoslynAnalysisTarget);

            AssertCodeAnalysisIsDisabled(result, properties.ResolvedCodeAnalysisRuleset);
        }

        #endregion SetRoslynSettingsTarget tests

        #region AddAnalysisResults tests

        [TestMethod]
        [Description("Checks the target is not executed if the temp folder is not set")]
        public void Roslyn_SetResults_TempFolderIsNotSet()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");

            var properties = new WellKnownProjectProperties
            {
                SonarQubeTempPath = ""
            };

            var projectRoot = BuildUtilities.CreateValidProjectRoot(TestContext, rootInputFolder, properties);

            var logger = new BuildLogger();

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
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");

            var properties = new WellKnownProjectProperties
            {
                SonarQubeTempPath = rootInputFolder
            };

            var projectRoot = BuildUtilities.CreateValidProjectRoot(TestContext, rootInputFolder, properties);

            var logger = new BuildLogger();

            // Act
            var result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.SetRoslynResultsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.SetRoslynResultsTarget);
            BuildAssertions.AssertAnalysisSettingDoesNotExist(result, RoslynAnalysisResultsSettingName);
        }

        [TestMethod]
        [Description("Checks the analysis setting is set if the result file exists")]
        public void Roslyn_SetResults_ResultsFileExists()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");

            var resultsFile = TestUtils.CreateTextFile(rootInputFolder, "error.report.txt", "dummy report content");

            var properties = new WellKnownProjectProperties
            {
                SonarQubeTempPath = rootInputFolder
            };
            properties[TargetProperties.ErrorLog] = resultsFile;

            var projectRoot = BuildUtilities.CreateValidProjectRoot(TestContext, rootInputFolder, properties);

            var logger = new BuildLogger();

            // Act
            var result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.CreateProjectSpecificDirs, TargetConstants.SetRoslynResultsTarget);

            var projectSpecificOutDir = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.ProjectSpecificOutDir);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.CreateProjectSpecificDirs);
            logger.AssertTargetExecuted(TargetConstants.SetRoslynResultsTarget);
            BuildAssertions.AssertExpectedAnalysisSetting(result, RoslynAnalysisResultsSettingName, resultsFile);
            BuildAssertions.AssertExpectedAnalysisSetting(result, AnalyzerWorkDirectoryResultsSettingName, projectSpecificOutDir);
        }

        #endregion AddAnalysisResults tests

        #region Combined tests

        [TestMethod]
        [Description("Checks the targets are executed in the expected order")]
        public void Roslyn_TargetExecutionOrder()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var properties = new WellKnownProjectProperties
            {
                SonarQubeTempPath = rootInputFolder,
                SonarQubeOutputPath = rootInputFolder,
                SonarQubeConfigPath = rootOutputFolder
            };

            var projectRoot = BuildUtilities.CreateValidProjectRoot(TestContext, rootInputFolder, properties);

            // Add some settings we expect to be ignored
            AddAnalysisSetting("sonar.other.setting", "other value", projectRoot);
            projectRoot.Save();

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath, TargetConstants.DefaultBuildTarget);

            // Assert
            // Checks that should succeed irrespective of the MSBuild version
            result.AssertTargetSucceeded(TargetConstants.DefaultBuildTarget);
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);

            result.AssertExpectedTargetOrdering(
                TargetConstants.ResolveCodeAnalysisRuleSet,
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.OverrideRoslynAnalysisTarget,
                TargetConstants.SetRoslynAnalysisPropertiesTarget,
                TargetConstants.CoreCompile,
                TargetConstants.DefaultBuildTarget,
                TargetConstants.SetRoslynResultsTarget,
                TargetConstants.WriteProjectDataTarget);
        }

        #endregion Combined tests

        #region Private methods

        private static void AddAnalysisSetting(string name, string value, ProjectRootElement project)
        {
            var element = project.AddItem(BuildTaskConstants.SettingItemName, name);
            element.AddMetadata(BuildTaskConstants.SettingValueMetadataName, value);
        }

        #endregion Private methods

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
            var targetDir = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.TargetDir);
            var targetFileName = result.ProjectStateAfterBuild.GetPropertyValue(TargetProperties.TargetFileName);

            var expectedErrorLog = Path.Combine(targetDir, string.Format(CultureInfo.InvariantCulture, ErrorLogFilePattern, targetFileName));

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
            DumpLists(result, itemType, expectedValues);
            foreach (var expectedValue in expectedValues)
            {
                BuildAssertions.AssertSingleItemExists(result, itemType, expectedValue);
            }
            BuildAssertions.AssertExpectedItemGroupCount(result, itemType, expectedValues.Length);
        }

        private void DumpLists(BuildResult actualResult, string itemType, string[] expected)
        {
            TestContext.WriteLine("");
            TestContext.WriteLine("Dumping <" + itemType + "> list: expected");
            foreach (var item in expected)
            {
                TestContext.WriteLine("\t{0}", item);
            }
            TestContext.WriteLine("");

            TestContext.WriteLine("");
            TestContext.WriteLine("Dumping <" + itemType + "> list: actual");
            foreach (var item in actualResult.ProjectStateAfterBuild.GetItems(itemType))
            {
                TestContext.WriteLine("\t{0}", item.EvaluatedInclude);
            }
            TestContext.WriteLine("");
        }

        #endregion Checks

        #region Setup

        /// <summary>
        /// Creates a valid project with the necessary ruleset and assembly files on disc
        /// to successfully run the "OverrideRoslynCodeAnalysisProperties" target
        /// </summary>
        private ProjectRootElement CreateValidProjectSetup(WellKnownProjectProperties properties)
        {
            var sqTempFolder = TestUtils.EnsureTestSpecificFolder(TestContext);

            var projectProperties = properties ?? new WellKnownProjectProperties();
            var projectFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Project");

            projectProperties.SonarQubeTempPath = sqTempFolder;
            projectProperties[TargetProperties.Language] = "C#";

            var projectRoot = BuildUtilities.CreateValidProjectRoot(TestContext, projectFolder, projectProperties);

            var projectFilePath = Path.Combine(projectFolder, "valid.project.proj");
            projectRoot.Save(projectFilePath);
            return projectRoot;
        }

        #endregion Setup
    }
}
