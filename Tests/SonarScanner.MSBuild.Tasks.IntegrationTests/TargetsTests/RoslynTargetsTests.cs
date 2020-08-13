/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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
        public void Roslyn_Settings_ValidSetup_ForProductProject_CS()
        {
            // Arrange and Act
            var result = Execute_Roslyn_Settings_ValidSetup(false, "C#");

            // Assert
            AssertExpectedResolvedRuleset(result, "d:\\my.ruleset.cs");

            // Expecting all analyzers from the config file, but none from the project file
            AssertExpectedAnalyzers(result,
                "c:\\1\\SonarAnalyzer.CSharp.dll",
                "c:\\1\\SonarAnalyzer.dll",
                "c:\\1\\Google.Protobuf.dll",
                "c:\\2\\SonarAnalyzer.Security.dll",
                "c:\\2\\Google.Protobuf.dll");

            // Expecting additional files from both config and project file
            var projectSpecificConfFilePath = result.GetCapturedPropertyValue(TargetProperties.ProjectConfFilePath);
            AssertExpectedAdditionalFiles(result,
                projectSpecificConfFilePath,
                "c:\\config.1.txt", "c:\\config.2.txt",
                "project.additional.file.1.txt", "x:\\aaa\\project.additional.file.2.txt");

        }

        [TestMethod]
        public void Roslyn_Settings_ValidSetup_ForProductProject_VB()
        {
            // Arrange and Act
            var result = Execute_Roslyn_Settings_ValidSetup(false, "VB");

            // Assert
            AssertExpectedResolvedRuleset(result, "d:\\my.ruleset.vb");

            // Expecting all analyzers from the config file, but none from the project file
            AssertExpectedAnalyzers(result,
                "c:\\0\\SonarAnalyzer.VisualBasic.dll", "c:\\0\\Google.Protobuf.dll",
                "c:\\config.analyzer2.vb.dll");

            // Expecting additional files from both config and project file
            var projectSpecificConfFilePath = result.GetCapturedPropertyValue(TargetProperties.ProjectConfFilePath);
            AssertExpectedAdditionalFiles(result,
                projectSpecificConfFilePath,
                "c:\\config.1.txt", "c:\\config.2.txt",
                "project.additional.file.1.txt", "x:\\aaa\\project.additional.file.2.txt");
        }

        [TestMethod]
        public void Roslyn_Settings_ValidSetup_ForTestProject_CS()
        {
            // Arrange and Act
            var result = Execute_Roslyn_Settings_ValidSetup(true, "C#");

            // Assert
            AssertExpectedResolvedRuleset(result, "d:\\my.ruleset.cs.test");

            // Expecting only the SonarC# analyzer
            AssertExpectedAnalyzers(result,
                "c:\\1\\SonarAnalyzer.CSharp.dll",
                "c:\\1\\SonarAnalyzer.dll",
                "c:\\1\\Google.Protobuf.dll");

            // Expecting only the additional files from the config file
            var projectSpecificConfFilePath = result.GetCapturedPropertyValue(TargetProperties.ProjectConfFilePath);
            AssertExpectedAdditionalFiles(result,
                projectSpecificConfFilePath,
                "c:\\config.1.txt", "c:\\config.2.txt");
        }

        [TestMethod]
        public void Roslyn_Settings_ValidSetup_ForTestProject_VB()
        {
            // Arrange and Act
            var result = Execute_Roslyn_Settings_ValidSetup(true, "VB");

            // Assert
            AssertExpectedResolvedRuleset(result, "d:\\my.ruleset.test.vb");

            // Expecting only the SonarVB analyzer
            AssertExpectedAnalyzers(result, "c:\\0\\SonarAnalyzer.VisualBasic.dll", "c:\\0\\Google.Protobuf.dll");

            // Expecting only the additional files from the config file
            var projectSpecificConfFilePath = result.GetCapturedPropertyValue(TargetProperties.ProjectConfFilePath);
            AssertExpectedAdditionalFiles(result,
                projectSpecificConfFilePath,
                "c:\\config.1.txt", "c:\\config.2.txt");
        }

        public BuildLog Execute_Roslyn_Settings_ValidSetup(bool isTestProject, string msBuildLanguage)
        {
            // Arrange

            // Set the config directory so the targets know where to look for the analysis config file
            var confDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "config");

            // Create a valid config file containing analyzer settings for both VB and C#
            var config = new AnalysisConfig
            {
                SonarQubeVersion = "7.3", // legacy behaviour i.e. overwrite existing analyzer settings
                AnalyzersSettings = new List<AnalyzerSettings>
                    {
                        // C#
                        new AnalyzerSettings
                        {
                            Language = "cs",
                            RuleSetFilePath = "d:\\my.ruleset.cs",
                            TestProjectRuleSetFilePath = "d:\\my.ruleset.cs.test",
                            AnalyzerPlugins = new List<AnalyzerPlugin>
                            {
                                new AnalyzerPlugin("csharp", "v1", "resName", new string [] { "c:\\1\\SonarAnalyzer.CSharp.dll", "c:\\1\\SonarAnalyzer.dll", "c:\\1\\Google.Protobuf.dll" }),
                                new AnalyzerPlugin("securitycsharpfrontend", "v1", "resName", new string [] { "c:\\2\\SonarAnalyzer.Security.dll", "c:\\2\\Google.Protobuf.dll" })
                            },
                            AdditionalFilePaths = new List<string> { "c:\\config.1.txt", "c:\\config.2.txt" }
                        },

                        // VB
                        new AnalyzerSettings
                        {
                            Language = "vbnet",
                            RuleSetFilePath = "d:\\my.ruleset.vb",
                            TestProjectRuleSetFilePath = "d:\\my.ruleset.test.vb",
                            AnalyzerPlugins = new List<AnalyzerPlugin>
                            {
                                new AnalyzerPlugin("vbnet", "v1", "resName", new string [] { "c:\\0\\SonarAnalyzer.VisualBasic.dll", "c:\\0\\Google.Protobuf.dll" }),
                                new AnalyzerPlugin("notVB", "v1", "resName", new string [] { "c:\\config.analyzer2.vb.dll" })
                            },
                            AdditionalFilePaths = new List<string> { "c:\\config.1.txt", "c:\\config.2.txt" }
                        }
                    }
            };
            
            // Create the project
            var projectSnippet = $@"
