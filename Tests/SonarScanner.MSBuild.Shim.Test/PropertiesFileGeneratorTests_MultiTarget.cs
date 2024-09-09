/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class PropertiesFileGeneratorTests_MultiTarget
{
    public TestContext TestContext { get; set; }

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

        // Arrange
        var solutionDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Solution1");

        var projectRoot = CreateProject(solutionDir, out List<string> files);

        var outDir = Path.Combine(solutionDir, ".sonarqube", "out");
        Directory.CreateDirectory(outDir);

        // Create two out folders for each configuration, all files in the projects were included in the analysis
        CreateProjectInfoAndFilesToAnalyze(guid, "Debug", files, projectRoot, outDir);
        CreateProjectInfoAndFilesToAnalyze(guid, "Release", files, projectRoot, outDir);

        var logger = new TestLogger();
        var config = new AnalysisConfig()
        {
            SonarProjectKey = guid.ToString(),
            SonarProjectName = "project 1",
            SonarOutputDir = outDir,
            SonarProjectVersion = "1.0",
        };

        var generator = new PropertiesFileGenerator(config, logger);

        // Act
        var result = generator.GenerateFile();

        // Assert
        AssertExpectedProjectCount(1, result);

        // One valid project info file -> file created
        AssertPropertiesFilesCreated(result, logger);
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

        // Arrange
        var solutionDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Solution1");

        var projectRoot = CreateProject(solutionDir, out List<string> files);

        var outDir = Path.Combine(solutionDir, ".sonarqube", "out");
        Directory.CreateDirectory(outDir);

        // Create two out folders for each configuration, the included files are different
        CreateProjectInfoAndFilesToAnalyze(guid, "Debug", files.Take(1), projectRoot, outDir);
        CreateProjectInfoAndFilesToAnalyze(guid, "Release", files.Skip(1), projectRoot, outDir);

        var logger = new TestLogger();
        var config = new AnalysisConfig()
        {
            SonarProjectKey = guid.ToString(),
            SonarProjectName = "project 1",
            SonarOutputDir = outDir,
            SonarProjectVersion = "1.0",
        };

        var generator = new PropertiesFileGenerator(config, logger);

        // Act
        var result = generator.GenerateFile();

        // Assert
        AssertExpectedProjectCount(1, result);

        // One valid project info file -> file created
        AssertPropertiesFilesCreated(result, logger);
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

        // Arrange
        var solutionDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Solution1");

        var projectRoot = CreateProject(solutionDir, out List<string> files);

        var outDir = Path.Combine(solutionDir, ".sonarqube", "out");
        Directory.CreateDirectory(outDir);

        // Create two out folders for each configuration, the included files are different
        CreateProjectInfoAndFilesToAnalyze(guid, "Debug", files.Take(1), projectRoot, outDir);
        CreateProjectInfoAndFilesToAnalyze(guid, "Release", files.Skip(1), projectRoot, outDir, isExcluded: true);

        var logger = new TestLogger();
        var config = new AnalysisConfig()
        {
            SonarProjectKey = guid.ToString(),
            SonarProjectName = "project 1",
            SonarOutputDir = outDir,
            SonarProjectVersion = "1.0",
        };

        var generator = new PropertiesFileGenerator(config, logger);

        // Act
        var result = generator.GenerateFile();

        // Assert
        AssertExpectedProjectCount(1, result);

        // One valid project info file -> file created
        AssertPropertiesFilesCreated(result, logger);
        var propertiesFileContent = File.ReadAllText(result.FullPropertiesFilePath);
        AssertFileIsReferenced(files[0], propertiesFileContent);
        AssertFileIsReferenced(files[1], propertiesFileContent, 0);
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

        // Arrange
        var solutionDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Solution1");

        var projectRoot = CreateProject(solutionDir, out var files);

        var outDir = Path.Combine(solutionDir, ".sonarqube", "out");
        Directory.CreateDirectory(outDir);

        // Create two out folders for each configuration, the included files are different
        CreateProjectInfoAndFilesToAnalyze(guid, "Debug", files.Take(1), projectRoot, outDir, isExcluded: true);
        CreateProjectInfoAndFilesToAnalyze(guid, "Release", files.Skip(1), projectRoot, outDir, isExcluded: true);

        var logger = new TestLogger();
        var config = new AnalysisConfig
        {
            SonarProjectKey = guid.ToString(),
            SonarProjectName = "project 1",
            SonarOutputDir = outDir,
            SonarProjectVersion = "1.0",
        };

        var generator = new PropertiesFileGenerator(config, logger);

        // Act
        var result = generator.GenerateFile();

        // Assert
        AssertExpectedProjectCount(1, result);

        // No valid project info files -> properties not created
        AssertPropertiesFilesNotCreated(result, logger);
    }

    private static void CreateProjectInfoAndFilesToAnalyze(Guid guid,
                                                           string configuration,
                                                           IEnumerable<string> files,
                                                           string projectRoot,
                                                           string outDir,
                                                           bool isExcluded = false)
    {
        var @out = Path.Combine(outDir, $"Project1_{configuration}");

        Directory.CreateDirectory(@out);

        // Create FilesToAnalyze.txt in each folder, they are the same,
        // because are the result of the compilation of the same project
        var filesToAnalyze_txt = Path.Combine(@out, TestUtils.FilesToAnalyze);
        File.WriteAllLines(filesToAnalyze_txt, files.ToArray());

        // Create project info for the configuration, the project path is important, the name is ignored
        var projectInfo = new ProjectInfo
        {
            FullPath = Path.Combine(projectRoot, "Project1.csproj"),
            ProjectGuid = guid,
            ProjectName = "Project1.csproj",
            ProjectType = ProjectType.Product,
            Encoding = "UTF-8",
            IsExcluded = isExcluded,
            AnalysisResults = [new() { Id = TestUtils.FilesToAnalyze, Location = filesToAnalyze_txt }]
        };
        TestUtils.CreateEmptyFile(projectRoot, "Project1.csproj");
        projectInfo.Save(Path.Combine(@out, FileConstants.ProjectInfoFileName));
    }

    private static string CreateProject(string destination, out List<string> files)
    {
        var projectRoot = Path.Combine(destination, "Project1");
        Directory.CreateDirectory(projectRoot);
        files =
        [
            CreateFile(projectRoot, "file1.cs"),
            CreateFile(projectRoot, "file2.cs")
        ];
        return projectRoot;
    }

    private static void AssertFileIsReferenced(string fullFilePath, string content, int times = 1)
    {
        var formattedPath = PropertiesWriter.Escape(fullFilePath);

        var index = content.IndexOf(formattedPath);
        if (times == 0)
        {
            index.Should().Be(-1, $"File should not be referenced: {formattedPath}");
        }
        else
        {
            index.Should().NotBe(-1, $"File should be referenced: {formattedPath}");

            for (var i = 0; i < times - 1; i++)
            {
                index = content.IndexOf(formattedPath, index + 1);
                index.Should().NotBe(-1, $"File should be referenced exactly {times} times: {formattedPath}");
            }

            index = content.IndexOf(formattedPath, index + 1);
            index.Should().Be(-1, $"File should be referenced exactly {times} times: {formattedPath}");
        }
    }

    private void AssertPropertiesFilesCreated(ProjectInfoAnalysisResult result, TestLogger logger)
    {
        result.FullPropertiesFilePath.Should().NotBeNull("Expecting the sonar-scanner properties file to have been set");

        var matches = result.GetProjectsByStatus(ProjectInfoValidity.Valid);
        matches.Should().NotBeEmpty("Expecting at least one valid ProjectInfo file to exist");

        TestContext.AddResultFile(result.FullPropertiesFilePath);

        logger.AssertErrorsLogged(0);
    }

    private void AssertPropertiesFilesNotCreated(ProjectInfoAnalysisResult result, TestLogger logger)
    {
        result.FullPropertiesFilePath.Should().BeNull("Expecting the sonar-scanner properties file to have been set");

        var matches = result.GetProjectsByStatus(ProjectInfoValidity.Valid);
        matches.Should().BeEmpty("Expecting no valid ProjectInfo files to exist");

        logger.AssertErrorsLogged(1);
    }

    private static void AssertExpectedProjectCount(int expected, ProjectInfoAnalysisResult actual)
    {
        actual.Projects.Should().HaveCount(expected, "Unexpected number of projects in the result");
    }

    private static string CreateFile(string parentDir, string fileName, string content = "")
    {
        var fullPath = Path.Combine(parentDir, fileName);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }
}
