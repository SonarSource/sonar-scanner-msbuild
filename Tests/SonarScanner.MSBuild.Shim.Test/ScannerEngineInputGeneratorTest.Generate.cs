/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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
    public void Generate_NoProjectInfoFiles()
    {
        // ScannerEngineInput should not be generated if there are no project info files.
        // Two sub-directories, neither containing a ProjectInfo.xml
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var subDir1 = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "dir1");
        var subDir2 = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "dir2");
        TestUtils.CreateEmptyFile(subDir1, "file1.txt");
        TestUtils.CreateEmptyFile(subDir2, "file2.txt");
        var config = new AnalysisConfig { SonarOutputDir = testDir, SonarQubeHostUrl = "http://sonarqube.com" };

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        AssertFailedToCreateScannerInput(scannerInput, """
            The SonarScanner for .NET integration failed: SonarQube was unable to collect the required information about your projects.
            Possible causes:
              1. The project has not been built - the project must be built in between the begin and end steps.
              2. An unsupported version of MSBuild has been used to build the project. Supported versions: MSBuild 16 and higher.
              3. The begin, build and end steps have not all been launched from the same folder.
              4. None of the analyzed projects have a valid ProjectGuid and you have not used a solution (.sln).
            """);
    }

    [TestMethod]
    public void Generate_ValidFiles()
    {
        // Only non-excluded projects with files to analyze should be marked as valid
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var withoutFilesDir = Path.Combine(testDir, "withoutFiles");
        var withoutFilesGuid = Guid.NewGuid();
        var withFiles1Guid = Guid.NewGuid();
        var withFiles2Guid = Guid.NewGuid();
        Directory.CreateDirectory(withoutFilesDir);
        TestUtils.CreateProjectInfoInSubDir(testDir, "withoutFiles", null, withoutFilesGuid, ProjectType.Product, false, Path.Combine(withoutFilesDir, "withoutFiles.proj"), "UTF-8"); // not excluded
        TestUtils.CreateEmptyFile(withoutFilesDir, "withoutFiles.proj");
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", null, testDir, projectGuid: withFiles1Guid);
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles2", null, testDir, projectGuid: withFiles2Guid);
        var config = CreateValidConfig(testDir);

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        AssertModules(scannerInput, withFiles1Guid, withFiles2Guid);
        var reader = CreateInputReader(scannerInput);
        reader.AssertProperty($"{withFiles1Guid.ToString().ToUpper()}.sonar.projectBaseDir", $"{testDir}{Path.DirectorySeparatorChar}projects{Path.DirectorySeparatorChar}withFiles1");
        reader.AssertProperty($"{withFiles1Guid.ToString().ToUpper()}.sonar.tests", string.Empty);
        reader.AssertProperty($"{withFiles1Guid.ToString().ToUpper()}.sonar.sources", $"{testDir}{Path.DirectorySeparatorChar}projects{Path.DirectorySeparatorChar}withFiles1{Path.DirectorySeparatorChar}contentFile1.txt");
        reader.AssertProperty($"{withFiles2Guid.ToString().ToUpper()}.sonar.projectBaseDir", $"{testDir}{Path.DirectorySeparatorChar}projects{Path.DirectorySeparatorChar}withFiles2");
        reader.AssertProperty($"{withFiles2Guid.ToString().ToUpper()}.sonar.tests", string.Empty);
        reader.AssertProperty($"{withFiles2Guid.ToString().ToUpper()}.sonar.sources", $"{testDir}{Path.DirectorySeparatorChar}projects{Path.DirectorySeparatorChar}withFiles2{Path.DirectorySeparatorChar}contentFile1.txt");
    }

    [TestMethod]
    public void Generate_Csproj_DoesNotExist()
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

        var scannerInput = CreateSut(config).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        AssertFailedToCreateScannerInput(scannerInput);
    }

    [TestMethod]
    [CombinatorialData]
    public void Generate_Duplicate_SameGuid_DifferentCase(PlatformOS os)
    {
        var guid = Guid.NewGuid();
        var testRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Projects");
        var projectFileOrig = CreateProject("Project1", "DifferentCasing.proj");
        var projectFileDiff = CreateProject("Project2", "dIFFERENTcASING.proj");    // Same file for windows, different for Unix
        var config = CreateValidConfig(testRootDir);

        var scannerInput = CreateSut(config, os: os).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        if (os == PlatformOS.Windows)
        {
            AssertModules(scannerInput, guid);
            runtime.Logger.Warnings.Should().BeEmpty("Windows is case insensitive and all project files are considered the same");
        }
        else
        {
            // Casing should not be ignored on non-windows OS, none of those two different project files with the same GUID will be analyzed
            AssertFailedToCreateScannerInput(scannerInput);
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
    public void Generate_ValidFiles_SourceEncoding_Provided()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);
        var config = CreateValidConfig(testDir);
        config.LocalSettings = [new(SonarProperties.SourceEncoding, "test-encoding-here")];

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        var reader = CreateInputReader(scannerInput);
        reader.AssertProperty(SonarProperties.SourceEncoding, "test-encoding-here");     // Global setting is passed to the scanner engine
    }

    [TestMethod]
    public void Generate_TFS_Coverage_TrxAreWritten()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);
        var config = CreateValidConfig(testDir);
        config.LocalSettings = [
            new(SonarProperties.VsCoverageXmlReportsPaths, "coverage-path"),
            new(SonarProperties.VsTestReportsPaths, "trx-path"),
        ];

        var scannerInput = CreateSut(config).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        var reader = CreateInputReader(scannerInput);
        reader.AssertProperty(SonarProperties.VsCoverageXmlReportsPaths, "coverage-path");
        reader.AssertProperty(SonarProperties.VsTestReportsPaths, "trx-path");
    }

    [TestMethod]
    public void Generate_FilesOutOfProjectRootDir()
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

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        // The project has no files in its root dir and the rest of the files are outside of the root, thus ignored and not analyzed.
        AssertFailedToCreateScannerInput(scannerInput);
        runtime.Logger.Should().HaveWarnings(2)
            .And.HaveWarnings(
            $"File '{Path.Combine(TestContext.TestRunDirectory, "txtFile.txt")}' is not located under the base directory '{projectDir}' and will not be analyzed.",
            $"File '{Path.Combine(TestContext.TestRunDirectory, "foo.cs")}' is not located under the base directory '{projectDir}' and will not be analyzed.");
    }

    [TestMethod]
    [DataRow(new string[] { ".nuget", "packages" }, false)]
    [DataRow(new string[] { "packages" }, true)]
    [DataRow(new string[] { ".nugetpackages" }, true)]
    [DataRow(new string[] { ".nuget", "foo", "packages" }, true)]
    public void Generate_FileOutOfProjectRootDir_WarningsAreNotLoggedForFilesInStandardNugetCache(string[] subDirNames, bool isRaisingAWarning)
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

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        // The project has no files in its root dir and the rest of the files are outside of the root, thus ignored and not analyzed.
        AssertFailedToCreateScannerInput(scannerInput);
        if (isRaisingAWarning)
        {
            runtime.Logger.Should().HaveWarnings(1)
                .And.HaveWarnings($"File '{Path.Combine(dirOutOfProjectRoot, "foo.cs")}' is not located under the base directory '{projectDir}' and will not be analyzed.");
        }
        else
        {
            runtime.Logger.Should().HaveNoWarnings();
        }
    }

    [TestMethod]
    public void Generate_AppIdentifier()
    {
        var config = CreateValidConfig();

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        var reader = CreateInputReader(scannerInput);
        reader.AssertProperty("sonar.scanner.app", "ScannerMSBuild");
        reader.AssertProperty("sonar.scanner.appVersion", Utilities.ScannerVersion);
    }

    [TestMethod]
    public void Generate_SharedFiles()
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

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        var reader = CreateInputReader(scannerInput);
        reader.AssertProperty("sonar.projectBaseDir", testDir);
        reader.AssertProperty("sonar.sources", sharedFile);
    }

    // SONARMSBRU-335 Case sensitive test is only relevant for Windows OS, as it is case insensitive by default
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void Generate_SharedFiles_CaseInsensitive()
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

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        var reader = CreateInputReader(scannerInput);
        reader.AssertProperty("sonar.projectBaseDir", testDir);
        reader.AssertProperty("sonar.sources", sharedFile);          // First one wins
    }

    // SONARMSBRU-336
    [TestMethod]
    public void Generate_SharedFiles_BelongToAnotherProject()
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

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        var reader = CreateInputReader(scannerInput);
        reader.AssertProperty("sonar.projectBaseDir", testDir);
        reader.AssertProperty("sonar.sources", string.Empty);
        reader.AssertProperty(project1Guid.ToString().ToUpper() + ".sonar.sources", fileInProject1);
    }

    [TestMethod] // https://jira.codehaus.org/browse/SONARMSBRU-13: Analysis fails if a content file referenced in the MSBuild project does not exist
    public void Generate_MissingFilesAreSkipped()
    {
        // Create project info with a managed file list and a content file list.
        // Each list refers to a file that does not exist on disk.
        // The missing files should not appear in the generated properties file.
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
        var config = CreateValidConfig();

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        CreateInputReader(scannerInput).AssertProperty($"{projectInfo.ProjectGuid.ToString().ToUpper()}.sonar.sources", string.Join(",", [existingManagedFile, existingContentFile]));
        runtime.Logger.Should().HaveWarnings(
            $"File '{missingManagedFile}' does not exist.",
            $"File '{missingContentFile}' does not exist.");
    }

    [TestMethod]
    public void Generate_AdditionalProperties()
    {
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var guid = Guid.NewGuid();
        TestUtils.CreateProjectWithFiles(TestContext, "project1", null, analysisRootDir, guid);
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

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        AssertModules(scannerInput, guid);
        // Sensitive data should be passed to the scanner-engine
        var reader = CreateInputReader(scannerInput);
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
    public void Generate_WhenNoGuid_NoWarnings()
    {
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "project1", null, analysisRootDir, Guid.Empty);
        var config = CreateValidConfig(analysisRootDir);

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        // Empty guids are supported by generating them to the ProjectInfo.xml by WriteProjectInfoFile. In case it is not in ProjectInfo.xml, ScannerEngineInput generation should fail.
        AssertFailedToCreateScannerInput(scannerInput);
        runtime.Logger.Warnings.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_AdditionalFiles_EndToEnd()
    {
        var project1 = "project1";
        var project2 = "project2";
        var project1Guid = Guid.NewGuid();
        var project2Guid = Guid.NewGuid();
        var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var rootProjects = Path.Combine(root, "projects");
        TestUtils.CreateProjectWithFiles(TestContext, project1, null, root, project1Guid);
        TestUtils.CreateProjectWithFiles(TestContext, project2, null, root, project2Guid);
        string[] rootSources =
        [
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.ipynb"),
            TestUtils.CreateEmptyFile(rootProjects, "rootSource.gsx"),
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
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project1), "project1.gsx"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project1), "project1.sql"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project1), "project1.py"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project1), "project1.ipynb"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project1), "project1.ts"),
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project1), "project1.php"),
        ];
        string[] project2Sources =
        [
            TestUtils.CreateEmptyFile(Path.Combine(rootProjects, project2), "project2.gsx"),
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
            new("sonar.gosu.file.suffixes", "gsx"),
            new("sonar.typescript.file.suffixes", ".ts,.tsx"),
            new("sonar.postgres.file.suffixes", "sql"),
            new("sonar.python.file.suffixes", "py"),
            new("sonar.ipynb.file.suffixes", "ipynb"),
            new("sonar.php.file.suffixes", "php"),
        ];
        var config = CreateValidConfig(root, serverProperties);

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        AssertModules(scannerInput, project1Guid, project2Guid);
        var reader = CreateInputReader(scannerInput);
        AssertExpectedPathsAddedToModuleFiles(project1Guid, project1Sources);
        AssertExpectedPathsAddedToModuleFiles(project2Guid, project2Sources);
        reader["sonar.sources"].Split(',').Select(x => x.Trim('\"')).Should().BeEquivalentTo(rootSources);
        reader["sonar.tests"].Split(',').Select(x => x.Trim('\"')).Should().BeEquivalentTo(rootTests.Concat(project2Tests));

        void AssertExpectedPathsAddedToModuleFiles(Guid projectGuid, string[] expectedPaths) =>
            expectedPaths.Should().BeSubsetOf(reader[$"{projectGuid.ToString().ToUpper()}.sonar.sources"].Split(',').Select(x => x.Trim('\"')));
    }

    [TestMethod]
    public void Generate_AdditionalFiles_OnlyTestFiles_EndToEnd()
    {
        var project1 = "project1";
        var project1Guid = Guid.NewGuid();
        var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var rootProjects = Path.Combine(root, "projects");
        TestUtils.CreateProjectWithFiles(TestContext, project1, null, root, project1Guid);
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

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        AssertModules(scannerInput, project1Guid);
        CreateInputReader(scannerInput)["sonar.tests"].Split(',').Select(x => x.Trim('\"')).Should().BeEquivalentTo(testFiles);
    }

    [TestMethod]
    public void Generate_CommandLineArgs_AddedToEngineInput()
    {
        cmdLineArgs.Add(SonarProperties.SonarPassword, "secret pwd");
        cmdLineArgs.Add(SonarProperties.SonarUserName, "secret username");
        cmdLineArgs.Add(SonarProperties.SonarToken, "secret token");
        cmdLineArgs.Add(SonarProperties.ClientCertPassword, "secret client certpwd");
        cmdLineArgs.Add("sonar.some.other.arg", "someValue");

        var config = CreateValidConfig();
        var reader = CreateInputReader(new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow));

        reader.AssertProperty(SonarProperties.SonarPassword, "secret pwd");
        reader.AssertProperty(SonarProperties.SonarUserName, "secret username");
        reader.AssertProperty(SonarProperties.SonarToken, "secret token");
        reader.AssertProperty(SonarProperties.ClientCertPassword, "secret client certpwd");
        reader.AssertProperty("sonar.some.other.arg", "someValue");
    }

    [TestMethod]
    public void Generate_AnalysisConfigSensitiveArgs_AddedToEngineInput()
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
        var reader = CreateInputReader(new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow));

        reader.AssertProperty(SonarProperties.SonarPassword, "secret pwd");
        reader.AssertProperty(SonarProperties.SonarUserName, "secret username");
        reader.AssertProperty(SonarProperties.SonarToken, "secret token");
        reader.AssertProperty(SonarProperties.ClientCertPassword, "secret client certpwd");
        reader.AssertProperty("sonar.some.other.arg", "someValue");
    }

    [TestMethod]
    public void Generate_CommandLineArgs_OverrideFileProperties()
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
        var reader = CreateInputReader(new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow));

        reader.AssertProperty(SonarProperties.SonarPassword, "cli pwd");
        reader.AssertProperty(SonarProperties.SonarUserName, "cli username");
        reader.AssertProperty(SonarProperties.SonarToken, "cli token");
        reader.AssertProperty(SonarProperties.ClientCertPassword, "cli client certpwd");
        reader.AssertProperty("sonar.some.other.arg", "cliValue");
    }

    /// <summary>
    /// Creates a single new project valid project with dummy files and analysis config file with the specified local settings.
    /// Checks that ScannerEngineInput is created.
    /// </summary>
    private ScannerEngineInput GenerateScannerInputAndAssert(string projectName, params Property[] localSettings)
    {
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, projectName);
        var guid = Guid.NewGuid();
        TestUtils.CreateProjectWithFiles(TestContext, projectName, null, analysisRootDir, guid);
        var config = CreateValidConfig(analysisRootDir);
        config.LocalSettings = [.. localSettings];

        var scannerInput = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        AssertModules(scannerInput, guid);
        return scannerInput;
    }

    private static ScannerEngineInputReader CreateInputReader(ScannerEngineInput scannerInput) =>
        new(scannerInput.ToString());
}
