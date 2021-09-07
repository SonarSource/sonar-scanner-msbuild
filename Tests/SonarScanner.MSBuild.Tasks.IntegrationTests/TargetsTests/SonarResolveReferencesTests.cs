/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2021 SonarSource SA
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
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Tasks;
using SonarScanner.MSBuild.Tasks.IntegrationTests;
using TestUtilities;

namespace SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class SonarResolveReferencesTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [TestCategory("IsTest")]
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

            var sonarResolvedReferences = result.GetCapturedItemValues(TargetProperties.SonarResolvedReferences);
            sonarResolvedReferences.Should().NotBeEmpty();
            sonarResolvedReferences.Should().Contain(x => x.Value.Contains("mscorlib"));
        }

        private string CreateProjectFile(string projectSnippet)
        {
            // This target captures the ItemGroup we're interested in
            var captureReferences = $@"
<Project ToolsVersion='Current' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <Target Name='CaptureValues' AfterTargets='{TargetConstants.SonarCategoriseProject}'>
    <Message Importance='high' Text='CAPTURE::ITEM::{TargetProperties.SonarResolvedReferences}::%({TargetProperties.SonarResolvedReferences}.Identity)' />
  </Target>
</Project>";
            var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var targetTestUtils = new TargetsTestsUtils(TestContext);
            var capturePath = targetTestUtils.CreateCaptureTargetsFile(projectDirectory, captureReferences);
            var projectTemplate = targetTestUtils.GetProjectTemplate(null, projectDirectory, null, projectSnippet, $"<Import Project='{capturePath}' />");
            return targetTestUtils.CreateProjectFile(projectDirectory, projectTemplate);
        }
    }
}
