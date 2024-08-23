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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Shim.Test;

#region Tests

[TestClass]
public class SonarProjectPropertiesValidatorTest
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void SonarProjectPropertiesValidatorTest_FailCurrentDirectory()
    {
        var folder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        File.Create(Path.Combine(folder, "sonar-project.properties"));

        var underTest = new SonarProjectPropertiesValidator();

        var result = underTest.AreExistingSonarPropertiesFilesPresent(folder, new List<ProjectData>(), out var expectedInvalidFolders);

        Assert.IsTrue(result);

        Assert.AreEqual(1, expectedInvalidFolders.Count());
        Assert.AreEqual(folder, expectedInvalidFolders.First());
    }

    [TestMethod]
    public void SonarProjectPropertiesValidatorTest_FailProjectDirectory()
    {
        var folder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var p1 = new ProjectData(MockProject(folder, "Project1")) { Status = ProjectInfoValidity.Valid };
        var p2 = new ProjectData(MockProject(folder, "Project2")) { Status = ProjectInfoValidity.Valid };
        var p3 = new ProjectData(MockProject(folder, "Project3")) { Status = ProjectInfoValidity.Valid };

        File.Create(Path.Combine(Path.GetDirectoryName(p1.Project.FullPath), "sonar-project.properties"));
        File.Create(Path.Combine(Path.GetDirectoryName(p3.Project.FullPath), "sonar-project.properties"));

        var projects = new List<ProjectData>
        {
            p1,
            p2,
            p3,
        };

        var underTest = new SonarProjectPropertiesValidator();

        var result = underTest.AreExistingSonarPropertiesFilesPresent(folder, projects, out var expectedInvalidFolders);

        Assert.IsTrue(result);

        Assert.AreEqual(2, expectedInvalidFolders.Count());
        Assert.AreEqual(Path.GetDirectoryName(p1.Project.FullPath), expectedInvalidFolders.ElementAt(0));
        Assert.AreEqual(Path.GetDirectoryName(p3.Project.FullPath), expectedInvalidFolders.ElementAt(1));
    }

    [TestMethod]
    public void SonarProjectPropertiesValidatorTest_SucceedAndSkipInvalidProjects()
    {
        var folder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var p1 = new ProjectData(MockProject(folder, "Project1")) { Status = ProjectInfoValidity.Valid  };
        var p2 = new ProjectData(MockProject(folder, "Project3")) { Status = ProjectInfoValidity.ExcludeFlagSet };
        var p3 = new ProjectData(MockProject(folder, "Project4")) { Status = ProjectInfoValidity.InvalidGuid };
        var p4 = new ProjectData(MockProject(folder, "Project5")) { Status = ProjectInfoValidity.NoFilesToAnalyze };

        File.Create(Path.Combine(Path.GetDirectoryName(p2.Project.FullPath), "sonar-project.properties"));
        File.Create(Path.Combine(Path.GetDirectoryName(p3.Project.FullPath), "sonar-project.properties"));
        File.Create(Path.Combine(Path.GetDirectoryName(p4.Project.FullPath), "sonar-project.properties"));

        var projects = new List<ProjectData>
        {
            p1,
            p2,
            p3,
            p4,
        };

        var underTest = new SonarProjectPropertiesValidator();

        var result = underTest.AreExistingSonarPropertiesFilesPresent(folder, projects, out var expectedInvalidFolders);

        Assert.IsFalse(result);
        Assert.AreEqual(0, expectedInvalidFolders.Count());
    }

    #endregion Tests

    #region Private methods

    private ProjectInfo MockProject(string folder, string projectName)
    {
        var projectFolder = Path.Combine(folder, projectName);
        Directory.CreateDirectory(projectFolder);
        var project = new ProjectInfo
        {
            FullPath = Path.Combine(projectFolder, projectName + ".csproj")
        };
        return project;
    }

    #endregion Private methods
}
