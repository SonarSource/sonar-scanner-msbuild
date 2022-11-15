/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.Common.TFS;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class PreProcessorTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task PreProc_InvalidArgs()
        {
            // Arrange
            var mockServer = new MockSonarQubeServer();
            var preprocessor = new TeamBuildPreProcessor(new MockObjectFactory(mockServer, Mock.Of<ITargetsInstaller>(), new MockRoslynAnalyzerProvider()), new TestLogger());

            // Act and assert
            Func<Task> act = async () => await preprocessor.Execute(null);
            await act.Should().ThrowExactlyAsync<ArgumentNullException>();
        }

        [TestMethod]
        public void PreProc_EndToEnd_SuccessCase()
        {
            // Checks end-to-end happy path for the pre-processor i.e.
            // * arguments are parsed
            // * targets are installed
            // * server properties are fetched
            // * rule sets are generated
            // * config file is created

            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
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
                .AddRule(new SonarRule("csharpsquid", "cs.rule3"));

            data.AddQualityProfile("qp2", "vbnet", null)
                .AddProject("key")
                .AddRule(new SonarRule("vbnet", "vb.rule3"));

            var mockAnalyzerProvider = new MockRoslynAnalyzerProvider
            {
                SettingsToReturn = new AnalyzerSettings
                {
                    RulesetPath = "c:\\xxx.ruleset"
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
                var success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0")).Result;
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoriesCreated(settings);

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetAllLanguages", 1);
            mockServer.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetRules", 2); // C# and VBNet

            logger.DebugMessages.Should().Contain("Base branch parameter was not provided. Incremental PR analysis is disabled.");

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, logger);
        }

        [TestMethod]
        public async Task PreProc_WithPullRequestBranch()
        {
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var logger = new TestLogger();
            var mockServer = new MockSonarQubeServer();
            mockServer.Data.Languages.Add("cs");

            var mockFactory = new MockObjectFactory(mockServer, Mock.Of<ITargetsInstaller>(), Mock.Of<IAnalyzerProvider>());

            using var teamBuildScope = PreprocessTestUtils.CreateValidNonTeamBuildScope();
            using var directoryScope = new WorkingDirectoryScope(workingDir);
            var preProcessor = new TeamBuildPreProcessor(mockFactory, logger);

            // Act
            var args = CreateArgs("key", "name", new Dictionary<string, string> { { SonarProperties.PullRequestBase, "BASE_BRANCH" } }).ToArray();
            var success = await preProcessor.Execute(args);
            success.Should().BeTrue("Expecting the pre-processing to complete successfully");

            logger.InfoMessages.Should().Contain("Processing pull request with base branch 'BASE_BRANCH'.");
        }

        [TestMethod]
        public void PreProc_EndToEnd_SuccessCase_NoActiveRule()
        {
            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var logger = new TestLogger();

            // Configure the server
            var mockServer = new MockSonarQubeServer();

            var data = mockServer.Data;
            data.ServerProperties.Add("server.key", "server value 1");

            data.Languages.Add("cs");
            data.Languages.Add("vbnet");
            data.Languages.Add("another_plugin");

            data.AddQualityProfile("qp1", "cs", null)
                .AddProject("key");

            data.AddQualityProfile("qp2", "vbnet", null)
                .AddProject("key")
                .AddRule(new SonarRule("vbnet", "vb.rule3"));

            var mockAnalyzerProvider = new MockRoslynAnalyzerProvider
            {
                SettingsToReturn = new AnalyzerSettings
                {
                    RulesetPath = "c:\\xxx.ruleset"
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
                var success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0")).Result;
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoriesCreated(settings);

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetAllLanguages", 1);
            mockServer.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetRules", 2); // C# and VBNet

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, logger);
        }

        [TestMethod]
        public void PreProc_EndToEnd_SuccessCase_With_Organization()
        {
            // Checks end-to-end happy path for the pre-processor i.e.
            // * arguments are parsed
            // * targets are installed
            // * server properties are fetched
            // * rule sets are generated
            // * config file is created

            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
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
                .AddRule(new SonarRule("csharpsquid", "cs.rule3"));

            data.AddQualityProfile("qp2", "vbnet", "organization")
                .AddProject("key")
                .AddRule(new SonarRule("vbnet", "vb.rule3"));

            var mockAnalyzerProvider = new MockRoslynAnalyzerProvider
            {
                SettingsToReturn = new AnalyzerSettings
                {
                    RulesetPath = "c:\\xxx.ruleset"
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
                var success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0", "organization")).Result;
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoriesCreated(settings);

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetAllLanguages", 1);
            mockServer.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetRules", 2); // C# and VBNet

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, logger);
        }

        [DataTestMethod]
        [DataRow("6.7.0.22152", true)]
        [DataRow("8.8.0.1121", false)]
        public void PreProc_EndToEnd_ShouldWarnOrNot_SonarQubeDeprecatedVersion(string sqVersion, bool shouldWarn)
        {
            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var logger = new TestLogger();

            // Configure the server
            var mockServer = new MockSonarQubeServer();

            var data = mockServer.Data;
            data.ServerProperties.Add("server.key", "server value 1");
            data.SonarQubeVersion = new Version(sqVersion);

            data.Languages.Add("cs");
            data.Languages.Add("vbnet");
            data.Languages.Add("another_plugin");

            data.AddQualityProfile("qp1", "cs", "organization")
                .AddProject("key")
                .AddRule(new SonarRule("csharpsquid", "cs.rule3"));

            data.AddQualityProfile("qp2", "vbnet", "organization")
                .AddProject("key")
                .AddRule(new SonarRule("vbnet", "vb.rule3"));

            var mockAnalyzerProvider = new MockRoslynAnalyzerProvider
            {
                SettingsToReturn = new AnalyzerSettings
                {
                    RulesetPath = "c:\\xxx.ruleset"
                }
            };

            var mockTargetsInstaller = new Mock<ITargetsInstaller>();
            var mockFactory = new MockObjectFactory(mockServer, mockTargetsInstaller.Object, mockAnalyzerProvider);

            using (PreprocessTestUtils.CreateValidNonTeamBuildScope())
            using (new WorkingDirectoryScope(workingDir))
            {
                var settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                settings.Should().NotBeNull("Test setup error: TFS environment variables have not been set correctly");
                settings.BuildEnvironment.Should().Be(BuildEnvironment.NotTeamBuild, "Test setup error: build environment was not set correctly");

                var preProcessor = new TeamBuildPreProcessor(mockFactory, logger);

                // Act
                var success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0", "organization")).Result;
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            }

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());

            if (shouldWarn)
            {
                mockServer.AssertWarningWritten("version is below supported");
            }
            else
            {
                mockServer.AssertNoWarningWritten();
            }
        }

        [TestMethod]
        public void PreProc_NoPlugin()
        {
            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
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
                    RulesetPath = "c:\\xxx.ruleset"
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
                var success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0")).Result;
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoriesCreated(settings);

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetAllLanguages", 1);
            mockServer.AssertMethodCalled("TryGetQualityProfile", 0); // No valid plugin
            mockServer.AssertMethodCalled("GetRules", 0); // No valid plugin

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 0, logger);

            // only contains SonarQubeAnalysisConfig (no rulesets or additional files)
            AssertDirectoryContains(settings.SonarConfigDirectory, Path.GetFileName(settings.AnalysisConfigFilePath));
        }

        [TestMethod]
        public void PreProc_NoProject()
        {
            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
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
                .AddRule(new SonarRule("fxcop", "cs.rule1"))
                .AddRule(new SonarRule("fxcop", "cs.rule2"));

            data.AddQualityProfile("qp2", "vbnet", null)
                .AddProject("invalid")
                .AddRule(new SonarRule("fxcop-vbnet", "vb.rule1"))
                .AddRule(new SonarRule("fxcop-vbnet", "vb.rule2"));

            var mockAnalyzerProvider = new MockRoslynAnalyzerProvider
            {
                SettingsToReturn = new AnalyzerSettings
                {
                    RulesetPath = "c:\\xxx.ruleset"
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
                var success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0", null)).Result;
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoriesCreated(settings);

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetAllLanguages", 1);
            mockServer.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetRules", 0); // no quality profile assigned to project

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 0, logger);

            // only contains SonarQubeAnalysisConfig (no rulesets or additional files)
            AssertDirectoryContains(settings.SonarConfigDirectory, Path.GetFileName(settings.AnalysisConfigFilePath));
        }

        [TestMethod]
        public void PreProc_HandleAnalysisException()
        {
            // Checks end-to-end behavior when AnalysisException is thrown inside FetchArgumentsAndRulesets
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var logger = new TestLogger();
            var mockServer = new ThrowingSonarQubeServer();
            var mockFactory = new MockObjectFactory(mockServer, new Mock<ITargetsInstaller>().Object, null);
            using (new WorkingDirectoryScope(workingDir))
            {
                var preProcessor = new TeamBuildPreProcessor(mockFactory, logger);
                var success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0", "InvalidOrganization")).Result;    // Should not throw
                success.Should().BeFalse("Expecting the pre-processing to fail");
                mockServer.AnalysisExceptionThrown.Should().BeTrue();
            }
        }

        [TestMethod]
        // Regression test for https://github.com/SonarSource/sonar-scanner-msbuild/issues/699
        public void PreProc_EndToEnd_Success_LocalSettingsAreUsedInSonarLintXML()
        {
            // Checks that local settings are used when creating the SonarLint.xml file,
            // overriding

            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var logger = new TestLogger();

            // Configure the server
            var mockServer = new MockSonarQubeServer();

            var data = mockServer.Data;
            data.Languages.Add("cs");
            data.AddQualityProfile("qp1", "cs", null)
                .AddProject("key")
                .AddRule(new SonarRule("csharpsquid", "cs.rule3"));

            // Server-side settings
            data.ServerProperties.Add("server.key", "server value 1");
            data.ServerProperties.Add("shared.key1", "server shared value 1");
            data.ServerProperties.Add("shared.CASING", "server upper case value");

            // Local settings that should override matching server settings
            var args = new List<string>(CreateValidArgs("key", "name", "1.0"));
            args.Add("/d:local.key=local value 1");
            args.Add("/d:shared.key1=local shared value 1 - should override server value");
            args.Add("/d:shared.casing=local lower case value");

            var mockAnalyzerProvider = new MockRoslynAnalyzerProvider
            {
                SettingsToReturn = new AnalyzerSettings
                {
                    RulesetPath = "c:\\xxx.ruleset"
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
                var success = preProcessor.Execute(args.ToArray()).Result;
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            }

            // Assert

            // Check the settings used when creating the SonarLint file - local and server settings should be merged
            mockAnalyzerProvider.SuppliedSonarProperties.Should().NotBeNull();
            mockAnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("server.key", "server value 1");
            mockAnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("local.key", "local value 1");
            mockAnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("shared.key1", "local shared value 1 - should override server value");
            // Keys are case-sensitive so differently cased values should be preserved
            mockAnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("shared.CASING", "server upper case value");
            mockAnalyzerProvider.SuppliedSonarProperties.AssertExpectedPropertyValue("shared.casing", "local lower case value");

            // Check the settings used when creating the config file - settings should be separate
            var actualConfig = AssertAnalysisConfig(settings.AnalysisConfigFilePath, 1, logger);

            AssertExpectedLocalSetting("local.key", "local value 1", actualConfig);
            AssertExpectedLocalSetting("shared.key1", "local shared value 1 - should override server value", actualConfig);
            AssertExpectedLocalSetting("shared.casing", "local lower case value", actualConfig);

            AssertExpectedServerSetting("server.key", "server value 1", actualConfig);
            AssertExpectedServerSetting("shared.key1", "server shared value 1", actualConfig);
            AssertExpectedServerSetting("shared.CASING", "server upper case value", actualConfig);
        }

        private IEnumerable<string> CreateArgs(string projectKey, string projectName, Dictionary<string, string> properties)
        {
            yield return $"/k:{projectKey}";
            yield return $"/n:{projectName}";

            foreach (var pair in properties)
            {
                yield return $"/d:{pair.Key}={pair.Value}";
            }
        }

        private static string[] CreateValidArgs(string projectKey, string projectName, string projectVersion) =>
            new[]
            {
                $"/k:{projectKey}",
                $"/n:{projectName}",
                $"/v:{projectVersion}",
                "/d:cmd.line1=cmdline.value.1",
                "/d:sonar.host.url=http://host",
                "/d:sonar.log.level=INFO|DEBUG"
            };

        private static string[] CreateValidArgs(string projectKey, string projectName, string projectVersion, string organization) =>
            new[]
            {
                $"/k:{projectKey}",
                $"/n:{projectName}",
                $"/v:{projectVersion}",
                $"/o:{organization}",
                "/d:cmd.line1=cmdline.value.1",
                "/d:sonar.host.url=http://host",
                "/d:sonar.log.level=INFO|DEBUG"
            };

        private static void AssertDirectoriesCreated(ITeamBuildSettings settings)
        {
            AssertDirectoryExists(settings.AnalysisBaseDirectory);
            AssertDirectoryExists(settings.SonarConfigDirectory);
            AssertDirectoryExists(settings.SonarOutputDirectory);
            // The bootstrapper is responsible for creating the bin directory
        }

        private AnalysisConfig AssertAnalysisConfig(string filePath, int noAnalyzers, TestLogger logger)
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

            return actualConfig;
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
            var found = Property.TryGetProperty(key, actualConfig.LocalSettings, out var actualProperty);

            found.Should().BeTrue("Failed to find the expected local setting: {0}", key);
            actualProperty.Value.Should().Be(expectedValue, "Unexpected property value. Key: {0}", key);
        }

        private static void AssertExpectedServerSetting(string key, string expectedValue, AnalysisConfig actualConfig)
        {
            var found = Property.TryGetProperty(key, actualConfig.ServerSettings, out var actualProperty);

            found.Should().BeTrue("Failed to find the expected server setting: {0}", key);
            actualProperty.Value.Should().Be(expectedValue, "Unexpected property value. Key: {0}", key);
        }

        private static void AssertDirectoryExists(string path) =>
            Directory.Exists(path).Should().BeTrue("Expected directory does not exist: {0}", path);

        private class ThrowingSonarQubeServer : ISonarQubeServer
        {
            public bool AnalysisExceptionThrown { get; private set; }

            public Task<IEnumerable<string>> GetAllLanguages() =>
                Task.FromResult(new[] { TeamBuildPreProcessor.CSharpLanguage, TeamBuildPreProcessor.VBNetLanguage }.AsEnumerable());

            public Task<IDictionary<string, string>> GetProperties(string projectKey, string projectBranch) =>
                Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string>());

            public Task<IList<SonarRule>> GetRules(string qProfile) =>
                throw new NotImplementedException();

            public Task<Version> GetServerVersion() =>
                Task.FromResult(new Version(8, 0));

            public Task<bool> IsServerLicenseValid() =>
                Task.FromResult(true);

            public Task<bool> TryDownloadEmbeddedFile(string pluginKey, string embeddedFileName, string targetDirectory) =>
                throw new NotImplementedException();

            public Task<Tuple<bool, string>> TryGetQualityProfile(string projectKey, string projectBranch, string organization, string language)
            {
                AnalysisExceptionThrown = true;
                throw new AnalysisException("This message and stacktrace should not propagate to the users");
            }

            public Task WarnIfSonarQubeVersionIsDeprecated() =>
                Task.CompletedTask;
        }
    }
}
