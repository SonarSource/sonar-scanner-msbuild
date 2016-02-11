//-----------------------------------------------------------------------
// <copyright file="RoslynAnalyzerProviderTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.PreProcessor.Roslyn;
using System;
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
        public void RoslynConfig_ConstructorArgumentChecks()
        {
            AssertException.Expects<ArgumentNullException>(() => new RoslynAnalyzerProvider(null, new TestLogger()));
            AssertException.Expects<ArgumentNullException>(() => new RoslynAnalyzerProvider(new MockAnalyzerInstaller(), null));
        }

        [TestMethod]
        public void RoslynConfig_SetupAnalyzers_ArgumentChecks()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings = CreateSettings(this.TestContext.DeploymentDirectory);
            MockSonarQubeServer mockServer = CreateValidServer("valid.project", null, "valid.profile");

            RoslynAnalyzerProvider testSubject = CreateTestSubject(logger);

            // Act and assert (branch can be null)
            AssertException.Expects<ArgumentNullException>(() => testSubject.SetupAnalyzers(null, settings, "project", "branch"));
            AssertException.Expects<ArgumentNullException>(() => testSubject.SetupAnalyzers(mockServer, null, "project", "branch"));
            AssertException.Expects<ArgumentNullException>(() => testSubject.SetupAnalyzers(mockServer, settings, null, "branch"));
        }

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
            AnalyzerSettings actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            AssertAnalyzerSetupNotPerformed(actualSettings, rootDir);

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
            AnalyzerSettings actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "unknown.project", null);

            // Assert
            AssertAnalyzerSetupNotPerformed(actualSettings, rootDir);

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
            csProfile.SetExport(RoslynAnalyzerProvider.RoslynCSharpFormatName, null);
            
            RoslynAnalyzerProvider testSubject = CreateTestSubject(logger);

            // Act
            AnalyzerSettings actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            AssertAnalyzerSetupNotPerformed(actualSettings, rootDir);

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
            csProfile.SetExport(RoslynAnalyzerProvider.RoslynCSharpFormatName, @"<?xml version=""1.0"" encoding=""utf-8""?>
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
            AnalyzerSettings actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            AssertAnalyzerSetupNotPerformed(actualSettings, rootDir);

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
            AnalyzerSettings actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", "missingBranch");

            // Assert
            AssertAnalyzerSetupNotPerformed(actualSettings, rootDir);

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
            AnalyzerSettings actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            CheckSettingsInvariants(actualSettings);
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);

            CheckRuleset(actualSettings, rootDir);
            CheckExpectedAdditionalFiles(testProfile, actualSettings);

            mockInstaller.AssertExpectedPluginsRequested(testProfile.Plugins);
            CheckExpectedAssemblies(actualSettings, "c:\\assembly1.dll", "d:\\foo\\assembly2.dll");
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
            AnalyzerSettings actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", "aBranch");

            // Assert
            CheckSettingsInvariants(actualSettings);
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);

            CheckRuleset(actualSettings, rootDir);
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
            AnalyzerSettings actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            CheckSettingsInvariants(actualSettings);
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);
            CheckRuleset(actualSettings, rootDir);
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

            RoslynAnalyzerProvider testSubject = CreateTestSubject(logger);

            // Act
            AnalyzerSettings actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            CheckSettingsInvariants(actualSettings);
            CheckRuleset(actualSettings, rootDir);
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

            RoslynAnalyzerProvider testSubject = CreateTestSubject(logger);

            // Act
            AnalyzerSettings actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            CheckSettingsInvariants(actualSettings);
            CheckRuleset(actualSettings, rootDir);
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
            csProfile.SetExport(RoslynAnalyzerProvider.RoslynCSharpFormatName, @"<?xml version=""1.0"" encoding=""utf-8""?>
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
            AnalyzerSettings actualSettings = testSubject.SetupAnalyzers(mockServer, settings, "valid.project", null);

            // Assert
            CheckSettingsInvariants(actualSettings);

            CheckExpectedAssemblies(actualSettings /* none */ );

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

        private static RoslynAnalyzerProvider CreateTestSubject(ILogger logger)
        {
            RoslynAnalyzerProvider testSubject = new RoslynAnalyzerProvider(new MockAnalyzerInstaller(), logger);
            return testSubject;
        }

        /// <summary>
        /// Creates and returns a mock server that is correctly configured to return
        /// a Roslyn ruleset for the specified project key and profile
        /// </summary>
        private MockSonarQubeServer CreateValidServer(string validProjectKey, string validProjectBranch, string validProfileName)
        {
            return CreateServer(validProjectKey, validProjectBranch, validProfileName, CreateValidCSharpProfile());
        }

        private MockSonarQubeServer CreateServer(string projectKey, string projectBranch, string profileName, WellKnownProfile profile)
        {
            ServerDataModel model = new ServerDataModel();

            // Add the required C# plugin and repository
            model.InstalledPlugins.Add(RoslynAnalyzerProvider.CSharpPluginKey);
            model.AddRepository(RoslynAnalyzerProvider.CSharpRepositoryKey, RoslynAnalyzerProvider.CSharpLanguage);

            // Add some dummy data
            model.InstalledPlugins.Add("unused");

            MockSonarQubeServer server = new MockSonarQubeServer();
            server.Data = model;

            AddWellKnownProfileToServer(projectKey, projectBranch, profileName, profile, server);

            return server;
        }

        private void AddWellKnownProfileToServer(string projectKey, string projectBranch, string profileName, WellKnownProfile profile, MockSonarQubeServer server)
        {
            string projectId = projectKey;
            if (!String.IsNullOrWhiteSpace(projectBranch))
            {
                projectId = projectKey + ":" + projectBranch;
            }

            server.Data.AddQualityProfile(profileName, "vb")
                .AddProject(projectId)
                .AddProject(profileName)
                .SetExport(profile.Format, "Invalid content - this export should not be requested");

            // Create a C# quality profile for the supplied profile
            server.Data.AddQualityProfile(profileName, RoslynAnalyzerProvider.CSharpLanguage)
                .AddProject(projectId)
                .AddProject("dummy project3") // more dummy data - apply the quality profile to another dummy project
                .SetExport(profile.Format, profile.Content);
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
    <Plugins>
      <Plugin Key=""csharp"" Version=""1.3.0"" />
      <Plugin Key=""wintellect.analyzers"" Version=""1.6.0-RC1"" />
    </Plugins>
    <NuGetPackages>
      <NuGetPackage Id=""Anything"" Version=""1.9.9"" />
    </NuGetPackages>
  </Deployment>
</RoslynExportProfile>";

            WellKnownProfile profile = new WellKnownProfile(RoslynAnalyzerProvider.RoslynCSharpFormatName, xml);
            profile.SetAdditionalFile("SonarLint.xml", file1Content);
            profile.SetAdditionalFile("MyAnalyzerData.xml", file2Content);

            profile.AddPlugin("csharp");
            profile.AddPlugin("wintellect.analyzers");

            return profile;
        }

        private static WellKnownProfile CreateRealSonarLintProfile()
        {
            WellKnownProfile profile = new WellKnownProfile(RoslynAnalyzerProvider.RoslynCSharpFormatName, SampleExportXml.RoslynExportedValidSonarLintXml);
            profile.SetAdditionalFile(SampleExportXml.RoslynExportedAdditionalFileName, null /* don't check */);
            profile.AddPlugin(SampleExportXml.RoslynExportedPluginKey);

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

        private static void CheckSettingsInvariants(AnalyzerSettings actualSettings)
        {
            Assert.IsNotNull(actualSettings, "Not expecting the config to be null");
            Assert.IsNotNull(actualSettings.AdditionalFilePaths);
            Assert.IsNotNull(actualSettings.AnalyzerAssemblyPaths);
            Assert.IsFalse(string.IsNullOrEmpty(actualSettings.RuleSetFilePath));

            // Any file paths returned in the config should exist
            foreach (string filePath in actualSettings.AdditionalFilePaths)
            {
                Assert.IsTrue(File.Exists(filePath), "Expected additional file does not exist: {0}", filePath);
            }
            Assert.IsTrue(File.Exists(actualSettings.RuleSetFilePath), "Specified ruleset does not exist: {0}", actualSettings.RuleSetFilePath);
        }

        private void CheckRuleset(AnalyzerSettings actualSettings, string rootTestDir)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(actualSettings.RuleSetFilePath), "Ruleset file path should be set");
            Assert.IsTrue(Path.IsPathRooted(actualSettings.RuleSetFilePath), "Ruleset file path should be absolute");
            Assert.IsTrue(File.Exists(actualSettings.RuleSetFilePath), "Specified ruleset file does not exist: {0}", actualSettings.RuleSetFilePath);
            this.TestContext.AddResultFile(actualSettings.RuleSetFilePath);

            CheckFileIsXml(actualSettings.RuleSetFilePath);

            Assert.AreEqual(RoslynAnalyzerProvider.RoslynCSharpRulesetFileName, Path.GetFileName(actualSettings.RuleSetFilePath), "Ruleset file does not have the expected name");

            string expectedFilePath = GetExpectedRulesetFilePath(rootTestDir);
            Assert.AreEqual(expectedFilePath, actualSettings.RuleSetFilePath, "Ruleset was not written to the expected location");

        }
        
        private void CheckExpectedAdditionalFiles(WellKnownProfile expected, AnalyzerSettings actualSettings)
        {
            foreach (string expectedFileName in expected.AdditionalFiles.Keys)
            {
                string expectedContent = expected.AdditionalFiles[expectedFileName];
                CheckExpectedAdditionalFileExists(expectedFileName, expectedContent, actualSettings);
            }
        }

        private void CheckExpectedAdditionalFileExists(string expectedFileName, string expectedContent, AnalyzerSettings actualSettings)
        {
            // Check one file of the expected name exists
            IEnumerable<string> matches = actualSettings.AdditionalFilePaths.Where(actual => string.Equals(expectedFileName, Path.GetFileName(actual), System.StringComparison.OrdinalIgnoreCase));
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

        private static void AssertAnalyzerSetupNotPerformed(AnalyzerSettings actualSettings, string rootTestDir)
        {
            Assert.IsNotNull(actualSettings, "Not expecting the settings to be null");

            string filePath = GetExpectedRulesetFilePath(rootTestDir);
            Assert.IsFalse(File.Exists(filePath), "Not expecting the ruleset file to exist: {0}", filePath);
        }

        private static void CheckFileIsXml(string fullPath)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(fullPath);
            Assert.IsNotNull(doc.FirstChild, "Expecting the file to contain some valid XML");
        }

        private static void CheckExpectedAssemblies(AnalyzerSettings actualSettings, params string[] expected)
        {
            foreach(string expectedItem in expected)
            {
                Assert.IsTrue(actualSettings.AnalyzerAssemblyPaths.Contains(expectedItem, StringComparer.OrdinalIgnoreCase),
                    "Expected assembly file path was not returned: {0}", expectedItem);
            }
            Assert.AreEqual(expected.Length, actualSettings.AnalyzerAssemblyPaths.Count(), "Too many assembly file paths returned");
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
            private readonly ISet<string> plugins;
            private readonly ISet<string> assemblyPaths;

            public WellKnownProfile(string format, string exportXml)
            {
                this.format = format;
                this.exportXml = exportXml;
                this.fileContentMap = new Dictionary<string, string>();
                this.plugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                this.assemblyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            public string Format { get { return this.format; } }
            public string Content { get { return this.exportXml; } }
            public IDictionary<string, string> AdditionalFiles { get { return this.fileContentMap; } }
            public IEnumerable<string> Plugins { get { return this.plugins; } }
            public ISet<string> AssemblyFilePaths { get { return this.assemblyPaths; } }

            public void SetAdditionalFile(string fileName, string textContent)
            {
                this.fileContentMap[fileName] = textContent;
            }

            public void AddPlugin(string key)
            {
                this.plugins.Add(key);
            }

            public void AddAssembly(string filePath)
            {
                this.assemblyPaths.Add(filePath);
            }
        }
    }
}
