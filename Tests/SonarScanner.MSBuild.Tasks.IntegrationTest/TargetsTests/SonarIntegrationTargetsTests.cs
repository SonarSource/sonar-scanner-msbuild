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

using SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests;
using TestUtilities;

namespace SonarScanner.MSBuild.Tasks.IntegrationTest.TargetsTests;

[TestClass]
public class SonarIntegrationTargetsTests
{
    public TestContext TestContext { get; set; }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Checks the properties are not set if the temp folder is not set")]
    public void IntTargets_TempFolderIsNotSet()
    {
        var result = CreateProjectAndLoad(null);
        result.AssertPropertyValue(TargetProperties.SonarQubeOutputPath, null);
        result.AssertPropertyValue(TargetProperties.SonarQubeConfigPath, null);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Checks the SonarQube paths are not set when the TeamBuild build directories are missing")]
    public void IntTargets_SonarPaths_TeamBuildBuildDirNotSet()
    {
        var projectXml = $@"
<PropertyGroup>
  <TF_BUILD_BUILDDIRECTORY />
  <AGENT_BUILDDIRECTORY />
</PropertyGroup>";
        var result = CreateProjectAndLoad(projectXml);
        result.AssertPropertyValue(TargetProperties.SonarQubeOutputPath, null);
        result.AssertPropertyValue(TargetProperties.SonarQubeConfigPath, null);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Checks the SonarQube paths are set correctly when the legacy TeamBuild directory is provided")]
    public void IntTargets_SonarPaths_TeamBuildPropertySet_Legacy()
    {
        var projectXml = $@"
<PropertyGroup>
  <SonarQubeTempPath>t:\TeamBuildDir_Legacy\.sonarqube</SonarQubeTempPath>
  <TF_BUILD_BUILDDIRECTORY>t:\TeamBuildDir_Legacy</TF_BUILD_BUILDDIRECTORY>
  <AGENT_BUILDDIRECTORY />
</PropertyGroup>";
        var result = CreateProjectAndLoad(projectXml);
        result.AssertPropertyValue(TargetProperties.SonarQubeOutputPath, @"t:\TeamBuildDir_Legacy\.sonarqube\out");
        result.AssertPropertyValue(TargetProperties.SonarQubeConfigPath, @"t:\TeamBuildDir_Legacy\.sonarqube\conf");
        result.AssertPropertyValue(TargetProperties.SonarTelemetryFilePath, @"t:\TeamBuildDir_Legacy\.sonarqube\out");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Checks the SonarQube paths are set correctly when the new TeamBuild build directory is provided")]
    public void IntTargets_SonarPaths_TeamBuildPropertySet_NonLegacy()
    {
        var projectXml = $@"
<PropertyGroup>
  <SonarQubeTempPath>t:\TeamBuildDir_NonLegacy\.sonarqube</SonarQubeTempPath>
  <TF_BUILD_BUILDDIRECTORY></TF_BUILD_BUILDDIRECTORY>
  <AGENT_BUILDDIRECTORY>t:\TeamBuildDir_NonLegacy</AGENT_BUILDDIRECTORY>
</PropertyGroup>";
        var result = CreateProjectAndLoad(projectXml);
        result.AssertPropertyValue(TargetProperties.SonarQubeOutputPath, @"t:\TeamBuildDir_NonLegacy\.sonarqube\out");
        result.AssertPropertyValue(TargetProperties.SonarQubeConfigPath, @"t:\TeamBuildDir_NonLegacy\.sonarqube\conf");
        result.AssertPropertyValue(TargetProperties.SonarTelemetryFilePath, @"t:\TeamBuildDir_NonLegacy\.sonarqube\out");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Checks the SonarQube paths are set correctly when the SonarQubeTempPath property is provided")]
    public void IntTargets_SonarPaths_TempPathSet()
    {
        var projectXml = $@"
<PropertyGroup>
  <SonarQubeTempPath>c:\sonarQTemp</SonarQubeTempPath>

  <!-- SonarQubeTempPath setting should take precedence -->
  <TF_BUILD_BUILDDIRECTORY>t:\Legacy TeamBuildPath\</TF_BUILD_BUILDDIRECTORY>
  <AGENT_BUILDDIRECTORY>x:\New Team Build Path\</AGENT_BUILDDIRECTORY>
</PropertyGroup>";
        var result = CreateProjectAndLoad(projectXml);
        result.AssertPropertyValue(TargetProperties.SonarQubeOutputPath, @"c:\sonarQTemp\out");
        result.AssertPropertyValue(TargetProperties.SonarQubeConfigPath, @"c:\sonarQTemp\conf");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Tests that the explicit property values for the output and config paths are used if supplied")]
    public void IntTargets_SonarPaths_OutputAndConfigPathsAreSet()
    {
        // The SonarQubeTempPath and TeamBuild paths should be ignored if the output and config are set explicitly
        var projectXml = $@"
<PropertyGroup>
  <SonarQubeOutputPath>c:\output</SonarQubeOutputPath>
  <SonarQubeConfigPath>c:\config</SonarQubeConfigPath>
  <SonarQubeTempPath>c:\sonarQTemp</SonarQubeTempPath>

  <!-- SonarQubeTempPath setting should take precedence -->
  <TF_BUILD_BUILDDIRECTORY>t:\Legacy TeamBuildPath\</TF_BUILD_BUILDDIRECTORY>
  <AGENT_BUILDDIRECTORY>x:\New TeamBuildPath\</AGENT_BUILDDIRECTORY>
</PropertyGroup>";
        var result = CreateProjectAndLoad(projectXml);
        result.AssertPropertyValue(TargetProperties.SonarQubeOutputPath, @"c:\output");
        result.AssertPropertyValue(TargetProperties.SonarQubeConfigPath, @"c:\config");
    }

    private BuildLog CreateProjectAndLoad(string projectSnippet)
    {
        projectSnippet += @"<Target Name=""DoNothing"" />";
        var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var targetTestUtils = new TargetsTestsUtils(TestContext);
        var projectTemplate = targetTestUtils.GetProjectTemplate(null, projectDirectory, null, projectSnippet);
        var projectFile = targetTestUtils.CreateProjectFile(projectDirectory, projectTemplate);
        return BuildRunner.BuildTargets(TestContext, projectFile, "DoNothing");
    }
}
