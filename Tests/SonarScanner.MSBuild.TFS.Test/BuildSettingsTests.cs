/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

namespace SonarScanner.MSBuild.TFS.Test;

[TestClass]
public class BuildSettingsTests
{
    [TestMethod]
    public void SettingsFromEnvironment_NoTFSVariable_NotTeamBuild()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(EnvironmentVariables.IsInTeamFoundationBuild, null);

        var settings = BuildSettings.SettingsFromEnvironment(new TestRuntime().Logger);
        CheckExpectedSettings(
            settings,
            false,
            Directory.GetCurrentDirectory(),
            null,
            null,
            null,
            null);
    }

    [TestMethod]
    public void SettingsFromEnvironment_IncompleteTFSVariableSet_NotTeamBuild()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(EnvironmentVariables.IsInTeamFoundationBuild, null);
        scope.SetVariable(EnvironmentVariables.BuildUriLegacy, "build uri");
        scope.SetVariable(EnvironmentVariables.TfsCollectionUriLegacy, "collection uri");
        scope.SetVariable(EnvironmentVariables.BuildDirectoryLegacy, "should be ignored");
        scope.SetVariable(EnvironmentVariables.BuildDirectoryTfs2015, "should be ignored");

        var settings = BuildSettings.SettingsFromEnvironment(new TestRuntime().Logger);
        CheckExpectedSettings(
            settings,
            false,
            Directory.GetCurrentDirectory(),
            null,
            null,
            null,
            null);
    }

    [TestMethod]
    public void SettingsFromEnvironment_InvalidTFSVariable_NotTeamBuild()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(EnvironmentVariables.IsInTeamFoundationBuild, "wibble");

        BuildSettings.SettingsFromEnvironment(new TestRuntime().Logger).IsAzureDevOps.Should().BeFalse();
    }

    [TestMethod]
    public void SettingsFromEnvironment_TFSVariableFalse_NotTeamBuild()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(EnvironmentVariables.IsInTeamFoundationBuild, "false");

        BuildSettings.SettingsFromEnvironment(new TestRuntime().Logger).IsAzureDevOps.Should().BeFalse();
    }

    [TestMethod]
    public void SettingsFromEnvironment_TFSVariableTrue_TeamBuild()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(EnvironmentVariables.IsInTeamFoundationBuild, "TRUE");
        scope.SetVariable(EnvironmentVariables.BuildUriTfs2015, "http://builduri");
        scope.SetVariable(EnvironmentVariables.TfsCollectionUriTfs2015, "http://collectionUri");
        scope.SetVariable(EnvironmentVariables.BuildDirectoryTfs2015, "non-legacy team build");
        scope.SetVariable(EnvironmentVariables.SourcesDirectoryTfs2015, @"c:\agent\_work\1");

        var settings = BuildSettings.SettingsFromEnvironment(new TestRuntime().Logger);
        settings.Should().NotBeNull("Failed to create the BuildSettings");
        CheckExpectedSettings(
            settings,
            true,
            Directory.GetCurrentDirectory(),
            "http://builduri",
            "http://collectionUri",
            "non-legacy team build",
            @"c:\agent\_work\1");
    }

    [TestMethod]
    public void SettingsFromEnvironment_TFSVariableTrue_BuildUriLegacySet_TeamBuildLegacy()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(EnvironmentVariables.IsInTeamFoundationBuild, "TRUE");
        scope.SetVariable(EnvironmentVariables.BuildUriLegacy, "http://builduri");
        scope.SetVariable(EnvironmentVariables.TfsCollectionUriLegacy, "http://collectionUri");
        scope.SetVariable(EnvironmentVariables.BuildDirectoryLegacy, "non-legacy team build");
        scope.SetVariable(EnvironmentVariables.SourcesDirectoryLegacy, @"c:\agent\_work\1");

        var logger = new TestRuntime().Logger;
        var settings = BuildSettings.SettingsFromEnvironment(logger);
        settings.Should().BeNull();
        logger.Should().HaveErrorOnce("Team Foundation Server detected, which is not supported.");
    }

    private static void CheckExpectedSettings(
        BuildSettings actual,
        bool expectedIsAzureDevOps,
        string expectedAnalysisDir,
        string expectedBuildUri,
        string expectedCollectionUri,
        string expectedBuildDir,
        string expectedSourcesDir)
    {
        actual.Should().NotBeNull("Returned settings should never be null");

        actual.IsAzureDevOps.Should().Be(expectedIsAzureDevOps, "Unexpected build environment returned");
        actual.AnalysisBaseDirectory.Should().Be(expectedAnalysisDir, "Unexpected analysis base directory returned");
        actual.BuildDirectory.Should().Be(expectedBuildDir, "Unexpected build directory returned");
        actual.BuildUri.Should().Be(expectedBuildUri, "Unexpected build uri returned");
        actual.TfsUri.Should().Be(expectedCollectionUri, "Unexpected tfs uri returned");

        if (actual.IsAzureDevOps)
        {
            actual.SourcesDirectory.Should().Be(expectedSourcesDir, "Unexpected sources directory returned");
        }
        else
        {
            actual.SourcesDirectory.Should().BeNull("Should not be able to set the sources directory");
        }

        // Check the calculated values
        actual.SonarConfigDirectory.Should().Be(Path.Combine(expectedAnalysisDir, "conf"), "Unexpected config dir");
        actual.SonarOutputDirectory.Should().Be(Path.Combine(expectedAnalysisDir, "out"), "Unexpected output dir");
        actual.SonarBinDirectory.Should().Be(Path.Combine(expectedAnalysisDir, "bin"), "Unexpected bin dir");
        actual.AnalysisConfigFilePath.Should().Be(Path.Combine(expectedAnalysisDir, "conf", FileConstants.ConfigFileName), "Unexpected analysis file path");

        actual.SonarScannerWorkingDirectory.Should().Be(Directory.GetParent(expectedAnalysisDir)!.FullName, "Unexpected sonar-scanner working dir");
    }
}
