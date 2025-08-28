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

[TestClass]
public class ProjectDataTest
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow("cs")]
    [DataRow("vbnet")]
    public void Orders_AnalyzerOutPaths(string languageKey)
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
        var results = projectInfos.ToProjectData(true, Substitute.For<ILogger>()).Single().AnalyzerOutPaths.ToList();

        results.Should().HaveCount(4);
        results[0].FullName.Should().Be(new FileInfo("2").FullName);
        results[1].FullName.Should().Be(new FileInfo("3").FullName);
        results[2].FullName.Should().Be(new FileInfo("4").FullName);
        results[3].FullName.Should().Be(new FileInfo("1").FullName);
    }

    [TestMethod]
    public void ProjectsWithDuplicateGuid()
    {
        var logger = new TestLogger();
        var guid = Guid.NewGuid();
        var projectInfos = new[]
        {
            new ProjectInfo { ProjectGuid = guid, FullPath = "path1" },
            new ProjectInfo { ProjectGuid = guid, FullPath = "path2" },
            new ProjectInfo { ProjectGuid = guid, FullPath = "path2" }
        };
        var result = projectInfos.ToProjectData(true, logger).Single();

        result.Status.Should().Be(ProjectInfoValidity.DuplicateGuid);
        logger.Warnings.Should().BeEquivalentTo(
            $@"Duplicate ProjectGuid: ""{guid}"". The project will not be analyzed. Project file: ""path1""",
            $@"Duplicate ProjectGuid: ""{guid}"". The project will not be analyzed. Project file: ""path2""");
    }

    // Repro for https://sonarsource.atlassian.net/browse/SCAN4NET-431
    [TestMethod]
    public void DoesNotChooseValidProject()
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
                    new(ScannerEngineInputGenerator.ReportFilePathsKeyCS, "validRoslyn"),
                    new(ScannerEngineInputGenerator.ProjectOutPathsKeyCS, "validOutPath")
                ],
                FullPath = fullPath,
            }
        };
        projectInfos[0].AddAnalyzerResult(AnalysisResultFileType.FilesToAnalyze, contentFileList1);
        projectInfos[1].AddAnalyzerResult(AnalysisResultFileType.FilesToAnalyze, contentFileList1);
        var sut = projectInfos.ToProjectData(true, Substitute.For<ILogger>()).Single();

        sut.Status.Should().Be(ProjectInfoValidity.Valid);
        sut.Project.AnalysisSettings.Should().BeEmpty();     // Expected to change when fixed later
    }

    [TestMethod]
    [DataRow("cs")]
    [DataRow("vbnet")]
    public void Telemetry_Multitargeting(string languageKey)
    {
        var guid = Guid.NewGuid();
        var propertyKey = $"sonar.{languageKey}.scanner.telemetry";
        var fullPath = TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "File.txt");
        var projectInfos = new[]
        {
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = [new(propertyKey, "1.json")],
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                TargetFramework = "net46",
                AnalysisSettings = [new(propertyKey, "2.json")],
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Release",
                TargetFramework = "netstandard2.0",
                AnalysisSettings =  [
                    new(propertyKey, "3.json"),
                    new(propertyKey, "4.json"),
                ],
                FullPath = fullPath,
            },
        };
        var results = projectInfos.ToProjectData(true, Substitute.For<ILogger>()).Single().TelemetryPaths.ToList();

        results.Should().BeEquivalentTo([new FileInfo("2.json"), new("1.json"), new("3.json"), new("4.json")], x => x.Excluding(x => x.Length).Excluding(x => x.Directory));
    }
}
