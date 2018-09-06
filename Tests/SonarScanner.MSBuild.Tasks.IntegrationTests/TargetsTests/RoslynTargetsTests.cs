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
using FluentAssertions;
using Microsoft.Build.Construction;
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

        #region SetRoslynSettingsTarget tests

        [TestMethod]
        [Description("Checks the happy path i.e. settings exist in config, no reason to exclude the project")]
        public void Roslyn_Settings_ValidSetup()
        {
            // Arrange

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

            projectRoot.Save(); // re-save the modified project

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath,
                TargetConstants.OverrideRoslynAnalysisTarget);

            var projectSpecificConfFilePath = result.GetCapturedPropertyValue(TargetProperties.ProjectConfFilePath);
            var expectedRoslynAdditionalFiles = new string[] { projectSpecificConfFilePath }
                .Concat(analyzerAdditionalFiles)
                .Concat(notRemovedAdditionalFiles)
                .ToArray();

            // Assert
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            result.BuildSucceeded.Should().BeTrue();

            // Check the error log and ruleset properties are set
            AssertErrorLogIsSetBySonarQubeTargets(result);
            AssertExpectedResolvedRuleset(result, "d:\\my.ruleset");
            AssertExpectedItemValuesExists(result, TargetProperties.AdditionalFilesItemType, expectedRoslynAdditionalFiles);
            AssertExpectedItemValuesExists(result, TargetProperties.AnalyzerItemType, expectedAssemblies);

            AssertWarningsAreNotTreatedAsErrorsNorIgnored(result);
        }
        
        [TestMethod]
        [Description("Checks any existing analyzers are overridden for projects using SonarQube pre-7.5")]
        public void Roslyn_Settings_ValidSetup_LegacyServer_Override_Analyzers()
        {
            // Arrange

            // Create a valid config containing analyzer settings
            var config = new AnalysisConfig
            {
                SonarQubeVersion = "6.7", // legacy version
                AnalyzersSettings = new List<AnalyzerSettings>
                {
                    new AnalyzerSettings
                    {
                        Language = "cs",
                        RuleSetFilePath = "d:\\my.ruleset",
                        AnalyzerAssemblyPaths = new List<string> { "c:\\data\\new.analyzer1.dll", "c:\\new.analyzer2.dll" },
                        AdditionalFilePaths = new List<string> { "c:\\config.1.txt", "c:\\config.2.txt" }
                    }
                }
            };

            var testSpecificProjectXml = @"
  <PropertyGroup>
    <ResolvedCodeAnalysisRuleSet>c:\should.be.overridden.ruleset</ResolvedCodeAnalysisRuleSet>
    <Language>C#</Language>
  </PropertyGroup>

  <ItemGroup>
    <!-- all analyzers specified in the project file should be removed -->
    <Analyzer Include='c:\should.be.removed.analyzer2.dll' />
    <Analyzer Include='should.be.removed.analyzer1.dll' />
  </ItemGroup>
  <ItemGroup>
    <!-- These additional files don't match ones in the config and should be preserved -->
    <AdditionalFiles Include='should.not.be.removed.additional1.txt' />
    <AdditionalFiles Include='should.not.be.removed.additional2.txt' />

    <!-- This additional file matches one in the config and should be replaced -->
    <AdditionalFiles Include='should.be.removed\CONFIG.1.TXT' />

  </ItemGroup>
";
            var projectFilePath = CreateProjectFile(config, testSpecificProjectXml);
            
            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            result.BuildSucceeded.Should().BeTrue();

            // Check the error log and ruleset properties are set
            AssertErrorLogIsSetBySonarQubeTargets(result);
            AssertExpectedResolvedRuleset(result, "d:\\my.ruleset");

            AssertExpectedAdditionalFiles(result,
                result.GetCapturedPropertyValue(TargetProperties.ProjectConfFilePath),
                "should.not.be.removed.additional1.txt",
                "should.not.be.removed.additional2.txt",
                "c:\\config.1.txt",
                "c:\\config.2.txt");

            AssertExpectedAnalyzers(result,
                "c:\\data\\new.analyzer1.dll",
                "c:\\new.analyzer2.dll");

            AssertWarningsAreNotTreatedAsErrorsNorIgnored(result);
        }

        [TestMethod]
        [Description("Checks that a config file with no analyzer settings does not cause an issue")]
        public void Roslyn_Settings_SettingsMissing_NoError()
        {
            // Arrange

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

            projectRoot.Save(); // re-save the modified project

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath, TargetConstants.OverrideRoslynAnalysisTarget);

            var projectSpecificConfFilePath = result.GetCapturedPropertyValue(TargetProperties.ProjectConfFilePath);

            var expectedRoslynAdditionalFiles = new string[] {
                projectSpecificConfFilePath,
                additionalFileName /* additional files are not removed any longer */
            };

            // Assert
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            result.BuildSucceeded.Should().BeTrue();

            // Check the error log and ruleset properties are set
            AssertErrorLogIsSetBySonarQubeTargets(result);
            AssertExpectedResolvedRuleset(result, string.Empty);
            result.AssertExpectedItemGroupCount(TargetProperties.AnalyzerItemType, 0);
            AssertExpectedItemValuesExists(result, TargetProperties.AdditionalFilesItemType, expectedRoslynAdditionalFiles);
        }

        [TestMethod]
        [Description("Checks the target is not executed if the temp folder is not set")]
        public void Roslyn_Settings_TempFolderIsNotSet()
        {
            // Arrange

            var properties = new WellKnownProjectProperties
            {
                ErrorLog = "pre-existing.log",
                ResolvedCodeAnalysisRuleset = "pre-existing.ruleset",
                WarningsAsErrors = "CS101",
                TreatWarningsAsErrors = "true"
            };

            var projectRoot = CreateValidProjectSetup(properties);
            projectRoot.AddProperty(TargetProperties.SonarQubeTempPath, string.Empty); // needs to overwritten once the valid project has been created

            projectRoot.Save(); // re-save the modified project

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            result.AssertTargetNotExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            result.AssertTargetNotExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);

            // Existing properties should not be changed
            AssertExpectedErrorLog(result, "pre-existing.log");
            AssertExpectedResolvedRuleset(result, "pre-existing.ruleset");
            result.AssertExpectedItemGroupCount(TargetProperties.AnalyzerItemType, 0);
            result.AssertExpectedItemGroupCount(TargetProperties.AdditionalFilesItemType, 0);

            result.AssertExpectedCapturedPropertyValue(TargetProperties.TreatWarningsAsErrors, "true");
            result.AssertExpectedCapturedPropertyValue(TargetProperties.WarningsAsErrors, "CS101");
        }

        [TestMethod]
        [Description("Checks an existing errorLog value is used if set")]
        public void Roslyn_Settings_ErrorLogAlreadySet()
        {
            // Arrange
            var properties = new WellKnownProjectProperties
            {
                [TargetProperties.ErrorLog] = "already.set.txt"
            };

            var projectRoot = CreateValidProjectSetup(properties);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            result.BuildSucceeded.Should().BeTrue();

            result.AssertExpectedCapturedPropertyValue(TargetProperties.ErrorLog, "already.set.txt");
        }

        [TestMethod]
        [Description("Checks the code analysis properties are cleared for test projects")]
        public void Roslyn_Settings_NotRunForTestProject()
        {
            // Arrange
            var properties = new WellKnownProjectProperties
            {
                ProjectTypeGuids = TargetConstants.MsTestProjectTypeGuid // mark the project as a test project
            };

            var projectRoot = CreateValidProjectSetup(properties);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            result.AssertTargetNotExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            result.BuildSucceeded.Should().BeTrue();

            AssertCodeAnalysisIsDisabled(result, string.Empty);
        }

        [TestMethod]
        [Description("Checks the code analysis properties are cleared for excludedprojects")]
        public void Roslyn_Settings_NotRunForExcludedProject()
        {
            // Arrange
            var properties = new WellKnownProjectProperties
            {
                SonarQubeExclude = "TRUE", // mark the project as excluded
                ResolvedCodeAnalysisRuleset = "Dummy value"
            };

            var projectRoot = CreateValidProjectSetup(properties);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            result.AssertTargetNotExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            result.BuildSucceeded.Should().BeTrue();

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

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath,
                TargetConstants.SetRoslynResultsTarget);

            // Assert
            result.AssertTargetNotExecuted(TargetConstants.SetRoslynResultsTarget);
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

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath, TargetConstants.SetRoslynResultsTarget);

            // Assert
            result.AssertTargetExecuted(TargetConstants.SetRoslynResultsTarget);
            AssertAnalysisSettingDoesNotExist(result, RoslynAnalysisResultsSettingName);
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
            AddCaptureTargetsImport(rootInputFolder, projectRoot);
            projectRoot.Save();

            // Act
            var result = BuildRunner.BuildTargets(TestContext,
                projectRoot.FullPath,
                TargetConstants.CreateProjectSpecificDirs, TargetConstants.SetRoslynResultsTarget);

            var projectSpecificOutDir = result.GetCapturedPropertyValue(TargetProperties.ProjectSpecificOutDir);

            // Assert
            result.AssertTargetExecuted(TargetConstants.CreateProjectSpecificDirs);
            result.AssertTargetExecuted(TargetConstants.SetRoslynResultsTarget);
            AssertExpectedAnalysisSetting(result, RoslynAnalysisResultsSettingName, resultsFile);
            AssertExpectedAnalysisSetting(result, AnalyzerWorkDirectoryResultsSettingName, projectSpecificOutDir);
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

        private static void AssertCodeAnalysisIsDisabled(BuildLog result, string expectedResolvedCodeAnalysisRuleset)
        {
            // Check the ruleset and error log are not set
            AssertExpectedErrorLog(result, string.Empty);
            AssertExpectedResolvedRuleset(result, expectedResolvedCodeAnalysisRuleset);
        }

        /// <summary>
        /// Checks the error log property has been set to the value supplied in the targets file
        /// </summary>
        private static void AssertErrorLogIsSetBySonarQubeTargets(BuildLog result)
        {
            var targetDir = result.GetCapturedPropertyValue(TargetProperties.TargetDir);
            var targetFileName = result.GetCapturedPropertyValue(TargetProperties.TargetFileName);

            var expectedErrorLog = Path.Combine(targetDir, string.Format(CultureInfo.InvariantCulture, ErrorLogFilePattern, targetFileName));

            AssertExpectedErrorLog(result, expectedErrorLog);
        }

        private static void AssertExpectedErrorLog(BuildLog result, string expectedErrorLog)
        {
            result.AssertExpectedCapturedPropertyValue(TargetProperties.ErrorLog, expectedErrorLog);
        }

        private static void AssertExpectedResolvedRuleset(BuildLog result, string expectedResolvedRuleset)
        {
            result.AssertExpectedCapturedPropertyValue(TargetProperties.ResolvedCodeAnalysisRuleset, expectedResolvedRuleset);
        }

        private void AssertExpectedItemValuesExists(BuildLog result, string itemType, params string[] expectedValues)
        {
            DumpLists(result, itemType, expectedValues);
            foreach (var expectedValue in expectedValues)
            {
                result.AssertSingleItemExists(itemType, expectedValue);
            }
            result.AssertExpectedItemGroupCount(itemType, expectedValues.Length);
        }

        private void AssertExpectedAnalyzers(BuildLog result, params string[] expected) =>
            AssertExpectedItemValuesExists(result, TargetProperties.AnalyzerItemType, expected);

        private void AssertExpectedAdditionalFiles(BuildLog result, params string[] expected) =>
            AssertExpectedItemValuesExists(result, TargetProperties.AdditionalFilesItemType, expected);

        private void DumpLists(BuildLog actualResult, string itemType, string[] expected)
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
            foreach (var item in actualResult.GetCapturedItemValues(itemType))
            {
                TestContext.WriteLine("\t{0}", item);
            }
            TestContext.WriteLine("");
        }

        /// <summary>
        /// Checks that no analysis warnings will be treated as errors nor will they be ignored
        /// </summary>
        private static void AssertWarningsAreNotTreatedAsErrorsNorIgnored(BuildLog actualResult)
        {
            actualResult.AssertExpectedCapturedPropertyValue(TargetProperties.TreatWarningsAsErrors, "false");
            actualResult.AssertExpectedCapturedPropertyValue(TargetProperties.WarningsAsErrors, "");
            actualResult.AssertExpectedCapturedPropertyValue(TargetProperties.WarningLevel, "4");
        }

        /// <summary>
        /// Checks that a SonarQubeSetting does not exist
        /// </summary>
        private static void AssertAnalysisSettingDoesNotExist(BuildLog actualResult, string settingName)
        {
            var matches = actualResult.GetCapturedItemValues(BuildTaskConstants.SettingItemName);

            matches.Should().BeEmpty("Not expected SonarQubeSetting with include value of '{0}' to exist. Actual occurrences: {1}", settingName, matches.Count());
        }

        /// <summary>
        /// Checks whether there is a single "SonarQubeSetting" item with the expected name and setting value
        /// </summary>
        private static void AssertExpectedAnalysisSetting(BuildLog actualResult, string settingName, string expectedValue)
        {
            /* The equivalent XML would look like this:
            <ItemGroup>
              <SonarQubeSetting Include="settingName">
                <Value>expectedValue</Value
              </SonarQubeSetting>
            </ItemGroup>
            */

            var settings = actualResult.GetCapturedItemValues(BuildTaskConstants.SettingItemName);
            settings.Any().Should().BeTrue();

            var matches = settings.Where(v => v.Value.Equals(settingName, System.StringComparison.Ordinal)).ToList();
            matches.Should().HaveCount(1, $"Only one and only expecting one SonarQubeSetting with include value of '{0}' to exist. Count: {matches.Count}", settingName);

            var item = matches[0];
            var value = item.Metadata.SingleOrDefault(v => v.Name.Equals(BuildTaskConstants.SettingValueMetadataName));

            value.Should().NotBeNull();
            value.Value.Should().Be(expectedValue, "SonarQubeSetting with include value '{0}' does not have the expected value", settingName);
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

            AddCaptureTargetsImport(projectFolder, projectRoot);

            var projectFilePath = Path.Combine(projectFolder, "valid.project.proj");
            projectRoot.Save(projectFilePath);
            return projectRoot;
        }

        /// <summary>
        /// Creates a valid project with the necessary ruleset and assembly files on disc
        /// to successfully run the "OverrideRoslynCodeAnalysisProperties" target
        /// </summary>
        private string CreateProjectFile(AnalysisConfig analysisConfig, string testSpecificProjectXml)
        {
            var projectDirectory = TestUtils.EnsureTestSpecificFolder(TestContext);

            CreateCaptureDataTargetsFile(projectDirectory);

            var configFilePath = Path.Combine(projectDirectory, FileConstants.ConfigFileName);
            analysisConfig.Save(configFilePath);

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            File.Exists(sqTargetFile).Should().BeTrue("Test error: the SonarQube analysis targets file could not be found. Full path: {0}", sqTargetFile);
            TestContext.AddResultFile(sqTargetFile);


            var template = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='15.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>

  <!-- Boilerplate -->
  <PropertyGroup>
    <ImportByWildcardBeforeMicrosoftCommonTargets>false</ImportByWildcardBeforeMicrosoftCommonTargets>
    <ImportByWildcardAfterMicrosoftCommonTargets>false</ImportByWildcardAfterMicrosoftCommonTargets>
    <ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets>false</ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets>
    <ImportUserLocationsByWildcardAfterMicrosoftCommonTargets>false</ImportUserLocationsByWildcardAfterMicrosoftCommonTargets>
    <OutputPath>bin\</OutputPath>
    <OutputType>library</OutputType>
    <ProjectGuid>ffdb93c0-2880-44c7-89a6-bbd4ddab034a</ProjectGuid>
    <CodePage>65001</CodePage>
  </PropertyGroup>

  <!-- Standard values that need to be set for each/most tests -->
  <PropertyGroup>

    <SonarQubeBuildTasksAssemblyFile>SONARSCANNER_MSBUILD_TASKS_DLL</SonarQubeBuildTasksAssemblyFile>
    <SonarQubeConfigPath>PROJECT_DIRECTORY_PATH</SonarQubeConfigPath>
    <SonarQubeTempPath>PROJECT_DIRECTORY_PATH</SonarQubeTempPath>

  </PropertyGroup>

  <!-- Test-specific data -->
  TEST_SPECIFIC_XML

  <!-- Standard boilerplate closing imports -->
  <Import Project='$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), SonarQube.Integration.targets))SonarQube.Integration.targets' />
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
  <Import Project='$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), Capture.targets))Capture.targets' />
</Project>
";
            var projectData = template.Replace("PROJECT_DIRECTORY_PATH", projectDirectory)
                .Replace("SONARSCANNER_MSBUILD_TASKS_DLL", typeof(WriteProjectInfoFile).Assembly.Location)
                .Replace("TEST_SPECIFIC_XML", testSpecificProjectXml ?? "<!-- none -->");

            var projectFilePath = Path.Combine(projectDirectory, TestContext.TestName + ".proj");
            File.WriteAllText(projectFilePath, projectData);
            TestContext.AddResultFile(projectFilePath);

            return projectFilePath;
        }

        private void AddCaptureTargetsImport(string projectFolder, ProjectRootElement projectRoot)
        {
            // Add an additional import that will dump data we are interested in to the build log
            var captureTargetsFilePath = CreateCaptureDataTargetsFile(projectFolder);
            projectRoot.AddImport(captureTargetsFilePath);

            var projectFilePath = Path.Combine(projectFolder, "valid.project.proj");
            projectRoot.Save(projectFilePath);
        }

        private string CreateCaptureDataTargetsFile(string directory)
        {
            // Most of the tests above want to check the value of build property
            // or item group after a target has been executed. However, this
            // information is not available through the buildlogger interface.
            // So, we'll add a special target that writes the properties/items
            // we are interested in to the message log.
            // The SimpleXmlLogger has special handling to extract the data
            // from the message and add it to the BuildLog.

            // Make sure that the target is run after all of the targets
            // used by the any of the tests.
            string afterTargets = string.Join(";",
                TargetConstants.SetRoslynResultsTarget,
                TargetConstants.OverrideRoslynAnalysisTarget,
                TargetConstants.SetRoslynAnalysisPropertiesTarget
                );

            string xml = $@"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='15.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  
  <Target Name='CaptureValues' AfterTargets='{afterTargets}'>
    <Message Importance='high' Text='CAPTURE::PROPERTY::TargetDir::$(TargetDir)' />
    <Message Importance='high' Text='CAPTURE::PROPERTY::TargetFileName::$(TargetFileName)' />
    <Message Importance='high' Text='CAPTURE::PROPERTY::ErrorLog::$(ErrorLog)' />
    <Message Importance='high' Text='CAPTURE::PROPERTY::ProjectConfFilePath::$(ProjectConfFilePath)' />
    <Message Importance='high' Text='CAPTURE::PROPERTY::ResolvedCodeAnalysisRuleSet::$(ResolvedCodeAnalysisRuleSet)' />
    <Message Importance='high' Text='CAPTURE::PROPERTY::TreatWarningsAsErrors::$(TreatWarningsAsErrors)' />
    <Message Importance='high' Text='CAPTURE::PROPERTY::WarningsAsErrors::$(WarningsAsErrors)' />
    <Message Importance='high' Text='CAPTURE::PROPERTY::WarningLevel::$(WarningLevel)' />
    <Message Importance='high' Text='CAPTURE::PROPERTY::ProjectSpecificOutDir::$(ProjectSpecificOutDir)' />

    <!-- Item group values will be written out one per line -->
    <Message Importance='high' Text='CAPTURE::ITEM::AdditionalFiles::%(AdditionalFiles.Identity)' Condition="" @(AdditionalFiles) != '' ""/>
    <Message Importance='high' Text='CAPTURE::ITEM::Analyzer::%(Analyzer.Identity)'  Condition="" @(Analyzer) != '' "" />

    <!-- For the SonarQubeSetting items, we also want to capture the Value metadata item -->
    <Message Importance='high' Text='CAPTURE::ITEM::SonarQubeSetting::%(SonarQubeSetting.Identity)::Value::%(SonarQubeSetting.Value)'  Condition="" @(SonarQubeSetting) != '' "" />
  </Target>
</Project>";

            // We're using :: as a separator here: replace it with whatever
            // whatever the logger is using as a separator
            xml = xml.Replace("::", SimpleXmlLogger.CapturedDataSeparator);

            var filePath = Path.Combine(directory, "Capture.targets");
            File.WriteAllText(filePath, xml);
            TestContext.AddResultFile(filePath);
            return filePath;
        }

        #endregion Setup
    }
}
