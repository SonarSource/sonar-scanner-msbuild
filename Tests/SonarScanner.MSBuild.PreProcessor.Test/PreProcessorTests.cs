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
        public void PreProc_InvalidArgs()
        {
            var factory = new MockObjectFactory();
            var preProcessor = new PreProcessor(factory, factory.Logger);
            preProcessor.Invoking(async x => await x.Execute(null)).Should().ThrowExactlyAsync<ArgumentNullException>();
        }

        [TestMethod]
        public async Task PreProc_EndToEnd_SuccessCase()
        {
            // Checks end-to-end happy path for the pre-processor i.e.
            // * arguments are parsed
            // * targets are installed
            // * server properties are fetched
            // * rule sets are generated
            // * config file is created
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var factory = new MockObjectFactory();
            using var teamBuildScope = PreprocessTestUtils.CreateValidNonTeamBuildScope();
            using var directoryScope = new WorkingDirectoryScope(workingDir);
            var settings = TeamBuildSettings.GetSettingsFromEnvironment(factory.Logger);
            settings.Should().NotBeNull("Test setup error: TFS environment variables have not been set correctly");
            settings.BuildEnvironment.Should().Be(BuildEnvironment.NotTeamBuild, "Test setup error: build environment was not set correctly");
            var preProcessor = new PreProcessor(factory, factory.Logger);

            var success = await preProcessor.Execute(CreateArgs());
            success.Should().BeTrue("Expecting the pre-processing to complete successfully");

            AssertDirectoriesCreated(settings);

            factory.TargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            factory.Server.AssertMethodCalled("GetProperties", 1);
            factory.Server.AssertMethodCalled("GetAllLanguages", 1);
            factory.Server.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            factory.Server.AssertMethodCalled("GetRules", 2); // C# and VBNet

            factory.Logger.AssertDebugLogged("Base branch parameter was not provided. Incremental PR analysis is disabled.");
            factory.Logger.AssertDebugLogged("Processing analysis cache");

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, factory.Logger);
        }

        [TestMethod]
        public async Task PreProc_WithPullRequestBranch()
        {
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var factory = new MockObjectFactory();
            factory.Server.Data.Languages.Add("cs");
            using var teamBuildScope = PreprocessTestUtils.CreateValidNonTeamBuildScope();
            using var directoryScope = new WorkingDirectoryScope(workingDir);
            var preProcessor = new PreProcessor(mockFactory, logger);

            var args = CreateArgs(properties: new Dictionary<string, string> { { SonarProperties.PullRequestBase, "BASE_BRANCH" } });
            var success = await preProcessor.Execute(args);
            success.Should().BeTrue("Expecting the pre-processing to complete successfully");

            factory.Logger.InfoMessages.Should().Contain("Processing pull request with base branch 'BASE_BRANCH'.");
        }

        [TestMethod]
        public async Task PreProc_EndToEnd_SuccessCase_NoActiveRule()
        {
            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var factory = new MockObjectFactory();
            factory.Server.Data.FindProfile("qp1").Rules.Clear();
            using var teamBuildScope = PreprocessTestUtils.CreateValidNonTeamBuildScope();
            using var directoryScope = new WorkingDirectoryScope(workingDir);
            var settings = TeamBuildSettings.GetSettingsFromEnvironment(factory.Logger);
            settings.Should().NotBeNull("Test setup error: TFS environment variables have not been set correctly");
            settings.BuildEnvironment.Should().Be(BuildEnvironment.NotTeamBuild, "Test setup error: build environment was not set correctly");
            var preProcessor = new PreProcessor(factory, factory.Logger);

                var preProcessor = new PreProcessor(mockFactory, logger);

                // Act
                var success = await preProcessor.Execute(CreateArgs());
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            factory.TargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            factory.Server.AssertMethodCalled("GetProperties", 1);
            factory.Server.AssertMethodCalled("GetAllLanguages", 1);
            factory.Server.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            factory.Server.AssertMethodCalled("GetRules", 2); // C# and VBNet

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, factory.Logger);
        }

        [TestMethod]
        public async Task PreProc_EndToEnd_SuccessCase_With_Organization()
        {
            // Checks end-to-end happy path for the pre-processor i.e.
            // * arguments are parsed
            // * targets are installed
            // * server properties are fetched
            // * rule sets are generated
            // * config file is created
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var factory = new MockObjectFactory(organization: "organization");
            using var teamBuildScoppe = PreprocessTestUtils.CreateValidNonTeamBuildScope();
            using var directoryScope = new WorkingDirectoryScope(workingDir);
            var settings = TeamBuildSettings.GetSettingsFromEnvironment(factory.Logger);
            settings.Should().NotBeNull("Test setup error: TFS environment variables have not been set correctly");
            settings.BuildEnvironment.Should().Be(BuildEnvironment.NotTeamBuild, "Test setup error: build environment was not set correctly");
            var preProcessor = new PreProcessor(factory, factory.Logger);

                var preProcessor = new PreProcessor(mockFactory, logger);

                // Act
                var success = await preProcessor.Execute(CreateArgs("organization"));
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            factory.TargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            factory.Server.AssertMethodCalled("GetProperties", 1);
            factory.Server.AssertMethodCalled("GetAllLanguages", 1);
            factory.Server.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            factory.Server.AssertMethodCalled("GetRules", 2); // C# and VBNet

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, factory.Logger);
        }

        [DataTestMethod]
        [DataRow("6.7.0.22152", true)]
        [DataRow("8.8.0.1121", false)]
        public async Task PreProc_EndToEnd_ShouldWarnOrNot_SonarQubeDeprecatedVersion(string sqVersion, bool shouldWarn)
        {
            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var factory = new MockObjectFactory();
            factory.Server.Data.SonarQubeVersion = new Version(sqVersion);
            using var teamBuildScope = PreprocessTestUtils.CreateValidNonTeamBuildScope();
            using var directoryScope = new WorkingDirectoryScope(workingDir);
            var settings = TeamBuildSettings.GetSettingsFromEnvironment(factory.Logger);
            settings.Should().NotBeNull("Test setup error: TFS environment variables have not been set correctly");
            settings.BuildEnvironment.Should().Be(BuildEnvironment.NotTeamBuild, "Test setup error: build environment was not set correctly");
            var preProcessor = new PreProcessor(factory, factory.Logger);

                var preProcessor = new PreProcessor(mockFactory, logger);

                // Act
                var success = await preProcessor.Execute(CreateArgs());
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            if (shouldWarn)
            {
                factory.Server.AssertWarningWritten("version is below supported");
            }
            else
            {
                factory.Server.AssertNoWarningWritten();
            }
        }

        [TestMethod]
        public async Task PreProc_NoPlugin()
        {
            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var factory = new MockObjectFactory();
            factory.Server.Data.Languages.Clear();
            factory.Server.Data.Languages.Add("invalid_plugin");
            using var teamBuildScope = PreprocessTestUtils.CreateValidNonTeamBuildScope();
            using var directoryScope = new WorkingDirectoryScope(workingDir);
            var settings = TeamBuildSettings.GetSettingsFromEnvironment(factory.Logger);
            settings.Should().NotBeNull("Test setup error: TFS environment variables have not been set correctly");
            settings.BuildEnvironment.Should().Be(BuildEnvironment.NotTeamBuild, "Test setup error: build environment was not set correctly");
            var preProcessor = new PreProcessor(factory, factory.Logger);

                var preProcessor = new PreProcessor(mockFactory, logger);

                // Act
                var success = await preProcessor.Execute(CreateArgs());
                success.Should().BeTrue("Expecting the pre-processing to complete successfully");
            factory.TargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            factory.Server.AssertMethodCalled("GetProperties", 1);
            factory.Server.AssertMethodCalled("GetAllLanguages", 1);
            factory.Server.AssertMethodCalled("TryGetQualityProfile", 0);   // No valid plugin
            factory.Server.AssertMethodCalled("GetRules", 0);               // No valid plugin

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 0, factory.Logger);

            // only contains SonarQubeAnalysisConfig (no rulesets or additional files)
            AssertDirectoryContains(settings.SonarConfigDirectory, Path.GetFileName(settings.AnalysisConfigFilePath));
        }

        [TestMethod]
        public async Task PreProc_NoProject()
        {
            // Arrange
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var factory = new MockObjectFactory(false);
            factory.Server.Data
                .AddQualityProfile("qp1", "cs", null)
                .AddProject("invalid")
                .AddRule(new SonarRule("fxcop", "cs.rule1"))
                .AddRule(new SonarRule("fxcop", "cs.rule2"));
            factory.Server.Data
                .AddQualityProfile("qp2", "vbnet", null)
                .AddProject("invalid")
                .AddRule(new SonarRule("fxcop-vbnet", "vb.rule1"))
                .AddRule(new SonarRule("fxcop-vbnet", "vb.rule2"));

            var mockAnalyzerProvider = MockAnalyzerProvider();
            var mockTargetsInstaller = new Mock<ITargetsInstaller>();
            var mockFactory = new MockObjectFactory(mockServer, mockTargetsInstaller.Object, mockAnalyzerProvider);

            var preProcessor = new PreProcessor(factory, factory.Logger);

            var success = await preProcessor.Execute(CreateArgs());
            success.Should().BeTrue("Expecting the pre-processing to complete successfully");

            AssertDirectoriesCreated(settings);

            factory.TargetsInstaller.Verify(x => x.InstallLoaderTargets(workingDir), Times.Once());
            factory.Server.AssertMethodCalled("GetProperties", 1);
            factory.Server.AssertMethodCalled("GetAllLanguages", 1);
            factory.Server.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            factory.Server.AssertMethodCalled("GetRules", 0); // no quality profile assigned to project

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 0, factory.Logger);

            // only contains SonarQubeAnalysisConfig (no rulesets or additional files)
            AssertDirectoryContains(settings.SonarConfigDirectory, Path.GetFileName(settings.AnalysisConfigFilePath));
        }

        [TestMethod]
        public async Task PreProc_HandleAnalysisException()
        {
            // Checks end-to-end behavior when AnalysisException is thrown inside FetchArgumentsAndRulesets
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var logger = new TestLogger();
            var mockServer = new ThrowingSonarQubeServer();
            var mockFactory = new MockObjectFactory(mockServer, new Mock<ITargetsInstaller>().Object, null);
            using (new WorkingDirectoryScope(workingDir))
            {
                var preProcessor = new PreProcessor(mockFactory, logger);
                var success = await preProcessor.Execute(CreateArgs("InvalidOrganization"));    // Should not throw
                success.Should().BeFalse("Expecting the pre-processing to fail");
                mockServer.AnalysisExceptionThrown.Should().BeTrue();
            }
        }

        [TestMethod]
        // Regression test for https://github.com/SonarSource/sonar-scanner-msbuild/issues/699
        public async Task PreProc_EndToEnd_Success_LocalSettingsAreUsedInSonarLintXML()
        {
            // Checks that local settings are used when creating the SonarLint.xml file, overriding
            var workingDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            // Local settings that should override matching server settings
            var args = new List<string>(CreateArgs())
            {
                "/d:local.key=local value 1",
                "/d:shared.key1=local shared value 1 - should override server value",
                "/d:shared.casing=local lower case value"
            };

            var mockAnalyzerProvider = MockAnalyzerProvider();
            var mockTargetsInstaller = new Mock<ITargetsInstaller>();
            var mockFactory = new MockObjectFactory(mockServer, mockTargetsInstaller.Object, mockAnalyzerProvider);

            TeamBuildSettings settings;
            using (PreprocessTestUtils.CreateValidNonTeamBuildScope())
            using (new WorkingDirectoryScope(workingDir))
            {
                settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                settings.Should().NotBeNull("Test setup error: TFS environment variables have not been set correctly");
                settings.BuildEnvironment.Should().Be(BuildEnvironment.NotTeamBuild, "Test setup error: build environment was not set correctly");

                var preProcessor = new PreProcessor(mockFactory, logger);

            var success = await preProcessor.Execute(args);
            success.Should().BeTrue("Expecting the pre-processing to complete successfully");


            // Check the settings used when creating the config file - settings should be separate
            var actualConfig = AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, factory.Logger);
            AssertExpectedLocalSetting(actualConfig, "local.key", "local value 1");
            AssertExpectedLocalSetting(actualConfig, "shared.key1", "local shared value 1 - should override server value");
            AssertExpectedLocalSetting(actualConfig, "shared.casing", "local lower case value");

            AssertExpectedServerSetting(actualConfig, "server.key", "server value 1");
            AssertExpectedServerSetting(actualConfig, "shared.key1", "server shared value 1");
            AssertExpectedServerSetting(actualConfig, "shared.CASING", "server upper case value");
        }

        private static IEnumerable<string> CreateArgs(string organization = null, Dictionary<string, string> properties = null)
        {
            yield return "/k:key";
            yield return "/n:name";
            yield return "/v:1.0";
            if (organization != null)
            {
                yield return $"/o:{organization}";
            }
            yield return "/d:cmd.line1=cmdline.value.1";
            yield return "/d:sonar.host.url=http://host";
            yield return "/d:sonar.log.level=INFO|DEBUG";

            if (properties != null)
            {
                foreach (var pair in properties)
                {
                    yield return $"/d:{pair.Key}={pair.Value}";
                }
            }
        }

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

            AssertExpectedLocalSetting(actualConfig, SonarProperties.HostUrl, "http://host");
            AssertExpectedLocalSetting(actualConfig, "cmd.line1", "cmdline.value.1");
            AssertExpectedServerSetting(actualConfig, "server.key", "server value 1");

            return actualConfig;
        }

        private void AssertConfigFileExists(string filePath)
        {
            File.Exists(filePath).Should().BeTrue("Expecting the analysis config file to exist. Path: {0}", filePath);
            TestContext.AddResultFile(filePath);
        }

        private static void AssertDirectoryContains(string dirPath, params string[] fileNames)
        {
            Directory.Exists(dirPath);
            var actualFileNames = Directory.GetFiles(dirPath).Select(Path.GetFileName);
            actualFileNames.Should().BeEquivalentTo(fileNames);
        }

        private static void AssertExpectedLocalSetting(AnalysisConfig actualConfig, string key, string expectedValue)
        {
            var found = Property.TryGetProperty(key, actualConfig.LocalSettings, out var actualProperty);

            found.Should().BeTrue("Failed to find the expected local setting: {0}", key);
            actualProperty.Value.Should().Be(expectedValue, "Unexpected property value. Key: {0}", key);
        }

        private static void AssertExpectedServerSetting(AnalysisConfig actualConfig, string key, string expectedValue)
        {
            var found = Property.TryGetProperty(key, actualConfig.ServerSettings, out var actualProperty);

            found.Should().BeTrue("Failed to find the expected server setting: {0}", key);
            actualProperty.Value.Should().Be(expectedValue, "Unexpected property value. Key: {0}", key);
        }

        private static void AssertDirectoryExists(string path) =>
            Directory.Exists(path).Should().BeTrue("Expected directory does not exist: {0}", path);

        private static MockRoslynAnalyzerProvider MockAnalyzerProvider() =>
            new() { SettingsToReturn = new AnalyzerSettings { RulesetPath = "c:\\xxx.ruleset" } };

        private static MockSonarQubeServer MockSonarQubeServer(bool withDefaultRules = true, string organization = null)
        {
            var mockServer = new MockSonarQubeServer();
            var data = mockServer.Data;
            data.ServerProperties.Add("server.key", "server value 1");
            data.Languages.Add("cs");
            data.Languages.Add("vbnet");
            data.Languages.Add("another_plugin");

            if (withDefaultRules)
            {
                data.AddQualityProfile("qp1", "cs", organization).AddProject("key").AddRule(new SonarRule("csharpsquid", "cs.rule.id"));
                data.AddQualityProfile("qp2", "vbnet", organization).AddProject("key").AddRule(new SonarRule("vbnet", "vb.rule.id"));
            }
            return mockServer;
        }

        private class ThrowingSonarQubeServer : ISonarQubeServer
        {
            public bool AnalysisExceptionThrown { get; private set; }

            public Task<IEnumerable<string>> GetAllLanguages() =>
                Task.FromResult(new[] { PreProcessor.CSharpLanguage, PreProcessor.VBNetLanguage }.AsEnumerable());

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
