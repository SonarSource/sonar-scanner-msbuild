/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
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
 
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.UnitTests
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
            GetAnalyzerSettings testSubject = new GetAnalyzerSettings
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
            GetAnalyzerSettings testSubject = new GetAnalyzerSettings
            {
                AnalysisConfigDir = this.TestContext.DeploymentDirectory
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
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            GetAnalyzerSettings testSubject = new GetAnalyzerSettings();

            AnalysisConfig config = new AnalysisConfig();
            string fullPath = Path.Combine(testDir, FileConstants.ConfigFileName);
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
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            GetAnalyzerSettings testSubject = new GetAnalyzerSettings();

            string[] expectedAnalyzers = new string[] { "c:\\analyzer1.DLL", "c:\\analyzer2.dll" };
            string[] expectedAdditionalFiles = new string[] { "c:\\add1.txt", "d:\\add2.txt" };

            // SONARMSBRU-216: non-assembly files should be filtered out
            List<string> filesInConfig = new List<string>(expectedAnalyzers)
            {
                "c:\\not_an_assembly.exe",
                "c:\\not_an_assembly.zip",
                "c:\\not_an_assembly.txt",
                "c:\\not_an_assembly.dll.foo",
                "c:\\not_an_assembly.winmd"
            };

            AnalysisConfig config = new AnalysisConfig
            {
                AnalyzersSettings = new List<AnalyzerSettings>()
            };

            AnalyzerSettings settings = new AnalyzerSettings
            {
                Language = "my lang",
                RuleSetFilePath = "f:\\yyy.ruleset",
                AnalyzerAssemblyPaths = filesInConfig,
                AdditionalFilePaths = expectedAdditionalFiles.ToList()
            };
            config.AnalyzersSettings.Add(settings);

            AnalyzerSettings anotherSettings = new AnalyzerSettings
            {
                Language = "cobol",
                RuleSetFilePath = "f:\\xxx.ruleset",
                AnalyzerAssemblyPaths = filesInConfig,
                AdditionalFilePaths = expectedAdditionalFiles.ToList()
            };
            config.AnalyzersSettings.Add(anotherSettings);

            string fullPath = Path.Combine(testDir, FileConstants.ConfigFileName);
            config.Save(fullPath);

            testSubject.AnalysisConfigDir = testDir;
            testSubject.Language = "my lang";

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            Assert.AreEqual("f:\\yyy.ruleset", testSubject.RuleSetFilePath);
            CollectionAssert.AreEquivalent(expectedAnalyzers, testSubject.AnalyzerFilePaths);
            CollectionAssert.AreEquivalent(expectedAdditionalFiles, testSubject.AdditionalFiles);
        }

        #endregion

        #region Checks methods

        private static void ExecuteAndCheckSuccess(Task task)
        {
            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            task.BuildEngine = dummyEngine;

            bool taskSucess = task.Execute();
            Assert.IsTrue(taskSucess, "Expecting the task to succeed");
            dummyEngine.AssertNoErrors();
            dummyEngine.AssertNoWarnings();
        }

        private static void CheckNoAnalyzerSettings(GetAnalyzerSettings executedTask)
        {
            Assert.IsNull(executedTask.RuleSetFilePath);
            Assert.IsNull(executedTask.AdditionalFiles);
            Assert.IsNull(executedTask.AnalyzerFilePaths);
        }

        #endregion
    }
}