<PropertyGroup>
    <Language>{msBuildLanguage}</Language>
    <SonarQubeTestProject>{isTestProject.ToString()}</SonarQubeTestProject>
    <ResolvedCodeAnalysisRuleset>c:\\should.be.overridden.ruleset</ResolvedCodeAnalysisRuleset>
</PropertyGroup>

<ItemGroup>
    <Analyzer Include='project.additional.analyzer1.dll' />
    <Analyzer Include='c:\project.additional.analyzer2.dll' />
    <AdditionalFiles Include='project.additional.file.1.txt' />
    <AdditionalFiles Include='x:\aaa\project.additional.file.2.txt' />
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(config, projectSnippet);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath,
                TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert - check invariants
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            result.AssertTaskExecuted("GetAnalyzerSettings");
            result.BuildSucceeded.Should().BeTrue();

            AssertErrorLogIsSetBySonarQubeTargets(result);
            AssertWarningsAreNotTreatedAsErrorsNorIgnored(result);

            return result;
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
                        AnalyzerPlugins = new List<AnalyzerPlugin>
                        {
                            CreateAnalyzerPlugin("c:\\data\\new.analyzer1.dll"),
                            CreateAnalyzerPlugin("c:\\new.analyzer2.dll")
                        },
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
    <AdditionalFiles Include='should.be.removed/CONFIG.1.TXT' />
    <AdditionalFiles Include='should.be.removed\CONFIG.2.TXT' />

  </ItemGroup>
