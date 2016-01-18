//-----------------------------------------------------------------------
// <copyright file="RoslynAnalyzerProviderTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.PreProcessor.Roslyn;
using System.IO;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class RoslynAnalyzerProviderTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void RoslynConfig_PluginNotInstalled()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", "valid.profile");
            mockServer.Data.InstalledPlugins.Remove(RoslynAnalyzerProvider.CSharpPluginKey);

            // Act
            CompilerAnalyzerConfig actualConfig = RoslynAnalyzerProvider.SetupAnalyzers(mockServer, settings, "valid.project", logger);

            // Assert
            AssertAnalyzerConfigNotPerformed(actualConfig, rootDir);

            logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        public void RoslynConfig_ProjectNotInProfile()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", "valid.profile");

            // Act
            CompilerAnalyzerConfig actualConfig = RoslynAnalyzerProvider.SetupAnalyzers(mockServer, settings, "unknown.project", logger);

            // Assert
            AssertAnalyzerConfigNotPerformed(actualConfig, rootDir);

            logger.AssertErrorsLogged(0);
        }


        [TestMethod]
        public void RoslynConfig_MissingRuleset()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", "valid.profile");
            QualityProfile csProfile = mockServer.Data.FindProfile("valid.profile", RoslynAnalyzerProvider.CSharpLanguage);
            csProfile.SetExport(RoslynAnalyzerProvider.RoslynCSharpFormatName, @"<?xml version=""1.0"" encoding=""utf-8""?>
<RoslynExportProfile Version=""1.0="">
  <Configuration>
    <!-- Missing ruleset -->
    <AdditionalFiles>
      <AdditionalFile FileName=""SonarLint.xml"" >
      </AdditionalFile>
    </AdditionalFiles>
  </Configuration>
  <Deployment>
    <NuGetPackages>
      <Package Id=""SonarLint"" Version=""1.3.0""/>
    </NuGetPackages>
  </Deployment>
</RoslynExportProfile>");

            // Act
            CompilerAnalyzerConfig actualConfig = RoslynAnalyzerProvider.SetupAnalyzers(mockServer, settings, "valid.project", logger);

            // Assert
            AssertAnalyzerConfigNotPerformed(actualConfig, rootDir);

            logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        public void RoslynConfig_ValidProfile()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);
            MockSonarQubeServer mockServer = CreateValidServer("valid.project", "valid.profile");

            // Act
            CompilerAnalyzerConfig actualConfig = RoslynAnalyzerProvider.SetupAnalyzers(mockServer, settings, "valid.project", logger);

            // Assert
            Assert.IsNotNull(actualConfig);

            CheckRulesetExists(actualConfig, rootDir);
        }

        #endregion


        #region Private methods

        private string CreateTestFolders()
        {
            string rootFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // Create the binary and conf folders that are created by the bootstrapper
            Directory.CreateDirectory(GetBinaryPath(rootFolder));
            Directory.CreateDirectory(GetConfPath(rootFolder));

            return rootFolder;
        }

        /// <summary>
        /// Creates and returns a mock server that is correctly configured to return
        /// a Roslyn ruleset for the specified project key and profile
        /// </summary>
        private MockSonarQubeServer CreateValidServer(string validProjectKey, string validProfileName)
        {
            ServerDataModel model = new ServerDataModel();
            model.InstalledPlugins.Add(RoslynAnalyzerProvider.CSharpPluginKey);
            model.InstalledPlugins.Add("unused");

            model.AddQualityProfile(validProfileName, "vb")
                .AddProject(validProjectKey)
                .AddProject(validProfileName);

            model.AddQualityProfile(validProfileName, RoslynAnalyzerProvider.CSharpLanguage)
                .AddProject(validProjectKey)
                .AddProject("project3")
                .SetExport(RoslynAnalyzerProvider.RoslynCSharpFormatName, GetValidCSharpProfile());

            model.AddRepository(RoslynAnalyzerProvider.CSharpRepositoryKey, RoslynAnalyzerProvider.CSharpLanguage);

            MockSonarQubeServer server = new MockSonarQubeServer();
            server.Data = model;
            return server;
        }

        private static string GetValidCSharpProfile()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<RoslynExportProfile Version=""1.0="">
  <Configuration>
    <RuleSet Name=""Rules for SonarQube"" Description=""This rule set was automatically generated from SonarQube."" ToolsVersion=""14.0"">
      <Rules AnalyzerId=""SonarLint.CSharp"" RuleNamespace=""SonarLint.CSharp"">
        <Rule Id=""S1116"" Action=""Warning"" />
        <Rule Id=""S1125"" Action=""Warning"" />
        <!-- other rules omitted -->
      </Rules>
      <Rules AnalyzerId=""Wintellect.Analyzers"" RuleNamespace=""Wintellect.Analyzers"">
        <Rule Id=""Wintellect003"" Action=""Warning"" />
        <!-- other rules omitted -->
      </Rules>
    </RuleSet>
    <AdditionalFiles>
      <AdditionalFile FileName=""SonarLint.xml"" >
        <AnalysisInput>
          <Rules>
            <Rule>
              <Key>S1067</Key>
              <Parameters>
                <Parameter>
                  <Key>max</Key>
                  <Value>3</Value>
                </Parameter>
              </Parameters>
            </Rule>
          </Rules>
          <Files>
          </Files>
        </AnalysisInput>
      </AdditionalFile>
      <AdditionalFile FileName=""MyAnalyzerData.xml"" >
        <Foo />
      </AdditionalFile>
    </AdditionalFiles>
  </Configuration>

  <Deployment>
    <NuGetPackages>
      <Package Id=""SonarLint"" Version=""1.3.0"" />
      <Package Id=""Wintellect.Analyzers"" Version=""1.0.5.0-rc1"" />
    </NuGetPackages>
  </Deployment>
