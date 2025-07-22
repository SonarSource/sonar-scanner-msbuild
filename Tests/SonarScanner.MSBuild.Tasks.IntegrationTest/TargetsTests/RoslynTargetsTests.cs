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

using System.Runtime.InteropServices;

namespace SonarScanner.MSBuild.Tasks.IntegrationTest.TargetsTests;

[TestClass]
public class RoslynTargetsTests
{
    private const string RoslynAnalysisResultsSettingName = "sonar.cs.roslyn.reportFilePaths";
    private const string AnalyzerWorkDirectoryResultsSettingName = "sonar.cs.analyzer.projectOutPaths";

    private readonly string baseDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"c:\" : @"/tmp/";
    private readonly string otherDrive = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"d:\" : @"d:/";
    private readonly string defaultFolder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"c:\1\" : @"/tmp/1/";
    private readonly string someFolder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"x:\aaa\" : @"/x:/aaa/";

    public TestContext TestContext { get; set; }

    [DataTestMethod]
    [DataRow("C#", false, "false")]
    [DataRow("C#", false, "true")]
    [DataRow("C#", true, "false")]
    [DataRow("VB", false, "false")]
    [DataRow("VB", false, "true")]
    [DataRow("VB", true, "false")]
    public void Settings_ValidSetup_ForAnalyzedProject(string msBuildLanguage, bool isTestProject, string excludeTestProjects)
    {
        var context = new TargetsTestsContext(TestContext, msBuildLanguage);
        var dummyCSQpRulesetPath = TestUtils.CreateValidEmptyRuleset(context.ConfigFolder, "C#-dummyQp");
        var dummyVBQpRulesetPath = TestUtils.CreateValidEmptyRuleset(context.ConfigFolder, "VB-dummyQp");
        var result = Execute_Settings_ValidSetup(context, isTestProject, excludeTestProjects, dummyCSQpRulesetPath, dummyVBQpRulesetPath);
        var actualProjectSpecificConfFolder = result.GetPropertyValue(TargetProperties.ProjectSpecificConfDir);
        Directory.Exists(actualProjectSpecificConfFolder).Should().BeTrue();
        var expectedMergedRuleSetFilePath = Path.Combine(actualProjectSpecificConfFolder, "merged.ruleset");
        AssertExpectedResolvedRuleset(result, expectedMergedRuleSetFilePath);
        RuleSetAssertions.CheckMergedRulesetFile(actualProjectSpecificConfFolder, $"{baseDir}should.be.overridden.ruleset");

        // Expecting all analyzers from the config file, but none from the project file
        AssertExpectedAnalyzers(
            result,
            $@"{defaultFolder}SonarAnalyzer.{msBuildLanguage}.dll",
            $@"{defaultFolder}SonarAnalyzer.dll",
            $@"{defaultFolder}Google.Protobuf.dll",
            $@"{baseDir}external.analyzer.{msBuildLanguage}.dll",
            "project.additional.analyzer1.dll",
            $@"{baseDir}project.additional.analyzer2.dll");

        // Expecting additional files from both config and project file
        AssertExpectedAdditionalFiles(result, "project.additional.file.1.txt", $@"{someFolder}project.additional.file.2.txt");
    }

    [DataTestMethod]
    [DataRow("C#")]
    [DataRow("VB")]
    public void Settings_ValidSetup_ForExcludedTestProject(string msBuildLanguage)
    {
        var context = new TargetsTestsContext(TestContext, msBuildLanguage);
        var result = Execute_Settings_ValidSetup(context, true, "true", @"foo-cs.ruleset", @"foo-vb.ruleset");

        AssertExpectedResolvedRuleset(result, $@"{otherDrive}{msBuildLanguage}-deactivated.ruleset");

        // Expecting only the SonarC# analyzer
        AssertExpectedAnalyzers(
            result,
            $@"{defaultFolder}SonarAnalyzer.{msBuildLanguage}.dll",
            $@"{defaultFolder}SonarAnalyzer.dll",
            $@"{defaultFolder}Google.Protobuf.dll");

        // Expecting only the additional files from the config file
        AssertExpectedAdditionalFiles(result);
    }

