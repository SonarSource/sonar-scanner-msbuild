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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;
using TestUtilities;

namespace SonarScanner.MSBuild.Tasks.UnitTest;

[TestClass]
public class WriteProjectInfoFileTests
{
    private const string ExpectedProjectInfoFileName = "ProjectInfo.xml";

    public TestContext TestContext { get; set; }

    #region Tests

    [TestMethod]
    [Description("Tests that the project info file is created when the task is executed")]
    public void WriteProjectInfoFile_FileCreated()
    {
        // Arrange
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var projectGuid = Guid.NewGuid();

        var task = new WriteProjectInfoFile
        {
            FullProjectPath = "c:\\fullPath\\project.proj",
            ProjectLanguage = "cs",
            IsTest = true,
            IsExcluded = false,
            OutputFolder = testFolder,
            ProjectGuid = projectGuid.ToString("B"),
            ProjectName = "MyProject",
            Configuration = "conf-1",
            Platform = "plat-1",
            TargetFramework = "target-1",
        };
        task.ProjectLanguage = ProjectLanguages.CSharp;
        // No analysis results are supplied

        // Act
        var reloadedProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);

        // Addition assertions
        reloadedProjectInfo.Should().NotBeNull("Supplied ProjectInfo should not be null");
        reloadedProjectInfo.FullPath.Should().Be("c:\\fullPath\\project.proj", "Unexpected FullPath");
        reloadedProjectInfo.ProjectLanguage.Should().Be(ProjectLanguages.CSharp, "Unexpected ProjectLanguage");
        reloadedProjectInfo.ProjectType.Should().Be(ProjectType.Test, "Unexpected ProjectType");
        reloadedProjectInfo.ProjectGuid.Should().Be(projectGuid, "Unexpected ProjectGuid");
        reloadedProjectInfo.ProjectName.Should().Be("MyProject", "Unexpected ProjectName");
        reloadedProjectInfo.IsExcluded.Should().BeFalse("Unexpected IsExcluded");
        reloadedProjectInfo.Configuration.Should().Be("conf-1");
        reloadedProjectInfo.Platform.Should().Be("plat-1");
        reloadedProjectInfo.TargetFramework.Should().Be("target-1");
    }

    [TestMethod]
    [Description("Tests that analysis results are correctly handled")]
    public void WriteProjectInfoFile_AnalysisResults()
    {
        // Arrange
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var projectGuid = Guid.NewGuid();

        var task = new WriteProjectInfoFile
        {
            BuildEngine = new DummyBuildEngine(),
            FullProjectPath = "x:\\a.csproj",
            IsTest = false,
            OutputFolder = testFolder,
            ProjectGuid = projectGuid.ToString("B"),
            ProjectName = "MyProject",
            ProjectLanguage = "C#"
        };

        var resultInputs = new List<ITaskItem>
        {
            // Add invalid task items
            // Note: the TaskItem class won't allow the item spec or metadata values to be null,
            // so we aren't testing those
            CreateMetadataItem("itemSpec1", "abc", "def"), // Id field is missing
            CreateAnalysisResultTaskItem("\r", "should be ignored - whitespace"), // whitespace id
            CreateAnalysisResultTaskItem("should be ignored - whitespace", " "), // whitespace location

            // Add valid task items
            CreateAnalysisResultTaskItem("id1", "location1"),
            CreateAnalysisResultTaskItem("id2", "location2", "md1", "md1 value", "md2", "md2 value") // valid but with extra metadata
        };

        task.AnalysisResults = resultInputs.ToArray();

        // Act
        ProjectInfo createdProjectInfo;
        using (new AssertIgnoreScope()) // We've deliberately created task items with unexpected item names that will cause assertions
        {
            createdProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);
        }

        // Assert
        AssertAnalysisResultExists(createdProjectInfo, "id1", "location1");
        AssertAnalysisResultExists(createdProjectInfo, "id2", "location2");
        AssertExpectedAnalysisResultCount(2, createdProjectInfo);
    }

    [TestMethod]
    [Description("Tests that analysis settings are correctly handled")]
    public void WriteProjectInfoFile_AnalysisSettings()
    {
        // Arrange
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var projectGuid = Guid.NewGuid();

        var task = new WriteProjectInfoFile();

        var buildEngine = new DummyBuildEngine();
        task.BuildEngine = buildEngine;
        task.FullProjectPath = "x:\\analysisSettings.csproj";
        task.IsTest = false;
        task.OutputFolder = testFolder;
        task.ProjectGuid = projectGuid.ToString("B");
        task.ProjectName = "MyProject";
        task.ProjectLanguage = "C#";

        // Example of a valid setting:
        // <SonarQubeSetting Include="sonar.resharper.projectname">
        //    <Value>C:\zzz\reportlocation.xxx</Value>
        // </SonarQubeSetting>

        var settingsInputs = new List<ITaskItem>
        {
            // Add invalid task items
            // Note: the TaskItem class won't allow the item spec or metadata values to be null,
            // so we aren't testing those
            CreateMetadataItem("invalid.missing.value.metadata", "NotValueMetadata", "missing value 1"), // value field is missing
            CreateAnalysisSettingTaskItem(" ", "should be ignored - key is whitespace only"), // whitespace key
            CreateAnalysisSettingTaskItem("invalid spaces in key", "spaces.in.key"), // spaces in key
            CreateAnalysisSettingTaskItem(" invalid.key.has.leading.whitespace", "leading whitespace in key"),
            CreateAnalysisSettingTaskItem("invalid.key.has.trailing.whitespace ", "trailing whitespace in key"),
            CreateAnalysisSettingTaskItem(".invalid.non.alpha.first.character", "non alpha first character"),

            // Add valid task items
            CreateAnalysisSettingTaskItem("valid.setting.1", @"c:\dir1\dir2\file.txt"),
            CreateAnalysisSettingTaskItem("valid.value.is.whitespace.only", " "),
            CreateMetadataItem("valid.metadata.name.is.case.insensitive", BuildTaskConstants.SettingValueMetadataName.ToUpperInvariant(), "uppercase metadata name"), // metadata name is in the wrong case
            CreateAnalysisSettingTaskItem("valid.value.has.whitespace", "valid setting with whitespace"),
            CreateAnalysisSettingTaskItem("X", "single character key"),
            CreateAnalysisSettingTaskItem("Y...", "single character followed by periods"),
            CreateAnalysisSettingTaskItem("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.sonar.projectName", "guid followed by key")
        };

        task.AnalysisSettings = settingsInputs.ToArray();

        // Act
        ProjectInfo createdProjectInfo;
        createdProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);

        // Assert
        buildEngine.AssertSingleWarningExists("invalid.missing.value.metadata");
        // Can't easily check for the message complaining against the empty key
        buildEngine.AssertSingleWarningExists("invalid spaces in key");
        buildEngine.AssertSingleWarningExists(" invalid.key.has.leading.whitespace");
        buildEngine.AssertSingleWarningExists("invalid.key.has.trailing.whitespace ");
        buildEngine.AssertSingleWarningExists(".invalid.non.alpha.first.character");

        AssertAnalysisSettingExists(createdProjectInfo, "valid.setting.1", @"c:\dir1\dir2\file.txt");
        AssertAnalysisSettingExists(createdProjectInfo, "valid.value.is.whitespace.only", null);
        AssertAnalysisSettingExists(createdProjectInfo, "valid.value.has.whitespace", "valid setting with whitespace");
        AssertAnalysisSettingExists(createdProjectInfo, "valid.metadata.name.is.case.insensitive", "uppercase metadata name");
        AssertAnalysisSettingExists(createdProjectInfo, "X", "single character key");
        AssertAnalysisSettingExists(createdProjectInfo, "Y...", "single character followed by periods");
        AssertAnalysisSettingExists(createdProjectInfo, "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.sonar.projectName", "guid followed by key");

        AssertExpectedAnalysisSettingsCount(7, createdProjectInfo);
    }

    [TestMethod]
    [Description("Tests that the project info file is created if a project guid is not supplied")]
    [WorkItem(50)] // Regression test for Bug 50:MSBuild projects with missing ProjectGuids cause the build to fail
    public void WriteProjectInfoFile_MissingProjectGuid()
    {
        // Arrange
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var task = new WriteProjectInfoFile
        {
            FullProjectPath = "c:\\fullPath\\project.proj",
            IsTest = true,
            OutputFolder = testFolder,
            ProjectName = "ProjectWithoutProjectGuid",
            ProjectLanguage = "C#"
        };
        // No analysis results are supplied

        // Act
        var engine = new DummyBuildEngine();
        task.BuildEngine = engine;
        var success = task.Execute();

        // Assert
        success.Should().BeTrue("Not expecting the task to fail as this would fail the build");
        engine.AssertNoErrors();
        engine.AssertNoWarnings();

        var projectInfoFilePath = Path.Combine(testFolder, ExpectedProjectInfoFileName);
        File.Exists(projectInfoFilePath).Should().BeTrue("Expecting the project info file to have been created");
    }

    [TestMethod]
    [Description("Tests that the project info file is created using solution Guid if a project guid is not supplied")]
    public void WriteProjectInfoFile_UseSolutionProjectGuid()
    {
        // Arrange
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var projectGuid = Guid.NewGuid();

        var task = new WriteProjectInfoFile
        {
            FullProjectPath = "c:\\fullPath\\project.proj",
            SolutionConfigurationContents = $@"<SolutionConfiguration>
                <ProjectConfiguration Project=""{{FOO}}"" AbsolutePath=""c:\fullPath\foo.proj"" BuildProjectInSolution=""True""> Debug | AnyCPU </ProjectConfiguration>
                <ProjectConfiguration Project=""{{{projectGuid}}}"" AbsolutePath=""c:\fullPath\project.proj"" BuildProjectInSolution=""True""> Debug | AnyCPU </ProjectConfiguration>
               </SolutionConfiguration >",
            IsTest = true,
            OutputFolder = testFolder,
            ProjectName = "ProjectWithoutProjectGuid",
            ProjectLanguage = "C#"
        };

        // Act
        var reloadedProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);

        // Addition assertions
        reloadedProjectInfo.Should().NotBeNull("Supplied ProjectInfo should not be null");
        reloadedProjectInfo.FullPath.Should().Be("c:\\fullPath\\project.proj", "Unexpected FullPath");
        reloadedProjectInfo.ProjectLanguage.Should().Be(ProjectLanguages.CSharp, "Unexpected ProjectLanguage");
        reloadedProjectInfo.ProjectType.Should().Be(ProjectType.Test, "Unexpected ProjectType");
        reloadedProjectInfo.ProjectGuid.Should().Be(projectGuid, "Unexpected ProjectGuid");
        reloadedProjectInfo.ProjectName.Should().Be("ProjectWithoutProjectGuid", "Unexpected ProjectName");
        reloadedProjectInfo.IsExcluded.Should().BeFalse("Unexpected IsExcluded");
        reloadedProjectInfo.Configuration.Should().BeNull();
        reloadedProjectInfo.Platform.Should().BeNull();
        reloadedProjectInfo.TargetFramework.Should().BeNull();
    }

    [TestMethod]
    [Description("Tests that project info files are created for unrecognized languages")]
    public void WriteProjectInfoFile_UnrecognisedLanguages()
    {
        // Arrange
        var task = new WriteProjectInfoFile
        {
            FullProjectPath = "c:\\fullPath\\project.proj",
            IsTest = true,
            ProjectName = "UnrecognisedLanguageProject",
            ProjectGuid = Guid.NewGuid().ToString("B"),

            // 1. Null language
            ProjectLanguage = null,
            OutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "null.language")
        };

        var actual = ExecuteAndCheckSucceeds(task, task.OutputFolder);
        actual.ProjectLanguage.Should().BeNull("Expecting the language to be null");

        // 2. Unrecognized language
        task.ProjectLanguage = "unrecognized language";
        task.OutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "unrecog.language");

        actual = ExecuteAndCheckSucceeds(task, task.OutputFolder);
        actual.ProjectLanguage.Should().Be("unrecognized language", "Unexpected value for project language");
    }

    [TestMethod]
    public void WriteProjectInfoFile_Execute_WhenCodePageAndWhateverProjectType_ExpectsGivenEncoding()
    {
        // Arrange
        var expectedEncoding = Encoding.GetEncoding(28591); // Any non-default encoding.
        var encodingProvider = new TestEncodingProvider(
            encoding =>
            {
                if (encoding == expectedEncoding.CodePage)
                {
                    return expectedEncoding;
                }
                else
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                }
            },
            x =>
            {
                Assert.Fail("should not have been called");
                throw new InvalidOperationException();
            });

        // Act
        var actual = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, expectedEncoding.CodePage, "whatever", "foo");

        // Assert
        expectedEncoding.WebName.Should().Be(actual.Encoding, "unexpected encoding");
    }

    [TestMethod]
    public void WriteProjectInfoFile_Execute_WhenNotCSharpWithNullCodePage_ExpectsNull()
    {
        // Arrange
        decimal? codePage = null;
        var encodingProvider = new TestEncodingProvider(
            x =>
            {
                Assert.Fail("should not have been called");
                throw new InvalidOperationException();
            },
            x =>
            {
                Assert.Fail("should not have been called");
                throw new InvalidOperationException();
            });

        // Act
        var actual = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "whatever", "foo9");

        // Assert
        actual.Encoding.Should().BeNull("unexpected encoding");
    }

    [TestMethod]
    public void WriteProjectInfoFile_Execute_WhenNotCSharpVBNetWithInferiorToZeroCodePage_ExpectsNull()
    {
        // Arrange
        const decimal codePage = -1;
        var encodingProvider = new TestEncodingProvider(
            x =>
            {
                Assert.Fail("should not have been called");
                throw new InvalidOperationException();
            },
            x =>
            {
                Assert.Fail("should not have been called");
                throw new InvalidOperationException();
            });

        // Act
        var actual = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "whatever", "foo10");

        // Assert
        actual.Encoding.Should().BeNull("unexpected encoding");
    }

    [TestMethod]
    public void WriteProjectInfoFile_Execute_WhenNotCSharpVBNetWithBiggerThanLongCodePage_ExpectsNull()
    {
        // Arrange
        var encodingProvider = new TestEncodingProvider(
            x =>
            {
                Assert.Fail("should not have been called");
                throw new InvalidOperationException();
            },
            x =>
            {
                Assert.Fail("should not have been called");
                throw new InvalidOperationException();
            });

        var codePage = (decimal)long.MaxValue + 1;

        // Act
        var actual = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "whatever", "foo11");

        // Assert
        actual.Encoding.Should().BeNull("unexpected encoding");
    }

    private ProjectInfo WriteProjectInfoFile_ExecuteAndReturn(IEncodingProvider encodingProvider, decimal? codePage, string projectLanguage, string folderName)
    {
        // Arrange
        var task = new WriteProjectInfoFile(encodingProvider)
        {
            FullProjectPath = "c:\\fullPath\\project.proj",
            IsTest = true,
            ProjectName = "Foo",
            ProjectGuid = Guid.NewGuid().ToString("B"),
            ProjectLanguage = projectLanguage,
            CodePage = codePage.ToString(),
            OutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, folderName)
        };

        // Act
        return ExecuteAndCheckSucceeds(task, task.OutputFolder);
    }

    [TestMethod]
    public void GetProjectGuid_WhenProjectGuidAndSolutionConfigurationContentsAreNull_ReturnsNull() =>
        AssertProjectGuidIsRandomlyGenerated(null, null, @"C:\NetCorePrj\MyNetCoreProject.csproj");

    [TestMethod]
    public void GetProjectGuid_WhenProjectGuidAndSolutionConfigurationContentsAreEmptyString_ReturnsRandomGuid() =>
        AssertProjectGuidIsRandomlyGenerated(string.Empty, string.Empty, @"C:\NetCorePrj\MyNetCoreProject.csproj");

    [TestMethod]
    public void GetProjectGuid_WhenProjectGuidNullAndSolutionConfigurationContentsEmptyString_ReturnsNull() =>
        AssertProjectGuidIsRandomlyGenerated(null, string.Empty, @"C:\NetCorePrj\MyNetCoreProject.csproj");

    [TestMethod]
    public void GetProjectGuid_WhenProjectGuidEmptyStringAndSolutionConfigurationContentsNull_ReturnsNull() =>
        AssertProjectGuidIsRandomlyGenerated(string.Empty, null, @"C:\NetCorePrj\MyNetCoreProject.csproj");

    private void AssertProjectGuidIsRandomlyGenerated(string projectGuid, string solutionConfigurationContents, string fullProjectPath)
    {
        // Arrange
        var sut = new WriteProjectInfoFile
        {
            FullProjectPath = fullProjectPath,
            ProjectGuid = projectGuid,
            SolutionConfigurationContents = solutionConfigurationContents
        };

        var engine = new DummyBuildEngine();
        sut.BuildEngine = engine;

        // Act
        var actual = sut.GetProjectGuid();

        // Assert
        actual.Should().NotBeNullOrEmpty();
        actual.Should().NotBe(Guid.Empty.ToString());
    }

    [TestMethod]
    public void GetProjectGuid_WhenProjectGuidHasValue_ReturnsProjectGuid()
    {
        // Arrange
        var expectedGuid = Guid.Empty.ToString();
        var sut = new WriteProjectInfoFile { ProjectGuid = expectedGuid, SolutionConfigurationContents = null };

        // Act
        var actual = sut.GetProjectGuid();

        // Assert
        actual.Should().Be(expectedGuid);
    }

    [TestMethod]
    public void GetProjectGuid_WhenSolutionConfigurationContentsHasValueAndProjectFound_ReturnsProjectGuidInSolution()
    {
        // Same paths
        AssertThatSolutionProjectGuidIsExpected(@"C:\NetStdApp\NetStdApp.csproj", @"C:\NetStdApp\NetStdApp.csproj");

        // Relative path
        AssertThatSolutionProjectGuidIsExpected(@"C:\NetStdApp\NetStdApp.csproj", @"C:\Foo\..\NetStdApp\NetStdApp.csproj");

        // Different case
        AssertThatSolutionProjectGuidIsExpected(@"C:\NetStdApp\NetStdApp.csproj", @"C:\NETSTDAPP\NetStdApp.csproj");
    }

    private void AssertThatSolutionProjectGuidIsExpected(string fullProjectPath, string solutionProjectPath)
    {
        // Arrange
        var expectedGuid = "{10F2915F-4AB3-4269-BC2B-4F72C6DE87C8}";
        var sut = new WriteProjectInfoFile
        {
            ProjectGuid = null,
            FullProjectPath = fullProjectPath,
            SolutionConfigurationContents = $@"<SolutionConfiguration>
  <ProjectConfiguration Project=""{expectedGuid}"" AbsolutePath=""{solutionProjectPath}"" BuildProjectInSolution=""True"">Debug|AnyCPU</ProjectConfiguration>
</SolutionConfiguration>"
        };

        // Act
        var actual = sut.GetProjectGuid();

        // Assert
        actual.Should().Be(expectedGuid);
    }

    [TestMethod]
    public void GetProjectGuid_WhenSolutionConfigurationContentsHasNoProjectAttribute_ReturnsNull()
    {
        // Arrange
        var sut = new WriteProjectInfoFile
        {
            ProjectGuid = null,
            FullProjectPath = @"C:\NetStdApp\NetStdApp.csproj",
            SolutionConfigurationContents = @"<SolutionConfiguration>
  <ProjectConfiguration AbsolutePath=""C:\NetStdApp\NetStdApp.csproj"" BuildProjectInSolution=""True"">Debug|AnyCPU</ProjectConfiguration>
</SolutionConfiguration>"
        };

        // Act
        var actual = sut.GetProjectGuid();

        // Assert
        actual.Should().BeNull();
    }

    [TestMethod]
    public void GetProjectGuid_WhenSolutionConfigurationContentsHasNoAbsolutePathAttribute_ReturnsNull()
    {
        // Arrange
        var sut = new WriteProjectInfoFile
        {
            ProjectGuid = null,
            FullProjectPath = @"C:\NetStdApp\NetStdApp.csproj",
            SolutionConfigurationContents = @"<SolutionConfiguration>
  <ProjectConfiguration Project=""{10F2915F-4AB3-4269-BC2B-4F72C6DE87C8}"" BuildProjectInSolution=""True"">Debug|AnyCPU</ProjectConfiguration>
</SolutionConfiguration>"
        };

        // Act
        var actual = sut.GetProjectGuid();

        // Assert
        actual.Should().BeNull();
    }

    [TestMethod]
    public void GetProjectGuid_WhenSolutionConfigurationContentsHasMultipleMatch_ReturnsFirstGuid()
    {
        // Arrange
        var expectedGuid = "{10F2915F-4AB3-4269-BC2B-4F72C6DE87C8}";
        var sut = new WriteProjectInfoFile
        {
            ProjectGuid = null,
            FullProjectPath = @"C:\NetStdApp\NetStdApp.csproj",
            SolutionConfigurationContents = $@"<SolutionConfiguration>
<ProjectConfiguration Project=""{expectedGuid}"" AbsolutePath=""C:\NetStdApp\NetStdApp.csproj"" BuildProjectInSolution=""True"">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project=""{Guid.NewGuid()}"" AbsolutePath=""C:\NetStdApp\NetStdApp.csproj"" BuildProjectInSolution=""True"">Debug|AnyCPU</ProjectConfiguration>
</SolutionConfiguration>"
        };

        // Act
        var actual = sut.GetProjectGuid();

        // Assert
        actual.Should().Be(expectedGuid);
    }

    [TestMethod]
    public void GetProjectGuid_InvalidPath_DoesNotThrow()
    {
        var expectedGuid = "{10F2915F-4AB3-4269-BC2B-4F72C6DE87C8}";
        var notValidPath = @"D:\a\1\s\src\https://someUrl.me:1623";
        var fullProjectPath = @"C:\NetStdApp\NetStdApp.csproj";
        var sut = new WriteProjectInfoFile
        {
            ProjectGuid = null,
            FullProjectPath = fullProjectPath,
            SolutionConfigurationContents = $@"<SolutionConfiguration>
<ProjectConfiguration Project=""{Guid.NewGuid()}"" AbsolutePath=""{notValidPath}"" BuildProjectInSolution=""True"">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project=""{expectedGuid}"" AbsolutePath=""{fullProjectPath}"" BuildProjectInSolution=""True"">Debug|AnyCPU</ProjectConfiguration>
</SolutionConfiguration>"
        };

        // Act
        var actual = sut.GetProjectGuid();

        // Assert
        actual.Should().Be(expectedGuid);
    }

    [TestMethod]
    public void GetProjectGuid_TwoProjectsWithSamePath_FirstProjectIsReturned()
    {
        var expectedGuid = "{10F2915F-4AB3-4269-BC2B-4F72C6DE87C8}";
        var fullProjectPath = @"C:\NetStdApp\NetStdApp.csproj";
        var sut = new WriteProjectInfoFile
        {
            ProjectGuid = null,
            FullProjectPath = fullProjectPath,
            SolutionConfigurationContents = $@"<SolutionConfiguration>
<ProjectConfiguration Project=""{expectedGuid}"" AbsolutePath=""{fullProjectPath}"" BuildProjectInSolution=""True"">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project=""{Guid.NewGuid()}"" AbsolutePath=""{fullProjectPath}"" BuildProjectInSolution=""True"">Debug|AnyCPU</ProjectConfiguration>
</SolutionConfiguration>"
        };

        // Act
        var actual = sut.GetProjectGuid();

        // Assert
        actual.Should().Be(expectedGuid);
    }

    #endregion Tests

    #region Helper methods

    private static ITaskItem CreateAnalysisResultTaskItem(string id, string location, params string[] idAndValuePairs)
    {
        var item = CreateMetadataItem(location, idAndValuePairs);
        item.SetMetadata(BuildTaskConstants.ResultMetadataIdProperty, id);
        return item;
    }

    private static ITaskItem CreateAnalysisSettingTaskItem(string key, string value)
    {
        ITaskItem item = new TaskItem(key);
        item.SetMetadata(BuildTaskConstants.SettingValueMetadataName, value);
        return item;
    }

    private static ITaskItem CreateMetadataItem(string itemSpec, params string[] idAndValuePairs)
    {
        ITaskItem item = new TaskItem(itemSpec);
        Math.DivRem(idAndValuePairs.Length, 2, out int remainder);
        remainder.Should().Be(0, "Test setup error: the supplied list should contain id-location pairs");

        for (var index = 0; index < idAndValuePairs.Length; index += 2)
        {
            item.SetMetadata(idAndValuePairs[index], idAndValuePairs[index + 1]);
        }
        return item;
    }

    #endregion Helper methods

    #region Checks

    private ProjectInfo ExecuteAndCheckSucceeds(Task task, string testFolder)
    {
        var expectedOutputFile = Path.Combine(testFolder, ExpectedProjectInfoFileName);
        File.Exists(expectedOutputFile).Should().BeFalse("Test error: output file should not exist before the task is executed");

        var result = task.Execute();

        result.Should().BeTrue("Expecting the task execution to succeed");
        File.Exists(expectedOutputFile).Should().BeTrue("Expected output file was not created by the task. Expected: {0}", expectedOutputFile);
        TestContext.AddResultFile(expectedOutputFile);

        var reloadedProjectInfo = ProjectInfo.Load(expectedOutputFile);
        reloadedProjectInfo.Should().NotBeNull("Not expecting the reloaded project info file to be null");
        return reloadedProjectInfo;
    }

    private static void AssertAnalysisResultExists(ProjectInfo actual, string expectedId, string expectedLocation)
    {
        actual.Should().NotBeNull("Supplied project info should not be null");
        actual.AnalysisResults.Should().NotBeNull("AnalysisResults should not be null");

        var result = actual.AnalysisResults.FirstOrDefault(ar => expectedId.Equals(ar.Id, StringComparison.InvariantCulture));
        result.Should().NotBeNull("AnalysisResult with the expected id does not exist. Id: {0}", expectedId);

        result.Location.Should().Be(expectedLocation, "Analysis result does not have the expected location");
    }

    private static void AssertExpectedAnalysisResultCount(int count, ProjectInfo actual)
    {
        actual.Should().NotBeNull("Supplied project info should not be null");
        actual.AnalysisResults.Should().NotBeNull("AnalysisResults should not be null");

        actual.AnalysisResults.Should().HaveCount(count, "Unexpected number of AnalysisResult items");
    }

    private static void AssertAnalysisSettingExists(ProjectInfo actual, string expectedId, string expectedValue)
    {
        actual.Should().NotBeNull("Supplied project info should not be null");
        actual.AnalysisSettings.Should().NotBeNull("AnalysisSettings should not be null");

        var setting = actual.AnalysisSettings.FirstOrDefault(ar => expectedId.Equals(ar.Id, StringComparison.InvariantCulture));
        setting.Should().NotBeNull("AnalysisSetting with the expected id does not exist. Id: {0}", expectedId);

        setting.Value.Should().Be(expectedValue, "Setting does not have the expected value");
    }

    private static void AssertExpectedAnalysisSettingsCount(int count, ProjectInfo actual)
    {
        actual.Should().NotBeNull("Supplied project info should not be null");
        actual.AnalysisSettings.Should().NotBeNull("AnalysisSettings should not be null");

        actual.AnalysisSettings.Should().HaveCount(count, "Unexpected number of AnalysisSettings items");
    }

    #endregion Checks
}