";
            var projectFilePath = CreateProjectFile(config, testSpecificProjectXml);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            result.AssertTargetExecuted(TargetConstants.CreateProjectSpecificDirs);
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
        [Description("Checks existing analysis settings are merged for projects using SonarQube 7.5+")]
        public void Roslyn_Settings_ValidSetup_NonLegacyServer_MergeSettings()
        {
            // Arrange

            var dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var dummyQpRulesetPath = TestUtils.CreateValidEmptyRuleset(dir, "dummyQp");

            // Create a valid config containing analyzer settings
            var config = new AnalysisConfig
            {
                SonarQubeVersion = "7.5", // non-legacy version
                ServerSettings = new AnalysisProperties
                {
                    new Property { Id = "sonar.cs.roslyn.ignoreIssues", Value = "false" }
                },
                AnalyzersSettings = new List<AnalyzerSettings>
                {
                    new AnalyzerSettings
                    {
                        Language = "cs",
                        RuleSetFilePath = dummyQpRulesetPath,
                        AnalyzerPlugins = new List<AnalyzerPlugin>
                        {
                            CreateAnalyzerPlugin("c:\\data\\new\\analyzer1.dll", "c:\\new.analyzer2.dll")                            
                        },
                        AdditionalFilePaths = new List<string> { "c:\\config\\duplicate.1.txt", "c:\\duplicate.2.txt" }
                    }
                }
            };

            var testSpecificProjectXml = @"
  <PropertyGroup>
    <ResolvedCodeAnalysisRuleSet>c:\original.ruleset</ResolvedCodeAnalysisRuleSet>
    <Language>C#</Language>
  </PropertyGroup>

  <ItemGroup>
    <!-- all analyzers specified in the project file should be preserved -->
    <Analyzer Include='c:\original\should.be.removed\analyzer1.dll' />
    <Analyzer Include='original\should.be.preserved\analyzer3.dll' />
  </ItemGroup>
  <ItemGroup>
    <!-- These additional files don't match ones in the config and should be preserved -->
    <AdditionalFiles Include='should.not.be.removed.additional1.txt' />
    <AdditionalFiles Include='should.not.be.removed.additional2.txt' />

    <!-- This additional file matches one in the config and should be replaced -->
    <AdditionalFiles Include='d:/should.be.removed/DUPLICATE.1.TXT' />
    <AdditionalFiles Include='d:\should.be.removed\duplicate.2.TXT' />

  </ItemGroup>
";
            var projectFilePath = CreateProjectFile(config, testSpecificProjectXml);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            result.AssertTargetExecuted(TargetConstants.CreateProjectSpecificDirs);
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            result.BuildSucceeded.Should().BeTrue();

            // Check the error log and ruleset properties are set
            AssertErrorLogIsSetBySonarQubeTargets(result);

            var actualProjectSpecificConfFolder = result.GetCapturedPropertyValue(TargetProperties.ProjectSpecificConfDir);
            Directory.Exists(actualProjectSpecificConfFolder).Should().BeTrue();

            var expectedMergedRuleSetFilePath = Path.Combine(actualProjectSpecificConfFolder, "merged.ruleset");
            AssertExpectedResolvedRuleset(result, expectedMergedRuleSetFilePath);
            RuleSetAssertions.CheckMergedRulesetFile(actualProjectSpecificConfFolder,
                @"c:\original.ruleset");

            AssertExpectedAdditionalFiles(result,
                result.GetCapturedPropertyValue(TargetProperties.ProjectConfFilePath),
                "should.not.be.removed.additional1.txt",
                "should.not.be.removed.additional2.txt",
                "c:\\config\\duplicate.1.txt",
                "c:\\duplicate.2.txt");

            AssertExpectedAnalyzers(result,
                "c:\\data\\new\\analyzer1.dll",
                "c:\\new.analyzer2.dll",
                "original\\should.be.preserved\\analyzer3.dll");

            AssertWarningsAreNotTreatedAsErrorsNorIgnored(result);
        }

        [TestMethod]
        public void Roslyn_Settings_LanguageMissing_NoError()
        {
            // Arrange

            // Set the config directory so the targets know where to look for the analysis config file
            var confDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "config");

            // Create a valid config file that does not contain analyzer settings
            var config = new AnalysisConfig();
            var configFilePath = Path.Combine(confDir, FileConstants.ConfigFileName);
            config.Save(configFilePath);

            // Create the project
            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeConfigPath>{confDir}</SonarQubeConfigPath>
  <ResolvedCodeAnalysisRuleset>c:\\should.be.overridden.ruleset</ResolvedCodeAnalysisRuleset>
  <Language />