    [TestMethod]
    [Description("Checks any existing analyzers are overridden for projects using SonarQube pre-7.5")]
    public void Settings_ValidSetup_LegacyServer_Override_Analyzers()
    {
        var context = new TargetsTestsContext(TestContext);
        var dataFolder = baseDir + "data" + Path.DirectorySeparatorChar;
        var config = new AnalysisConfig
        {
            SonarQubeHostUrl = "http://sonarqube.com",
            SonarQubeVersion = "6.7", // legacy version
            AnalyzersSettings =
            [
                new AnalyzerSettings
                {
                    Language = "cs",
                    RulesetPath = $@"{otherDrive}my.ruleset",
                    AnalyzerPlugins = [CreateAnalyzerPlugin($@"{dataFolder}new.analyzer1.dll"), CreateAnalyzerPlugin($@"{baseDir}new.analyzer2.dll")],
                    AdditionalFilePaths = [$@"{baseDir}config.1.txt", $@"{baseDir}config.2.txt"]
                }
            ]
        };

        var testSpecificProjectXml = $"""
              <PropertyGroup>
                <ResolvedCodeAnalysisRuleSet>{baseDir}should.be.overridden.ruleset</ResolvedCodeAnalysisRuleSet>
                <Language>C#</Language>
              </PropertyGroup>

              <ItemGroup>
                <!-- all analyzers specified in the project file should be removed -->
                <Analyzer Include='{baseDir}should.be.preserved.analyzer2.dll' />
                <Analyzer Include='should.be.preserved.analyzer1.dll' />
              </ItemGroup>
              <ItemGroup>
                <!-- These additional files don't match ones in the config and should be preserved -->
                <AdditionalFiles Include='should.be.preserved.additional1.txt' />
                <AdditionalFiles Include='should.be.preserved.additional2.txt' />

                <!-- This additional file matches one in the config and should be removed -->
                <AdditionalFiles Include='should.be.removed/CONFIG.1.TXT' />
                <AdditionalFiles Include='should.be.removed\CONFIG.2.TXT' />
              </ItemGroup>
            """;

        var filePath = context.CreateProjectFile(testSpecificProjectXml, config: config);

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarOverrideRunAnalyzers, TargetConstants.OverrideRoslynAnalysis);

