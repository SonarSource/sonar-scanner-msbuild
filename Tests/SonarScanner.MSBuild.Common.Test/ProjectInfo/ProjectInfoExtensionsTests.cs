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
    public void GetAllAnalysisFilesTest()
    {
        var dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var filesToAnalyze = Path.Combine(dir, AnalysisResultFileType.FilesToAnalyze.ToString());
        var logger = new TestLogger();
        var projectInfo = new ProjectInfo
        {
            AnalysisResultFiles = [new(AnalysisResultFileType.FilesToAnalyze, filesToAnalyze)]
        };
        File.WriteAllLines(
            filesToAnalyze,
            [
                "C:\\foo",
                "C:\\bar",
                "not:allowed",
                "C:\\baz",
            ]);

        var result = projectInfo.AllAnalysisFiles(logger);

#if NETFRAMEWORK
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("foo");
        result[1].Name.Should().Be("bar");
        result[2].Name.Should().Be("baz");
        logger.Should().HaveDebugOnce("Could not add 'not:allowed' to the analysis. The given path's format is not supported.");
#else
        // NET supports "not:allowed"
        result.Should().HaveCount(4);
#endif
    }
}
