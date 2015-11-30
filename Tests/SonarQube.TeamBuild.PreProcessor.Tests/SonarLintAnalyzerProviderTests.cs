//-----------------------------------------------------------------------
// <copyright file="SonarLintAnalyzerProviderTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.Integration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class SonarLintAnalyzerProviderTests
    {
        private const string DummyZippedBinaryFileName1 = "dummy1.dll";
        private const string DummyZippedBinaryFileName2 = "dummy2.dll";

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void SonarLint_ValidRuleset()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);
            MockSonarQubeServer mockServer = CreateValidServer("valid.project", "valid.profile");

            // Act
            SonarLintAnalyzerProvider.SetupAnalyzers(mockServer, settings, "valid.project", logger);

            // Assert
            CheckRulesetExists(rootDir);
            CheckSonarLintBinariesExist(rootDir);
        }

        [TestMethod]
        public void SonarLint_PluginNotInstalled()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", "valid.profile");
            mockServer.Data.InstalledPlugins.Remove(SonarLintAnalyzerProvider.CSharpPluginKey);

            // Act
            SonarLintAnalyzerProvider.SetupAnalyzers(mockServer, settings, "valid.project", logger);

            // Assert
            CheckRulesetDoesNotExist(rootDir);
            CheckBinariesDoNotExist(rootDir);
            logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        public void SonarLint_ProjectNotInProfile()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", "valid.profile");
            mockServer.Data.FindProfile("valid.profile", SonarLintAnalyzerProvider.CSharpLanguage).ActiveRules.Clear();

            // Act
            SonarLintAnalyzerProvider.SetupAnalyzers(mockServer, settings, "unknown.project", logger);

            // Assert
            CheckRulesetDoesNotExist(rootDir);
            CheckBinariesDoNotExist(rootDir);
            logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        public void SonarLint_InvalidRuleset()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", "valid.profile");
            mockServer.Data.FindProfile("valid.profile", SonarLintAnalyzerProvider.CSharpLanguage).SetExport(SonarLintAnalyzerProvider.SonarLintProfileFormatName, "not a ruleset");

            // Act
            SonarLintAnalyzerProvider.SetupAnalyzers(mockServer, settings, "valid.project", logger);

            // Assert
            CheckRulesetDoesNotExist(rootDir);
            CheckBinariesDoNotExist(rootDir);
            logger.AssertErrorsLogged(1); // Expecting an error in this case: the profile exists but couldn't be retrieved
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
        /// a SonarLint ruleset for the specified project key and profile
        /// </summary>
        private MockSonarQubeServer CreateValidServer(string validProjectKey, string validProfileName)
        {
            ServerDataModel model = new ServerDataModel();
            model.InstalledPlugins.Add(SonarLintAnalyzerProvider.CSharpPluginKey);
            model.InstalledPlugins.Add("unused");

            model.AddQualityProfile(validProfileName, "vb")
                .AddProject(validProjectKey)
                .AddProject(validProfileName);

            model.AddQualityProfile(validProfileName, SonarLintAnalyzerProvider.CSharpLanguage)
                .AddProject(validProjectKey)
                .AddProject("project3")
                .SetExport(SonarLintAnalyzerProvider.SonarLintProfileFormatName,
@"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Rules for SonarLint"" Description=""This rule set was automatically generated from SonarQube."" ToolsVersion=""14.0"">
  <Rules AnalyzerId=""SonarLint"" RuleNamespace=""SonarLint"">
    <Rule Id=""S1656"" Action=""Warning"" />
  </Rules>
</RuleSet>")
                .SetExport(SonarLintAnalyzerProvider.SonarLintParametersFormatName,
@"<?xml version=""1.0"" encoding=""UTF - 8""?>
<AnalysisInput>
  <Rules>
  </Rules>
  <Files>
  </Files>
</AnalysisInput>");

            model.AddRepository(SonarLintAnalyzerProvider.CSharpRepositoryKey, SonarLintAnalyzerProvider.CSharpLanguage);

            byte[] sonarLintData = CreateDummySonarLintZip();
            model.AddEmbeddedFile(SonarLintAnalyzerProvider.CSharpPluginKey, SonarLintAnalyzerProvider.EmbeddedSonarLintZipFileName, sonarLintData);

            MockSonarQubeServer server = new MockSonarQubeServer();
            server.Data = model;
            return server;
        }

        private static TeamBuildSettings CreateSettings(string rootDir)
        {
            TeamBuildSettings settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(rootDir);
            return settings;
        }

        private static string GetExpectedRulesetFileName(string rootDir)
        {
            return Path.Combine(GetConfPath(rootDir), SonarLintAnalyzerProvider.RoslynCSharpRulesetFileName);
        }

        private static string GetExpectedParametersFileName(string rootDir)
        {
            return Path.Combine(GetConfPath(rootDir), SonarLintAnalyzerProvider.SonarLintCSharpParametersFileName);
        }

        private static string GetConfPath(string rootDir)
        {
            return Path.Combine(rootDir, "conf");
        }

        private static string GetBinaryPath(string rootDir)
        {
            return Path.Combine(rootDir, "bin");
        }

        private static string[] GetBinaries(string rootDir)
        {
            string binDir = GetBinaryPath(rootDir);

            return Directory.GetFiles(binDir, "*.dll")
                .Select(p => Path.GetFileName(p)).ToArray();
        }

        private byte[] CreateDummySonarLintZip()
        {
            // Create a temporary directory structure
            string tempDir = TestUtils.CreateTestSpecificFolder(this.TestContext, System.Guid.NewGuid().ToString());
            string zipDir = Path.Combine(tempDir, "zipDir");
            Directory.CreateDirectory(zipDir);
            string zippedFilePath = Path.Combine(tempDir, SonarLintAnalyzerProvider.EmbeddedSonarLintZipFileName);

            // Create and read the zip file           
            TestUtils.CreateTextFile(zipDir, DummyZippedBinaryFileName1, "dummy file content");
            TestUtils.CreateTextFile(zipDir, DummyZippedBinaryFileName2, "dummy file content 2");
            ZipFile.CreateFromDirectory(zipDir, zippedFilePath);
            byte[] zipData = File.ReadAllBytes(zippedFilePath);

            // Cleanup
            Directory.Delete(tempDir, true);

            return zipData;
        }


        #endregion

        #region Checks

        private static void CheckRulesetExists(string rootTestDir)
        {
            string filePath = GetExpectedRulesetFileName(rootTestDir);
            Assert.IsTrue(File.Exists(filePath), "Expected ruleset file does not exist: {0}", filePath);

            filePath = GetExpectedParametersFileName(rootTestDir);
            Assert.IsTrue(File.Exists(filePath), "Expected parameters file does not exist: {0}", filePath);
        }

        private static void CheckRulesetDoesNotExist(string rootTestDir)
        {
            string filePath = GetExpectedRulesetFileName(rootTestDir);
            Assert.IsFalse(File.Exists(filePath), "Not expecting the ruleset file to exist: {0}", filePath);

            filePath = GetExpectedParametersFileName(rootTestDir);
            Assert.IsFalse(File.Exists(filePath), "Not expecting the parameters file to exist: {0}", filePath);
        }

        private static void CheckSonarLintBinariesExist(string rootTestDir)
        {
            string[] binaries = GetBinaries(rootTestDir);
            CollectionAssert.Contains(binaries, DummyZippedBinaryFileName1, "Expected binary does not exist: " + DummyZippedBinaryFileName1);
            CollectionAssert.Contains(binaries, DummyZippedBinaryFileName2, "Expected binary does not exist: " + DummyZippedBinaryFileName2);
            Assert.AreEqual(2, binaries.Length, "Unexpected number of files downloaded");
        }

        private static void CheckBinariesDoNotExist(string rootTestDir)
        {
            Assert.IsFalse(GetBinaries(rootTestDir).Any(), "Not expecting any output binaries to exist");

        }

        #endregion

    }
}
