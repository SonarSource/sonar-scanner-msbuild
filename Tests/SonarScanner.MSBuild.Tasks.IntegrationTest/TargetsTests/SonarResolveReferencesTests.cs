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

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Tasks.IntegrationTest;
using TestUtilities;

namespace SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests;

[TestClass]
public class SonarResolveReferencesTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void BuildIntegration_ResolvesReferences()
    {
        // Arrange
        var rootFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var projectSnippet = $@"
<PropertyGroup>
    <SonarQubeTempPath>{rootFolder}</SonarQubeTempPath>
</PropertyGroup>";

        var filePath = CreateProjectFile(projectSnippet);

        // Act
        var result = BuildRunner.BuildTargets(TestContext, filePath, TargetConstants.DefaultBuild);

        // Assert
        result.AssertTargetSucceeded(TargetConstants.DefaultBuild);
        result.AssertTargetExecuted(TargetConstants.SonarResolveReferences);
        result.AssertTargetExecuted(TargetConstants.SonarCategoriseProject);

        var sonarResolvedReferences = result.GetItem(TargetProperties.SonarResolvedReferences);
        sonarResolvedReferences.Should().NotBeEmpty();
        sonarResolvedReferences.Should().Contain(x => x.Text.Contains("mscorlib"));
    }

    private string CreateProjectFile(string projectSnippet)
    {
        var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var targetTestUtils = new TargetsTestsUtils(TestContext);
        var projectTemplate = targetTestUtils.GetProjectTemplate(null, projectDirectory, null, projectSnippet);
        return targetTestUtils.CreateProjectFile(projectDirectory, projectTemplate);
    }
}
