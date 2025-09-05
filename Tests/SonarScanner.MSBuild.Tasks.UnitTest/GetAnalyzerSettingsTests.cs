/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

using static TestUtilities.TestUtils;
using MSCA = Microsoft.CodeAnalysis;

namespace SonarScanner.MSBuild.Tasks.UnitTest;

[TestClass]
public class GetAnalyzerSettingsTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void MissingConfigDir_NoError()
    {
        var testSubject = new GetAnalyzerSettings
        {
            AnalysisConfigDir = "c:\\missing"
        };
        ExecuteAndCheckSuccess(testSubject);
        CheckNoAnalyzerSettings(testSubject);
    }

    [TestMethod]
    public void MissingConfigFile_NoError()
    {
        var testSubject = new GetAnalyzerSettings
        {
            AnalysisConfigDir = CreateTestSpecificFolderWithSubPaths(TestContext)
        };
        ExecuteAndCheckSuccess(testSubject);
        CheckNoAnalyzerSettings(testSubject);
    }

    [TestMethod]
    public void ConfigExistsButNoAnalyzerSettings_NoError()
    {
        var testSubject = CreateConfiguredTestSubject(new AnalysisConfig(), "anyLanguage", TestContext);
        ExecuteAndCheckSuccess(testSubject);
        CheckNoAnalyzerSettings(testSubject);
    }

    [TestMethod]
    [DataRow("7.3", DisplayName = "Legacy")]
    [DataRow("7.4")]
    public void ConfigExists_NoLanguage_SettingsOverwritten(string sonarQubeVersion)
    {
        var config = new AnalysisConfig
        {
            SonarQubeVersion = sonarQubeVersion,
            AnalyzersSettings =
            [
                new AnalyzerSettings
                {
                    Language = "cs",
                    RulesetPath = "f:\\yyy.ruleset",
                    AnalyzerPlugins = [CreateAnalyzerPlugin(Path.Combine(DriveRoot(), "local_analyzer.dll"))],
                    AdditionalFilePaths = [Path.Combine(DriveRoot(), "add1.txt"), Path.Combine(DriveRoot("d"), "add2.txt"), Path.Combine(DriveRoot("e"), "subdir", "add3.txt")]
                }
            ]
        };
        var testSubject = CreateConfiguredTestSubject(config, string.Empty, TestContext);
        testSubject.OriginalAdditionalFiles =
        [
            "original.should.be.preserved.txt"
        ];

        ExecuteAndCheckSuccess(testSubject);

        testSubject.RuleSetFilePath.Should().BeNull();
        testSubject.AnalyzerFilePaths.Should().BeNull();
        testSubject.AdditionalFilePaths.Should().BeEquivalentTo("original.should.be.preserved.txt");
    }

    // SONARMSBRU-216: non-assembly files should be filtered out
    [TestMethod]
    public void ConfigExists_Legacy_SettingsOverwritten()
    {
        var filesInConfig = new List<AnalyzerPlugin>
        {
            CreateAnalyzerPlugin(Path.Combine(DriveRoot(), "analyzer1.dll")),
            CreateAnalyzerPlugin(
                Path.Combine(DriveRoot(), "not_an_assembly.exe"),
                Path.Combine(DriveRoot(), "not_an_assembly.zip"),
                Path.Combine(DriveRoot(), "not_an_assembly.txt"),
                Path.Combine(DriveRoot("d"), "analyzer2.dll")),
            CreateAnalyzerPlugin(
                Path.Combine(DriveRoot(), "not_an_assembly.dll.foo"),
                Path.Combine(DriveRoot(), "not_an_assembly.winmd")),
            CreateAnalyzerPlugin(Path.Combine(DriveRoot("e"), "analyzer3.dll"))
        };
        var config = new AnalysisConfig
        {
            SonarQubeHostUrl = "http://sonarqube.com",
            SonarQubeVersion = "7.3",
            ServerSettings =
            [
                // Setting should be ignored
                new("sonar.cs.roslyn.ignoreIssues", "true")
            ],
            AnalyzersSettings =
            [
                new AnalyzerSettings
                {
                    Language = "cs",
                    RulesetPath = Path.Combine(DriveRoot("f"), "yyy.ruleset"),
                    AnalyzerPlugins = filesInConfig,
                    AdditionalFilePaths = [Path.Combine(DriveRoot(), "add1.txt"), Path.Combine(DriveRoot("d"), "add2.txt"), Path.Combine(DriveRoot("e"), "subdir", "add3.txt")]
                },

                new AnalyzerSettings
                {
                    Language = "cobol",
                    RulesetPath = Path.Combine(DriveRoot("f"), "xxx.ruleset"),
                    AnalyzerPlugins = filesInConfig,
                    AdditionalFilePaths = [Path.Combine(DriveRoot("e"), "cobol", "add1.txt"), Path.Combine(DriveRoot("d"), "cobol", "add2.txt")]
                }
            ]
        };
        var testSubject = CreateConfiguredTestSubject(config, "cs", TestContext);
        testSubject.OriginalAdditionalFiles =
        [
            "original.should.be.preserved.txt",
            Path.Combine("original.should.be.removed", "add2.txt"),
            Path.Combine(DriveRoot("e"), "foo", "should.be.removed", "add3.txt")
        ];

        ExecuteAndCheckSuccess(testSubject);

        testSubject.RuleSetFilePath.Should().Be(Path.Combine(DriveRoot("f"), "yyy.ruleset"));
        testSubject.AnalyzerFilePaths.Should().BeEquivalentTo(Path.Combine(DriveRoot(), "analyzer1.dll"), Path.Combine(DriveRoot("d"), "analyzer2.dll"), Path.Combine(DriveRoot("e"), "analyzer3.dll"));
        testSubject.AdditionalFilePaths.Should().BeEquivalentTo(
            Path.Combine(DriveRoot(), "add1.txt"),
            Path.Combine(DriveRoot("d"), "add2.txt"),
            Path.Combine(DriveRoot("e"), "subdir", "add3.txt"),
            "original.should.be.preserved.txt");
    }

    // Expecting both the additional files and the analyzers to be merged
    [TestMethod]
    public void ConfigExists_SettingsMerged()
    {
        var filesInConfig = new List<AnalyzerPlugin>
        {
            new()
            {
                AssemblyPaths =
                [
                    Path.Combine(DriveRoot(), "config", "analyzer1.DLL"),
                    Path.Combine(DriveRoot(), "not_an_assembly.exe"),
                    Path.Combine(DriveRoot(), "not_an_assembly.zip"),
                ]
            },
            new()
            {
                AssemblyPaths =
                [
                    Path.Combine(DriveRoot(), "config", "analyzer2.dll"),
                    Path.Combine(DriveRoot(), "not_an_assembly.txt"),
                    Path.Combine(DriveRoot(), "not_an_assembly.dll.foo"),
                    Path.Combine(DriveRoot(), "not_an_assembly.winmd")
                ]
            }
        };

        var config = new AnalysisConfig
        {
            SonarQubeVersion = "7.4",
            ServerSettings = [new("sonar.cs.roslyn.ignoreIssues", "false")],
            AnalyzersSettings =
            [
                new AnalyzerSettings
                {
                    Language = "cs",
                    RulesetPath = Path.Combine(DriveRoot("f"), "yyy.ruleset"),
                    AnalyzerPlugins = filesInConfig,
                    AdditionalFilePaths = [Path.Combine(DriveRoot(), "config", "add1.txt"), Path.Combine(DriveRoot("d"), "config", "add2.txt")]
                },

                new AnalyzerSettings
                {
                    Language = "cobol",
                    RulesetPath = Path.Combine(DriveRoot("f"), "xxx.ruleset"),
                    AnalyzerPlugins = [],
                    AdditionalFilePaths = [Path.Combine(DriveRoot(), "cobol.", "add1.txt"), Path.Combine(DriveRoot("d"), "cobol", "add2.txt")]
                }
            ]
        };
        var testSubject = CreateConfiguredTestSubject(config, "cs", TestContext);
        testSubject.OriginalAnalyzers =
        [
            Path.Combine(DriveRoot(), "original.should.be.preserved", "analyzer1.DLL"),
            Path.Combine(DriveRoot("f"), "original.should.be.preserved", "analyzer3.dll"),
            Path.Combine(DriveRoot(), "SonarAnalyzer", "should.be.preserved.SomeAnalyzer.dll"),
            Path.Combine(DriveRoot(), "should.be.removed", "SonarAnalyzer.Fake.DLL"), // We consider all analyzers starting with 'SonarAnalyzer' as ours, this will be removed as a duplicate reference
            Path.Combine(DriveRoot(), "should.be.removed", "SonarAnalyzer.CFG.dll"),
            Path.Combine(DriveRoot(), "should.be.removed", "SonarAnalyzer.dll"),
            Path.Combine(DriveRoot(), "should.be.removed", "SonarAnalyzer.CSharp.dll"),
            Path.Combine(DriveRoot(), "should.be.removed", "SonarAnalyzer.vIsUaLbAsIc.dll"),
            Path.Combine(DriveRoot(), "should.be.removed", "sOnAranaLYZer.Security.dll")
        ];
        testSubject.OriginalAdditionalFiles =
        [
            "original.should.be.preserved.txt",
            Path.Combine("original.should.be.removed", "add2.txt")
        ];

        ExecuteAndCheckSuccess(testSubject);

        testSubject.RuleSetFilePath.Should().Be(Path.Combine(DriveRoot("f"), "yyy.ruleset"));
        testSubject.AnalyzerFilePaths.Should().BeEquivalentTo(
            Path.Combine(DriveRoot(), "config", "analyzer1.DLL"),
            Path.Combine(DriveRoot(), "config", "analyzer2.dll"),
            Path.Combine(DriveRoot(), "original.should.be.preserved", "analyzer1.DLL"),
            Path.Combine(DriveRoot("f"), "original.should.be.preserved", "analyzer3.dll"),
            Path.Combine(DriveRoot(), "SonarAnalyzer", "should.be.preserved.SomeAnalyzer.dll"));
        testSubject.AdditionalFilePaths.Should().BeEquivalentTo(
            Path.Combine(DriveRoot(), "config", "add1.txt"),
            Path.Combine(DriveRoot("d"), "config", "add2.txt"),
            "original.should.be.preserved.txt");
    }

    [TestMethod]
    [DataRow("7.3", "cs", null, "wintellect1", "Google.Protobuf", DisplayName = "Legacy CS - Sonar Config used")]
    [DataRow("7.3", "vbnet", null, "wintellect1", "Google.Protobuf", DisplayName = "Legacy VB - Sonar Config used")]
    [DataRow("7.4", "cs", null, "wintellect1", "analyzer1.should.be.preserved", "analyzer2.should.be.preserved", DisplayName = "CS - Merged with user provided")]
    [DataRow("7.4", "vbnet", null, "wintellect1", "analyzer1.should.be.preserved", "analyzer2.should.be.preserved", DisplayName = "VB - Merged with user provided")]
    public void ConfigExists_ForProductProject(string sonarQubeVersion, string language, string excludeTestProject, params string[] additionalDlls)
    {
        var alwaysPresentAnalyzers = new[] { $"sonar.{language}", "Google.Protobuf" };
        var expectedAnalyzers = alwaysPresentAnalyzers.Concat(additionalDlls).Select(x => Path.Combine(DriveRoot(), $"{x}.dll"));

        var sut = Execute_ConfigExists(sonarQubeVersion, language, false, null);

        sut.RuleSetFilePath.Should().Be(Path.Combine(DriveRoot(), $"{language}-normal.ruleset"));
        sut.AnalyzerFilePaths.Should().BeEquivalentTo(expectedAnalyzers);
        sut.AdditionalFilePaths.Should().BeEquivalentTo(
            Path.Combine(DriveRoot(), $"add1.{language}.txt"),
            Path.Combine(DriveRoot("d"), "replaced1.txt"),
            "original.should.be.preserved.for.product.txt");
    }

    [TestMethod]
    [DataRow("8.0.0.18955", "cs", "true", DisplayName = "SonarCloud build version - needs exclusion parameter CS")]
    [DataRow("8.9", "cs", "true", DisplayName = "SQ 8.9 - needs exclusion parameter CS")]
    [DataRow("9.0", "cs", "TRUE", DisplayName = "SQ 9.0 - needs exclusion parameter CS")]
    [DataRow("10.0", "cs", "tRUE", DisplayName = "SQ 10.0 - needs exclusion parameter CS")]
    [DataRow("8.0.0.18955", "vbnet", "true", DisplayName = "SonarCloud build version - needs exclusion parameter CS")]
    [DataRow("8.9", "vbnet", "true", DisplayName = "SQ 8.9 - needs exclusion parameter VB")]
    public void ConfigExists_ForTestProject_WhenExcluded_DeactivatedSonarAnalyzerSettingsUsed(string sonarQubeVersion, string language, string excludeTestProject)
    {
        var executedTask = Execute_ConfigExists(sonarQubeVersion, language, true, excludeTestProject);

        executedTask.RuleSetFilePath.Should().Be(Path.Combine(DriveRoot(), $"{language}-deactivated.ruleset"));
        executedTask.AnalyzerFilePaths.Should().BeEquivalentTo(Path.Combine(DriveRoot(), $"sonar.{language}.dll"), Path.Combine(DriveRoot(), "Google.Protobuf.dll"));
        executedTask.AdditionalFilePaths.Should().BeEquivalentTo(Path.Combine(DriveRoot(), $"add1.{language}.txt"), Path.Combine(DriveRoot("d"), "replaced1.txt"));
    }

    [TestMethod]
    [DataRow("8.0.0.18955", "cs", null, DisplayName = "SonarCloud build version CS")]
    [DataRow("8.9", "cs", null)]
    [DataRow("8.9", "cs", "false")]
    [DataRow("9.0", "cs", "FALSE")]
    [DataRow("10.0", "cs", "UnexpectedParamValue")]
    [DataRow("8.0.0.18955", "vbnet", null, DisplayName = "SonarCloud build version VB")]
    [DataRow("8.9", "vbnet", null)]
    [DataRow("8.9", "vbnet", "false")]
    [DataRow("9.0", "vbnet", "FALSE")]
    [DataRow("10.0", "vbnet", "UnexpectedParamValue")]
    public void ConfigExists_ForTestProject_SonarAnalyzersAndConfigurationMergedWithUserProvided(string sonarQubeVersion, string language, string excludeTestProject)
    {
        var executedTask = Execute_ConfigExists(sonarQubeVersion, language, true, excludeTestProject);

        executedTask.RuleSetFilePath.Should().Be(Path.Combine(DriveRoot(), $"{language}-normal.ruleset"));
        executedTask.AnalyzerFilePaths.Should().BeEquivalentTo(
            Path.Combine(DriveRoot(), "wintellect1.dll"),
            Path.Combine(DriveRoot(), "Google.Protobuf.dll"),
            Path.Combine(DriveRoot(), $"sonar.{language}.dll"),
            Path.Combine(DriveRoot(), "analyzer1.should.be.preserved.dll"),
            Path.Combine(DriveRoot(), "analyzer2.should.be.preserved.dll"));
        // This TestProject is not excluded => additional file "original.should.be.removed.for.excluded.test.txt" should be preserved
        executedTask.AdditionalFilePaths.Should().BeEquivalentTo(
            Path.Combine(DriveRoot(), $"add1.{language}.txt"),
            Path.Combine(DriveRoot("d"), "replaced1.txt"),
            "original.should.be.removed.for.excluded.test.txt");
    }

    [TestMethod]
    public void ConfigExists_ForTestProject_WhenUnknownLanguage_SonarAnalyzersAndConfigurationUsed()
    {
        var executedTask = Execute_ConfigExists("7.4", "unknownLang", true, null);

        executedTask.RuleSetFilePath.Should().BeNull();
        executedTask.AnalyzerFilePaths.Should().BeNull();
        executedTask.AdditionalFilePaths.Should().BeEquivalentTo("original.should.be.removed.for.excluded.test.txt", Path.Combine("original.should.be.preserved", "replaced1.txt"));
    }

    // The "importAllValue" setting should be ignored for old server versions
    [TestMethod]
    [DataRow("cs")]
    [DataRow("vbnet")]
    public void ShouldMerge_OldServerVersion_ReturnsFalse(string language) =>
        CheckShouldMerge("7.3.1", language, ignoreExternalIssues: "true", expected: false)
            .Should().HaveInfos("External issues are not supported on this version of SonarQube. Version 7.4+ is required.");

    [TestMethod]
    [DataRow("cs")]
    [DataRow("vbnet")]
    public void ShouldMerge_NewServerVersion_ReturnsTrue(string language) =>
        CheckShouldMerge("7.4.1", language, ignoreExternalIssues: "true", expected: true);

    [TestMethod]
    [DataRow("cs")]
    [DataRow("vbnet")]
    public void ShouldMerge_NewServerVersion_InvalidSetting_NoError_ReturnsTrue(string language) =>
        CheckShouldMerge("7.4", language, ignoreExternalIssues: "not a boolean value", expected: true)
            .Should().HaveNoWarnings();

    [TestMethod]
    public void MergeRulesets_NoOriginalRuleset_FirstGeneratedRulsetUsed()
    {
        var config = new AnalysisConfig
        {
            SonarQubeVersion = "7.4",
            AnalyzersSettings =
            [
                new AnalyzerSettings
                {
                    Language = "xxx",
                    RulesetPath = "firstGeneratedRuleset.txt"
                }
            ]
        };
        var testSubject = CreateConfiguredTestSubject(config, "xxx", TestContext);
        testSubject.OriginalRulesetFilePath = null;

        ExecuteAndCheckSuccess(testSubject);

        testSubject.RuleSetFilePath.Should().Be("firstGeneratedRuleset.txt");
    }

    [TestMethod]
    [DataRow(@".\..\originalRuleset.txt", false, DisplayName = "Relative path")]
    [DataRow(@"solution.folder\originalRuleset.txt", true, DisplayName = "Absolute path")]
    public void MergeRulesets_OriginalRulesetSpecified_RelativePath_SecondGeneratedRulesetUsed(string originalRulesetFilePath, bool absolutePath)
    {
        var rootDir = DriveRoot();
        if (absolutePath)
        {
            originalRulesetFilePath = Path.Combine(rootDir, originalRulesetFilePath);
        }
        var dir = CreateTestSpecificFolderWithSubPaths(TestContext);
        var dummyQpRulesetPath = CreateValidEmptyRuleset(dir, "dummyQp");
        var config = CreateMergingAnalysisConfig("xxx", dummyQpRulesetPath);

        var testSubject = CreateConfiguredTestSubject(config, "xxx", TestContext);
        testSubject.CurrentProjectDirectoryPath = Path.Combine(rootDir, "solution.folder", "project.folder");
        testSubject.OriginalRulesetFilePath = originalRulesetFilePath.Replace('\\', Path.DirectorySeparatorChar);
        testSubject.ProjectSpecificConfigDirectory = testSubject.AnalysisConfigDir;

        ExecuteAndCheckSuccess(testSubject);

        CheckMergedRulesetFile(testSubject, Path.Combine(rootDir, "solution.folder", "originalRuleset.txt"));
    }

    [TestMethod]
    // Regression test for #581: Sonar issues are reported as external issues
    // https://github.com/SonarSource/sonar-scanner-msbuild/issues/581
    // Off in QP, on locally -> off
    public void MergeRuleset_CheckQPSettingsWin()
    {
        var qpRulesetPath = CreateRuleset("qpRuleset", """
            <?xml version='1.0' encoding='utf-8'?>
            <RuleSet xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' Name='x' Description='x' ToolsVersion='14.0'>
              <Rules AnalyzerId='analyzer1' RuleNamespace='ns1'>
                <Rule Id='SharedRuleOffInQP' Action='None' />
                <Rule Id='SharedRuleOffInLocal' Action='Warning' />
                <Rule Id='QPOnlyRule' Action='Info' />
              </Rules>
            </RuleSet>
            """);
        var localRulesetPath = CreateRuleset("localRuleset", """
            <?xml version='1.0' encoding='utf-8'?>
            <RuleSet xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' Name='x' Description='x' ToolsVersion='14.0'>
              <Rules AnalyzerId='analyzer1' RuleNamespace='ns1'>
                <Rule Id='SharedRuleOffInQP' Action='Error' />
                <Rule Id='SharedRuleOffInLocal' Action='None' />
                <Rule Id='LocalOnlyRule' Action='Error' />
              </Rules>
            </RuleSet>
            """);
        var config = CreateMergingAnalysisConfig("xxx", qpRulesetPath);
        var testSubject = CreateConfiguredTestSubject(config, "xxx", TestContext);
        testSubject.OriginalRulesetFilePath = localRulesetPath;
        testSubject.ProjectSpecificConfigDirectory = testSubject.AnalysisConfigDir;

        ExecuteAndCheckSuccess(testSubject);

        CheckMergedRulesetFile(testSubject, localRulesetPath);
        var actualRuleset = MSCA.RuleSet.LoadEffectiveRuleSetFromFile(testSubject.RuleSetFilePath);
        CheckExpectedDiagnosticLevel(actualRuleset, "SharedRuleOffInQP", MSCA.ReportDiagnostic.Suppress);
        CheckExpectedDiagnosticLevel(actualRuleset, "SharedRuleOffInLocal", MSCA.ReportDiagnostic.Warn);
        CheckExpectedDiagnosticLevel(actualRuleset, "QPOnlyRule", MSCA.ReportDiagnostic.Info);
        CheckExpectedDiagnosticLevel(actualRuleset, "LocalOnlyRule", MSCA.ReportDiagnostic.Error);
    }

    private GetAnalyzerSettings Execute_ConfigExists(string sonarQubeVersion, string language, bool isTestProject, string excludeTestProject)
    {
        // Want to test the behaviour with old and new SQ version. Expecting the same results in each case.
        var config = new AnalysisConfig
        {
            SonarQubeVersion = sonarQubeVersion,
            SonarQubeHostUrl = "http://localhost:9000", // If any SQ 8.0 version is passed (other than 8.0.0.29455), this will be classified as SonarCloud
            ServerSettings =
            [
                // Server settings should be ignored. "true" value should break existing tests.
                new("sonar.cs.roslyn.ignoreIssues", "true"),
                new("sonar.vbnet.roslyn.ignoreIssues", "true"),
                // Server settings should be ignored - it should never come from the server
                new("sonar.dotnet.excludeTestProjects", "true")
            ],
            LocalSettings = excludeTestProject is null
                ? null
                : [new("sonar.dotnet.excludeTestProjects", excludeTestProject)],
            AnalyzersSettings =
            [
                new AnalyzerSettings
                {
                    Language = "cs",
                    RulesetPath = Path.Combine(DriveRoot(), "cs-normal.ruleset"),
                    DeactivatedRulesetPath = Path.Combine(DriveRoot(), "cs-deactivated.ruleset"),
                    AnalyzerPlugins =
                    [
                        new("roslyn.wintellect", "2.0", "dummy resource", [Path.Combine(DriveRoot(), "wintellect1.dll"), @"c:\wintellect\bar.ps1", Path.Combine(DriveRoot(), "Google.Protobuf.dll")]),
                        new("csharp", "1.1", "dummy resource2", [Path.Combine(DriveRoot(), "sonar.cs.dll"), @"c:\foo.ps1", Path.Combine(DriveRoot(), "Google.Protobuf.dll")]),
                    ],
                    AdditionalFilePaths = [Path.Combine(DriveRoot(), "add1.cs.txt"), Path.Combine(DriveRoot("d"), "replaced1.txt")]
                },
                new AnalyzerSettings
                {
                    Language = "vbnet",
                    RulesetPath = Path.Combine(DriveRoot(), "vbnet-normal.ruleset"),
                    DeactivatedRulesetPath = Path.Combine(DriveRoot(), "vbnet-deactivated.ruleset"),
                    AnalyzerPlugins =
                    [
                        new("roslyn.wintellect", "2.0", "dummy resource", [Path.Combine(DriveRoot(), "wintellect1.dll"), @"c:\wintellect\bar.ps1", Path.Combine(DriveRoot(), "Google.Protobuf.dll")]),
                        new("vbnet", "1.1", "dummy resource2", [Path.Combine(DriveRoot(), "sonar.vbnet.dll"), @"c:\foo.ps1", Path.Combine(DriveRoot(), "Google.Protobuf.dll")]),
                    ],
                    AdditionalFilePaths = [Path.Combine(DriveRoot(), "add1.vbnet.txt"), Path.Combine(DriveRoot("d"), "replaced1.txt")]
                },
                new AnalyzerSettings // Settings for a different language
                {
                    Language = "cobol",
                    RulesetPath = @"c:\cobol-normal.ruleset",
                    DeactivatedRulesetPath = @"c:\cobol-deactivated.ruleset",
                    AnalyzerPlugins =
                    [
                        new AnalyzerPlugin("cobol.analyzer", "1.0", "dummy resource", [@"c:\cobol1.dll", @"c:\cobol2.dll"])
                    ],
                    AdditionalFilePaths = [Path.Combine(DriveRoot(), "cobol.", "add1.txt"), Path.Combine(DriveRoot("d"), "cobol", "add2.txt")]
                }
            ]
        };
        var testSubject = CreateConfiguredTestSubject(config, language, TestContext);
        testSubject.IsTestProject = isTestProject;
        testSubject.OriginalAnalyzers =
        [
            Path.Combine(DriveRoot(), "analyzer1.should.be.preserved.dll"),
            Path.Combine(DriveRoot(), "analyzer2.should.be.preserved.dll"),
            Path.Combine(DriveRoot(), "Google.Protobuf.dll"), // same name as an assembly in the csharp plugin (above)
        ];
        testSubject.OriginalAdditionalFiles =
        [
            isTestProject ? "original.should.be.removed.for.excluded.test.txt" : "original.should.be.preserved.for.product.txt",
            Path.Combine("original.should.be.preserved", "replaced1.txt")
        ];

        ExecuteAndCheckSuccess(testSubject);

        return testSubject;
    }

    // Should default to true i.e. don't override, merge
    private static TestLogger CheckShouldMerge(string serverVersion, string language, string ignoreExternalIssues, bool expected)
    {
        var logger = new TestLogger();
        var config = new AnalysisConfig
        {
            SonarQubeHostUrl = "http://sonarqube.com",
            SonarQubeVersion = serverVersion
        };
        if (ignoreExternalIssues is null)
        {
            config.ServerSettings = [new($"sonar.{language}.roslyn.ignoreIssues", ignoreExternalIssues)];
        }

        var result = GetAnalyzerSettings.ShouldMergeAnalysisSettings(language, config, logger);

        result.Should().Be(expected);
        return logger;
    }

    private static GetAnalyzerSettings CreateConfiguredTestSubject(AnalysisConfig config, string language, TestContext testContext)
    {
        var testDir = CreateTestSpecificFolderWithSubPaths(testContext);
        var testSubject = new GetAnalyzerSettings
        {
            Language = language,
            AnalysisConfigDir = testDir,
        };
        config.Save(Path.Combine(testDir, FileConstants.ConfigFileName));

        return testSubject;
    }

    private static AnalysisConfig CreateMergingAnalysisConfig(string language, string qpRulesetFilePath) =>
        new()
        {
            SonarQubeVersion = "7.4",
            ServerSettings = [new($"sonar.{language}.roslyn.ignoreIssues", "false")],
            AnalyzersSettings =
            [
                new AnalyzerSettings
                {
                    Language = language,
                    RulesetPath = qpRulesetFilePath
                }
            ]
        };

    private string CreateRuleset(string fileNameWithoutExtension, string content)
    {
        var dir = CreateTestSpecificFolderWithSubPaths(TestContext);
        var filePath = CreateTextFile(dir, fileNameWithoutExtension + ".ruleset", content);
        return filePath;
    }

    private static AnalyzerPlugin CreateAnalyzerPlugin(params string[] fileList) =>
        new() { AssemblyPaths = [.. fileList] };

    private static void ExecuteAndCheckSuccess(GetAnalyzerSettings analyzerSettings)
    {
        var dummyEngine = new DummyBuildEngine();
        analyzerSettings.BuildEngine = dummyEngine;

        var taskSucess = analyzerSettings.Execute();

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
        ruleset.SpecificDiagnosticOptions.Should().ContainKey(ruleId);
        ruleset.SpecificDiagnosticOptions[ruleId].Should().Be(expected);
    }
}
