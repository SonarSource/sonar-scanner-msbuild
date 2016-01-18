//-----------------------------------------------------------------------
// <copyright file="RoslynAnalyzerProviderTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.PreProcessor.Roslyn;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
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
            MockSonarQubeServer mockServer = CreateServer("valid.project", "valid.profile", testProfile);

            // Act
            CompilerAnalyzerConfig actualConfig = RoslynAnalyzerProvider.SetupAnalyzers(mockServer, settings, "valid.project", logger);

            // Assert
            CheckConfigInvariants(actualConfig);
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);

            CheckRuleset(actualConfig, rootDir);
            CheckExpectedAdditionalFiles(testProfile, actualConfig);
        }

        [TestMethod]
        public void RoslynConfig_ValidRealSonarLintProfile()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            WellKnownProfile testProfile = CreateRealSonarLintProfile();
            MockSonarQubeServer mockServer = CreateServer("valid.project", "valid.profile", testProfile);

            // Act
            CompilerAnalyzerConfig actualConfig = RoslynAnalyzerProvider.SetupAnalyzers(mockServer, settings, "valid.project", logger);

            // Assert
            CheckConfigInvariants(actualConfig);
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);
            CheckRuleset(actualConfig, rootDir);
            CheckExpectedAdditionalFiles(testProfile, actualConfig);

            // Check the additional file is valid XML
            Assert.AreEqual(1, actualConfig.AdditionalFilePaths.Count(), "Test setup error: expecting only one additional file. Check the sample export XML has not changed");
            string filePath = actualConfig.AdditionalFilePaths.First();
            CheckFileIsXml(filePath);
        }

        [TestMethod]
        public void RoslynConfig_MissingAdditionalFileName_AdditionalFileIgnored()
        {
            // Arrange
            string rootDir = CreateTestFolders();
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(rootDir);

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", "valid.profile");
            QualityProfile csProfile = mockServer.Data.FindProfile("valid.profile", RoslynAnalyzerProvider.CSharpLanguage);

            string expectedFileContent = "bar";

            csProfile.SetExport(RoslynAnalyzerProvider.RoslynCSharpFormatName, @"<?xml version=""1.0"" encoding=""utf-8""?>
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

            // Act
            CompilerAnalyzerConfig actualConfig = RoslynAnalyzerProvider.SetupAnalyzers(mockServer, settings, "valid.project", logger);

            // Assert
            CheckConfigInvariants(actualConfig);
            CheckRuleset(actualConfig, rootDir);
            CheckExpectedAdditionalFileExists("foo.txt", expectedFileContent, actualConfig);

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

            MockSonarQubeServer mockServer = CreateValidServer("valid.project", "valid.profile");
            QualityProfile csProfile = mockServer.Data.FindProfile("valid.profile", RoslynAnalyzerProvider.CSharpLanguage);
            csProfile.SetExport(RoslynAnalyzerProvider.RoslynCSharpFormatName, @"<?xml version=""1.0"" encoding=""utf-8""?>
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

            // Act
            CompilerAnalyzerConfig actualConfig = RoslynAnalyzerProvider.SetupAnalyzers(mockServer, settings, "valid.project", logger);

            // Assert
            CheckConfigInvariants(actualConfig);
            CheckRuleset(actualConfig, rootDir);
            CheckExpectedAdditionalFileExists("foo.txt", expectedFileContent, actualConfig);
            CheckExpectedAdditionalFileExists("file2.txt", string.Empty, actualConfig);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
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
            return CreateServer(validProjectKey, validProfileName, CreateValidCSharpProfile());
        }

        private MockSonarQubeServer CreateServer(string projectKey, string profileName, WellKnownProfile profile)
        {
            ServerDataModel model = new ServerDataModel();

            // Add the required C# plugin and repository
            model.InstalledPlugins.Add(RoslynAnalyzerProvider.CSharpPluginKey);
            model.AddRepository(RoslynAnalyzerProvider.CSharpRepositoryKey, RoslynAnalyzerProvider.CSharpLanguage);

            // Add some dummy data
            model.InstalledPlugins.Add("unused");

            model.AddQualityProfile(profileName, "vb")
                .AddProject(projectKey)
                .AddProject(profileName)
                .SetExport(profile.Format, "Invalid content - this export should not be requested");

            // Create a C# quality profile for the supplied profile
            model.AddQualityProfile(profileName, RoslynAnalyzerProvider.CSharpLanguage)
                .AddProject(projectKey)
                .AddProject("dummy project3") // more dummy data - apply the quality profile to another dummy project
                .SetExport(profile.Format, profile.Content);

            MockSonarQubeServer server = new MockSonarQubeServer();
            server.Data = model;
            return server;
        }


        private static WellKnownProfile CreateValidCSharpProfile()
        {
            string file1Content = @"<AnalysisInput>
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
";

            string file2Content = "<Foo />";

            string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
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
      <AdditionalFile FileName=""SonarLint.xml"" >" + GetBase64EncodedString(file1Content) + @"</AdditionalFile>
      <AdditionalFile FileName=""MyAnalyzerData.xml"" >" + GetBase64EncodedString(file2Content)  + @"</AdditionalFile>
    </AdditionalFiles>
  </Configuration>

  <Deployment>
    <NuGetPackages>
      <Package Id=""SonarLint"" Version=""1.3.0"" />
      <Package Id=""Wintellect.Analyzers"" Version=""1.0.5.0-rc1"" />
    </NuGetPackages>
  </Deployment>
</RoslynExportProfile>";

            WellKnownProfile profile = new WellKnownProfile(RoslynAnalyzerProvider.RoslynCSharpFormatName, xml);
            profile.SetAdditionalFile("SonarLint.xml", file1Content);
            profile.SetAdditionalFile("MyAnalyzerData.xml", file2Content);

            profile.SetPackage("SonarLint", "1.3.0");
            profile.SetPackage("Wintellect.Analyzers", "1.0.5.0-rc1");

            return profile;
        }

        private static WellKnownProfile CreateRealSonarLintProfile()
        {
            WellKnownProfile profile = new WellKnownProfile(RoslynAnalyzerProvider.RoslynCSharpFormatName, SampleExportXml.RoslynExportedValidSonarLintXml);
            profile.SetAdditionalFile(SampleExportXml.RoslynExportedAdditionalFileName, null /* don't check */);
            profile.SetPackage(SampleExportXml.RoslynExportedPackageId, SampleExportXml.RoslynExportedPackageVersion);

            return profile;
        }

        private static TeamBuildSettings CreateSettings(string rootDir)
        {
            TeamBuildSettings settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(rootDir);
            return settings;
        }

        private static string GetExpectedRulesetFilePath(string rootDir)
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

        private static string GetBase64EncodedString(string text)
        {
            return System.Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        }

        #endregion

        #region Checks

        private static void CheckConfigInvariants(CompilerAnalyzerConfig actualConfig)
        {
            Assert.IsNotNull(actualConfig, "Not expecting the config to be null");
            Assert.IsNotNull(actualConfig.AdditionalFilePaths);
            Assert.IsNotNull(actualConfig.AnalyzerAssemblyPaths);
            Assert.IsFalse(string.IsNullOrEmpty(actualConfig.RulesetFilePath));

            // Any file paths returned in the config should exist
            foreach (string filePath in actualConfig.AdditionalFilePaths)
            {
                Assert.IsTrue(File.Exists(filePath), "Expected additional file does not exist: {0}", filePath);
            }
            Assert.IsTrue(File.Exists(actualConfig.RulesetFilePath), "Specified ruleset does not exist: {0}", actualConfig.RulesetFilePath);
        }

        private void CheckRuleset(CompilerAnalyzerConfig actualConfig, string rootTestDir)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(actualConfig.RulesetFilePath), "Ruleset file path should be set");
            Assert.IsTrue(Path.IsPathRooted(actualConfig.RulesetFilePath), "Ruleset file path should be absolute");
            Assert.IsTrue(File.Exists(actualConfig.RulesetFilePath), "Specified ruleset file does not exist: {0}", actualConfig.RulesetFilePath);
            this.TestContext.AddResultFile(actualConfig.RulesetFilePath);

            CheckFileIsXml(actualConfig.RulesetFilePath);

            Assert.AreEqual(RoslynAnalyzerProvider.RoslynCSharpRulesetFileName, Path.GetFileName(actualConfig.RulesetFilePath), "Ruleset file does not have the expected name");

            string expectedFilePath = GetExpectedRulesetFilePath(rootTestDir);
            Assert.AreEqual(expectedFilePath, actualConfig.RulesetFilePath, "Ruleset was not written to the expected location");

        }
        
        private void CheckExpectedAdditionalFiles(WellKnownProfile expected, CompilerAnalyzerConfig actualConfig)
        {
            foreach (string expectedFileName in expected.AdditionalFiles.Keys)
            {
                string expectedContent = expected.AdditionalFiles[expectedFileName];
                CheckExpectedAdditionalFileExists(expectedFileName, expectedContent, actualConfig);
            }
        }

        private void CheckExpectedAdditionalFileExists(string expectedFileName, string expectedContent, CompilerAnalyzerConfig actualConfig)
        {
            // Check one file of the expected name exists
            IEnumerable<string> matches = actualConfig.AdditionalFilePaths.Where(actual => string.Equals(expectedFileName, Path.GetFileName(actual), System.StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(1, matches.Count(), "Unexpected number of files named \"{0}\". One and only one expected", expectedFileName);

            // Check the file exists and has the expected content
            string actualFilePath = matches.First();
            Assert.IsTrue(File.Exists(actualFilePath), "AdditionalFile does not exist: {0}", actualFilePath);

            // Dump the contents to help with debugging
            this.TestContext.AddResultFile(actualFilePath);
            this.TestContext.WriteLine("File contents: {0}", actualFilePath);
            this.TestContext.WriteLine(File.ReadAllText(actualFilePath));
            this.TestContext.WriteLine("");

            if (expectedContent != null) // null expected means "don't check"
            {
                Assert.AreEqual(expectedContent, File.ReadAllText(actualFilePath), "Additional file does not have the expected content: {0}", expectedFileName);
            }
        }

        private static void AssertAnalyzerConfigNotPerformed(CompilerAnalyzerConfig actual, string rootTestDir)
        {
            Assert.IsNull(actual, "Not expecting a config instance to have been returned");

            string filePath = GetExpectedRulesetFilePath(rootTestDir);
            Assert.IsFalse(File.Exists(filePath), "Not expecting the ruleset file to exist: {0}", filePath);
        }

        private static void CheckFileIsXml(string fullPath)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(fullPath);
            Assert.IsNotNull(doc.FirstChild, "Expecting the file to contain some valid XML");
        }

        #endregion

        /// <summary>
        /// Data class that describes a single profile
        /// </summary>
        private class WellKnownProfile
        {
            private readonly string format;
            private readonly string exportXml;
            private readonly Dictionary<string, string> fileContentMap;
            private readonly Dictionary<string, string> packageIdVersionMap;

            public WellKnownProfile(string format, string exportXml)
            {
                this.format = format;
                this.exportXml = exportXml;
                this.fileContentMap = new Dictionary<string, string>();
                this.packageIdVersionMap = new Dictionary<string, string>();
            }

            public string Format { get { return this.format; } }
            public string Content { get { return this.exportXml; } }
            public IDictionary<string, string> AdditionalFiles { get { return this.fileContentMap; } }
            public IDictionary<string, string> Packages { get { return this.packageIdVersionMap; } }

            public void SetAdditionalFile(string fileName, string textContent)
            {
                this.fileContentMap[fileName] = textContent;
            }

            public void SetPackage(string id, string version)
            {
                this.packageIdVersionMap[id] = version;
            }
        }
    }
}
