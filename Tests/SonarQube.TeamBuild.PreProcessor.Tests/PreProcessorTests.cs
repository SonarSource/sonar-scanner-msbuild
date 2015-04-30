//-----------------------------------------------------------------------
// <copyright file="PreProcessorTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System.IO;
using System.Text;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class PreProcessorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PreProc_EmptySonarRunnerProperties()
        {
            // Checks the pre-processor creates a valid config file

            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string propertiesFile = CreateEmptyPropertiesFile(testDir);

            MockPropertiesFetcher mockPropertiesFetcher = new MockPropertiesFetcher();
            MockRulesetGenerator mockRulesetGenerator = new MockRulesetGenerator();
            TestLogger logger = new TestLogger();

            string expectedConfigFileName;

            using (PreprocessTestUtils.CreateValidLegacyTeamBuildScope("tfs uri", "http://builduri", testDir))
            {
                TeamBuildSettings settings = TeamBuildSettings.GetSettingsFromEnvironment(new ConsoleLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                expectedConfigFileName = settings.AnalysisConfigFilePath;

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockPropertiesFetcher, mockRulesetGenerator);

                // Act
                preProcessor.Execute(logger, "key", "name", "ver", propertiesFile);
            }

            // Assert
            Assert.IsTrue(File.Exists(expectedConfigFileName), "Config file does not exist: {0}", expectedConfigFileName);
            AnalysisConfig config = AnalysisConfig.Load(expectedConfigFileName);
            Assert.IsTrue(Directory.Exists(config.SonarOutputDir), "Output directory was not created: {0}", config.SonarOutputDir);
            Assert.IsTrue(Directory.Exists(config.SonarConfigDir), "Config directory was not created: {0}", config.SonarConfigDir);
            Assert.AreEqual("key", config.SonarProjectKey);
            Assert.AreEqual("name", config.SonarProjectName);
            Assert.AreEqual("ver", config.SonarProjectVersion);
            Assert.AreEqual("http://builduri", config.GetBuildUri());
            Assert.AreEqual("tfs uri", config.GetTfsUri());
            Assert.AreEqual(propertiesFile, config.SonarRunnerPropertiesPath);

            mockPropertiesFetcher.AssertFetchPropertiesCalled();
            mockPropertiesFetcher.CheckFetcherArguments("http://localhost:9000", "key");

            mockRulesetGenerator.AssertGenerateCalled();
            mockRulesetGenerator.CheckGeneratorArguments("http://localhost:9000", "key");
        }

        [TestMethod]
        public void PreProc_NonEmptySonarRunnerProperties()
        {
            // Checks the ruleset generator is called with the expected arguments
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string propertiesFile = CreatePropertiesFile(testDir, "my url", "my user name", "my password");

            MockPropertiesFetcher mockPropertiesFetcher = new MockPropertiesFetcher();
            MockRulesetGenerator mockRulesetGenerator = new MockRulesetGenerator();
            TestLogger logger = new TestLogger();

            string expectedConfigFileName;

            using (PreprocessTestUtils.CreateValidLegacyTeamBuildScope("tfs uri", "build uri", testDir))
            {
                TeamBuildSettings settings = TeamBuildSettings.GetSettingsFromEnvironment(new ConsoleLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                expectedConfigFileName = settings.AnalysisConfigFilePath;

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockPropertiesFetcher, mockRulesetGenerator);

                // Act
                preProcessor.Execute(logger, "key", "name", "ver", propertiesFile);
            }

            // Assert
            mockPropertiesFetcher.AssertFetchPropertiesCalled();
            mockPropertiesFetcher.CheckFetcherArguments("my url", "key");

            mockRulesetGenerator.AssertGenerateCalled();
            mockRulesetGenerator.CheckGeneratorArguments("my url", "key");
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        #endregion

        #region Private methods

        private static string CreateEmptyPropertiesFile(string outputDirectory)
        {
            return CreatePropertiesFile(outputDirectory, null, null, null);
        }

        private static string CreatePropertiesFile(string outputDirectory, string url, string userName, string password)
        {
            string propertiesFile = Path.Combine(outputDirectory, "propertiesFile.txt");
            Assert.IsFalse(File.Exists(propertiesFile), "Test setup error: the properties file already exists. File: {0}", propertiesFile);

            StringBuilder sb = new StringBuilder();
            if (url != null)
            {
                sb.AppendFormat("sonar.host.url={0}", url);
                sb.AppendLine();
            }
            if (userName != null)
            {
                sb.AppendFormat("sonar.login={0}", userName);
                sb.AppendLine();
            }
            if (password != null)
            {
                sb.AppendFormat("sonar.password={0}", password);
                sb.AppendLine();
            }
            
            File.WriteAllText(propertiesFile, sb.ToString());

            return propertiesFile;
        }

        #endregion
    }
}
