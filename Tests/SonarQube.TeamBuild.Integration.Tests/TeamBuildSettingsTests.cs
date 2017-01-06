/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

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
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, null);
                result = TeamBuildSettings.IsInTeamBuild;
                Assert.IsFalse(result);
            }

            // 2. Env var set to a non-boolean -> false
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "wibble");
                result = TeamBuildSettings.IsInTeamBuild;
                Assert.IsFalse(result);
            }

            // 3. Env var set to false -> false
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "false");
                result = TeamBuildSettings.IsInTeamBuild;
                Assert.IsFalse(result);
            }

            // 4. Env var set to true -> true
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "TRUE");
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
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, null);

                logger = new TestLogger();
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);

                // Check the environment properties
                CheckExpectedSettings(
                    settings,
                    BuildEnvironment.NotTeamBuild,
                    Directory.GetCurrentDirectory(),
                    null,
                    null,
                    null,
                    null);
            }

            // 2. Some Team build settings provided, but not marked as in team build
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, null);
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_Legacy, "build uri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_Legacy, "collection uri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildDirectory_Legacy, "should be ignored");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildDirectory_TFS2015, "should be ignored");

                logger = new TestLogger();
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);

                CheckExpectedSettings(
                    settings,
                    BuildEnvironment.NotTeamBuild,
                    Directory.GetCurrentDirectory(),
                    null,
                    null,
                    null,
                    null);
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
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "TRUE");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_Legacy, "http://legacybuilduri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_Legacy, "http://legacycollectionUri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildDirectory_Legacy, "legacy build dir");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.SourcesDirectory_Legacy, @"c:\build\1234"); ;

                // Act
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            }

            // Assert
            Assert.IsNotNull(settings, "Failed to create the TeamBuildSettings");
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            // Check the environment properties
            CheckExpectedSettings(
                settings,
                BuildEnvironment.LegacyTeamBuild,
                Directory.GetCurrentDirectory(),
                "http://legacybuilduri",
                "http://legacycollectionUri",
                "legacy build dir",
                @"c:\build\1234");
        }

        [TestMethod]
        public void TBSettings_NonLegacyTeamBuild()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            TeamBuildSettings settings;

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "TRUE");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_TFS2015, "http://builduri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_TFS2015, "http://collectionUri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildDirectory_TFS2015, "non-legacy team build");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.SourcesDirectory_TFS2015, @"c:\agent\_work\1"); ;

                // Act
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            }

            // Assert
            Assert.IsNotNull(settings, "Failed to create the TeamBuildSettings");
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            // Check the environment properties
            CheckExpectedSettings(
                settings,
                BuildEnvironment.TeamBuild,
                Directory.GetCurrentDirectory(),
                "http://builduri",
                "http://collectionUri",
                "non-legacy team build",
                @"c:\agent\_work\1");
        }

        #endregion Test methods

        #region Checks

        private static void CheckExpectedSettings(
            TeamBuildSettings actual,
            BuildEnvironment expectedEnvironment,
            string expectedAnalysisDir,
            string expectedBuildUri,
            string expectedCollectionUri,
            string expectedBuildDir,
            string expectedSourcesDir)
        {
            Assert.IsNotNull(actual, "Returned settings should never be null");

            Assert.AreEqual(expectedEnvironment, actual.BuildEnvironment, "Unexpected build environment returned");
            Assert.AreEqual(expectedAnalysisDir, actual.AnalysisBaseDirectory, "Unexpected analysis base directory returned");
            Assert.AreEqual(expectedBuildDir, actual.BuildDirectory, "Unexpected build directory returned");
            Assert.AreEqual(expectedBuildUri, actual.BuildUri, "Unexpected build uri returned");
            Assert.AreEqual(expectedCollectionUri, actual.TfsUri, "Unexpected tfs uri returned");

            if (actual.BuildEnvironment == BuildEnvironment.NotTeamBuild)
            {
                Assert.IsNull(actual.SourcesDirectory, "Should not be able to set the sources directory");
            }
            else
            {
                Assert.AreEqual(expectedSourcesDir, actual.SourcesDirectory, "Unexpected sources directory returned");
            }

            // Check the calculated values
            Assert.AreEqual(Path.Combine(expectedAnalysisDir, "conf"), actual.SonarConfigDirectory, "Unexpected config dir");
            Assert.AreEqual(Path.Combine(expectedAnalysisDir, "out"), actual.SonarOutputDirectory, "Unexpected output dir");
            Assert.AreEqual(Path.Combine(expectedAnalysisDir, "bin"), actual.SonarBinDirectory, "Unexpected bin dir");
            Assert.AreEqual(Path.Combine(expectedAnalysisDir, "conf", FileConstants.ConfigFileName), actual.AnalysisConfigFilePath, "Unexpected analysis file path");

            Assert.AreEqual(Directory.GetParent(expectedAnalysisDir).FullName, actual.SonarScannerWorkingDirectory, "Unexpected sonar-scanner working dir");
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

        #endregion Checks
    }
}