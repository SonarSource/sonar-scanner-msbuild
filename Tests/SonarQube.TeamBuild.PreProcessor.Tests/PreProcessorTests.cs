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
using System.Net;
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
            ProcessedArgs validArgs = new ProcessedArgs("key", "name", "ver", EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance);

            MockPropertiesFetcher mockPropertiesFetcher = new MockPropertiesFetcher();
            MockRulesetGenerator mockRulesetGenerator = new MockRulesetGenerator();
            TeamBuildPreProcessor preprocessor = new TeamBuildPreProcessor(mockPropertiesFetcher, mockRulesetGenerator);

            // Act and assert
            AssertException.Expects<ArgumentNullException>(() => preprocessor.Execute(null, validLogger));
            AssertException.Expects<ArgumentNullException>(() => preprocessor.Execute(validArgs, null));

            string expectedConfigFileName;
        }

        [TestMethod]
        public void PreProc_FileProperties_NotSupplied()
        {
            // No file properties are supplied; specifically, the SonarQube url is not supplied
            // so execution should fail

            // Arrange
            MockPropertiesFetcher mockPropertiesFetcher = new MockPropertiesFetcher();
            MockRulesetGenerator mockRulesetGenerator = new MockRulesetGenerator();
            TestLogger logger = new TestLogger();

            string expectedConfigFileName;

            using (PreprocessTestUtils.CreateValidLegacyTeamBuildScope("tfs uri", "http://builduri"))
            {
                TeamBuildSettings settings = TeamBuildSettings.GetSettingsFromEnvironment(new ConsoleLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                expectedConfigFileName = settings.AnalysisConfigFilePath;

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockPropertiesFetcher, mockRulesetGenerator);

                // Act
                ProcessedArgs args = new ProcessedArgs("key", "name", "ver", EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance);
                
                bool executed = preProcessor.Execute(args, logger);

                // Assert
                Assert.IsFalse(executed);
            }

            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists(SonarQube.TeamBuild.PreProcessor.Resources.ERROR_NoHostUrl);
            mockRulesetGenerator.AssertGenerateCalled(0);
        }

        [TestMethod]
        public void PreProc_FileProperties_Supplied()
        {
            // Arrange

            MockPropertiesFetcher mockPropertiesFetcher = new MockPropertiesFetcher();
            MockRulesetGenerator mockRulesetGenerator = new MockRulesetGenerator();
            TestLogger logger = new TestLogger();

            // Create the list of file properties
            ListPropertiesProvider fileProperties = new ListPropertiesProvider();
            fileProperties.AddProperty(SonarProperties.HostUrl, "my url");
            fileProperties.AddProperty(SonarProperties.SonarUserName, "my user name");
            fileProperties.AddProperty(SonarProperties.SonarPassword, "my password");

            string expectedConfigFilePath;

            using (PreprocessTestUtils.CreateValidLegacyTeamBuildScope("tfs uri", "build uri"))
            {
                TeamBuildSettings settings = TeamBuildSettings.GetSettingsFromEnvironment(new ConsoleLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                expectedConfigFilePath = settings.AnalysisConfigFilePath;

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockPropertiesFetcher, mockRulesetGenerator);

                // Act
                ProcessedArgs args = new ProcessedArgs("key", "name", "ver", new ListPropertiesProvider(), fileProperties);
                bool executed = preProcessor.Execute(args, logger);
                Assert.IsTrue(executed);
            }

            // Assert
            AssertConfigFileExists(expectedConfigFilePath);

            mockPropertiesFetcher.AssertFetchPropertiesCalled();
            mockPropertiesFetcher.CheckFetcherArguments("my url", "key");

            mockRulesetGenerator.AssertGenerateCalled(2);
            mockRulesetGenerator.CheckGeneratorArguments("my url", "csharp", "cs", "fxcop", "key", "SonarQubeFxCop-cs.ruleset");
            mockRulesetGenerator.CheckGeneratorArguments("my url", "vbnet", "vbnet", "fxcop-vbnet", "key", "SonarQubeFxCop-vbnet.ruleset");

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void PreProc_NoHostUrl()
        {
            // Arrange
            MockPropertiesFetcher mockPropertiesFetcher = new MockPropertiesFetcher();
            mockPropertiesFetcher.FetchException = new System.Net.WebException("fail", WebExceptionStatus.ConnectFailure);

            MockRulesetGenerator mockRulesetGenerator = new MockRulesetGenerator();
            TestLogger logger = new TestLogger();
            ProcessedArgs args = null;

            using (PreprocessTestUtils.CreateValidLegacyTeamBuildScope("tfs uri", "build uri"))
            {
                TeamBuildSettings settings = TeamBuildSettings.GetSettingsFromEnvironment(new ConsoleLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockPropertiesFetcher, mockRulesetGenerator);

                // Act
                args = new ProcessedArgs("key", "name", "ver", new ListPropertiesProvider(), new ListPropertiesProvider());
                bool executed = preProcessor.Execute(args, logger);
                Assert.IsFalse(executed);
            }

            // Assert
            logger.AssertErrorsLogged(1);
            logger.AssertErrorLogged(PreProcessor.Resources.ERROR_NoHostUrl);
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

            // The set of server properties to return
            mockPropertiesFetcher.PropertiesToReturn.Add("shared.key1", "server value 1 - should be overridden by cmd line");
            mockPropertiesFetcher.PropertiesToReturn.Add("shared.key2", "server value 2 - should be overridden by file");
            mockPropertiesFetcher.PropertiesToReturn.Add("server.only", "server value 3 - only on server");
            mockPropertiesFetcher.PropertiesToReturn.Add("xxx", "server value xxx - lower case");

            // The set of command line properties to supply
            ListPropertiesProvider cmdLineProperties = new ListPropertiesProvider();
            cmdLineProperties.AddProperty("shared.key1", "cmd line value1 - should override server value");
            cmdLineProperties.AddProperty("cmd.line.only", "cmd line value4 - only on command line");
            cmdLineProperties.AddProperty("XXX", "cmd line value XXX - upper case");
            cmdLineProperties.AddProperty(SonarProperties.HostUrl, "http://host");

            // The set of file properties to supply
            ListPropertiesProvider fileProperties = new ListPropertiesProvider();
            fileProperties.AddProperty("shared.key1", "file value1 - should be overridden");
            fileProperties.AddProperty("shared.key2", "file value2 - should override server value");
            fileProperties.AddProperty("file.only", "file value3 - only in file");
            fileProperties.AddProperty("XXX", "cmd line value XXX - upper case");

            string configFilePath;
            using (PreprocessTestUtils.CreateValidLegacyTeamBuildScope("tfs uri", "build uri"))
            {
                TeamBuildSettings settings = TeamBuildSettings.GetSettingsFromEnvironment(new ConsoleLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                configFilePath = settings.AnalysisConfigFilePath;

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockPropertiesFetcher, mockRulesetGenerator);

                // Act
                ProcessedArgs args = new ProcessedArgs("key", "name", "ver", cmdLineProperties, fileProperties);
                bool executed = preProcessor.Execute(args, logger);
                Assert.IsTrue(executed);
            }

            // Assert
            AssertConfigFileExists(configFilePath);
            mockPropertiesFetcher.AssertFetchPropertiesCalled();

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            AnalysisConfig actualConfig = AnalysisConfig.Load(configFilePath);
            AssertExpectedAnalysisSetting("shared.key1", "cmd line value1 - should override server value", actualConfig);
            AssertExpectedAnalysisSetting("shared.key2", "file value2 - should override server value", actualConfig);
            AssertExpectedAnalysisSetting("server.only", "server value 3 - only on server", actualConfig);
            AssertExpectedAnalysisSetting("cmd.line.only", "cmd line value4 - only on command line", actualConfig);
            AssertExpectedAnalysisSetting("file.only", "file value3 - only in file", actualConfig);
            AssertExpectedAnalysisSetting("xxx", "server value xxx - lower case", actualConfig);
            AssertExpectedAnalysisSetting("XXX", "cmd line value XXX - upper case", actualConfig);
            AssertExpectedAnalysisSetting(SonarProperties.HostUrl, "http://host", actualConfig);
        }

        [TestMethod]
        [Description("Checks that the pre-processor logs the expected message if it appears to be called from the 0.9 bootstrapper")]
        public void PreProc_CalledFromV0_9()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "sqtemp");
            TestLogger logger = new TestLogger();

            // Act
            ProcessRunner runner = new ProcessRunner();
            bool success = runner.Execute(typeof(TeamBuildPreProcessor).Assembly.Location , "begin", testDir, logger);

            // Assert
            Assert.IsFalse(success);
            logger.AssertSingleErrorExists(SonarQube.TeamBuild.PreProcessor.Resources.ERROR_InvokedFromBootstrapper0_9);
            logger.AssertErrorsLogged(1);
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