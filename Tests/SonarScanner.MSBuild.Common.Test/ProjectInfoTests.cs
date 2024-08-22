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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class ProjectInfoTests
{
    public TestContext TestContext { get; set; }

    #region Tests

    [TestMethod]
    public void ProjectInfo_Serialization_InvalidFileName()
    {
        // 0. Setup
        var pi = new ProjectInfo();

        // 1a. Missing file name - save
        Action act = () => pi.Save(null);
        act.Should().ThrowExactly<ArgumentNullException>();
        act = () => pi.Save(string.Empty);
        act.Should().ThrowExactly<ArgumentNullException>();
        act = () => pi.Save("\r\t ");
        act.Should().ThrowExactly<ArgumentNullException>();

        // 1b. Missing file name - load
        act = () => ProjectInfo.Load(null);
        act.Should().ThrowExactly<ArgumentNullException>();
        act = () => ProjectInfo.Load(string.Empty);
        act.Should().ThrowExactly<ArgumentNullException>();
        act = () => ProjectInfo.Load("\r\t ");
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    [Description("Checks ProjectInfo can be serialized and deserialized")]
    public void ProjectInfo_Serialization_SaveAndReload()
    {
        // 0. Setup
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var projectGuid = Guid.NewGuid();

        var originalProjectInfo = new ProjectInfo
        {
            FullPath = "c:\\fullPath\\project.proj",
            ProjectLanguage = "a language",
            ProjectType = ProjectType.Product,
            ProjectGuid = projectGuid,
            ProjectName = "MyProject",
            Encoding = "MyEncoding"
        };

        var fileName = Path.Combine(testFolder, "ProjectInfo1.xml");

        SaveAndReloadProjectInfo(originalProjectInfo, fileName);
    }

    [TestMethod]
    [Description("Checks analysis results can be serialized and deserialized")]
    public void ProjectInfo_Serialization_AnalysisResults()
    {
        // 0. Setup
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var projectGuid = Guid.NewGuid();

        var originalProjectInfo = new ProjectInfo
        {
            ProjectGuid = projectGuid
        };

        // 1. Null list
        SaveAndReloadProjectInfo(originalProjectInfo, Path.Combine(testFolder, "ProjectInfo_AnalysisResults1.xml"));

        // 2. Empty list
        originalProjectInfo.AnalysisResults = new List<AnalysisResult>();
        SaveAndReloadProjectInfo(originalProjectInfo, Path.Combine(testFolder, "ProjectInfo_AnalysisResults2.xml"));

        // 3. Non-empty list
        originalProjectInfo.AnalysisResults.Add(new AnalysisResult() { Id = string.Empty, Location = string.Empty }); // empty item
        originalProjectInfo.AnalysisResults.Add(new AnalysisResult() { Id = "Id1", Location = "location1" });
        originalProjectInfo.AnalysisResults.Add(new AnalysisResult() { Id = "Id2", Location = "location2" });
        SaveAndReloadProjectInfo(originalProjectInfo, Path.Combine(testFolder, "ProjectInfo_AnalysisResults3.xml"));
    }

    #endregion Tests

    #region Helper methods

    private void SaveAndReloadProjectInfo(ProjectInfo original, string outputFileName)
    {
        File.Exists(outputFileName).Should().BeFalse("Test error: file should not exist at the start of the test. File: {0}", outputFileName);
        original.Save(outputFileName);
        File.Exists(outputFileName).Should().BeTrue("Failed to create the output file. File: {0}", outputFileName);
        TestContext.AddResultFile(outputFileName);

        var reloadedProjectInfo = ProjectInfo.Load(outputFileName);
        reloadedProjectInfo.Should().NotBeNull("Reloaded project info should not be null");

        ProjectInfoAssertions.AssertExpectedValues(original, reloadedProjectInfo);
    }

    #endregion Helper methods
}