</RoslynExportProfile>";
        }

        private static TeamBuildSettings CreateSettings(string rootDir)
        {
            TeamBuildSettings settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(rootDir);
            return settings;
        }

        private static string GetExpectedRulesetFileName(string rootDir)
        {
            return Path.Combine(GetConfPath(rootDir), RoslynAnalyzerProvider.RoslynCSharpRulesetFileName);
        }

        private static string GetConfPath(string rootDir)
        {
            return Path.Combine(rootDir, "conf");
        }

        private static string GetBinaryPath(string rootDir)
        {
            return Path.Combine(rootDir, "bin");
        }

        #endregion

        #region Checks

        private void CheckRulesetExists(CompilerAnalyzerConfig actualConfig, string rootTestDir)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(actualConfig.RulesetFilePath), "Ruleset file path should be set");
            Assert.IsTrue(Path.IsPathRooted(actualConfig.RulesetFilePath), "Ruleset file path should be absolute");
            Assert.IsTrue(File.Exists(actualConfig.RulesetFilePath), "Expected ruleset file does not exist: {0}", actualConfig.RulesetFilePath);
            this.TestContext.AddResultFile(actualConfig.RulesetFilePath);

            Assert.AreEqual(RoslynAnalyzerProvider.RoslynCSharpRulesetFileName, Path.GetFileName(actualConfig.RulesetFilePath), "Ruleset file does not have the expected name");

            string expectedFilePath = GetExpectedRulesetFileName(rootTestDir);
            Assert.AreEqual(expectedFilePath, actualConfig.RulesetFilePath, "Ruleset was not written to the expected location");
        }

        private static void CheckRulesetDoesNotExist(string rootTestDir)
        {
            string filePath = GetExpectedRulesetFileName(rootTestDir);
            Assert.IsFalse(File.Exists(filePath), "Not expecting the ruleset file to exist: {0}", filePath);
        }

        private static void AssertAnalyzerConfigNotPerformed(CompilerAnalyzerConfig actual, string rootTestDir)
        {
            Assert.IsNull(actual, "Not expecting a config instance to have been returned");
            CheckRulesetDoesNotExist(rootTestDir);
        }

        #endregion

    }
}
