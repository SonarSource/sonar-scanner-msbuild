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
    public void CreateFromEnvironment_NoBuildUri_IsNotAzureDevOps()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(EnvironmentVariables.BuildUriTfs2015, null);

        var settings = BuildSettings.CreateFromEnvironment(new TestRuntime().Logger);
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
    public void CreateFromEnvironment_NoBuildUri_OtherTFSRelatedVariablesSet_IsNotAzureDevOps()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(EnvironmentVariables.BuildUriTfs2015, null);
        scope.SetVariable(EnvironmentVariables.BuildDirectoryTfs2015, "should be ignored");

        var settings = BuildSettings.CreateFromEnvironment(new TestRuntime().Logger);
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
    public void CreateFromEnvironment_BuildUriSet_IsAzureDevOps()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(EnvironmentVariables.BuildUriTfs2015, "http://builduri");

        var settings = BuildSettings.CreateFromEnvironment(new TestRuntime().Logger);
        settings.Should().NotBeNull();
        CheckExpectedSettings(
            settings,
            true,
            Directory.GetCurrentDirectory(),
            "http://builduri",
            null,
            null,
            null);
    }

    [TestMethod]
    public void CreateFromEnvironment_BuildUriLegacySet_NotSupported()
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(EnvironmentVariables.BuildUriLegacy, "http://builduri");

        var logger = new TestLogger();
        BuildSettings.CreateFromEnvironment(logger).Should().BeNull();
        logger.Should().HaveErrorOnce("Team Foundation Server detected, which is not supported by this version of SonarScanner for .NET. Use older version of the scanner.");
    }

    private static void CheckExpectedSettings(BuildSettings actual,
                                              bool expectedIsAzureDevOps,
                                              string expectedAnalysisDir,
                                              string expectedBuildUri,
                                              string expectedCollectionUri,
                                              string expectedBuildDir,
                                              string expectedSourcesDir)
    {
        actual.Should().NotBeNull();

        actual.IsAzureDevOps.Should().Be(expectedIsAzureDevOps);
        actual.AnalysisBaseDirectory.Should().Be(expectedAnalysisDir);
        actual.BuildDirectory.Should().Be(expectedBuildDir);
        actual.BuildUri.Should().Be(expectedBuildUri);
        actual.TfsUri.Should().Be(expectedCollectionUri);

        if (actual.IsAzureDevOps)
        {
            actual.SourcesDirectory.Should().Be(expectedSourcesDir);
        }
        else
        {
            actual.SourcesDirectory.Should().BeNull("Should not be able to set the sources directory");
        }

        // Check the calculated values
        actual.SonarConfigDirectory.Should().Be(Path.Combine(expectedAnalysisDir, "conf"));
        actual.SonarOutputDirectory.Should().Be(Path.Combine(expectedAnalysisDir, "out"));
        actual.SonarBinDirectory.Should().Be(Path.Combine(expectedAnalysisDir, "bin"));
        actual.AnalysisConfigFilePath.Should().Be(Path.Combine(expectedAnalysisDir, "conf", FileConstants.ConfigFileName));

        actual.SonarScannerWorkingDirectory.Should().Be(Directory.GetParent(expectedAnalysisDir)!.FullName);
    }
}
