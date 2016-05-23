//-----------------------------------------------------------------------
// <copyright file="PreProcessorTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System;
using System.IO;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class PreProcessorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PreProc_InvalidArgs()
        {
            // Arrange
            MockSonarQubeServer mockServer = new MockSonarQubeServer();

            TeamBuildPreProcessor preprocessor = new TeamBuildPreProcessor(
                new MockObjectFactory(mockServer, new MockTargetsInstaller(), new MockRoslynAnalyzerProvider()),
                new TestLogger());

            // Act and assert
            AssertException.Expects<ArgumentNullException>(() => preprocessor.Execute(null));
        }

        [TestMethod]
        public void PreProc_EndToEnd_SuccessCase()
        {
            // Checks end-to-end happy path for the pre-processor i.e.
            // * arguments are parsed
            // * targets are installed
            // * server properties are fetched
            // * rulesets are generated
            // * config file is created

            // Arrange
            string workingDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            TestLogger logger = new TestLogger();

            // Configure the server
            MockSonarQubeServer mockServer = new MockSonarQubeServer();

            ServerDataModel data = mockServer.Data;
            data.ServerProperties.Add("server.key", "server value 1");

            data.InstalledPlugins.Add("csharp");
            data.InstalledPlugins.Add("vbnet");

            data.AddRepository("fxcop", "cs")
                .AddRule("cs.rule1", "cs.rule1.internal")
                .AddRule("cs.rule2", "cs.rule2.internal");

            data.AddRepository("fxcop-vbnet", "vbnet")
                .AddRule("vb.rule1", "vb.rule1.internal")
                .AddRule("vb.rule2", "vb.rule2.internal");

            data.AddQualityProfile("test.profile", "cs")
                .AddProject("key");
            data.AddRuleToProfile("cs.rule1", "test.profile");

            data.AddQualityProfile("test.profile", "vbnet")
                .AddProject("key");
            data.AddRuleToProfile("vb.rule2", "test.profile");

            MockRoslynAnalyzerProvider mockAnalyzerProvider = new MockRoslynAnalyzerProvider();
            mockAnalyzerProvider.SettingsToReturn = new AnalyzerSettings();
            mockAnalyzerProvider.SettingsToReturn.RuleSetFilePath = "c:\\xxx.ruleset";

            MockTargetsInstaller mockTargetsInstaller = new MockTargetsInstaller();

            MockObjectFactory mockFactory = new MockObjectFactory(mockServer, mockTargetsInstaller, mockAnalyzerProvider);


            string[] validArgs = new string[] {
                "/k:key", "/n:name", "/v:1.0",
                "/d:cmd.line1=cmdline.value.1",
                "/d:sonar.host.url=http://host",
                "/d:sonar.log.level=INFO|DEBUG"};

            TeamBuildSettings settings;
            using (PreprocessTestUtils.CreateValidNonTeamBuildScope())
            using (new WorkingDirectoryScope(workingDir))
            {
                settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                Assert.AreEqual(BuildEnvironment.NotTeamBuild, settings.BuildEnvironment, "Test setup error: build environment was not set correctly");

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockFactory, logger);

                // Act
                bool success = preProcessor.Execute(validArgs);
                Assert.IsTrue(success, "Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoryExists(settings.AnalysisBaseDirectory);
            AssertDirectoryExists(settings.SonarConfigDirectory);
            AssertDirectoryExists(settings.SonarOutputDirectory);
            // The bootstrapper is responsible for creating the bin directory

            mockTargetsInstaller.AssertsTargetsCopied();
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetInternalKeys", 2); // C# and VB

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
            logger.AssertVerbosity(LoggerVerbosity.Debug);

            AssertConfigFileExists(settings.AnalysisConfigFilePath);
            AnalysisConfig actualConfig = AnalysisConfig.Load(settings.AnalysisConfigFilePath);

            Assert.AreEqual("key", actualConfig.SonarProjectKey, "Unexpected project key");
            Assert.AreEqual("name", actualConfig.SonarProjectName, "Unexpected project name");
            Assert.AreEqual("1.0", actualConfig.SonarProjectVersion, "Unexpected project version");

            Assert.IsNotNull(actualConfig.AnalyzerSettings, "Analyzer settings should not be null");
            Assert.AreEqual("c:\\xxx.ruleset", actualConfig.AnalyzerSettings.RuleSetFilePath, "Unexpected ruleset path");

            AssertExpectedLocalSetting(SonarProperties.HostUrl, "http://host", actualConfig);
            AssertExpectedLocalSetting("cmd.line1", "cmdline.value.1", actualConfig);
            AssertExpectedServerSetting("server.key", "server value 1", actualConfig);

            string fxCopFilePath = AssertFileExists(settings.SonarConfigDirectory, TeamBuildPreProcessor.FxCopCSharpRuleset);
            PreProcessAsserts.AssertRuleSetContainsRules(fxCopFilePath, "cs.rule1");

            fxCopFilePath = AssertFileExists(settings.SonarConfigDirectory, TeamBuildPreProcessor.FxCopVBNetRuleset);
            PreProcessAsserts.AssertRuleSetContainsRules(fxCopFilePath, "vb.rule2");
        }

        #endregion Tests

        #region Checks

        private void AssertConfigFileExists(string filePath)
        {
            Assert.IsTrue(File.Exists(filePath), "Expecting the analysis config file to exist. Path: {0}", filePath);
            this.TestContext.AddResultFile(filePath);
        }

        private static void AssertExpectedLocalSetting(string key, string expectedValue, AnalysisConfig actualConfig)
        {
            Property actualProperty;
            bool found = Property.TryGetProperty(key, actualConfig.LocalSettings, out actualProperty);

            Assert.IsTrue(found, "Failed to find the expected local setting: {0}", key);
            Assert.AreEqual(expectedValue, actualProperty.Value, "Unexpected property value. Key: {0}", key);
        }

        private static void AssertExpectedServerSetting(string key, string expectedValue, AnalysisConfig actualConfig)
        {
            Property actualProperty;
            bool found = Property.TryGetProperty(key, actualConfig.ServerSettings, out actualProperty);

            Assert.IsTrue(found, "Failed to find the expected server setting: {0}", key);
            Assert.AreEqual(expectedValue, actualProperty.Value, "Unexpected property value. Key: {0}", key);
        }

        private static void AssertDirectoryExists(string path)
        {
            Assert.IsTrue(Directory.Exists(path), "Expected directory does not exist: {0}", path);
        }

        private static string AssertFileExists(string directory, string fileName)
        {
            string fullPath = Path.Combine(directory, fileName);
            Assert.IsTrue(File.Exists(fullPath), "Expected file does not exist");
            return fullPath;
        }

        #endregion Checks
    }
}