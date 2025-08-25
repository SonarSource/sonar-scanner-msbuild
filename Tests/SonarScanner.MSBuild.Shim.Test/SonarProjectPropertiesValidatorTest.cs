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
public class SonarProjectPropertiesValidatorTest
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void SonarProjectPropertiesValidatorTest_FailCurrentDirectory()
    {
        var folder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        File.Create(Path.Combine(folder, "sonar-project.properties"));
        var sut = new SonarProjectPropertiesValidator();
        var result = sut.AreExistingSonarPropertiesFilesPresent(folder, [], out var expectedInvalidFolders);

        result.Should().BeTrue();
        expectedInvalidFolders.Should().BeEquivalentTo(folder);
    }

    [TestMethod]
    public void SonarProjectPropertiesValidatorTest_FailProjectDirectory()
    {
        var folder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var p1 = CreateProjectData(folder, "Project1", ProjectInfoValidity.Valid);
        var p2 = CreateProjectData(folder, "Project2", ProjectInfoValidity.Valid);
        var p3 = CreateProjectData(folder, "Project3", ProjectInfoValidity.Valid);
        var projects = new List<ProjectData> { p1, p2, p3, };
        File.Create(Path.Combine(Path.GetDirectoryName(p1.Project.FullPath), "sonar-project.properties"));
        File.Create(Path.Combine(Path.GetDirectoryName(p3.Project.FullPath), "sonar-project.properties"));
        var sut = new SonarProjectPropertiesValidator();
        var result = sut.AreExistingSonarPropertiesFilesPresent(folder, projects, out var expectedInvalidFolders);

        result.Should().BeTrue();
        expectedInvalidFolders.Should().BeEquivalentTo(Path.GetDirectoryName(p1.Project.FullPath), Path.GetDirectoryName(p3.Project.FullPath));
    }

    [TestMethod]
    public void SonarProjectPropertiesValidatorTest_SucceedAndSkipInvalidProjects()
    {
        var folder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var p1 = CreateProjectData(folder, "Project1", ProjectInfoValidity.Valid);
        var p2 = CreateProjectData(folder, "Project3", ProjectInfoValidity.ExcludeFlagSet);
        var p3 = CreateProjectData(folder, "Project4", ProjectInfoValidity.InvalidGuid);
        var p4 = CreateProjectData(folder, "Project5", ProjectInfoValidity.NoFilesToAnalyze);
        var projects = new List<ProjectData> { p1, p2, p3, p4, };
        File.Create(Path.Combine(Path.GetDirectoryName(p2.Project.FullPath), "sonar-project.properties"));
        File.Create(Path.Combine(Path.GetDirectoryName(p3.Project.FullPath), "sonar-project.properties"));
        File.Create(Path.Combine(Path.GetDirectoryName(p4.Project.FullPath), "sonar-project.properties"));
        var sut = new SonarProjectPropertiesValidator();
        var result = sut.AreExistingSonarPropertiesFilesPresent(folder, projects, out var expectedInvalidFolders);

        result.Should().BeFalse();
        expectedInvalidFolders.Should().BeEmpty();
    }

    private static ProjectData CreateProjectData(string folder, string projectName, ProjectInfoValidity status)
    {
        var projectFolder = Path.Combine(folder, projectName);
        Directory.CreateDirectory(projectFolder);
        var project = new ProjectInfo { FullPath = Path.Combine(projectFolder, projectName + ".csproj") };
        return new(new[] { project }.GroupBy(x => x.ProjectGuid).Single(), true, Substitute.For<ILogger>()) { Status = status };
    }
}
