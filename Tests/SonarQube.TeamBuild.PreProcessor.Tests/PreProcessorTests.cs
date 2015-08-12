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
        public void PreProc_LocalPropertiesOverrideServerSettings()
        {
            // Checks command line properties override those fetched from the server

            // Arrange
            MockRulesetGenerator mockRulesetGenerator = new MockRulesetGenerator();
            TestLogger logger = new TestLogger();

            MockPropertiesFetcher mockPropertiesFetcher = new MockPropertiesFetcher();
            mockPropertiesFetcher.PropertiesToReturn = new Dictionary<string, string>();

            MockTargetsInstaller mockTargetsInstaller = new MockTargetsInstaller();

            // The set of server properties to return
            mockPropertiesFetcher.PropertiesToReturn.Add("shared.key1", "server value 1 - should be overridden by cmd line");
            mockPropertiesFetcher.PropertiesToReturn.Add("server.only", "server value 3 - only on server");
            mockPropertiesFetcher.PropertiesToReturn.Add("xxx", "server value xxx - lower case");

            string[] validArgs = new string[] {
                "/k:key", "/n:name", "/v:1.0",

                "/d:shared.key1=cmd line value1 - should override server value",
                "/d:cmd.line.only=cmd line value4 - only on command line",
                "/d:XXX=cmd line value XXX - upper case",
                "/d:sonar.host.url=http://host" };

            string configFilePath;
            using (PreprocessTestUtils.CreateValidLegacyTeamBuildScope("tfs uri", "build uri"))
            {
                TeamBuildSettings settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                configFilePath = settings.AnalysisConfigFilePath;

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockPropertiesFetcher, mockRulesetGenerator, mockTargetsInstaller);

                // Act
                bool executed = preProcessor.Execute(validArgs, logger);
                Assert.IsTrue(executed);
            }

            // Assert
            AssertConfigFileExists(configFilePath);
            mockPropertiesFetcher.AssertFetchPropertiesCalled();
            mockTargetsInstaller.AssertsTargetsCopied();

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            AnalysisConfig actualConfig = AnalysisConfig.Load(configFilePath);
            AssertExpectedAnalysisSetting("shared.key1", "cmd line value1 - should override server value", actualConfig);
            AssertExpectedAnalysisSetting("server.only", "server value 3 - only on server", actualConfig);
            AssertExpectedAnalysisSetting("cmd.line.only", "cmd line value4 - only on command line", actualConfig);
            AssertExpectedAnalysisSetting("xxx", "server value xxx - lower case", actualConfig);
            AssertExpectedAnalysisSetting("XXX", "cmd line value XXX - upper case", actualConfig);
            AssertExpectedAnalysisSetting(SonarProperties.HostUrl, "http://host", actualConfig);
        }

        #endregion Tests

        #region Checks

        private static void AssertConfigFileExists(string filePath)
        {
            Assert.IsTrue(File.Exists(filePath), "Expecting the analysis config file to exist. Path: {0}", filePath);
        }

        private static void AssertExpectedAnalysisSetting(string key, string expectedValue, AnalysisConfig actualConfig)
        {
            AnalysisSetting setting;
            actualConfig.TryGetSetting(key, out setting);

            Assert.IsNotNull(setting, "Failed to retrieve the expected setting. Key: {0}", key);
            Assert.AreEqual(expectedValue, setting.Value, "Unexpected setting value. Key: {0}", key);
        }

        #endregion Checks
    }
}