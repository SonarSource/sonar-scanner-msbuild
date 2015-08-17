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
using System.Collections.Generic;
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
            TestLogger validLogger = new TestLogger();

            string[] validArgs = new string[] { "/k:key", "/n:name", "/v:1.0" };

            MockPropertiesFetcher mockPropertiesFetcher = new MockPropertiesFetcher();
            MockRulesetGenerator mockRulesetGenerator = new MockRulesetGenerator();
            MockTargetsInstaller mockTargetsInstaller = new MockTargetsInstaller();
            TeamBuildPreProcessor preprocessor = new TeamBuildPreProcessor(mockPropertiesFetcher, mockRulesetGenerator, mockTargetsInstaller);

            // Act and assert
            AssertException.Expects<ArgumentNullException>(() => preprocessor.Execute(null, validLogger));
            AssertException.Expects<ArgumentNullException>(() => preprocessor.Execute(validArgs, null));
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
            MockRulesetGenerator mockRulesetGenerator = new MockRulesetGenerator();
            TestLogger logger = new TestLogger();

            MockPropertiesFetcher mockPropertiesFetcher = new MockPropertiesFetcher();
            mockPropertiesFetcher.PropertiesToReturn = new Dictionary<string, string>();

            MockTargetsInstaller mockTargetsInstaller = new MockTargetsInstaller();

            // The set of server properties to return
            mockPropertiesFetcher.PropertiesToReturn.Add("server.key", "server value 1");

            string[] validArgs = new string[] {
                "/k:key", "/n:name", "/v:1.0",

                "/d:cmd.line1=cmdline.value.1",
                "/d:sonar.host.url=http://host" };

            TeamBuildSettings settings;
            using (PreprocessTestUtils.CreateValidNonTeamBuildScope())
            using (new WorkingDirectoryScope(workingDir))
            {
                settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                Assert.AreEqual(BuildEnvironment.NotTeamBuild, settings.BuildEnvironment, "Test setup error: build environment was not set correctly");

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockPropertiesFetcher, mockRulesetGenerator, mockTargetsInstaller);

                // Act
                bool success = preProcessor.Execute(validArgs, logger);
                Assert.IsTrue(success, "Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoryExists(settings.AnalysisBaseDirectory);
            AssertDirectoryExists(settings.SonarConfigDirectory);
            AssertDirectoryExists(settings.SonarOutputDirectory);
            // The bootstrapper is responsible for creating the bin directory

            mockTargetsInstaller.AssertsTargetsCopied();
            mockPropertiesFetcher.AssertFetchPropertiesCalled();
            mockRulesetGenerator.AssertGenerateCalled(2); // C# and VB

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            AssertConfigFileExists(settings.AnalysisConfigFilePath);
            AnalysisConfig actualConfig = AnalysisConfig.Load(settings.AnalysisConfigFilePath);

            Assert.AreEqual("key", actualConfig.SonarProjectKey, "Unexpected project key");
            Assert.AreEqual("name", actualConfig.SonarProjectName, "Unexpected project name");
            Assert.AreEqual("1.0", actualConfig.SonarProjectVersion, "Unexpected project version");

            AssertExpectedAnalysisSetting(SonarProperties.HostUrl, "http://host", actualConfig);
            AssertExpectedAnalysisSetting("cmd.line1", "cmdline.value.1", actualConfig);
            AssertExpectedAnalysisSetting("server.key", "server value 1", actualConfig);
        }

        #endregion Tests

        #region Checks

        private void AssertConfigFileExists(string filePath)
        {
            Assert.IsTrue(File.Exists(filePath), "Expecting the analysis config file to exist. Path: {0}", filePath);
            this.TestContext.AddResultFile(filePath);
        }

        private static void AssertExpectedAnalysisSetting(string key, string expectedValue, AnalysisConfig actualConfig)
        {
            AnalysisSetting setting;
            actualConfig.TryGetSetting(key, out setting);

            Assert.IsNotNull(setting, "Failed to retrieve the expected setting. Key: {0}", key);
            Assert.AreEqual(expectedValue, setting.Value, "Unexpected setting value. Key: {0}", key);
        }

        private static void AssertDirectoryExists(string path)
        {
            Assert.IsTrue(Directory.Exists(path), "Expected directory does not exist: {0}", path);
        }

        #endregion Checks
    }
}