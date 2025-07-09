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

using System.Runtime.InteropServices;

namespace SonarScanner.MSBuild.Tasks.IntegrationTest.TargetsTests;

public class TargetsTestsContext
{
    private readonly string language;

    public TestContext TestContext { get; }
    public string ProjectFolder { get; }
    public string ConfigFolder { get; }
    public string InputFolder { get; }
    public string OutputFolder { get; }

    public TargetsTestsContext(TestContext testContext, string language = "C#", string inputFolder = "Inputs")
    {
        this.language = language;
        TestContext = testContext;
        ProjectFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        ConfigFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Config");
        InputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, inputFolder);
        OutputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Outputs");
    }

    public string CreateInputFile(string fileName)
    {
        var filePath = Path.Combine(InputFolder, fileName);
        File.WriteAllText(filePath, null);
        return filePath;
    }

    public string CreateProjectFile(string testSpecificProjectXml, AnalysisConfig config = null, bool emptySqProperties = false)
    {
        if (config is not null)
        {
            var configFilePath = Path.Combine(ConfigFolder, FileConstants.ConfigFileName);
            config.Save(configFilePath);
        }
        var targetFramework = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "net48" : "net9";
        var projectExt = language is "VB" ? ".vbproj" : ".csproj";
        var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
        File.Exists(sqTargetFile).Should().BeTrue("Test error: the SonarQube analysis targets file could not be found. Full path: {0}", sqTargetFile);
        TestContext.AddResultFile(sqTargetFile);

        testSpecificProjectXml ??= "<!-- none -->";
        if (!emptySqProperties)
        {
            testSpecificProjectXml = $"""
                <PropertyGroup>
                    <SonarQubeTempPath>{ProjectFolder}</SonarQubeTempPath>
                    <SonarQubeOutputPath>{OutputFolder}</SonarQubeOutputPath>
                    <SonarQubeConfigPath>{ConfigFolder}</SonarQubeConfigPath>
                </PropertyGroup>

                """
                // Provided testSpecificProjectXml must be appended so it can override these properties.
                + testSpecificProjectXml;
        }

        var projectData = Resources.TargetTestsProjectTemplate.Replace("PROJECT_DIRECTORY_PATH", ProjectFolder)
            .Replace("TARGET_FRAMEWORK", targetFramework)
            .Replace("SONARSCANNER_MSBUILD_TASKS_DLL", typeof(WriteProjectInfoFile).Assembly.Location)
            .Replace("TEST_SPECIFIC_XML", testSpecificProjectXml)
            .Replace("LANGUAGE", language);

        var projectFilePath = Path.Combine(ProjectFolder, TestContext.TestName + projectExt);
        File.WriteAllText(projectFilePath, projectData);
        TestContext.AddResultFile(projectFilePath);

        return projectFilePath;
    }
}
