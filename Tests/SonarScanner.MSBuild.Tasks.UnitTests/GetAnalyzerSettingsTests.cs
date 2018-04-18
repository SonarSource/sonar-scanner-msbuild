/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Tasks.UnitTests
{
    [TestClass]
    public class GetAnalyzerSettingsTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void GetAnalyzerSettings_MissingConfigDir_NoError()
        {
            // Arrange
            var testSubject = new GetAnalyzerSettings
            {
                AnalysisConfigDir = "c:\\missing"
            };

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            CheckNoAnalyzerSettings(testSubject);
        }

        [TestMethod]
        public void GetAnalyzerSettings_MissingConfigFile_NoError()
        {
            // Arrange
            var testSubject = new GetAnalyzerSettings
            {
                AnalysisConfigDir = TestContext.DeploymentDirectory
            };

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            CheckNoAnalyzerSettings(testSubject);
        }

        [TestMethod]
        public void GetAnalyzerSettings_ConfigExistsButNoAnalyzerSettings_NoError()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var testSubject = new GetAnalyzerSettings();

            var config = new AnalysisConfig();
            var fullPath = Path.Combine(testDir, FileConstants.ConfigFileName);
            config.Save(fullPath);

            testSubject.AnalysisConfigDir = testDir;

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            CheckNoAnalyzerSettings(testSubject);
        }

        [TestMethod]
        public void GetAnalyzerSettings_ConfigExists_DataReturned()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var testSubject = new GetAnalyzerSettings();

            var expectedAnalyzers = new string[] { "c:\\analyzer1.DLL", "c:\\analyzer2.dll" };
            var expectedAdditionalFiles = new string[] { "c:\\add1.txt", "d:\\add2.txt" };

            // SONARMSBRU-216: non-assembly files should be filtered out
            var filesInConfig = new List<string>(expectedAnalyzers)
            {
                "c:\\not_an_assembly.exe",
                "c:\\not_an_assembly.zip",
                "c:\\not_an_assembly.txt",
                "c:\\not_an_assembly.dll.foo",
                "c:\\not_an_assembly.winmd"
            };

            var config = new AnalysisConfig
            {
                AnalyzersSettings = new List<AnalyzerSettings>()
            };

            var settings = new AnalyzerSettings
            {
                Language = "my lang",
                RuleSetFilePath = "f:\\yyy.ruleset",
                AnalyzerAssemblyPaths = filesInConfig,
                AdditionalFilePaths = expectedAdditionalFiles.ToList()
            };
            config.AnalyzersSettings.Add(settings);

            var anotherSettings = new AnalyzerSettings
            {
                Language = "cobol",
                RuleSetFilePath = "f:\\xxx.ruleset",
                AnalyzerAssemblyPaths = filesInConfig,
                AdditionalFilePaths = expectedAdditionalFiles.ToList()
            };
            config.AnalyzersSettings.Add(anotherSettings);

            var fullPath = Path.Combine(testDir, FileConstants.ConfigFileName);
            config.Save(fullPath);

            testSubject.AnalysisConfigDir = testDir;
            testSubject.Language = "my lang";

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            testSubject.RuleSetFilePath.Should().Be("f:\\yyy.ruleset");
            testSubject.AnalyzerFilePaths.Should().BeEquivalentTo(expectedAnalyzers);
            testSubject.AdditionalFiles.Should().BeEquivalentTo(expectedAdditionalFiles);
        }

        #endregion Tests

        #region Checks methods

        private static void ExecuteAndCheckSuccess(Task task)
        {
            var dummyEngine = new DummyBuildEngine();
            task.BuildEngine = dummyEngine;

            var taskSucess = task.Execute();
            taskSucess.Should().BeTrue("Expecting the task to succeed");
            dummyEngine.AssertNoErrors();
            dummyEngine.AssertNoWarnings();
        }

        private static void CheckNoAnalyzerSettings(GetAnalyzerSettings executedTask)
        {
            executedTask.RuleSetFilePath.Should().BeNull();
            executedTask.AdditionalFiles.Should().BeNull();
            executedTask.AnalyzerFilePaths.Should().BeNull();
        }

        #endregion Checks methods
    }
}
