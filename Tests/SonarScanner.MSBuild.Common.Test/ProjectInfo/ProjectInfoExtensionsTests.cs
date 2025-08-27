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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class ProjectInfoExtensionsTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void TryGetAnalysisSetting_WhenProjectInfoIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.TryGetAnalyzerResult(null, "foo", out var result);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectInfo");
    }

    [TestMethod]
    public void TryGetAnalyzerResult_WhenProjectInfoIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.TryGetAnalysisSetting(null, "foo", out var result);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectInfo");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenProjectInfoIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(null, "foo", "bar");

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectInfo");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenIdIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(new ProjectInfo(), null, "bar");

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("id");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenIdIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(new ProjectInfo(), "", "bar");

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("id");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenIdIsWhitespaces_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(new ProjectInfo(), "   ", "bar");

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("id");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenLocationIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(new ProjectInfo(), "foo", null);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("location");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenLocationIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(new ProjectInfo(), "foo", "");

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("location");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenLocationIsWhitespaces_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(new ProjectInfo(), "foo", "   ");

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("location");
    }

    [TestMethod]
    public void GetDirectory_WhenProjectInfoIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.GetDirectory(null);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectInfo");
    }

    [TestMethod]
    public void GetProjectGuidAsString_WhenProjectInfoIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.GetProjectGuidAsString(null);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectInfo");
    }

    [TestMethod]
    public void GetAllAnalysisFilesTest()
    {
        var dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var filesToAnalyze = Path.Combine(dir, AnalysisResultFileType.FilesToAnalyze.ToString());
        var logger = new TestLogger();
        var projectInfo = new ProjectInfo
        {
            AnalysisResults =
            [
                new AnalysisResult
                {
                    Id = AnalysisResultFileType.FilesToAnalyze.ToString(),
                    Location = filesToAnalyze,
                }
            ]
        };
        File.WriteAllLines(
            filesToAnalyze,
            [
                "C:\\foo",
                "C:\\bar",
                "not:allowed",
                "C:\\baz",
            ]);

        var result = projectInfo.GetAllAnalysisFiles(logger);

#if NETFRAMEWORK
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("foo");
        result[1].Name.Should().Be("bar");
        result[2].Name.Should().Be("baz");
        logger.AssertSingleDebugMessageExists("Could not add 'not:allowed' to the analysis. The given path's format is not supported.");
#else
        // NET supports "not:allowed"
        result.Should().HaveCount(4);
#endif
    }
}
