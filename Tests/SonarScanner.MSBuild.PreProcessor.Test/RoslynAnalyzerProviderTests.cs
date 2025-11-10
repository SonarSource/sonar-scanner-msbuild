/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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
        var analyzerInstaller = new MockAnalyzerInstaller();
        var logger = new TestLogger();
        var rules = Enumerable.Empty<SonarRule>();
        var language = RoslynAnalyzerProvider.CSharpLanguage;
        var sonarProperties = new ListPropertiesProvider();
        var settings = CreateSettings(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext));
        ((Func<RoslynAnalyzerProvider>)(() => new RoslynAnalyzerProvider(null, logger, settings, sonarProperties, rules, language))).Should().ThrowExactly<ArgumentNullException>();
        ((Func<RoslynAnalyzerProvider>)(() => new RoslynAnalyzerProvider(analyzerInstaller, null, settings, sonarProperties, rules, language))).Should().ThrowExactly<ArgumentNullException>();
        ((Func<RoslynAnalyzerProvider>)(() => new RoslynAnalyzerProvider(analyzerInstaller, logger, null, sonarProperties, rules, language))).Should().ThrowExactly<ArgumentNullException>();
        ((Func<RoslynAnalyzerProvider>)(() => new RoslynAnalyzerProvider(analyzerInstaller, logger, settings, null, rules, language))).Should().ThrowExactly<ArgumentNullException>();
        ((Func<RoslynAnalyzerProvider>)(() => new RoslynAnalyzerProvider(analyzerInstaller, logger, settings, sonarProperties, null, language))).Should().ThrowExactly<ArgumentNullException>();
        ((Func<RoslynAnalyzerProvider>)(() => new RoslynAnalyzerProvider(analyzerInstaller, logger, settings, sonarProperties, rules, null))).Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    public void RoslynConfig_NoActiveRules() =>
        new Context(TestContext, [], [], []).ActualSettings.Should().NotBeNull();

    [TestMethod]
    public void RoslynConfig_PropertyWithoutDot()
    {
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
        {
            {"propertyWithoutDot", "someValue"}
        });
        new Context(TestContext, sonarProperties, [], []).ActualSettings.Should().NotBeNull();
    }

    [TestMethod]
    public void RoslynConfig_NoAssemblies()
    {
        var context = new Context(TestContext, [], [[@"c:\assembly1.dll"]]);
        context.AssertCorrectAnalyzerSettings();
        context.AssertNoWarningsOrErrors();
        context.AssertInfoMessages("No Roslyn analyzer plugins were specified so no Roslyn analyzers will be run for cs");
        context.AssertCorrectRulesets();
        context.AssertExpectedAssemblies();
        context.AssertEmptyPluginsRequested();
    }

    [TestMethod]
    [DataRow(RoslynAnalyzerProvider.CSharpLanguage)]
    [DataRow(RoslynAnalyzerProvider.VBNetLanguage)]
    public void RoslynConfig_ValidProfile_WithLegacy(string language)
    {
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
        {
            {"wintellect.pluginKey", "wintellect"},
            {"wintellect.pluginVersion", "1.13.0"},
            {"wintellect.staticResourceName", "wintellect.zip"},
            {"sonar.cs.analyzer.dotnet.pluginKey", "cs"},
            {"sonar.cs.analyzer.dotnet.pluginVersion", "1.42.0"},
            {"sonar.cs.analyzer.dotnet.staticResourceName", "SonarAnalyzer.zip"},
            {"sonar.vbnet.analyzer.dotnet.pluginKey", "vbnet"},
            {"sonar.vbnet.analyzer.dotnet.pluginVersion", "1.42.0"},
            {"sonar.vbnet.analyzer.dotnet.staticResourceName", "SonarAnalyzer.zip"},
            {"sonar.vbnet.testPropertyPattern", "foo"},
            {"sonaranalyzer-cs.analyzerId", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-cs.ruleNamespace", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-cs.pluginKey", "cs"},
            {"sonaranalyzer-cs.staticResourceName", "SonarAnalyzer.zip"},
            {"sonaranalyzer-cs.nuget.packageId", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-cs.pluginVersion", "1.42.0"},
            {"sonaranalyzer-cs.nuget.packageVersion", "1.13.0"},
            {"sonaranalyzer-vbnet.analyzerId", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-vbnet.ruleNamespace", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-vbnet.pluginKey", "vbnet"},
            {"sonaranalyzer-vbnet.staticResourceName", "SonarAnalyzer.zip"},
            {"sonaranalyzer-vbnet.nuget.packageId", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-vbnet.pluginVersion", "1.42.0"},
            {"sonaranalyzer-vbnet.nuget.packageVersion", "1.13.0"},
            {"sonar.cs.testPropertyPattern", "foo"},
            {"sonar.sources", "**/*.*"},
            {"sonar.cs.foo", "bar"},
            {"sonar.vbnet.foo", "bar"},
            {"sonar.cs.analyzer.security.pluginKey", "securitycsharpfrontend" },
            {"sonar.cs.analyzer.security.pluginVersion", "2.34.0" },
            {"sonar.cs.analyzer.security.staticResourceName", "SecurityAnalyzer.zip" },
            {"sonaranalyzer.security.cs.pluginKey", "OLDSecurityCSharpFrontend" },
            {"sonaranalyzer.security.cs.pluginVersion", "OLDSecurityCSharpFrontend" },
            {"sonaranalyzer.security.cs.staticResourceName", "OLDSecurityCSharpFrontend" },
        });
        var context = new Context(TestContext, sonarProperties, [[@"c:\assembly1.dll"], [@"d:\foo\assembly2.dll"]], null, language);

        context.AssertCorrectAnalyzerSettings();
        context.AssertNoWarningsOrErrors();
        context.AssertInfoMessages($"Provisioning analyzer assemblies for {language}...");
        context.AssertCorrectRulesets();
        context.AssertExpectedAssemblies(@"c:\assembly1.dll", @"d:\foo\assembly2.dll");
        context.AssertExpectedPluginsRequested();
        context.AssertExpectedAdditionalFileExists(ExpectedSonarLintXml(language));
    }

    [TestMethod]
    [DataRow(RoslynAnalyzerProvider.CSharpLanguage)]
    [DataRow(RoslynAnalyzerProvider.VBNetLanguage)]
    public void RoslynConfig_ValidProfile_LegacyOnly(string language)
    {
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
        {
            {"wintellect.analyzerId", "Wintellect.Analyzers" },
            {"wintellect.ruleNamespace", "Wintellect.Analyzers" },
            {"wintellect.pluginKey", "wintellect"},
            {"wintellect.pluginVersion", "1.13.0"},
            {"wintellect.staticResourceName", "wintellect.zip"},
            {"sonaranalyzer-cs.analyzerId", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-cs.ruleNamespace", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-cs.pluginKey", "cs"},
            {"sonaranalyzer-cs.staticResourceName", "SonarAnalyzer.zip"},
            {"sonaranalyzer-cs.nuget.packageId", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-cs.pluginVersion", "1.42.0"},
            {"sonaranalyzer-cs.nuget.packageVersion", "1.13.0"},
            {"sonaranalyzer-vbnet.analyzerId", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-vbnet.ruleNamespace", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-vbnet.pluginKey", "vbnet"},
            {"sonaranalyzer-vbnet.staticResourceName", "SonarAnalyzer.zip"},
            {"sonaranalyzer-vbnet.nuget.packageId", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-vbnet.pluginVersion", "1.42.0"},
            {"sonaranalyzer-vbnet.nuget.packageVersion", "1.13.0"},
            {"sonar.vbnet.testPropertyPattern", "foo"},
            {"sonar.cs.testPropertyPattern", "foo"},
            {"sonar.sources", "**/*.*"},
            {"sonar.cs.foo", "bar"},
            {"sonar.vbnet.foo", "bar"},
            {"sonaranalyzer.security.cs.pluginKey", "securitycsharpfrontend" },
            {"sonaranalyzer.security.cs.pluginVersion", "2.34.0" },
            {"sonaranalyzer.security.cs.staticResourceName", "SecurityAnalyzer.zip" },
        });
        var context = new Context(TestContext, sonarProperties, [[@"c:\assembly1.dll"], [@"d:\foo\assembly2.dll"]], null, language);

        context.AssertCorrectAnalyzerSettings();
        context.AssertNoWarningsOrErrors();
        context.AssertCorrectRulesets();
        context.AssertExpectedAssemblies(@"c:\assembly1.dll", @"d:\foo\assembly2.dll");
        context.AssertExpectedPluginsRequested();
        context.AssertExpectedAdditionalFileExists(ExpectedSonarLintXml(language));
    }

    [TestMethod]
    [DataRow(RoslynAnalyzerProvider.CSharpLanguage)]
    [DataRow(RoslynAnalyzerProvider.VBNetLanguage)]
    public void RoslynConfig_ValidProfile_WithoutLegacy(string language)
    {
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
        {
            {"wintellect.pluginKey", "wintellect"},
            {"wintellect.pluginVersion", "1.13.0"},
            {"wintellect.staticResourceName", "wintellect.zip"},
            {"sonar.cs.analyzer.dotnet.pluginKey", "cs"},
            {"sonar.cs.analyzer.dotnet.pluginVersion", "1.42.0"},
            {"sonar.cs.analyzer.dotnet.staticResourceName", "SonarAnalyzer.zip"},
            {"sonar.vbnet.analyzer.dotnet.pluginKey", "vbnet"},
            {"sonar.vbnet.analyzer.dotnet.pluginVersion", "1.42.0"},
            {"sonar.vbnet.analyzer.dotnet.staticResourceName", "SonarAnalyzer.zip"},
            {"sonar.vbnet.testPropertyPattern", "foo"},
            {"sonar.cs.testPropertyPattern", "foo"},
            {"sonar.sources", "**/*.*"},
            {"sonar.cs.foo", "bar"},
            {"sonar.vbnet.foo", "bar"},
            {"sonar.cs.analyzer.security.pluginKey", "securitycsharpfrontend"},
            {"sonar.cs.analyzer.security.pluginVersion", "2.34.0"},
            {"sonar.cs.analyzer.security.staticResourceName", "SecurityAnalyzer.zip"},
        });
        var context = new Context(TestContext, sonarProperties, [[@"c:\assembly1.dll"], [@"d:\foo\assembly2.dll"]], null, language);

        context.AssertCorrectAnalyzerSettings();
        context.AssertNoWarningsOrErrors();
        context.AssertInfoMessages($"Provisioning analyzer assemblies for {language}...");
        context.AssertCorrectRulesets();
        context.AssertExpectedAssemblies(@"c:\assembly1.dll", @"d:\foo\assembly2.dll");
        context.AssertExpectedPluginsRequested();
        context.AssertExpectedAdditionalFileExists(ExpectedSonarLintXml(language));
    }

    [TestMethod]
    [DataRow("sonar.cs.analyzer.dotnet.missing-pluginKey", "sonar.cs.analyzer.dotnet.version", "sonar.cs.analyzer.dotnet.staticResourceName")]
    [DataRow("sonar.cs.analyzer.dotnet.pluginKey", "sonar.cs.analyzer.dotnet.missing-version", "sonar.cs.analyzer.dotnet.staticResourceName")]
    [DataRow("sonar.cs.analyzer.dotnet.pluginKey", "sonar.cs.analyzer.dotnet.version", "sonar.cs.analyzer.dotnet.missing-staticResourceName")]
    public void RoslynConfig_IncompletePluginIgnored(string analyzerIdProperty, string verisonProperty, string staticResourceProperty)
    {
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
            {
                {analyzerIdProperty, "SonarAnalyzer.CSharp"},
                {verisonProperty, "SonarAnalyzer.CSharp"},
                {staticResourceProperty, "SonarAnalyzer.zip"},
            });
        var context = new Context(TestContext, sonarProperties, [[@"c:\assembly1.dll"], [@"d:\foo\assembly2.dll"]]);
        context.AssertCorrectAnalyzerSettings();
        context.AssertNoWarningsOrErrors();
        context.AssertInfoMessages("No Roslyn analyzer plugins were specified so no Roslyn analyzers will be run for cs");
        context.AssertCorrectRulesets();
        context.AssertExpectedAssemblies();
        context.AssertEmptyPluginsRequested();
    }

    private static BuildSettings CreateSettings(string rootDir) =>
        BuildSettings.CreateSettingsForTesting(rootDir);

    private static string ExpectedSonarLintXml(string language) =>
        language switch
        {
            RoslynAnalyzerProvider.CSharpLanguage => """
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
                      <Key>cs-S1116</Key>
                      <Parameters>
                        <Parameter>
                          <Key>key</Key>
                          <Value>value</Value>
                        </Parameter>
                      </Parameters>
                    </Rule>
                    <Rule>
                      <Key>cs-S1125</Key>
                    </Rule>
                  </Rules>
                  <Files>
                  </Files>
                </AnalysisInput>

                """,
            RoslynAnalyzerProvider.VBNetLanguage => """
                <?xml version="1.0" encoding="UTF-8"?>
                <AnalysisInput>
                  <Settings>
                    <Setting>
                      <Key>sonar.vbnet.testPropertyPattern</Key>
                      <Value>foo</Value>
                    </Setting>
                    <Setting>
                      <Key>sonar.vbnet.foo</Key>
                      <Value>bar</Value>
                    </Setting>
                  </Settings>
                  <Rules>
                    <Rule>
                      <Key>vbnet-S1116</Key>
                      <Parameters>
                        <Parameter>
                          <Key>key</Key>
                          <Value>value</Value>
                        </Parameter>
                      </Parameters>
                    </Rule>
                    <Rule>
                      <Key>vbnet-S1125</Key>
                    </Rule>
                  </Rules>
                  <Files>
                  </Files>
                </AnalysisInput>

                """,
            _ => throw new NotSupportedException($"Unexpected language: {language}")
        };

    private sealed class Context
    {
        private readonly TestContext testContext;
        private readonly TestLogger logger = new();
        private readonly string rootDir;
        private readonly MockAnalyzerInstaller analyzerInstaller;
        private readonly string language;

        public AnalyzerSettings ActualSettings { get; set; }

        public Context(TestContext testContext, ListPropertiesProvider properties, List<string[]> analyzerPlugins, IEnumerable<SonarRule> rules = null, string language = null)
        {
            this.testContext = testContext;
            rootDir = CreateTestFolders();
            analyzerInstaller = new MockAnalyzerInstaller(CreateAnalyzerPlugins(analyzerPlugins));
            this.language = language ?? RoslynAnalyzerProvider.CSharpLanguage;
            var sut = new RoslynAnalyzerProvider(analyzerInstaller, logger, CreateSettings(rootDir), properties, rules ?? CreateRules(), this.language);
            ActualSettings = sut.SetupAnalyzer();
        }

        public void AssertCorrectRulesets()
        {
            CheckRuleset(ActualSettings.RulesetPath, false);
            CheckRuleset(ActualSettings.DeactivatedRulesetPath, true);
        }

        public void AssertNoWarningsOrErrors() =>
            logger.Should().HaveNoWarnings().And.HaveNoErrors();

        public void AssertInfoMessages(params string[] expected)
        {
            logger.Should().HaveInfos(expected.Length);
            foreach (var info in expected)
            {
                logger.Should().HaveInfos(info);
            }
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

        public void AssertEmptyPluginsRequested() =>
            analyzerInstaller.AssertOnlyExpectedPluginsRequested([]);

        public void AssertExpectedPluginsRequested()
        {
            var expectedPlugins = new List<Plugin>()
                {
                    new() { Key = "wintellect", Version = "1.13.0", StaticResourceName = "wintellect.zip" },
                    new() { Key = language, Version = "1.42.0", StaticResourceName = "SonarAnalyzer.zip" },
                };
            if (language == RoslynAnalyzerProvider.CSharpLanguage)
            {
                expectedPlugins.Add(new() { Key = "securitycsharpfrontend", Version = "2.34.0", StaticResourceName = "SecurityAnalyzer.zip" });
            }
            analyzerInstaller.AssertOnlyExpectedPluginsRequested(expectedPlugins);
        }

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
                File.ReadAllText(actualFilePath).Should().BeIgnoringLineEndings(expectedContent);
            }
        }

        private List<SonarRule> CreateRules() =>
            language switch
            {
                RoslynAnalyzerProvider.CSharpLanguage =>
                    [
                        new("csharpsquid", "cs-S1116", true) { Parameters = new() { { "key", "value" } } },
                        new("csharpsquid", "cs-S1125", true),
                        new("roslyn.wintellect", "Wintellect003", true),
                        new("csharpsquid", "cs-S1000", false),
                        new("roslyn.sonaranalyzer.security.cs", "SECURITY2076", true)
                    ],
                RoslynAnalyzerProvider.VBNetLanguage => // VB.NET language will never receive roslyn.sonaranalyzer.security.cs rules, because those are only in the C# QP
                    [
                        new("roslyn.wintellect", "Wintellect003", true),
                        new("vbnet", "vbnet-S1116", true) { Parameters = new() { { "key", "value" } } },
                        new("vbnet", "vbnet-S1125", true),
                        new("vbnet", "vbnet-S1000", false),
                    ],
                _ => throw new NotSupportedException($"Unexpected language: {language}")
            };

        private string ExpectedRuleSetFormat() =>
            language switch
            {
                RoslynAnalyzerProvider.CSharpLanguage => $$"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <RuleSet {{XmlnsDefinition()}} Name="Rules for SonarQube" Description="This rule set was automatically generated from SonarQube" ToolsVersion="14.0">
                      <Rules AnalyzerId="SonarScannerFor.NET" RuleNamespace="SonarScannerFor.NET">
                        <Rule Id="cs-S1116" Action="{0}" />
                        <Rule Id="cs-S1125" Action="{0}" />
                        <Rule Id="Wintellect003" Action="{0}" />
                        <Rule Id="cs-S1000" Action="None" />
                        <Rule Id="SECURITY2076" Action="{0}" />
                      </Rules>
                    </RuleSet>
                    """,
                RoslynAnalyzerProvider.VBNetLanguage => $$"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <RuleSet {{XmlnsDefinition()}} Name="Rules for SonarQube" Description="This rule set was automatically generated from SonarQube" ToolsVersion="14.0">
                      <Rules AnalyzerId="SonarScannerFor.NET" RuleNamespace="SonarScannerFor.NET">
                        <Rule Id="Wintellect003" Action="{0}" />
                        <Rule Id="vbnet-S1116" Action="{0}" />
                        <Rule Id="vbnet-S1125" Action="{0}" />
                        <Rule Id="vbnet-S1000" Action="None" />
                      </Rules>
                    </RuleSet>
                    """,
                _ => throw new NotSupportedException($"Unexpected language: {language}")
            };

        private void CheckRuleset(string ruleSetPath, bool isDeactivated)
        {
            var expectedFileName = isDeactivated ? $"Sonar-{language}-none.ruleset" : $"Sonar-{language}.ruleset";
            var expectedRule = isDeactivated ? "None" : "Warning";
            var expectedContent = string.Format(ExpectedRuleSetFormat(), expectedRule);
            ruleSetPath.Should().NotBeNullOrEmpty("Ruleset file path should be set");
            Path.IsPathRooted(ruleSetPath).Should().BeTrue("Ruleset file path should be absolute");
            Path.GetFileName(ruleSetPath).Should().Be(expectedFileName, "Ruleset file does not have the expected name");
            Path.GetDirectoryName(ruleSetPath).Should().Be(Path.Combine(rootDir, "conf"), "Ruleset was not written to the expected location");
            File.Exists(ruleSetPath).Should().BeTrue("Specified ruleset file does not exist: {0}", ruleSetPath);
            testContext.AddResultFile(ruleSetPath);
            CheckFileIsXml(ruleSetPath);
            File.ReadAllText(ruleSetPath).Should().BeIgnoringLineEndings(expectedContent);
        }

        private static List<AnalyzerPlugin> CreateAnalyzerPlugins(List<string[]> pluginList) =>
            pluginList.Select(x => new AnalyzerPlugin { AssemblyPaths = x.ToList() }).ToList();

        private static string XmlnsDefinition() =>
#if NET
            @"xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema""";
#else
            @"xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""";
#endif

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
