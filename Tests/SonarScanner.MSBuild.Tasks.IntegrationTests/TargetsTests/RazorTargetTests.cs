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

using System.Globalization;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Tasks;
using SonarScanner.MSBuild.Tasks.IntegrationTests;
using TestUtilities;

namespace SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class RazorTargetTests
    {
        private const string ErrorLogFilePattern = "{0}.RoslynCA.json";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Razor_ForProductProject_CheckErrorLogProperties()
        {
            // Arrange
            var rootInputFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Inputs");

            var projectSnippet = $@"
<PropertyGroup>
<ErrorLog>%%ErrorLogFile%%</ErrorLog>
<SonarCompileErrorLog>%%ErrorLogFile%%</SonarCompileErrorLog>
  <SonarQubeTempPath>{rootInputFolder}</SonarQubeTempPath>
</PropertyGroup>
";

            var projectFilePath = CreateProjectFile(null, projectSnippet);

            // Act
            var result = BuildRunner.BuildTargets(TestContext,
                projectFilePath,
                TargetConstants.SetRazorCodeAnalysisPropertiesTarget);

            // Assert
            result.AssertTargetExecuted(TargetConstants.SetRazorCodeAnalysisPropertiesTarget);
            AssertErrorLogIsSetBySonarQubeTargets(result);
        }

        private static void AssertErrorLogIsSetBySonarQubeTargets(BuildLog result)
        {
            var targetDir = result.GetCapturedPropertyValue(TargetProperties.TargetDir);
            var targetFileName = result.GetCapturedPropertyValue(TargetProperties.TargetFileName);

            var expectedErrorLog = Path.Combine(targetDir, string.Format(CultureInfo.InvariantCulture, ErrorLogFilePattern, targetFileName));

            AssertExpectedErrorLog(result, expectedErrorLog);
        }

        private static void AssertExpectedErrorLog(BuildLog result, string expectedErrorLog)
        {
            result.AssertExpectedCapturedPropertyValue(TargetProperties.ErrorLog, expectedErrorLog);
            result.AssertExpectedCapturedPropertyValue(TargetProperties.RazorSonarCompileErrorLog, expectedErrorLog);
            result.AssertExpectedCapturedPropertyValue(TargetProperties.RazorCompilationErrorLog, expectedErrorLog);
        }

        /// <summary>
        /// Creates a valid project with the necessary ruleset and assembly files on disc
        /// to successfully run the "OverrideRoslynCodeAnalysisProperties" target
        /// </summary>
        private string CreateProjectFile(AnalysisConfig analysisConfig, string testSpecificProjectXml)
        {
            var projectDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            CreateCaptureDataTargetsFile(projectDirectory);

            if (analysisConfig != null)
            {
                var configFilePath = Path.Combine(projectDirectory, FileConstants.ConfigFileName);
                analysisConfig.Save(configFilePath);
            }

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContext);
            File.Exists(sqTargetFile).Should().BeTrue("Test error: the SonarQube analysis targets file could not be found. Full path: {0}", sqTargetFile);
            TestContext.AddResultFile(sqTargetFile);

            string template = "";

            using (var stream = typeof(RazorTargetTests).Assembly.GetManifestResourceStream("SonarScanner.Integration.Tasks.IntegrationTests.Resources.RoslynTargetTestsTemplate.xml"))
            using (var reader = new StreamReader(stream))
            {
                template = reader.ReadToEnd();
            }

            var errorFilePath = string.Format(ErrorLogFilePattern, Path.Combine(projectDirectory, "bin", TestContext.TestName + ".proj.dll"));
            testSpecificProjectXml = testSpecificProjectXml.Replace("%%ErrorLogFile%%", errorFilePath);

            var projectData = template.Replace("PROJECT_DIRECTORY_PATH", projectDirectory)
                .Replace("SONARSCANNER_MSBUILD_TASKS_DLL", typeof(WriteProjectInfoFile).Assembly.Location)
                .Replace("TEST_SPECIFIC_XML", testSpecificProjectXml ?? "<!-- none -->");

            var projectFilePath = Path.Combine(projectDirectory, TestContext.TestName + ".proj.txt");
            File.WriteAllText(projectFilePath, projectData);
            TestContext.AddResultFile(projectFilePath);

            return projectFilePath;
        }

        private string CreateCaptureDataTargetsFile(string directory)
        {
            // Most of the tests above want to check the value of build property
            // or item group after a target has been executed. However, this
            // information is not available through the buildlogger interface.
            // So, we'll add a special target that writes the properties/items
            // we are interested in to the message log.
            // The SimpleXmlLogger has special handling to extract the data
            // from the message and add it to the BuildLog.

            // Make sure that the target is run after all of the targets
            // used by the any of the tests.
            string afterTargets = string.Join(";",
                TargetConstants.SetRazorCodeAnalysisPropertiesTarget
                );

            string xml = "";

            using (var stream = typeof(RazorTargetTests).Assembly.GetManifestResourceStream("SonarScanner.Integration.Tasks.IntegrationTests.Resources.RazorTargetTestsCaptureDataTargetsFileTemplate.xml"))
            using (var reader = new StreamReader(stream))
            {
                xml = reader.ReadToEnd();
            }

            xml = string.Format(xml, afterTargets);

            // We're using :: as a separator here: replace it with whatever
            // whatever the logger is using as a separator
            xml = xml.Replace("::", SimpleXmlLogger.CapturedDataSeparator);

            var filePath = Path.Combine(directory, "Capture.targets");
            File.WriteAllText(filePath, xml);
            TestContext.AddResultFile(filePath);
            return filePath;
        }
    }
}
