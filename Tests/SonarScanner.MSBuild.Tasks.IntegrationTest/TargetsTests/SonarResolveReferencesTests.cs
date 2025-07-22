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

using SonarScanner.MSBuild.Tasks.IntegrationTest;
using SonarScanner.MSBuild.Tasks.IntegrationTest.TargetsTests;

namespace SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests;

[TestClass]
public class SonarResolveReferencesTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void BuildIntegration_ResolvesReferences()
    {
        var context = new TargetsTestsContext(TestContext);
        var projectSnippet = $"""
            <PropertyGroup>
                <SonarQubeTempPath>{context.ProjectFolder}</SonarQubeTempPath>
            </PropertyGroup>
            """;
        var filePath = context.CreateProjectFile(projectSnippet, emptySqProperties: true, template: Resources.TargetTestsProjectTemplate);

        var result = BuildRunner.BuildTargets(TestContext, filePath);

        result.AssertTargetSucceeded(TargetConstants.DefaultBuild);
        result.AssertTargetExecuted(TargetConstants.SonarResolveReferences);
        result.AssertTargetExecuted(TargetConstants.SonarCategoriseProject);
        var sonarResolvedReferences = result.GetItem(TargetProperties.SonarResolvedReferences);
        sonarResolvedReferences.Should().NotBeEmpty();
        sonarResolvedReferences.Should().Contain(x => x.Text.Contains("mscorlib"));
    }
}
