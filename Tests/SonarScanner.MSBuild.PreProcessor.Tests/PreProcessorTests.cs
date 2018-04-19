/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;
using SonarScanner.MSBuild.TFS;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Tests
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
            var mockServer = new MockSonarQubeServer();

            var preprocessor = new TeamBuildPreProcessor(
                new MockObjectFactory(mockServer, new Mock<ITargetsInstaller>().Object, new MockRoslynAnalyzerProvider()),
                new TestLogger());

            // Act and assert
            Action act = () => preprocessor.Execute(null); act.Should().ThrowExactly<ArgumentNullException>();
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
            var workingDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var logger = new TestLogger();

            // Configure the server
            var mockServer = new MockSonarQubeServer();

            var data = mockServer.Data;
            data.ServerProperties.Add("server.key", "server value 1");

            data.Languages.Add("cs");
            data.Languages.Add("vbnet");
            data.Languages.Add("another_plugin");

            data.AddQualityProfile("qp1", "cs", null)
                .AddProject("key")
                .AddRule(new ActiveRule("csharpsquid", "cs.rule3"));

            data.AddQualityProfile("qp2", "vbnet", null)
                .AddProject("key")
                .AddRule(new ActiveRule("vbnet", "vb.rule3"));

            var mockAnalyzerProvider = new MockRoslynAnalyzerProvider
            {
                SettingsToReturn = new AnalyzerSettings
                {
                    RuleSetFilePath = "c:\\xxx.ruleset"
                }
            };

            var mockTargetsInstaller = new Mock<ITargetsInstaller>();
            var mockFactory = new MockObjectFactory(mockServer, mockTargetsInstaller.Object, mockAnalyzerProvider);

            TeamBuildSettings settings;
            using (PreprocessTestUtils.CreateValidNonTeamBuildScope())
            using (new WorkingDirectoryScope(workingDir))
            {
                settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                settings.Should().NotBeNull("Test setup error: TFS environment variables have not been set correctly");
                settings.BuildEnvironment.Should().Be(BuildEnvironment.NotTeamBuild, "Test setup error: build environment was not set correctly");

                var preProcessor = new TeamBuildPreProcessor(mockFactory, logger);

                // Act
                var success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0"));
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoriesCreated(settings);

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetAllLanguages", 1);
            mockServer.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetActiveRules", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetInactiveRules", 2); // C# and VBNet

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, logger);
        }

        [TestMethod]
        public void PreProc_EndToEnd_SuccessCase_With_Organization()
        {
            // Checks end-to-end happy path for the pre-processor i.e.
            // * arguments are parsed
            // * targets are installed
            // * server properties are fetched
            // * rulesets are generated
            // * config file is created

            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var logger = new TestLogger();

            // Configure the server
            var mockServer = new MockSonarQubeServer();

            var data = mockServer.Data;
            data.ServerProperties.Add("server.key", "server value 1");

            data.Languages.Add("cs");
            data.Languages.Add("vbnet");
            data.Languages.Add("another_plugin");

            data.AddQualityProfile("qp1", "cs", "organization")
                .AddProject("key")
                .AddRule(new ActiveRule("csharpsquid", "cs.rule3"));

            data.AddQualityProfile("qp2", "vbnet", "organization")
                .AddProject("key")
                .AddRule(new ActiveRule("vbnet", "vb.rule3"));

            var mockAnalyzerProvider = new MockRoslynAnalyzerProvider
            {
                SettingsToReturn = new AnalyzerSettings
                {
                    RuleSetFilePath = "c:\\xxx.ruleset"
                }
            };

            var mockTargetsInstaller = new Mock<ITargetsInstaller>();
            var mockFactory = new MockObjectFactory(mockServer, mockTargetsInstaller.Object, mockAnalyzerProvider);

            TeamBuildSettings settings;
            using (PreprocessTestUtils.CreateValidNonTeamBuildScope())
            using (new WorkingDirectoryScope(workingDir))
            {
                settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                settings.Should().NotBeNull("Test setup error: TFS environment variables have not been set correctly");
                settings.BuildEnvironment.Should().Be(BuildEnvironment.NotTeamBuild, "Test setup error: build environment was not set correctly");

                var preProcessor = new TeamBuildPreProcessor(mockFactory, logger);

                // Act
                var success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0", "organization"));
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoriesCreated(settings);

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetAllLanguages", 1);
            mockServer.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetActiveRules", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetInactiveRules", 2); // C# and VBNet

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, logger);
        }

        [TestMethod]
        public void PreProc_NoPlugin()
        {
            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var logger = new TestLogger();

            // Configure the server
            var mockServer = new MockSonarQubeServer();

            var data = mockServer.Data;
            data.ServerProperties.Add("server.key", "server value 1");

            data.Languages.Add("invalid_plugin");

            var mockAnalyzerProvider = new MockRoslynAnalyzerProvider
            {
                SettingsToReturn = new AnalyzerSettings
                {
                    RuleSetFilePath = "c:\\xxx.ruleset"
                }
            };

            var mockTargetsInstaller = new Mock<ITargetsInstaller>();
            var mockFactory = new MockObjectFactory(mockServer, mockTargetsInstaller.Object, mockAnalyzerProvider);

            TeamBuildSettings settings;
            using (PreprocessTestUtils.CreateValidNonTeamBuildScope())
            using (new WorkingDirectoryScope(workingDir))
            {
                settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                settings.Should().NotBeNull("Test setup error: TFS environment variables have not been set correctly");
                settings.BuildEnvironment.Should().Be(BuildEnvironment.NotTeamBuild, "Test setup error: build environment was not set correctly");

                var preProcessor = new TeamBuildPreProcessor(mockFactory, logger);

                // Act
                var success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0"));
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoriesCreated(settings);

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetAllLanguages", 1);
            mockServer.AssertMethodCalled("TryGetQualityProfile", 0); // No valid plugin
            mockServer.AssertMethodCalled("GetActiveRules", 0); // No valid plugin
            mockServer.AssertMethodCalled("GetInactiveRules", 0); // No valid plugin

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 0, logger);

            // only contains SonarQubeAnalysisConfig (no rulesets or additional files)
            AssertDirectoryContains(settings.SonarConfigDirectory, Path.GetFileName(settings.AnalysisConfigFilePath));
        }

        [TestMethod]
        public void PreProc_NoProject()
        {
            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var logger = new TestLogger();

            // Configure the server
            var mockServer = new MockSonarQubeServer();

            var data = mockServer.Data;
            data.ServerProperties.Add("server.key", "server value 1");

            data.Languages.Add("cs");
            data.Languages.Add("vbnet");
            data.Languages.Add("another_plugin");

            data.AddQualityProfile("qp1", "cs", null)
                .AddProject("invalid")
                .AddRule(new ActiveRule("fxcop", "cs.rule1"))
                .AddRule(new ActiveRule("fxcop", "cs.rule2"));

            data.AddQualityProfile("qp2", "vbnet", null)
                .AddProject("invalid")
                .AddRule(new ActiveRule("fxcop-vbnet", "vb.rule1"))
                .AddRule(new ActiveRule("fxcop-vbnet", "vb.rule2"));

            var mockAnalyzerProvider = new MockRoslynAnalyzerProvider
            {
                SettingsToReturn = new AnalyzerSettings
                {
                    RuleSetFilePath = "c:\\xxx.ruleset"
                }
            };

            var mockTargetsInstaller = new Mock<ITargetsInstaller>();
            var mockFactory = new MockObjectFactory(mockServer, mockTargetsInstaller.Object, mockAnalyzerProvider);

            TeamBuildSettings settings;
            using (PreprocessTestUtils.CreateValidNonTeamBuildScope())
            using (new WorkingDirectoryScope(workingDir))
            {
                settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                settings.Should().NotBeNull("Test setup error: TFS environment variables have not been set correctly");
                settings.BuildEnvironment.Should().Be(BuildEnvironment.NotTeamBuild, "Test setup error: build environment was not set correctly");

                var preProcessor = new TeamBuildPreProcessor(mockFactory, logger);

                // Act
                var success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0", null));
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoriesCreated(settings);

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetAllLanguages", 1);
            mockServer.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetActiveRules", 0); // no quality profile assigned to project
            mockServer.AssertMethodCalled("GetInactiveRules", 0);

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 0, logger);

            // only contains SonarQubeAnalysisConfig (no rulesets or additional files)
            AssertDirectoryContains(settings.SonarConfigDirectory, Path.GetFileName(settings.AnalysisConfigFilePath));
        }

        #endregion Tests

        #region Setup

        private string[] CreateValidArgs(string projectKey, string projectName, string projectVersion)
        {
            return new string[] {
                "/k:" + projectKey, "/n:" + projectName, "/v:" + projectVersion,
                "/d:cmd.line1=cmdline.value.1",
                "/d:sonar.host.url=http://host",
                "/d:sonar.log.level=INFO|DEBUG"};
        }

        private string[] CreateValidArgs(string projectKey, string projectName, string projectVersion, string organization)
        {
            return new string[] {
                "/k:" + projectKey, "/n:" + projectName, "/v:" + projectVersion, "/o:" + organization,
                "/d:cmd.line1=cmdline.value.1",
                "/d:sonar.host.url=http://host",
                "/d:sonar.log.level=INFO|DEBUG"};
        }

        #endregion Setup

        #region Checks

        private void AssertDirectoriesCreated(TeamBuildSettings settings)
        {
            AssertDirectoryExists(settings.AnalysisBaseDirectory);
            AssertDirectoryExists(settings.SonarConfigDirectory);
            AssertDirectoryExists(settings.SonarOutputDirectory);
            // The bootstrapper is responsible for creating the bin directory
        }

        private void AssertAnalysisConfig(string filePath, int noAnalyzers, TestLogger logger)
        {
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
            logger.AssertVerbosity(LoggerVerbosity.Debug);

            AssertConfigFileExists(filePath);
            var actualConfig = AnalysisConfig.Load(filePath);

            actualConfig.SonarProjectKey.Should().Be("key", "Unexpected project key");
            actualConfig.SonarProjectName.Should().Be("name", "Unexpected project name");
            actualConfig.SonarProjectVersion.Should().Be("1.0", "Unexpected project version");

            actualConfig.AnalyzersSettings.Should().NotBeNull("Analyzer settings should not be null");
            actualConfig.AnalyzersSettings.Should().HaveCount(noAnalyzers);

            AssertExpectedLocalSetting(SonarProperties.HostUrl, "http://host", actualConfig);
            AssertExpectedLocalSetting("cmd.line1", "cmdline.value.1", actualConfig);
            AssertExpectedServerSetting("server.key", "server value 1", actualConfig);
        }

        private void AssertConfigFileExists(string filePath)
        {
            File.Exists(filePath).Should().BeTrue("Expecting the analysis config file to exist. Path: {0}", filePath);
            TestContext.AddResultFile(filePath);
        }

        private void AssertDirectoryContains(string dirPath, params string[] fileNames)
        {
            Directory.Exists(dirPath);
            var actualFileNames = Directory.GetFiles(dirPath).Select(f => Path.GetFileName(f));
            actualFileNames.Should().BeEquivalentTo(fileNames);
        }

        private static void AssertExpectedLocalSetting(string key, string expectedValue, AnalysisConfig actualConfig)
        {
            var found = Property.TryGetProperty(key, actualConfig.LocalSettings, out Property actualProperty);

            found.Should().BeTrue("Failed to find the expected local setting: {0}", key);
            actualProperty.Value.Should().Be(expectedValue, "Unexpected property value. Key: {0}", key);
        }

        private static void AssertExpectedServerSetting(string key, string expectedValue, AnalysisConfig actualConfig)
        {
            var found = Property.TryGetProperty(key, actualConfig.ServerSettings, out Property actualProperty);

            found.Should().BeTrue("Failed to find the expected server setting: {0}", key);
            actualProperty.Value.Should().Be(expectedValue, "Unexpected property value. Key: {0}", key);
        }

        private static void AssertDirectoryExists(string path)
        {
            Directory.Exists(path).Should().BeTrue("Expected directory does not exist: {0}", path);
        }

        private static string AssertFileExists(string directory, string fileName)
        {
            var fullPath = Path.Combine(directory, fileName);
            File.Exists(fullPath).Should().BeTrue("Expected file does not exist");
            return fullPath;
        }

        #endregion Checks
    }
}
