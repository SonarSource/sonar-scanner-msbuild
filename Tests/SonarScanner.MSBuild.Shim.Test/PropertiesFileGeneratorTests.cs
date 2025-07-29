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
using System.Text.RegularExpressions;
using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class PropertiesFileGeneratorTests
{
    private const string TestSonarqubeOutputDir = @"e:\.sonarqube\out";

    private const string ProjectBaseDirInfoMessage =
        "Starting with Scanner for .NET v8 the way the `sonar.projectBaseDir` property is automatically detected has changed "
        + "and this has an impact on the files that are analyzed and other properties that are resolved relative to it like `sonar.exclusions` and `sonar.test.exclusions`. "
        + "If you would like to customize the behavior, please set the `sonar.projectBaseDir` property to point to a directory that contains all the source code you want to analyze. "
        + "The path may be relative (to the directory from which the analysis was started) or absolute.";

    private readonly TestLogger logger = new();

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void PropertiesFileGenerator_WhenConfigIsNull_Throws() =>
        ((Action)(() => new PropertiesFileGenerator(null, new TestLogger()))).Should()
            .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("analysisConfig");

    [TestMethod]
    public void PropertiesFileGenerator_FirstConstructor_WhenLoggerIsNull_Throws() =>
        ((Action)(() => new PropertiesFileGenerator(new AnalysisConfig(), null, new RoslynV1SarifFixer(new TestLogger()), new RuntimeInformationWrapper(), null))).Should()
            .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");

    [TestMethod]
    public void PropertiesFileGenerator_SecondConstructor_WhenLoggerIsNull_Throws() =>
        // the RoslynV1SarifFixer will throw
        ((Action)(() => new PropertiesFileGenerator(new AnalysisConfig(), null))).Should()
        .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");

    [TestMethod]
    public void PropertiesFileGenerator_WhenFixerIsNull_Throws() =>
        ((Action)(() => new PropertiesFileGenerator(new AnalysisConfig(), new TestLogger(), null, new RuntimeInformationWrapper(), null))).Should()
            .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("fixer");

    [TestMethod]
    public void PropertiesFileGenerator_WhenRuntimeInformationWrapperIsNull_Throws()
    {
        var logger = new TestLogger();
        ((Action)(() => new PropertiesFileGenerator(new AnalysisConfig(), logger, new RoslynV1SarifFixer(logger), null, null))).Should()
            .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("runtimeInformationWrapper");
    }

    [TestMethod]
    public void PropertiesFileGenerator_WhenAdditionalFileServiceIsNull_Throws()
    {
        var logger = new TestLogger();
        ((Action)(() => new PropertiesFileGenerator(new AnalysisConfig(), logger, new RoslynV1SarifFixer(logger), new RuntimeInformationWrapper(), null))).Should()
            .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("additionalFilesService");
    }

    [TestMethod]
    public void GenerateFile_NoProjectInfoFiles()
    {
        // Properties file should not be generated if there are no project info files.

        // Arrange - two sub-directories, neither containing a ProjectInfo.xml
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var subDir1 = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "dir1");
        var subDir2 = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "dir2");

        TestUtils.CreateEmptyFile(subDir1, "file1.txt");
        TestUtils.CreateEmptyFile(subDir2, "file2.txt");
        var config = new AnalysisConfig() { SonarOutputDir = testDir, SonarQubeHostUrl = "http://sonarqube.com" };

        // Act
        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        // Assert
        AssertFailedToCreatePropertiesFiles(result, logger);
        AssertExpectedProjectCount(0, result);
    }

    [TestMethod]
    public void GenerateFile_ValidFiles()
    {
        // Only non-excluded projects with files to analyze should be marked as valid
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var withoutFilesDir = Path.Combine(testDir, "withoutFiles");
        Directory.CreateDirectory(withoutFilesDir);

        TestUtils.CreateProjectInfoInSubDir(testDir, "withoutFiles", null, Guid.NewGuid(), ProjectType.Product, false, Path.Combine(withoutFilesDir, "withoutFiles.proj"), "UTF-8"); // not excluded
        TestUtils.CreateEmptyFile(withoutFilesDir, "withoutFiles.proj");
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles2", testDir);

        var config = CreateValidConfig(testDir);

        // Act
        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        // Assert
        AssertExpectedStatus("withoutFiles", ProjectInfoValidity.NoFilesToAnalyze, result);
        AssertExpectedStatus("withFiles1", ProjectInfoValidity.Valid, result);
        AssertExpectedStatus("withFiles2", ProjectInfoValidity.Valid, result);
        AssertExpectedProjectCount(3, result);

        // One valid project info file -> file created
        AssertPropertiesFilesCreated(result, logger);
    }

    [TestMethod]
    public void GenerateFile_Csproj_DoesNotExist()
    {
        var projectName = "withoutCsproj";
        var rootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "projects");
        var projectDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, Path.Combine("projects", projectName));
        TestUtils.CreateProjectInfoInSubDir(
            rootDir,
            projectName,
            null,
            Guid.NewGuid(),
            ProjectType.Product,
            false,
            Path.Combine(projectDir, "NotExisting.proj"),
            "UTF-8");
        var config = CreateValidConfig(rootDir);

        var result = CreateSut(config).GenerateFile();

        AssertExpectedStatus(projectName, ProjectInfoValidity.ProjectNotFound, result);
        AssertExpectedProjectCount(1, result);
    }

    // Case sensitive test is only relevant for Windows OS, as it is case insensitive by default
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void GenerateFile_Duplicate_SameGuid_DifferentCase_ShouldNotIgnoreCase()
    {
        var projectName1 = "withFiles1";
        var projectName2 = "withFiles2";

        var testRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "projects");
        var project1Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, Path.Combine("projects", projectName1));

        // Casing should not be ignored on non-windows OS
        var runtimeInformation = Substitute.For<IRuntimeInformationWrapper>();
        runtimeInformation.IsOS(OSPlatform.Windows).Returns(false);

        var guid = Guid.NewGuid();
        var contentProjectInfo1 = TestUtils.CreateProjectInfoInSubDir(testRootDir, projectName1, null, guid, ProjectType.Product, false, Path.Combine(project1Dir, "withoutfile.proj"), "UTF-8");
        TestUtils.CreateProjectInfoInSubDir(testRootDir, projectName2, null, guid, ProjectType.Product, false, Path.Combine(project1Dir, "withoutFile.proj"), "UTF-8"); // not excluded

        // Create content / managed files if required
        var contentFile1 = TestUtils.CreateEmptyFile(project1Dir, "contentFile1.txt");
        var contentFileList1 = TestUtils.CreateFile(project1Dir, "contentList.txt", contentFile1);

        TestUtils.AddAnalysisResult(contentProjectInfo1, AnalysisType.FilesToAnalyze, contentFileList1);

        var config = CreateValidConfig(testRootDir);

        // Act
        var result = CreateSut(config, runtimeInformationWrapper: runtimeInformation).GenerateFile();

        // Assert
        AssertExpectedStatus(projectName1, ProjectInfoValidity.DuplicateGuid, result);
        AssertExpectedProjectCount(1, result);

        logger.Warnings.Should().HaveCount(2).And.BeEquivalentTo(
            $"Duplicate ProjectGuid: \"{guid}\". The project will not be analyzed. Project file: \"{Path.Combine(project1Dir, "withoutfile.proj")}\"",
            $"Duplicate ProjectGuid: \"{guid}\". The project will not be analyzed. Project file: \"{Path.Combine(project1Dir, "withoutFile.proj")}\"");
    }

    // Case sensitive test is only relevant for Windows OS, as it is case insensitive by default
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void GenerateFile_Duplicate_SameGuid_DifferentCase_ShouldIgnoreCase()
    {
        // Arrange
        var projectName1 = "withFiles1";
        var projectName2 = "withFiles2";

        var testRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "projects");
        var project1Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, Path.Combine("projects", projectName1));

        // Casing can be ignored on windows OS
        var runtimeInformation = Substitute.For<IRuntimeInformationWrapper>();
        runtimeInformation.IsOS(OSPlatform.Windows).Returns(true);

        var guid = Guid.NewGuid();
        var contentProjectInfo1 = TestUtils.CreateProjectInfoInSubDir(testRootDir, projectName1, null, guid, ProjectType.Product, false, project1Dir + "\\withoutfile.proj", "UTF-8");
        TestUtils.CreateEmptyFile(project1Dir, "withoutfile.proj");
        TestUtils.CreateProjectInfoInSubDir(testRootDir, projectName2, null, guid, ProjectType.Product, false, project1Dir + "\\withoutFile.proj", "UTF-8"); // not excluded

        // Create content / managed files if required
        var contentFile1 = TestUtils.CreateEmptyFile(project1Dir, "contentFile1.txt");
        var contentFileList1 = TestUtils.CreateFile(project1Dir, "contentList.txt", contentFile1);

        TestUtils.AddAnalysisResult(contentProjectInfo1, AnalysisType.FilesToAnalyze, contentFileList1);

        var config = CreateValidConfig(testRootDir);

        // Act
        var result = CreateSut(config, runtimeInformationWrapper: runtimeInformation).GenerateFile();

        // Assert
        AssertExpectedStatus(projectName1, ProjectInfoValidity.Valid, result);
        AssertExpectedProjectCount(1, result);
    }

    [TestMethod]
    public void GenerateFile_ValidFiles_SourceEncoding_Provided()
    {
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);

        var config = CreateValidConfig(testDir);
        config.LocalSettings = new AnalysisProperties { new(SonarProperties.SourceEncoding, "test-encoding-here") };

        // Act
        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        // Assert
        var settingsFileContent = File.ReadAllText(result.FullPropertiesFilePath);
        settingsFileContent.Should().Contain("sonar.sourceEncoding=test-encoding-here", "Command line parameter 'sonar.sourceEncoding' is ignored.");
        logger.DebugMessages.Should().Contain(string.Format(Resources.DEBUG_DumpSonarProjectProperties, settingsFileContent));
    }

    [TestMethod]
    public void GenerateFile_TFS_Coverage_TrxAreWritten()
    {
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);

        var config = CreateValidConfig(testDir);
        config.LocalSettings = new AnalysisProperties
        {
            new(SonarProperties.VsCoverageXmlReportsPaths, "coverage-path"),
            new(SonarProperties.VsTestReportsPaths, "trx-path"),
        };

        // Act
        var result = CreateSut(config).GenerateFile();

        // Assert
        var settingsFileContent = File.ReadAllText(result.FullPropertiesFilePath);
        settingsFileContent.Should().Contain("sonar.cs.vscoveragexml.reportsPaths=coverage-path");
        settingsFileContent.Should().Contain("sonar.cs.vstest.reportsPaths=trx-path");
        logger.DebugMessages.Should().Contain(string.Format(Resources.DEBUG_DumpSonarProjectProperties, settingsFileContent));
    }

    [TestMethod]
    public void GenerateFile_SensitiveParamsNotLogged()
    {
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);

        var config = CreateValidConfig(testDir);
        config.LocalSettings = new AnalysisProperties
        {
            new(SonarProperties.ClientCertPath, "Client cert path"),           // should be logged as it is not sensitive
            new(SonarProperties.ClientCertPassword, "Client cert password")    // should not be logged as it is sensitive
        };

        // Act
        CreateSut(config).GenerateFile();

        // Assert
        logger.DebugMessages.Any(x => x.Contains("Client cert path")).Should().BeTrue();
        logger.DebugMessages.Any(x => x.Contains("Client cert password")).Should().BeFalse();
    }

    [TestMethod]
    public void GenerateFile_ValidFiles_WithAlreadyValidSarif()
    {
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        // SARIF file path
        var testSarifPath = Path.Combine(testDir, "testSarif.json");

        // Create SARIF report path property and add it to the project info
        var projectSettings = new AnalysisProperties { new(PropertiesFileGenerator.ReportFilePathsCSharpPropertyKey, testSarifPath) };
        var projectGuid = Guid.NewGuid();
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", ProjectLanguages.CSharp, testDir, projectGuid, true, projectSettings);

        var config = CreateValidConfig(testDir);

        // Mock SARIF fixer simulates already valid sarif
        var mockSarifFixer = new MockRoslynV1SarifFixer(testSarifPath);
        var mockReturnPath = mockSarifFixer.ReturnVal;

        // Act
        var result = CreateSut(config, mockSarifFixer).GenerateFile();

        // Assert
        mockSarifFixer.CallCount.Should().Be(1);

        // Already valid SARIF -> no change in file -> unchanged property
        var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        provider.AssertSettingExists(projectGuid.ToString().ToUpper() + "." + PropertiesFileGenerator.ReportFilePathsCSharpPropertyKey, mockReturnPath);
    }

    [DataTestMethod]
    [DataRow(ProjectLanguages.CSharp, PropertiesFileGenerator.ReportFilePathsCSharpPropertyKey, RoslynV1SarifFixer.CSharpLanguage)]
    [DataRow(ProjectLanguages.VisualBasic, PropertiesFileGenerator.ReportFilePathsVbNetPropertyKey, RoslynV1SarifFixer.VBNetLanguage)]
    public void GenerateFile_ValidFiles_WithFixableSarif(string projectLanguage, string propertyKey, string expectedSarifLanguage)
    {
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        // SARIF file path
        var testSarifPath = Path.Combine(testDir, "testSarif.json");

        // Create SARIF report path property and add it to the project info
        var projectSettings = new AnalysisProperties { new(propertyKey, testSarifPath) };
        var projectGuid = Guid.NewGuid();
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", projectLanguage, testDir, projectGuid, true, projectSettings);

        var config = CreateValidConfig(testDir);

        // Mock SARIF fixer simulates fixable SARIF with fixed name
        var returnPathFileName = Path.GetFileNameWithoutExtension(testSarifPath) + RoslynV1SarifFixer.FixedFileSuffix + Path.GetExtension(testSarifPath);
        var sarifFixer = new MockRoslynV1SarifFixer(Path.Combine(testDir, returnPathFileName));

        // Act
        var result = CreateSut(config, sarifFixer).GenerateFile();

        // Assert
        sarifFixer.CallCount.Should().Be(1);
        sarifFixer.LastLanguage.Should().Be(expectedSarifLanguage);

        // Fixable SARIF -> new file saved -> changed property
        var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        provider.AssertSettingExists(projectGuid.ToString().ToUpper() + "." + propertyKey, sarifFixer.ReturnVal);
    }

    [TestMethod]
    public void GenerateFile_WithMultipleAnalyzerAndRoslynOutputPaths_ShouldBeSupported()
    {
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var config = CreateValidConfig(testDir);

        var testSarifPath1 = Path.Combine(testDir, "testSarif1.json");
        var testSarifPath2 = Path.Combine(testDir, "testSarif2.json");
        var testSarifPath3 = Path.Combine(testDir, "testSarif3.json");

        // Mock SARIF fixer simulates fixable SARIF with fixed name
        var mockSarifFixer = new MockRoslynV1SarifFixer(null);
        var projectSettings = new AnalysisProperties
        {
            new(PropertiesFileGenerator.ReportFilePathsVbNetPropertyKey, string.Join(PropertiesFileGenerator.RoslynReportPathsDelimiter.ToString(), testSarifPath1, testSarifPath2, testSarifPath3))
        };

        var projectGuid = Guid.NewGuid();
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", ProjectLanguages.VisualBasic, testDir, projectGuid, true, projectSettings);

        var result = CreateSut(config, mockSarifFixer).GenerateFile();
        var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        provider.AssertSettingExists(
            $"{projectGuid.ToString().ToUpper()}.{PropertiesFileGenerator.ReportFilePathsVbNetPropertyKey}",
            $"{testSarifPath1}.fixed.mock.json,{testSarifPath2}.fixed.mock.json,{testSarifPath3}.fixed.mock.json");
    }

    [TestMethod]
    public void GenerateFile_ValidFiles_WithUnfixableSarif()
    {
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        // SARIF file path
        var testSarifPath = Path.Combine(testDir, "testSarif.json");

        // Create SARIF report path property and add it to the project info
        var projectSettings = new AnalysisProperties { new(PropertiesFileGenerator.ReportFilePathsCSharpPropertyKey, testSarifPath) };
        var projectGuid = Guid.NewGuid();
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", null, testDir, projectGuid, true, projectSettings);

        var config = CreateValidConfig(testDir);

        // Mock SARIF fixer simulated unfixable/absent file
        var mockSarifFixer = new MockRoslynV1SarifFixer(null);

        // Act
        var result = CreateSut(config, mockSarifFixer).GenerateFile();

        // Assert
        mockSarifFixer.CallCount.Should().Be(1);

        // One valid project info file -> file created
        AssertPropertiesFilesCreated(result, logger);

        // Unfixable SARIF -> cannot fix -> report file property removed
        var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        provider.AssertSettingDoesNotExist(projectGuid.ToString().ToUpper() + "." + PropertiesFileGenerator.ReportFilePathsCSharpPropertyKey);
    }

    [TestMethod]
    public void GenerateFile_FilesOutOfProjectRootDir_TheyAreNotAnalyzedAndCorrectWarningsAreLogged()
    {
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var projectDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
        var projectPath = Path.Combine(projectDir, "project.proj");
        var projectInfo = TestUtils.CreateProjectInfoInSubDir(testDir, "project", null, Guid.NewGuid(), ProjectType.Product, false, projectPath, "UTF-8");
        TestUtils.CreateEmptyFile(projectDir, "project.proj");

        string[] filesOutsideProjectPath = ["dllFile.dll", "exeFile.exe", "txtFile.txt", "foo.cs", "foo.DLL", "bar.EXE"];
        var filesToBeAnalyzedPaths = new List<string>();
        foreach (var fileName in filesOutsideProjectPath)
        {
            filesToBeAnalyzedPaths.Add(TestUtils.CreateEmptyFile(TestContext.TestDir, fileName));
        }

        // To add the files above, to the list of files that are to be analyzed, you need to add their paths to
        // the "contentList.txt" which is placed inside the projectDir folder.
        var contentFileListPath = TestUtils.CreateFile(projectDir, "contentList.txt", string.Join(Environment.NewLine, filesToBeAnalyzedPaths));
        // Add the file path of "contentList.txt" to the projectInfo.xml
        TestUtils.AddAnalysisResult(projectInfo, AnalysisType.FilesToAnalyze, contentFileListPath);
        var config = CreateValidConfig(testDir);

        // Act
        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        // Assert
        AssertExpectedProjectCount(1, result);
        // The project has no files in its root dir and the rest of the files are outside of the root, thus ignored and not analyzed.
        AssertExpectedStatus("project", ProjectInfoValidity.NoFilesToAnalyze, result);
        logger.AssertWarningsLogged(2);
        logger.AssertSingleWarningExists($"File '{Path.Combine(TestContext.TestDir, "txtFile.txt")}' is not located under the base directory");
        logger.AssertSingleWarningExists($"File '{Path.Combine(TestContext.TestDir, "foo.cs")}' is not located under the base directory");
    }

    [DataTestMethod]
    [DataRow(new string[] { ".nuget", "packages" }, false)]
    [DataRow(new string[] { "packages" }, true)]
    [DataRow(new string[] { ".nugetpackages" }, true)]
    [DataRow(new string[] { ".nuget", "foo", "packages" }, true)]
    public void GenerateFile_FileOutOfProjectRootDir_WarningsAreNotLoggedForFilesInStandardNugetCache(string[] subDirNames, bool isRaisingAWarning)
    {
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var dirOutOfProjectRoot = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, subDirNames);
        var projectDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
        var projectPath = Path.Combine(projectDir, "project.proj");
        var projectInfo = TestUtils.CreateProjectInfoInSubDir(testDir, "project", null, Guid.NewGuid(), ProjectType.Product, false, projectPath, "UTF-8");
        TestUtils.CreateEmptyFile(projectDir, "project.proj");

        var fileInNugetCache = TestUtils.CreateEmptyFile(dirOutOfProjectRoot, "foo.cs");

        // To add the files above, to the list of files that are to be analyzed, you need to add their paths to
        // the "contentList.txt" which is placed inside the projectDir folder.
        var contentFileListPath = TestUtils.CreateFile(projectDir, "contentList.txt", fileInNugetCache);
        // Add the file path of "contentList.txt" to the projectInfo.xml
        TestUtils.AddAnalysisResult(projectInfo, AnalysisType.FilesToAnalyze, contentFileListPath);
        var config = CreateValidConfig(testDir);

        // Act
        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        // Assert
        AssertExpectedProjectCount(1, result);
        // The project has no files in its root dir and the rest of the files are outside of the root, thus ignored and not analyzed.
        AssertExpectedStatus("project", ProjectInfoValidity.NoFilesToAnalyze, result);
        if (isRaisingAWarning)
        {
            logger.AssertWarningsLogged(1);
            logger.AssertSingleWarningExists($"File '{Path.Combine(dirOutOfProjectRoot, "foo.cs")}' is not located under the base directory");
        }
        else
        {
            logger.AssertWarningsLogged(0);
        }
    }

    [TestMethod]
    public void GenerateFile_SharedFiles()
    {
        // Shared files should be attached to the root project

        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var project1Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project1");
        var project1Path = Path.Combine(project1Dir, "project1.proj");
        var project1Info = TestUtils.CreateProjectInfoInSubDir(testDir, "projectName1", null, Guid.NewGuid(), ProjectType.Product, false, project1Path, "UTF-8"); // not excluded
        TestUtils.CreateEmptyFile(project1Dir, "project1.proj");
        var sharedFile = Path.Combine(testDir, "contentFile.txt");
        TestUtils.CreateEmptyFile(testDir, "contentFile.txt");

        // Reference shared file, but not under the project directory
        var contentFileList1 = TestUtils.CreateFile(project1Dir, "contentList.txt", sharedFile);
        TestUtils.AddAnalysisResult(project1Info, AnalysisType.FilesToAnalyze, contentFileList1);

        var project2Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project2");
        var project2Path = Path.Combine(project2Dir, "project2.proj");
        var project2Info = TestUtils.CreateProjectInfoInSubDir(testDir, "projectName2", null, Guid.NewGuid(), ProjectType.Product, false, project2Path, "UTF-8"); // not excluded
        TestUtils.CreateEmptyFile(project2Dir, "project2.proj");

        // Reference shared file, but not under the project directory
        var contentFileList2 = TestUtils.CreateFile(project2Dir, "contentList.txt", sharedFile);
        TestUtils.AddAnalysisResult(project2Info, AnalysisType.FilesToAnalyze, contentFileList2);
        var config = CreateValidConfig(testDir);

        // Act
        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        // Assert
        var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        provider.AssertSettingExists("sonar.projectBaseDir", testDir);
        provider.AssertSettingExists("sonar.sources", sharedFile);
    }

    // SONARMSBRU-335
    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void GenerateFile_SharedFiles_CaseInsensitive()
    {
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        // Create 2 uuids and order them so that test is reproducible
        var uuids = new Guid[] { Guid.NewGuid(), Guid.NewGuid() };
        Array.Sort(uuids);

        var project1Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project1");
        var project1Path = Path.Combine(project1Dir, "project1.proj");
        var project1Info = TestUtils.CreateProjectInfoInSubDir(testDir, "projectName1", null, uuids[0], ProjectType.Product, false, project1Path, "UTF-8"); // not excluded
        TestUtils.CreateEmptyFile(project1Dir, "project1.proj");
        var sharedFile = Path.Combine(testDir, "contentFile.txt");
        var sharedFileDifferentCase = Path.Combine(testDir, "ContentFile.TXT");
        TestUtils.CreateEmptyFile(testDir, "contentFile.txt");

        // Reference shared file, but not under the project directory
        var contentFileList1 = TestUtils.CreateFile(project1Dir, "contentList.txt", sharedFile);
        TestUtils.AddAnalysisResult(project1Info, AnalysisType.FilesToAnalyze, contentFileList1);

        var project2Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project2");
        var project2Path = Path.Combine(project2Dir, "project2.proj");
        var project2Info = TestUtils.CreateProjectInfoInSubDir(testDir, "projectName2", null, uuids[1], ProjectType.Product, false, project2Path, "UTF-8"); // not excluded
        TestUtils.CreateEmptyFile(project2Dir, "project2.proj");

        // Reference shared file, but not under the project directory
        var contentFileList2 = TestUtils.CreateFile(project2Dir, "contentList.txt", sharedFileDifferentCase);
        TestUtils.AddAnalysisResult(project2Info, AnalysisType.FilesToAnalyze, contentFileList2);
        var config = CreateValidConfig(testDir);

        // Act
        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        // Assert
        var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        provider.AssertSettingExists("sonar.projectBaseDir", testDir);
        // First one wins
        provider.AssertSettingExists("sonar.sources", sharedFile);
    }

    // SONARMSBRU-336
    [TestMethod]
    public void GenerateFile_SharedFiles_BelongToAnotherProject()
    {
        // Shared files that belong to another project should NOT be attached to the root project
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var project1Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project1");
        var project1Path = Path.Combine(project1Dir, "project1.proj");
        TestUtils.CreateEmptyFile(project1Dir, "project1.proj");
        var project1Guid = Guid.NewGuid();
        var project1Info = TestUtils.CreateProjectInfoInSubDir(testDir, "projectName1", null, project1Guid, ProjectType.Product, false, project1Path, "UTF-8"); // not excluded
        var fileInProject1 = Path.Combine(project1Dir, "contentFile.txt");
        TestUtils.CreateEmptyFile(project1Dir, "contentFile.txt");

        // Reference shared file, but not under the project directory
        var contentFileList1 = TestUtils.CreateFile(project1Dir, "contentList.txt", fileInProject1);
        TestUtils.AddAnalysisResult(project1Info, AnalysisType.FilesToAnalyze, contentFileList1);
        var project2Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project2");
        var project2Path = Path.Combine(project2Dir, "project2.proj");
        TestUtils.CreateEmptyFile(project2Dir, "project2.proj");
        var project2Info = TestUtils.CreateProjectInfoInSubDir(testDir, "projectName2", null, Guid.NewGuid(), ProjectType.Product, false, project2Path, "UTF-8"); // not excluded

        // Reference shared file, but not under the project directory
        var contentFileList2 = TestUtils.CreateFile(project2Dir, "contentList.txt", fileInProject1);
        TestUtils.AddAnalysisResult(project2Info, AnalysisType.FilesToAnalyze, contentFileList2);
        var config = CreateValidConfig(testDir);

        // Act
        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        // Assert
        var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        provider.AssertSettingExists("sonar.projectBaseDir", testDir);
        provider.AssertSettingDoesNotExist("sonar.sources");
        provider.AssertSettingExists(project1Guid.ToString().ToUpper() + ".sonar.sources", fileInProject1);
    }

    [TestMethod] // https://jira.codehaus.org/browse/SONARMSBRU-13: Analysis fails if a content file referenced in the MSBuild project does not exist
    public void GenerateFile_MissingFilesAreSkipped()
    {
        // Create project info with a managed file list and a content file list.
        // Each list refers to a file that does not exist on disk.
        // The missing files should not appear in the generated properties file.

        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var projectBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Project1");
        var projectFullPath = TestUtils.CreateEmptyFile(projectBaseDir, "project1.proj");
        var existingManagedFile = TestUtils.CreateEmptyFile(projectBaseDir, "File1.cs");
        var existingContentFile = TestUtils.CreateEmptyFile(projectBaseDir, "Content1.txt");
        var missingManagedFile = Path.Combine(projectBaseDir, "MissingFile1.cs");
        var missingContentFile = Path.Combine(projectBaseDir, "MissingContent1.txt");
        var projectInfo = new ProjectInfo()
        {
            FullPath = projectFullPath,
            AnalysisResults = new List<AnalysisResult>(),
            IsExcluded = false,
            ProjectGuid = Guid.NewGuid(),
            ProjectName = "project1.proj",
            ProjectType = ProjectType.Product,
            Encoding = "UTF-8"
        };

        var analysisFileList = CreateFileList(projectBaseDir, TestUtils.FilesToAnalyze, existingManagedFile, missingManagedFile, existingContentFile, missingContentFile);
        projectInfo.AddAnalyzerResult(AnalysisType.FilesToAnalyze, analysisFileList);
        var projectInfoDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "ProjectInfo1Dir");
        var projectInfoFilePath = Path.Combine(projectInfoDir, FileConstants.ProjectInfoFileName);
        projectInfo.Save(projectInfoFilePath);

        var logger = new TestLogger();
        var config = new AnalysisConfig()
        {
            SonarQubeHostUrl = "http://sonarqube.com",
            SonarProjectKey = "my_project_key",
            SonarProjectName = "my_project_name",
            SonarProjectVersion = "1.0",
            SonarOutputDir = testDir
        };

        // Act
        var result = new PropertiesFileGenerator(config, logger).GenerateFile();
        var actual = File.ReadAllText(result.FullPropertiesFilePath);

        // Assert
        AssertFileIsReferenced(existingContentFile, actual);
        AssertFileIsReferenced(existingManagedFile, actual);

        AssertFileIsNotReferenced(missingContentFile, actual);
        AssertFileIsNotReferenced(missingManagedFile, actual);

        logger.AssertSingleWarningExists(missingManagedFile);
        logger.AssertSingleWarningExists(missingContentFile);
    }

    [TestMethod]
    [Description("Checks that the generated properties file contains additional properties")]
    public void GenerateFile_AdditionalProperties()
    {
        // Arrange
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "project1", analysisRootDir);
        var config = CreateValidConfig(analysisRootDir);

        // Add additional properties
        config.LocalSettings = new AnalysisProperties
        {
            new("key1", "value1"),
            new("key.2", "value two"),
            new("key.3", " "),
            // Sensitive data should not be written
            new(SonarProperties.SonarPassword, "secret pwd"),
            new(SonarProperties.SonarUserName, "secret username"),
            new(SonarProperties.SonarToken, "secret token"),
            new(SonarProperties.ClientCertPassword, "secret client certpwd")
        };

        // Server properties should not be added
        config.ServerSettings = new AnalysisProperties { new("server.key", "should not be added") };

        // Act
        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        // Assert
        AssertExpectedProjectCount(1, result);

        // One valid project info file -> file created
        AssertPropertiesFilesCreated(result, logger);

        var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        provider.AssertSettingExists("key1", "value1");
        provider.AssertSettingExists("key.2", "value two");
        provider.AssertSettingExists("key.3", string.Empty);

        provider.AssertSettingDoesNotExist("server.key");
        provider.AssertSettingDoesNotExist(SonarProperties.SonarPassword);
        provider.AssertSettingDoesNotExist(SonarProperties.SonarUserName);
        provider.AssertSettingDoesNotExist(SonarProperties.ClientCertPassword);
        provider.AssertSettingDoesNotExist(SonarProperties.SonarToken);
    }

    [TestMethod]
    public void GenerateFile_WhenNoGuid_NoWarnings()
    {
        // Arrange
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "project1", null, analysisRootDir, Guid.Empty);
        var config = CreateValidConfig(analysisRootDir);

        // Act
        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        // Assert
        AssertExpectedProjectCount(1, result);
        // Empty guids are supported by generating them to the ProjectInfo.xml by WriteProjectInfoFile. In case it is not in ProjectInfo.xml, sonar-project.properties generation should fail.
        AssertFailedToCreatePropertiesFiles(result, logger);
        logger.Warnings.Should().BeEmpty();
    }

    [TestMethod] // Old VS Bootstrapper should be forceably disabled: https://jira.sonarsource.com/browse/SONARMSBRU-122
    public void GenerateFile_VSBootstrapperIsDisabled()
    {
        var result = ExecuteAndCheckSucceeds("disableBootstrapper", logger);

        var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        provider.AssertSettingExists(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");
        logger.AssertWarningsLogged(0);
    }

    [TestMethod]
    public void GenerateFile_VSBootstrapperIsDisabled_OverrideUserSettings_DifferentValue()
    {
        // Arrange
        // Try to explicitly enable the setting
        var bootstrapperProperty = new Property(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "true");

        // Act
        var result = ExecuteAndCheckSucceeds("disableBootstrapperDiff", logger, bootstrapperProperty);

        // Assert
        var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        provider.AssertSettingExists(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");
        logger.AssertSingleWarningExists(AnalysisConfigExtensions.VSBootstrapperPropertyKey);
    }

    [TestMethod]
    public void GenerateFile_VSBootstrapperIsDisabled_OverrideUserSettings_SameValue()
    {
        // Arrange
        var bootstrapperProperty = new Property(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");

        // Act
        var result = ExecuteAndCheckSucceeds("disableBootstrapperSame", logger, bootstrapperProperty);

        // Assert
        var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        provider.AssertSettingExists(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");
        logger.AssertDebugMessageExists(AnalysisConfigExtensions.VSBootstrapperPropertyKey);
        logger.AssertWarningsLogged(0); // not expecting a warning if the user has supplied the value we want
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void GenerateFile_ComputeProjectBaseDir()
    {
        VerifyProjectBaseDir(
            expectedValue: @"d:\work\mysources", // if there is a user value, use it
            teamBuildValue: @"d:\work",
            userValue: @"d:\work\mysources",
            projectPaths: [@"d:\work\proj1.csproj"]);

        VerifyProjectBaseDir(
            expectedValue: @"d:\work",  // if no user value, use the team build value
            teamBuildValue: @"d:\work",
            userValue: null,
            projectPaths: [@"e:\work"]);

        VerifyProjectBaseDir(
            expectedValue: @"e:\work",  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [@"e:\work"]);

        VerifyProjectBaseDir(
            expectedValue: @"e:\work",  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [@"e:\work", @"e:\work"]);

        VerifyProjectBaseDir(
            expectedValue: @"e:\work",  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [@"e:\work\A", @"e:\work\B\C"]);

        VerifyProjectBaseDir(
            expectedValue: @"e:\work",  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [@"e:\work\A", @"e:\work\B", @"e:\work\C"]);

        VerifyProjectBaseDir(
            expectedValue: @"e:\work\A",  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [@"e:\work\A\X", @"e:\work\A", @"e:\work\A"]);

        VerifyProjectBaseDir(
            expectedValue: null,  // if no common root exists, return null
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [@"f:\work\A", @"e:\work\B"]);

        // Support relative paths
        VerifyProjectBaseDir(
            expectedValue: Path.Combine(Directory.GetCurrentDirectory(), "src"),
            teamBuildValue: null,
            userValue: @".\src",
            projectPaths: [@"d:\work\proj1.csproj"]);

        // Support short name paths
        var result = ComputeProjectBaseDir(
            teamBuildValue: null,
            userValue: @"C:\PROGRA~1",
            projectPaths: [@"d:\work\proj1.csproj"]);
        result.Should().BeOneOf(@"C:\Program Files", @"C:\Program Files (x86)");
    }

    [DataTestMethod]
    [DataRow(@"d:\work", @"d:\work\mysources", new[] { @"d:\work\proj1.csproj" }, false)]
    [DataRow(@"d:\work", null, new[] { @"e:\work" }, false)]
    [DataRow(null, "", new[] { @"e:\work" }, true)]
    [DataRow(null, "", new[] { @"e:\work", @"e:\work" }, true)]
    public void GenerateFile_LogsProjectBaseDirInfo(string teamBuildValue, string userValue, string[] projectPaths, bool shouldLog)
    {
        var config = new AnalysisConfig()
        {
            SonarOutputDir = TestSonarqubeOutputDir,
            SourcesDirectory = teamBuildValue,
            LocalSettings = [new(SonarProperties.ProjectBaseDir, userValue)]
        };
        new PropertiesFileGenerator(config, logger).ComputeProjectBaseDir(projectPaths.Select(x => new DirectoryInfo(x)).ToList());

        if (shouldLog)
        {
            logger.AssertInfoLogged(ProjectBaseDirInfoMessage);
        }
        else
        {
            logger.AssertMessageNotLogged(ProjectBaseDirInfoMessage);
        }
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void TryWriteProperties_WhenThereIsNoCommonPath_LogsError()
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory!, ".sonarqube", "out");
        Directory.CreateDirectory(outPath);
        var fileToAnalyzePath = TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "file.cs");
        var filesToAnalyzePath = TestUtils.CreateFile(TestContext.TestRunDirectory, TestUtils.FilesToAnalyze, fileToAnalyzePath);
        var config = new AnalysisConfig { SonarOutputDir = outPath };
        var sut = new PropertiesFileGenerator(config, logger);

        var firstProjectInfo = new ProjectInfo
        {
            ProjectGuid = Guid.NewGuid(),
            FullPath = Path.Combine(TestContext.TestRunDirectory, "First"),
            ProjectName = "First",
            AnalysisSettings = [],
            AnalysisResults = [new AnalysisResult { Id = TestUtils.FilesToAnalyze, Location = filesToAnalyzePath }]
        };
        var secondProjectInfo = new ProjectInfo
        {
            ProjectGuid = Guid.NewGuid(),
            FullPath = Path.Combine(Path.GetTempPath(), "Second"),
            ProjectName = "Second",
            AnalysisSettings = [],
            AnalysisResults = [new AnalysisResult { Id = TestUtils.FilesToAnalyze, Location = filesToAnalyzePath }]
        };
        TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "First");
        TestUtils.CreateEmptyFile(Path.GetTempPath(), "Second");

        // In order to force automatic root path detection to point to file system root,
        // create a project in the test run directory and a second one in the temp folder.
        sut.TryWriteProperties(new PropertiesWriter(config, this.logger), [firstProjectInfo, secondProjectInfo], out _);

        logger.AssertErrorLogged("""The project base directory cannot be automatically detected. Please specify the "/d:sonar.projectBaseDir" on the begin step.""");
    }

    [TestMethod]
    public void TryWriteProperties_WhenThereAreNoValidProjects_LogsError()
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory!, ".sonarqube", "out");
        Directory.CreateDirectory(outPath);
        var config = new AnalysisConfig { SonarOutputDir = outPath };
        var sut = new PropertiesFileGenerator(config, logger);

        var firstProjectInfo = new ProjectInfo
        {
            ProjectGuid = Guid.NewGuid(),
            FullPath = Path.Combine(TestContext.TestRunDirectory, "First"),
            ProjectName = "First",
            IsExcluded = true,
            AnalysisSettings = [],
            AnalysisResults = []
        };
        var secondProjectInfo = new ProjectInfo
        {
            ProjectGuid = Guid.NewGuid(),
            FullPath = Path.Combine(TestContext.TestRunDirectory, "Second"),
            ProjectName = "Second",
            AnalysisSettings = [],
            AnalysisResults = []
        };
        TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "First");
        TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "Second");
        sut.TryWriteProperties(new PropertiesWriter(config, this.logger), [firstProjectInfo, secondProjectInfo], out _);

        logger.AssertInfoLogged($"The exclude flag has been set so the project will not be analyzed. Project file: {firstProjectInfo.FullPath}");
        logger.AssertErrorLogged("No analysable projects were found. SonarQube analysis will not be performed. Check the build summary report for details.");
    }

    [DataTestMethod]
    [DataRow("cs")]
    [DataRow("vbnet")]
    public void ProjectData_Orders_AnalyzerOutPaths(string languageKey)
    {
        var guid = Guid.NewGuid();
        var propertyKey = $"sonar.{languageKey}.analyzer.projectOutPaths";
        TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "foo");
        var fullPath = Path.Combine(TestContext.TestRunDirectory, "foo");
        var projectInfos = new[]
        {
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Release",
                Platform = "anyCpu",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = new AnalysisProperties { new(propertyKey, "1") },
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "anyCpu",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = new AnalysisProperties { new(propertyKey, "2") },
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "x86",
                TargetFramework = "net46",
                AnalysisSettings = new AnalysisProperties { new(propertyKey, "3") },
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "x86",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = new AnalysisProperties { new(propertyKey, "4") },
                FullPath = fullPath,
            },
        };

        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
        var propertiesFileGenerator = CreateSut(CreateValidConfig(analysisRootDir));
        var results = propertiesFileGenerator.ToProjectData(projectInfos.GroupBy(p => p.ProjectGuid).First()).AnalyzerOutPaths.ToList();

        results.Should().HaveCount(4);
        results[0].FullName.Should().Be(new FileInfo("2").FullName);
        results[1].FullName.Should().Be(new FileInfo("3").FullName);
        results[2].FullName.Should().Be(new FileInfo("4").FullName);
        results[3].FullName.Should().Be(new FileInfo("1").FullName);
    }

    [DataTestMethod]
    [DataRow("cs")]
    [DataRow("vbnet")]
    public void Telemetry_Multitargeting(string languageKey)
    {
        var guid = Guid.NewGuid();
        var propertyKey = $"sonar.{languageKey}.scanner.telemetry";
        TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "foo");
        var fullPath = Path.Combine(TestContext.TestRunDirectory, "foo");
        var projectInfos = new[]
        {
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = new() { new(propertyKey, "1.json") },
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                TargetFramework = "net46",
                AnalysisSettings = new() { new(propertyKey, "2.json") },
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Release",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = new()
                {
                    new(propertyKey, "3.json"),
                    new(propertyKey, "4.json"),
                },
                FullPath = fullPath,
            },
        };

        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
        var propertiesFileGenerator = CreateSut(CreateValidConfig(analysisRootDir));
        var results = propertiesFileGenerator.ToProjectData(projectInfos.GroupBy(x => x.ProjectGuid).Single()).TelemetryPaths.ToList();

        results.Should().BeEquivalentTo([new FileInfo("2.json"), new("1.json"), new("3.json"), new("4.json")], x => x.Excluding(x => x.Length).Excluding(x => x.Directory));
    }

    [TestMethod]
    public void ToProjectData_ProjectsWithDuplicateGuid()
    {
        var guid = Guid.NewGuid();
        var projectInfos = new[]
        {
            new ProjectInfo
            {
                ProjectGuid = guid,
                FullPath = "path1"
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                FullPath = "path2"
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                FullPath = "path2"
            },
        };
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
        var propertiesFileGenerator = new PropertiesFileGenerator(CreateValidConfig(analysisRootDir), logger);
        var result = propertiesFileGenerator.ToProjectData(projectInfos.GroupBy(p => p.ProjectGuid).First());

        result.Status.Should().Be(ProjectInfoValidity.DuplicateGuid);
        logger.Warnings.Should().BeEquivalentTo(
            new[]
            {
                $"Duplicate ProjectGuid: \"{guid}\". The project will not be analyzed. Project file: \"path1\"",
                $"Duplicate ProjectGuid: \"{guid}\". The project will not be analyzed. Project file: \"path2\"",
            });
    }

    // Repro for https://sonarsource.atlassian.net/browse/SCAN4NET-431
    [TestMethod]
    public void ToProjectData_DoesNotChooseValidProject()
    {
        var guid = Guid.NewGuid();
        TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "foo");
        var fullPath = Path.Combine(TestContext.TestRunDirectory, "foo");
        var contentFile1 = TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "contentFile1.txt");
        var contentFileList1 = TestUtils.CreateFile(TestContext.TestRunDirectory, "contentList.txt", contentFile1);
        var projectInfos = new[]
        {
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "x86",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = new AnalysisProperties {  },
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "x86",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = new AnalysisProperties
                {
                    new(PropertiesFileGenerator.ReportFilePathsCSharpPropertyKey, "validRoslyn"),
                    new(PropertiesFileGenerator.ProjectOutPathsCsharpPropertyKey, "validOutPath")
                },
                FullPath = fullPath,
            }
        };
        projectInfos[0].AddAnalyzerResult(AnalysisType.FilesToAnalyze, contentFileList1);
        projectInfos[1].AddAnalyzerResult(AnalysisType.FilesToAnalyze, contentFileList1);
        var config = CreateValidConfig("outputDir");
        var propertiesFileGenerator = new PropertiesFileGenerator(config, logger);

        var sut = propertiesFileGenerator.ToProjectData(projectInfos.GroupBy(x => x.ProjectGuid).First());

        sut.Status.Should().Be(ProjectInfoValidity.Valid);
        sut.Project.AnalysisSettings.Should().BeNullOrEmpty(); // Expected to change when fixed
        var writer = new PropertiesWriter(config, new TestLogger());
        writer.WriteSettingsForProject(sut);
        var resultString = writer.Flush();
        resultString.Should().NotContain("validRoslyn"); // Expected to change when fixed
        resultString.Should().NotContain("validOutPath"); // Expected to change when fixed
    }

    [TestMethod]
    public void GetClosestProjectOrDefault_WhenNoProjects_ReturnsNull()
    {
        // Arrange & Act
        var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo("foo"), Enumerable.Empty<ProjectData>());

        // Assert
        actual.Should().BeNull();
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void GetClosestProjectOrDefault_WhenNoMatch_ReturnsNull()
    {
        // Arrange
        var projects = new[]
        {
            new ProjectData(new ProjectInfo { FullPath = "D:\\foo.csproj" }),
            new ProjectData(new ProjectInfo { FullPath = "~foo\\bar.csproj" }),
            new ProjectData(new ProjectInfo { FullPath = "C:\\foobar.csproj" }),
        };

        // Act
        var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo("E:\\foo"), projects);

        // Assert
        actual.Should().BeNull();
    }

    [TestMethod]
    public void GetClosestProjectOrDefault_WhenOnlyOneProjectMatchingWithSameCase_ReturnsProject()
    {
        var projects = new[]
        {
            new ProjectData(new ProjectInfo { FullPath = "foo.csproj" }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine("~foo", "bar.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "foo", "foo.csproj") }),
        };

        var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot(), "foo", "foo.cs")), projects);

        actual.Should().Be(projects[2]);
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void GetClosestProjectOrDefault_WhenOnlyOneProjectMatchingWithDifferentCase_ReturnsProject()
    {
        var projects = new[]
        {
            new ProjectData(new ProjectInfo { FullPath = "foo.csproj" }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine("~foo", "bar.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "foo", "foo.csproj") }),
        };

        var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot("C"), "FOO", "FOO.cs")), projects);

        actual.Should().Be(projects[2]);
    }

    [TestMethod]
    public void GetClosestProjectOrDefault_WhenOnlyOneProjectMatchingWithDifferentSeparators_ReturnsProject()
    {
        var projects = new[]
        {
            new ProjectData(new ProjectInfo { FullPath = "foo.csproj" }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine("~foo", "bar.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = $"{TestUtils.DriveRoot()}{Path.AltDirectorySeparatorChar}foo{Path.AltDirectorySeparatorChar}foo.csproj" }),
        };

        var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot(), "foo", "foo.cs")), projects);

        actual.Should().Be(projects[2]);
    }

    [TestMethod]
    public void GetClosestProjectOrDefault_WhenMultipleProjectsMatch_ReturnsProjectWithLongestMatch()
    {
        var projects = new[]
        {
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "foo.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "foo", "bar.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "foo", "bar", "foo.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "foo", "xxx.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "foo", "bar", "foobar", "foo.csproj") }),
        };

        var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot(), "foo", "bar", "foo.cs")), projects);

        actual.Should().Be(projects[2]);
    }

    [TestMethod]
    public void GetClosestProjectOrDefault_WhenMultipleProjectsMatchWithSameLength_ReturnsClosestProject()
    {
        var projects = new[]
        {
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "fooNet46.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "fooXamarin.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "fooNetStd.csproj") }),
        };

        var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot(), "foo", "bar", "foo.cs")), projects);

        actual.Should().Be(projects[0]);
    }

    [TestMethod]
    public void ComputeProjectBaseDir_BestCommonRoot_AllInRoot_NoWarning()
    {
        var sut = new PropertiesFileGenerator(new(), logger);
        var projectPaths = new[]
        {
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Lib")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Src")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Test")),
        };

        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name"));
        logger.AssertNoWarningsLogged();
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    // On Unix, there always is a best common root "/" and there are never projects outside of the root.
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_BestCommonRoot_ProjectOutsideRoot_LogsWarning()
    {
        var sut = new PropertiesFileGenerator(new(), logger);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\Projects\Name\Src"),
            new DirectoryInfo(@"C:\Projects\Name\Test"),
            new DirectoryInfo(@"D:\OutsideRoot"),
            new DirectoryInfo(@"E:\AlsoOutside"),
        };

        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(@"C:\Projects\Name");
        logger.AssertWarningLogged(@"Directory 'D:\OutsideRoot' is not located under the base directory 'C:\Projects\Name' and will not be analyzed.");
        logger.AssertWarningLogged(@"Directory 'E:\AlsoOutside' is not located under the base directory 'C:\Projects\Name' and will not be analyzed.");
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    // On Linux there always is a best common root "/".
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_NoBestCommonRoot_ReturnsNull()
    {
        var sut = new PropertiesFileGenerator(new AnalysisConfig(), logger);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\RootOnce"),
            new DirectoryInfo(@"D:\AlsoOnce"),
            new DirectoryInfo(@"E:\NotHelping"),
        };

        sut.ComputeProjectBaseDir(projectPaths).Should().BeNull();
        logger.AssertNoErrorsLogged();
        logger.AssertNoWarningsLogged();
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    [TestMethod]
    public void ComputeProjectBaseDir_WorkingDirectory_AllFilesInWorkingDirectory()
    {
        var sut = new PropertiesFileGenerator(new AnalysisConfig { SonarScannerWorkingDirectory = Path.Combine(TestUtils.DriveRoot(), "Projects") }, logger);
        var projectPaths = new[]
        {
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Lib")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Src")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Test")),
        };

        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(Path.Combine(TestUtils.DriveRoot(), "Projects"));
        logger.AssertNoWarningsLogged();
        logger.DebugMessages.Should().BeEquivalentTo($"Using working directory as project base directory: '{Path.Combine(TestUtils.DriveRoot(), "Projects")}'.");
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    [TestMethod]
    public void ComputeProjectBaseDir_WorkingDirectory_FilesOutsideWorkingDirectory_FallsBackToCommonPath()
    {
        var sut = new PropertiesFileGenerator(new AnalysisConfig { SonarScannerWorkingDirectory = Path.Combine(TestUtils.DriveRoot(), "Solution", "Net") }, logger);
        var projectPaths = new[]
        {
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Solution", "Net", "Name", "Lib")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Solution", "Net", "Name", "Src")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Solution", "JS")), // At least one directory is not below SonarScannerWorkingDirectory. We fall back to the common root logic.
        };

        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(Path.Combine(TestUtils.DriveRoot(), "Solution"));
        logger.AssertNoWarningsLogged();
        logger.DebugMessages.Should().ContainSingle().Which.Should().BeIgnoringLineEndings(
            $"""
            Using longest common projects path as a base directory: '{Path.Combine(TestUtils.DriveRoot(), "Solution")}'. Identified project paths:
            {Path.Combine(TestUtils.DriveRoot(), "Solution", "Net", "Name", "Lib")}
            {Path.Combine(TestUtils.DriveRoot(), "Solution", "Net", "Name", "Src")}
            {Path.Combine(TestUtils.DriveRoot(), "Solution", "JS")}
            """);
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    // On Unix, there always is a best common root "/".
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_BestCommonRoot_CaseSensitive_NoRoot_ReturnsNull()
    {
        var runtimeInformationWrapper = Substitute.For<IRuntimeInformationWrapper>();
        runtimeInformationWrapper.IsOS(OSPlatform.Windows).Returns(false);
        var additionalFileService = Substitute.For<IAdditionalFilesService>();
        var sut = new PropertiesFileGenerator(new() { SonarOutputDir = @"C:\fallback" }, logger, new RoslynV1SarifFixer(logger), runtimeInformationWrapper, additionalFileService);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\Projects\Name\Lib"),
            new DirectoryInfo(@"c:\projects\name\Test"),
        };

        sut.ComputeProjectBaseDir(projectPaths).Should().BeNull();
        logger.AssertNoWarningsLogged();
        logger.AssertNoErrorsLogged();
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    // Case sensitive tests don't apply to Unix.
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_BestCommonRoot_CaseInsensitive()
    {
        var runtimeInformationWrapper = Substitute.For<IRuntimeInformationWrapper>();
        runtimeInformationWrapper.IsOS(OSPlatform.Windows).Returns(true);
        var additionalFileService = Substitute.For<IAdditionalFilesService>();
        var sut = new PropertiesFileGenerator(new(), logger, new RoslynV1SarifFixer(logger), runtimeInformationWrapper, additionalFileService);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\Projects\Name\Lib"),
            new DirectoryInfo(@"c:\projects\name\Test"),
        };

        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(@"C:\Projects\Name");
        logger.AssertNoWarningsLogged();
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    // On Unix, there always is a best common root "/".
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_WorkingDirectory_FilesOutsideWorkingDirectory_NoCommonRoot()
    {
        var logger = new TestLogger();
        var sut = new PropertiesFileGenerator(new AnalysisConfig { SonarScannerWorkingDirectory = @"C:\Solution" }, logger);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\Solution\Net\Name\Lib"),
            new DirectoryInfo(@"C:\Solution\Net\Name\Src"),
            new DirectoryInfo(@"D:\SomewhereElse"), // At least one directory is not below SonarScannerWorkingDirectory. We fall back to the common root logic.
        };

        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(@"C:\Solution\Net\Name");
        logger.Warnings.Should().BeEquivalentTo(@"Directory 'D:\SomewhereElse' is not located under the base directory 'C:\Solution\Net\Name' and will not be analyzed.");
        logger.DebugMessages.Should().BeEquivalentTo(
            """
            Using longest common projects path as a base directory: 'C:\Solution\Net\Name'. Identified project paths:
            C:\Solution\Net\Name\Lib
            C:\Solution\Net\Name\Src
            D:\SomewhereElse
            """);
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    [DataTestMethod] // the priority is local > scannerEnv > server.
    [DataRow("local", null, null, "local")]
    [DataRow("local", "scannerEnv", null, "local")]
    [DataRow("local", null, "server", "local")]
    [DataRow("local", "scannerEnv", "server", "local")]
    [DataRow(null, "scannerEnv", null, "scannerEnv")]
    [DataRow(null, "scannerEnv", "server", "scannerEnv")]
    [DataRow(null, null, "server", "server")]
    public void ComputeProjectBaseDir_SetFromMultipleSources(string local, string scannerEnv, string server, string expected)
    {
        var projectBaseDirKey = "sonar.projectBaseDir";
        using var scope = new EnvironmentVariableScope();
        var config = new AnalysisConfig { LocalSettings = [], ServerSettings = [] };

        if (local is not null)
        {
            config.LocalSettings.Add(new(projectBaseDirKey, local));
        }
        if (server is not null)
        {
            config.ServerSettings.Add(new(projectBaseDirKey, server));
        }
        if (scannerEnv is not null)
        {
            scope.SetVariable("SONARQUBE_SCANNER_PARAMS", $$"""{"{{projectBaseDirKey}}": "{{scannerEnv}}"}""");
        }

        new PropertiesFileGenerator(config, logger).ComputeProjectBaseDir([]).Name.Should().Be(expected);
        logger.DebugMessages.Should().ContainSingle(x => x.StartsWith("Using user supplied project base directory:"));
    }

    [TestMethod]
    public void GenerateFile_AdditionalFiles_EndToEnd()
    {
        var project1 = "project1";
        var project2 = "project2";
        var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var rootProjects = Path.Combine(root, "projects");

        TestUtils.CreateProjectWithFiles(TestContext, project1, root);
        TestUtils.CreateProjectWithFiles(TestContext, project2, root);
        string[] rootSources =
        [
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.ipynb"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.php"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.py"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.spec.ipynb"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.spec.py"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.spec.sql"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.sql"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.test.ipynb"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.test.php"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.test.py"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.test.sql"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.ts"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.tsx"),
        ];
        string[] rootTests =
        [
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project2), "project2.spec.tsx"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project2), "project2.test.tsx"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.spec.ts"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.test.tsx"),
        ];
        string[] project1Sources =
        [
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project1), "project1.sql"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project1), "project1.py"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project1), "project1.ipynb"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project1), "project1.ts"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project1), "project1.php"),
        ];
        string[] project2Sources =
        [
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project2), "project2.tsx"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project2), "project2.sql"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project2), "project2.py"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project2), "project2.ipynb"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project2), "project2.php"),
        ];

        AnalysisProperties serverProperties =
        [
            new("sonar.typescript.file.suffixes", ".ts,.tsx"),
            new("sonar.tsql.file.suffixes", "sql"),
            new("sonar.python.file.suffixes", "py"),
            new("sonar.ipynb.file.suffixes", "ipynb"),
            new("sonar.php.file.suffixes", "php"),
        ];
        var config = CreateValidConfig(root, serverProperties);

        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        AssertExpectedProjectCount(2, result);
        AssertPropertiesFilesCreated(result, logger);
        AssertExpectedStatus(project1, ProjectInfoValidity.Valid, result);
        AssertExpectedStatus(project2, ProjectInfoValidity.Valid, result);
        AssertExpectedPathsAddedToModuleFiles(project1, project1Sources);
        AssertExpectedPathsAddedToModuleFiles(project2, project2Sources);

        // Multiline string literal doesn't work here because of environment-specific line ending.
        var propertiesFile = File.ReadAllText(result.FullPropertiesFilePath);
        PropertiesValues(propertiesFile, "sonar.sources").Should().BeEquivalentTo(rootSources.Select(x => x.Replace(@"\", @"\\")));
        PropertiesValues(propertiesFile, "sonar.tests").Should().BeEquivalentTo(rootTests.Select(x => x.Replace(@"\", @"\\")));

        void AssertExpectedPathsAddedToModuleFiles(string projectId, string[] expectedPaths) =>
         expectedPaths.Should().BeSubsetOf(result.Projects.Single(x => x.Project.ProjectName == projectId).SonarQubeModuleFiles.Select(x => x.FullName));
    }

    [TestMethod]
    public void GenerateFile_AdditionalFiles_OnlyTestFiles_EndToEnd()
    {
        var project1 = "project1";
        var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var rootProjects = Path.Combine(root, "projects");

        TestUtils.CreateProjectWithFiles(TestContext, project1, root);
        string[] testFiles =
        [
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project1), "project1.spec.tsx"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project1), "project1.test.tsx"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.spec.ts"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.test.tsx"),
        ];
        AnalysisProperties serverProperties =
        [
            new("sonar.typescript.file.suffixes", ".ts,.tsx"),
        ];
        var config = CreateValidConfig(root, serverProperties, rootProjects);

        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        AssertExpectedProjectCount(1, result);
        AssertPropertiesFilesCreated(result, logger);
        AssertExpectedStatus(project1, ProjectInfoValidity.Valid, result);

        // Multiline string literal doesn't work here because of environment-specific line ending.
        var propertiesFile = File.ReadAllText(result.FullPropertiesFilePath);
        PropertiesValues(propertiesFile, "sonar.tests").Should().BeEquivalentTo(testFiles.Select(x => x.Replace(@"\", @"\\")));
    }

    [DataTestMethod]
    [DataRow("https://sonarcloud.io")]
    [DataRow("https://sonarqube.us")]
    [DataRow("https://sonarqqqq.whale")]    // Any value, as long as it was auto-computed by the default URL mechanism and stored in SonarQubeAnalysisConfig.xml
    public void TryWriteProperties_HostUrl_NotSet_UseSonarQubeHostUrl(string sonarQubeHostUrl)
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory, ".sonarqube", "out");
        var config = new AnalysisConfig { SonarProjectKey = "key", SonarOutputDir = outPath, SonarQubeHostUrl = sonarQubeHostUrl };
        var writer = new PropertiesWriter(config, logger);
        TryWriteProperties_HostUrl_Execute(config, writer);

        writer.Flush().Should().Contain($"sonar.host.url={sonarQubeHostUrl}");
        logger.AssertDebugLogged("Setting analysis property: sonar.host.url=" + sonarQubeHostUrl);
    }

    [TestMethod]
    public void TryWriteProperties_HostUrl_ExplicitValue_Propagated()
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory, ".sonarqube", "out");
        var config = new AnalysisConfig { SonarProjectKey = "key", SonarOutputDir = outPath, SonarQubeHostUrl = "Property should take precedence and this should not be used" };
        config.LocalSettings = [new Property(SonarProperties.HostUrl, "http://localhost:9000")];
        var writer = new PropertiesWriter(config, logger);
        TryWriteProperties_HostUrl_Execute(config, writer);

        writer.Flush().Should().Contain("sonar.host.url=http://localhost:9000");
    }

    private void TryWriteProperties_HostUrl_Execute(AnalysisConfig config, PropertiesWriter writer)
    {
        Directory.CreateDirectory(config.SonarOutputDir);
        var sut = new PropertiesFileGenerator(config, logger);
        var projectPath = TestUtils.CreateEmptyFile(config.SonarOutputDir, "Project.csproj");
        var sourceFilePath = TestUtils.CreateEmptyFile(config.SonarOutputDir, "Program.cs");
        var filesToAnalyzePath = TestUtils.CreateFile(config.SonarOutputDir, "FilesToAnalyze.txt", sourceFilePath);
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(EnvScannerPropertiesProvider.ENV_VAR_KEY, null);  // Otherwise it is picking up our own analysis variables and explicit sonarcloud.io URL
        var project = new ProjectInfo
        {
            ProjectGuid = new Guid("A85D6F60-4D86-401E-BE44-177F524BD4BB"),
            FullPath = projectPath,
            ProjectName = "Project",
            IsExcluded = false,
            AnalysisSettings = [],
            AnalysisResults = [new AnalysisResult { Id = AnalysisType.FilesToAnalyze.ToString(), Location = filesToAnalyzePath }],
        };
        sut.TryWriteProperties(writer, [project], out _).Should().BeTrue();
    }

    /// <summary>
    /// Creates a single new project valid project with dummy files and analysis config file with the specified local settings.
    /// Checks that a property file is created.
    /// </summary>
    private ProjectInfoAnalysisResult ExecuteAndCheckSucceeds(string projectName, TestLogger logger, params Property[] localSettings)
    {
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, projectName);
        TestUtils.CreateProjectWithFiles(TestContext, projectName, analysisRootDir);
        var config = CreateValidConfig(analysisRootDir);
        config.LocalSettings = new AnalysisProperties();
        foreach (var property in localSettings)
        {
            config.LocalSettings.Add(property);
        }

        // Act
        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        // Assert
        AssertExpectedProjectCount(1, result);
        AssertPropertiesFilesCreated(result, logger);

        return result;
    }

    private static void AssertFailedToCreatePropertiesFiles(ProjectInfoAnalysisResult result, TestLogger logger)
    {
        result.FullPropertiesFilePath.Should().BeNull("Not expecting the sonar-scanner properties file to have been set");
        result.RanToCompletion.Should().BeFalse("Expecting the property file generation to have failed");

        AssertNoValidProjects(result);

        logger.AssertErrorsLogged();
    }

    private void AssertPropertiesFilesCreated(ProjectInfoAnalysisResult result, TestLogger logger)
    {
        result.FullPropertiesFilePath.Should().NotBeNull("Expecting the sonar-scanner properties file to have been set");

        AssertValidProjectsExist(result);
        //TestContext.AddResultFile(result.FullPropertiesFilePath);

        logger.AssertErrorsLogged(0);
    }

    private static void AssertExpectedStatus(string expectedProjectName, ProjectInfoValidity expectedStatus, ProjectInfoAnalysisResult actual)
    {
        var matches = actual.GetProjectsByStatus(expectedStatus).Where(p => p.ProjectName.Equals(expectedProjectName));
        matches.Should().ContainSingle("ProjectInfo was not classified as expected. Project name: {0}, expected status: {1}", expectedProjectName, expectedStatus);
    }

    private static void AssertNoValidProjects(ProjectInfoAnalysisResult actual)
    {
        IEnumerable<ProjectInfo> matches = actual.GetProjectsByStatus(ProjectInfoValidity.Valid);
        matches.Should().BeEmpty("Not expecting to find any valid ProjectInfo files");
    }

    private static void AssertValidProjectsExist(ProjectInfoAnalysisResult actual)
    {
        IEnumerable<ProjectInfo> matches = actual.GetProjectsByStatus(ProjectInfoValidity.Valid);
        matches.Should().NotBeEmpty("Expecting at least one valid ProjectInfo file to exist");
    }

    private static void AssertExpectedProjectCount(int expected, ProjectInfoAnalysisResult actual)
    {
        actual.Projects.Should().HaveCount(expected, "Unexpected number of projects in the result");
    }

    private static void AssertFileIsReferenced(string fullFilePath, string content)
    {
        var formattedPath = PropertiesWriter.Escape(fullFilePath);
        content.Should().Contain(formattedPath, "Files should be referenced: {0}", formattedPath);
    }

    private static void AssertFileIsNotReferenced(string fullFilePath, string content)
    {
        var formattedPath = PropertiesWriter.Escape(fullFilePath);
        content.Should().NotContain(formattedPath, "File should not be referenced: {0}", formattedPath);
    }

    private static string ComputeProjectBaseDir(string teamBuildValue, string userValue, string[] projectPaths)
    {
        var config = new AnalysisConfig();
        var logger = new TestLogger();
        new PropertiesWriter(config, logger);
        config.SonarOutputDir = TestSonarqubeOutputDir;
        config.SourcesDirectory = teamBuildValue;
        config.LocalSettings ??= new();
        config.LocalSettings.Add(new(SonarProperties.ProjectBaseDir, userValue));

        // Act
        return new PropertiesFileGenerator(config, logger)
            .ComputeProjectBaseDir(projectPaths.Select(x => new DirectoryInfo(x)).ToList())
            ?.FullName;
    }

    private void VerifyProjectBaseDir(string expectedValue, string teamBuildValue, string userValue, string[] projectPaths)
    {
        var result = ComputeProjectBaseDir(teamBuildValue, userValue, projectPaths);
        result.Should().Be(expectedValue);
    }

    private static AnalysisConfig CreateValidConfig(string outputDir, AnalysisProperties serverProperties = null, string workingDir = null)
    {
        var dummyProjectKey = Guid.NewGuid().ToString();
        var config = new AnalysisConfig()
        {
            SonarOutputDir = outputDir,
            SonarQubeHostUrl = "http://sonarqube.com",
            SonarProjectKey = dummyProjectKey,
            SonarProjectName = dummyProjectKey,
            SonarConfigDir = Path.Combine(outputDir, "config"),
            SonarProjectVersion = "1.0",
            SonarScannerWorkingDirectory = workingDir,
            ServerSettings = serverProperties ?? [],
            LocalSettings = [],
            ScanAllAnalysis = true,
        };

        return config;
    }

    private static string CreateFileList(string parentDir, string fileName, params string[] files)
    {
        var fullPath = Path.Combine(parentDir, fileName);
        File.WriteAllLines(fullPath, files);
        return fullPath;
    }

    private PropertiesFileGenerator CreateSut(
        AnalysisConfig analysisConfig,
        IRoslynV1SarifFixer sarifFixer = null,
        IRuntimeInformationWrapper runtimeInformationWrapper = null,
        IAdditionalFilesService additionalFileService = null)
    {
        sarifFixer ??= new RoslynV1SarifFixer(logger);
        runtimeInformationWrapper ??= new RuntimeInformationWrapper();
        additionalFileService ??= new AdditionalFilesService(DirectoryWrapper.Instance, logger);
        return new(analysisConfig, logger, sarifFixer, runtimeInformationWrapper, additionalFileService);
    }

    // https://regex101.com/r/BTyGeP/1
    private IEnumerable<string> PropertiesValues(string properties, string key) =>
        Regex
            .Match(properties, $@"^{Regex.Escape(key)}=\\\r?\n(?<values>(?:.*,\\\r?\n)*[^\r\n]*)", RegexOptions.Multiline)
            .Groups["values"]
            .Value
            .Split([@$",\{Environment.NewLine}", Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
}