        result.AssertTargetExecuted(TargetConstants.SonarCreateProjectSpecificDirs);
        result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
        result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisProperties);
        result.BuildSucceeded.Should().BeTrue();

        AssertErrorLogIsSetBySonarQubeTargets(result);
        AssertExpectedResolvedRuleset(result, $@"{otherDrive}my.ruleset");
        AssertExpectedAdditionalFiles(result, "should.be.preserved.additional1.txt", "should.be.preserved.additional2.txt");
        AssertExpectedAnalyzers(result, $@"{dataFolder}new.analyzer1.dll", $@"{baseDir}new.analyzer2.dll");
        AssertWarningsAreNotTreatedAsErrorsNorIgnored(result);
        AssertRunAnalyzersIsEnabled(result);
    }

    [TestMethod]
    [Description("Checks existing analysis settings are merged for projects using SonarQube 7.5+")]
    public void Settings_ValidSetup_NonLegacyServer_MergeSettings()
    {
        var context = new TargetsTestsContext(TestContext);
        var dummyQpRulesetPath = TestUtils.CreateValidEmptyRuleset(context.ProjectFolder, "dummyQp");
        var config = new AnalysisConfig
        {
            SonarQubeVersion = "7.5", // non-legacy version
            ServerSettings = [new("sonar.cs.roslyn.ignoreIssues", "false")],
            AnalyzersSettings =
            [
                new AnalyzerSettings
                {
                    Language = "cs",
                    RulesetPath = dummyQpRulesetPath,
                    AnalyzerPlugins = [CreateAnalyzerPlugin($@"{baseDir}data\new\analyzer1.dll", $@"{baseDir}new.analyzer2.dll")],
                    AdditionalFilePaths = [$@"{baseDir}config.1.txt", $@"{baseDir}config.2.txt"]
                }
            ]
        };

        var origDir = $"original{Path.DirectorySeparatorChar}";
        var preservedDir = $"should.be.preserved{Path.DirectorySeparatorChar}";
        var removedDir = $"{baseDir}should.be.removed{Path.DirectorySeparatorChar}";
        var testSpecificProjectXml = $"""
              <PropertyGroup>
                <ResolvedCodeAnalysisRuleSet>{baseDir}original.ruleset</ResolvedCodeAnalysisRuleSet>
              </PropertyGroup>

              <ItemGroup>
                <!-- all analyzers specified in the project file should be preserved -->
                <Analyzer Include='{baseDir}{origDir}{preservedDir}analyzer1.dll' />
                <Analyzer Include='{origDir}{preservedDir}analyzer3.dll' />
                <Analyzer Include='should.be.preserved.SonarAnalyzer.Fake.dll' />
                <Analyzer Include='{baseDir}SonarAnalyzer{Path.DirectorySeparatorChar}should.be.preserved.SomeAnalyzer.dll' />
                <Analyzer Include='{removedDir}SonarAnalyzer.CFG.dll' />
                <Analyzer Include='{removedDir}SonarAnalyzer.dll' />
                <Analyzer Include='{removedDir}SonarAnalyzer.CSharp.dll' />
                <Analyzer Include='{removedDir}SonarAnalyzer.vIsUaLbAsIc.dll' />
                <Analyzer Include='{removedDir}SonarAnalyzer.Security.dll' />
              </ItemGroup>
              <ItemGroup>
                <!-- These additional files don't match ones in the config and should be preserved -->
                <AdditionalFiles Include='should.be.preserved.additional1.txt' />
                <AdditionalFiles Include='should.be.preserved.additional2.txt' />

                <!-- This additional file matches one in the config and should be removed -->
                <AdditionalFiles Include='d:/duplicate.should.be.removed/CONFIG.1.TXT' />
                <AdditionalFiles Include='d:\duplicate.should.be.removed\config.2.TXT' />
              </ItemGroup>
            """;
        var filePath = context.CreateProjectFile(testSpecificProjectXml, config: config);

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarOverrideRunAnalyzers, TargetConstants.OverrideRoslynAnalysis);

        result.AssertTargetExecuted(TargetConstants.SonarCreateProjectSpecificDirs);
        result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
        result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisProperties);
        result.BuildSucceeded.Should().BeTrue();

        AssertErrorLogIsSetBySonarQubeTargets(result);
        var actualProjectSpecificConfFolder = result.GetPropertyValue(TargetProperties.ProjectSpecificConfDir);
        Directory.Exists(actualProjectSpecificConfFolder).Should().BeTrue();
        var expectedMergedRuleSetFilePath = Path.Combine(actualProjectSpecificConfFolder, "merged.ruleset");
        AssertExpectedResolvedRuleset(result, expectedMergedRuleSetFilePath);
        RuleSetAssertions.CheckMergedRulesetFile(actualProjectSpecificConfFolder, $@"{baseDir}original.ruleset");
        AssertExpectedAdditionalFiles(result, "should.be.preserved.additional1.txt", "should.be.preserved.additional2.txt");
        AssertExpectedAnalyzers(
            result,
            $@"{baseDir}data{Path.DirectorySeparatorChar}new{Path.DirectorySeparatorChar}analyzer1.dll",
            $@"{baseDir}new.analyzer2.dll",
            $@"{origDir}{preservedDir}analyzer3.dll",
            $@"{baseDir}{origDir}{preservedDir}analyzer1.dll",
            @"should.be.preserved.SonarAnalyzer.Fake.dll",
            $@"{baseDir}SonarAnalyzer{Path.DirectorySeparatorChar}should.be.preserved.SomeAnalyzer.dll");
        AssertWarningsAreNotTreatedAsErrorsNorIgnored(result);
        AssertRunAnalyzersIsEnabled(result);
    }

    [TestMethod]
    public void Settings_LanguageMissing_NoError()
    {
        var context = new TargetsTestsContext(TestContext);
        // Create a valid config file that does not contain analyzer settings
        var config = new AnalysisConfig();
        var configFilePath = Path.Combine(context.ConfigFolder, FileConstants.ConfigFileName);
        config.Save(configFilePath);
        var projectSnippet = $"""
            <PropertyGroup>
              <SonarQubeConfigPath>{context.ConfigFolder}</SonarQubeConfigPath>
              <SonarQubeOutputPath>{context.OutputFolder}</SonarQubeOutputPath>
              <ResolvedCodeAnalysisRuleset>{baseDir}should.be.overridden.ruleset</ResolvedCodeAnalysisRuleset>
              <Language />
            </PropertyGroup>

            <ItemGroup>
              <Analyzer Include='should.be.removed.analyzer1.dll' />
              <AdditionalFiles Include='should.be.preserved.additional1.txt' />
            </ItemGroup>
            """;
        var filePath = context.CreateProjectFile(projectSnippet, config: config);

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);

        result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
        result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisProperties);
        result.BuildSucceeded.Should().BeTrue();
        result.Messages.Should().Contain("Analysis language is not specified");

        AssertErrorLogIsSetBySonarQubeTargets(result);
        AssertExpectedResolvedRuleset(result, string.Empty);
        result.AssertItemGroupCount(TargetProperties.AnalyzerItemType, 0);
        AssertExpectedItemValuesExists(
            result,
            TargetProperties.AdditionalFilesItemType,
            new[]
            {
                result.GetPropertyValue(TargetProperties.SonarProjectOutFolderFilePath),
                result.GetPropertyValue(TargetProperties.SonarProjectConfigFilePath),
                "should.be.preserved.additional1.txt" /* additional files are not removed */
            });
    }

    [TestMethod]
    [Description("Checks that a config file with no analyzer settings does not cause an issue")]
    public void Settings_SettingsMissing_NoError()
    {
        var context = new TargetsTestsContext(TestContext);
        // Create a valid config file that does not contain analyzer settings
        var config = new AnalysisConfig();
        var configFilePath = Path.Combine(context.ConfigFolder, FileConstants.ConfigFileName);
        config.Save(configFilePath);
        var projectSnippet = $"""
            <PropertyGroup>
              <SonarQubeConfigPath>{context.ConfigFolder}</SonarQubeConfigPath>
              <SonarQubeOutputPath>{context.OutputFolder}</SonarQubeOutputPath>
              <ResolvedCodeAnalysisRuleset>{baseDir}should.be.overridden.ruleset</ResolvedCodeAnalysisRuleset>
            </PropertyGroup>

            <ItemGroup>
              <Analyzer Include='should.be.removed.analyzer1.dll' />
              <AdditionalFiles Include='should.be.preserved.additional1.txt' />
            </ItemGroup>
            """;
        var filePath = context.CreateProjectFile(projectSnippet, config: config);

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);

        result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
        result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisProperties);
        result.BuildSucceeded.Should().BeTrue();

        AssertErrorLogIsSetBySonarQubeTargets(result);
        AssertExpectedResolvedRuleset(result, string.Empty);
        result.AssertItemGroupCount(TargetProperties.AnalyzerItemType, 0);
        AssertExpectedItemValuesExists(
            result,
            TargetProperties.AdditionalFilesItemType,
            result.GetPropertyValue(TargetProperties.SonarProjectOutFolderFilePath),
            result.GetPropertyValue(TargetProperties.SonarProjectConfigFilePath),
            "should.be.preserved.additional1.txt");
    }

    [TestMethod]
    [Description("Checks the target is not executed if the temp folder is not set")]
    public void Settings_TempFolderIsNotSet()
    {
        var context = new TargetsTestsContext(TestContext);
        // When building with dotnet on linux vs msbuild on windows,
        // SYSLIB0011 is automatticaly added to WarningsAsErrors on linux, by dotnet
        // to avoid this we target netcore2.1, which does not have this warning
        var projectSnippet = """
            <PropertyGroup>
              <ErrorLog>pre-existing.log</ErrorLog>
              <ResolvedCodeAnalysisRuleset>pre-existing.ruleset</ResolvedCodeAnalysisRuleset>
              <WarningsAsErrors>CS101</WarningsAsErrors>
              <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              <RunAnalyzers>false</RunAnalyzers>
              <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
              <TargetFramework>netcore2.1</TargetFramework>
            
              <!-- This will override the value that was set earlier in the project file -->
              <SonarQubeTempPath />
            </PropertyGroup>
            """;
        var filePath = context.CreateProjectFile(projectSnippet);

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);

        result.AssertTargetNotExecuted(TargetConstants.OverrideRoslynAnalysis);
        result.AssertTargetNotExecuted(TargetConstants.SetRoslynAnalysisProperties);

        // Existing properties should not be changed
        AssertExpectedErrorLog(result, "pre-existing.log");
        AssertExpectedResolvedRuleset(result, "pre-existing.ruleset");
        result.AssertItemGroupCount(TargetProperties.AnalyzerItemType, 0);
        result.AssertItemGroupCount(TargetProperties.AdditionalFilesItemType, 0);

        // Properties are not overriden
        result.AssertPropertyValue(TargetProperties.TreatWarningsAsErrors, "true");
        result.AssertPropertyValue(TargetProperties.WarningsAsErrors, "CS101");
        result.AssertPropertyValue(TargetProperties.RunAnalyzers, "false");
        result.AssertPropertyValue(TargetProperties.RunAnalyzersDuringBuild, "false");
    }

    [TestMethod]
    [Description("Checks an existing errorLog value is used if set")]
    public void Settings_ErrorLogAlreadySet()
    {
        var context = new TargetsTestsContext(TestContext);
        var projectSnippet = """
            <PropertyGroup>
              <ErrorLog>already.set.txt</ErrorLog>
            </PropertyGroup>
            """;

        var filePath = context.CreateProjectFile(projectSnippet);

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);

        result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
        result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisProperties);
        result.BuildSucceeded.Should().BeTrue();
        result.AssertPropertyValue(TargetProperties.ErrorLog, "already.set.txt");
        result.AssertPropertyValue(TargetProperties.SonarErrorLog, "already.set.txt");
    }

    [TestMethod]
    [Description("Checks the code analysis properties are cleared for excludedprojects")]
    public void Settings_NotRunForExcludedProject()
    {
        var context = new TargetsTestsContext(TestContext);
        var projectSnippet = $"""
            <PropertyGroup>
              <SonarQubeExclude>TRUE</SonarQubeExclude>
              <ResolvedCodeAnalysisRuleset>Dummy value</ResolvedCodeAnalysisRuleset>
              <ErrorLog>{baseDir}UserDefined.json</ErrorLog>
              <RunAnalyzers>false</RunAnalyzers>
              <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
            </PropertyGroup>
            """;
        var filePath = context.CreateProjectFile(projectSnippet);

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);

        result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
        result.AssertTargetNotExecuted(TargetConstants.SetRoslynAnalysisProperties);
        result.BuildSucceeded.Should().BeTrue();
        result.AssertPropertyValue("ResolvedCodeAnalysisRuleset", "Dummy value");
        result.AssertPropertyValue(TargetProperties.RunAnalyzers, "false");             // We don't embed analyzers => we don't need to override this
        result.AssertPropertyValue(TargetProperties.RunAnalyzersDuringBuild, "false");
        result.AssertPropertyValue(TargetProperties.SonarErrorLog, null);
        result.AssertPropertyValue(TargetProperties.ErrorLog, $@"{baseDir}UserDefined.json");  // Do not override
    }

    [TestMethod]
    [Description("Checks the target is not executed if the temp folder is not set")]
    public void SetResults_TempFolderIsNotSet()
    {
        var context = new TargetsTestsContext(TestContext);
        var projectSnippet = """
            <PropertyGroup>
              <SonarQubeTempPath />
            </PropertyGroup>
            """;
        var filePath = context.CreateProjectFile(projectSnippet);

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.OverrideRoslynAnalysis);

        result.AssertTargetNotExecuted(TargetConstants.SonarWriteProjectData);
    }

    [TestMethod]
    [Description("Checks the analysis setting is not set if the results file does not exist")]
    public void SetResults_ResultsFileDoesNotExist()
    {
        var context = new TargetsTestsContext(TestContext);
        var projectSnippet = $"""
            <PropertyGroup>
              <ProjectSpecificOutDir>{context.OutputFolder}</ProjectSpecificOutDir>
              <SonarQubeTempPath>{context.InputFolder}</SonarQubeTempPath>
            </PropertyGroup>
            """;
        var filePath = context.CreateProjectFile(projectSnippet);

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.InvokeSonarWriteProjectData_NonRazorProject);

        result.AssertTargetExecuted(TargetConstants.InvokeSonarWriteProjectData_NonRazorProject);
        result.AssertTargetExecuted(TargetConstants.SonarWriteProjectData);
        AssertAnalysisSettingDoesNotExist(result, RoslynAnalysisResultsSettingName);
    }

    [TestMethod]
    [Description("Checks the analysis setting is set if the result file exists")]
    public void SetResults_ResultsFileExists()
    {
        var context = new TargetsTestsContext(TestContext);
        var resultsFile = TestUtils.CreateTextFile(context.InputFolder, "error.report.txt", "dummy report content");
        var projectSnippet = $"""
            <PropertyGroup>
              <SonarQubeTempPath>{context.InputFolder}</SonarQubeTempPath>
              <SonarErrorLog>{resultsFile}</SonarErrorLog>
            </PropertyGroup>
            """;
        var filePath = context.CreateProjectFile(projectSnippet);

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarCreateProjectSpecificDirs, TargetConstants.InvokeSonarWriteProjectData_NonRazorProject);

        var projectSpecificOutDir = result.GetPropertyValue(TargetProperties.ProjectSpecificOutDir);
        result.AssertTargetExecuted(TargetConstants.SonarCreateProjectSpecificDirs);
        result.AssertTargetExecuted(TargetConstants.InvokeSonarWriteProjectData_NonRazorProject);
        result.AssertTargetExecuted(TargetConstants.SonarWriteProjectData);
        AssertExpectedAnalysisSetting(result, RoslynAnalysisResultsSettingName, resultsFile);
        AssertExpectedAnalysisSetting(result, AnalyzerWorkDirectoryResultsSettingName, projectSpecificOutDir);
    }

    [TestMethod]
    [Description("Checks the targets are executed in the expected order")]
    public void TargetExecutionOrder()
    {
        var context = new TargetsTestsContext(TestContext);
        // We need to set the CodeAnalyisRuleSet property if we want ResolveCodeAnalysisRuleSet
        // to be executed. See test bug https://github.com/SonarSource/sonar-scanner-msbuild/issues/776
        var dummyQpRulesetPath = TestUtils.CreateValidEmptyRuleset(context.InputFolder, "dummyQp");
        var projectSnippet = $"""
            <PropertyGroup>
              <SonarQubeTempPath>{context.InputFolder}</SonarQubeTempPath>
              <SonarQubeOutputPath>{context.OutputFolder}</SonarQubeOutputPath>
              <SonarQubeConfigPath>{context.ConfigFolder}</SonarQubeConfigPath>
              <CodeAnalysisRuleSet>{dummyQpRulesetPath}</CodeAnalysisRuleSet>
            </PropertyGroup>

            <ItemGroup>
              <SonarQubeSetting Include='sonar.other.setting'>
                <Value>other value</Value>
              </SonarQubeSetting>
            </ItemGroup>
            """;
        var filePath = context.CreateProjectFile(projectSnippet);

        var result = BuildRunner.BuildTargets(TestContext, filePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild);
        result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
        result.AssertTargetOrdering(
            TargetConstants.SonarResolveReferences,
            TargetConstants.SonarOverrideRunAnalyzers,
            TargetConstants.BeforeCompile,
            TargetConstants.ResolveCodeAnalysisRuleSet,
            TargetConstants.SonarCategoriseProject,
            TargetConstants.OverrideRoslynAnalysis,
            TargetConstants.SetRoslynAnalysisProperties,
            TargetConstants.CoreCompile,
            TargetConstants.DefaultBuild,
            TargetConstants.InvokeSonarWriteProjectData_NonRazorProject,
            TargetConstants.SonarWriteProjectData);
    }

    /// <summary>
    /// Checks the error log property has been set to the value supplied in the targets file.
    /// </summary>
    private static void AssertErrorLogIsSetBySonarQubeTargets(BuildLog result)
    {
        var projectSpecificOutDir = result.GetPropertyValue(TargetProperties.ProjectSpecificOutDir);
        AssertExpectedErrorLog(result, Path.Combine(projectSpecificOutDir, "Issues.json"));
    }

    private static void AssertExpectedErrorLog(BuildLog result, string expectedErrorLog) =>
        result.AssertPropertyValue(TargetProperties.ErrorLog, expectedErrorLog);

    private static void AssertExpectedResolvedRuleset(BuildLog result, string expectedResolvedRuleset) =>
        result.AssertPropertyValue(TargetProperties.ResolvedCodeAnalysisRuleset, expectedResolvedRuleset);

    private void AssertExpectedItemValuesExists(BuildLog result, string itemType, params string[] expectedValues)
    {
        DumpLists(result, itemType, expectedValues);
        foreach (var expectedValue in expectedValues)
        {
            result.AssertSingleItemExists(itemType, expectedValue);
        }
        result.AssertItemGroupCount(itemType, expectedValues.Length);
    }

    private void AssertExpectedAnalyzers(BuildLog result, params string[] expected) =>
        AssertExpectedItemValuesExists(result, TargetProperties.AnalyzerItemType, expected);

    private void AssertExpectedAdditionalFiles(BuildLog result, params string[] testSpecificAdditionalFiles)
    {
        var projectSetupAdditionalFiles = new[] { $@"{baseDir}config.1.txt", $@"{baseDir}config.2.txt" };
        var projectSpecificOutFolderFilePath = result.GetPropertyValue(TargetProperties.SonarProjectOutFolderFilePath);
        var projectSpecificConfigFilePath = result.GetPropertyValue(TargetProperties.SonarProjectConfigFilePath);
        var allExpectedAdditionalFiles = projectSetupAdditionalFiles.Concat(testSpecificAdditionalFiles).Concat([projectSpecificOutFolderFilePath, projectSpecificConfigFilePath]);
        AssertExpectedItemValuesExists(result, TargetProperties.AdditionalFilesItemType, allExpectedAdditionalFiles.ToArray());
    }

    private void DumpLists(BuildLog actualResult, string itemType, string[] expected)
    {
        TestContext.WriteLine(string.Empty);
        TestContext.WriteLine("Dumping <" + itemType + "> list: expected");
        foreach (var item in expected)
        {
            TestContext.WriteLine("\t{0}", item);
        }
        TestContext.WriteLine(string.Empty);
        TestContext.WriteLine(string.Empty);
        TestContext.WriteLine("Dumping <" + itemType + "> list: actual");
        foreach (var item in actualResult.GetItem(itemType))
        {
            TestContext.WriteLine("\t{0}", item.Text);
        }
        TestContext.WriteLine(string.Empty);
    }

    /// <summary>
    /// Checks that no analysis warnings will be treated as errors nor will they be ignored.
    /// </summary>
    private static void AssertWarningsAreNotTreatedAsErrorsNorIgnored(BuildLog actualResult)
    {
        actualResult.AssertPropertyValue(TargetProperties.TreatWarningsAsErrors, "false");
        actualResult.AssertPropertyValue(TargetProperties.WarningsAsErrors, string.Empty);
        actualResult.AssertPropertyValue(TargetProperties.WarningLevel, "4");
    }

    /// <summary>
    /// Checks that VS2019 properties are set to run the analysis.
    /// </summary>
    private static void AssertRunAnalyzersIsEnabled(BuildLog actualResult)
    {
        actualResult.AssertPropertyValue(TargetProperties.RunAnalyzers, "true");
        actualResult.AssertPropertyValue(TargetProperties.RunAnalyzersDuringBuild, "true");
    }

    /// <summary>
    /// Checks that a SonarQubeSetting does not exist.
    /// </summary>
    private static void AssertAnalysisSettingDoesNotExist(BuildLog actualResult, string settingName)
    {
        var matches = actualResult.GetItem(BuildTaskConstants.SettingItemName);
        matches.Should().NotContain(x => x.Text.Equals(settingName), "Not expected SonarQubeSetting with include value of '{0}' to exist. Actual occurrences: {1}", settingName, matches.Count());
    }

    /// <summary>
    /// Checks whether there is a single "SonarQubeSetting" item with the expected name and setting value.
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
        var settings = actualResult.GetItem(BuildTaskConstants.SettingItemName);
        settings.Should().NotBeEmpty();

        var matches = settings.Where(x => x.Text.Equals(settingName, StringComparison.Ordinal)).ToList();
        matches.Should().ContainSingle($"Only one and only expecting one SonarQubeSetting with include value of '{0}' to exist. Count: {matches.Count}", settingName);

        var item = matches[0];
        if (item.Metadata.TryGetValue(BuildTaskConstants.SettingValueMetadataName, out var value))
        {
            value.Should().Be(expectedValue, "SonarQubeSetting with include value '{0}' does not have the expected value", settingName);
        }
        else
        {
            Assert.Fail("SonarQubeSetting does not have value.");
        }
    }

    private BuildLog Execute_Settings_ValidSetup(TargetsTestsContext context, bool isTestProject, string excludeTestProject, string csRuleSetPath, string vbRulesetPath)
    {
        // Create a valid config file containing analyzer settings for both VB and C#
        var config = new AnalysisConfig
        {
            SonarQubeHostUrl = "http://sonarqube.com",
            SonarQubeVersion = "8.9", // Latest behavior, test code is analyzed by default.
            ServerSettings =
            [
                new("sonar.cs.roslyn.ignoreIssues", "true"),
                new("sonar.vbnet.roslyn.ignoreIssues", "true")
            ],
            LocalSettings = [new("sonar.dotnet.excludeTestProjects", excludeTestProject)],
            AnalyzersSettings =
            [
                new AnalyzerSettings
                {
                    Language = "cs",
                    RulesetPath = csRuleSetPath,
                    DeactivatedRulesetPath = $@"{otherDrive}C#-deactivated.ruleset",
                    AnalyzerPlugins =
                    [
                        new AnalyzerPlugin("csharp", "v1", "resName", [$@"{defaultFolder}SonarAnalyzer.C#.dll", $@"{defaultFolder}SonarAnalyzer.dll", $@"{defaultFolder}Google.Protobuf.dll"]),
                        new AnalyzerPlugin("external-cs", "v1", "resName", [$@"{baseDir}external.analyzer.C#.dll"])
                    ],
                    AdditionalFilePaths = [$@"{baseDir}config.1.txt", $@"{baseDir}config.2.txt"]
                },

                // VB
                new AnalyzerSettings
                {
                    Language = "vbnet",
                    RulesetPath = vbRulesetPath,
                    DeactivatedRulesetPath = $@"{otherDrive}VB-deactivated.ruleset",
                    AnalyzerPlugins =
                    [
                        new AnalyzerPlugin("vbnet", "v1", "resName", [$@"{defaultFolder}SonarAnalyzer.VB.dll", $@"{defaultFolder}SonarAnalyzer.dll", $@"{defaultFolder}Google.Protobuf.dll"]),
                        new AnalyzerPlugin("external-vb", "v1", "resName", [$@"{baseDir}external.analyzer.VB.dll"])
                    ],
                    AdditionalFilePaths = [$@"{baseDir}config.1.txt", $@"{baseDir}config.2.txt"]
                }
            ]
        };

        // Create the project
        var projectSnippet = $"""
            <PropertyGroup>
                <SonarQubeTestProject>{isTestProject}</SonarQubeTestProject>
                <ProjectSpecificOutDir>{context.OutputFolder}</ProjectSpecificOutDir>
                <ResolvedCodeAnalysisRuleset>{baseDir}should.be.overridden.ruleset</ResolvedCodeAnalysisRuleset>
                <!-- These should be overriden by the targets file -->
                <RunAnalyzers>false</RunAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
            </PropertyGroup>

            <ItemGroup>
                <Analyzer Include='project.additional.analyzer1.dll' />
                <Analyzer Include='{baseDir}project.additional.analyzer2.dll' />
                <AdditionalFiles Include='project.additional.file.1.txt' />
                <AdditionalFiles Include='{someFolder}project.additional.file.2.txt' />
            </ItemGroup>
            """;
        var filePath = context.CreateProjectFile(projectSnippet, config: config);

        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.SonarOverrideRunAnalyzers, TargetConstants.OverrideRoslynAnalysis);

        // Assert - check invariants
        result.AssertTargetExecuted(TargetConstants.OverrideRoslynAnalysis);
        result.AssertTargetExecuted(TargetConstants.SetRoslynAnalysisProperties);
        result.AssertTaskExecuted("GetAnalyzerSettings");
        result.BuildSucceeded.Should().BeTrue();
        AssertErrorLogIsSetBySonarQubeTargets(result);
        AssertWarningsAreNotTreatedAsErrorsNorIgnored(result);
        AssertRunAnalyzersIsEnabled(result);
        var capturedProjectSpecificConfDir = result.GetPropertyValue(TargetProperties.ProjectSpecificConfDir);
        result.Messages.Should().Contain($@"Sonar: ({Path.GetFileName(filePath)}) Analysis configured successfully with {Path.Combine(capturedProjectSpecificConfDir, "SonarProjectConfig.xml")}.");

        return result;
    }

    private static AnalyzerPlugin CreateAnalyzerPlugin(params string[] fileList) =>
        new AnalyzerPlugin
        {
            AssemblyPaths = [..fileList]
        };
}
