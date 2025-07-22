/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

using System.Runtime.InteropServices;

namespace SonarScanner.MSBuild.Tasks.IntegrationTest.TargetsTests;

[TestClass]
public class SonarIntegrationTargetsTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [Description("Checks the properties are not set if the temp folder is not set")]
    public void IntTargets_TempFolderIsNotSet()
    {
        var result = CreateProjectAndLoad(null);
        result.AssertPropertyValue(TargetProperties.SonarQubeOutputPath, null);
        result.AssertPropertyValue(TargetProperties.SonarQubeConfigPath, null);
    }

    [TestMethod]
    [Description("Checks the SonarQube paths are not set when the TeamBuild build directories are missing")]
    public void IntTargets_SonarPaths_TeamBuildBuildDirNotSet()
    {
        var projectXml = """
            <PropertyGroup>
              <TF_BUILD_BUILDDIRECTORY />
              <AGENT_BUILDDIRECTORY />
            </PropertyGroup>
            """;
        var result = CreateProjectAndLoad(projectXml);
        result.AssertPropertyValue(TargetProperties.SonarQubeOutputPath, null);
        result.AssertPropertyValue(TargetProperties.SonarQubeConfigPath, null);
    }

    [TestMethod]
    [Description("Checks the SonarQube paths are set correctly when the legacy TeamBuild directory is provided")]
    public void IntTargets_SonarPaths_TeamBuildPropertySet_Legacy()
    {
        var legacyTeamBuildDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"t:\TeamBuildDir_Legacy\" : @"t:/TeamBuildDir_Legacy/";
        var projectXml = $"""
            <PropertyGroup>
              <SonarQubeTempPath>{legacyTeamBuildDir}.sonarqube</SonarQubeTempPath>
              <TF_BUILD_BUILDDIRECTORY>t:{Path.DirectorySeparatorChar}TeamBuildDir_Legacy</TF_BUILD_BUILDDIRECTORY>
              <AGENT_BUILDDIRECTORY />
            </PropertyGroup>
            """;
        var result = CreateProjectAndLoad(projectXml);
        result.AssertPropertyValue(TargetProperties.SonarQubeOutputPath, $@"{legacyTeamBuildDir}.sonarqube\out");
        result.AssertPropertyValue(TargetProperties.SonarQubeConfigPath, $@"{legacyTeamBuildDir}.sonarqube\conf");
        result.AssertPropertyValue(TargetProperties.SonarTelemetryFilePath, $@"{legacyTeamBuildDir}.sonarqube\out\Telemetry.Targets.S4NET.json");
    }

    [TestMethod]
    [Description("Checks the SonarQube paths are set correctly when the new TeamBuild build directory is provided")]
    public void IntTargets_SonarPaths_TeamBuildPropertySet_NonLegacy()
    {
        var teamBuildDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"t:\TeamBuildDir_NonLegacy\" : @"t:/TeamBuildDir_NonLegacy/";
        var projectXml = $"""
            <PropertyGroup>
              <SonarQubeTempPath>{teamBuildDir}.sonarqube</SonarQubeTempPath>
              <TF_BUILD_BUILDDIRECTORY></TF_BUILD_BUILDDIRECTORY>
              <AGENT_BUILDDIRECTORY>t:{Path.DirectorySeparatorChar}TeamBuildDir_NonLegacy</AGENT_BUILDDIRECTORY>
            </PropertyGroup>
            """;
        var result = CreateProjectAndLoad(projectXml);
        result.AssertPropertyValue(TargetProperties.SonarQubeOutputPath, $@"{teamBuildDir}.sonarqube\out");
        result.AssertPropertyValue(TargetProperties.SonarQubeConfigPath, $@"{teamBuildDir}.sonarqube\conf");
        result.AssertPropertyValue(TargetProperties.SonarTelemetryFilePath, $@"{teamBuildDir}.sonarqube\out\Telemetry.Targets.S4NET.json");
    }

    [TestMethod]
    [Description("Checks the SonarQube paths are set correctly when the SonarQubeTempPath property is provided")]
    public void IntTargets_SonarPaths_TempPathSet()
    {
        var projectXml = $"""
            <PropertyGroup>
              <SonarQubeTempPath>c:{Path.DirectorySeparatorChar}sonarQTemp</SonarQubeTempPath>

              <!-- SonarQubeTempPath setting should take precedence -->
              <TF_BUILD_BUILDDIRECTORY>t:{Path.DirectorySeparatorChar}Legacy TeamBuildPath{Path.DirectorySeparatorChar}</TF_BUILD_BUILDDIRECTORY>
              <AGENT_BUILDDIRECTORY>x:{Path.DirectorySeparatorChar}New Team Build Path{Path.DirectorySeparatorChar}</AGENT_BUILDDIRECTORY>
            </PropertyGroup>
            """;
        var result = CreateProjectAndLoad(projectXml);
        result.AssertPropertyValue(TargetProperties.SonarQubeOutputPath, $@"c:{Path.DirectorySeparatorChar}sonarQTemp\out");
        result.AssertPropertyValue(TargetProperties.SonarQubeConfigPath, $@"c:{Path.DirectorySeparatorChar}sonarQTemp\conf");
    }

    [TestMethod]
    [Description("Tests that the explicit property values for the output and config paths are used if supplied")]
    public void IntTargets_SonarPaths_OutputAndConfigPathsAreSet()
    {
        // The SonarQubeTempPath and TeamBuild paths should be ignored if the output and config are set explicitly
        var projectXml = $"""
            <PropertyGroup>
              <SonarQubeOutputPath>c:{Path.DirectorySeparatorChar}output</SonarQubeOutputPath>
              <SonarQubeConfigPath>c:{Path.DirectorySeparatorChar}config</SonarQubeConfigPath>
              <SonarQubeTempPath>c:{Path.DirectorySeparatorChar}sonarQTemp</SonarQubeTempPath>

              <!-- SonarQubeTempPath setting should take precedence -->
              <TF_BUILD_BUILDDIRECTORY>t:{Path.DirectorySeparatorChar}Legacy TeamBuildPath{Path.DirectorySeparatorChar}</TF_BUILD_BUILDDIRECTORY>
              <AGENT_BUILDDIRECTORY>x:{Path.DirectorySeparatorChar}New TeamBuildPath{Path.DirectorySeparatorChar}</AGENT_BUILDDIRECTORY>
            </PropertyGroup>
            """;
        var result = CreateProjectAndLoad(projectXml);
        result.AssertPropertyValue(TargetProperties.SonarQubeOutputPath, $@"c:{Path.DirectorySeparatorChar}output");
        result.AssertPropertyValue(TargetProperties.SonarQubeConfigPath, $@"c:{Path.DirectorySeparatorChar}config");
    }

    private BuildLog CreateProjectAndLoad(string projectSnippet)
    {
        var x = new TargetsTestsContext(TestContext);
        projectSnippet += @"<Target Name=""DoNothing""/>";
        var projectFile = x.CreateProjectFile(projectSnippet, sqProperties: string.Empty);
        return BuildRunner.BuildTargets(TestContext, projectFile, "DoNothing");
    }
}
