//-----------------------------------------------------------------------
// <copyright file="TeamBuildSettingsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
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
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.IsInTeamBuild, null);
                result = TeamBuildSettings.IsInTeamBuild;
                Assert.IsFalse(result);
            }

            // 2. Env var set to a non-boolean -> false
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.IsInTeamBuild, "wibble");
                result = TeamBuildSettings.IsInTeamBuild;
                Assert.IsFalse(result);
            }

            // 3. Env var set to false -> false
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.IsInTeamBuild, "false");
                result = TeamBuildSettings.IsInTeamBuild;
                Assert.IsFalse(result);
            }

            // 4. Env var set to true -> true
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.IsInTeamBuild, "TRUE");
                result = TeamBuildSettings.IsInTeamBuild;
                Assert.IsTrue(result);
            }
        }

        [TestMethod]
        public void TBSettings_NotTeamBuild()
        {
            // 0. Setup
            TestLogger logger;
            TeamBuildSettings settings;

            // 1. No environment vars set -> use the temp path
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.SQAnalysisRootPath, null);
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.IsInTeamBuild, null);

                logger = new TestLogger();
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);

                // Check the environment properties
                CheckExpectedSettings(settings, BuildEnvironment.NotTeamBuild, Path.GetTempPath(), null, null);
            }


            // 2. SQ analysis dir set
            using(EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.SQAnalysisRootPath, "d:\\sqdir");
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.IsInTeamBuild, null);

                logger = new TestLogger();
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);

                CheckExpectedSettings(settings, BuildEnvironment.NotTeamBuild, "d:\\sqdir", null, null);
            }


            // 3. Some Team build settings provided, but not marked as in team build
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.SQAnalysisRootPath, "x:\\a");
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.IsInTeamBuild, null);

                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildUri_Legacy, "build uri");
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.TfsCollectionUri_Legacy, "collection uri");

                logger = new TestLogger();
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);

                CheckExpectedSettings(settings, BuildEnvironment.NotTeamBuild, "x:\\a", null, null);
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
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.IsInTeamBuild, "TRUE");
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildDirectory_Legacy, "build dir");
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildUri_Legacy, "http://legacybuilduri");
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.TfsCollectionUri_Legacy, "http://legacycollectionUri");

                // Act
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            }

            // Assert
            Assert.IsNotNull(settings, "Failed to create the TeamBuildSettings");
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            // Check the environment properties
            CheckExpectedSettings(settings, BuildEnvironment.LegacyTeamBuild, "build dir", "http://legacybuilduri", "http://legacycollectionUri");
        }

        [TestMethod]
        public void TBSettings_NonLegacyTeamBuild()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings;

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.IsInTeamBuild, "TRUE");
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildDirectory_TFS2015, "build dir");
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildUri_TFS2015, "http://builduri");
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.TfsCollectionUri_TFS2015, "http://collectionUri");

                // Act
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            }

            // Assert
            Assert.IsNotNull(settings, "Failed to create the TeamBuildSettings");
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            // Check the environment properties
            CheckExpectedSettings(settings, BuildEnvironment.TeamBuild, "build dir", "http://builduri", "http://collectionUri");
        }

        #endregion

        #region Checks

        private static void CheckExpectedSettings(TeamBuildSettings actual, BuildEnvironment expectedEnvironment, string expectedDir, string expectedBuildUri, string expectedCollectionUri)
        {
            Assert.IsNotNull(actual, "Returned settings should never be null");

            Assert.AreEqual(expectedEnvironment, actual.BuildEnvironment, "Unexpected build environment returned");
            Assert.AreEqual(expectedDir, actual.BuildDirectory, "Unexpected build directory returned");
            Assert.AreEqual(expectedBuildUri, actual.BuildUri, "Unexpected build uri returned");
            Assert.AreEqual(expectedCollectionUri, actual.TfsUri, "Unexpected tfs uri returned");

            // Check the calculated values
            Assert.AreEqual(Path.Combine(expectedDir,"SQTemp\\Config"), actual.SonarConfigDir, "Unexpected config dir");
            Assert.AreEqual(Path.Combine(expectedDir, "SQTemp\\Output"), actual.SonarOutputDir, "Unexpected output dir");
            Assert.AreEqual(Path.Combine(expectedDir, "SQTemp\\Config", TeamBuildSettings.ConfigFileName), actual.AnalysisConfigFilePath, "Unexpected analysis file path");
        }

        #endregion

    }
}
