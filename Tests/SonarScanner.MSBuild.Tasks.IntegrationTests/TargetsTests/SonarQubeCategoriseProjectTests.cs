/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
 * mailto:info AT sonarsource DOT com
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

using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Tasks;
using SonarScanner.MSBuild.Tasks.IntegrationTests;
using TestUtilities;

namespace SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class SonarQubeCategoriseProjectTests
    {
        public TestContext TestContext { get; set; }

        #region Detection of test projects

        [TestMethod]
        [TestCategory("IsTest")]
        public void SimpleProject_NoTestMarkers_IsNotATestProject()
        {
            // Act
            var result = BuildAndRunTarget("foo.proj", "");

            // Assert
            AssertIsNotTestProject(result);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void ExplicitMarking_IsTrue()
        {
            const string projectXmlSnippet = @"
<PropertyGroup>
  <SonarQubeTestProject>true</SonarQubeTestProject>
</PropertyGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

            // Assert
            AssertIsTestProject(result);
            AssertProjectIsNotExcluded(result);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void ExplicitMarking_False()
        {
            // If the project is explicitly marked as not a test then the other conditions should be ignored
            const string projectXmlSnippet = @"
<PropertyGroup>
  <ProjectTypeGuids>D1C3357D-82B4-43D2-972C-4D5455F0A7DB;3AC096D0-A1C2-E12C-1390-A8335801FDAB;BF3D2153-F372-4432-8D43-09B24D530F20</ProjectTypeGuids>
  <SonarQubeTestProject>false</SonarQubeTestProject>
</PropertyGroup>

<ItemGroup>
  <Service Include='{D1C3357D-82B4-43D2-972C-4D5455F0A7DB}' />
  <ProjectCapability Include='TestContainer' />
</ItemGroup>

";
            var configFilePath = CreateAnalysisConfigWithRegEx("*");

            // Act
            var result = BuildAndRunTarget("Test.proj", projectXmlSnippet, configFilePath);

            // Assert
            AssertIsNotTestProject(result);
            AssertProjectIsNotExcluded(result);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WildcardMatch_Default_NoMatchForTestName()
        {
            // Check the default behavior - name is not checked
            var configFilePath = CreateAnalysisConfigWithRegEx(null);

            // Act
            var result = BuildAndRunTarget("MyTests.csproj", string.Empty, configFilePath);

            // Assert
            AssertIsNotTestProject(result);
            AssertProjectIsNotExcluded(result);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WildcardMatch_Default_NoMatchForOtherName()
        {
            // Check the default behavior - name is not checked
            var configFilePath = CreateAnalysisConfigWithRegEx(null);

            // Act
            var result = BuildAndRunTarget("foo.proj", string.Empty, configFilePath);

            // Assert
            AssertIsNotTestProject(result);
            AssertProjectIsNotExcluded(result);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WildcardMatch_UserSpecified_Match()
        {
            // Check user-specified wildcard matching

            // Arrange
            var configFilePath = CreateAnalysisConfigWithRegEx(".*foo.*");

            // Act
            var result = BuildAndRunTarget("foo.proj", string.Empty, configFilePath);

            // Assert
            AssertIsTestProject(result);
            AssertProjectIsNotExcluded(result);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WildcardMatch_UserSpecified_NoMatch()
        {
            // Check user-specified wildcard matching

            // Arrange
            var configFilePath = CreateAnalysisConfigWithRegEx(".*foo.*");

            // Act
            // Using a project name that will be recognized as a test by the default regex
            var result = BuildAndRunTarget("TestafoXB.proj", string.Empty, configFilePath);

            // Assert
            AssertIsNotTestProject(result);
            AssertProjectIsNotExcluded(result);
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void ProjectTypeGuids_IsRecognized()
        {
            // Snippet with the Test Project Type Guid between two other Guids
            const string projectXmlSnippet = @"
<PropertyGroup>
  <ProjectTypeGuids>D1C3357D-82B4-43D2-972C-4D5455F0A7DB;3AC096D0-A1C2-E12C-1390-A8335801FDAB;BF3D2153-F372-4432-8D43-09B24D530F20</ProjectTypeGuids>
</PropertyGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

            // Assert
            AssertIsTestProject(result);
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void ProjectTypeGuids_IsRecognized_CaseInsensitive()
        {
            const string projectXmlSnippet = @"
<PropertyGroup>
  <ProjectTypeGuids>3AC096D0-A1C2-E12C-1390-A8335801fdab</ProjectTypeGuids>
</PropertyGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

            // Assert
            AssertIsTestProject(result);
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void ServiceGuid_IsRecognized()
        {
            const string projectXmlSnippet = @"
<ItemGroup>
  <Service Include='{D1C3357D-82B4-43D2-972C-4D5455F0A7DB}' />
  <Service Include='{82A7F48D-3B50-4B1E-B82E-3ADA8210C358}' />
  <Service Include='{BF3D2153-F372-4432-8D43-09B24D530F20}' />
</ItemGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

            // Assert
            AssertIsTestProject(result);
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void ServiceGuid_IsRecognized_CaseInsensitive()
        {
            const string projectXmlSnippet = @"
<ItemGroup>
  <Service Include='{82a7f48d-3b50-4b1e-b82e-3ada8210c358}' />
</ItemGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

            // Assert
            AssertIsTestProject(result);
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void ProjectCapability_IsRecognized()
        {
            const string projectXmlSnippet = @"
<ItemGroup>
  <ProjectCapability Include='Foo' />
  <ProjectCapability Include='TestContainer' />
  <ProjectCapability Include='Something else' />
</ItemGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

            // Assert
            AssertIsTestProject(result);
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void ProjectCapability_IsRecognized_CaseInsensitive()
        {
            const string projectXmlSnippet = @"
<ItemGroup>
  <ProjectCapability Include='testcontainer' />
</ItemGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

            // Assert
            AssertIsTestProject(result);
        }

        #endregion Detection of test projects

        #region SQL Server projects tests

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void SqlServerProjectsAreNotExcluded()
        {
            const string projectXmlSnippet = @"
<PropertyGroup>
  <SqlTargetName>non-empty</SqlTargetName>
</PropertyGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.sqproj", projectXmlSnippet);

            // Assert
            AssertIsNotTestProject(result);
            AssertProjectIsNotExcluded(result);
        }

        #endregion SQL Server projects tests

        #region Fakes projects tests

        [TestMethod]
        [TestCategory("ProjectInfo")] // SONARMSBRU-26: MS Fakes should be excluded from analysis
        public void FakesProjects_AreExcluded_WhenNoExplicitSonarProperties()
        {
            const string projectXmlSnippet = @"
<PropertyGroup>
  <AssemblyName>f.fAKes</AssemblyName>
</PropertyGroup>
";
            // Act
            var result = BuildAndRunTarget("f.proj", projectXmlSnippet);

            // Assert
            AssertIsTestProject(result);
            AssertProjectIsExcluded(result);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void FakesProjects_FakesInName_AreNotExcluded()
        {
            // Checks that projects with ".fakes" in the name are not excluded

            const string projectXmlSnippet = @"
<PropertyGroup>
  <AssemblyName>f.fAKes.proj</AssemblyName>
</PropertyGroup>
";
            // Act
            var result = BuildAndRunTarget("f.proj", projectXmlSnippet);

            // Assert
            AssertIsNotTestProject(result);
            AssertProjectIsNotExcluded(result);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void FakesProjects_AreNotTestProjects_WhenExplicitSonarTestProperty() // @odalet - Issue #844
        {
            // Checks that fakes projects are not marked as test if the project
            // says otherwise.

            const string projectXmlSnippet = @"
<PropertyGroup>
  <SonarQubeTestProject>false</SonarQubeTestProject>
  <AssemblyName>MyFakeProject.fakes</AssemblyName>
</PropertyGroup>
";
            // Act
            var result = BuildAndRunTarget("f.proj", projectXmlSnippet);

            // Assert
            AssertIsNotTestProject(result);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void FakesProjects_AreNotExcluded_WhenExplicitSonarExcludeProperty() // @odalet - Issue #844
        {
            // Checks that fakes projects are not excluded if the project
            // says otherwise.

            const string projectXmlSnippet = @"
<PropertyGroup>
  <SonarQubeExclude>false</SonarQubeExclude>
  <AssemblyName>MyFakeProject.fakes</AssemblyName>
</PropertyGroup>
";
            // Act
            var result = BuildAndRunTarget("f.proj", projectXmlSnippet);

            // Assert
            AssertProjectIsNotExcluded(result);
        }

        #endregion Fakes projects tests

        #region Temp projects tests

        [TestMethod]
        public void WpfTemporaryProjects_AreExcluded()
        {
            ExecuteTest("f.tmp_proj", expectedExclusionState: true);
            ExecuteTest("f.TMP_PROJ", expectedExclusionState: true);
            ExecuteTest("f_wpftmp.csproj", expectedExclusionState: true);
            ExecuteTest("f_WpFtMp.csproj", expectedExclusionState: true);
            ExecuteTest("f_wpftmp.vbproj", expectedExclusionState: true);

            ExecuteTest("WpfApplication.csproj", expectedExclusionState: false);
            ExecuteTest("ftmp_proj.csproj", expectedExclusionState: false);
            ExecuteTest("wpftmp.csproj", expectedExclusionState: false);

            void ExecuteTest(string projectName, bool expectedExclusionState)
            {
                // Act
                var result = BuildAndRunTarget(projectName, string.Empty);

                // Assert
                AssertIsNotTestProject(result);

                if (expectedExclusionState)
                {
                    AssertProjectIsExcluded(result);
                }
                else
                {
                    AssertProjectIsNotExcluded(result);
                }
            }
        }

        #endregion Temp projects tests

        private BuildLog BuildAndRunTarget(string projectFileName, string projectXmlSnippet, string analysisConfigDir = "c:\\dummy")
        {
            var projectFilePath = CreateProjectFile(projectFileName, projectXmlSnippet, analysisConfigDir);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath,
                TargetConstants.CategoriseProjectTarget);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.CategoriseProjectTarget);
            return result;
        }

        private string CreateProjectFile(string projectFileName, string xmlSnippet, string analysisConfigDir)
        {
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            var projectFilePath = Path.Combine(rootInputFolder, projectFileName);

            // Boilerplate XML for minimal project file that will execute the "categorise project" task
            var projectXml = @"<Project Sdk='Microsoft.NET.Sdk'>

  <!-- Test-specific XML snippet -->
  {0}

  <!-- Boilerplate -->
  <PropertyGroup>
    <ProjectGuid>{1}</ProjectGuid>
    <SonarQubeTempPath>c:\dummy\path</SonarQubeTempPath>
    <SonarQubeOutputPath>c:\dummy\path</SonarQubeOutputPath>
    <SonarQubeConfigPath>{4}</SonarQubeConfigPath>
    <SonarQubeBuildTasksAssemblyFile>{2}</SonarQubeBuildTasksAssemblyFile>
  </PropertyGroup>

  <!-- We need to write out the properties we want to check later -->
  <Target Name='CaptureData' AfterTargets='SonarQubeCategoriseProject' >
    <Message Importance='high' Text='CAPTURE___PROPERTY___tmpSQServiceList___$(tmpSQServiceList)' />
    <Message Importance='high' Text='CAPTURE___PROPERTY___tmpSQProjectCapabilities___$(tmpSQProjectCapabilities)' />
    <Message Importance='high' Text='CAPTURE___PROPERTY___SonarQubeTestProject___$(SonarQubeTestProject)' />
    <Message Importance='high' Text='CAPTURE___PROPERTY___SonarQubeExclude___$(SonarQubeExclude)' />
  </Target>

  <Import Project='{3}' />
</Project>
";
            BuildUtilities.CreateFileFromTemplate(projectFilePath, TestContext, projectXml,
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
            if (regExExpression != null)
            {
                config.LocalSettings = new AnalysisProperties
                {
                    new Property { Id = IsTestFileByName.TestRegExSettingId, Value = regExExpression }
                };
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

        private static void AssertIsTestProject(BuildLog log)
        {
            log.GetPropertyAsBoolean(TargetProperties.SonarQubeTestProject).Should().BeTrue();
            log.MessageLog.Should().Contain("categorized as TEST project (test code). This MSBuild project will not be analyzed.\n");
        }

        private static void AssertIsNotTestProject(BuildLog log)
        {
            log.GetPropertyAsBoolean(TargetProperties.SonarQubeTestProject).Should().BeFalse();
            log.MessageLog.Should().Contain("categorized as MAIN project (production code).\n");
        }

        private static void AssertProjectIsExcluded(BuildLog log)
        {
            log.GetPropertyAsBoolean(TargetProperties.SonarQubeExcludeMetadata).Should().BeTrue();
        }

        private static void AssertProjectIsNotExcluded(BuildLog log)
        {
            log.GetPropertyAsBoolean(TargetProperties.SonarQubeExcludeMetadata).Should().BeFalse();
        }

    }
}
