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

using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Tasks;
using SonarScanner.MSBuild.Tasks.IntegrationTest;
using TestUtilities;

namespace SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests;

public class TargetsTestsUtils
{
    public TestContext TestContextInstance { get; set; }

    public TargetsTestsUtils(TestContext testContext)
    {
        TestContextInstance = testContext;
    }

    /// <summary>
    /// Creates a valid project with the necessary ruleset and assembly files on disc
    /// to successfully run the "OverrideRoslynCodeAnalysisProperties" target
    /// </summary>
    private string GetProjectTemplate(AnalysisConfig analysisConfig, string projectDirectory)
    {
        if (analysisConfig != null)
        {
            var configFilePath = Path.Combine(projectDirectory, FileConstants.ConfigFileName);
            analysisConfig.Save(configFilePath);
        }

        var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContextInstance);
        File.Exists(sqTargetFile).Should().BeTrue("Test error: the SonarQube analysis targets file could not be found. Full path: {0}", sqTargetFile);
        TestContextInstance.AddResultFile(sqTargetFile);
        return Resources.TargetTestsProjectTemplateOld;
    }

    public string GetProjectTemplate(AnalysisConfig analysisConfig, string projectDirectory, string testProperties, string testXml, string sqOutputPath = null) =>
        GetProjectTemplate(analysisConfig, projectDirectory)
            .Replace("SONARSCANNER_MSBUILD_TASKS_DLL", typeof(WriteProjectInfoFile).Assembly.Location)
            .Replace("TEST_SPECIFIC_PROPERTIES", testProperties ?? "<!-- none -->")
            .Replace("TEST_SPECIFIC_XML", testXml ?? "<!-- none -->")
            .Replace("PROJECT_DIRECTORY_PATH", projectDirectory)    // This needs to be after TEST_SPECIFIC_* because they use this constant inside
            .Replace("SQ_OUTPUT_PATH", sqOutputPath);

    public string CreateProjectFile(string projectDirectory, string projectData)
    {
        var projectFilePath = Path.Combine(projectDirectory, TestContextInstance.TestName + ".proj.txt");
        File.WriteAllText(projectFilePath, projectData);
        TestContextInstance.AddResultFile(projectFilePath);

        return projectFilePath;
    }
}
