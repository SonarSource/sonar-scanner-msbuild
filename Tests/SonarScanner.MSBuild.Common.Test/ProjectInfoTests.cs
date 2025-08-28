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
public class ProjectInfoTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("\r\t ")]
    public void Save_InvalidFileName(string fileName) =>
        new ProjectInfo().Invoking(x => x.Save(fileName)).Should().ThrowExactly<ArgumentNullException>();

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("\r\t ")]
    public void Load_InvalidFileName(string fileName) =>
        FluentActions.Invoking(() => ProjectInfo.Load(fileName)).Should().ThrowExactly<ArgumentNullException>();

    [TestMethod]
    [Description("Checks ProjectInfo can be serialized and deserialized")]
    public void ProjectInfo_Serialization_SaveAndReload()
    {
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var projectGuid = Guid.NewGuid();
        var originalProjectInfo = new ProjectInfo
        {
            FullPath = "c:\\fullPath\\project.proj",
            ProjectLanguage = "a language",
            ProjectType = ProjectType.Product,
            ProjectGuid = projectGuid,
            ProjectName = "MyProject",
            Encoding = "MyEncoding",
            AnalysisResultFiles = [],
            AnalysisSettings = []
        };
        var fileName = Path.Combine(testFolder, "ProjectInfo1.xml");
        SaveAndReloadProjectInfo(originalProjectInfo, fileName);
    }

    [TestMethod]
    [Description("Checks analysis results can be serialized and deserialized")]
    public void ProjectInfo_Serialization_AnalysisResults()
    {
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var projectGuid = Guid.NewGuid();
        var originalProjectInfo = new ProjectInfo { ProjectGuid = projectGuid, AnalysisResultFiles = [], AnalysisSettings = [] };

        // Empty list
        SaveAndReloadProjectInfo(originalProjectInfo, Path.Combine(testFolder, "ProjectInfo_AnalysisResults2.xml"));

        // Non-empty list
        originalProjectInfo.AnalysisResultFiles.Add(new() { Id = string.Empty, Location = string.Empty }); // empty item
        originalProjectInfo.AnalysisResultFiles.Add(new() { Id = "Id1", Location = "location1" });
        originalProjectInfo.AnalysisResultFiles.Add(new() { Id = "Id2", Location = "location2" });
        SaveAndReloadProjectInfo(originalProjectInfo, Path.Combine(testFolder, "ProjectInfo_AnalysisResults3.xml"));
    }

    [TestMethod]
    public void AddAnalyzerResult_NullOrEmpty_Throws()
    {
        var sut = new ProjectInfo();
        sut.Invoking(x => x.AddAnalyzerResult(null, "value")).Should().Throw<ArgumentNullException>().WithParameterName("id");
        sut.Invoking(x => x.AddAnalyzerResult(string.Empty, "value")).Should().Throw<ArgumentNullException>().WithParameterName("id");
        sut.Invoking(x => x.AddAnalyzerResult("id", null)).Should().Throw<ArgumentNullException>().WithParameterName("location");
        sut.Invoking(x => x.AddAnalyzerResult("id", string.Empty)).Should().Throw<ArgumentNullException>().WithParameterName("location");
    }

    [TestMethod]
    public void ProjectFileDirectory_NoFullPath_ReturnsNull() =>
        new ProjectInfo { FullPath = null }.ProjectFileDirectory().Should().BeNull();

    [TestMethod]
    public void FindAnalysisSetting_Null_ReturnsNull() =>
        new ProjectInfo { AnalysisSettings = null }.FindAnalysisSetting("id").Should().BeNull();

    private void SaveAndReloadProjectInfo(ProjectInfo original, string outputFileName)
    {
        File.Exists(outputFileName).Should().BeFalse("Test error: file should not exist at the start of the test. File: {0}", outputFileName);
        original.Save(outputFileName);
        File.Exists(outputFileName).Should().BeTrue("Failed to create the output file. File: {0}", outputFileName);
        TestContext.AddResultFile(outputFileName);

        ProjectInfo.Load(outputFileName).Should().BeEquivalentTo(original);
    }
}
