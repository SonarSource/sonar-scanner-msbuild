/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
 * mailto: info AT sonarsource DOT com
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

namespace SonarScanner.MSBuild.Tasks.UnitTest
{
    [TestClass]
    public class GetAnalyzerSettingsTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void MissingConfigDir_NoError()
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
        public void MissingConfigFile_NoError()
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
        public void ConfigExistsButNoAnalyzerSettings_NoError()
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
        public void ConfigExists_NoLanguage_SettingsOverwritten(string sonarQubeVersion)
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

            var testSubject = CreateConfiguredTestSubject(config, string.Empty /* no language specified */, TestContext);
            testSubject.OriginalAdditionalFiles = new[]
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
        public void ConfigExists_Legacy_SettingsOverwritten()
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
                    new("sonar.cs.roslyn.ignoreIssues", "true")
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
            testSubject.OriginalAdditionalFiles = new[]
            {
                "original.should.be.preserved.txt",
                "original.should.be.removed\\add2.txt",
                "e://foo//should.be.removed//add3.txt"
            };

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            testSubject.RuleSetFilePath.Should().Be("f:\\yyy.ruleset");
            testSubject.AnalyzerFilePaths.Should().BeEquivalentTo("c:\\analyzer1.DLL", "d:\\analyzer2.dll", "e:\\analyzer3.dll");
            testSubject.AdditionalFilePaths.Should().BeEquivalentTo("c:\\add1.txt", "d:\\add2.txt", "e:\\subdir\\add3.txt", "original.should.be.preserved.txt");
        }

        [TestMethod]
        public void ConfigExists_SettingsMerged()
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
                ServerSettings = new AnalysisProperties { new("sonar.cs.roslyn.ignoreIssues", "false") },
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

            testSubject.OriginalAnalyzers = new[]
            {
                "c:\\original.should.be.preserved\\analyzer1.DLL",
                "f:\\original.should.be.preserved\\analyzer3.dll",
                "c:\\SonarAnalyzer\\should.be.preserved.SomeAnalyzer.dll",
                "c:\\should.be.removed\\SonarAnalyzer.Fake.DLL", // We consider all analyzers starting with 'SonarAnalyzer' as ours, this will be removed as a duplicate reference
                "c:\\should.be.removed\\SonarAnalyzer.CFG.dll",
                "c:\\should.be.removed\\SonarAnalyzer.dll",
                "c:\\should.be.removed\\SonarAnalyzer.CSharp.dll",
                "c:\\should.be.removed\\SonarAnalyzer.vIsUaLbAsIc.dll",
                "c:\\should.be.removed\\sOnAranaLYZer.Security.dll"
            };

            testSubject.OriginalAdditionalFiles = new[]
            {
                "original.should.be.preserved.txt",
                "original.should.be.removed\\add2.txt"
            };

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            testSubject.RuleSetFilePath.Should().Be("f:\\yyy.ruleset");

            testSubject.AnalyzerFilePaths.Should().BeEquivalentTo(
                "c:\\config\\analyzer1.DLL",
                "c:\\config\\analyzer2.dll",
                "c:\\original.should.be.preserved\\analyzer1.DLL",
                "f:\\original.should.be.preserved\\analyzer3.dll",
                "c:\\SonarAnalyzer\\should.be.preserved.SomeAnalyzer.dll");

            testSubject.AdditionalFilePaths.Should().BeEquivalentTo(
                "c:\\config\\add1.txt",
                "d:\\config\\add2.txt",
                "original.should.be.preserved.txt");
        }

        [DataTestMethod]
        [DataRow("7.3", "cs", DisplayName = "Legacy CS")]
        [DataRow("7.3", "vbnet", DisplayName = "Legacy VB")]
        public void ConfigExists_ForLegacyProductProject_SonarAnalyzersAndConfigurationUsed(string sonarQubeVersion, string language)
        {
            // Arrange and Act
            var executedTask = Execute_ConfigExists(sonarQubeVersion, language, false, null);

            // Assert
            executedTask.RuleSetFilePath.Should().Be($@"c:\{language}-normal.ruleset");
            // There are two Google.Protobuf.dll as one is from the C# analyzer and one from the VBNet analyzer.
            executedTask.AnalyzerFilePaths.Should().BeEquivalentTo(@"c:\wintellect1.dll", @"c:\Google.Protobuf.dll", $@"c:\sonar.{language}.dll", @"c:\Google.Protobuf.dll");
            executedTask.AdditionalFilePaths.Should().BeEquivalentTo($@"c:\add1.{language}.txt", @"d:\replaced1.txt", "original.should.be.preserved.for.product.txt");
        }

        [DataTestMethod]
        [DataRow("7.4", "cs")]
        [DataRow("7.4", "vbnet")]
        public void ConfigExists_ForProductProject_SonarAnalyzersAndConfigurationMergedWithUserProvided(string sonarQubeVersion, string language)
        {
            // Arrange and Act
            var executedTask = Execute_ConfigExists(sonarQubeVersion, language, false, null);

            // Assert
            executedTask.RuleSetFilePath.Should().Be($@"c:\{language}-normal.ruleset");
            executedTask.AnalyzerFilePaths.Should().BeEquivalentTo(@"c:\wintellect1.dll", @"c:\Google.Protobuf.dll", $@"c:\sonar.{language}.dll", @"c:\analyzer1.should.be.preserved.dll", @"c:\analyzer2.should.be.preserved.dll");
            executedTask.AdditionalFilePaths.Should().BeEquivalentTo($@"c:\add1.{language}.txt", @"d:\replaced1.txt", "original.should.be.preserved.for.product.txt");
        }

        [DataTestMethod]
        [DataRow("8.0.0.18955", "cs", /* not set */ null, DisplayName = "SonarCloud build version CS")]
        [DataRow("8.9", "cs", /* not set */ null)]
        [DataRow("8.9", "cs", "false")]
        [DataRow("9.0", "cs", "FALSE")]
        [DataRow("10.0", "cs", "UnexpectedParamValue")]
        [DataRow("8.0.0.18955", "vbnet", /* not set */ null, DisplayName = "SonarCloud build version VB")]
        [DataRow("8.9", "vbnet", /* not set */ null)]
        [DataRow("8.9", "vbnet", "false")]
        [DataRow("9.0", "vbnet", "FALSE")]
        [DataRow("10.0", "vbnet", "UnexpectedParamValue")]
        public void ConfigExists_ForTestProject_SonarAnalyzersAndConfigurationMergedWithUserProvided(string sonarQubeVersion, string language, string excludeTestProject)
        {
            // Arrange and Act
            var executedTask = Execute_ConfigExists(sonarQubeVersion, language, true, excludeTestProject);

            // Assert
            executedTask.RuleSetFilePath.Should().Be($@"c:\{language}-normal.ruleset");
            executedTask.AnalyzerFilePaths.Should().BeEquivalentTo(@"c:\wintellect1.dll", @"c:\Google.Protobuf.dll", $@"c:\sonar.{language}.dll", @"c:\analyzer1.should.be.preserved.dll", @"c:\analyzer2.should.be.preserved.dll");
            // This TestProject is not excluded => additional file "original.should.be.removed.for.excluded.test.txt" should be preserved
            executedTask.AdditionalFilePaths.Should().BeEquivalentTo($@"c:\add1.{language}.txt", @"d:\replaced1.txt", "original.should.be.removed.for.excluded.test.txt");
        }

        [DataTestMethod]
        [DataRow("8.0.0.18955", "cs", "true", DisplayName = "SonarCloud build version - needs exclusion parameter CS")]
        [DataRow("8.9", "cs", "true", DisplayName = "SQ 8.9 - needs exclusion parameter CS")]
        [DataRow("9.0", "cs", "TRUE", DisplayName = "SQ 9.0 - needs exclusion parameter CS")]
        [DataRow("10.0", "cs", "tRUE", DisplayName = "SQ 10.0 - needs exclusion parameter CS")]
        [DataRow("8.0.0.18955", "vbnet", "true", DisplayName = "SonarCloud build version - needs exclusion parameter CS")]
        [DataRow("8.9", "vbnet", "true", DisplayName = "SQ 8.9 - needs exclusion parameter VB")]
        public void ConfigExists_ForTestProject_WhenExcluded_DeactivatedSonarAnalyzerSettingsUsed(string sonarQubeVersion, string language, string excludeTestProject)
        {
            // Arrange and Act
            var executedTask = Execute_ConfigExists(sonarQubeVersion, language, true, excludeTestProject);

            // Assert
            executedTask.RuleSetFilePath.Should().Be($@"c:\{language}-deactivated.ruleset");
            executedTask.AnalyzerFilePaths.Should().BeEquivalentTo($@"c:\sonar.{language}.dll", @"c:\Google.Protobuf.dll");
            executedTask.AdditionalFilePaths.Should().BeEquivalentTo($@"c:\add1.{language}.txt", @"d:\replaced1.txt");
        }

        [TestMethod]
        public void ConfigExists_ForTestProject_WhenUnknownLanguage_SonarAnalyzersAndConfigurationUsed()
        {
            // Arrange and Act
            var executedTask = Execute_ConfigExists("7.4", "unknownLang", true, null);

            // Assert
            executedTask.RuleSetFilePath.Should().BeNull();
            executedTask.AnalyzerFilePaths.Should().BeNull();
            executedTask.AdditionalFilePaths.Should().BeEquivalentTo("original.should.be.removed.for.excluded.test.txt", "original.should.be.preserved\\replaced1.txt");
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
        public void ShouldMerge_NewServerVersion_ReturnsTrue(string language) =>
            CheckShouldMerge("7.4.1", language, ignoreExternalIssues: "true", expected: true);

        [DataTestMethod]
        [DataRow("cs")]
        [DataRow("vbnet")]
        public void ShouldMerge_NewServerVersion_InvalidSetting_NoError_ReturnsTrue(string language)
        {
            var logger = CheckShouldMerge("7.4", language, ignoreExternalIssues: "not a boolean value", expected: true);
            logger.AssertNoWarningsLogged();
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

        private GetAnalyzerSettings Execute_ConfigExists(string sonarQubeVersion, string language, bool isTestProject, string excludeTestProject)
        {
            // Want to test the behaviour with old and new SQ version. Expecting the same results in each case.
            // Arrange
            var config = new AnalysisConfig
            {
                SonarQubeVersion = sonarQubeVersion,
                SonarQubeHostUrl = "http://localhost:9000", // If any SQ 8.0 version is passed (other than 8.0.0.29455), this will be classified as SonarCloud
                ServerSettings = new AnalysisProperties
                {
                    // Server settings should be ignored. "true" value should break existing tests.
                    new("sonar.cs.roslyn.ignoreIssues", "true"),
                    new("sonar.vbnet.roslyn.ignoreIssues", "true"),
                    // Server settings should be ignored - it should never come from the server
                    new("sonar.dotnet.excludeTestProjects", "true")
                },
                LocalSettings = excludeTestProject == null
                    ? null
                    : new AnalysisProperties { new("sonar.dotnet.excludeTestProjects", excludeTestProject) },
                AnalyzersSettings = new List<AnalyzerSettings>
                {
                    new AnalyzerSettings
                    {
                        Language = "cs",
                        RulesetPath = @"c:\cs-normal.ruleset",
                        DeactivatedRulesetPath = @"c:\cs-deactivated.ruleset",
                        AnalyzerPlugins = new List<AnalyzerPlugin>
                        {
                            new AnalyzerPlugin("roslyn.wintellect", "2.0", "dummy resource", new[] { @"c:\wintellect1.dll", @"c:\wintellect\bar.ps1", @"c:\Google.Protobuf.dll" }),
                            new AnalyzerPlugin("csharp", "1.1", "dummy resource2", new[] { @"c:\sonar.cs.dll", @"c:\foo.ps1", @"c:\Google.Protobuf.dll" }),
                        },
                        AdditionalFilePaths = new List<string> { @"c:\add1.cs.txt", @"d:\replaced1.txt" }
                    },
                    new AnalyzerSettings
                    {
                        Language = "vbnet",
                        RulesetPath = @"c:\vbnet-normal.ruleset",
                        DeactivatedRulesetPath = @"c:\vbnet-deactivated.ruleset",
                        AnalyzerPlugins = new List<AnalyzerPlugin>
                        {
                            new AnalyzerPlugin("roslyn.wintellect", "2.0", "dummy resource", new[] { @"c:\wintellect1.dll", @"c:\wintellect\bar.ps1", @"c:\Google.Protobuf.dll" }),
                            new AnalyzerPlugin("vbnet", "1.1", "dummy resource2", new[] { @"c:\sonar.vbnet.dll", @"c:\foo.ps1", @"c:\Google.Protobuf.dll" }),
                        },
                        AdditionalFilePaths = new List<string> { @"c:\add1.vbnet.txt", @"d:\replaced1.txt" }
                    },
                    new AnalyzerSettings // Settings for a different language
                    {
                        Language = "cobol",
                        RulesetPath = @"c:\cobol-normal.ruleset",
                        DeactivatedRulesetPath = @"c:\cobol-deactivated.ruleset",
                        AnalyzerPlugins = new List<AnalyzerPlugin>
                        {
                            new AnalyzerPlugin("cobol.analyzer", "1.0", "dummy resource", new[] { @"c:\cobol1.dll", @"c:\cobol2.dll" })
                        },
                        AdditionalFilePaths = new List<string> { @"c:\cobol.\add1.txt", @"d:\cobol\add2.txt" }
                    }
                }
            };

            var testSubject = CreateConfiguredTestSubject(config, language, TestContext);
            testSubject.IsTestProject = isTestProject;
            testSubject.OriginalAnalyzers = new[]
            {
                 "c:\\analyzer1.should.be.preserved.dll",
                 "c:\\analyzer2.should.be.preserved.dll",
                 "c:\\Google.Protobuf.dll", // same name as an assembly in the csharp plugin (above)
            };
            testSubject.OriginalAdditionalFiles = new[]
            {
                isTestProject ? "original.should.be.removed.for.excluded.test.txt" : "original.should.be.preserved.for.product.txt",
                "original.should.be.preserved\\replaced1.txt",
            };

            // Act
            ExecuteAndCheckSuccess(testSubject);
            return testSubject;
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
                config.ServerSettings = new AnalysisProperties { new($"sonar.{language}.roslyn.ignoreIssues", ignoreExternalIssues) };
            }

            var result = GetAnalyzerSettings.ShouldMergeAnalysisSettings(language, config, logger);

            result.Should().Be(expected);
            return logger;
        }

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
                ServerSettings = new AnalysisProperties { new($"sonar.{language}.roslyn.ignoreIssues", "false") },
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
