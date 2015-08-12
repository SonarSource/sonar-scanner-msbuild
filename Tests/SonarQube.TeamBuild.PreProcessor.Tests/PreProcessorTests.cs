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
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
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
            using (new WorkingDirectoryScope(testDir))
            {
                TeamBuildSettings settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                configFilePath = settings.AnalysisConfigFilePath;

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockPropertiesFetcher, mockRulesetGenerator, mockTargetsInstaller);

                // Act
                bool success = preProcessor.Execute(validArgs, logger);
                Assert.IsTrue(success, "Expecting the pre-processing to complete successfully");
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

        [TestMethod]
        [WorkItem(127)] // Do not store the db and server credentials in the config files: http://jira.sonarsource.com/browse/SONARMSBRU-127
        public void PreProc_AnalysisConfigDoesNotContainSensitiveData()
        {
            // Arrange
            string configFilePath;
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            TestLogger logger = new TestLogger();

            TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(new MockPropertiesFetcher(), new MockRulesetGenerator(), new MockTargetsInstaller());
            
            string[] validArgs = new string[] {
                // Public args - should be written to the config file
                "/k:key", "/n:name", "/v:1.0",
                "/d:sonar.host.url=http://host",

                // Sensitive values - should not be written to the config file
                "/d:sonar.login=secret login",
                "/d:sonar.password=secret password",
                "/d:sonar.jdbc.username=secret db password",
                "/d:sonar.jdbc.password=secret db password"
            };

            using (new WorkingDirectoryScope(testDir))
            {
                configFilePath = TeamBuildSettings.GetSettingsFromEnvironment(logger).AnalysisConfigFilePath;

                // Act
                bool success = preProcessor.Execute(validArgs, logger);
                Assert.IsTrue(success, "Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertConfigFileExists(configFilePath);
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            // Check the config
            AnalysisConfig actualConfig = AnalysisConfig.Load(configFilePath);

            // "Public" arguments should be in the file
            AssertExpectedAnalysisSetting(SonarProperties.ProjectKey, "key", actualConfig);
            AssertExpectedAnalysisSetting(SonarProperties.ProjectName, "name", actualConfig);
            AssertExpectedAnalysisSetting(SonarProperties.ProjectVersion, "1.0", actualConfig);
            AssertExpectedAnalysisSetting(SonarProperties.HostUrl, "http://host", actualConfig);

            // Sensitive arguments should be in the file
            AssertSettingDoesNotExist(SonarProperties.SonarUserName, actualConfig);
            AssertSettingDoesNotExist(SonarProperties.SonarPassword, actualConfig);
            AssertSettingDoesNotExist(SonarProperties.DbUserName, actualConfig);
            AssertSettingDoesNotExist(SonarProperties.DbPassword, actualConfig);
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

        private static void AssertSettingDoesNotExist(string key, AnalysisConfig actualConfig)
        {
            AnalysisSetting setting;
            bool found = actualConfig.TryGetSetting(key, out setting);
            Assert.IsFalse(found, "The setting should not exist. Key: {0}", key);
        }

        #endregion Checks
    }
}