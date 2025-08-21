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

public partial class PropertiesFileGeneratorTests
{
    [TestMethod]
    [DataRow("cs")]
    [DataRow("vbnet")]
    public void ToProjectData_Orders_AnalyzerOutPaths(string languageKey)
    {
        var guid = Guid.NewGuid();
        var propertyKey = $"sonar.{languageKey}.analyzer.projectOutPaths";
        var fullPath = TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "File.txt");
        var projectInfos = new[]
        {
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Release",
                Platform = "anyCpu",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = [new(propertyKey, "1")],
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "anyCpu",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = [new(propertyKey, "2")],
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "x86",
                TargetFramework = "net46",
                AnalysisSettings = [new(propertyKey, "3")],
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "x86",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = [new(propertyKey, "4")],
                FullPath = fullPath,
            },
        };

        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
        var propertiesFileGenerator = CreateSut(CreateValidConfig(analysisRootDir));
        var results = propertiesFileGenerator.ToProjectData(projectInfos.GroupBy(x => x.ProjectGuid).First()).AnalyzerOutPaths.ToList();

        results.Should().HaveCount(4);
        results[0].FullName.Should().Be(new FileInfo("2").FullName);
        results[1].FullName.Should().Be(new FileInfo("3").FullName);
        results[2].FullName.Should().Be(new FileInfo("4").FullName);
        results[3].FullName.Should().Be(new FileInfo("1").FullName);
    }

    [TestMethod]
    public void ToProjectData_ProjectsWithDuplicateGuid()
    {
        var guid = Guid.NewGuid();
        var projectInfos = new[]
        {
            new ProjectInfo { ProjectGuid = guid, FullPath = "path1" },
            new ProjectInfo { ProjectGuid = guid, FullPath = "path2" },
            new ProjectInfo { ProjectGuid = guid, FullPath = "path2" }
        };
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
        var propertiesFileGenerator = new PropertiesFileGenerator(CreateValidConfig(analysisRootDir), logger);
        var result = propertiesFileGenerator.ToProjectData(projectInfos.GroupBy(x => x.ProjectGuid).First());

        result.Status.Should().Be(ProjectInfoValidity.DuplicateGuid);
        logger.Warnings.Should().BeEquivalentTo(
            $@"Duplicate ProjectGuid: ""{guid}"". The project will not be analyzed. Project file: ""path1""",
            $@"Duplicate ProjectGuid: ""{guid}"". The project will not be analyzed. Project file: ""path2""");
    }

    // Repro for https://sonarsource.atlassian.net/browse/SCAN4NET-431
    [TestMethod]
    public void ToProjectData_DoesNotChooseValidProject()
    {
        var guid = Guid.NewGuid();
        var fullPath = TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "File.txt");
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
                AnalysisSettings = [],
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "x86",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = [
                    new(PropertiesFileGenerator.ReportFilePathsCSharpPropertyKey, "validRoslyn"),
                    new(PropertiesFileGenerator.ProjectOutPathsCsharpPropertyKey, "validOutPath")
                ],
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
        var writer = new PropertiesWriter(config);
        writer.WriteSettingsForProject(sut);
        var resultString = writer.Flush();
        resultString.Should().NotContain("validRoslyn"); // Expected to change when fixed
        resultString.Should().NotContain("validOutPath"); // Expected to change when fixed
    }
}
