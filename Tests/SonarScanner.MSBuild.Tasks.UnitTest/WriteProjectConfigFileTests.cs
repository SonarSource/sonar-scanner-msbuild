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

using Task = Microsoft.Build.Utilities.Task;

namespace SonarScanner.MSBuild.Tasks.UnitTest;

[TestClass]
public class WriteProjectConfigFileTests
{
    private const string ExpectedProjectConfigFileName = "SonarProjectConfig.xml";

    public TestContext TestContext { get; set; }

    [TestMethod]
    [Description("Tests that the project config file is created when the task is executed.")]
    public void Execute_FileCreated()
    {
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var task = new WriteProjectConfigFile
        {
            ConfigDir = testFolder,
            AnalysisConfigPath = @"c:\fullPath\config.xml",
            ProjectPath = @"c:\fullPath\project.xproj",
            FilesToAnalyzePath = @"c:\fullPath\files.txt",
            OutPath = @"c:\fullPath\out\42\",
            IsTest = false,
            TargetFramework = "target-42"
        };

        var reloadedConfig = ExecuteAndReloadConfig(task, testFolder);

        reloadedConfig.AnalysisConfigPath.Should().Be(@"c:\fullPath\config.xml");
        reloadedConfig.ProjectPath.Should().Be(@"c:\fullPath\project.xproj");
        reloadedConfig.OutPath.Should().Be(@"c:\fullPath\out\42\");
        reloadedConfig.FilesToAnalyzePath.Should().Be(@"c:\fullPath\files.txt");
        reloadedConfig.ProjectType.Should().Be(ProjectType.Product);
        reloadedConfig.TargetFramework.Should().Be("target-42");
    }

    [TestMethod]
    [Description("Tests that all existing config properties are filled from the task.")]
    public void Execute_AllPropertiesAreSet()
    {
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var task = new WriteProjectConfigFile
        {
            ConfigDir = testFolder,
            AnalysisConfigPath = "not empty",
            ProjectPath = "not empty",
            FilesToAnalyzePath = "not empty",
            OutPath = "not empty",
            IsTest = true,
            TargetFramework = "not empty"
        };

        var reloadedConfig = ExecuteAndReloadConfig(task, testFolder);

        foreach (var property in reloadedConfig.GetType().GetProperties())
        {
            var value = property.GetValue(reloadedConfig)?.ToString();
            value.Should().NotBeNullOrEmpty($"property '{property.Name}' should have value");
        }
    }

    [DataRow(true, ProjectType.Test)]
    [DataRow(false, ProjectType.Product)]
    [TestMethod]
    public void Execute_IsTest_TrueReturnsTypeTest(bool isTest, ProjectType expectedType)
    {
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var task = new WriteProjectConfigFile
        {
            ConfigDir = testFolder,
            IsTest = isTest,
        };
        ExecuteAndReloadConfig(task, testFolder).ProjectType
            .Should().Be(expectedType);
    }

    private ProjectConfig ExecuteAndReloadConfig(WriteProjectConfigFile writeProjectConfigFile, string testFolder)
    {
        var expectedFile = Path.Combine(testFolder, ExpectedProjectConfigFileName);
        File.Exists(expectedFile).Should().BeFalse("Test error: output file should not exist before the task is executed");

        var result = writeProjectConfigFile.Execute();

        result.Should().BeTrue("Expecting the task execution to succeed");
        File.Exists(expectedFile).Should().BeTrue("Expected output file was not created by the task. Expected: {0}", expectedFile);
        TestContext.AddResultFile(expectedFile);

        var config = ProjectConfig.Load(expectedFile);
        config.Should().NotBeNull("Not expecting the reloaded project config file to be null");
        return config;
    }
}
