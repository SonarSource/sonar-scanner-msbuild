//-----------------------------------------------------------------------
// <copyright file="TeamBuildSettingsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.IO;
using TestUtilities;

namespace SonarQube.TeamBuild.Integration.Tests
{
    [TestClass]
    public class TeamBuildSettingsTests
    {
        #region Test methods

        [TestMethod]
        public void TBSettings_IsInTeamBuild()
        {
            // 0. Setup
            bool result;

            // 1. Env var not set
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamBuild, null);
                result = TeamBuildSettings.IsInTeamBuild;
                Assert.IsFalse(result);
            }

            // 2. Env var set to a non-boolean -> false
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamBuild, "wibble");
                result = TeamBuildSettings.IsInTeamBuild;
                Assert.IsFalse(result);
            }

            // 3. Env var set to false -> false
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamBuild, "false");
                result = TeamBuildSettings.IsInTeamBuild;
                Assert.IsFalse(result);
            }

            // 4. Env var set to true -> true
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamBuild, "TRUE");
                result = TeamBuildSettings.IsInTeamBuild;
                Assert.IsTrue(result);
            }
        }

        [TestMethod]
        public void TBSettings_SkipLegacyCodeCoverage()
        {
            // 0. Setup
            bool result;

            // 1. Env var not set
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.SkipLegacyCodeCoverage, null);
                result = TeamBuildSettings.SkipLegacyCodeCoverageProcessing;
                Assert.IsFalse(result);
            }

            // 2. Env var set to a non-boolean -> false
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.SkipLegacyCodeCoverage, "wibble");
                result = TeamBuildSettings.SkipLegacyCodeCoverageProcessing;
                Assert.IsFalse(result);
            }

            // 3. Env var set to false -> false
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.SkipLegacyCodeCoverage, "false");
                result = TeamBuildSettings.SkipLegacyCodeCoverageProcessing;
                Assert.IsFalse(result);
            }

            // 4. Env var set to true -> true
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.SkipLegacyCodeCoverage, "TRUE");
                result = TeamBuildSettings.SkipLegacyCodeCoverageProcessing;
                Assert.IsTrue(result);
            }
        }

        [TestMethod]
        public void TBSettings_LegacyCodeCoverageTimeout()
        {
            // 0. Setup - none

            // 1. Env var not set
            CheckExpectedTimeoutReturned(null, TeamBuildSettings.DefaultLegacyCodeCoverageTimeout);

            // 2. Env var set to a non-integer -> default
            CheckExpectedTimeoutReturned("blah blah", TeamBuildSettings.DefaultLegacyCodeCoverageTimeout);

            // 3. Env var set to a non-integer number -> default
            CheckExpectedTimeoutReturned("-123.456", TeamBuildSettings.DefaultLegacyCodeCoverageTimeout);

            // 4. Env var set to a positive integer -> returnd
            CheckExpectedTimeoutReturned("987654321", 987654321);

            // 5. Env var set to a negative integer -> returnd
            CheckExpectedTimeoutReturned("-123", -123);
        }

        [TestMethod]
        public void TBSettings_NotTeamBuild()
        {
            // 0. Setup
            TestLogger logger;
            TeamBuildSettings settings;

            // 1. No environment vars set
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamBuild, null);

                logger = new TestLogger();
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);

                // Check the environment properties
                CheckExpectedSettings(settings, BuildEnvironment.NotTeamBuild, Directory.GetCurrentDirectory(), null, null, null, "hmm");
            }

            // 2. Some Team build settings provided, but not marked as in team build
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamBuild, null);
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_Legacy, "build uri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_Legacy, "collection uri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildDirectory_Legacy, "should be ignored");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildDirectory_TFS2015, "should be ignored");

                logger = new TestLogger();
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);

                CheckExpectedSettings(settings, BuildEnvironment.NotTeamBuild, Directory.GetCurrentDirectory(), null, null, null, "hmm");
            }

        }

        [TestMethod]
        public void TBSettings_LegacyTeamBuild()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings;

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamBuild, "TRUE");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_Legacy, "http://legacybuilduri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_Legacy, "http://legacycollectionUri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildDirectory_Legacy, "legacy build dir");

                // Act
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            }

            // Assert
            Assert.IsNotNull(settings, "Failed to create the TeamBuildSettings");
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            // Check the environment properties
            CheckExpectedSettings(settings, BuildEnvironment.LegacyTeamBuild, Directory.GetCurrentDirectory(), "http://legacybuilduri", "http://legacycollectionUri", "legacy build dir", "hmm");
        }

        [TestMethod]
        public void TBSettings_NonLegacyTeamBuild()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings;

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamBuild, "TRUE");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_TFS2015, "http://builduri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_TFS2015, "http://collectionUri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildDirectory_TFS2015, "non-legacy team build");

                // Act
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            }

            // Assert
            Assert.IsNotNull(settings, "Failed to create the TeamBuildSettings");
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            // Check the environment properties
            CheckExpectedSettings(settings, BuildEnvironment.TeamBuild, Directory.GetCurrentDirectory(), "http://builduri", "http://collectionUri", "non-legacy team build", "hmm");
        }

        #endregion

        #region Checks

        private static void CheckExpectedSettings(TeamBuildSettings actual, BuildEnvironment expectedEnvironment, string expectedAnalysisDir, string expectedBuildUri, string expectedCollectionUri, string expectedBuildDir, string expectedSonarRunnerWorkingDir)
        {
            Assert.IsNotNull(actual, "Returned settings should never be null");

            Assert.AreEqual(expectedEnvironment, actual.BuildEnvironment, "Unexpected build environment returned");
            Assert.AreEqual(expectedAnalysisDir, actual.AnalysisBaseDirectory, "Unexpected analysis base directory returned");
            Assert.AreEqual(expectedBuildDir, actual.BuildDirectory, "Unexpected build directory returned");
            Assert.AreEqual(expectedBuildUri, actual.BuildUri, "Unexpected build uri returned");
            Assert.AreEqual(expectedCollectionUri, actual.TfsUri, "Unexpected tfs uri returned");

            // Check the calculated values
            Assert.AreEqual(Path.Combine(expectedAnalysisDir, "conf"), actual.SonarConfigDirectory, "Unexpected config dir");
            Assert.AreEqual(Path.Combine(expectedAnalysisDir, "out"), actual.SonarOutputDirectory, "Unexpected output dir");
            Assert.AreEqual(Path.Combine(expectedAnalysisDir, "bin"), actual.SonarBinDirectory, "Unexpected bin dir");
            Assert.AreEqual(Path.Combine(expectedAnalysisDir, "conf", FileConstants.ConfigFileName), actual.AnalysisConfigFilePath, "Unexpected analysis file path");

            Assert.AreEqual(Directory.GetParent(expectedAnalysisDir).FullName, actual.SonarRunnerWorkingDirectory);
        }

        private static void CheckExpectedTimeoutReturned(string envValue, int expected)
        {
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.LegacyCodeCoverageTimeoutInMs, envValue);
                int result = TeamBuildSettings.LegacyCodeCoverageProcessingTimeout;
                Assert.AreEqual(expected, result, "Unexpected timeout value returned. Environment value: {0}", envValue);
            }
        }

        #endregion

    }
}
