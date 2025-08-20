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
public class ProjectInfoReportBuilderTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void PIRB_WriteSummaryReport_InvalidArgs_Throws()
    {
        var analysisConfig = new AnalysisConfig();
        var analysisResult = new ProjectInfoAnalysisResult([]);
        var loggerMock = Substitute.For<ILogger>();

        // 1. Invalid analysis config
        Action op = () => ProjectInfoReportBuilder.WriteSummaryReport(null, analysisResult, loggerMock);
        op.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("config");

        // 2. Invalid result
        op = () => ProjectInfoReportBuilder.WriteSummaryReport(analysisConfig, null, loggerMock);
        op.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("result");

        // 3. Invalid logger
        op = () => ProjectInfoReportBuilder.WriteSummaryReport(analysisConfig, analysisResult, null);
        op.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void PIRB_WriteSummaryReport_ValidArgs_FileCreated()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var analysisConfig = new AnalysisConfig() { SonarOutputDir = testDir };
        var analysisResult = new ProjectInfoAnalysisResult([
            CreateProjectData("project1", ProjectType.Product, ProjectInfoValidity.ExcludeFlagSet),
            CreateProjectData("project2", ProjectType.Product, ProjectInfoValidity.InvalidGuid),
            CreateProjectData("project3", ProjectType.Product, ProjectInfoValidity.InvalidGuid),
            CreateProjectData("project4", ProjectType.Product, ProjectInfoValidity.NoFilesToAnalyze),
            CreateProjectData("project5", ProjectType.Test, ProjectInfoValidity.NoFilesToAnalyze),
            CreateProjectData("project6", ProjectType.Test, ProjectInfoValidity.NoFilesToAnalyze),
            CreateProjectData("project7", ProjectType.Test, ProjectInfoValidity.Valid),
            CreateProjectData("project8", ProjectType.Test, ProjectInfoValidity.Valid),
            CreateProjectData("project9", ProjectType.Test, ProjectInfoValidity.Valid),
            CreateProjectData("projectA", ProjectType.Test, ProjectInfoValidity.Valid)]);
        var loggerMock = Substitute.For<ILogger>();
        ProjectInfoReportBuilder.WriteSummaryReport(analysisConfig, analysisResult, loggerMock);

        var expectedFileName = Path.Combine(testDir, ProjectInfoReportBuilder.ReportFileName);
        File.Exists(expectedFileName).Should().BeTrue();
        TestContext.AddResultFile(expectedFileName);

        var contents = File.ReadAllText(expectedFileName);
        contents.Should().Contain("project1");
        contents.Should().Contain("project2");
        contents.Should().Contain("project3");
        contents.Should().Contain("project4");
        contents.Should().Contain("project5");
        contents.Should().Contain("project6");
        contents.Should().Contain("project7");
        contents.Should().Contain("project8");
        contents.Should().Contain("project9");
        contents.Should().Contain("projectA");
    }

    private static ProjectData CreateProjectData(string projectPath, ProjectType type, ProjectInfoValidity validity)
    {
        var projectInfo = new ProjectInfo()
        {
            FullPath = projectPath,
            ProjectType = type
        };

        return new ProjectData(projectInfo)
        {
            Status = validity
        };
    }
}
