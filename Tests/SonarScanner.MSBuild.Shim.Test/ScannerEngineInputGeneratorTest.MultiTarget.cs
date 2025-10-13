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
    public void Same_Files_In_All_Targets_Are_Not_Duplicated()
    {
        var guid = Guid.NewGuid();
        /*  Solution1/
                .sonarqube/
                    conf/
                    out/
                        Project1_Debug/
                            ProjectInfo.xml
                            FilesToAnalyze.txt (file1.cs, file2.cs)
                        Project1_Release/
                            ProjectInfo.xml
                            FilesToAnalyze.txt (file1.cs, file2.cs)
                Project1/
                    file1.cs
                    file2.cs
        */
        var solutionDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Solution1");
        var projectRoot = CreateProject(solutionDir, out var files);
        var outDir = Path.Combine(solutionDir, ".sonarqube", "out");
        Directory.CreateDirectory(outDir);
        // Create two out folders for each configuration, all files in the projects were included in the analysis
        CreateProjectInfoAndFilesToAnalyze(guid, "Debug", files, projectRoot, outDir);
        CreateProjectInfoAndFilesToAnalyze(guid, "Release", files, projectRoot, outDir);
        var config = new AnalysisConfig
        {
            SonarProjectKey = guid.ToString(),
            SonarProjectName = "project 1",
            SonarOutputDir = outDir,
            SonarProjectVersion = "1.0",
        };
        var generator = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime);
        var result = generator.GenerateResult(runtime.DateTime.OffsetNow);

        AssertExpectedProjectCount(1, result);

        // One valid project info file -> file created
        AssertScannerInputCreated(result);
        var propertiesFileContent = File.ReadAllText(result.FullPropertiesFilePath);
        AssertFileIsReferenced(files[0], propertiesFileContent);
        AssertFileIsReferenced(files[1], propertiesFileContent);
    }

    [TestMethod]
    public void Different_Files_Per_Target_Are_Merged()
    {
        var guid = Guid.NewGuid();
        /*  Solution1/
                .sonarqube/
                    conf/
                    out/
                        Project1_Debug/
                            ProjectInfo.xml
                            FilesToAnalyze.txt (file1.cs)
                        Project1_Release/
                            ProjectInfo.xml
                            FilesToAnalyze.txt (file2.cs)
                Project1/
                    file1.cs // included in Debug build
                    file2.cs // included in Release build
        */
        var solutionDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Solution1");
        var projectRoot = CreateProject(solutionDir, out var files);
        var outDir = Path.Combine(solutionDir, ".sonarqube", "out");
        Directory.CreateDirectory(outDir);
        // Create two out folders for each configuration, the included files are different
        CreateProjectInfoAndFilesToAnalyze(guid, "Debug", files.Take(1), projectRoot, outDir);
        CreateProjectInfoAndFilesToAnalyze(guid, "Release", files.Skip(1), projectRoot, outDir);
        var config = new AnalysisConfig
        {
            SonarProjectKey = guid.ToString(),
            SonarProjectName = "project 1",
            SonarOutputDir = outDir,
            SonarProjectVersion = "1.0",
        };
        var generator = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime);
        var result = generator.GenerateResult(runtime.DateTime.OffsetNow);

        AssertExpectedProjectCount(1, result);
        // One valid project info file -> file created
        AssertScannerInputCreated(result);
        var propertiesFileContent = File.ReadAllText(result.FullPropertiesFilePath);
        AssertFileIsReferenced(files[0], propertiesFileContent);
        AssertFileIsReferenced(files[1], propertiesFileContent);
    }

    [TestMethod]
    public void Different_Files_Per_Target_The_Ignored_Compilations_Are_Not_Included()
    {
        var guid = Guid.NewGuid();
        /*  Solution1/
                .sonarqube/
                    conf/
                    out/
                        Project1_Debug/
                            ProjectInfo.xml
                            FilesToAnalyze.txt (file1.cs)
                        Project1_Release/ // ignored, file2.cs should not be included in the properties file
                            ProjectInfo.xml
                            FilesToAnalyze.txt (file2.cs)
                Project1/
                    file1.cs // included in Debug
                    file2.cs // included in Release
        */
        var solutionDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Solution1");
        var projectRoot = CreateProject(solutionDir, out var files);
        var outDir = Path.Combine(solutionDir, ".sonarqube", "out");
        Directory.CreateDirectory(outDir);
        // Create two out folders for each configuration, the included files are different
        CreateProjectInfoAndFilesToAnalyze(guid, "Debug", files.Take(1), projectRoot, outDir);
        CreateProjectInfoAndFilesToAnalyze(guid, "Release", files.Skip(1), projectRoot, outDir, isExcluded: true);
        var config = new AnalysisConfig
        {
            SonarProjectKey = guid.ToString(),
            SonarProjectName = "project 1",
            SonarOutputDir = outDir,
            SonarProjectVersion = "1.0",
        };
        var generator = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime);
        var result = generator.GenerateResult(runtime.DateTime.OffsetNow);

        AssertExpectedProjectCount(1, result);
        // One valid project info file -> file created
        AssertScannerInputCreated(result);
        var propertiesFileContent = File.ReadAllText(result.FullPropertiesFilePath);
        AssertFileIsReferenced(files[0], propertiesFileContent);
        AssertFileIsNotReferenced(files[1], propertiesFileContent);
    }

    [TestMethod]
    public void All_Targets_Excluded_Compilations_Are_Not_Included()
    {
        var guid = Guid.NewGuid();
        /*  Solution1/
                .sonarqube/
                    conf/
                    out/
                        Project1_Debug/
                            ProjectInfo.xml
                            FilesToAnalyze.txt (file1.cs)
                        Project1_Release/ // ignored, file2.cs should not be included in the properties file
                            ProjectInfo.xml
                            FilesToAnalyze.txt (file2.cs)
                Project1/
                    file1.cs // included in Debug
                    file2.cs // included in Release
        */
        var solutionDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Solution1");
        var projectRoot = CreateProject(solutionDir, out var files);
        var outDir = Path.Combine(solutionDir, ".sonarqube", "out");
        Directory.CreateDirectory(outDir);
        // Create two out folders for each configuration, the included files are different
        CreateProjectInfoAndFilesToAnalyze(guid, "Debug", files.Take(1), projectRoot, outDir, isExcluded: true);
        CreateProjectInfoAndFilesToAnalyze(guid, "Release", files.Skip(1), projectRoot, outDir, isExcluded: true);
        var config = new AnalysisConfig
        {
            SonarProjectKey = guid.ToString(),
            SonarProjectName = "project 1",
            SonarOutputDir = outDir,
            SonarProjectVersion = "1.0",
        };
        var generator = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime);
        var result = generator.GenerateResult(runtime.DateTime.OffsetNow);

        AssertExpectedProjectCount(1, result);
        // No valid project info files -> properties not created
        AssertFailedToCreateScannerInput(result);
    }

    private static void CreateProjectInfoAndFilesToAnalyze(Guid guid,
                                                           string configuration,
                                                           IEnumerable<string> files,
                                                           string projectRoot,
                                                           string outDir,
                                                           bool isExcluded = false)
    {
        // Create FilesToAnalyze.txt in each folder, they are the same, because are the result of the compilation of the same project
        var projectOutDir = Path.Combine(outDir, $"Project1_{configuration}");
        var filesToAnalyze = TestUtils.CreateFile(projectOutDir, "FilesToAnalyze.txt", string.Join(Environment.NewLine, files));
        // Create project info for the configuration, the project path is important, the name is ignored
        var projectInfo = new ProjectInfo
        {
            FullPath = Path.Combine(projectRoot, "Project1.csproj"),
            ProjectGuid = guid,
            ProjectName = "Project1.csproj",
            ProjectType = ProjectType.Product,
            Encoding = "UTF-8",
            IsExcluded = isExcluded,
            AnalysisResultFiles = [new(AnalysisResultFileType.FilesToAnalyze, filesToAnalyze)]
        };
        TestUtils.CreateEmptyFile(projectRoot, "Project1.csproj");
        projectInfo.Save(Path.Combine(projectOutDir, FileConstants.ProjectInfoFileName));
    }

    private static string CreateProject(string destination, out List<string> files)
    {
        var projectRoot = Path.Combine(destination, "Project1");
        Directory.CreateDirectory(projectRoot);
        files = [
            TestUtils.CreateEmptyFile(projectRoot, "file1.cs"),
            TestUtils.CreateEmptyFile(projectRoot, "file2.cs")
        ];
        return projectRoot;
    }
}
