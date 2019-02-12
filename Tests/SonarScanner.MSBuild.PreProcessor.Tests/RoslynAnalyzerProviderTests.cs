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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Client.Models;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn;
using SonarScanner.MSBuild.TFS;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Tests
{
    [TestClass]
    public class RoslynAnalyzerProviderTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

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
            // Arrange
            var logger = new TestLogger();
            var activeRules = new List<SonarQubeRule>();
            var inactiveRules = new List<SonarQubeRule>();
            var pluginKey = RoslynAnalyzerProvider.CSharpPluginKey;
            IDictionary<string, string> serverSettings = new Dictionary<string, string>();
            var settings = CreateSettings(TestUtils.CreateTestSpecificFolder(TestContext));

            var testSubject = CreateTestSubject(logger);

            // Act and assert
            Action act = () => testSubject.SetupAnalyzer(null, serverSettings, activeRules, inactiveRules, pluginKey);
            act.Should().ThrowExactly<ArgumentNullException>();
            act = () => testSubject.SetupAnalyzer(settings, null, activeRules, inactiveRules, pluginKey);
            act.Should().ThrowExactly<ArgumentNullException>();
            act = () => testSubject.SetupAnalyzer(settings, serverSettings, null, inactiveRules, pluginKey);
            act.Should().ThrowExactly<ArgumentNullException>();
            act = () => testSubject.SetupAnalyzer(settings, serverSettings, activeRules, null, pluginKey);
            act.Should().ThrowExactly<ArgumentNullException>();
            act = () => testSubject.SetupAnalyzer(settings, serverSettings, activeRules, inactiveRules, null);
            act.Should().ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        public void RoslynConfig_NoActiveRules()
        {
            // Arrange
            var logger = new TestLogger();
            var activeRules = new List<SonarQubeRule>();
            var inactiveRules = new List<SonarQubeRule>();
            var pluginKey = "csharp";
            IDictionary<string, string> serverSettings = new Dictionary<string, string>();
            var settings = CreateSettings(TestUtils.CreateTestSpecificFolder(TestContext));

            var testSubject = CreateTestSubject(logger);

            // Act and assert
            testSubject.SetupAnalyzer(settings, serverSettings, activeRules, inactiveRules, pluginKey).Should().NotBeNull();
        }

        [TestMethod]
        public void RoslynConfig_NoAssemblies()
        {
            // Arrange
            var rootFolder = CreateTestFolders();
            var logger = new TestLogger();
            var activeRules = createActiveRules();
            var inactiveRules = createInactiveRules();
            var language = RoslynAnalyzerProvider.CSharpLanguage;

            // missing properties to get plugin related properties
            IDictionary<string, string> serverSettings = new Dictionary<string, string>
            {
                { "wintellect.analyzerId", "Wintellect.Analyzers" },
                { "wintellect.ruleNamespace", "Wintellect.Analyzers" },
                { "sonaranalyzer-cs.analyzerId", "SonarAnalyzer.CSharp" },
                { "sonaranalyzer-cs.ruleNamespace", "SonarAnalyzer.CSharp" }
            };

            var mockInstaller = new MockAnalyzerInstaller
            {
                AssemblyPathsToReturn = new HashSet<string>(new string[] { "c:\\assembly1.dll", "d:\\foo\\assembly2.dll" })
            };
            var settings = CreateSettings(rootFolder);

            var testSubject = new RoslynAnalyzerProvider(mockInstaller, logger);

            // Act
            var actualSettings = testSubject.SetupAnalyzer(settings, serverSettings, activeRules, inactiveRules, language);

            // Assert
            CheckSettingsInvariants(actualSettings);
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);

            CheckRuleset(actualSettings, rootFolder, language);
            actualSettings.AnalyzerAssemblyPaths.Should().BeEmpty();
            var plugins = new List<string>();
            mockInstaller.AssertExpectedPluginsRequested(plugins);
        }

        [TestMethod]
        public void RoslynConfig_GetRoslynFormatName()
        {
            RoslynAnalyzerProvider.GetRoslynFormatName(RoslynAnalyzerProvider.CSharpLanguage).Should().Be("roslyn-cs");
        }

        [TestMethod]
        public void RoslynConfig_ValidProfile()
        {
            // Arrange
            var rootFolder = CreateTestFolders();
            var logger = new TestLogger();
            var activeRules = createActiveRules();
            var inactiveRules = createInactiveRules();
            var language = RoslynAnalyzerProvider.CSharpLanguage;
            var mockInstaller = new MockAnalyzerInstaller
            {
                AssemblyPathsToReturn = new HashSet<string>(new string[] { "c:\\assembly1.dll", "d:\\foo\\assembly2.dll" })
            };
            var settings = CreateSettings(rootFolder);

            var testSubject = new RoslynAnalyzerProvider(mockInstaller, logger);

            // Act
            var actualSettings = testSubject.SetupAnalyzer(settings, ServerSettings, activeRules, inactiveRules, language);

            // Assert
            CheckSettingsInvariants(actualSettings);
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);

            CheckRuleset(actualSettings, rootFolder, language);
            CheckExpectedAdditionalFiles(actualSettings);
            CheckExpectedAssemblies(actualSettings, "c:\\assembly1.dll", "d:\\foo\\assembly2.dll");
            var plugins = new List<string>
            {
                "wintellect",
                "csharp"
            };
            mockInstaller.AssertExpectedPluginsRequested(plugins);
        }

        #endregion Tests

        #region Private methods

        private List<SonarQubeRule> createInactiveRules()
        {
            var list = new List<SonarQubeRule>
            {
                new SonarQubeRule("S1000", "csharpsquid", false)
            };
            return list;
        }

        private List<SonarQubeRule> createActiveRules()
        {
            /*
            <Rules AnalyzerId=""SonarLint.CSharp"" RuleNamespace=""SonarLint.CSharp"">
              <Rule Id=""S1116"" Action=""Warning""/>
              <Rule Id=""S1125"" Action=""Warning""/>
            </Rules>
            <Rules AnalyzerId=""Wintellect.Analyzers"" RuleNamespace=""Wintellect.Analyzers"">
              <Rule Id=""Wintellect003"" Action=""Warning""/>
            </Rules>
            */
            var rules = new List<SonarQubeRule>();
            var ruleWithParameter = new SonarQubeRule("S1116", "csharpsquid", true, new Dictionary<string, string>
            {
                { "key", "value" }
            });
            rules.Add(ruleWithParameter);
            rules.Add(new SonarQubeRule("S1125", "csharpsquid", true));
            rules.Add(new SonarQubeRule("Wintellect003", "roslyn.wintellect", true));

            return rules;
        }

        private static readonly IDictionary<string, string> ServerSettings = new Dictionary<string, string>
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
            {"sonaranalyzer-cs.nuget.packageVersion", "1.13.0"}
        };

        private string CreateTestFolders()
        {
            var rootFolder = TestUtils.CreateTestSpecificFolder(TestContext);

            // Create the binary and conf folders that are created by the bootstrapper
            Directory.CreateDirectory(GetBinaryPath(rootFolder));
            Directory.CreateDirectory(GetConfPath(rootFolder));

            return rootFolder;
        }

        private static RoslynAnalyzerProvider CreateTestSubject(ILogger logger)
        {
            var testSubject = new RoslynAnalyzerProvider(new MockAnalyzerInstaller(), logger);
            return testSubject;
        }

        private static TeamBuildSettings CreateSettings(string rootDir)
        {
            var settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(rootDir);
            return settings;
        }

        private static string GetExpectedRulesetFilePath(string rootDir, string language)
        {
            return Path.Combine(GetConfPath(rootDir), RoslynAnalyzerProvider.GetRoslynRulesetFileName(language));
        }

        private static string GetConfPath(string rootDir)
        {
            return Path.Combine(rootDir, "conf");
        }

        private static string GetBinaryPath(string rootDir)
        {
            return Path.Combine(rootDir, "bin");
        }

        #endregion Private methods

        #region Checks

        private static void CheckSettingsInvariants(AnalyzerSettings actualSettings)
        {
            actualSettings.Should().NotBeNull("Not expecting the config to be null");
            actualSettings.AdditionalFilePaths.Should().NotBeNull();
            actualSettings.AnalyzerAssemblyPaths.Should().NotBeNull();
            string.IsNullOrEmpty(actualSettings.RuleSetFilePath).Should().BeFalse();

            // Any file paths returned in the config should exist
            foreach (var filePath in actualSettings.AdditionalFilePaths)
            {
                File.Exists(filePath).Should().BeTrue("Expected additional file does not exist: {0}", filePath);
            }
            File.Exists(actualSettings.RuleSetFilePath).Should().BeTrue("Specified ruleset does not exist: {0}", actualSettings.RuleSetFilePath);
        }

        private void CheckRuleset(AnalyzerSettings actualSettings, string rootTestDir, string language)
        {
            string.IsNullOrWhiteSpace(actualSettings.RuleSetFilePath).Should().BeFalse("Ruleset file path should be set");
            Path.IsPathRooted(actualSettings.RuleSetFilePath).Should().BeTrue("Ruleset file path should be absolute");
            File.Exists(actualSettings.RuleSetFilePath).Should().BeTrue("Specified ruleset file does not exist: {0}", actualSettings.RuleSetFilePath);
            TestContext.AddResultFile(actualSettings.RuleSetFilePath);

            CheckFileIsXml(actualSettings.RuleSetFilePath);

            Path.GetFileName(actualSettings.RuleSetFilePath).Should().Be(RoslynAnalyzerProvider.GetRoslynRulesetFileName(language), "Ruleset file does not have the expected name");

            var expectedFilePath = GetExpectedRulesetFilePath(rootTestDir, language);
            actualSettings.RuleSetFilePath.Should().Be(expectedFilePath, "Ruleset was not written to the expected location");

            var expectedContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" Name=""Rules for SonarQube"" Description=""This rule set was automatically generated from SonarQube"" ToolsVersion=""14.0"">
  <Rules AnalyzerId=""SonarAnalyzer.CSharp"" RuleNamespace=""SonarAnalyzer.CSharp"">
    <Rule Id=""S1116"" Action=""Warning"" />
    <Rule Id=""S1125"" Action=""Warning"" />
    <Rule Id=""S1000"" Action=""None"" />
  </Rules>
  <Rules AnalyzerId=""Wintellect.Analyzers"" RuleNamespace=""Wintellect.Analyzers"">
    <Rule Id=""Wintellect003"" Action=""Warning"" />
  </Rules>
</RuleSet>";
            File.ReadAllText(actualSettings.RuleSetFilePath).Should().Be(expectedContent, "Ruleset file does not have the expected content: {0}", actualSettings.RuleSetFilePath);
        }

        private void CheckExpectedAdditionalFiles(AnalyzerSettings actualSettings)
        {
            // Currently, only SonarLint.xml is written
            var filePaths = actualSettings.AdditionalFilePaths;
            filePaths.Should().ContainSingle();

            CheckExpectedAdditionalFileExists("SonarLint.xml", @"<?xml version=""1.0"" encoding=""UTF-8""?>
<AnalysisInput>
  <Settings>
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
", actualSettings);
        }

        private void CheckExpectedAdditionalFileExists(string expectedFileName, string expectedContent, AnalyzerSettings actualSettings)
        {
            // Check one file of the expected name exists
            var matches = actualSettings.AdditionalFilePaths.Where(actual => string.Equals(expectedFileName, Path.GetFileName(actual), System.StringComparison.OrdinalIgnoreCase));
            matches.Should().ContainSingle("Unexpected number of files named \"{0}\". One and only one expected", expectedFileName);

            // Check the file exists and has the expected content
            var actualFilePath = matches.First();
            File.Exists(actualFilePath).Should().BeTrue("AdditionalFile does not exist: {0}", actualFilePath);

            // Dump the contents to help with debugging
            TestContext.AddResultFile(actualFilePath);
            TestContext.WriteLine("File contents: {0}", actualFilePath);
            TestContext.WriteLine(File.ReadAllText(actualFilePath));
            TestContext.WriteLine("");

            if (expectedContent != null) // null expected means "don't check"
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
                actualSettings.AnalyzerAssemblyPaths.Contains(expectedItem, StringComparer.OrdinalIgnoreCase)
                    .Should().BeTrue("Expected assembly file path was not returned: {0}", expectedItem);
            }
            actualSettings.AnalyzerAssemblyPaths.Should().HaveCount(expected.Length, "Too many assembly file paths returned");
        }

        #endregion Checks
    }
}
