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

namespace SonarScanner.MSBuild.Tasks.IntegrationTest.TargetsTests;

[TestClass]
public class ImportBeforeTargetsTests
{
    /// <summary>
    /// Name of the property to check for to determine whether
    /// the targets have been imported or not
    /// </summary>
    private const string DummyAnalysisTargetsMarkerProperty = "DummyProperty";

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void TestInitialize() =>
        TestUtils.EnsureImportBeforeTargetsExists(TestContext);

    #region Tests

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Checks the properties are not set if SonarQubeTargetsPath is missing")]
    public void ImportsBefore_SonarQubeTargetsPathNotSet()
    {
        // 1. Prebuild
        var projectXml = """
            <PropertyGroup>
              <SonarQubeTargetsPath />
              <AGENT_BUILDDIRECTORY />
              <TF_BUILD_BUILDDIRECTORY />
            </PropertyGroup>
            """;
        var projectFilePath = CreateProjectFile(projectXml);
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertPropertyValue(TargetProperties.SonarQubeTargetFilePath, null);
        AssertAnalysisTargetsAreNotImported(result);
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild);
        result.AssertTargetNotExecuted(TargetConstants.ImportBeforeInfo);
        result.AssertErrorCount(0);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Checks the properties are not set if the project is building inside Visual Studio")]
    public void ImportsBefore_BuildingInsideVS_NotImported()
    {
        var dummySonarTargetsDir = EnsureDummyIntegrationTargetsFileExists();
        var projectXml = $"""
            <PropertyGroup>
              <SonarQubeTempPath>{Path.GetTempPath()}</SonarQubeTempPath>
              <SonarQubeTargetsPath>{Path.GetDirectoryName(dummySonarTargetsDir)}</SonarQubeTargetsPath>
              <BuildingInsideVisualStudio>tRuE</BuildingInsideVisualStudio>
            </PropertyGroup>
            """;
        var projectFilePath = CreateProjectFile(projectXml);
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertPropertyValue(TargetProperties.SonarQubeTargetFilePath, dummySonarTargetsDir);
        AssertAnalysisTargetsAreNotImported(result);
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild);
        result.AssertTargetNotExecuted(TargetConstants.ImportBeforeInfo);
        result.AssertErrorCount(0);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Checks what happens if the analysis targets cannot be located")]
    public void ImportsBefore_MissingAnalysisTargets()
    {
        var projectXml = """
            <PropertyGroup>
              <SonarQubeTempPath>nonExistentPath</SonarQubeTempPath>
              <MSBuildExtensionsPath>nonExistentPath</MSBuildExtensionsPath>
              <AGENT_BUILDDIRECTORY />
              <TF_BUILD_BUILDDIRECTORY />
            </PropertyGroup>
            """;
        var projectFilePath = CreateProjectFile(projectXml);
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        AssertAnalysisTargetsAreNotImported(result); // Targets should not be imported
        result.AssertPropertyValue(TargetProperties.SonarQubeTargetsPath, @"nonExistentPath\bin\targets");
        result.AssertPropertyValue(TargetProperties.SonarQubeTargetFilePath, @"nonExistentPath\bin\targets\SonarQube.Integration.targets");
        result.BuildSucceeded.Should().BeTrue();
        result.AssertTargetExecuted(TargetConstants.ImportBeforeInfo);
        result.AssertErrorCount(0);

        var projectName = Path.GetFileName(projectFilePath);
        result.Messages.Should().Contain($"Sonar: ({projectName}) SonarQube analysis targets imported: ");
        result.Messages.Should().Contain($@"Sonar: ({projectName}) The analysis targets file not found: nonExistentPath\bin\targets\SonarQube.Integration.targets");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    [Description("Checks that the targets are imported if the properties are set correctly and the targets can be found")]
    public void ImportsBefore_TargetsExist()
    {
        var dummySonarTargetsDir = EnsureDummyIntegrationTargetsFileExists();
        var projectXml = $"""
            <PropertyGroup>
              <SonarQubeTempPath>{Path.GetTempPath()}</SonarQubeTempPath>
              <SonarQubeTargetsPath>{Path.GetDirectoryName(dummySonarTargetsDir)}</SonarQubeTargetsPath>
              <AGENT_BUILDDIRECTORY />
              <TF_BUILD_BUILDDIRECTORY />
            </PropertyGroup>
            """;
        var projectFilePath = CreateProjectFile(projectXml);
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath);

        result.AssertPropertyValue(TargetProperties.SonarQubeTargetFilePath, dummySonarTargetsDir);
        AssertAnalysisTargetsAreImported(result);
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild);
        result.AssertTargetExecuted(TargetConstants.ImportBeforeInfo);
        result.AssertErrorCount(0);
    }

    #endregion Tests

    #region Private methods

    /// <summary>
    /// Ensures that a dummy targets file with the name of the SonarQube analysis targets file exists.
    /// Return the full path to the targets file.
    /// </summary>
    private string EnsureDummyIntegrationTargetsFileExists()
    {
        // This can't just be in the TestContext.DeploymentDirectory as this will
        // be shared with other tests, and some of those tests might be deploying
        // the real analysis targets to that location.
        var testSpecificDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var fullPath = Path.Combine(testSpecificDir, TargetConstants.AnalysisTargetFile);
        if (!File.Exists(fullPath))
        {
// To check whether the targets are imported or not we check for
// the existence of the DummyProperty, below.
            var contents = """
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <DummyProperty>123</DummyProperty>
                  </PropertyGroup>
                  <Target Name='DummyTarget' />
                </Project>
                """;
            File.WriteAllText(fullPath, contents);
        }
        return fullPath;
    }

    private string CreateProjectFile(string testSpecificProjectXml)
    {
        var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var importsBeforeTargets = Path.Combine(projectDirectory, TargetConstants.ImportsBeforeFile);
        // Locate the real "ImportsBefore" target file
        File.Exists(importsBeforeTargets).Should().BeTrue("Test error: the SonarQube imports before target file does not exist. Path: {0}", importsBeforeTargets);

        var projectData = Resources.ImportBeforeTargetTestsTemplate.Replace("SQ_IMPORTS_BEFORE", importsBeforeTargets).Replace("TEST_SPECIFIC_XML", testSpecificProjectXml);
        return new TargetsTestsUtils(TestContext).CreateProjectFile(projectDirectory, projectData);
    }

    #endregion Private methods

    #region Assertions

    private static void AssertAnalysisTargetsAreNotImported(BuildLog result)
    {
        var propertyInstance = result.GetPropertyValue(DummyAnalysisTargetsMarkerProperty, true);
        propertyInstance.Should().BeNull("SonarQube Analysis targets should not have been imported");
    }

    private static void AssertAnalysisTargetsAreImported(BuildLog result)
    {
        var propertyInstance = result.GetPropertyValue(DummyAnalysisTargetsMarkerProperty, true);
        propertyInstance.Should().NotBeNull("Failed to import the SonarQube Analysis targets");
    }

    #endregion Assertions
}
