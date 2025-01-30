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

using System.Xml;
using SonarScanner.MSBuild.PreProcessor.Roslyn;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class RoslynAnalyzerProviderTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void RoslynConfig_ConstructorArgumentChecks()
    {
        ((Func<RoslynAnalyzerProvider>)(() => new RoslynAnalyzerProvider(null, new TestLogger()))).Should().ThrowExactly<ArgumentNullException>();
        ((Func<RoslynAnalyzerProvider>)(() => new RoslynAnalyzerProvider(new MockAnalyzerInstaller(), null))).Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    public void RoslynConfig_SetupAnalyzers_ArgumentChecks()
    {
        var logger = new TestLogger();
        var rules = Enumerable.Empty<SonarRule>();
        var language = RoslynAnalyzerProvider.CSharpLanguage;
        var sonarProperties = new ListPropertiesProvider();
        var settings = CreateSettings(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext));
        var testSubject = new RoslynAnalyzerProvider(new MockAnalyzerInstaller(), logger);
        ((Func<AnalyzerSettings>)(() => testSubject.SetupAnalyzer(null, sonarProperties, rules, language))).Should().ThrowExactly<ArgumentNullException>();
        ((Func<AnalyzerSettings>)(() => testSubject.SetupAnalyzer(settings, null, rules, language))).Should().ThrowExactly<ArgumentNullException>();
        ((Func<AnalyzerSettings>)(() => testSubject.SetupAnalyzer(settings, sonarProperties, null, language))).Should().ThrowExactly<ArgumentNullException>();
        ((Func<AnalyzerSettings>)(() => testSubject.SetupAnalyzer(settings, sonarProperties, rules, null))).Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    public void RoslynConfig_NoActiveRules()
    {
        var context = new Context(TestContext, new ListPropertiesProvider(), [], []);
        context.ActualSettings.Should().NotBeNull();
    }

    [TestMethod]
    public void RoslynConfig_NoAssemblies()
    {
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
        {
            { "wintellect.analyzerId", "Wintellect.Analyzers" },
            { "wintellect.ruleNamespace", "Wintellect.Analyzers" },
            { "sonaranalyzer-cs.analyzerId", "SonarAnalyzer.CSharp" },
            { "sonaranalyzer-cs.ruleNamespace", "SonarAnalyzer.CSharp" }
        });
        var context = new Context(TestContext, sonarProperties, [[@"c:\assembly1.dll"]]);

        context.AssertCorrectAnalyzerSettings();
        context.AssertNoWarningsOrErrors();
        context.AssertCorrectRulesets();
        context.AssertExpectedAssemblies([]);
        context.AssertExpectedPluginsRequested([]);
    }

    [TestMethod]
    public void RoslynConfig_ValidProfile()
    {
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
        var context = new Context(TestContext, sonarProperties, [[@"c:\assembly1.dll"], [@"d:\foo\assembly2.dll"]]);
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

        context.AssertCorrectAnalyzerSettings();
        context.AssertNoWarningsOrErrors();
        context.AssertCorrectRulesets();
        context.AssertExpectedAssemblies(@"c:\assembly1.dll", @"d:\foo\assembly2.dll");
        context.AssertExpectedPluginsRequested(["wintellect", "csharp"]);
        context.AssertExpectedAdditionalFileExists(expectedSonarLintXml);
    }

    private static BuildSettings CreateSettings(string rootDir) =>
        BuildSettings.CreateNonTeamBuildSettingsForTesting(rootDir);

    private class Context
    {
        private readonly TestContext testContext;
        private readonly TestLogger logger = new();
        private readonly string rootDir;
        private readonly MockAnalyzerInstaller analyzerInstaller;

        public AnalyzerSettings ActualSettings { get; set; }

        public Context(TestContext testContext, ListPropertiesProvider properties, List<string[]> analyzerPlugins, IEnumerable<SonarRule> rules = null)
        {
            this.testContext = testContext;
            rootDir = CreateTestFolders();
            analyzerInstaller = new MockAnalyzerInstaller(CreateAnalyzerPlugins(analyzerPlugins));
            var sut = new RoslynAnalyzerProvider(analyzerInstaller, logger);
            ActualSettings = sut.SetupAnalyzer(CreateSettings(rootDir), properties, rules ?? CreateRules(), RoslynAnalyzerProvider.CSharpLanguage);
        }

        public void AssertCorrectRulesets()
        {
            CheckRuleset(ActualSettings.RulesetPath, false);
            CheckRuleset(ActualSettings.DeactivatedRulesetPath, true);
        }

        public void AssertNoWarningsOrErrors()
        {
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);
        }

        public void AssertCorrectAnalyzerSettings()
        {
            ActualSettings.Should().NotBeNull("Not expecting the config to be null");
            ActualSettings.AdditionalFilePaths.Should().NotBeNull();
            ActualSettings.AnalyzerPlugins.Should().NotBeNull();
            ActualSettings.RulesetPath.Should().NotBeNullOrEmpty();
            // Any file paths returned in the config should exist
            foreach (var filePath in ActualSettings.AdditionalFilePaths)
            {
                File.Exists(filePath).Should().BeTrue("Expected additional file does not exist: {0}", filePath);
            }
            File.Exists(ActualSettings.RulesetPath).Should().BeTrue("Specified ruleset does not exist: {0}", ActualSettings.RulesetPath);
        }

        public void AssertExpectedAssemblies(params string[] expected)
        {
            foreach (var expectedItem in expected)
            {
                ActualSettings.AnalyzerPlugins
                    .SelectMany(x => x.AssemblyPaths)
                    .Contains(expectedItem, StringComparer.OrdinalIgnoreCase)
                    .Should().BeTrue("Expected assembly file path was not returned: {0}", expectedItem);
            }
            ActualSettings.AnalyzerPlugins.Should().HaveCount(expected.Length, "Too many assembly file paths returned");
        }

        public void AssertExpectedPluginsRequested(IEnumerable<string> plugins) =>
            analyzerInstaller.AssertExpectedPluginsRequested(plugins);

        public void AssertExpectedAdditionalFileExists(string expectedContent, string expectedFileName = "SonarLint.xml")
        {
            ActualSettings.AdditionalFilePaths.Should().ContainSingle();
            // Check one file of the expected name exists
            var matches = ActualSettings.AdditionalFilePaths.Where(x => string.Equals(expectedFileName, Path.GetFileName(x), StringComparison.OrdinalIgnoreCase));
            matches.Should().ContainSingle("Unexpected number of files named \"{0}\". One and only one expected", expectedFileName);
            // Check the file exists and has the expected content
            var actualFilePath = matches.First();
            File.Exists(actualFilePath).Should().BeTrue("AdditionalFile does not exist: {0}", actualFilePath);
            // Dump the contents to help with debugging
            testContext.AddResultFile(actualFilePath);
            testContext.WriteLine("File contents: {0}", actualFilePath);
            testContext.WriteLine(File.ReadAllText(actualFilePath));
            testContext.WriteLine(string.Empty);
            if (expectedContent is not null)
            {
                File.ReadAllText(actualFilePath).Should().Be(expectedContent, "Additional file does not have the expected content: {0}", expectedFileName);
            }
        }

        private static List<SonarRule> CreateRules() =>
            [
                new("csharpsquid", "S1116", true) { Parameters = new() { { "key", "value" } } },
                new("csharpsquid", "S1125", true),
                new("roslyn.wintellect", "Wintellect003", true),
                new("csharpsquid", "S1000", false)
            ];

        private void CheckRuleset(string ruleSetPath, bool isDeactivated)
        {
            var expectedFileName = isDeactivated ? $"Sonar-{RoslynAnalyzerProvider.CSharpLanguage}-none.ruleset" : $"Sonar-{RoslynAnalyzerProvider.CSharpLanguage}.ruleset";
            var expectedRule = isDeactivated ? "None" : "Warning";
            var expectedContent = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <RuleSet xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Name="Rules for SonarQube" Description="This rule set was automatically generated from SonarQube" ToolsVersion="14.0">
                  <Rules AnalyzerId="SonarScannerFor.NET" RuleNamespace="SonarScannerFor.NET">
                    <Rule Id="S1116" Action="{expectedRule}" />
                    <Rule Id="S1125" Action="{expectedRule}" />
                    <Rule Id="Wintellect003" Action="{expectedRule}" />
                    <Rule Id="S1000" Action="None" />
                  </Rules>
                </RuleSet>
                """;
            ruleSetPath.Should().NotBeNullOrEmpty("Ruleset file path should be set");
            Path.IsPathRooted(ruleSetPath).Should().BeTrue("Ruleset file path should be absolute");
            Path.GetFileName(ruleSetPath).Should().Be(expectedFileName, "Ruleset file does not have the expected name");
            Path.GetDirectoryName(ruleSetPath).Should().Be(Path.Combine(rootDir, "conf"), "Ruleset was not written to the expected location");
            File.Exists(ruleSetPath).Should().BeTrue("Specified ruleset file does not exist: {0}", ruleSetPath);
            testContext.AddResultFile(ruleSetPath);
            CheckFileIsXml(ruleSetPath);
            File.ReadAllText(ruleSetPath).Should().Be(expectedContent, "Ruleset file does not have the expected content: {0}", ruleSetPath);
        }

        private static IList<AnalyzerPlugin> CreateAnalyzerPlugins(List<string[]> pluginList) =>
            pluginList.Select(x => new AnalyzerPlugin { AssemblyPaths = x.ToList() }).ToList();

        private static void CheckFileIsXml(string fullPath)
        {
            var doc = new XmlDocument();
            doc.Load(fullPath);
            doc.FirstChild.Should().NotBeNull("Expecting the file to contain some valid XML");
        }

        private string CreateTestFolders()
        {
            var rootFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
            // Create the binary and conf folders that are created by the bootstrapper
            Directory.CreateDirectory(Path.Combine(rootFolder, "bin"));
            Directory.CreateDirectory(Path.Combine(rootFolder, "conf"));
            return rootFolder;
        }
    }
}
