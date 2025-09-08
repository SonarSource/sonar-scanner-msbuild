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

namespace SonarScanner.MSBuild.Shim.Test;

public partial class ScannerEngineInputGeneratorTest
{
    [TestMethod]
    public void GenerateResult_NoProjectInfoFiles()
    {
        // Properties file should not be generated if there are no project info files.
        // Two sub-directories, neither containing a ProjectInfo.xml
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var subDir1 = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "dir1");
        var subDir2 = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "dir2");
        TestUtils.CreateEmptyFile(subDir1, "file1.txt");
        TestUtils.CreateEmptyFile(subDir2, "file2.txt");
        var config = new AnalysisConfig { SonarOutputDir = testDir, SonarQubeHostUrl = "http://sonarqube.com" };
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();

        AssertFailedToCreateScannerInput(result);
        AssertExpectedProjectCount(0, result);
    }

    [TestMethod]
    public void GenerateResult_ValidFiles()
    {
        // Only non-excluded projects with files to analyze should be marked as valid
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var withoutFilesDir = Path.Combine(testDir, "withoutFiles");
        Directory.CreateDirectory(withoutFilesDir);
        TestUtils.CreateProjectInfoInSubDir(testDir, "withoutFiles", null, Guid.NewGuid(), ProjectType.Product, false, Path.Combine(withoutFilesDir, "withoutFiles.proj"), "UTF-8"); // not excluded
        TestUtils.CreateEmptyFile(withoutFilesDir, "withoutFiles.proj");
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles2", testDir);
        var config = CreateValidConfig(testDir);
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();

        AssertExpectedStatus("withoutFiles", ProjectInfoValidity.NoFilesToAnalyze, result);
        AssertExpectedStatus("withFiles1", ProjectInfoValidity.Valid, result);
        AssertExpectedStatus("withFiles2", ProjectInfoValidity.Valid, result);
        AssertExpectedProjectCount(3, result);

        // One valid project info file -> file created
        AssertScannerInputCreated(result);
    }

    [TestMethod]
    public void GenerateResult_Csproj_DoesNotExist()
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
        var result = CreateSut(config).GenerateResult();

        AssertExpectedStatus(projectName, ProjectInfoValidity.ProjectNotFound, result);
        AssertExpectedProjectCount(1, result);
    }

    [TestMethod]
    [DataRow(PlatformOS.Windows)]
    [DataRow(PlatformOS.Linux)]
    [DataRow(PlatformOS.MacOSX)]
    [DataRow(PlatformOS.Alpine)]
    public void GenerateResult_Duplicate_SameGuid_DifferentCase(PlatformOS os)
    {
        var guid = Guid.NewGuid();
        var testRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Projects");
        var projectFileOrig = CreateProject("Project1", "DifferentCasing.proj");
        var projectFileDiff = CreateProject("Project2", "dIFFERENTcASING.proj");    // Same file for windows, different for Unix
        var config = CreateValidConfig(testRootDir);
        var result = CreateSut(config, os: os).GenerateResult();

        AssertExpectedProjectCount(1, result);
        if (os == PlatformOS.Windows)
        {
            AssertExpectedStatus("Project1", ProjectInfoValidity.Valid, result);
            runtime.Logger.Warnings.Should().BeEmpty("Windows is case insensitive and all project files are considered the same");
        }
        else
        {
            // Casing should not be ignored on non-windows OS, none of those two different project files with the same GUID will be analyzed
            AssertExpectedStatus("Project1", ProjectInfoValidity.DuplicateGuid, result);
            runtime.Logger.Warnings.Should().HaveCount(2).And.BeEquivalentTo(
                $"Duplicate ProjectGuid: \"{guid}\". The project will not be analyzed. Project file: \"{projectFileOrig}\"",
                $"Duplicate ProjectGuid: \"{guid}\". The project will not be analyzed. Project file: \"{projectFileDiff}\"");
        }

        string CreateProject(string projectName, string projectFileName)
        {
            var projectDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "SameDirForBoth");
            var projectFile = TestUtils.CreateEmptyFile(projectDir, projectFileName);
            var projectInfo = TestUtils.CreateProjectInfoInSubDir(testRootDir, projectName, null, guid, ProjectType.Product, false, projectFile, "UTF-8");
            // Create content / managed files to make it valid
            var contentFile = TestUtils.CreateEmptyFile(projectDir, "ContentFile.txt");
            var contentFileList = TestUtils.CreateFile(projectDir, "ContentList.txt", contentFile);
            TestUtils.AddAnalysisResult(projectInfo, AnalysisResultFileType.FilesToAnalyze, contentFileList);
            return projectFile;
        }
    }

    [TestMethod]
    public void GenerateResult_ValidFiles_SourceEncoding_Provided()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);
        var config = CreateValidConfig(testDir);
        config.LocalSettings = [new(SonarProperties.SourceEncoding, "test-encoding-here")];
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();

        var settingsFileContent = File.ReadAllText(result.FullPropertiesFilePath);
        settingsFileContent.Should().Contain("sonar.sourceEncoding=test-encoding-here", "Command line parameter 'sonar.sourceEncoding' is ignored.");
        runtime.Should().HaveDebugsLogged(string.Format(Resources.DEBUG_DumpSonarProjectProperties, settingsFileContent));
    }

    [TestMethod]
    public void GenerateResult_TFS_Coverage_TrxAreWritten()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);
        var config = CreateValidConfig(testDir);
        config.LocalSettings = [
            new(SonarProperties.VsCoverageXmlReportsPaths, "coverage-path"),
            new(SonarProperties.VsTestReportsPaths, "trx-path"),
        ];
        var result = CreateSut(config).GenerateResult();

        var settingsFileContent = File.ReadAllText(result.FullPropertiesFilePath);
        settingsFileContent.Should().Contain("sonar.cs.vscoveragexml.reportsPaths=coverage-path");
        settingsFileContent.Should().Contain("sonar.cs.vstest.reportsPaths=trx-path");
        runtime.Should().HaveDebugsLogged(string.Format(Resources.DEBUG_DumpSonarProjectProperties, settingsFileContent));
    }

    [TestMethod]
    public void GenerateResult_SensitiveParamsNotLogged()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);
        var config = CreateValidConfig(testDir);
        config.LocalSettings = [
            new(SonarProperties.ClientCertPath, "Client cert path"),           // should be logged as it is not sensitive
            new(SonarProperties.ClientCertPassword, "Client cert password")    // should not be logged as it is sensitive
        ];
        CreateSut(config).GenerateResult();

        runtime.Logger.DebugMessages.Should().Contain(x => x.Contains("Client cert path"));
        runtime.Logger.DebugMessages.Should().NotContain(x => x.Contains("Client cert password"));
    }

    [TestMethod]
    public void GenerateResult_ValidFiles_WithAlreadyValidSarif()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        // SARIF file path
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        // Create SARIF report path property and add it to the project info
        var projectSettings = new AnalysisProperties { new("sonar.cs.roslyn.reportFilePaths", testSarifPath) };
        var projectGuid = Guid.NewGuid();
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", ProjectLanguages.CSharp, testDir, projectGuid, true, projectSettings);
        var config = CreateValidConfig(testDir);
        // Mock SARIF fixer simulates already valid sarif
        var mockSarifFixer = new MockRoslynV1SarifFixer(testSarifPath);
        var mockReturnPath = mockSarifFixer.ReturnVal;
        var result = CreateSut(config, mockSarifFixer).GenerateResult();

        mockSarifFixer.CallCount.Should().Be(1);
        // Already valid SARIF -> no change in file -> unchanged property
        var sqProperties = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        sqProperties.AssertSettingExists(projectGuid.ToString().ToUpper() + ".sonar.cs.roslyn.reportFilePaths", AddQuotes(mockReturnPath));
        CreateInputReader(result).AssertProperty(projectGuid.ToString().ToUpper() + ".sonar.cs.roslyn.reportFilePaths", mockReturnPath);
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.roslyn.reportFilePaths", "cs")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.roslyn.reportFilePaths", "vbnet")]
    public void GenerateResult_ValidFiles_WithFixableSarif(string projectLanguage, string propertyKey, string expectedSarifLanguage)
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        // SARIF file path
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        // Create SARIF report path property and add it to the project info
        var projectSettings = new AnalysisProperties { new(propertyKey, testSarifPath) };
        var projectGuid = Guid.NewGuid();
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", projectLanguage, testDir, projectGuid, true, projectSettings);
        var config = CreateValidConfig(testDir);
        // Mock SARIF fixer simulates fixable SARIF with fixed name
        var returnPathFileName = Path.GetFileNameWithoutExtension(testSarifPath) + "_fixed" + Path.GetExtension(testSarifPath);
        var sarifFixer = new MockRoslynV1SarifFixer(Path.Combine(testDir, returnPathFileName));
        var result = CreateSut(config, sarifFixer).GenerateResult();

        sarifFixer.CallCount.Should().Be(1);
        sarifFixer.LastLanguage.Should().Be(expectedSarifLanguage);
        // Fixable SARIF -> new file saved -> changed property
        var sqProperties = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        sqProperties.AssertSettingExists(projectGuid.ToString().ToUpper() + "." + propertyKey, AddQuotes(sarifFixer.ReturnVal));
        CreateInputReader(result).AssertProperty(projectGuid.ToString().ToUpper() + "." + propertyKey, sarifFixer.ReturnVal);
    }

    [TestMethod]
    public void GenerateResult_WithMultipleAnalyzerAndRoslynOutputPaths_ShouldBeSupported()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var config = CreateValidConfig(testDir);
        var testSarifPath1 = Path.Combine(testDir, "testSarif1.json");
        var testSarifPath2 = Path.Combine(testDir, "testSarif2.json");
        var testSarifPath3 = Path.Combine(testDir, "testSarif3.json");
        // Mock SARIF fixer simulates fixable SARIF with fixed name
        var mockSarifFixer = new MockRoslynV1SarifFixer(null);
        var projectSettings = new AnalysisProperties
        {
            new("sonar.vbnet.roslyn.reportFilePaths", $"{testSarifPath1}|{testSarifPath2}|{testSarifPath3}")
        };
        var projectGuid = Guid.NewGuid();
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", ProjectLanguages.VisualBasic, testDir, projectGuid, true, projectSettings);
        var result = CreateSut(config, mockSarifFixer).GenerateResult();

        var sqProperties = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        sqProperties.AssertSettingExists(
            $"{projectGuid.ToString().ToUpper()}.sonar.vbnet.roslyn.reportFilePaths",
            $@"""{testSarifPath1}.fixed.mock.json"",""{testSarifPath2}.fixed.mock.json"",""{testSarifPath3}.fixed.mock.json""");
        CreateInputReader(result).AssertProperty(
            $"{projectGuid.ToString().ToUpper()}.sonar.vbnet.roslyn.reportFilePaths",
            $"{testSarifPath1}.fixed.mock.json,{testSarifPath2}.fixed.mock.json,{testSarifPath3}.fixed.mock.json");
    }

    [TestMethod]
    public void GenerateResult_ValidFiles_WithUnfixableSarif()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        // SARIF file path
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        // Create SARIF report path property and add it to the project info
        var projectSettings = new AnalysisProperties { new("sonar.cs.roslyn.reportFilePaths", testSarifPath) };
        var projectGuid = Guid.NewGuid();
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", null, testDir, projectGuid, true, projectSettings);
        var config = CreateValidConfig(testDir);
        // Mock SARIF fixer simulated unfixable/absent file
        var mockSarifFixer = new MockRoslynV1SarifFixer(null);
        var result = CreateSut(config, mockSarifFixer).GenerateResult();

        mockSarifFixer.CallCount.Should().Be(1);
        // One valid project info file -> file created
        AssertScannerInputCreated(result);
        // Unfixable SARIF -> cannot fix -> report file property removed
        var sqProperties = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        sqProperties.AssertSettingDoesNotExist(projectGuid.ToString().ToUpper() + "." + "sonar.cs.roslyn.reportFilePaths");
        CreateInputReader(result).AssertPropertyDoesNotExist(projectGuid.ToString().ToUpper() + "." + "sonar.cs.roslyn.reportFilePaths");
    }

    [TestMethod]
    public void GenerateResult_FilesOutOfProjectRootDir_TheyAreNotAnalyzedAndCorrectWarningsAreLogged()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var projectDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
        var projectPath = TestUtils.CreateEmptyFile(projectDir, "project.proj");
        var projectInfo = TestUtils.CreateProjectInfoInSubDir(testDir, "project", null, Guid.NewGuid(), ProjectType.Product, false, projectPath, "UTF-8");
        string[] filesOutsideProjectPath = ["dllFile.dll", "exeFile.exe", "txtFile.txt", "foo.cs", "foo.DLL", "bar.EXE"];
        var filesToBeAnalyzedPaths = new List<string>();
        foreach (var fileName in filesOutsideProjectPath)
        {
            filesToBeAnalyzedPaths.Add(TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, fileName));
        }
        // To add the files above, to the list of files that are to be analyzed, you need to add their paths to
        // the "contentList.txt" which is placed inside the projectDir folder.
        var contentFileListPath = TestUtils.CreateFile(projectDir, "contentList.txt", string.Join(Environment.NewLine, filesToBeAnalyzedPaths));
        // Add the file path of "contentList.txt" to the projectInfo.xml
        TestUtils.AddAnalysisResult(projectInfo, AnalysisResultFileType.FilesToAnalyze, contentFileListPath);
        var config = CreateValidConfig(testDir);
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();

        AssertExpectedProjectCount(1, result);
        // The project has no files in its root dir and the rest of the files are outside of the root, thus ignored and not analyzed.
        AssertExpectedStatus("project", ProjectInfoValidity.NoFilesToAnalyze, result);
        runtime.Should().HaveWarningsLogged(2);
        runtime.Should().HaveWarningsLogged(
            $"File '{Path.Combine(TestContext.TestRunDirectory, "txtFile.txt")}' is not located under the base directory '{projectDir}' and will not be analyzed.",
            $"File '{Path.Combine(TestContext.TestRunDirectory, "foo.cs")}' is not located under the base directory '{projectDir}' and will not be analyzed.");
    }

    [TestMethod]
    [DataRow(new string[] { ".nuget", "packages" }, false)]
    [DataRow(new string[] { "packages" }, true)]
    [DataRow(new string[] { ".nugetpackages" }, true)]
    [DataRow(new string[] { ".nuget", "foo", "packages" }, true)]
    public void GenerateResult_FileOutOfProjectRootDir_WarningsAreNotLoggedForFilesInStandardNugetCache(string[] subDirNames, bool isRaisingAWarning)
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var dirOutOfProjectRoot = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, subDirNames);
        var projectDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
        var projectPath = TestUtils.CreateEmptyFile(projectDir, "project.proj");
        var projectInfo = TestUtils.CreateProjectInfoInSubDir(testDir, "project", null, Guid.NewGuid(), ProjectType.Product, false, projectPath, "UTF-8");
        var fileInNugetCache = TestUtils.CreateEmptyFile(dirOutOfProjectRoot, "foo.cs");
        // To add the files above, to the list of files that are to be analyzed, you need to add their paths to
        // the "contentList.txt" which is placed inside the projectDir folder.
        var contentFileListPath = TestUtils.CreateFile(projectDir, "contentList.txt", fileInNugetCache);
        // Add the file path of "contentList.txt" to the projectInfo.xml
        TestUtils.AddAnalysisResult(projectInfo, AnalysisResultFileType.FilesToAnalyze, contentFileListPath);
        var config = CreateValidConfig(testDir);
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();

        AssertExpectedProjectCount(1, result);
        // The project has no files in its root dir and the rest of the files are outside of the root, thus ignored and not analyzed.
        AssertExpectedStatus("project", ProjectInfoValidity.NoFilesToAnalyze, result);
        if (isRaisingAWarning)
        {
            runtime.Should().HaveWarningsLogged(1);
            runtime.Should().HaveSingleWarningLogged($"File '{Path.Combine(dirOutOfProjectRoot, "foo.cs")}' is not located under the base directory '{projectDir}' and will not be analyzed.");
        }
        else
        {
            runtime.Should().HaveWarningsLogged(0);
        }
    }

    [TestMethod]
    public void GenerateResult_SharedFiles()
    {
        // Shared files should be attached to the root project
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var project1Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project1");
        var project1Path = TestUtils.CreateEmptyFile(project1Dir, "project1.proj");
        var project1Info = TestUtils.CreateProjectInfoInSubDir(testDir, "projectName1", null, Guid.NewGuid(), ProjectType.Product, false, project1Path, "UTF-8"); // not excluded
        var sharedFile = TestUtils.CreateEmptyFile(testDir, "contentFile.txt");
        // Reference shared file, but not under the project directory
        var contentFileList1 = TestUtils.CreateFile(project1Dir, "contentList.txt", sharedFile);
        TestUtils.AddAnalysisResult(project1Info, AnalysisResultFileType.FilesToAnalyze, contentFileList1);
        var project2Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project2");
        var project2Path = TestUtils.CreateEmptyFile(project2Dir, "project2.proj");
        var project2Info = TestUtils.CreateProjectInfoInSubDir(testDir, "projectName2", null, Guid.NewGuid(), ProjectType.Product, false, project2Path, "UTF-8"); // not excluded
        // Reference shared file, but not under the project directory
        var contentFileList2 = TestUtils.CreateFile(project2Dir, "contentList.txt", sharedFile);
        TestUtils.AddAnalysisResult(project2Info, AnalysisResultFileType.FilesToAnalyze, contentFileList2);
        var config = CreateValidConfig(testDir);
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();

        var sqProperties = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        sqProperties.AssertSettingExists("sonar.projectBaseDir", testDir);
        sqProperties.AssertSettingExists("sonar.sources", AddQuotes(sharedFile));
        var reader = CreateInputReader(result);
        reader.AssertProperty("sonar.projectBaseDir", testDir);
        reader.AssertProperty("sonar.sources", sharedFile);
    }

    // SONARMSBRU-335 Case sensitive test is only relevant for Windows OS, as it is case insensitive by default
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void GenerateResult_SharedFiles_CaseInsensitive()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        // Create 2 UUIDs and order them so that test is reproducible
        var uuids = new[] { Guid.NewGuid(), Guid.NewGuid() }.OrderBy(x => x).ToArray();
        var project1Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project1");
        var project1Path = TestUtils.CreateEmptyFile(project1Dir, "project1.proj");
        var project1Info = TestUtils.CreateProjectInfoInSubDir(testDir, "projectName1", null, uuids[0], ProjectType.Product, false, project1Path, "UTF-8"); // not excluded
        var sharedFile = TestUtils.CreateEmptyFile(testDir, "contentFile.txt");
        var sharedFileDifferentCase = Path.Combine(testDir, "ContentFile.TXT");
        // Reference shared file, but not under the project directory
        var contentFileList1 = TestUtils.CreateFile(project1Dir, "contentList.txt", sharedFile);
        TestUtils.AddAnalysisResult(project1Info, AnalysisResultFileType.FilesToAnalyze, contentFileList1);
        var project2Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project2");
        var project2Path = TestUtils.CreateEmptyFile(project2Dir, "project2.proj");
        var project2Info = TestUtils.CreateProjectInfoInSubDir(testDir, "projectName2", null, uuids[1], ProjectType.Product, false, project2Path, "UTF-8"); // not excluded
        // Reference shared file, but not under the project directory
        var contentFileList2 = TestUtils.CreateFile(project2Dir, "contentList.txt", sharedFileDifferentCase);
        TestUtils.AddAnalysisResult(project2Info, AnalysisResultFileType.FilesToAnalyze, contentFileList2);
        var config = CreateValidConfig(testDir);
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();

        var sqProperties = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        sqProperties.AssertSettingExists("sonar.projectBaseDir", testDir);
        sqProperties.AssertSettingExists("sonar.sources", AddQuotes(sharedFile));   // First one wins
        var reader = CreateInputReader(result);
        reader.AssertProperty("sonar.projectBaseDir", testDir);
        reader.AssertProperty("sonar.sources", sharedFile);          // First one wins
    }

    // SONARMSBRU-336
    [TestMethod]
    public void GenerateResult_SharedFiles_BelongToAnotherProject()
    {
        // Shared files that belong to another project should NOT be attached to the root project
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var project1Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project1");
        var project1Path = TestUtils.CreateEmptyFile(project1Dir, "project1.proj");
        var project1Guid = Guid.NewGuid();
        var project1Info = TestUtils.CreateProjectInfoInSubDir(testDir, "projectName1", null, project1Guid, ProjectType.Product, false, project1Path, "UTF-8"); // not excluded
        var fileInProject1 = TestUtils.CreateEmptyFile(project1Dir, "contentFile.txt");
        // Reference shared file, but not under the project directory
        var contentFileList1 = TestUtils.CreateFile(project1Dir, "contentList.txt", fileInProject1);
        TestUtils.AddAnalysisResult(project1Info, AnalysisResultFileType.FilesToAnalyze, contentFileList1);
        var project2Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project2");
        var project2Path = TestUtils.CreateEmptyFile(project2Dir, "project2.proj");
        var project2Info = TestUtils.CreateProjectInfoInSubDir(testDir, "projectName2", null, Guid.NewGuid(), ProjectType.Product, false, project2Path, "UTF-8"); // not excluded
        // Reference shared file, but not under the project directory
        var contentFileList2 = TestUtils.CreateFile(project2Dir, "contentList.txt", fileInProject1);
        TestUtils.AddAnalysisResult(project2Info, AnalysisResultFileType.FilesToAnalyze, contentFileList2);
        var config = CreateValidConfig(testDir);
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();

        var sqProperties = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        sqProperties.AssertSettingExists("sonar.projectBaseDir", testDir);
        sqProperties.AssertSettingDoesNotExist("sonar.sources");
        sqProperties.AssertSettingExists(project1Guid.ToString().ToUpper() + ".sonar.sources", AddQuotes(fileInProject1));
        var reader = CreateInputReader(result);
        reader.AssertProperty("sonar.projectBaseDir", testDir);
        reader.AssertProperty("sonar.sources", string.Empty);
        reader.AssertProperty(project1Guid.ToString().ToUpper() + ".sonar.sources", fileInProject1);
    }

    [TestMethod] // https://jira.codehaus.org/browse/SONARMSBRU-13: Analysis fails if a content file referenced in the MSBuild project does not exist
    public void GenerateResult_MissingFilesAreSkipped()
    {
        // Create project info with a managed file list and a content file list.
        // Each list refers to a file that does not exist on disk.
        // The missing files should not appear in the generated properties file.
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var projectBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Project1");
        var projectFullPath = TestUtils.CreateEmptyFile(projectBaseDir, "project1.proj");
        var existingManagedFile = TestUtils.CreateEmptyFile(projectBaseDir, "File1.cs");
        var existingContentFile = TestUtils.CreateEmptyFile(projectBaseDir, "Content1.txt");
        var missingManagedFile = Path.Combine(projectBaseDir, "MissingFile1.cs");
        var missingContentFile = Path.Combine(projectBaseDir, "MissingContent1.txt");
        var projectInfo = new ProjectInfo
        {
            FullPath = projectFullPath,
            AnalysisResultFiles = [],
            IsExcluded = false,
            ProjectGuid = Guid.NewGuid(),
            ProjectName = "project1.proj",
            ProjectType = ProjectType.Product,
            Encoding = "UTF-8"
        };
        var analysisFileList = CreateFileList(projectBaseDir, AnalysisResultFileType.FilesToAnalyze.ToString(), existingManagedFile, missingManagedFile, existingContentFile, missingContentFile);
        projectInfo.AddAnalyzerResult(AnalysisResultFileType.FilesToAnalyze, analysisFileList);
        var projectInfoDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "ProjectInfo1Dir");
        var projectInfoFilePath = Path.Combine(projectInfoDir, FileConstants.ProjectInfoFileName);
        projectInfo.Save(projectInfoFilePath);
        var config = new AnalysisConfig
        {
            SonarQubeHostUrl = "http://sonarqube.com",
            SonarProjectKey = "my_project_key",
            SonarProjectName = "my_project_name",
            SonarProjectVersion = "1.0",
            SonarOutputDir = testDir
        };
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();
        var actual = File.ReadAllText(result.FullPropertiesFilePath);

        AssertFileIsReferenced(existingContentFile, actual);
        AssertFileIsReferenced(existingManagedFile, actual);
        AssertFileIsNotReferenced(missingContentFile, actual);
        AssertFileIsNotReferenced(missingManagedFile, actual);
        runtime.Should().HaveSingleWarningLogged($"File '{missingManagedFile}' does not exist.");
        runtime.Should().HaveSingleWarningLogged($"File '{missingContentFile}' does not exist.");
    }

    [TestMethod]
    [Description("Checks that the generated properties file contains additional properties")]
    public void GenerateResult_AdditionalProperties()
    {
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "project1", analysisRootDir);
        var config = CreateValidConfig(analysisRootDir);
        // Add additional properties
        config.LocalSettings = new AnalysisProperties
        {
            new("key1", "value1"),
            new("key.2", "value two"),
            new("key.3", " "),
            new(SonarProperties.SonarPassword, "secret pwd"),
            new(SonarProperties.SonarUserName, "secret username"),
            new(SonarProperties.SonarToken, "secret token"),
            new(SonarProperties.ClientCertPassword, "secret client certpwd")
        };
        // Server properties should not be added
        config.ServerSettings = [new("server.key", "should not be added")];
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();

        AssertExpectedProjectCount(1, result);
        // One valid project info file -> file created
        AssertScannerInputCreated(result);

        // Sensitive data should not be written to the SQProperties File
        var sqProperties = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        sqProperties.AssertSettingExists("key1", "value1");
        sqProperties.AssertSettingExists("key.2", "value two");
        sqProperties.AssertSettingExists("key.3", string.Empty);
        sqProperties.AssertSettingDoesNotExist(SonarProperties.SonarPassword);
        sqProperties.AssertSettingDoesNotExist(SonarProperties.SonarUserName);
        sqProperties.AssertSettingDoesNotExist(SonarProperties.SonarToken);
        sqProperties.AssertSettingDoesNotExist(SonarProperties.ClientCertPassword);
        sqProperties.AssertSettingDoesNotExist("server.key");

        // Sensitive data should be passed to the scanner-engine
        var reader = CreateInputReader(result);
        reader.AssertProperty("key1", "value1");
        reader.AssertProperty("key.2", "value two");
        reader.AssertProperty("key.3", " ");
        reader.AssertProperty(SonarProperties.SonarPassword, "secret pwd");
        reader.AssertProperty(SonarProperties.SonarUserName, "secret username");
        reader.AssertProperty(SonarProperties.SonarToken, "secret token");
        reader.AssertProperty(SonarProperties.ClientCertPassword, "secret client certpwd");
        reader.AssertPropertyDoesNotExist("server.key");
    }

    [TestMethod]
    public void GenerateResult_WhenNoGuid_NoWarnings()
    {
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "project1", null, analysisRootDir, Guid.Empty);
        var config = CreateValidConfig(analysisRootDir);
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();

        AssertExpectedProjectCount(1, result);
        // Empty guids are supported by generating them to the ProjectInfo.xml by WriteProjectInfoFile. In case it is not in ProjectInfo.xml, sonar-project.properties generation should fail.
        AssertFailedToCreateScannerInput(result);
        runtime.Logger.Warnings.Should().BeEmpty();
    }

    [TestMethod] // Old VS Bootstrapper should be forceably disabled: https://jira.sonarsource.com/browse/SONARMSBRU-122
    public void GenerateResult_VSBootstrapperIsDisabled()
    {
        var result = GenerateResultAndAssert("disableBootstrapper");

        var sqProperties = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        sqProperties.AssertSettingExists(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");
        CreateInputReader(result).AssertProperty(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");
        runtime.Should().HaveWarningsLogged(0);
    }

    [TestMethod]
    public void GenerateResult_VSBootstrapperIsDisabled_OverrideUserSettings_DifferentValue()
    {
        // Try to explicitly enable the setting
        var bootstrapperProperty = new Property(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "true");
        var result = GenerateResultAndAssert("disableBootstrapperDiff", bootstrapperProperty);

        var sqProperties = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        sqProperties.AssertSettingExists(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");
        CreateInputReader(result).AssertProperty(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");
        runtime.Should().HaveSingleWarningLogged("Overriding analysis property. Effective value: sonar.visualstudio.enable=false");
    }

    [TestMethod]
    public void GenerateResult_VSBootstrapperIsDisabled_OverrideUserSettings_SameValue()
    {
        var bootstrapperProperty = new Property(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");
        var result = GenerateResultAndAssert("disableBootstrapperSame", bootstrapperProperty);

        var sqProperties = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        sqProperties.AssertSettingExists(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");
        CreateInputReader(result).AssertProperty(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");
        runtime.Should().HaveDebugsLogged("Analysis property is already correctly set: sonar.visualstudio.enable=false");
        runtime.Should().HaveWarningsLogged(0); // not expecting a warning if the user has supplied the value we want
    }

    [TestMethod]
    public void GenerateResult_ComputeProjectBaseDir()
    {
        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("d"), "work", "mysources"), // if there is a user value, use it
            teamBuildValue: Path.Combine(TestUtils.DriveRoot("d"), "work"),
            userValue: Path.Combine(TestUtils.DriveRoot("d"), "work", "mysources"),
            projectPaths: [Path.Combine(TestUtils.DriveRoot("d"), "work", "proj1.csproj")]);

        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("d"), "work"),  // if no user value, use the team build value
            teamBuildValue: Path.Combine(TestUtils.DriveRoot("d"), "work"),
            userValue: null,
            projectPaths: [Path.Combine(TestUtils.DriveRoot("e"), "work")]);

        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("e"), "work"),  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [Path.Combine(TestUtils.DriveRoot("e"), "work")]);

        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("e"), "work"),  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [Path.Combine(TestUtils.DriveRoot("e"), "work"), Path.Combine(TestUtils.DriveRoot("e"), "work")]);

        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("e"), "work"),  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [Path.Combine(TestUtils.DriveRoot("e"), "work", "A"), Path.Combine(TestUtils.DriveRoot("e"), "work", "B", "C")]);

        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("e"), "work"),  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [Path.Combine(TestUtils.DriveRoot("e"), "work", "A"), Path.Combine(TestUtils.DriveRoot("e"), "work", "B"), Path.Combine(TestUtils.DriveRoot("e"), "work", "C")]);

        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("e"), "work", "A"),  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [Path.Combine(TestUtils.DriveRoot("e"), "work", "A", "X"), Path.Combine(TestUtils.DriveRoot("e"), "work", "A"), Path.Combine(TestUtils.DriveRoot("e"), "work", "A")]);

        // Support relative paths
        VerifyProjectBaseDir(
            expectedValue: Path.Combine(Directory.GetCurrentDirectory(), "src"),
            teamBuildValue: null,
            userValue: Path.Combine(".", "src"),
            projectPaths: [@"d:\work\proj1.csproj"]);
    }

    [TestMethod]
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    public void GenerateResult_ComputeProjectBaseDir_Windows()
    {
        VerifyProjectBaseDir(
            expectedValue: null,  // if no common root exists, return null
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [@"f:\work\A", @"e:\work\B"]);

        // Support short name paths
        var result = ComputeProjectBaseDir(
            teamBuildValue: null,
            userValue: @"C:\PROGRA~1",
            projectPaths: [@"d:\work\proj1.csproj"]);
        result.Should().BeOneOf(@"C:\Program Files", @"C:\Program Files (x86)");
    }

    [TestMethod]
    [DataRow(@"d:\work", @"d:\work\mysources", new[] { @"d:\work\proj1.csproj" }, false)]
    [DataRow(@"d:\work", null, new[] { @"e:\work" }, false)]
    [DataRow(null, "", new[] { @"e:\work" }, true)]
    [DataRow(null, "", new[] { @"e:\work", @"e:\work" }, true)]
    public void GenerateResult_LogsProjectBaseDirInfo(string teamBuildValue, string userValue, string[] projectPaths, bool shouldLog)
    {
        var config = new AnalysisConfig()
        {
            SonarOutputDir = TestSonarqubeOutputDir,
            SourcesDirectory = teamBuildValue,
            LocalSettings = [new(SonarProperties.ProjectBaseDir, userValue)]
        };
        new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).ComputeProjectBaseDir(projectPaths.Select(x => new DirectoryInfo(x)).ToList());

        if (shouldLog)
        {
            runtime.Should().HaveInfosLogged(ProjectBaseDirInfoMessage);
        }
        else
        {
            runtime.Should().NotHaveInfoLogged(ProjectBaseDirInfoMessage);
        }
    }

    [TestMethod]
    public void GenerateResult_AdditionalFiles_EndToEnd()
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
        string[] project2Tests =
        [
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project2), "project2.spec.tsx"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project2), "project2.test.tsx"),
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
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();

        AssertExpectedProjectCount(2, result);
        AssertScannerInputCreated(result);
        AssertExpectedStatus(project1, ProjectInfoValidity.Valid, result);
        AssertExpectedStatus(project2, ProjectInfoValidity.Valid, result);
        AssertExpectedPathsAddedToModuleFiles(project1, project1Sources);
        AssertExpectedPathsAddedToModuleFiles(project2, project2Sources);

        var properties = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        properties.PropertyValue("sonar.sources").Split(',').Select(x => x.Trim('\"')).Should().BeEquivalentTo(rootSources);
        properties.PropertyValue("sonar.tests").Split(',').Select(x => x.Trim('\"')).Should().BeEquivalentTo(rootTests.Concat(project2Tests));
        var reader = CreateInputReader(result);
        reader["sonar.sources"].Split(',').Select(x => x.Trim('\"')).Should().BeEquivalentTo(rootSources);
        reader["sonar.tests"].Split(',').Select(x => x.Trim('\"')).Should().BeEquivalentTo(rootTests.Concat(project2Tests));

        void AssertExpectedPathsAddedToModuleFiles(string projectId, string[] expectedPaths) =>
            expectedPaths.Should().BeSubsetOf(result.Projects.Single(x => x.Project.ProjectName == projectId).SonarQubeModuleFiles.Select(x => x.FullName));
    }

    [TestMethod]
    public void GenerateResult_AdditionalFiles_OnlyTestFiles_EndToEnd()
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
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();

        AssertExpectedProjectCount(1, result);
        AssertScannerInputCreated(result);
        AssertExpectedStatus(project1, ProjectInfoValidity.Valid, result);

        var properties = new SQPropertiesFileReader(result.FullPropertiesFilePath);
        properties.PropertyValue("sonar.tests").Split(',').Select(x => x.Trim('\"')).Should().BeEquivalentTo(testFiles);
        CreateInputReader(result)["sonar.tests"].Split(',').Select(x => x.Trim('\"')).Should().BeEquivalentTo(testFiles);
    }

    [TestMethod]
    public void GenerateResult_CommandLineArgs_AddedToEngineInput()
    {
        cmdLineArgs.Add(SonarProperties.SonarPassword, "secret pwd");
        cmdLineArgs.Add(SonarProperties.SonarUserName, "secret username");
        cmdLineArgs.Add(SonarProperties.SonarToken, "secret token");
        cmdLineArgs.Add(SonarProperties.ClientCertPassword, "secret client certpwd");
        cmdLineArgs.Add("sonar.some.other.arg", "someValue");

        var reader = CreateInputReader(new ScannerEngineInputGenerator(CreateValidConfig(), cmdLineArgs, runtime).GenerateResult());

        reader.AssertProperty(SonarProperties.SonarPassword, "secret pwd");
        reader.AssertProperty(SonarProperties.SonarUserName, "secret username");
        reader.AssertProperty(SonarProperties.SonarToken, "secret token");
        reader.AssertProperty(SonarProperties.ClientCertPassword, "secret client certpwd");
        reader.AssertProperty("sonar.some.other.arg", "someValue");
    }

    [TestMethod]
    public void GenerateResult_AnalysisConfigSensitiveArgs_AddedToEngineInput()
    {
        var config = CreateValidConfig();
        config.LocalSettings =
        [
            new(SonarProperties.SonarPassword, "secret pwd"),
            new(SonarProperties.SonarUserName, "secret username"),
            new(SonarProperties.SonarToken, "secret token"),
            new(SonarProperties.ClientCertPassword, "secret client certpwd"),
            new("sonar.some.other.arg", "someValue")
        ];
        var reader = CreateInputReader(new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult());

        reader.AssertProperty(SonarProperties.SonarPassword, "secret pwd");
        reader.AssertProperty(SonarProperties.SonarUserName, "secret username");
        reader.AssertProperty(SonarProperties.SonarToken, "secret token");
        reader.AssertProperty(SonarProperties.ClientCertPassword, "secret client certpwd");
        reader.AssertProperty("sonar.some.other.arg", "someValue");
    }

    [TestMethod]
    public void GenerateResult_CommandLineArgs_OverrideFileProperties()
    {
        cmdLineArgs.Add(SonarProperties.SonarPassword, "cli pwd");
        cmdLineArgs.Add(SonarProperties.SonarUserName, "cli username");
        cmdLineArgs.Add(SonarProperties.SonarToken, "cli token");
        cmdLineArgs.Add(SonarProperties.ClientCertPassword, "cli client certpwd");
        cmdLineArgs.Add("sonar.some.other.arg", "cliValue");

        var config = CreateValidConfig();
        config.LocalSettings =
        [
            new(SonarProperties.SonarPassword, "file pwd"),
            new(SonarProperties.SonarUserName, "file username"),
            new(SonarProperties.SonarToken, "file token"),
            new(SonarProperties.ClientCertPassword, "file client certpwd"),
            new("sonar.some.other.arg", "fileValue")
        ];
        var reader = CreateInputReader(new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult());

        reader.AssertProperty(SonarProperties.SonarPassword, "cli pwd");
        reader.AssertProperty(SonarProperties.SonarUserName, "cli username");
        reader.AssertProperty(SonarProperties.SonarToken, "cli token");
        reader.AssertProperty(SonarProperties.ClientCertPassword, "cli client certpwd");
        reader.AssertProperty("sonar.some.other.arg", "cliValue");
    }

    /// <summary>
    /// Creates a single new project valid project with dummy files and analysis config file with the specified local settings.
    /// Checks that a property file is created.
    /// </summary>
    private AnalysisResult GenerateResultAndAssert(string projectName, params Property[] localSettings)
    {
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, projectName);
        TestUtils.CreateProjectWithFiles(TestContext, projectName, analysisRootDir);
        var config = CreateValidConfig(analysisRootDir);
        config.LocalSettings = [.. localSettings];
        var result = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).GenerateResult();

        AssertExpectedProjectCount(1, result);
        AssertScannerInputCreated(result);
        return result;
    }

    private static ScannerEngineInputReader CreateInputReader(AnalysisResult result) =>
        new(result.ScannerEngineInput.ToString());
}