</PropertyGroup>

<ItemGroup>
  <Analyzer Include='should.be.removed.analyzer1.dll' />
  <AdditionalFiles Include='should.not.be.removed.additional1.txt' />
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(null, projectSnippet);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath, TargetConstants.OverrideRoslynAnalysisTarget);

            var projectSpecificConfFilePath = result.GetCapturedPropertyValue(TargetProperties.ProjectConfFilePath);

            var expectedRoslynAdditionalFiles = new string[] {
                projectSpecificConfFilePath,
                "should.not.be.removed.additional1.txt" /* additional files are not removed */
            };

            // Assert
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            result.BuildSucceeded.Should().BeTrue();

            result.MessageLog.Should().Contain("Analysis language is not specified");

            AssertErrorLogIsSetBySonarQubeTargets(result);
            AssertExpectedResolvedRuleset(result, string.Empty);
            result.AssertExpectedItemGroupCount(TargetProperties.AnalyzerItemType, 0);
            AssertExpectedItemValuesExists(result, TargetProperties.AdditionalFilesItemType, expectedRoslynAdditionalFiles);
        }

        [TestMethod]
        [Description("Checks that a config file with no analyzer settings does not cause an issue")]
        public void Roslyn_Settings_SettingsMissing_NoError()
        {
            // Arrange

            // Set the config directory so the targets know where to look for the analysis config file
            var confDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "config");

            // Create a valid config file that does not contain analyzer settings
            var config = new AnalysisConfig();
            var configFilePath = Path.Combine(confDir, FileConstants.ConfigFileName);
            config.Save(configFilePath);

            // Create the project
            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeConfigPath>{confDir}</SonarQubeConfigPath>
  <ResolvedCodeAnalysisRuleset>c:\\should.be.overridden.ruleset</ResolvedCodeAnalysisRuleset>
</PropertyGroup>

<ItemGroup>
  <Analyzer Include='should.be.removed.analyzer1.dll' />
  <AdditionalFiles Include='should.not.be.removed.additional1.txt' />
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(null, projectSnippet);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath, TargetConstants.OverrideRoslynAnalysisTarget);

            var projectSpecificConfFilePath = result.GetCapturedPropertyValue(TargetProperties.ProjectConfFilePath);

            var expectedRoslynAdditionalFiles = new string[] {
                projectSpecificConfFilePath,
                "should.not.be.removed.additional1.txt" /* additional files are not removed any longer */
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
            var projectSnippet = @"
<PropertyGroup>
  <ErrorLog>pre-existing.log</ErrorLog>
  <ResolvedCodeAnalysisRuleset>pre-existing.ruleset</ResolvedCodeAnalysisRuleset>
  <WarningsAsErrors>CS101</WarningsAsErrors>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>

  <!-- This will override the value that was set earlier in the project file -->
  <SonarQubeTempPath />
</PropertyGroup>
";
            var projectFilePath = CreateProjectFile(null, projectSnippet);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath, TargetConstants.OverrideRoslynAnalysisTarget);

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
            var projectSnippet = @"
<PropertyGroup>
  <ErrorLog>already.set.txt</ErrorLog>
