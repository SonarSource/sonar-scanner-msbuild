/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
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

using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Tasks;
using SonarScanner.MSBuild.Tasks.IntegrationTests;
using TestUtilities;

namespace SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests
{
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
        public string GetProjectTemplate(AnalysisConfig analysisConfig, string projectDirectory)
        {
            if (analysisConfig != null)
            {
                var configFilePath = Path.Combine(projectDirectory, FileConstants.ConfigFileName);
                analysisConfig.Save(configFilePath);
            }

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContextInstance);
            File.Exists(sqTargetFile).Should().BeTrue("Test error: the SonarQube analysis targets file could not be found. Full path: {0}", sqTargetFile);
            TestContextInstance.AddResultFile(sqTargetFile);
            return Properties.Resources.TargetTestsProjectTemplate;
        }

        public string GetProjectTemplate(AnalysisConfig analysisConfig, string projectDirectory, string testProperties, string testXml, string testImports)
        {
            var template = GetProjectTemplate(analysisConfig, projectDirectory);

            return template.Replace("PROJECT_DIRECTORY_PATH", projectDirectory)
                .Replace("SONARSCANNER_MSBUILD_TASKS_DLL", typeof(WriteProjectInfoFile).Assembly.Location)
                .Replace("TEST_SPECIFIC_XML", testXml ?? "<!-- none -->")
                .Replace("TEST_SPECIFIC_IMPORTS", testImports ?? "<!-- none -->")
                .Replace("TEST_SPECIFIC_PROPERTIES", testProperties ?? "<!-- none -->");
        }

        public string CreateProjectFile(string projectDirectory, string projectData)
        {
            var projectFilePath = Path.Combine(projectDirectory, TestContextInstance.TestName + ".proj.txt");
            File.WriteAllText(projectFilePath, projectData);
            TestContextInstance.AddResultFile(projectFilePath);

            return projectFilePath;
        }

        public void CreateCaptureDataTargetsFile(string directory, string afterTargets)
        {
            // Most of the tests above want to check the value of build property
            // or item group after a target has been executed. However, this
            // information is not available through the buildlogger interface.
            // So, we'll add a special target that writes the properties/items
            // we are interested in to the message log.
            // The SimpleXmlLogger has special handling to extract the data
            // from the message and add it to the BuildLog.
            string xml = Properties.Resources.CaptureDataTargetsFileTemplate;
            xml = string.Format(xml, afterTargets);

            // We're using :: as a separator here: replace it with whatever
            // the logger is using as a separator
            xml = xml.Replace("::", SimpleXmlLogger.CapturedDataSeparator);

            var filePath = Path.Combine(directory, "Capture.targets");
            File.WriteAllText(filePath, xml);
            TestContextInstance.AddResultFile(filePath);
        }
    }
}
