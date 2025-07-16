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

using SonarScanner.MSBuild.Tasks;
using SonarScanner.MSBuild.Tasks.IntegrationTest;

namespace SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests;

[TestClass]
public class SonarCategoriseProjectTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void SimpleProject_NoTestMarkers_IsNotATestProject()
    {
        var result = BuildAndRunTarget("foo.proj", string.Empty);
        AssertIsNotTestProject(result);
    }

    [TestMethod]
    public void ExplicitMarking_IsTrue()
    {
        var result = BuildAndRunTarget("foo.proj", """
            <PropertyGroup>
              <SonarQubeTestProject>true</SonarQubeTestProject>
            </PropertyGroup>
            """);
        AssertIsTestProject(result, $"Sonar: (foo.proj) SonarQubeTestProject has been set explicitly to true.");
        AssertProjectIsNotExcluded(result);
    }

    [TestMethod]
    public void ExplicitMarking_IsFalse()
    {
        // If the project is explicitly marked as not a test then the other conditions should be ignored
        const string projectXmlSnippet = """
            <PropertyGroup>
              <ProjectTypeGuids>D1C3357D-82B4-43D2-972C-4D5455F0A7DB;3AC096D0-A1C2-E12C-1390-A8335801FDAB;BF3D2153-F372-4432-8D43-09B24D530F20</ProjectTypeGuids>
              <SonarQubeTestProject>false</SonarQubeTestProject>
            </PropertyGroup>

            <ItemGroup>
              <Service Include='{D1C3357D-82B4-43D2-972C-4D5455F0A7DB}' />
              <ProjectCapability Include='TestContainer' />
            </ItemGroup>
            """;
        var configFilePath = CreateAnalysisConfigWithRegEx("*");
        var result = BuildAndRunTarget("foo.proj", projectXmlSnippet, configFilePath);

        AssertIsNotTestProject(result);
        AssertProjectIsNotExcluded(result);
        result.Messages.Should().Contain($"Sonar: (foo.proj) SonarQubeTestProject has been set explicitly to false.");
    }
    [DataRow("MyTests.csproj", null)]
    [DataRow("foo.proj", null)]
    [DataRow("TestafoXB.proj", ".*foo.*")]
    [DataTestMethod]
    public void WildcardMatch_NoMatch(string projectName, string regex)
    {
        var configFilePath = CreateAnalysisConfigWithRegEx(regex);

        var result = BuildAndRunTarget(projectName, string.Empty, configFilePath);

        AssertIsNotTestProject(result, projectName);
        AssertProjectIsNotExcluded(result);
    }

    [TestMethod]
    public void WildcardMatch_UserSpecified_Match()
    {
        // Check user-specified wildcard matching
        var configFilePath = CreateAnalysisConfigWithRegEx(".*foo.*");

        var result = BuildAndRunTarget("foo.proj", string.Empty, configFilePath);

        AssertIsTestProject(result, "Sonar: (foo.proj) project is evaluated as a test project based on the project name.");
        AssertProjectIsNotExcluded(result);
    }

    [TestMethod]
    public void ProjectTypeGuids_IsRecognized()
    {
        // Snippet with the Test Project Type Guid between two other Guids
        const string projectXmlSnippet = """
            <PropertyGroup>
              <ProjectTypeGuids>D1C3357D-82B4-43D2-972C-4D5455F0A7DB;3AC096D0-A1C2-E12C-1390-A8335801FDAB;BF3D2153-F372-4432-8D43-09B24D530F20</ProjectTypeGuids>
            </PropertyGroup>
            """;
        var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

        AssertIsTestProject(result, "Sonar: (foo.proj) project has the MSTest project type guid -> test project.");
    }

    [TestMethod]
    public void ProjectTypeGuids_IsRecognized_CaseInsensitive()
    {
        const string projectXmlSnippet = """
            <PropertyGroup>
              <ProjectTypeGuids>3AC096D0-A1C2-E12C-1390-A8335801fdab</ProjectTypeGuids>
            </PropertyGroup>
            """;
        var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

        AssertIsTestProject(result, "Sonar: (foo.proj) project has the MSTest project type guid -> test project.");
    }

    [TestMethod]
    public void ServiceGuid_IsRecognized()
    {
        const string projectXmlSnippet = """
            <ItemGroup>
              <Service Include='{D1C3357D-82B4-43D2-972C-4D5455F0A7DB}' />
              <Service Include='{82A7F48D-3B50-4B1E-B82E-3ADA8210C358}' />
              <Service Include='{BF3D2153-F372-4432-8D43-09B24D530F20}' />
            </ItemGroup>
            """;
        var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

        AssertIsTestProject(result, "Sonar: (foo.proj) project has the legacy Test Explorer Service tag {82A7F48D-3B50-4B1E-B82E-3ADA8210C358} -> test project.");
    }

    [TestMethod]
    public void ServiceGuid_IsRecognized_CaseInsensitive()
    {
        const string projectXmlSnippet = """
            <ItemGroup>
              <Service Include='{82a7f48d-3b50-4b1e-b82e-3ada8210c358}' />
            </ItemGroup>
            """;
        var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

        AssertIsTestProject(result, "Sonar: (foo.proj) project has the legacy Test Explorer Service tag {82A7F48D-3B50-4B1E-B82E-3ADA8210C358} -> test project.");
    }

    [TestMethod]
    public void ProjectCapability_IsRecognized()
    {
        const string projectXmlSnippet = """
            <ItemGroup>
              <ProjectCapability Include='Foo' />
              <ProjectCapability Include='TestContainer' />
              <ProjectCapability Include='Something else' />
            </ItemGroup>
            """;
        var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

        AssertIsTestProject(result, "Sonar: (foo.proj) project has the ProjectCapability 'TestContainer' -> test project.");
    }

    [TestMethod]
    public void ProjectCapability_IsRecognized_CaseInsensitive()
    {
        const string projectXmlSnippet = """
            <ItemGroup>
              <ProjectCapability Include='testcontainer' />
            </ItemGroup>
            """;
        var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

        AssertIsTestProject(result, "Sonar: (foo.proj) project has the ProjectCapability 'TestContainer' -> test project.");
    }

    [TestMethod]
    public void References_IsProduct()
    {
        const string projectXmlSnippet = """
            <ItemGroup>
              <SonarResolvedReferences Include='mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />
              <SonarResolvedReferences Include='SimpleName' />
            </ItemGroup>
            """;
        var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

        AssertIsNotTestProject(result);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void References_IsTest()
    {
        const string projectXmlSnippet = """
            <ItemGroup>
              <SonarResolvedReferences Include='mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />
              <SonarResolvedReferences Include='SimpleName' />
              <SonarResolvedReferences Include='Moq, Version=4.16.0.0, Culture=neutral, PublicKeyToken=69f491c39445e920' />
              <SonarResolvedReferences Include='Microsoft.VisualStudio.TestPlatform.TestFramework' />
            </ItemGroup>
            """;
        var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

        AssertIsTestProject(result, "Sonar: (foo.proj) project is evaluated as a test project based on the 'Moq' reference.");
        // Only first match is reported in the log
        result.Messages.Should().NotContain("Sonar: (foo.proj) project is evaluated as a test project based on the 'Microsoft.VisualStudio.TestPlatform.TestFramework' reference.");
    }

    [TestMethod]
    public void SqlServerProjectsAreNotExcluded()
    {
        const string projectXmlSnippet = """
            <PropertyGroup>
              <SqlTargetName>non-empty</SqlTargetName>
            </PropertyGroup>
            """;
        var result = BuildAndRunTarget("foo.sqproj", projectXmlSnippet);

        AssertIsNotTestProject(result, "foo.sqproj");
        AssertProjectIsNotExcluded(result);
    }

    [TestMethod] // SONARMSBRU-26: MS Fakes should be excluded from analysis
    public void FakesProjects_AreExcluded_WhenNoExplicitSonarProperties()
    {
        const string projectXmlSnippet = """
            <PropertyGroup>
              <AssemblyName>f.fAKes</AssemblyName>
            </PropertyGroup>
            """;
        var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

        AssertIsTestProject(result, "Sonar: (foo.proj) project is a temporary project generated by Microsoft Fakes and will be ignored.");
        AssertProjectIsExcluded(result);
    }

    [TestMethod]
    public void FakesProjects_FakesInName_AreNotExcluded()
    {
        // Checks that projects with ".fakes" in the name are not excluded
        const string projectXmlSnippet = """
            <PropertyGroup>
              <AssemblyName>f.fAKes.proj</AssemblyName>
            </PropertyGroup>
            """;
        var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

        AssertIsNotTestProject(result);
        AssertProjectIsNotExcluded(result);
    }

    [TestMethod]
    public void FakesProjects_AreNotTestProjects_WhenExplicitSonarTestProperty() // @odalet - Issue #844
    {
        // Checks that fakes projects are not marked as test if the project
        // says otherwise.
        const string projectXmlSnippet = """
            <PropertyGroup>
              <SonarQubeTestProject>false</SonarQubeTestProject>
              <AssemblyName>MyFakeProject.fakes</AssemblyName>
            </PropertyGroup>
            """;
        var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

        AssertIsNotTestProject(result);
    }

    [TestMethod]
    public void FakesProjects_AreNotExcluded_WhenExplicitSonarExcludeProperty() // @odalet - Issue #844
    {
        // Checks that fakes projects are not excluded if the project
        // says otherwise.
        const string projectXmlSnippet = """
            <PropertyGroup>
              <SonarQubeExclude>false</SonarQubeExclude>
              <AssemblyName>MyFakeProject.fakes</AssemblyName>
            </PropertyGroup>
            """;
        var result = BuildAndRunTarget("f.proj", projectXmlSnippet);

        AssertProjectIsNotExcluded(result);
    }

    [DataRow("f.tmp_proj", true)]
    [DataRow("f.TMP_PROJ", true)]
    [DataRow("f_wpftmp.csproj", true)]
    [DataRow("f_WpFtMp.csproj", true)]
    [DataRow("f_wpftmp.vbproj", true)]
    [DataRow("WpfApplication.csproj", false)]
    [DataRow("ftmp_proj.csproj", false)]
    [DataRow("wpftmp.csproj", false)]
    [DataTestMethod]
    public void WpfTemporaryProjects_AreExcluded(string projectName, bool expectedExclusionState)
    {
        var result = BuildAndRunTarget(projectName, string.Empty);
        AssertIsNotTestProject(result, projectName);
        if (expectedExclusionState)
        {
            AssertProjectIsExcluded(result);
            result.Messages.Should().Contain($"Sonar: ({projectName}) project is a temporary project and will be excluded.");
        }
        else
        {
            AssertProjectIsNotExcluded(result);
        }
    }

    private BuildLog BuildAndRunTarget(string projectFileName, string projectXmlSnippet, string analysisConfigDir = "c:\\dummy")
    {
        var projectFilePath = CreateProjectFile(projectFileName, projectXmlSnippet, analysisConfigDir);
        var result = BuildRunner.BuildTargets(TestContext, projectFilePath, TargetConstants.SonarCategoriseProject);
        result.AssertTargetSucceeded(TargetConstants.SonarCategoriseProject);
        return result;
    }

    private string CreateProjectFile(string projectFileName, string xmlSnippet, string analysisConfigDir)
    {
        var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
        var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
        var projectFilePath = Path.Combine(rootInputFolder, projectFileName);

        // Boilerplate XML for minimal project file that will execute the "categorise project" task
        var projectXml = Resources.CategoriseProjectTestTemplate;

        BuildUtilities.CreateFileFromTemplate(
            projectFilePath,
            TestContext,
            projectXml,
            xmlSnippet,
            Guid.NewGuid(),
            typeof(WriteProjectInfoFile).Assembly.Location,
            sqTargetFile,
            analysisConfigDir);

        return projectFilePath;
    }

    /// <summary>
    /// Creates an analysis config file, replacing one if it already exists.
    /// If the supplied "regExExpression" is not null then the appropriate setting
    /// entry will be created in the file
    /// </summary>
    /// <returns>The directory containing the config file</returns>
    private string CreateAnalysisConfigWithRegEx(string regExExpression)
    {
        var config = new AnalysisConfig();
        if (regExExpression is not null)
        {
            config.LocalSettings = [new(IsTestFileByName.TestRegExSettingId, regExExpression)];
        }
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var fullPath = Path.Combine(testDir, "SonarQubeAnalysisConfig.xml");
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        config.Save(fullPath);
        TestContext.AddResultFile(fullPath);
        return testDir;
    }

    private static void AssertIsTestProject(BuildLog log, string expectedReason)
    {
        log.GetPropertyAsBoolean(TargetProperties.SonarQubeTestProject).Should().BeTrue();
        log.Messages.Should()
            .Contain("Sonar: (foo.proj) Categorizing project as test or product code...")
            .And.Contain("Sonar: (foo.proj) categorized as TEST project (test code).")
            .And.Contain(expectedReason);
    }

    private static void AssertIsNotTestProject(BuildLog log, string projectName = "foo.proj")
    {
        log.GetPropertyAsBoolean(TargetProperties.SonarQubeTestProject).Should().BeFalse();
        log.Messages.Should()
            .Contain($"Sonar: ({projectName}) Categorizing project as test or product code...")
            .And.Contain($"Sonar: ({projectName}) categorized as MAIN project (production code).");
    }

    private static void AssertProjectIsExcluded(BuildLog log) =>
        log.GetPropertyAsBoolean(TargetProperties.SonarQubeExcludeMetadata).Should().BeTrue();

    private static void AssertProjectIsNotExcluded(BuildLog log) =>
        log.GetPropertyAsBoolean(TargetProperties.SonarQubeExcludeMetadata).Should().BeFalse();
}