</PropertyGroup>
";

            var projectFilePath = CreateProjectFile(null, projectSnippet);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            result.BuildSucceeded.Should().BeTrue();

            result.AssertExpectedCapturedPropertyValue(TargetProperties.ErrorLog, "already.set.txt");
        }

        [TestMethod]
        [Description("Checks the code analysis properties are cleared for excludedprojects")]
        public void Roslyn_Settings_NotRunForExcludedProject()
        {
            // Arrange
            var projectSnippet = @"
<PropertyGroup>
  <SonarQubeExclude>TRUE</SonarQubeExclude>
  <ResolvedCodeAnalysisRuleset>Dummy value</ResolvedCodeAnalysisRuleset>
</PropertyGroup>
";
            var projectFilePath = CreateProjectFile(null, projectSnippet);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath, TargetConstants.OverrideRoslynAnalysisTarget);

            // Assert
            result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysisTarget);
            result.AssertTargetNotExecuted(TargetConstants.SetRoslynAnalysisPropertiesTarget);
            result.BuildSucceeded.Should().BeTrue();

            result.AssertExpectedCapturedPropertyValue("ResolvedCodeAnalysisRuleset", "Dummy value");
        }

        #endregion SetRoslynSettingsTarget tests

        #region AddAnalysisResults tests

        [TestMethod]
        [Description("Checks the target is not executed if the temp folder is not set")]
        public void Roslyn_SetResults_TempFolderIsNotSet()
        {
            // Arrange
            var projectSnippet = @"
<PropertyGroup>
  <SonarQubeTempPath />
</PropertyGroup>
";
            var projectFilePath = CreateProjectFile(null, projectSnippet);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath,
                TargetConstants.SetRoslynResultsTarget);

            // Assert
            result.AssertTargetNotExecuted(TargetConstants.SetRoslynResultsTarget);
        }

        [TestMethod]
        [Description("Checks the analysis setting is not set if the results file does not exist")]
        public void Roslyn_SetResults_ResultsFileDoesNotExist()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");

            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
</PropertyGroup>
";
            var projectFilePath = CreateProjectFile(null, projectSnippet);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath, TargetConstants.SetRoslynResultsTarget);

            // Assert
            result.AssertTargetExecuted(TargetConstants.SetRoslynResultsTarget);
            AssertAnalysisSettingDoesNotExist(result, RoslynAnalysisResultsSettingName);
        }

        [TestMethod]
        [Description("Checks the analysis setting is set if the result file exists")]
        public void Roslyn_SetResults_ResultsFileExists()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var resultsFile = TestUtils.CreateTextFile(rootInputFolder, "error.report.txt", "dummy report content");

            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarCompileErrorLog>{resultsFile}</SonarCompileErrorLog>
</PropertyGroup>
";
            var projectFilePath = CreateProjectFile(null, projectSnippet);

            // Act
            var result = BuildRunner.BuildTargets(TestContext,
                projectFilePath,
                TargetConstants.CreateProjectSpecificDirs, TargetConstants.SetRoslynResultsTarget);

            var projectSpecificOutDir = result.GetCapturedPropertyValue(TargetProperties.ProjectSpecificOutDir);

            // Assert
            result.AssertTargetExecuted(TargetConstants.CreateProjectSpecificDirs);
            result.AssertTargetExecuted(TargetConstants.SetRoslynResultsTarget);
            AssertExpectedAnalysisSetting(result, RoslynAnalysisResultsSettingName, resultsFile);
            AssertExpectedAnalysisSetting(result, AnalyzerWorkDirectoryResultsSettingName, projectSpecificOutDir);
        }

        [TestMethod]
        [Description("Checks the analysis settings are set if the normal Roslyn and the Razor result files exist")]
        public void Roslyn_SetResults_BothResultsFilesCreated()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");

            var resultsFile = TestUtils.CreateTextFile(rootInputFolder, "error.report.txt", "dummy report content");
            var razorResultsFile = TestUtils.CreateTextFile(rootInputFolder, "razor.error.report.txt", "dummy report content");
            
            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarCompileErrorLog>{resultsFile}</SonarCompileErrorLog>
  <RazorSonarCompileErrorLog>{razorResultsFile}</RazorSonarCompileErrorLog>
