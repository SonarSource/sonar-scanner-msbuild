//-----------------------------------------------------------------------
// <copyright file="WriteProjectInfoFileTargetTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.IntegrationTests.TargetsTests
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
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.SonarTestProject = "true";
            preImportProperties.AssemblyName = "MyTest.proj";

            EnsureAnalysisConfig(rootInputFolder, "pattern that won't match anything");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "test.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

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
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, "*.*");

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.SonarTestProject = "false";
            preImportProperties.ProjectTypeGuids = "X;" + TargetConstants.MsTestProjectTypeGuid.ToUpperInvariant() + ";Y";

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "foo.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

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
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "foo.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

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
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, ".*foo.*");

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "fOO.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

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
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, ".*foo.*");

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);

            // Use project name that will be recognised as a test by the default regex
            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "TestafoXB.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsProductProject(projectInfo);
            AssertProjectIsNotExcluded(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_HasTestGuid()
        {
            // Checks the MSTest project type guid is recognised

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, "pattern that won't match anything");

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.ProjectTypeGuids = "X" + TargetConstants.MsTestProjectTypeGuid.ToUpperInvariant() + "Y";

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "a.b");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
            AssertProjectIsNotExcluded(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_HasTestGuid_LowerCase()
        {
            // Checks the MSTest project type guid is recognised

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, "pattern that won't match anything");

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.ProjectTypeGuids = "X" + TargetConstants.MsTestProjectTypeGuid.ToLowerInvariant() + "Y";

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "a.b");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
            AssertProjectIsNotExcluded(projectInfo);
        }

        #endregion

        #region Fakes projects tests

        [TestMethod]
        [TestCategory("ProjectInfo")] // SONARMSBRU-26: MS Fakes should be excluded from analysis
        public void WriteProjectInfo_FakesProjectsAreExcluded()
        {
            // Checks that fakes projects are excluded from analysis

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, "pattern that won't match anything");

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.AssemblyName = "f.fAKes";
            
            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "f.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

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
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, "pattern that won't match anything");

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.AssemblyName = "f.fakes.proj";

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "f.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsProductProject(projectInfo);
            AssertProjectIsNotExcluded(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_FakesProjects_ExplicitSonarTestPropertyIsIgnored()
        {
            // Checks that fakes projects are recognised and marked as test
            // projects, irrespective of whether the SonarQubeTestProject is
            // already set.

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            EnsureAnalysisConfig(rootInputFolder, "pattern that won't match anything");

            WellKnownProjectProperties preImportProperties = CreateDefaultAnalysisProperties(rootInputFolder, rootOutputFolder);
            preImportProperties.SonarTestProject = "false";
            preImportProperties.AssemblyName = "MyFakeProject.fakes";

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "f.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
            AssertProjectIsExcluded(projectInfo);
        }

        #endregion

        #region File list tests

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_NoFiles()
        {
            // The content file list should not be created if there are no files

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "content.proj.txt");
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

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
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "content.X.proj.txt");
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Files we expect to be included
            string file1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "false");
            string file2 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "FALSE");
            string file3 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, null); // no metadata

            // Files we don't expect to be included
            AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "true");
            AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "TRUE");
            AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "true");
            AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "TRUE");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

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
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "agC.proj.txt");
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);
            
            // Files we don't expect to be included
            AddFileToProject(projectRoot, TargetProperties.ItemType_Content, null, "TRUE"); // only AutoGen, set to true
            AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "false", "truE"); // exclude=false, autogen=true
            AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "true", "false"); // exclude=true, autogen=false

            // Files we expect to be included
            string content1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, null, null); // no metadata
            string compile1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, null, null); // no metadata
            string autogenContentFalseAndIncluded = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "false", "FALSe"); // exclude=false, autogen=false
            string autogenCompileFalseAndIncluded = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "false", "faLSE"); // exclude=false, autogen=false
            
            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

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
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "fileTypes.proj.txt");
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Files we don't expect to be included by default
            AddFileToProject(projectRoot, "fooType");
            AddFileToProject(projectRoot, "barType");

            // Files we expect to be included by default
            string managed1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, sonarQubeExclude: null);
            string content1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, sonarQubeExclude: null);
            string embedded1 = AddFileToProject(projectRoot, "EmbeddedResource", sonarQubeExclude: null);
            string none1 = AddFileToProject(projectRoot, "None", sonarQubeExclude: null);
            string nativeCompile1 = AddFileToProject(projectRoot, "ClCompile", sonarQubeExclude: null);
            string page1 = AddFileToProject(projectRoot, "Page", sonarQubeExclude: null);
            string typeScript1 = AddFileToProject(projectRoot, "TypeScriptCompile", sonarQubeExclude: null);

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze, managed1, content1, embedded1, none1, nativeCompile1, page1, typeScript1);

            projectRoot.AddProperty("SQAnalysisFileItemTypes", "$(SQAnalysisFileItemTypes);fooType;barType;");
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_FilesTypes_OnlySpecified()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "fileTypes.proj.txt");
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Files we don't expect to be included by default
            string fooType1 = AddFileToProject(projectRoot, "fooType", sonarQubeExclude: null);
            string xxxType1 = AddFileToProject(projectRoot, "xxxType", sonarQubeExclude: null);
            AddFileToProject(projectRoot, "barType", sonarQubeExclude: null);
            
            // Files we'd normally expect to be included by default
            AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, sonarQubeExclude: null);
            AddFileToProject(projectRoot, TargetProperties.ItemType_Content, sonarQubeExclude: null);

            projectRoot.AddProperty("SQAnalysisFileItemTypes", "fooType;xxxType");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze, fooType1, xxxType1);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_FilesTypes_SpecifiedPlusDefaults()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "fileTypes.proj.txt");
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Files we don't expect to be included by default
            string fooType1 = AddFileToProject(projectRoot, "fooType", sonarQubeExclude: null);
            string xxxType1 = AddFileToProject(projectRoot, "xxxType", sonarQubeExclude: null);
            AddFileToProject(projectRoot, "barType", sonarQubeExclude: null);

            // Files we'd normally expect to be included by default
            string managed1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, sonarQubeExclude: null);
            string content1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, sonarQubeExclude: null);

            // Update the "item types" property to add some extra item type
            // NB this has to be done *after* the integration targets have been imported
            ProjectPropertyGroupElement group = projectRoot.CreatePropertyGroupElement();
            projectRoot.AppendChild(group);
            group.AddProperty("SQAnalysisFileItemTypes", "fooType;$(SQAnalysisFileItemTypes);xxxType");
            projectRoot.Save();

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze, fooType1, xxxType1, content1, managed1);
        }

        #endregion
        
        #region Miscellaneous tests

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_AnalysisSettings()
        {
            // Check analysis settings are correctly passed from the targets to the task
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "content.proj.txt");
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

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
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder, noWarningOrErrors: false /* expecting warnings */);

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
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            BuildLogger logger = new BuildLogger();

            string sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(this.TestContext);
            string projectFilePath = Path.Combine(rootInputFolder, "project.txt");
            Guid projectGuid = Guid.NewGuid();

            string projectXml = @"<?xml version='1.0' encoding='utf-8'?>
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
            ProjectRootElement projectRoot = BuildUtilities.CreateProjectFromTemplate(projectFilePath, this.TestContext, projectXml,
                projectGuid.ToString(),
                rootOutputFolder,
                typeof(WriteProjectInfoFile).Assembly.Location,
                sqTargetFile);

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger,
                TargetConstants.WriteProjectDataTarget);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.WriteProjectDataTarget);

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);

            Assert.AreEqual(projectGuid, projectInfo.ProjectGuid, "Unexpected project guid");
            Assert.IsNull(projectInfo.ProjectLanguage, "Expecting the project language to be null");
            Assert.IsFalse(projectInfo.IsExcluded, "Project should not be marked as excluded");
            Assert.AreEqual(ProjectType.Product, projectInfo.ProjectType, "Project should be marked as a product project");
            Assert.AreEqual(0, projectInfo.AnalysisResults.Count, "Not expecting any analysis results to have been created");
        }

        [TestMethod]
        public void WriteProjectInfo_UnrecognisedLanguage()
        {
            // Checks the WriteProjectInfo target handles projects with unrecognised languages

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            BuildLogger logger = new BuildLogger();

            string sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(this.TestContext);
            string projectFilePath = Path.Combine(rootInputFolder, "unrecognisedLanguage.proj.txt");

            string projectXml = @"<?xml version='1.0' encoding='utf-8'?>
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
            ProjectRootElement projectRoot = BuildUtilities.CreateProjectFromTemplate(projectFilePath, this.TestContext, projectXml,
                rootOutputFolder,
                typeof(WriteProjectInfoFile).Assembly.Location,
                sqTargetFile);

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger,
                TargetConstants.WriteProjectDataTarget);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.WriteProjectDataTarget);

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);

            Assert.AreEqual("my.special.language", projectInfo.ProjectLanguage, "Unexpected project language");
            Assert.AreEqual(0, projectInfo.AnalysisResults.Count, "Not expecting any analysis results to have been created");
        }


        #endregion

        #region Private methods

        /// <summary>
        /// Creates a new project using the supplied descriptor and properties, then
        /// execute the WriteProjectInfoFile task. The method will check the build succeeded
        /// and that a single project output file was created.
        /// </summary>
        /// <returns>The project info file that was created during the build</returns>
        private ProjectInfo ExecuteWriteProjectInfo(ProjectDescriptor descriptor, WellKnownProjectProperties preImportProperties, string rootOutputFolder)
        {
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, preImportProperties, rootOutputFolder);

            return this.ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);
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
            
            ProjectRootElement projectRoot = BuildUtilities.CreateInitializedProjectRoot(this.TestContext, descriptor, preImportProperties);

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
            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger,
                // The "write" target depends on a couple of other targets having executed first to set properties appropriately
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.WriteProjectDataTarget);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.CalculateFilesToAnalyzeTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.WriteProjectDataTarget);

            logger.AssertTargetExecuted(TargetConstants.WriteProjectDataTarget);

            if (noWarningOrErrors)
            {
                logger.AssertNoWarningsOrErrors();
            }
            
            // Check expected project outputs
            Assert.AreEqual(1, Directory.EnumerateDirectories(rootOutputFolder).Count(), "Only expecting one child directory to exist under the root analysis output folder");
            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);

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
            ProjectItemElement newItem = AddFileToProject(projectRoot, includeName);
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
            ProjectItemElement newItem = AddFileToProject(projectRoot, includeName);
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
            string projectPath = Path.GetDirectoryName(projectRoot.DirectoryPath); // project needs to have been saved for this to work
            string fileName = includeName + "_" + System.Guid.NewGuid().ToString();
            string fullPath = Path.Combine(projectPath, fileName);
            File.WriteAllText(fullPath, "");

            ProjectItemElement element = projectRoot.AddItem(includeName, fullPath);
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
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = configPath;
            preImportProperties.SonarQubeOutputPath = outputPath;
            preImportProperties.SonarQubeConfigPath = configPath;
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
            AnalysisConfig config = new AnalysisConfig();
            if (regExExpression != null)
            {
                config.LocalSettings = new AnalysisProperties();
                config.LocalSettings.Add(new Property() { Id = IsTestFileByName.TestRegExSettingId, Value = regExExpression });
            }

            string fullPath = Path.Combine(parentDir, FileConstants.ConfigFileName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            config.Save(fullPath);
        }

        private static ProjectItemElement AddItem(ProjectRootElement projectRoot, string itemTypeName, string include, params string[] idAndValuePairs)
        {
            ProjectItemElement item = projectRoot.AddItem(itemTypeName, include);
            
            int remainder;
            Math.DivRem(idAndValuePairs.Length, 2, out remainder);
            Assert.AreEqual(0, remainder, "Test setup error: the supplied list should contain id-location pairs");

            for (int index = 0; index < idAndValuePairs.Length; index += 2)
            {
                item.AddMetadata(idAndValuePairs[index], idAndValuePairs[index + 1]);
            }

            return item;
        }

        #endregion

        #region Assertions

        private static void AssertIsProductProject(ProjectInfo projectInfo)
        {
            Assert.AreEqual(ProjectType.Product, projectInfo.ProjectType, "Should be a product (i.e. non-test) project");
        }

        private static void AssertIsTestProject(ProjectInfo projectInfo)
        {
            Assert.AreEqual(ProjectType.Test, projectInfo.ProjectType, "Should be a test project");
        }

        private static void AssertProjectIsExcluded(ProjectInfo projectInfo)
        {
            Assert.IsTrue(projectInfo.IsExcluded, "Expecting the project to be excluded");
        }

        private static void AssertProjectIsNotExcluded(ProjectInfo projectInfo)
        {
            Assert.IsFalse(projectInfo.IsExcluded, "Not expecting the project to be excluded");
        }

        private void AssertResultFileDoesNotExist(ProjectInfo projectInfo, AnalysisType resultType)
        {
            AnalysisResult result;
            bool found = projectInfo.TryGetAnalyzerResult(resultType, out result);

            if (found)
            {
                this.TestContext.AddResultFile(result.Location);
            }

            Assert.IsFalse(found, "Analysis result found unexpectedly. Result type: {0}", resultType);
        }

        private void AssertResultFileExists(ProjectInfo projectInfo, AnalysisType resultType, params string[] expected)
        {
            AnalysisResult result;
            bool found = projectInfo.TryGetAnalyzerResult(resultType, out result);

            Assert.IsTrue(found, "Analysis result not found: {0}", resultType);
            Assert.IsTrue(File.Exists(result.Location), "Analysis result file not found");

            this.TestContext.AddResultFile(result.Location);

            string[] actualFiles = File.ReadAllLines(result.Location);

            try
            {
                CollectionAssert.AreEquivalent(expected, actualFiles, "The analysis result file does not contain the expected entries");
            }
            catch (AssertFailedException)
            {
                this.TestContext.WriteLine("Expected files: {1}{0}", Environment.NewLine, string.Join("\t" + Environment.NewLine, expected));
                this.TestContext.WriteLine("Actual files: {1}{0}", Environment.NewLine, string.Join("\t" + Environment.NewLine, actualFiles));
                throw;
            }
        }

        private void AssertSettingExists(ProjectInfo projectInfo, string expectedId, string expectedValue)
        {
            Property actualSetting;
            bool found = projectInfo.TryGetAnalysisSetting(expectedId, out actualSetting);
            Assert.IsTrue(found, "Expecting the analysis setting to be found. Id: {0}", expectedId);

            // Check the implementation of TryGetAnalysisSetting
            Assert.IsNotNull(actualSetting, "The returned setting should not be null if the function returned true");
            Assert.AreEqual(expectedId, actualSetting.Id, "TryGetAnalysisSetting returned a setting with an unexpected id");

            Assert.AreEqual(expectedValue, actualSetting.Value, "Setting has an unexpected value. Id: {0}", expectedId);
        }

        #endregion
    }
}
