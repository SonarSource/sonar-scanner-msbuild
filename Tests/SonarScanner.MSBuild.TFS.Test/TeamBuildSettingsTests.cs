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

using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.TFS;
using TestUtilities;

namespace SonarScanner.MSBuild.TFS.Tests
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
            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, null);
                result = TeamBuildSettings.IsInTeamBuild;
                result.Should().BeFalse();
            }

            // 2. Env var set to a non-boolean -> false
            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "wibble");
                result = TeamBuildSettings.IsInTeamBuild;
                result.Should().BeFalse();
            }

            // 3. Env var set to false -> false
            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "false");
                result = TeamBuildSettings.IsInTeamBuild;
                result.Should().BeFalse();
            }

            // 4. Env var set to true -> true
            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "TRUE");
                result = TeamBuildSettings.IsInTeamBuild;
                result.Should().BeTrue();
            }
        }

        [TestMethod]
        public void TBSettings_SkipLegacyCodeCoverage()
        {
            // 0. Setup
            bool result;

            // 1. Env var not set
            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.SkipLegacyCodeCoverage, null);
                result = TeamBuildSettings.SkipLegacyCodeCoverageProcessing;
                result.Should().BeFalse();
            }

            // 2. Env var set to a non-boolean -> false
            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.SkipLegacyCodeCoverage, "wibble");
                result = TeamBuildSettings.SkipLegacyCodeCoverageProcessing;
                result.Should().BeFalse();
            }

            // 3. Env var set to false -> false
            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.SkipLegacyCodeCoverage, "false");
                result = TeamBuildSettings.SkipLegacyCodeCoverageProcessing;
                result.Should().BeFalse();
            }

            // 4. Env var set to true -> true
            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.SkipLegacyCodeCoverage, "TRUE");
                result = TeamBuildSettings.SkipLegacyCodeCoverageProcessing;
                result.Should().BeTrue();
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
            using (var scope = new EnvironmentVariableScope())
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
            using (var scope = new EnvironmentVariableScope())
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
            var logger = new TestLogger();
            TeamBuildSettings settings;

            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "TRUE");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_Legacy, "http://legacybuilduri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_Legacy, "http://legacycollectionUri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildDirectory_Legacy, "legacy build dir");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.SourcesDirectory_Legacy, @"c:\build\1234");

                // Act
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            }

            // Assert
            settings.Should().NotBeNull("Failed to create the TeamBuildSettings");
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
            var logger = new TestLogger();
            TeamBuildSettings settings;

            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "TRUE");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_TFS2015, "http://builduri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_TFS2015, "http://collectionUri");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildDirectory_TFS2015, "non-legacy team build");
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.SourcesDirectory_TFS2015, @"c:\agent\_work\1");

                // Act
                settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            }

            // Assert
            settings.Should().NotBeNull("Failed to create the TeamBuildSettings");
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
            actual.Should().NotBeNull("Returned settings should never be null");

            actual.BuildEnvironment.Should().Be(expectedEnvironment, "Unexpected build environment returned");
            actual.AnalysisBaseDirectory.Should().Be(expectedAnalysisDir, "Unexpected analysis base directory returned");
            actual.BuildDirectory.Should().Be(expectedBuildDir, "Unexpected build directory returned");
            actual.BuildUri.Should().Be(expectedBuildUri, "Unexpected build uri returned");
            actual.TfsUri.Should().Be(expectedCollectionUri, "Unexpected tfs uri returned");

            if (actual.BuildEnvironment == BuildEnvironment.NotTeamBuild)
            {
                actual.SourcesDirectory.Should().BeNull("Should not be able to set the sources directory");
            }
            else
            {
                actual.SourcesDirectory.Should().Be(expectedSourcesDir, "Unexpected sources directory returned");
            }

            // Check the calculated values
            actual.SonarConfigDirectory.Should().Be(Path.Combine(expectedAnalysisDir, "conf"), "Unexpected config dir");
            actual.SonarOutputDirectory.Should().Be(Path.Combine(expectedAnalysisDir, "out"), "Unexpected output dir");
            actual.SonarBinDirectory.Should().Be(Path.Combine(expectedAnalysisDir, "bin"), "Unexpected bin dir");
            actual.AnalysisConfigFilePath.Should().Be(Path.Combine(expectedAnalysisDir, "conf", FileConstants.ConfigFileName), "Unexpected analysis file path");

            actual.SonarScannerWorkingDirectory.Should().Be(Directory.GetParent(expectedAnalysisDir).FullName, "Unexpected sonar-scanner working dir");
        }

        private static void CheckExpectedTimeoutReturned(string envValue, int expected)
        {
            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(TeamBuildSettings.EnvironmentVariables.LegacyCodeCoverageTimeoutInMs, envValue);
                var result = TeamBuildSettings.LegacyCodeCoverageProcessingTimeout;
                result.Should().Be(expected, "Unexpected timeout value returned. Environment value: {0}", envValue);
            }
        }

        #endregion Checks
    }
}
