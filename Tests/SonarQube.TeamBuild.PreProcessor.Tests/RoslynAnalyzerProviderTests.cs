/*
 * SonarQube Scanner for MSBuild
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.PreProcessor.Roslyn;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class RoslynAnalyzerProviderTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void RoslynConfig_ConstructorArgumentChecks()
        {
            AssertException.Expects<ArgumentNullException>(() => new RoslynAnalyzerProvider(null, new TestLogger()));
            AssertException.Expects<ArgumentNullException>(() => new RoslynAnalyzerProvider(new MockAnalyzerInstaller(), null));
        }

        [TestMethod]
        public void RoslynConfig_SetupAnalyzers_ArgumentChecks()
        {
            // Arrange
            var logger = new TestLogger();
            IList<ActiveRule> activeRules = new List<ActiveRule>();
            IList<string> inactiveRules = new List<string>();
            var pluginKey = RoslynAnalyzerProvider.CSharpPluginKey;
            IDictionary<string, string> serverSettings = new Dictionary<string, string>();
            var settings = CreateSettings(TestContext.DeploymentDirectory);

            var testSubject = CreateTestSubject(logger);

            // Act and assert
            AssertException.Expects<ArgumentNullException>(() => testSubject.SetupAnalyzer(null, serverSettings, activeRules, inactiveRules, pluginKey));
            AssertException.Expects<ArgumentNullException>(() => testSubject.SetupAnalyzer(settings, null, activeRules, inactiveRules, pluginKey));
            AssertException.Expects<ArgumentNullException>(() => testSubject.SetupAnalyzer(settings, serverSettings, null, inactiveRules, pluginKey));
            AssertException.Expects<ArgumentNullException>(() => testSubject.SetupAnalyzer(settings, serverSettings, activeRules, null, pluginKey));
            AssertException.Expects<ArgumentNullException>(() => testSubject.SetupAnalyzer(settings, serverSettings, activeRules, inactiveRules, null));
        }

        [TestMethod]
        public void RoslynConfig_NoActiveRules()
        {
            // Arrange
            var logger = new TestLogger();
            IList<ActiveRule> activeRules = new List<ActiveRule>();
            IList<string> inactiveRules = new List<string>();
            var pluginKey = "csharp";
            IDictionary<string, string> serverSettings = new Dictionary<string, string>();
            var settings = CreateSettings(TestContext.DeploymentDirectory);

            var testSubject = CreateTestSubject(logger);

            // Act and assert
            Assert.IsNull(testSubject.SetupAnalyzer(settings, serverSettings, activeRules, inactiveRules, pluginKey));
        }

        [TestMethod]
        public void RoslynConfig_NoAssemblies()
        {
            // Arrange
            var rootFolder = CreateTestFolders();
            var logger = new TestLogger();
            IList<ActiveRule> activeRules = createActiveRules();
            IList<string> inactiveRules = createInactiveRules();
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
            Assert.IsTrue(!actualSettings.AnalyzerAssemblyPaths.Any());
            var plugins = new List<string>();
            mockInstaller.AssertExpectedPluginsRequested(plugins);
        }

        [TestMethod]
        public void RoslynConfig_GetRoslynFormatName()
        {
            Assert.AreEqual("roslyn-cs", RoslynAnalyzerProvider.GetRoslynFormatName(RoslynAnalyzerProvider.CSharpLanguage));
        }

        [TestMethod]
        public void RoslynConfig_ValidProfile()
        {
            // Arrange
            var rootFolder = CreateTestFolders();
            var logger = new TestLogger();
            IList<ActiveRule> activeRules = createActiveRules();
            IList<string> inactiveRules = createInactiveRules();
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
            CheckExpectedAdditionalFiles(rootFolder, language, settings.SonarOutputDirectory, actualSettings);
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

        private List<string> createInactiveRules()
        {
            var list = new List<string>
            {
                "csharpsquid:S1000"
            };
            return list;
        }

        private List<ActiveRule> createActiveRules()
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
            var rules = new List<ActiveRule>();
            var ruleWithParameter = new ActiveRule("csharpsquid", "S1116");
            var p = new Dictionary<string, string>
            {
                { "key", "value" }
            };
            ruleWithParameter.Parameters = p;
            rules.Add(ruleWithParameter);
            rules.Add(new ActiveRule("csharpsquid", "S1125"));
            rules.Add(new ActiveRule("roslyn.wintellect", "Wintellect003"));

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
            Assert.IsNotNull(actualSettings, "Not expecting the config to be null");
            Assert.IsNotNull(actualSettings.AdditionalFilePaths);
            Assert.IsNotNull(actualSettings.AnalyzerAssemblyPaths);
            Assert.IsFalse(string.IsNullOrEmpty(actualSettings.RuleSetFilePath));

            // Any file paths returned in the config should exist
            foreach (var filePath in actualSettings.AdditionalFilePaths)
            {
                Assert.IsTrue(File.Exists(filePath), "Expected additional file does not exist: {0}", filePath);
            }
            Assert.IsTrue(File.Exists(actualSettings.RuleSetFilePath), "Specified ruleset does not exist: {0}", actualSettings.RuleSetFilePath);
        }

        private void CheckRuleset(AnalyzerSettings actualSettings, string rootTestDir, string language)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(actualSettings.RuleSetFilePath), "Ruleset file path should be set");
            Assert.IsTrue(Path.IsPathRooted(actualSettings.RuleSetFilePath), "Ruleset file path should be absolute");
            Assert.IsTrue(File.Exists(actualSettings.RuleSetFilePath), "Specified ruleset file does not exist: {0}", actualSettings.RuleSetFilePath);
            TestContext.AddResultFile(actualSettings.RuleSetFilePath);

            CheckFileIsXml(actualSettings.RuleSetFilePath);

            Assert.AreEqual(RoslynAnalyzerProvider.GetRoslynRulesetFileName(language), Path.GetFileName(actualSettings.RuleSetFilePath), "Ruleset file does not have the expected name");

            var expectedFilePath = GetExpectedRulesetFilePath(rootTestDir, language);
            Assert.AreEqual(expectedFilePath, actualSettings.RuleSetFilePath, "Ruleset was not written to the expected location");

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
            Assert.AreEqual(expectedContent, File.ReadAllText(actualSettings.RuleSetFilePath), "Ruleset file does not have the expected content: {0}", actualSettings.RuleSetFilePath);
        }

        private void CheckExpectedAdditionalFiles(string rootTestDir, string language, string outDir, AnalyzerSettings actualSettings)
        {
            // Currently, only SonarLint.xml is written
            var filePaths = actualSettings.AdditionalFilePaths;
            Assert.AreEqual(filePaths.Count(), 1);

            //string expectedContent = expected.AdditionalFiles[expectedFileName];
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
            Assert.AreEqual(1, matches.Count(), "Unexpected number of files named \"{0}\". One and only one expected", expectedFileName);

            // Check the file exists and has the expected content
            var actualFilePath = matches.First();
            Assert.IsTrue(File.Exists(actualFilePath), "AdditionalFile does not exist: {0}", actualFilePath);

            // Dump the contents to help with debugging
            TestContext.AddResultFile(actualFilePath);
            TestContext.WriteLine("File contents: {0}", actualFilePath);
            TestContext.WriteLine(File.ReadAllText(actualFilePath));
            TestContext.WriteLine("");

            if (expectedContent != null) // null expected means "don't check"
            {
                Assert.AreEqual(expectedContent, File.ReadAllText(actualFilePath), "Additional file does not have the expected content: {0}", expectedFileName);
            }
        }

        private static void CheckFileIsXml(string fullPath)
        {
            var doc = new XmlDocument();
            doc.Load(fullPath);
            Assert.IsNotNull(doc.FirstChild, "Expecting the file to contain some valid XML");
        }

        private static void CheckExpectedAssemblies(AnalyzerSettings actualSettings, params string[] expected)
        {
            foreach (var expectedItem in expected)
            {
                Assert.IsTrue(actualSettings.AnalyzerAssemblyPaths.Contains(expectedItem, StringComparer.OrdinalIgnoreCase),
                    "Expected assembly file path was not returned: {0}", expectedItem);
            }
            Assert.AreEqual(expected.Length, actualSettings.AnalyzerAssemblyPaths.Count(), "Too many assembly file paths returned");
        }

        #endregion Checks

        /*
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void RoslynConfig_PluginNotInstalled()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", null, "valid.profile");
            mockServer.Data.InstalledPlugins.Remove(RoslynAnalyzerProvider.CSharpPluginKey);

            RoslynAnalyzerProvider testSubject = CreateTestSubject(logger);

            // Act
            IEnumerable<AnalyzerSettings> actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            AssertNoRulesetFiles(actualSettings, rootDir);

            logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        public void RoslynConfig_ProjectNotInProfile()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", null, "valid.profile");

            RoslynAnalyzerProvider testSubject = CreateTestSubject(logger);

            // Act
            IEnumerable<AnalyzerSettings> actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "unknown.project", null);

            // Assert
            AssertNoRulesetFiles(actualSettings, rootDir);

            logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        public void RoslynConfig_ProfileExportIsUnavailable_FailsGracefully()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            // Create a server that doesn't export the expected format (simulates
            // calling an older plugin version)
            MockSonarQubeServer mockServer = CreateValidServer("valid.project", null, "valid.profile");
            QualityProfile csProfile = mockServer.Data.FindProfile("valid.profile", RoslynAnalyzerProvider.CSharpLanguage);
            csProfile.SetExport(RoslynAnalyzerProvider.GetRoslynFormatName(RoslynAnalyzerProvider.CSharpLanguage), null);

            RoslynAnalyzerProvider testSubject = CreateTestSubject(logger);

            // Act
            IEnumerable<AnalyzerSettings> actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            AssertNoRulesetFiles(actualSettings, rootDir);

            logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        public void RoslynConfig_MissingRuleset()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", null, "valid.profile");
            QualityProfile csProfile = mockServer.Data.FindProfile("valid.profile", RoslynAnalyzerProvider.CSharpLanguage);
            csProfile.SetExport(RoslynAnalyzerProvider.GetRoslynFormatName(RoslynAnalyzerProvider.CSharpLanguage), @"<?xml version=""1.0"" encoding=""utf-8""?>
<RoslynExportProfile Version=""1.0="">
  <Configuration>
    <!-- Missing ruleset -->
    <AdditionalFiles>
      <AdditionalFile FileName=""SonarLint.xml"" >
      </AdditionalFile>
    </AdditionalFiles>
  </Configuration>
  <Deployment />
</RoslynExportProfile>");

            RoslynAnalyzerProvider testSubject = CreateTestSubject(logger);

            // Act
            IEnumerable<AnalyzerSettings> actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            AssertNoRulesetFiles(actualSettings, rootDir);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void RoslynConfig_BranchMissingRuleset()
        {
            // This test is a regression scenario for SONARMSBRU-187:
            // We do not expect the project profile to be returned if we ask for a branch-specific profile

            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            WellKnownProfile testProfile = CreateValidCSharpProfile();
            MockSonarQubeServer mockServer = CreateServer("valid.project", null, "valid.profile", testProfile);

            MockAnalyzerInstaller mockInstaller = new MockAnalyzerInstaller();
            mockInstaller.AssemblyPathsToReturn = new HashSet<string>(new string[] { "c:\\assembly1.dll", "d:\\foo\\assembly2.dll" });

            RoslynAnalyzerProvider testSubject = new RoslynAnalyzerProvider(mockInstaller, logger);

            // Act
            IEnumerable<AnalyzerSettings> actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", "missingBranch");

            // Assert
            AssertNoRulesetFiles(actualSettings, rootDir);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void RoslynConfig_ValidProfile()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            WellKnownProfile testProfile = CreateValidCSharpProfile();
            MockSonarQubeServer mockServer = CreateServer("valid.project", null, "valid.profile", testProfile);

            MockAnalyzerInstaller mockInstaller = new MockAnalyzerInstaller();
            mockInstaller.AssemblyPathsToReturn = new HashSet<string>(new string[] { "c:\\assembly1.dll", "d:\\foo\\assembly2.dll" });

            RoslynAnalyzerProvider testSubject = new RoslynAnalyzerProvider(mockInstaller, logger);

            // Act
            IEnumerable<AnalyzerSettings> actualSettingsEnum = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            CheckContainsOneAnalyzer(actualSettingsEnum);
            AnalyzerSettings actualSettings = actualSettingsEnum.First();
            CheckSettingsInvariants(actualSettings);
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);

            CheckRuleset(actualSettings, rootDir, RoslynAnalyzerProvider.CSharpLanguage);
            CheckExpectedAdditionalFiles(testProfile, actualSettings);

            mockInstaller.AssertExpectedPluginsRequested(testProfile.Plugins);
            CheckExpectedAssemblies(actualSettings, "c:\\assembly1.dll", "d:\\foo\\assembly2.dll");
        }

        [TestMethod]
        public void RoslynConfig_MultipleLanguages()
        {
            string profileName = "valid.profile";
            string projectKey = "projectKey";
            string projectBranch = null;

            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            // Add CSharp
            WellKnownProfile testCSProfile = CreateValidCSharpProfile();
            MockSonarQubeServer mockServer = CreateServer(projectKey, projectBranch, profileName, testCSProfile);

            // Add VBNet
            WellKnownProfile testVBNetProfile = CreateValidVBNetProfile();
            mockServer.Data.AddQualityProfile(profileName, RoslynAnalyzerProvider.VBNetLanguage)
             .AddProject(projectKey)
             .AddProject("dummy")
             .SetExport(testVBNetProfile.Format, testVBNetProfile.Content);

            mockServer.Data.InstalledPlugins.Add(RoslynAnalyzerProvider.VBNetPluginKey);
            mockServer.Data.AddRepository(RoslynAnalyzerProvider.VBNetRepositoryKey, RoslynAnalyzerProvider.VBNetLanguage);

            // Test
            MockAnalyzerInstaller mockInstaller = new MockAnalyzerInstaller();
            mockInstaller.AssemblyPathsToReturn = new HashSet<string>(new string[] { "c:\\assembly1.dll", "d:\\foo\\assembly2.dll" });

            RoslynAnalyzerProvider testSubject = new RoslynAnalyzerProvider(mockInstaller, logger);

            // Act
            IEnumerable<AnalyzerSettings> actualSettingsEnum = testSubject.SetupAnalyzer(mockServer, settings, projectKey, null);

            // Assert
            Assert.IsTrue(actualSettingsEnum.Count() == 2, "Expecting settings list to have 2 elements (CS and VBNet), but had {0}", actualSettingsEnum.Count());

            foreach (AnalyzerSettings actualSettings in actualSettingsEnum)
            {
                CheckSettingsInvariants(actualSettings);
                CheckRuleset(actualSettings, rootDir, actualSettings.Language);
                CheckExpectedAssemblies(actualSettings, "c:\\assembly1.dll", "d:\\foo\\assembly2.dll");
            }

            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);

            mockInstaller.AssertExpectedPluginsRequested(testVBNetProfile.Plugins);
            mockInstaller.AssertExpectedPluginsRequested(testCSProfile.Plugins);
        }

        [TestMethod]
        public void RoslynConfig_ValidProfile_BranchSpecific()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            // Differentiate the branch-specific and non-branch-specific profiles
            WellKnownProfile nonBranchSpecificProfile = CreateValidCSharpProfile();
            WellKnownProfile branchSpecificProfile = CreateValidCSharpProfile();
            branchSpecificProfile.AssemblyFilePaths.Add("e:\\assembly3.dll");

            MockSonarQubeServer mockServer = CreateServer("valid.project", null, "valid.profile", nonBranchSpecificProfile);
            AddWellKnownProfileToServer("valid.project", "aBranch", "valid.anotherProfile", branchSpecificProfile, mockServer);

            MockAnalyzerInstaller mockInstaller = new MockAnalyzerInstaller();
            mockInstaller.AssemblyPathsToReturn = new HashSet<string>(new string[] { "c:\\assembly1.dll", "d:\\foo\\assembly2.dll", "e:\\assembly3.dll" });

            RoslynAnalyzerProvider testSubject = new RoslynAnalyzerProvider(mockInstaller, logger);

            // Act
            IEnumerable<AnalyzerSettings> actualSettingsEnum = testSubject.SetupAnalyzer(mockServer, settings, "valid.project", "aBranch");

            // Assert
            CheckContainsOneAnalyzer(actualSettingsEnum);
            AnalyzerSettings actualSettings = actualSettingsEnum.First();
            CheckSettingsInvariants(actualSettings);
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);

            CheckRuleset(actualSettings, rootDir, RoslynAnalyzerProvider.CSharpLanguage);
            CheckExpectedAdditionalFiles(branchSpecificProfile, actualSettings);

            mockInstaller.AssertExpectedPluginsRequested(branchSpecificProfile.Plugins);
            CheckExpectedAssemblies(actualSettings, "c:\\assembly1.dll", "d:\\foo\\assembly2.dll", "e:\\assembly3.dll");
        }

        [TestMethod]
        public void RoslynConfig_ValidRealSonarLintProfile()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            WellKnownProfile testProfile = CreateRealSonarLintProfile();
            MockSonarQubeServer mockServer = CreateServer("valid.project", null, "valid.profile", testProfile);

            RoslynAnalyzerProvider testSubject = CreateTestSubject(logger);

            // Act
            IEnumerable<AnalyzerSettings> actualSettingsEnum = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            CheckContainsOneAnalyzer(actualSettingsEnum);
            AnalyzerSettings actualSettings = actualSettingsEnum.First();
            CheckSettingsInvariants(actualSettings);
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);
            CheckRuleset(actualSettings, rootDir, RoslynAnalyzerProvider.CSharpLanguage);
            CheckExpectedAdditionalFiles(testProfile, actualSettings);

            // Check the additional file is valid XML
            Assert.AreEqual(1, actualSettings.AdditionalFilePaths.Count(), "Test setup error: expecting only one additional file. Check the sample export XML has not changed");
            string filePath = actualSettings.AdditionalFilePaths.First();
            CheckFileIsXml(filePath);
        }

        [TestMethod]
        public void RoslynConfig_MissingAdditionalFileName_AdditionalFileIgnored()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", null, "valid.profile");
            QualityProfile csProfile = mockServer.Data.FindProfile("valid.profile", RoslynAnalyzerProvider.CSharpLanguage);

            string expectedFileContent = "bar";

            csProfile.SetExport(RoslynAnalyzerProvider.GetRoslynFormatName(RoslynAnalyzerProvider.CSharpLanguage), @"<?xml version=""1.0"" encoding=""utf-8""?>
<RoslynExportProfile Version=""1.0="">
  <Configuration>
    <RuleSet />
    <AdditionalFiles>
      <AdditionalFile /> <!-- Missing file name -->
      <AdditionalFile FileName=""foo.txt"" >" + GetBase64EncodedString(expectedFileContent) +  @"</AdditionalFile>
    </AdditionalFiles>
  </Configuration>
  <Deployment />
</RoslynExportProfile>");

            RoslynAnalyzerProvider testSubject = CreateTestSubject(logger);

            // Act
            IEnumerable<AnalyzerSettings> actualSettingsEnum = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            CheckContainsOneAnalyzer(actualSettingsEnum);
            AnalyzerSettings actualSettings = actualSettingsEnum.First();
            CheckSettingsInvariants(actualSettings);
            CheckRuleset(actualSettings, rootDir, RoslynAnalyzerProvider.CSharpLanguage);
            CheckExpectedAdditionalFileExists("foo.txt", expectedFileContent, actualSettings);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void RoslynConfig_DuplicateAdditionalFileName_DuplicateFileIgnored()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            string expectedFileContent = "expected";
            string unexpectedFileContent = "not expected: file should already exist with the expected content";

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", null, "valid.profile");
            QualityProfile csProfile = mockServer.Data.FindProfile("valid.profile", RoslynAnalyzerProvider.CSharpLanguage);
            csProfile.SetExport(RoslynAnalyzerProvider.GetRoslynFormatName(RoslynAnalyzerProvider.CSharpLanguage), @"<?xml version=""1.0"" encoding=""utf-8""?>
<RoslynExportProfile Version=""1.0="">
  <Configuration>
    <RuleSet />
    <AdditionalFiles>
      <AdditionalFile FileName=""foo.txt"" >" + GetBase64EncodedString(expectedFileContent) + @"</AdditionalFile>
      <AdditionalFile FileName=""foo.txt"" >" + GetBase64EncodedString(unexpectedFileContent) + @"</AdditionalFile>
      <AdditionalFile FileName=""file2.txt""></AdditionalFile>
    </AdditionalFiles>
  </Configuration>
  <Deployment />
</RoslynExportProfile>");

            RoslynAnalyzerProvider testSubject = CreateTestSubject(logger);

            // Act
            IEnumerable<AnalyzerSettings> actualSettingsEnum = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            CheckContainsOneAnalyzer(actualSettingsEnum);
            AnalyzerSettings actualSettings = actualSettingsEnum.First();
            CheckSettingsInvariants(actualSettings);
            CheckRuleset(actualSettings, rootDir, RoslynAnalyzerProvider.CSharpLanguage);
            CheckExpectedAdditionalFileExists("foo.txt", expectedFileContent, actualSettings);
            CheckExpectedAdditionalFileExists("file2.txt", string.Empty, actualSettings);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void RoslynConfig_NoAnalyzerAssemblies_Succeeds()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", null, "valid.profile");
            QualityProfile csProfile = mockServer.Data.FindProfile("valid.profile", RoslynAnalyzerProvider.CSharpLanguage);
            csProfile.SetExport(RoslynAnalyzerProvider.GetRoslynFormatName(RoslynAnalyzerProvider.CSharpLanguage), @"<?xml version=""1.0"" encoding=""utf-8""?>
<RoslynExportProfile Version=""1.0="">
  <Configuration>
    <RuleSet />
    <AdditionalFiles />
  </Configuration>
  <Deployment>
    <Plugins /> <!-- empty -->
  </Deployment>
</RoslynExportProfile>");

            RoslynAnalyzerProvider testSubject = CreateTestSubject(logger);

            // Act
            IEnumerable<AnalyzerSettings> actualSettingsEnum = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            CheckContainsOneAnalyzer(actualSettingsEnum);
            AnalyzerSettings actualSettings = actualSettingsEnum.First();
            CheckSettingsInvariants(actualSettings);

            CheckExpectedAssemblies(actualSettings  );

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        #endregion Tests

        #region Private methods

        private static string GetBase64EncodedString(string text)
        {
            return System.Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        }

        #endregion Private methods

        #region Checks

        private static void AssertNoRulesetFiles(IEnumerable<AnalyzerSettings> actualSettings, string rootTestDir)
        {
            Assert.IsNotNull(actualSettings, "Not expecting the settings to be null");

            string filePath = GetExpectedRulesetFilePath(rootTestDir, RoslynAnalyzerProvider.CSharpLanguage);
            Assert.IsFalse(File.Exists(filePath), "Not expecting the ruleset file to exist: {0}", filePath);

            filePath = GetExpectedRulesetFilePath(rootTestDir, RoslynAnalyzerProvider.VBNetLanguage);
            Assert.IsFalse(File.Exists(filePath), "Not expecting the ruleset file to exist: {0}", filePath);
        }

        #endregion Checks

        */
    }
}