</PropertyGroup>
";
            var projectFilePath = CreateProjectFile(null, projectSnippet);

            // Act
            var result = BuildRunner.BuildTargets(TestContext,
                projectFilePath,
                TargetConstants.CreateProjectSpecificDirs, TargetConstants.SetRoslynResultsTarget);

            var projectSpecificOutDir = result.GetCapturedPropertyValue(TargetProperties.ProjectSpecificOutDir);

            // Assert
            result.AssertTargetExecuted(TargetConstants.CreateProjectSpecificDirs);
            result.AssertTargetExecuted(TargetConstants.SetRoslynResultsTarget);
            AssertExpectedAnalysisSetting(result, RoslynAnalysisResultsSettingName, resultsFile + "|" + razorResultsFile);
            AssertExpectedAnalysisSetting(result, AnalyzerWorkDirectoryResultsSettingName, projectSpecificOutDir);
        }

        #endregion AddAnalysisResults tests

        #region Combined tests

        [TestMethod]
        [Description("Checks the targets are executed in the expected order")]
        public void Roslyn_TargetExecutionOrder()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // We need to set the CodeAnalyisRuleSet property if we want ResolveCodeAnalysisRuleSet
            // to be executed. See test bug https://github.com/SonarSource/sonar-scanner-msbuild/issues/776
            var dummyQpRulesetPath = TestUtils.CreateValidEmptyRuleset(rootInputFolder, "dummyQp");

            var projectSnippet = $@"
<PropertyGroup>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
  <SonarQubeOutputPath>{rootInputFolder}</SonarQubeOutputPath>
  <SonarQubeConfigPath>{rootOutputFolder}</SonarQubeConfigPath>
  <CodeAnalysisRuleSet>{dummyQpRulesetPath}</CodeAnalysisRuleSet>
</PropertyGroup>

<ItemGroup>
  <SonarQubeSetting Include='sonar.other.setting'>
    <Value>other value</Value>
  </SonarQubeSetting>
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(null, projectSnippet);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath, TargetConstants.DefaultBuildTarget);

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

        #region Checks

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
                TestContext.WriteLine("\t{0}", item.Value);
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
            settings.Should().NotBeEmpty();

            var matches = settings.Where(v => v.Value.Equals(settingName, System.StringComparison.Ordinal)).ToList();
            matches.Should().ContainSingle($"Only one and only expecting one SonarQubeSetting with include value of '{0}' to exist. Count: {matches.Count}", settingName);

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
        private string CreateProjectFile(AnalysisConfig analysisConfig, string testSpecificProjectXml)
        {
            var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            CreateCaptureDataTargetsFile(projectDirectory);

            if (analysisConfig != null)
            {
                var configFilePath = Path.Combine(projectDirectory, FileConstants.ConfigFileName);
                analysisConfig.Save(configFilePath);
            }

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            File.Exists(sqTargetFile).Should().BeTrue("Test error: the SonarQube analysis targets file could not be found. Full path: {0}", sqTargetFile);
            TestContext.AddResultFile(sqTargetFile);


            var template = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='Current' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>

  <!-- Boilerplate -->
  <!-- All of these boilerplate properties can be overridden by setting the value again in the test-specific XML snippet -->
  <PropertyGroup>
    <ImportByWildcardBeforeMicrosoftCommonTargets>false</ImportByWildcardBeforeMicrosoftCommonTargets>
    <ImportByWildcardAfterMicrosoftCommonTargets>false</ImportByWildcardAfterMicrosoftCommonTargets>
    <ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets>false</ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets>
    <ImportUserLocationsByWildcardAfterMicrosoftCommonTargets>false</ImportUserLocationsByWildcardAfterMicrosoftCommonTargets>
    <OutputPath>bin\</OutputPath>
    <OutputType>library</OutputType>
    <ProjectGuid>ffdb93c0-2880-44c7-89a6-bbd4ddab034a</ProjectGuid>
    <CodePage>65001</CodePage>
    <Language>C#</Language>
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

            var projectFilePath = Path.Combine(projectDirectory, TestContext.TestName + ".proj.txt");
            File.WriteAllText(projectFilePath, projectData);
            TestContext.AddResultFile(projectFilePath);

            return projectFilePath;
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
<Project ToolsVersion='Current' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  
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
    <Message Importance='high' Text='CAPTURE::PROPERTY::ProjectSpecificConfDir::$(ProjectSpecificConfDir)' />

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

        private static AnalyzerPlugin CreateAnalyzerPlugin(params string[] fileList) =>
            new AnalyzerPlugin
            {
                AssemblyPaths = new List<string>(fileList)
            };

        #endregion Setup
    }
}
