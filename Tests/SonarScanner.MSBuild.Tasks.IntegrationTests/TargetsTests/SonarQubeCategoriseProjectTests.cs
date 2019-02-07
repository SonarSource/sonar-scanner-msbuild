/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Tasks;
using SonarScanner.MSBuild.Tasks.IntegrationTests;
using TestUtilities;

namespace SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class SonarQubeCategoriseProjectTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [TestCategory("IsTest")]
        public void SimpleProject_NoTestMarkers_IsNotATestProject()
        {
            // Act
            var result = BuildAndRunTarget("foo.proj", "");

            // Assert
            result.AssertExpectedCapturedPropertyValue("SonarQubeTestProject", "False");
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void ProjectTypeGuids_IsRecognized()
        {
            // Snippet with the Test Project Type Guid between two other Guids
            var projectXmlSnippet = @"
<PropertyGroup>
  <ProjectTypeGuids>D1C3357D-82B4-43D2-972C-4D5455F0A7DB;3AC096D0-A1C2-E12C-1390-A8335801FDAB;BF3D2153-F372-4432-8D43-09B24D530F20</ProjectTypeGuids>
</PropertyGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

            // Assert
            result.AssertExpectedCapturedPropertyValue("SonarQubeTestProject", "true");
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void ProjectTypeGuids_IsRecognized_CaseInsensitive()
        {
            var projectXmlSnippet = @"
<PropertyGroup>
  <ProjectTypeGuids>3AC096D0-A1C2-E12C-1390-A8335801fdab</ProjectTypeGuids>
</PropertyGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

            // Assert
            result.AssertExpectedCapturedPropertyValue("SonarQubeTestProject", "true");
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void ServiceGuid_IsRecognized()
        {
            var projectXmlSnippet = @"
<ItemGroup>
  <Service Include='{D1C3357D-82B4-43D2-972C-4D5455F0A7DB}' />
  <Service Include='{82A7F48D-3B50-4B1E-B82E-3ADA8210C358}' />
  <Service Include='{BF3D2153-F372-4432-8D43-09B24D530F20}' />
</ItemGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

            // Assert
            result.AssertExpectedCapturedPropertyValue("SonarQubeTestProject", "true");
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void ServiceGuid_IsRecognized_CaseInsensitive()
        {
            var projectXmlSnippet = @"
<ItemGroup>
  <Service Include='{82a7f48d-3b50-4b1e-b82e-3ada8210c358}' />
</ItemGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

            // Assert
            result.AssertExpectedCapturedPropertyValue("SonarQubeTestProject", "true");
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void ProjectCapability_IsRecognized()
        {
            var projectXmlSnippet = @"
<ItemGroup>
  <ProjectCapability Include='Foo' />
  <ProjectCapability Include='TestContainer' />
  <ProjectCapability Include='Something else' />
</ItemGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

            // Assert
            result.AssertExpectedCapturedPropertyValue("SonarQubeTestProject", "true");
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void ProjectCapability_IsRecognized_CaseInsensitive()
        {
            var projectXmlSnippet = @"
<ItemGroup>
  <ProjectCapability Include='testcontainer' />
</ItemGroup>
";
            // Act
            var result = BuildAndRunTarget("foo.proj", projectXmlSnippet);

            // Assert
            result.AssertExpectedCapturedPropertyValue("SonarQubeTestProject", "true");
        }

        #endregion Tests

        private BuildLog BuildAndRunTarget(string projectFileName, string projectXmlSnippet)
        {
            var projectFilePath = CreateProjectFile(projectFileName, projectXmlSnippet);

            // Act
            var result = BuildRunner.BuildTargets(TestContext, projectFilePath,
                TargetConstants.CategoriseProjectTarget);

            // Assert
            result.AssertTargetSucceeded(TargetConstants.CategoriseProjectTarget);
            return result;
        }

        private string CreateProjectFile(string projectFileName, string xmlSnippet)
        {
            var rootInputFolder = TestUtils.CreateTestSpecificFolder(TestContext, "Inputs");

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            var projectFilePath = Path.Combine(rootInputFolder, "project.txt");

            // Boilerplate XML for minimal project file that will execute the "categorise project" task
            var projectXml = @"<Project Sdk='Microsoft.NET.Sdk'>

  <!-- Test-specific XML snippet -->
  {0}

  <!-- Boilerplate -->
  <PropertyGroup>
    <ProjectGuid>{1}</ProjectGuid>
    <SonarQubeTempPath>c:\dummy\path</SonarQubeTempPath>
    <SonarQubeOutputPath>c:\dummy\path</SonarQubeOutputPath>
    <SonarQubeBuildTasksAssemblyFile>{2}</SonarQubeBuildTasksAssemblyFile>
  </PropertyGroup>

  <!-- We need to write out the properties we want to check later -->
  <Target Name='CaptureData' AfterTargets='SonarQubeCategoriseProject' >
    <Message Importance='high' Text='CAPTURE___PROPERTY___tmpSQServiceList___$(tmpSQServiceList)' />
    <Message Importance='high' Text='CAPTURE___PROPERTY___tmpSQProjectCapabilities___$(tmpSQProjectCapabilities)' />
    <Message Importance='high' Text='CAPTURE___PROPERTY___SonarQubeTestProject___$(SonarQubeTestProject)' />
  </Target>

  <Import Project='{3}' />
</Project>
";
            BuildUtilities.CreateFileFromTemplate(projectFilePath, TestContext, projectXml,
                xmlSnippet,
                Guid.NewGuid(),
                typeof(WriteProjectInfoFile).Assembly.Location,
                sqTargetFile
                );

            return projectFilePath;
        }
    }
}
