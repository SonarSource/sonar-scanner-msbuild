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
    public void RoslynConfig_NoActiveRules()
    {
        var context = new Context(TestContext, new ListPropertiesProvider(), [], []);
        context.ActualSettings.Should().NotBeNull();
    }

    [TestMethod]
    public void RoslynConfig_PropertyWithNoFullStop()
    {
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
        {
            {"propertyWithNoFullStop", "someValue"}
        });
        var context = new Context(TestContext, sonarProperties, [], []);
        context.ActualSettings.Should().NotBeNull();
    }

    [TestMethod]
    public void RoslynConfig_NoAssemblies()
    {
        var context = new Context(TestContext, new ListPropertiesProvider(), [[@"c:\assembly1.dll"]]);

        context.AssertCorrectAnalyzerSettings();
        context.AssertNoWarningsOrErrors();
        context.AssertInfoMessages("No Roslyn analyzer plugins were specified so no Roslyn analyzers will be run for cs");
        context.AssertCorrectRulesets();
        context.AssertExpectedAssemblies([]);
        context.AssertExpectedPluginsRequested([]);
    }

    [TestMethod]
    public void RoslynConfig_ValidProfile()
    {
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
        {
            {"wintellect.pluginKey", "wintellect"},
            {"wintellect.pluginVersion", "1.13.0"},
            {"wintellect.staticResourceName", "wintellect.zip"},
            {"sonar.cs.analyzer.dotnet.pluginKey", "csharp"},
            {"sonar.cs.analyzer.dotnet.pluginVersion", "1.42.0"},
            {"sonar.cs.analyzer.dotnet.staticResourceName", "SonarAnalyzer.zip"},
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
                  <Key>sonar.cs.analyzer.dotnet.pluginKey</Key>
                  <Value>csharp</Value>
                </Setting>
                <Setting>
                  <Key>sonar.cs.analyzer.dotnet.pluginVersion</Key>
                  <Value>1.42.0</Value>
                </Setting>
                <Setting>
                  <Key>sonar.cs.analyzer.dotnet.staticResourceName</Key>
                  <Value>SonarAnalyzer.zip</Value>
                </Setting>
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
        context.AssertExpectedPluginsRequested(
            [new() { Key = "wintellect", Version = "1.13.0", StaticResourceName = "wintellect.zip" },
            new() { Key = "csharp", Version = "1.42.0", StaticResourceName = "SonarAnalyzer.zip" }]);
        context.AssertExpectedAdditionalFileExists(expectedSonarLintXml);
    }

    [TestMethod]
    public void RoslynConfig_ValidProfile_Legacy_Only()
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
            {"sonaranalyzer-cs.pluginKey", "csharp"},
            {"sonaranalyzer-cs.staticResourceName", "SonarAnalyzer.zip"},
            {"sonaranalyzer-cs.nuget.packageId", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-cs.pluginVersion", "1.42.0"},
            {"sonaranalyzer-cs.nuget.packageVersion", "1.13.0"},
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
        context.AssertExpectedPluginsRequested([
            new() { Key = "wintellect", Version = "1.13.0", StaticResourceName = "wintellect.zip" },
            new() { Key = "csharp", Version = "1.42.0", StaticResourceName = "SonarAnalyzer.zip" }]);
        context.AssertExpectedAdditionalFileExists(expectedSonarLintXml);
    }

    [TestMethod]
    public void RoslynConfig_ValidProfile_Valid_And_Legacy()
    {
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
        {
            {"wintellect.analyzerId", "Wintellect.Analyzers" },
            {"wintellect.ruleNamespace", "Wintellect.Analyzers" },
            {"wintellect.pluginKey", "wintellect"},
            {"wintellect.pluginVersion", "1.13.0"},
            {"wintellect.staticResourceName", "wintellect.zip"},
            {"sonaranalyzer-cs.pluginKey", "csharp"},
            {"sonaranalyzer-cs.staticResourceName", "OLD.SonarAnalyzer.zip"},
            {"sonaranalyzer-cs.pluginVersion", "OLD.1.13.0"},
            {"sonar.cs.analyzer.dotnet.pluginKey", "csharp"},
            {"sonar.cs.analyzer.dotnet.pluginVersion", "1.42.0"},
            {"sonar.cs.analyzer.dotnet.staticResourceName", "SonarAnalyzer.zip"},
            {"sonaranalyzer-cs.nuget.packageId", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer-cs.nuget.packageVersion", "1.13.0"},
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
                  <Key>sonar.cs.analyzer.dotnet.pluginKey</Key>
                  <Value>csharp</Value>
                </Setting>
                <Setting>
                  <Key>sonar.cs.analyzer.dotnet.pluginVersion</Key>
                  <Value>1.42.0</Value>
                </Setting>
                <Setting>
                  <Key>sonar.cs.analyzer.dotnet.staticResourceName</Key>
                  <Value>SonarAnalyzer.zip</Value>
                </Setting>
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
        context.AssertInfoMessages("Provisioning analyzer assemblies for cs...");
        context.AssertCorrectRulesets();
        context.AssertExpectedAssemblies(@"c:\assembly1.dll", @"d:\foo\assembly2.dll");
        context.AssertExpectedPluginsRequested(
            [new() { Key = "wintellect", Version = "1.13.0", StaticResourceName = "wintellect.zip" },
            new() { Key = "csharp", Version = "1.42.0", StaticResourceName = "SonarAnalyzer.zip" }]);
        context.AssertExpectedAdditionalFileExists(expectedSonarLintXml);
    }

    [TestMethod]
    public void RoslynConfig_ValidProfile_Security_Frontend()
    {
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
        {
            {"sonar.cs.analyzer.security.pluginKey", "securitycsharpfrontend"},
            {"sonar.cs.analyzer.security.pluginVersion", "1.13.0"},
            {"sonar.cs.analyzer.security.staticResourceName", "SecurityAnalyzer.zip"},
        });
        var context = new Context(TestContext, sonarProperties, [[@"c:\assembly1.dll"], [@"d:\foo\assembly2.dll"]], [new SonarRule("roslyn.sonaranalyzer.security.cs", "S2076", true)]);

        var expectedSonarLintXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <AnalysisInput>
              <Settings>
                <Setting>
                  <Key>sonar.cs.analyzer.security.pluginKey</Key>
                  <Value>securitycsharpfrontend</Value>
                </Setting>
                <Setting>
                  <Key>sonar.cs.analyzer.security.pluginVersion</Key>
                  <Value>1.13.0</Value>
                </Setting>
                <Setting>
                  <Key>sonar.cs.analyzer.security.staticResourceName</Key>
                  <Value>SecurityAnalyzer.zip</Value>
                </Setting>
              </Settings>
              <Rules>
              </Rules>
              <Files>
              </Files>
            </AnalysisInput>

            """;

        var expectedSecurityRules = """
        <?xml version="1.0" encoding="utf-8"?>
        <RuleSet xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Name="Rules for SonarQube" Description="This rule set was automatically generated from SonarQube" ToolsVersion="14.0">
          <Rules AnalyzerId="SonarScannerFor.NET" RuleNamespace="SonarScannerFor.NET">
            <Rule Id="S2076" Action="{0}" />
          </Rules>
        </RuleSet>
        """;

        context.AssertCorrectAnalyzerSettings();
        context.AssertNoWarningsOrErrors();
        context.AssertInfoMessages("Provisioning analyzer assemblies for cs...");
        context.AssertCorrectRulesets(expectedSecurityRules);
        context.AssertExpectedAssemblies(@"c:\assembly1.dll", @"d:\foo\assembly2.dll");
        context.AssertExpectedPluginsRequested(
            [new() { Key = "securitycsharpfrontend", Version = "1.13.0", StaticResourceName = "SecurityAnalyzer.zip" }]);
        context.AssertExpectedAdditionalFileExists(expectedSonarLintXml);
    }

    [TestMethod]
    public void RoslynConfig_ValidProfile_Security_Frontend_LegacyOnly()
    {
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
        {
            {"sonaranalyzer.security.cs.analyzerId", "SonarAnalyzer.Security"},
            {"sonaranalyzer.security.cs.ruleNamespace", "SonarAnalyzer.Security"},
            {"sonaranalyzer.security.cs.pluginKey", "securitycsharpfrontend"},
            {"sonaranalyzer.security.cs.pluginVersion", "1.13.0"},
            {"sonaranalyzer.security.cs.staticResourceName", "SecurityAnalyzer.zip"},
            {"sonaranalyzer.security.cs.nuget.packageId", "SonarAnalyzer.CSharp"},
            {"sonaranalyzer.security.cs.nuget.packageVersion", "1.13.0"}
        });
        var context = new Context(TestContext, sonarProperties, [[@"c:\assembly1.dll"], [@"d:\foo\assembly2.dll"]], [new SonarRule("roslyn.sonaranalyzer.security.cs", "S2076", true)]);

        var expectedSonarLintXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <AnalysisInput>
              <Settings>
              </Settings>
              <Rules>
              </Rules>
              <Files>
              </Files>
            </AnalysisInput>

            """;

        var expectedSecurityRules = """
        <?xml version="1.0" encoding="utf-8"?>
        <RuleSet xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Name="Rules for SonarQube" Description="This rule set was automatically generated from SonarQube" ToolsVersion="14.0">
          <Rules AnalyzerId="SonarScannerFor.NET" RuleNamespace="SonarScannerFor.NET">
            <Rule Id="S2076" Action="{0}" />
          </Rules>
        </RuleSet>
        """;

        context.AssertCorrectAnalyzerSettings();
        context.AssertNoWarningsOrErrors();
        context.AssertInfoMessages("Provisioning analyzer assemblies for cs...");
        context.AssertCorrectRulesets(expectedSecurityRules);
        context.AssertExpectedAssemblies(@"c:\assembly1.dll", @"d:\foo\assembly2.dll");
        context.AssertExpectedPluginsRequested(
            [new() { Key = "securitycsharpfrontend", Version = "1.13.0", StaticResourceName = "SecurityAnalyzer.zip" }]);
        context.AssertExpectedAdditionalFileExists(expectedSonarLintXml);
    }

    [TestMethod]
    public void RoslynConfig_ValidProfile_Security_Frontend_And_Legacy()
    {
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
        {
            {"sonar.cs.analyzer.security.pluginKey", "securitycsharpfrontend"},
            {"sonar.cs.analyzer.security.pluginVersion", "1.13.0"},
            {"sonar.cs.analyzer.security.staticResourceName", "SecurityAnalyzer.zip"},
            {"sonaranalyzer.security.cs.analyzerId", "SonarAnalyzer.Security"},
            {"sonaranalyzer.security.cs.ruleNamespace", "SonarAnalyzer.Security"},
            {"sonaranalyzer.security.cs.pluginKey", "OLD securitycsharpfrontend"},
            {"sonaranalyzer.security.cs.pluginVersion", "OLD 1.13.0"},
            {"sonaranalyzer.security.cs.staticResourceName", "OLD SecurityAnalyzer.zip"}
        });
        var context = new Context(TestContext, sonarProperties, [[@"c:\assembly1.dll"], [@"d:\foo\assembly2.dll"]], [new SonarRule("roslyn.sonaranalyzer.security.cs", "S2076", true)]);

        var expectedSonarLintXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <AnalysisInput>
              <Settings>
                <Setting>
                  <Key>sonar.cs.analyzer.security.pluginKey</Key>
                  <Value>securitycsharpfrontend</Value>
                </Setting>
                <Setting>
                  <Key>sonar.cs.analyzer.security.pluginVersion</Key>
                  <Value>1.13.0</Value>
                </Setting>
                <Setting>
                  <Key>sonar.cs.analyzer.security.staticResourceName</Key>
                  <Value>SecurityAnalyzer.zip</Value>
                </Setting>
              </Settings>
              <Rules>
              </Rules>
              <Files>
              </Files>
            </AnalysisInput>

            """;
        var expectedSecurityRules = """
        <?xml version="1.0" encoding="utf-8"?>
        <RuleSet xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Name="Rules for SonarQube" Description="This rule set was automatically generated from SonarQube" ToolsVersion="14.0">
          <Rules AnalyzerId="SonarScannerFor.NET" RuleNamespace="SonarScannerFor.NET">
            <Rule Id="S2076" Action="{0}" />
          </Rules>
        </RuleSet>
        """;
        context.AssertCorrectAnalyzerSettings();
        context.AssertNoWarningsOrErrors();
        context.AssertInfoMessages("Provisioning analyzer assemblies for cs...");
        context.AssertCorrectRulesets(expectedSecurityRules);
        context.AssertExpectedAssemblies(@"c:\assembly1.dll", @"d:\foo\assembly2.dll");
        context.AssertExpectedPluginsRequested(
            [new() { Key = "securitycsharpfrontend", Version = "1.13.0", StaticResourceName = "SecurityAnalyzer.zip" }]);
        context.AssertExpectedAdditionalFileExists(expectedSonarLintXml);
    }

    [TestMethod]
    public void RoslynConfig_IncompletePluginDoesNotPopulate()
    {
        var sonarProperties = new ListPropertiesProvider(new Dictionary<string, string>
            {
                {"wintellect.pluginKey", "wintellect"},
                {"sonar.cs.analyzer.dotnet.analyzerId", "SonarAnalyzer.CSharp"},
                {"sonar.cs.analyzer.dotnet.ruleNamespace", "SonarAnalyzer.CSharp"},
                {"sonar.cs.analyzer.dotnet.staticResourceName", "SonarAnalyzer.zip"},
            });
        var context = new Context(TestContext, sonarProperties, [[@"c:\assembly1.dll"], [@"d:\foo\assembly2.dll"]]);
        var expectedSonarLintXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <AnalysisInput>
              <Settings>
                <Setting>
                  <Key>sonar.cs.analyzer.dotnet.analyzerId</Key>
                  <Value>SonarAnalyzer.CSharp</Value>
                </Setting>
                <Setting>
                  <Key>sonar.cs.analyzer.dotnet.ruleNamespace</Key>
                  <Value>SonarAnalyzer.CSharp</Value>
                </Setting>
                <Setting>
                  <Key>sonar.cs.analyzer.dotnet.staticResourceName</Key>
                  <Value>SonarAnalyzer.zip</Value>
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
        context.AssertInfoMessages("No Roslyn analyzer plugins were specified so no Roslyn analyzers will be run for cs");
        context.AssertCorrectRulesets();
        context.AssertExpectedAssemblies([]);
        context.AssertExpectedPluginsRequested([]);
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
            var sut = new RoslynAnalyzerProvider(analyzerInstaller, logger, CreateSettings(rootDir), properties, rules ?? CreateRules(), RoslynAnalyzerProvider.CSharpLanguage);
            ActualSettings = sut.SetupAnalyzer();
        }

        public void AssertCorrectRulesets(string expectedRules = null)
        {
            CheckRuleset(ActualSettings.RulesetPath, false, expectedRules);
            CheckRuleset(ActualSettings.DeactivatedRulesetPath, true, expectedRules);
        }

        public void AssertNoWarningsOrErrors()
        {
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);
        }

        public void AssertInfoMessages(params string[] expected)
        {
            logger.AssertMessagesLogged(expected.Length);
            foreach (var info in expected)
            {
                logger.AssertInfoLogged(info);
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

        public void AssertExpectedPluginsRequested(IEnumerable<Plugin> plugins) =>
            analyzerInstaller.AssertOnlyExpectedPluginsRequested(plugins);

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

        private void CheckRuleset(string ruleSetPath, bool isDeactivated, string expectedRules = null)
        {
            var expectedFileName = isDeactivated ? $"Sonar-{RoslynAnalyzerProvider.CSharpLanguage}-none.ruleset" : $"Sonar-{RoslynAnalyzerProvider.CSharpLanguage}.ruleset";
            var expectedRule = isDeactivated ? "None" : "Warning";
            var expectedContent = string.Format(expectedRules ?? """
                <?xml version="1.0" encoding="utf-8"?>
                <RuleSet xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Name="Rules for SonarQube" Description="This rule set was automatically generated from SonarQube" ToolsVersion="14.0">
                  <Rules AnalyzerId="SonarScannerFor.NET" RuleNamespace="SonarScannerFor.NET">
                    <Rule Id="S1116" Action="{0}" />
                    <Rule Id="S1125" Action="{0}" />
                    <Rule Id="Wintellect003" Action="{0}" />
                    <Rule Id="S1000" Action="None" />
                  </Rules>
                </RuleSet>
                """, expectedRule);
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
