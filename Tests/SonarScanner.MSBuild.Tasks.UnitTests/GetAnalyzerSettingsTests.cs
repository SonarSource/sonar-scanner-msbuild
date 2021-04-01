/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
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
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;
using MSCA = Microsoft.CodeAnalysis;

namespace SonarScanner.MSBuild.Tasks.UnitTests
{
    [TestClass]
    public class GetAnalyzerSettingsTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void GetAnalyzerSettings_MissingConfigDir_NoError()
        {
            // Arrange
            var testSubject = new GetAnalyzerSettings
            {
                AnalysisConfigDir = "c:\\missing"
            };

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            CheckNoAnalyzerSettings(testSubject);
        }

        [TestMethod]
        public void GetAnalyzerSettings_MissingConfigFile_NoError()
        {
            // Arrange
            var testSubject = new GetAnalyzerSettings
            {
                AnalysisConfigDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext)
            };

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            CheckNoAnalyzerSettings(testSubject);
        }

        [TestMethod]
        public void GetAnalyzerSettings_ConfigExistsButNoAnalyzerSettings_NoError()
        {
            // Arrange
            var testSubject = CreateConfiguredTestSubject(new AnalysisConfig(), "anyLanguage", TestContext);

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            CheckNoAnalyzerSettings(testSubject);
        }

        [DataTestMethod]
        [DataRow("7.3", DisplayName = "Legacy")]
        [DataRow("7.4")]
        public void GetAnalyzerSettings_ConfigExists_NoLanguage_SettingsOverwritten(string sonarQubeVersion)
        {
            // Arrange
            var config = new AnalysisConfig
            {
                SonarQubeVersion = sonarQubeVersion,
                AnalyzersSettings = new List<AnalyzerSettings>
                {
                    new AnalyzerSettings
                    {
                        Language = "cs",
                        RulesetPath = "f:\\yyy.ruleset",
                        AnalyzerPlugins = new List<AnalyzerPlugin> { CreateAnalyzerPlugin("c:\\local_analyzer.dll") },
                        AdditionalFilePaths = new List<string> { "c:\\add1.txt", "d:\\add2.txt", "e:\\subdir\\add3.txt" }
                    }
                }
            };

            var testSubject = CreateConfiguredTestSubject(config, "" /* no language specified */, TestContext);
            testSubject.OriginalAdditionalFiles = new string[]
            {
                "original.should.be.preserved.txt"
            };

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            testSubject.RuleSetFilePath.Should().BeNull();
            testSubject.AnalyzerFilePaths.Should().BeNull();
            testSubject.AdditionalFilePaths.Should().BeEquivalentTo("original.should.be.preserved.txt");
        }

        [TestMethod]
        public void GetAnalyzerSettings_ConfigExists_Legacy_SettingsOverwritten()
        {
            // Arrange
            // SONARMSBRU-216: non-assembly files should be filtered out
            var filesInConfig = new List<AnalyzerPlugin>
            {
                CreateAnalyzerPlugin("c:\\analyzer1.DLL"),
                CreateAnalyzerPlugin(
                    "c:\\not_an_assembly.exe",
                    "c:\\not_an_assembly.zip",
                    "c:\\not_an_assembly.txt",
                    "d:\\analyzer2.dll"),
                CreateAnalyzerPlugin(
                    "c:\\not_an_assembly.dll.foo",
                    "c:\\not_an_assembly.winmd"),
                CreateAnalyzerPlugin("e:\\analyzer3.dll")
            };

            var config = new AnalysisConfig
            {
                SonarQubeHostUrl = "http://sonarqube.com",
                SonarQubeVersion = "7.3",
                ServerSettings = new AnalysisProperties
                {
                    // Setting should be ignored
                    new Property { Id = "sonar.cs.roslyn.ignoreIssues", Value = "true" }
                },
                AnalyzersSettings = new List<AnalyzerSettings>
                {
                    new AnalyzerSettings
                    {
                        Language = "cs",
                        RulesetPath = "f:\\yyy.ruleset",
                        AnalyzerPlugins = filesInConfig,
                        AdditionalFilePaths = new List<string> { "c:\\add1.txt", "d:\\add2.txt", "e:\\subdir\\add3.txt" }
                    },

                    new AnalyzerSettings
                    {
                        Language = "cobol",
                        RulesetPath = "f:\\xxx.ruleset",
                        AnalyzerPlugins = filesInConfig,
                        AdditionalFilePaths = new List<string> { "c:\\cobol.\\add1.txt", "d:\\cobol\\add2.txt" }
                    }
                }
            };

            var testSubject = CreateConfiguredTestSubject(config, "cs", TestContext);
            testSubject.OriginalAdditionalFiles = new string[]
            {
                "original.should.be.preserved.txt",
                "original.should.be.replaced\\add2.txt",
                "e://foo//should.be.replaced//add3.txt"
            };

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            testSubject.RuleSetFilePath.Should().Be("f:\\yyy.ruleset");
            testSubject.AnalyzerFilePaths.Should().BeEquivalentTo("c:\\analyzer1.DLL", "d:\\analyzer2.dll", "e:\\analyzer3.dll");
            testSubject.AdditionalFilePaths.Should().BeEquivalentTo("c:\\add1.txt", "d:\\add2.txt",
                "e:\\subdir\\add3.txt", "original.should.be.preserved.txt");
        }

        [TestMethod]
        public void GetAnalyzerSettings_ConfigExists_NewBehaviour_SettingsMerged()
        {
            // Expecting both the additional files and the analyzers to be merged

            // Arrange
            // SONARMSBRU-216: non-assembly files should be filtered out
            var filesInConfig = new List<AnalyzerPlugin>
            {
                new AnalyzerPlugin
                {
                    AssemblyPaths = new List<string>
                    {
                        "c:\\config\\analyzer1.DLL",
                        "c:\\not_an_assembly.exe",
                        "c:\\not_an_assembly.zip",
                    }
                },
                new AnalyzerPlugin
                {
                    AssemblyPaths = new List<string>
                    {
                        "c:\\config\\analyzer2.dll",
                        "c:\\not_an_assembly.txt",
                        "c:\\not_an_assembly.dll.foo",
                        "c:\\not_an_assembly.winmd"
                    }
                }
            };

            var config = new AnalysisConfig
            {
                SonarQubeVersion = "7.4",
                ServerSettings = new AnalysisProperties
                                {
                    new Property { Id = "sonar.cs.roslyn.ignoreIssues", Value = "false" }
                },
                AnalyzersSettings = new List<AnalyzerSettings>
                {
                    new AnalyzerSettings
                    {
                        Language = "cs",
                        RulesetPath = "f:\\yyy.ruleset",
                        AnalyzerPlugins = filesInConfig,
                        AdditionalFilePaths = new List<string> { "c:\\config\\add1.txt", "d:\\config\\add2.txt" }
                    },

                    new AnalyzerSettings
                    {
                        Language = "cobol",
                        RulesetPath = "f:\\xxx.ruleset",
                        AnalyzerPlugins = new List<AnalyzerPlugin>(),
                        AdditionalFilePaths = new List<string> { "c:\\cobol.\\add1.txt", "d:\\cobol\\add2.txt" }
                    }

                }
            };

            var testSubject = CreateConfiguredTestSubject(config, "cs", TestContext);

            testSubject.OriginalAnalyzers = new string[]
            {
                "c:\\original.should.be.removed\\analyzer1.DLL",
                "f:\\original.should.be.preserved\\analyzer3.dll"
            };

            testSubject.OriginalAdditionalFiles = new string[]
            {
                "original.should.be.preserved.txt",
                "original.should.be.replaced\\add2.txt"
            };

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            testSubject.RuleSetFilePath.Should().Be("f:\\yyy.ruleset");

            testSubject.AnalyzerFilePaths.Should().BeEquivalentTo(
                "c:\\config\\analyzer1.DLL",
                "c:\\config\\analyzer2.dll",
                "f:\\original.should.be.preserved\\analyzer3.dll");

            testSubject.AdditionalFilePaths.Should().BeEquivalentTo(
                "c:\\config\\add1.txt",
                "d:\\config\\add2.txt",
                "original.should.be.preserved.txt");
        }

        [DataTestMethod]
        [DataRow("7.3", DisplayName = "Legacy")]
        [DataRow("7.4")]
        public void GetAnalyzerSettings_ConfigExists_TestProject_SonarAnalyzerSettingsUsed_CSharp(string sonarQubeVersion)
        {
            // Arrange and Act
            var executedTask = ExecuteGetAnalyzerSettings_ConfigExists_TestProject_SonarAnalyzerSettingsUsed(sonarQubeVersion, "cs");

            // Assert
            executedTask.RuleSetFilePath.Should().Be("c:\\csharp-deactivated.ruleset");
            executedTask.AnalyzerFilePaths.Should().BeEquivalentTo("c:\\sonar.csharp1.dll", "c:\\Google.Protobuf.dll");
            executedTask.AdditionalFilePaths.Should().BeEquivalentTo("c:\\add1.txt", "d:\\replaced1.txt");
        }

        [DataTestMethod]
        [DataRow("7.3", DisplayName = "Legacy")]
        [DataRow("7.4")]
        public void GetAnalyzerSettings_ConfigExists_TestProject_SonarAnalyzerSettingsUsed_VB(string sonarQubeVersion)
        {
            // Arrange and Act
            var executedTask = ExecuteGetAnalyzerSettings_ConfigExists_TestProject_SonarAnalyzerSettingsUsed(sonarQubeVersion, "vbnet");

            // Assert
            executedTask.RuleSetFilePath.Should().Be("c:\\vbnet-deactivated.ruleset");
            executedTask.AnalyzerFilePaths.Should().BeEquivalentTo("c:\\sonar.vbnet1.dll", "c:\\Google.Protobuf.dll");
            executedTask.AdditionalFilePaths.Should().BeEquivalentTo("c:\\add1.txt.vb", "d:\\replaced1.txt");
        }

        [TestMethod]
        public void GetAnalyzerSettings_ConfigExists_NewBehaviour_TestProject_SonarAnalyzerSettingsUsed_UnknownLanguage()
        {
            // Arrange and Act
            var executedTask = ExecuteGetAnalyzerSettings_ConfigExists_TestProject_SonarAnalyzerSettingsUsed("7.4", "unknownLang");

            // Assert
            executedTask.RuleSetFilePath.Should().BeNull();
            executedTask.AnalyzerFilePaths.Should().BeNull();
            executedTask.AdditionalFilePaths.Should().BeEquivalentTo("original.should.be.removed.txt", "original.should.be.replaced\\replaced1.txt");
        }

        private GetAnalyzerSettings ExecuteGetAnalyzerSettings_ConfigExists_TestProject_SonarAnalyzerSettingsUsed(string sonarQubeVersion, string language)
        {
            // Want to test the behaviour with old and new SQ version. Expecting the same results in each case.
            // Arrange
            var config = new AnalysisConfig
            {
                SonarQubeVersion = sonarQubeVersion,
                ServerSettings = new AnalysisProperties
                {
                    // Setting should be ignored
                    new Property { Id = "sonar.cs.roslyn.ignoreIssues", Value = "true" },
                    new Property { Id = "sonar.vbnet.roslyn.ignoreIssues", Value = "true" }
                },
                AnalyzersSettings = new List<AnalyzerSettings>
                {
                    new AnalyzerSettings
                    {
                        Language = "cs",
                        RulesetPath = @"f:\csharp-normal.ruleset",
                        DeactivatedRulesetPath = @"c:\csharp-deactivated.ruleset",
                        AnalyzerPlugins = new List<AnalyzerPlugin>
                        {
                            new AnalyzerPlugin("roslyn.wintellect", "2.0", "dummy resource", new [] { @"c:\wintellect1.dll", @"c:\wintellect\bar.ps1", @"c:\Google.Protobuf.dll" }),
                            new AnalyzerPlugin("csharp", "1.1", "dummy resource2", new [] { @"c:\sonar.csharp1.dll", @"c:\foo.ps1", @"c:\Google.Protobuf.dll" }),
                        },
                        AdditionalFilePaths = new List<string> { @"c:\add1.txt", @"d:\replaced1.txt" }
                    },
                    new AnalyzerSettings
                    {
                        Language = "vbnet",
                        RulesetPath = @"f:\vbnet-normal.ruleset.vb",
                        DeactivatedRulesetPath = @"c:\vbnet-deactivated.ruleset",
                        AnalyzerPlugins = new List<AnalyzerPlugin>
                        {
                            new AnalyzerPlugin("roslyn.wintellect", "2.0", "dummy resource", new [] { @"c:\wintellect1.dll", @"c:\wintellect\bar.ps1", @"c:\Google.Protobuf.dll" }),
                            new AnalyzerPlugin("vbnet", "1.1", "dummy resource2", new [] { @"c:\sonar.vbnet1.dll", @"c:\foo.ps1", @"c:\Google.Protobuf.dll" }),
                        },
                        AdditionalFilePaths = new List<string> { @"c:\add1.txt.vb", @"d:\replaced1.txt" }
                    },
                    new AnalyzerSettings // Settings for a different language
                    {
                        Language = "cobol",
                        RulesetPath = @"f:\cobol-normal.ruleset",
                        DeactivatedRulesetPath = @"c:\cobol-deactivated.ruleset",
                        AnalyzerPlugins = new List<AnalyzerPlugin>
                        {
                            new AnalyzerPlugin("cobol.analyzer", "1.0", "dummy resource", new [] { @"c:\cobol1.dll", @"c:\cobol2.dll" })
                        },
                        AdditionalFilePaths = new List<string> { @"c:\cobol.\add1.txt", @"d:\cobol\add2.txt" }
                    }
                }
            };

            var testSubject = CreateConfiguredTestSubject(config, language, TestContext);
            testSubject.IsTestProject = true;
            testSubject.OriginalAnalyzers = new[]
            {
                 "c:\\analyzer1.should.be.replaced.dll",
                 "c:\\analyzer2.should.be.replaced.dll",
                 "c:\\Google.Protobuf.dll", // same name as an assembly in the csharp plugin (above)
            };
            testSubject.OriginalAdditionalFiles = new[]
            {
                "original.should.be.removed.txt",
                "original.should.be.replaced\\replaced1.txt",
            };

            // Act
            ExecuteAndCheckSuccess(testSubject);
            return testSubject;
        }

        [DataTestMethod]
        [DataRow("cs")]
        [DataRow("vbnet")]
        public void ShouldMerge_OldServerVersion_ReturnsFalse(string language)
        {
            // The "importAllValue" setting should be ignored for old server versions
            var logger = CheckShouldMerge("7.3.1", language, ignoreExternalIssues: "true", expected: false);
            logger.AssertInfoMessageExists("External issues are not supported on this version of SonarQube. SQv7.4+ is required.");
        }

        [DataTestMethod]
        [DataRow("cs")]
        [DataRow("vbnet")]
        public void ShouldMerge_Multiples_NewServer_NoSetting_ReturnsFalse(string language)
        {
            // Should default to false i.e. override, don't merge
            var logger = CheckShouldMerge("7.4.0.0", language, ignoreExternalIssues: null /* not set */, expected: false);
            logger.AssertDebugLogged($"sonar.{language}.roslyn.ignoreIssues=true");
        }

        [DataTestMethod]
        [DataRow("cs")]
        [DataRow("vbnet")]
        public void ShouldMerge_NewServerVersion_SettingIsTrue_ReturnsFalse(string language)
        {
            var logger = CheckShouldMerge("7.4.0", language, ignoreExternalIssues: "true", expected: false);
            logger.AssertDebugLogged($"sonar.{language}.roslyn.ignoreIssues=true");
        }

        [DataTestMethod]
        [DataRow("cs")]
        [DataRow("vbnet")]
        public void ShouldMerge_NewServerVersion_SettingIsFalse_ReturnsTrue(string language)
        {
            var logger = CheckShouldMerge("7.4", language, ignoreExternalIssues: "false", expected: true);
            logger.AssertDebugLogged($"sonar.{language}.roslyn.ignoreIssues=false");
        }

        [DataTestMethod]
        [DataRow("cs")]
        [DataRow("vbnet")]
        public void ShouldMerge_NewServerVersion_InvalidSetting_NoError_ReturnsFalse(string language)
        {
            var logger = CheckShouldMerge("7.4", language, ignoreExternalIssues: "not a boolean value", expected: false);
            logger.AssertSingleWarningExists($"Invalid value for 'sonar.{language}.roslyn.ignoreIssues'. Expecting 'true' or 'false'. Actual: 'not a boolean value'. External issues will not be imported.");
        }

        private static TestLogger CheckShouldMerge(string serverVersion, string language, string ignoreExternalIssues, bool expected)
        {
            // Should default to true i.e. don't override, merge
            var logger = new TestLogger();
            var config = new AnalysisConfig
            {
                SonarQubeHostUrl = "http://sonarqube.com",
                SonarQubeVersion = serverVersion
            };
            if (ignoreExternalIssues != null)
            {
                config.ServerSettings = new AnalysisProperties
                {
                    new Property { Id = $"sonar.{language}.roslyn.ignoreIssues", Value = ignoreExternalIssues }
                };
            }

            var result = GetAnalyzerSettings.ShouldMergeAnalysisSettings(language, config, logger);

            result.Should().Be(expected);
            return logger;
        }

        [TestMethod]
        public void MergeRulesets_NoOriginalRuleset_FirstGeneratedRulsetUsed()
        {
            // Arrange
            var config = new AnalysisConfig
            {
                SonarQubeVersion = "7.4",
                AnalyzersSettings = new List<AnalyzerSettings>
                {
                    new AnalyzerSettings
                    {
                        Language = "xxx",
                        RulesetPath = "firstGeneratedRuleset.txt"
                    }
                }
            };

            var testSubject = CreateConfiguredTestSubject(config, "xxx", TestContext);
            testSubject.OriginalRulesetFilePath = null;

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            testSubject.RuleSetFilePath.Should().Be("firstGeneratedRuleset.txt");
        }

        [DataTestMethod]
        [DataRow(@".\..\originalRuleset.txt", DisplayName = "Relative path")]
        [DataRow(@"c:\solution.folder\originalRuleset.txt", DisplayName = "Absolute path")]
        public void MergeRulesets_OriginalRulesetSpecified_RelativePath_SecondGeneratedRulesetUsed(string originalRulesetFilePath)
        {
            // Arrange

            var dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var dummyQpRulesetPath = TestUtils.CreateValidEmptyRuleset(dir, "dummyQp");
            var config = CreateMergingAnalysisConfig("xxx", dummyQpRulesetPath);

            var testSubject = CreateConfiguredTestSubject(config, "xxx", TestContext);
            testSubject.CurrentProjectDirectoryPath = @"c:\solution.folder\project.folder";
            testSubject.OriginalRulesetFilePath = originalRulesetFilePath;
            testSubject.ProjectSpecificConfigDirectory = testSubject.AnalysisConfigDir;

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            CheckMergedRulesetFile(testSubject, @"c:\solution.folder\originalRuleset.txt");
        }

        [TestMethod]
        // Regression test for #581: Sonar issues are reported as external issues
        // https://github.com/SonarSource/sonar-scanner-msbuild/issues/581
        public void MergeRuleset_CheckQPSettingsWin()
        {
            // Arrange
            // Off in QP, on locally -> off
            var qpRulesetPath = CreateRuleset("qpRuleset", @"<?xml version='1.0' encoding='utf-8'?>
<RuleSet xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' Name='x' Description='x' ToolsVersion='14.0'>
  <Rules AnalyzerId='analyzer1' RuleNamespace='ns1'>
    <Rule Id='SharedRuleOffInQP' Action='None' />
    <Rule Id='SharedRuleOffInLocal' Action='Warning' />
    <Rule Id='QPOnlyRule' Action='Info' />
  </Rules>
</RuleSet>");

            var localRulesetPath = CreateRuleset("localRuleset", @"<?xml version='1.0' encoding='utf-8'?>
<RuleSet xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' Name='x' Description='x' ToolsVersion='14.0'>
  <Rules AnalyzerId='analyzer1' RuleNamespace='ns1'>
    <Rule Id='SharedRuleOffInQP' Action='Error' />
    <Rule Id='SharedRuleOffInLocal' Action='None' />
    <Rule Id='LocalOnlyRule' Action='Error' />
  </Rules>
</RuleSet>");

            var config = CreateMergingAnalysisConfig("xxx", qpRulesetPath);

            var testSubject = CreateConfiguredTestSubject(config, "xxx", TestContext);
            testSubject.OriginalRulesetFilePath = localRulesetPath;
            testSubject.ProjectSpecificConfigDirectory = testSubject.AnalysisConfigDir;

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            CheckMergedRulesetFile(testSubject, localRulesetPath);

            // Check individual rule severities are set correctly
            var finalPath = testSubject.RuleSetFilePath;
            var actualRuleset = MSCA.RuleSet.LoadEffectiveRuleSetFromFile(finalPath);

            CheckExpectedDiagnosticLevel(actualRuleset, "SharedRuleOffInQP", MSCA.ReportDiagnostic.Suppress);
            CheckExpectedDiagnosticLevel(actualRuleset, "SharedRuleOffInLocal", MSCA.ReportDiagnostic.Warn);
            CheckExpectedDiagnosticLevel(actualRuleset, "QPOnlyRule", MSCA.ReportDiagnostic.Info);
            CheckExpectedDiagnosticLevel(actualRuleset, "LocalOnlyRule", MSCA.ReportDiagnostic.Error);
        }

        #endregion Tests

        #region Private methods

        private static GetAnalyzerSettings CreateConfiguredTestSubject(AnalysisConfig config, string language, TestContext testContext)
        {
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
            var testSubject = new GetAnalyzerSettings
            {
                Language = language
            };

            var fullPath = Path.Combine(testDir, FileConstants.ConfigFileName);
            config.Save(fullPath);

            testSubject.AnalysisConfigDir = testDir;
            return testSubject;
        }

        private static AnalysisConfig CreateMergingAnalysisConfig(string language, string qpRulesetFilePath) =>
            new AnalysisConfig
            {
                SonarQubeVersion = "7.4",
                ServerSettings = new AnalysisProperties
                {
                    new Property { Id = $"sonar.{language}.roslyn.ignoreIssues", Value = "false" }
                },
                AnalyzersSettings = new List<AnalyzerSettings>
                {
                    new AnalyzerSettings
                    {
                        Language = language,
                        RulesetPath = qpRulesetFilePath
                    }
                }
            };

        public string CreateRuleset(string fileNameWithoutExtension, string content)
        {
            var dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var filePath = TestUtils.CreateTextFile(dir, fileNameWithoutExtension + ".ruleset", content);
            return filePath;
        }

        private static AnalyzerPlugin CreateAnalyzerPlugin(params string[] fileList) =>
            new AnalyzerPlugin { AssemblyPaths = new List<string>(fileList) };

        #endregion

        #region Checks methods

        private static void ExecuteAndCheckSuccess(Task task)
        {
            var dummyEngine = new DummyBuildEngine();
            task.BuildEngine = dummyEngine;

            var taskSucess = task.Execute();
            taskSucess.Should().BeTrue("Expecting the task to succeed");
            dummyEngine.AssertNoErrors();
            dummyEngine.AssertNoWarnings();
        }

        private static void CheckNoAnalyzerSettings(GetAnalyzerSettings executedTask)
        {
            executedTask.RuleSetFilePath.Should().BeNull();
            executedTask.AdditionalFilePaths.Should().BeNull();
            executedTask.AnalyzerFilePaths.Should().BeNull();
        }

        private void CheckMergedRulesetFile(GetAnalyzerSettings executedTask, string originalRulesetFullPath)
        {
            var expectedMergedRulesetFilePath = RuleSetAssertions.CheckMergedRulesetFile(executedTask.ProjectSpecificConfigDirectory, originalRulesetFullPath);
            TestContext.AddResultFile(expectedMergedRulesetFilePath);
            executedTask.RuleSetFilePath.Should().Be(expectedMergedRulesetFilePath);
        }

        private static void CheckExpectedDiagnosticLevel(MSCA.RuleSet ruleset, string ruleId, MSCA.ReportDiagnostic expected)
        {
            ruleset.SpecificDiagnosticOptions.Keys.Contains(ruleId).Should().BeTrue();
            ruleset.SpecificDiagnosticOptions[ruleId].Should().Be(expected);
        }

        #endregion Checks methods
    }
}
