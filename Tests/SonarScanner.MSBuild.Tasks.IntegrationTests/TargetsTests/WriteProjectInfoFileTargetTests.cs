/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using System.Text;
using FluentAssertions;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class WriteProjectInfoFileTargetTests
    {
        public TestContext TestContext { get; set; }

        #region Test project recognition tests

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_ExplicitMarking_True()
        {
            // If the project is explicitly marked as a test then the other condition should be ignored

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.SonarTestProject = "true";
            preImportProperties.AssemblyName = "MyTest.proj";

            EnsureAnalysisConfig(rootInputFolder, "pattern that won't match anything");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "test.proj");

            // Act
            var projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
            AssertProjectIsNotExcluded(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_ExplicitMarking_False()
        {
            // If the project is explicitly marked as not a test then the other condition should be ignored

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, "*.*");

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.SonarTestProject = "false";
            preImportProperties.ProjectTypeGuids = "X;" + TargetConstants.MsTestProjectTypeGuid.ToUpperInvariant() + ";Y";

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "foo.proj");

            // Act
            var projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsProductProject(projectInfo);
            AssertProjectIsNotExcluded(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_WildcardMatch_Default_NoMatch()
        {
            // Check the default wildcard matching

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "foo.proj");

            // Act
            var projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsProductProject(projectInfo);
            AssertProjectIsNotExcluded(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_WildcardMatch_UserSpecified_Match()
        {
            // Check user-specified wildcard matching

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, ".*foo.*");

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "foo.proj");

            // Act
            var projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
            AssertProjectIsNotExcluded(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_WildcardMatch_UserSpecified_NoMatch()
        {
            // Check user-specified wildcard matching

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, ".*foo.*");

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Use project name that will be recognized as a test by the default regex
            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "TestafoXB.proj");

            // Act
            var projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsProductProject(projectInfo);
            AssertProjectIsNotExcluded(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_HasTestGuid()
        {
            // Checks the MSTest project type guid is recognized

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, "pattern that won't match anything");

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.ProjectTypeGuids = "X" + TargetConstants.MsTestProjectTypeGuid.ToUpperInvariant() + "Y";

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "a.b");

            // Act
            var projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
            AssertProjectIsNotExcluded(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_HasTestGuid_LowerCase()
        {
            // Checks the MSTest project type guid is recognized

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, "pattern that won't match anything");

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.ProjectTypeGuids = "X" + TargetConstants.MsTestProjectTypeGuid.ToLowerInvariant() + "Y";

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "a.b");

            // Act
            var projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
            AssertProjectIsNotExcluded(projectInfo);
        }

        #endregion Test project recognition tests

        #region SQL Server projects tests

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_SqlServerProjectsAreNotExcluded()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties["SqlTargetName"] = "non-empty";

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertProjectIsNotExcluded(projectInfo);
        }

        #endregion SQL Server projects tests

        #region Fakes projects tests

        [TestMethod]
        [TestCategory("ProjectInfo")] // SONARMSBRU-26: MS Fakes should be excluded from analysis
        public void WriteProjectInfo_FakesProjectsAreExcluded()
        {
            // Checks that fakes projects are excluded from analysis

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, "pattern that won't match anything");

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.AssemblyName = "f.fAKes";

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "f.proj");

            // Act
            var projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
            AssertProjectIsExcluded(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_FakesProjects_FakesInName()
        {
            // Checks that projects with ".fakes" in the name are not excluded

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, "pattern that won't match anything");

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.AssemblyName = "f.fakes.proj";

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "f.proj");

            // Act
            var projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsProductProject(projectInfo);
            AssertProjectIsNotExcluded(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_FakesProjects_ExplicitSonarTestPropertyIsIgnored()
        {
            // Checks that fakes projects are recognized and marked as test
            // projects, irrespective of whether the SonarQubeTestProject is
            // already set.

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, "pattern that won't match anything");

            var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.SonarTestProject = "false";
            preImportProperties.AssemblyName = "MyFakeProject.fakes";

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "f.proj");

            // Act
            var projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
            AssertProjectIsExcluded(projectInfo);
        }

        #endregion Fakes projects tests

        #region Temp projects tests

        [TestMethod]
        public void WriteProjectInfo_WpfTmpCases_ProjectIsExcluded()
        {
            // Used by inner method as a way to change directory name
            int counter = 0;

            WriteProjectInfo_WpfTmpCase_ProjectIsExcluded("f.tmp_proj", expectedExclusionState: true);
            WriteProjectInfo_WpfTmpCase_ProjectIsExcluded("f.TMP_PROJ", expectedExclusionState: true);
            WriteProjectInfo_WpfTmpCase_ProjectIsExcluded("f_wpftmp.csproj", expectedExclusionState: true);
            WriteProjectInfo_WpfTmpCase_ProjectIsExcluded("f_WpFtMp.csproj", expectedExclusionState: true);
            WriteProjectInfo_WpfTmpCase_ProjectIsExcluded("f_wpftmp.vbproj", expectedExclusionState: true);

            WriteProjectInfo_WpfTmpCase_ProjectIsExcluded("WpfApplication.csproj", expectedExclusionState: false);
            WriteProjectInfo_WpfTmpCase_ProjectIsExcluded("ftmp_proj.csproj", expectedExclusionState: false);
            WriteProjectInfo_WpfTmpCase_ProjectIsExcluded("wpftmp.csproj", expectedExclusionState: false);

            void WriteProjectInfo_WpfTmpCase_ProjectIsExcluded(string projectName, bool expectedExclusionState)
            {
                // Arrange
                var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, counter.ToString(), "Inputs");
                var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, counter.ToString(), "Outputs");

                counter++;

                EnsureAnalysisConfig(rootInputFolder, "pattern that won't match anything");

                var preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
                var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, projectName);

                // Act
                var projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

                // Assert
                AssertIsNotTestProject(projectInfo);

                if (expectedExclusionState)
                {
                    AssertProjectIsExcluded(projectInfo);
                }
                else
                {
                    AssertProjectIsNotExcluded(projectInfo);
                }
            }
        }

        #endregion Temp projects tests

        #region File list tests

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_NoFiles()
        {
            // The content file list should not be created if there are no files

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "content.proj.txt");
            var projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileDoesNotExist(projectInfo, AnalysisType.FilesToAnalyze);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_HasFiles()
        {
            // The analysis file list should be created with the expected files

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "content.X.proj.txt");
            var projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Files we expect to be included
            var file1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "false");
            var file2 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "FALSE");
            var file3 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, null); // no metadata

            // Files we don't expect to be included
            AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "true");
            AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "TRUE");
            AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "true");
            AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "TRUE");

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze, file1, file2, file3);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_AutoGenFilesIgnored()
        {
            // The content file list should not include items with <AutoGen>true</AutoGen> metadata

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "agC.proj.txt");
            var projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Files we don't expect to be included
            AddFileToProject(projectRoot, TargetProperties.ItemType_Content, null, "TRUE"); // only AutoGen, set to true
            AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "false", "truE"); // exclude=false, autogen=true
            AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "true", "false"); // exclude=true, autogen=false

            // Files we expect to be included
            var content1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, null, null); // no metadata
            var compile1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, null, null); // no metadata
            var autogenContentFalseAndIncluded = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "false", "FALSe"); // exclude=false, autogen=false
            var autogenCompileFalseAndIncluded = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "false", "faLSE"); // exclude=false, autogen=false

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze, compile1, content1, autogenContentFalseAndIncluded, autogenCompileFalseAndIncluded);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_FilesTypes_Defaults()
        {
            // Check that all default item types are included for analysis

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "fileTypes.proj.txt");
            var projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Files we don't expect to be included by default
            AddFileToProject(projectRoot, "fooType");
            AddFileToProject(projectRoot, "barType");

            // Files we expect to be included by default
            var managed1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, sonarQubeExclude: null);
            var content1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, sonarQubeExclude: null);
            var embedded1 = AddFileToProject(projectRoot, "EmbeddedResource", sonarQubeExclude: null);
            var none1 = AddFileToProject(projectRoot, "None", sonarQubeExclude: null);
            var nativeCompile1 = AddFileToProject(projectRoot, "ClCompile", sonarQubeExclude: null);
            var page1 = AddFileToProject(projectRoot, "Page", sonarQubeExclude: null);
            var typeScript1 = AddFileToProject(projectRoot, "TypeScriptCompile", sonarQubeExclude: null);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze, managed1, content1, embedded1, none1, nativeCompile1, typeScript1);

            projectRoot.AddProperty("SQAnalysisFileItemTypes", "$(SQAnalysisFileItemTypes);fooType;barType;");
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_FilesTypes_OnlySpecified()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "fileTypes.proj.txt");
            var projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Files we don't expect to be included by default
            var fooType1 = AddFileToProject(projectRoot, "fooType", sonarQubeExclude: null);
            var xxxType1 = AddFileToProject(projectRoot, "xxxType", sonarQubeExclude: null);
            AddFileToProject(projectRoot, "barType", sonarQubeExclude: null);

            // Files we'd normally expect to be included by default
            AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, sonarQubeExclude: null);
            AddFileToProject(projectRoot, TargetProperties.ItemType_Content, sonarQubeExclude: null);

            projectRoot.AddProperty("SQAnalysisFileItemTypes", "fooType;xxxType");

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze, fooType1, xxxType1);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_FilesTypes_SpecifiedPlusDefaults()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "fileTypes.proj.txt");
            var projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Files we don't expect to be included by default
            var fooType1 = AddFileToProject(projectRoot, "fooType", sonarQubeExclude: null);
            var xxxType1 = AddFileToProject(projectRoot, "xxxType", sonarQubeExclude: null);
            AddFileToProject(projectRoot, "barType", sonarQubeExclude: null);

            // Files we'd normally expect to be included by default
            var managed1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, sonarQubeExclude: null);
            var content1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, sonarQubeExclude: null);

            // Update the "item types" property to add some extra item type
            // NB this has to be done *after* the integration targets have been imported
            var group = projectRoot.CreatePropertyGroupElement();
            projectRoot.AppendChild(group);
            group.AddProperty("SQAnalysisFileItemTypes", "fooType;$(SQAnalysisFileItemTypes);xxxType");
            projectRoot.Save();

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze, fooType1, xxxType1, content1, managed1);
        }

        #endregion File list tests

        #region Miscellaneous tests

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_ProjectWithCodePage()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);
            descriptor.Encoding = Encoding.GetEncoding(1250);

            var projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder, noWarningOrErrors: false /* expecting warnings */);

            // Assert
            projectInfo.Encoding.Should().Be(descriptor.Encoding.WebName);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_ProjectWithNoCodePage()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder);
            descriptor.Encoding = null;
            var projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder, noWarningOrErrors: false /* expecting warnings */);

            // Assert
            projectInfo.Encoding.Should().BeNull();
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_AnalysisSettings()
        {
            // Check analysis settings are correctly passed from the targets to the task
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "content.proj.txt");
            var projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Invalid items
            AddItem(projectRoot, "UnrelatedItemType", "irrelevantItem"); // should be ignored
            AddItem(projectRoot, TargetProperties.ItemType_Compile, "IrrelevantFile.cs"); // should be ignored
            AddItem(projectRoot, BuildTaskConstants.SettingItemName, "invalid.settings.no.value.metadata"); // invalid -> ignored

            // Module-level settings
            AddItem(projectRoot, BuildTaskConstants.SettingItemName, "valid.setting1", BuildTaskConstants.SettingValueMetadataName, "value1");
            AddItem(projectRoot, BuildTaskConstants.SettingItemName, "valid.setting2...", BuildTaskConstants.SettingValueMetadataName, "value 2 with spaces");
            AddItem(projectRoot, BuildTaskConstants.SettingItemName, "valid.path", BuildTaskConstants.SettingValueMetadataName, @"d:\aaa\bbb.txt");
            AddItem(projectRoot, BuildTaskConstants.SettingItemName, "common.setting.name", BuildTaskConstants.SettingValueMetadataName, @"local value");

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder, noWarningOrErrors: false /* expecting warnings */);

            // Assert
            AssertSettingExists(projectInfo, "valid.setting1", "value1");
            AssertSettingExists(projectInfo, "valid.setting2...", "value 2 with spaces");
            AssertSettingExists(projectInfo, "valid.path", @"d:\aaa\bbb.txt");
            AssertSettingExists(projectInfo, "common.setting.name", "local value");
            // Additional settings might be added by other targets so we won't check the total number of settings
        }

        [TestMethod]
        public void WriteProjectInfo_BareProject()
        {
            // Checks the WriteProjectInfo target handles non-VB/C# project types
            // that don't import the standard targets or set the expected properties

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            var projectFilePath = Path.Combine(rootInputFolder, "project.txt");
            var projectGuid = Guid.NewGuid();

            var projectXml = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='12.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>

  <PropertyGroup>
    <ProjectGuid>{0}</ProjectGuid>

    <SonarQubeTempPath>{1}</SonarQubeTempPath>
    <SonarQubeOutputPath>{1}</SonarQubeOutputPath>
    <SonarQubeBuildTasksAssemblyFile>{2}</SonarQubeBuildTasksAssemblyFile>
  </PropertyGroup>

  <Import Project='{3}' />
</Project>
";
            var projectRoot = BuildUtilities.CreateProjectFromTemplate(projectFilePath, TestContext, projectXml,
                projectGuid.ToString(),
                rootOutputFolder,
                typeof(WriteProjectInfoFile).Assembly.Location,
                sqTargetFile);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath,
                TargetConstants.WriteProjectDataTarget);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.WriteProjectDataTarget);

            var projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);

            projectInfo.ProjectGuid.Should().Be(projectGuid, "Unexpected project guid");
            projectInfo.ProjectLanguage.Should().BeNull("Expecting the project language to be null");
            projectInfo.IsExcluded.Should().BeFalse("Project should not be marked as excluded");
            projectInfo.ProjectType.Should().Be(ProjectType.Product, "Project should be marked as a product project");
            projectInfo.AnalysisResults.Should().BeEmpty("Not expecting any analysis results to have been created");
        }

        [TestMethod]
        public void WriteProjectInfo_UnrecognisedLanguage()
        {
            // Checks the WriteProjectInfo target handles projects with unrecognized languages

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Outputs");

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            var projectFilePath = Path.Combine(rootInputFolder, "unrecognisedLanguage.proj.txt");

            var projectXml = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='12.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>

  <PropertyGroup>
    <Language>my.special.language</Language>
    <ProjectGuid>670DAF47-CBD4-4735-B7A3-42C0A02B1CB9</ProjectGuid>

    <SonarQubeTempPath>{0}</SonarQubeTempPath>
    <SonarQubeOutputPath>{0}</SonarQubeOutputPath>
    <SonarQubeBuildTasksAssemblyFile>{1}</SonarQubeBuildTasksAssemblyFile>
  </PropertyGroup>

  <Import Project='{2}' />
</Project>
";
            var projectRoot = BuildUtilities.CreateProjectFromTemplate(projectFilePath, TestContext, projectXml,
                rootOutputFolder,
                typeof(WriteProjectInfoFile).Assembly.Location,
                sqTargetFile);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath,
                TargetConstants.WriteProjectDataTarget);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.WriteProjectDataTarget);

            var projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);

            projectInfo.ProjectLanguage.Should().Be("my.special.language", "Unexpected project language");
            projectInfo.AnalysisResults.Should().BeEmpty("Not expecting any analysis results to have been created");
        }

        #endregion Miscellaneous tests

        #region Private methods

        /// <summary>
        /// Creates a new project using the supplied descriptor and properties, then
        /// execute the WriteProjectInfoFile task. The method will check the build succeeded
        /// and that a single project output file was created.
        /// </summary>
        /// <returns>The project info file that was created during the build</returns>
        private ProjectInfo ExecuteWriteProjectInfo(ProjectDescriptor descriptor, WellKnownProjectProperties preImportProperties, string rootOutputFolder)
        {
            var projectRoot = CreateInitializedProject(descriptor, preImportProperties, rootOutputFolder);

            return ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);
        }

        private ProjectRootElement CreateInitializedProject(ProjectDescriptor descriptor, WellKnownProjectProperties preImportProperties, string rootOutputFolder)
        {
            // Still need to set the conditions so the target is invoked
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;

            // The temp folder needs to be set for the targets to succeed. Use the temp folder
            // if one has not been supplied.
            if (string.IsNullOrEmpty(preImportProperties.SonarQubeTempPath))
            {
                preImportProperties.SonarQubeTempPath = Path.GetTempPath();
            }

            // The config folder needs to be set for the targets to succeed. Use the temp folder
            // if one has not been supplied.
            if (string.IsNullOrEmpty(preImportProperties.SonarQubeConfigPath))
            {
                preImportProperties.SonarQubeConfigPath = Path.GetTempPath();
            }

            var projectRoot = BuildUtilities.CreateInitializedProjectRoot(TestContext, descriptor, preImportProperties);

            return projectRoot;
        }

        /// <summary>
        /// Executes the WriteProjectInfoFile target in the the supplied project.
        /// The method will check the build succeeded and that a single project
        /// output file was created.
        /// </summary>
        /// <returns>The project info file that was created during the build</returns>
        private ProjectInfo ExecuteWriteProjectInfo(ProjectRootElement projectRoot, string rootOutputFolder, bool noWarningOrErrors = true)
        {
            projectRoot.Save();
            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectRoot.FullPath,
                // The "write" target depends on a couple of other targets having executed first to set properties appropriately
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.CreateProjectSpecificDirs,
                TargetConstants.WriteProjectDataTarget);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.CalculateFilesToAnalyzeTarget);
            result.AssertTargetSucceeded(TargetConstants.CreateProjectSpecificDirs);
            result.AssertTargetSucceeded(TargetConstants.WriteProjectDataTarget);

            result.AssertTargetExecuted(TargetConstants.WriteProjectDataTarget);

            if (noWarningOrErrors)
            {
                result.AssertNoWarningsOrErrors();
            }

            // Check expected project outputs
            Directory.EnumerateDirectories(rootOutputFolder).Should().HaveCount(1, "Only expecting one child directory to exist under the root analysis output folder");
            var projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);

            return projectInfo;
        }

        /// <summary>
        /// Creates an empty file on disc and adds it to the project as an
        /// item with the specified ItemGroup include name.
        /// The SonarQubeExclude metadata item is set to the specified value.
        /// </summary>
        /// <param name="includeName">The name of the item type e.g. Compile, Content</param>
        /// <param name="sonarQubeExclude">The value to assign to the SonarExclude metadata item</param>
        /// <returns>The full path to the new file</returns>
        private string AddFileToProject(ProjectRootElement projectRoot, string includeName, string sonarQubeExclude)
        {
            var newItem = AddFileToProject(projectRoot, includeName);
            if (sonarQubeExclude != null)
            {
                newItem.AddMetadata(TargetProperties.SonarQubeExcludeMetadata, sonarQubeExclude);
            }

            return newItem.Include;
        }

        /// Creates an empty file on disc and adds it to the project as an
        /// item with the specified ItemGroup include name.
        /// The SonarQubeExclude metadata item is set to the specified value.
        /// </summary>
        /// <param name="includeName">The name of the item type e.g. Compile, Content</param>
        /// <param name="sonarQubeExclude">The value to assign to the SonarExclude metadata item</param>
        /// <returns>The full path to the new file</returns>
        private string AddFileToProject(ProjectRootElement projectRoot, string includeName, string sonarQubeExclude, string autoGen)
        {
            var newItem = AddFileToProject(projectRoot, includeName);
            if (sonarQubeExclude != null)
            {
                newItem.AddMetadata(TargetProperties.SonarQubeExcludeMetadata, sonarQubeExclude);
            }
            if (autoGen != null)
            {
                newItem.AddMetadata(TargetProperties.AutoGenMetadata, autoGen);
            }

            return newItem.Include;
        }

        /// <summary>
        /// Creates an empty file on disc and adds it to the project as an
        /// item with the specified ItemGroup include name.
        /// The SonarQubeExclude metadata item is set to the specified value.
        /// </summary>
        /// <param name="includeName">The name of the item type e.g. Compile, Content</param>
        /// <returns>The new project item</returns>
        private ProjectItemElement AddFileToProject(ProjectRootElement projectRoot, string includeName)
        {
            var projectPath = Path.GetDirectoryName(projectRoot.DirectoryPath); // project needs to have been saved for this to work
            var fileName = includeName + "_" + System.Guid.NewGuid().ToString();
            var fullPath = Path.Combine(projectPath, fileName);
            File.WriteAllText(fullPath, "");

            var element = projectRoot.AddItem(includeName, fullPath);
            return element;
        }

        /// <summary>
        /// Creates a default set of properties sufficient to trigger test analysis
        /// </summary>
        /// <param name="inputPath">The analysis config directory</param>
        /// <param name="outputPath">The output path into which results should be written</param>
        /// <returns></returns>
        private static WellKnownProjectProperties CreateDefaultAnalysisProperties(string configPath, string outputPath)
        {
            var preImportProperties = new WellKnownProjectProperties
            {
                SonarQubeTempPath = configPath,
                SonarQubeOutputPath = outputPath,
                SonarQubeConfigPath = configPath
            };
            return preImportProperties;
        }

        /// <summary>
        /// Ensures an analysis config file exists in the specified directory,
        /// replacing one if it already exists.
        /// If the supplied "regExExpression" is not null then the appropriate setting
        /// entry will be created in the file
        /// </summary>
        private static void EnsureAnalysisConfig(string parentDir, string regExExpression)
        {
            var config = new AnalysisConfig();
            if (regExExpression != null)
            {
                config.LocalSettings = new AnalysisProperties
                {
                    new Property { Id = IsTestFileByName.TestRegExSettingId, Value = regExExpression }
                };
            }

            var fullPath = Path.Combine(parentDir, FileConstants.ConfigFileName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            config.Save(fullPath);
        }

        private static ProjectItemElement AddItem(ProjectRootElement projectRoot, string itemTypeName, string include, params string[] idAndValuePairs)
        {
            var item = projectRoot.AddItem(itemTypeName, include);

            Math.DivRem(idAndValuePairs.Length, 2, out int remainder);
            remainder.Should().Be(0, "Test setup error: the supplied list should contain id-location pairs");

            for (var index = 0; index < idAndValuePairs.Length; index += 2)
            {
                item.AddMetadata(idAndValuePairs[index], idAndValuePairs[index + 1]);
            }

            return item;
        }

        #endregion Private methods

        #region Assertions

        private static void AssertIsProductProject(ProjectInfo projectInfo)
        {
            projectInfo.ProjectType.Should().Be(ProjectType.Product, "Should be a product (i.e. non-test) project");
        }

        private static void AssertIsTestProject(ProjectInfo projectInfo)
        {
            projectInfo.ProjectType.Should().Be(ProjectType.Test, "Should be a test project");
        }

        private static void AssertIsNotTestProject(ProjectInfo projectInfo)
        {
            projectInfo.ProjectType.Should().NotBe(ProjectType.Test, "Should not be a test project");
        }

        private static void AssertProjectIsExcluded(ProjectInfo projectInfo)
        {
            projectInfo.IsExcluded.Should().BeTrue("Expecting the project to be excluded");
        }

        private static void AssertProjectIsNotExcluded(ProjectInfo projectInfo)
        {
            projectInfo.IsExcluded.Should().BeFalse("Not expecting the project to be excluded");
        }

        private void AssertResultFileDoesNotExist(ProjectInfo projectInfo, AnalysisType resultType)
        {
            var found = projectInfo.TryGetAnalyzerResult(resultType, out AnalysisResult result);

            if (found)
            {
                TestContext.AddResultFile(result.Location);
            }

            found.Should().BeFalse("Analysis result found unexpectedly. Result type: {0}", resultType);
        }

        private void AssertResultFileExists(ProjectInfo projectInfo, AnalysisType resultType, params string[] expected)
        {
            var found = projectInfo.TryGetAnalyzerResult(resultType, out AnalysisResult result);

            found.Should().BeTrue("Analysis result not found: {0}", resultType);
            File.Exists(result.Location).Should().BeTrue("Analysis result file not found");

            TestContext.AddResultFile(result.Location);

            var actualFiles = File.ReadAllLines(result.Location);

            try
            {
                actualFiles.Should().BeEquivalentTo(expected, "The analysis result file does not contain the expected entries");
            }
            catch (AssertFailedException)
            {
                TestContext.WriteLine("Expected files: {1}{0}", Environment.NewLine, string.Join("\t" + Environment.NewLine, expected));
                TestContext.WriteLine("Actual files: {1}{0}", Environment.NewLine, string.Join("\t" + Environment.NewLine, actualFiles));
                throw;
            }
        }

        private void AssertSettingExists(ProjectInfo projectInfo, string expectedId, string expectedValue)
        {
            var found = projectInfo.TryGetAnalysisSetting(expectedId, out Property actualSetting);
            found.Should().BeTrue("Expecting the analysis setting to be found. Id: {0}", expectedId);

            // Check the implementation of TryGetAnalysisSetting
            actualSetting.Should().NotBeNull("The returned setting should not be null if the function returned true");
            actualSetting.Id.Should().Be(expectedId, "TryGetAnalysisSetting returned a setting with an unexpected id");

            actualSetting.Value.Should().Be(expectedValue, "Setting has an unexpected value. Id: {0}", expectedId);
        }

        #endregion Assertions
    }
}
