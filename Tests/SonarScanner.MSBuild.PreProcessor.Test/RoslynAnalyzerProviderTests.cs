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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class RoslynAnalyzerProviderTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void RoslynConfig_ConstructorArgumentChecks()
    {
        Action act = () => new RoslynAnalyzerProvider(null, new TestLogger());
        act.Should().ThrowExactly<ArgumentNullException>();
        act = () => new RoslynAnalyzerProvider(new MockAnalyzerInstaller(), null);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    public void RoslynConfig_SetupAnalyzers_ArgumentChecks()
    {
        var logger = new TestLogger();
        var rules = Enumerable.Empty<SonarRule>();
        var language = RoslynAnalyzerProvider.CSharpLanguage;
        var sonarProperties = new ListPropertiesProvider();
        var settings = CreateSettings(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext));

        var testSubject = CreateTestSubject(logger);

        Action act = () => testSubject.SetupAnalyzer(null, sonarProperties, rules, language);
        act.Should().ThrowExactly<ArgumentNullException>();
        act = () => testSubject.SetupAnalyzer(settings, null, rules, language);
        act.Should().ThrowExactly<ArgumentNullException>();
        act = () => testSubject.SetupAnalyzer(settings, sonarProperties, null, language);
        act.Should().ThrowExactly<ArgumentNullException>();
        act = () => testSubject.SetupAnalyzer(settings, sonarProperties, rules, null);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    public void RoslynConfig_NoActiveRules()
    {
        var logger = new TestLogger();
        var rules = Enumerable.Empty<SonarRule>();
        var pluginKey = "csharp";
        var sonarProperties = new ListPropertiesProvider();
        var settings = CreateSettings(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext));

        var testSubject = CreateTestSubject(logger);

        testSubject.SetupAnalyzer(settings, sonarProperties, rules, pluginKey).Should().NotBeNull();
    }

    [TestMethod]
    public void RoslynConfig_NoAssemblies()
    {
        var rootFolder = CreateTestFolders();
        var logger = new TestLogger();
        var rules = CreateRules();
        var language = RoslynAnalyzerProvider.CSharpLanguage;
        // missing properties to get plugin related properties
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
        {
            { "wintellect.analyzerId", "Wintellect.Analyzers" },
            { "wintellect.ruleNamespace", "Wintellect.Analyzers" },
            { "sonaranalyzer-cs.analyzerId", "SonarAnalyzer.CSharp" },
            { "sonaranalyzer-cs.ruleNamespace", "SonarAnalyzer.CSharp" }
        });
        var mockInstaller = new MockAnalyzerInstaller
        {
            AnalyzerPluginsToReturn = new List<AnalyzerPlugin> { CreateAnalyzerPlugin("c:\\assembly1.dll", "d:\\foo\\assembly2.dll") }
        };
        var settings = CreateSettings(rootFolder);
        var testSubject = new RoslynAnalyzerProvider(mockInstaller, logger);

        var actualSettings = testSubject.SetupAnalyzer(settings, sonarProperties, rules, language);

        CheckSettingsInvariants(actualSettings);
        logger.AssertWarningsLogged(0);
        logger.AssertErrorsLogged(0);
        CheckRuleset(actualSettings.RulesetPath, rootFolder, language);
        CheckTestRuleset(actualSettings.DeactivatedRulesetPath, rootFolder, language);
        actualSettings.AnalyzerPlugins.Should().BeEmpty();
        var plugins = new List<string>();
        mockInstaller.AssertExpectedPluginsRequested(plugins);
    }

    [TestMethod]
    public void RoslynConfig_ValidProfile()
    {
        var rootFolder = CreateTestFolders();
        var logger = new TestLogger();
        var rules = CreateRules();
        var language = RoslynAnalyzerProvider.CSharpLanguage;
        var mockInstaller = new MockAnalyzerInstaller
        {
            AnalyzerPluginsToReturn = new List<AnalyzerPlugin>
            {
                CreateAnalyzerPlugin("c:\\assembly1.dll"),
                CreateAnalyzerPlugin("d:\\foo\\assembly2.dll")
            }
        };
        var settings = CreateSettings(rootFolder);

        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
        {
            // for ruleset
            {"wintellect.analyzerId", "Wintellect.Analyzers" },
            {"wintellect.ruleNamespace", "Wintellect.Analyzers" },

            // to fetch assemblies
            {"wintellect.pluginKey", "wintellect"},
            {"wintellect.pluginVersion", "1.13.0"},
            {"wintellect.staticResourceName", "SonarAnalyzer.zip"},

            {"sonaranalyzer-cs.analyzerId", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-cs.ruleNamespace", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-cs.pluginKey", "csharp"},
            {"sonaranalyzer-cs.staticResourceName", "SonarAnalyzer.zip"},
            {"sonaranalyzer-cs.nuget.packageId", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-cs.pluginVersion", "1.13.0"},
            {"sonaranalyzer-cs.nuget.packageVersion", "1.13.0"},

            // Extra properties - those started sonar.cs should be included, the others ignored
            {"sonar.vb.testPropertyPattern", "foo"},
            {"sonar.cs.testPropertyPattern", "foo"},
            {"sonar.sources", "**/*.*"},
            {"sonar.cs.foo", "bar"}
        });
        var expectedSonarLintXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <AnalysisInput>
              <Settings>
                <Setting>
                  <Key>sonar.cs.testPropertyPattern</Key>
                  <Value>foo</Value>
                </Setting>
                <Setting>
                  <Key>sonar.cs.foo</Key>
                  <Value>bar</Value>
                </Setting>
              </Settings>
              <Rules>
                <Rule>
                  <Key>S1116</Key>
                  <Parameters>
                    <Parameter>
                      <Key>key</Key>
                      <Value>value</Value>
                    </Parameter>
                  </Parameters>
                </Rule>
                <Rule>
                  <Key>S1125</Key>
                </Rule>
              </Rules>
              <Files>
              </Files>
            </AnalysisInput>

            """;
        var testSubject = new RoslynAnalyzerProvider(mockInstaller, logger);

        var actualSettings = testSubject.SetupAnalyzer(settings, sonarProperties, rules, language);

        CheckSettingsInvariants(actualSettings);
        logger.AssertWarningsLogged(0);
        logger.AssertErrorsLogged(0);
        CheckRuleset(actualSettings.RulesetPath, rootFolder, language);
        CheckTestRuleset(actualSettings.DeactivatedRulesetPath, rootFolder, language);
        // Currently, only SonarLint.xml is written
        var filePaths = actualSettings.AdditionalFilePaths;
        filePaths.Should().ContainSingle();
        CheckExpectedAdditionalFileExists("SonarLint.xml", expectedSonarLintXml, actualSettings);
        CheckExpectedAssemblies(actualSettings, "c:\\assembly1.dll", "d:\\foo\\assembly2.dll");
        var plugins = new List<string>
        {
            "wintellect",
            "csharp"
        };
        mockInstaller.AssertExpectedPluginsRequested(plugins);
    }

    private List<SonarRule> CreateRules() =>
        /*
        <Rules AnalyzerId=""SonarLint.CSharp"" RuleNamespace=""SonarLint.CSharp"">
        <Rule Id=""S1116"" Action=""Warning""/>
        <Rule Id=""S1125"" Action=""Warning""/>
        </Rules>
        <Rules AnalyzerId=""Wintellect.Analyzers"" RuleNamespace=""Wintellect.Analyzers"">
        <Rule Id=""Wintellect003"" Action=""Warning""/>
        </Rules>
        */
        [
            new SonarRule("csharpsquid", "S1116", true)
            {
                Parameters = new Dictionary<string, string> { { "key", "value" } }
            },
            new SonarRule("csharpsquid", "S1125", true),
            new SonarRule("roslyn.wintellect", "Wintellect003", true),
            new SonarRule("csharpsquid", "S1000", false)
        ];

    private string CreateTestFolders()
    {
        var rootFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        // Create the binary and conf folders that are created by the bootstrapper
        Directory.CreateDirectory(GetBinaryPath(rootFolder));
        Directory.CreateDirectory(GetConfPath(rootFolder));

        return rootFolder;
    }

    private static RoslynAnalyzerProvider CreateTestSubject(ILogger logger) =>
        new(new MockAnalyzerInstaller(), logger);

    private static BuildSettings CreateSettings(string rootDir) =>
        BuildSettings.CreateNonTeamBuildSettingsForTesting(rootDir);

    private static string GetConfPath(string rootDir) =>
        Path.Combine(rootDir, "conf");

    private static string GetBinaryPath(string rootDir) =>
        Path.Combine(rootDir, "bin");

    private static AnalyzerPlugin CreateAnalyzerPlugin(params string[] fileList) =>
        new()
        {
            AssemblyPaths = new List<string>(fileList)
        };

    private static void CheckSettingsInvariants(AnalyzerSettings actualSettings)
    {
        actualSettings.Should().NotBeNull("Not expecting the config to be null");
        actualSettings.AdditionalFilePaths.Should().NotBeNull();
        actualSettings.AnalyzerPlugins.Should().NotBeNull();
        actualSettings.RulesetPath.Should().NotBeNullOrEmpty();
        // Any file paths returned in the config should exist
        foreach (var filePath in actualSettings.AdditionalFilePaths)
        {
            File.Exists(filePath).Should().BeTrue("Expected additional file does not exist: {0}", filePath);
        }
        File.Exists(actualSettings.RulesetPath).Should().BeTrue("Specified ruleset does not exist: {0}", actualSettings.RulesetPath);
    }

    private void CheckRuleset(string ruleSetPath, string rootDir, string language)
    {
        ruleSetPath.Should().NotBeNullOrEmpty("Ruleset file path should be set");
        Path.IsPathRooted(ruleSetPath).Should().BeTrue("Ruleset file path should be absolute");
        File.Exists(ruleSetPath).Should().BeTrue("Specified ruleset file does not exist: {0}", ruleSetPath);
        TestContext.AddResultFile(ruleSetPath);
        CheckFileIsXml(ruleSetPath);
        Path.GetFileName(ruleSetPath).Should().Be($"Sonar-{language}.ruleset", "Ruleset file does not have the expected name");
        Path.GetDirectoryName(ruleSetPath).Should().Be(GetConfPath(rootDir), "Ruleset was not written to the expected location");
        var expectedContent = """
            <?xml version="1.0" encoding="utf-8"?>
            <RuleSet xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Name="Rules for SonarQube" Description="This rule set was automatically generated from SonarQube" ToolsVersion="14.0">
              <Rules AnalyzerId="SonarAnalyzer.CSharp" RuleNamespace="SonarAnalyzer.CSharp">
                <Rule Id="S1116" Action="Warning" />
                <Rule Id="S1125" Action="Warning" />
                <Rule Id="S1000" Action="None" />
              </Rules>
              <Rules AnalyzerId="Wintellect.Analyzers" RuleNamespace="Wintellect.Analyzers">
                <Rule Id="Wintellect003" Action="Warning" />
              </Rules>
            </RuleSet>
            """;
        File.ReadAllText(ruleSetPath).Should().Be(expectedContent, "Ruleset file does not have the expected content: {0}", ruleSetPath);
    }

    private void CheckTestRuleset(string ruleSetPath, string rootDir, string language)
    {
        ruleSetPath.Should().NotBeNullOrEmpty("Ruleset file path should be set");
        Path.IsPathRooted(ruleSetPath).Should().BeTrue("Ruleset file path should be absolute");
        File.Exists(ruleSetPath).Should().BeTrue("Specified ruleset file does not exist: {0}", ruleSetPath);
        TestContext.AddResultFile(ruleSetPath);
        CheckFileIsXml(ruleSetPath);
        Path.GetFileName(ruleSetPath).Should().Be($"Sonar-{language}-none.ruleset", "Ruleset file does not have the expected name");
        Path.GetDirectoryName(ruleSetPath).Should().Be(GetConfPath(rootDir), "Ruleset was not written to the expected location");
        var expectedContent = """
            <?xml version="1.0" encoding="utf-8"?>
            <RuleSet xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Name="Rules for SonarQube" Description="This rule set was automatically generated from SonarQube" ToolsVersion="14.0">
              <Rules AnalyzerId="SonarAnalyzer.CSharp" RuleNamespace="SonarAnalyzer.CSharp">
                <Rule Id="S1116" Action="None" />
                <Rule Id="S1125" Action="None" />
                <Rule Id="S1000" Action="None" />
              </Rules>
              <Rules AnalyzerId="Wintellect.Analyzers" RuleNamespace="Wintellect.Analyzers">
                <Rule Id="Wintellect003" Action="None" />
              </Rules>
            </RuleSet>
            """;
        File.ReadAllText(ruleSetPath).Should().Be(expectedContent, "Ruleset file does not have the expected content: {0}", ruleSetPath);
    }

    private void CheckExpectedAdditionalFileExists(string expectedFileName, string expectedContent, AnalyzerSettings actualSettings)
    {
        // Check one file of the expected name exists
        var matches = actualSettings.AdditionalFilePaths.Where(x => string.Equals(expectedFileName, Path.GetFileName(x), StringComparison.OrdinalIgnoreCase));
        matches.Should().ContainSingle("Unexpected number of files named \"{0}\". One and only one expected", expectedFileName);
        // Check the file exists and has the expected content
        var actualFilePath = matches.First();
        File.Exists(actualFilePath).Should().BeTrue("AdditionalFile does not exist: {0}", actualFilePath);
        // Dump the contents to help with debugging
        TestContext.AddResultFile(actualFilePath);
        TestContext.WriteLine("File contents: {0}", actualFilePath);
        TestContext.WriteLine(File.ReadAllText(actualFilePath));
        TestContext.WriteLine(string.Empty);
        if (expectedContent is not null)
        {
            File.ReadAllText(actualFilePath).Should().Be(expectedContent, "Additional file does not have the expected content: {0}", expectedFileName);
        }
    }

    private static void CheckFileIsXml(string fullPath)
    {
        var doc = new XmlDocument();
        doc.Load(fullPath);
        doc.FirstChild.Should().NotBeNull("Expecting the file to contain some valid XML");
    }

    private static void CheckExpectedAssemblies(AnalyzerSettings actualSettings, params string[] expected)
    {
        foreach (var expectedItem in expected)
        {
            actualSettings.AnalyzerPlugins
                .SelectMany(x => x.AssemblyPaths)
                .Contains(expectedItem, StringComparer.OrdinalIgnoreCase)
                .Should().BeTrue("Expected assembly file path was not returned: {0}", expectedItem);
        }
        actualSettings.AnalyzerPlugins.Should().HaveCount(expected.Length, "Too many assembly file paths returned");
    }
}
