//-----------------------------------------------------------------------
// <copyright file="TeamBuildSettings.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarQube.TeamBuild.Integration.Tests
{
    [TestClass]
    public class TeamBuildSettingsTests
    {
        #region Test methods

        [TestMethod]
        public void TBSettings_MissingEnvVars()
        {
            // 0. Setup
            TestLogger logger;
            TeamBuildSettings settings;

            // 1. All settings missing
            logger = new TestLogger();
            settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            Assert.IsNull(settings);
            logger.AssertErrorsLogged(3); // one for each missing variable

            // 2. One setting provided
            using(EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildDirectory, "build dir");

                logger = new TestLogger();
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
                Assert.IsNull(settings);
                logger.AssertErrorsLogged(2);
            }

            // 3. Two settings provided
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildUri, "build uri");
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.TfsCollectionUri, "collection uri");

                logger = new TestLogger();
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
                Assert.IsNull(settings);
                logger.AssertErrorsLogged(1);
            }

        }

        [TestMethod]
        public void TBSettings_AllEnvVarsAvailable()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings;

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildDirectory, "build dir");
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildUri, "build uri");
                scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.TfsCollectionUri, "collection uri");

                // Act
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            }

            // Assert
            Assert.IsNotNull(settings, "Failed to create the TeamBuildSettings");
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            // Check the environment properties
            Assert.AreEqual("build dir", settings.BuildDirectory, "Unexpected build directory returned");
            Assert.AreEqual("build uri", settings.BuildUri, "Unexpected build uri returned");
            Assert.AreEqual("collection uri", settings.TfsUri, "Unexpected tfs uri returned");

            // Check the calculated values
            Assert.AreEqual("build dir\\SQTemp\\Config", settings.SonarConfigDir, "Unexpected config dir");
            Assert.AreEqual("build dir\\SQTemp\\Output", settings.SonarOutputDir, "Unexpected outpu dir");
            Assert.AreEqual("build dir\\SQTemp\\Config\\" + TeamBuildSettings.ConfigFileName, settings.AnalysisConfigFilePath, "Unexpected analysis file path");
        }

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

        #endregion

    }
}
