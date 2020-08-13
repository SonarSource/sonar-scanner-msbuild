/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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

        #region File list tests

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_NoFiles()
        {
            // The content file list should not be created if there are no files

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            var projectFilePath = CreateProjectFile(null, null, rootOutputFolder);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectFilePath, rootOutputFolder);

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
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // Note: the included/excluded files don't actually have to exist
            string projectXml = $@"
<ItemGroup>
  <Content Include='included1.txt'>
    <SonarQubeExclude>false</SonarQubeExclude>
  </Content>
  <Compile Include='included2.txt'>
    <SonarQubeExclude>false</SonarQubeExclude>
  </Compile>
  <Content Include='included3.txt' />


  <Content Include='excluded1.txt'>
    <SonarQubeExclude>true</SonarQubeExclude>
  </Content>
  <Content Include='excluded2.txt'>
    <SonarQubeExclude>TRUE</SonarQubeExclude>
  </Content>
  <Compile Include='excluded3.txt'>
    <SonarQubeExclude>true</SonarQubeExclude>
  </Compile>
  <Compile Include='excluded4.txt'>
    <SonarQubeExclude>TRUE</SonarQubeExclude>
  </Compile>

</ItemGroup>
";
            var projectFilePath = CreateProjectFile(null, projectXml, rootOutputFolder);
            var projectDir = Path.GetDirectoryName(projectFilePath);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectFilePath, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze,
                projectDir + "\\included1.txt",
                projectDir + "\\included2.txt",
                projectDir + "\\included3.txt");
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_AutoGenFilesIgnored()
        {
            // The content file list should not include items with <AutoGen>true</AutoGen> metadata

            // Arrange
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");
            
            // Note: the included/excluded files don't actually have to exist
            string projectXml = $@"
<ItemGroup>
  <!-- Files we expect to be excluded -->
  <Content Include='excluded1.txt'>
    <AutoGen>TRUE</AutoGen>
  </Content>
  <Compile Include='excluded2.txt'>
    <SonarQubeExclude>false</SonarQubeExclude>
    <AutoGen>truE</AutoGen>
  </Compile>
  <Compile Include='excluded3.txt'>
    <SonarQubeExclude>true</SonarQubeExclude>
    <AutoGen>false</AutoGen>
  </Compile>

  <!-- Files we expect to be included -->
  <Content Include='included1.txt' />
  <Compile Include='included2.txt' />
  <Content Include='included3.txt' >
    <SonarQubeExclude>false</SonarQubeExclude>
    <AutoGen>FALSe</AutoGen>
  </Content>
  <Compile Include='included4.txt'>
    <SonarQubeExclude>false</SonarQubeExclude>
    <AutoGen>faLSe</AutoGen>
  </Compile>

</ItemGroup>
";
            var projectFilePath = CreateProjectFile(null, projectXml, rootOutputFolder);
            var projectDir = Path.GetDirectoryName(projectFilePath);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectFilePath, rootOutputFolder);


            // Act
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze,
                projectDir + "\\included1.txt",
                projectDir + "\\included2.txt",
                projectDir + "\\included3.txt",
                projectDir + "\\included4.txt");
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_FilesTypes_Defaults()
        {
            // Check that all default item types are included for analysis

            // Arrange
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // Note: the included/excluded files don't actually have to exist
            string projectXml = $@"
<ItemGroup>
  <!-- Files we expect to be excluded -->
  <fooType Include='xfile1.txt' />
  <barType Include='xfile2.txt' />

  <!-- Files we expect to be included -->
  <Compile Include='compile.txt'>
    <SonarQubeExclude></SonarQubeExclude>
  </Compile>
  <Content Include='content.txt' >
    <SonarQubeExclude />
  </Content>
  <EmbeddedResource Include='resource.res' >
    <SonarQubeExclude />
  </EmbeddedResource>
  <None Include='none.none' />
  <ClCompile Include='code.cpp' />
  <Page Include='page.page' />
  <TypeScriptCompile Include='tsfile.ts' />

</ItemGroup>
";
            var projectFilePath = CreateProjectFile(null, projectXml, rootOutputFolder);
            var projectDir = Path.GetDirectoryName(projectFilePath);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectFilePath, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze,
                projectDir + "\\compile.txt",
                projectDir + "\\content.txt",
                projectDir + "\\resource.res",
                projectDir + "\\none.none",
                projectDir + "\\code.cpp",
                projectDir + "\\tsfile.ts",
                projectDir + "\\page.page");
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_FilesTypes_PageAndApplicationDefinition()
        {
            // Check that all default item types are included for analysis

            // Arrange
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // Note: the included/excluded files don't actually have to exist
            string projectXml = $@"
<ItemGroup>

  <!-- Files we expect to be included -->
  <ApplicationDefinition Include='MyApp.xaml'>
      <Generator>MSBuild:Compile</Generator>
      <SubType>Designer</SubType>
  </ApplicationDefinition>
  <Page Include='HomePage.xaml'>
      <SubType>Designer</SubType>
      <Generator>MSBuild:Compile</Generator>
    </Page>
  <Compile Include='MyApp.cs'>
      <DependentUpon>MyApp.xaml</DependentUpon>
      <SubType>Code</SubType>
    </Compile>
<Compile Include='HomePage.cs'>
      <DependentUpon>App.xaml</DependentUpon>
      <SubType>Code</SubType>
    </Compile>
</ItemGroup>
";
            var projectFilePath = CreateProjectFile(null, projectXml, rootOutputFolder);
            var projectDir = Path.GetDirectoryName(projectFilePath);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectFilePath, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze,
                projectDir + "\\MyApp.xaml",
                projectDir + "\\MyApp.cs",
                projectDir + "\\HomePage.xaml",
                projectDir + "\\HomePage.cs");
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_FilesTypes_OnlySpecified()
        {
            // Arrange
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // Note: the included/excluded files don't actually have to exist
            string projectXml = $@"
<PropertyGroup>
  <!-- Set the file types to be included -->
  <SQAnalysisFileItemTypes>fooType;xxxType</SQAnalysisFileItemTypes>
</PropertyGroup>

<ItemGroup>

  <!-- Files we don't expect to be excluded by default -->
  <fooType Include='foo.foo' />
  <xxxType Include='xxxType.xxx' />
  <barType Include='barType.xxx' />

  <!-- Files we normal expect to be included by default -->
  <Compile Include='compile.txt' />
  <Content Include='content.txt' />

</ItemGroup>
";
            var projectFilePath = CreateProjectFile(null, projectXml, rootOutputFolder);
            var projectDir = Path.GetDirectoryName(projectFilePath);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectFilePath, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze,
                projectDir + "\\foo.foo",
                projectDir + "\\xxxType.xxx");
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_AnalysisFileList_FilesTypes_SpecifiedPlusDefaults()
        {
            // Arrange
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            // Note: the included/excluded files don't actually have to exist
            string projectXml = $@"
<PropertyGroup>
  <!-- Specify some additional types to be included -->
  <SQAdditionalAnalysisFileItemTypes>fooType;xxxType</SQAdditionalAnalysisFileItemTypes>
</PropertyGroup>

<ItemGroup>

  <!-- Files we don't expect to be excluded by default -->
  <fooType Include='foo.foo' />
  <xxxType Include='xxxType.xxx' />
  <barType Include='barType.xxx' />

  <!-- Files we normal expect to be included by default -->
  <Compile Include='compile.txt' />
  <Content Include='content.txt' />

</ItemGroup>
";
            var projectFilePath = CreateProjectFile(null, projectXml, rootOutputFolder);
            var projectDir = Path.GetDirectoryName(projectFilePath);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectFilePath, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.FilesToAnalyze,
                projectDir + "\\foo.foo",
                projectDir + "\\xxxType.xxx",
                projectDir + "\\compile.txt",
                projectDir + "\\content.txt");
        }

        #endregion File list tests

        #region Miscellaneous tests

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_IsNotTestAndNotExcluded()
        {
            // Check that SonarQubeTestProject and SonarQubeExclude are
            // correctly set for "normal" projects

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");
            var analysisConfig = new AnalysisConfig
            {
                LocalSettings = new AnalysisProperties
                {
                    new Property { Id = IsTestFileByName.TestRegExSettingId, Value = "pattern that won't match anything" }
                }
            };

            var projectFilePath = CreateProjectFile(analysisConfig, null, rootOutputFolder);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectFilePath, rootOutputFolder);

            // Assert
            AssertIsProductProject(projectInfo);
            AssertProjectIsNotExcluded(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_IsTestAndIsExcluded()
        {
            // Check that SonarQubeTestProject and SonarQubeExclude are
            // correctly serialized. We'll test using a fakes project since
            // both values should be set to true.

            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");
            var analysisConfig = new AnalysisConfig
            {
                LocalSettings = new AnalysisProperties
                {
                    new Property { Id = IsTestFileByName.TestRegExSettingId, Value = "pattern that won't match anything" }
                }
            };

            string projectXml = $@"
<PropertyGroup>
  <AssemblyName>f.fAKes</AssemblyName>
</PropertyGroup>
";
            var projectFilePath = CreateProjectFile(analysisConfig, projectXml, rootOutputFolder);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectFilePath, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
            AssertProjectIsExcluded(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_ProjectWithCodePage()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");
            var analysisConfig = new AnalysisConfig
            {
                LocalSettings = new AnalysisProperties
                {
                    new Property { Id = IsTestFileByName.TestRegExSettingId, Value = "pattern that won't match anything" }
                }
            };

            string projectXml = $@"
<PropertyGroup>
  <CodePage>1250</CodePage>
</PropertyGroup>
";
            var projectFilePath = CreateProjectFile(analysisConfig, projectXml, rootOutputFolder);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectFilePath, rootOutputFolder, noWarningOrErrors: false /* expecting warnings */);

            // Assert
            projectInfo.Encoding.Should().Be("windows-1250");
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_ProjectWithNoCodePage()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            string projectXml = $@"
<PropertyGroup>
  <CodePage />
</PropertyGroup>
";
            var projectFilePath = CreateProjectFile(null, projectXml, rootOutputFolder);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectFilePath, rootOutputFolder, noWarningOrErrors: false /* expecting warnings */);

            // Assert
            projectInfo.Encoding.Should().BeNull();
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_AnalysisSettings()
        {
            // Check analysis settings are correctly passed from the targets to the task
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

            string projectXml = $@"
<ItemGroup>
  <!-- Items that should not produce settings in the projectInfo.xml -->
  <UnrelatedItemType Include='irrelevantItem' />
  <Compile Include='irrelevantFile.cs' />
  <SonarQubeSetting Include='invalid.settings.no.value.metadata' />

  <!-- Items that should produce settings in the projectInfo.xml -->
  <SonarQubeSetting Include='valid.setting1' >
    <Value>value1</Value>
  </SonarQubeSetting>

  <SonarQubeSetting Include='valid.setting2...' >
    <Value>value 2 with spaces</Value>
  </SonarQubeSetting>

  <SonarQubeSetting Include='valid.path' >
    <Value>d:\aaa\bbb.txt</Value>
  </SonarQubeSetting>

  <SonarQubeSetting Include='common.setting.name' >
    <Value>local value</Value>
  </SonarQubeSetting>

</ItemGroup>
";
            var projectFilePath = CreateProjectFile(null, projectXml, rootOutputFolder);

            // Act
            var projectInfo = ExecuteWriteProjectInfo(projectFilePath, rootOutputFolder, noWarningOrErrors: false /* expecting warnings */);

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
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

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
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");
            var rootOutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");

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
        
        private ProjectInfo ExecuteWriteProjectInfo(string projectFilePath, string rootOutputFolder, bool noWarningOrErrors = true)
        {
            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath,
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
            var projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectFilePath);

            return projectInfo;
        }

        private string CreateProjectFile(AnalysisConfig analysisConfig, string testSpecificProjectXml, string sonarQubeOutputPath)
        {
            var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            if (analysisConfig != null)
            {
                var configFilePath = Path.Combine(projectDirectory, FileConstants.ConfigFileName);
                analysisConfig.Save(configFilePath);
            }

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            File.Exists(sqTargetFile).Should().BeTrue("Test error: the SonarQube analysis targets file could not be found. Full path: {0}", sqTargetFile);
            TestContext.AddResultFile(sqTargetFile);


            var template = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion='Current' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>

  <!-- Boilerplate -->
  <!-- All of these boilerplate properties can be overridden by setting the value again in the test-specific XML snippet -->
  <PropertyGroup>
    <ImportByWildcardBeforeMicrosoftCommonTargets>false</ImportByWildcardBeforeMicrosoftCommonTargets>
    <ImportByWildcardAfterMicrosoftCommonTargets>false</ImportByWildcardAfterMicrosoftCommonTargets>
    <ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets>false</ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets>
    <ImportUserLocationsByWildcardAfterMicrosoftCommonTargets>false</ImportUserLocationsByWildcardAfterMicrosoftCommonTargets>
    <OutputPath>bin\</OutputPath>
    <OutputType>library</OutputType>
    <ProjectGuid>ffdb93c0-2880-44c7-89a6-bbd4ddab034a</ProjectGuid>
    <Language>C#</Language>
  </PropertyGroup>

  <PropertyGroup>
    <!-- Standard values that need to be set for each/most tests -->
    <SonarQubeBuildTasksAssemblyFile>SONARSCANNER_MSBUILD_TASKS_DLL</SonarQubeBuildTasksAssemblyFile>
    <SonarQubeConfigPath>PROJECT_DIRECTORY_PATH</SonarQubeConfigPath>
    <SonarQubeTempPath>PROJECT_DIRECTORY_PATH</SonarQubeTempPath>
    <SonarQubeOutputPath>SQ_OUTPUT_PATH</SonarQubeOutputPath>

    <!-- Ensure the project is isolated from environment variables that could be picked up when running on a TeamBuild build agent-->
    <TF_BUILD_BUILDDIRECTORY />
    <AGENT_BUILDDIRECTORY />
  </PropertyGroup>

  <!-- Test-specific data -->
  TEST_SPECIFIC_XML

  <!-- Standard boilerplate closing imports -->
  <Import Project='$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), SonarQube.Integration.targets))SonarQube.Integration.targets' />
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>
";
            var projectData = template.Replace("PROJECT_DIRECTORY_PATH", projectDirectory)
                .Replace("SONARSCANNER_MSBUILD_TASKS_DLL", typeof(WriteProjectInfoFile).Assembly.Location)
                .Replace("TEST_SPECIFIC_XML", testSpecificProjectXml ?? "<!-- none -->")
                .Replace("SQ_OUTPUT_PATH", sonarQubeOutputPath);

            var projectFilePath = Path.Combine(projectDirectory, TestContext.TestName + ".proj.txt");
            File.WriteAllText(projectFilePath, projectData);
            TestContext.AddResultFile(projectFilePath);

            return projectFilePath;
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
