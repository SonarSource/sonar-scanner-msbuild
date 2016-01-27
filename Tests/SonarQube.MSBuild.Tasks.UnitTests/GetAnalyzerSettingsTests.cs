//-----------------------------------------------------------------------
// <copyright file="GetAnalyzerSettingsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
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
            GetAnalyzerSettings testSubject = new GetAnalyzerSettings();
            testSubject.AnalysisConfigDir = "c:\\missing";

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            CheckNoAnalyzerSettings(testSubject);
        }

        [TestMethod]
        public void GetAnalyzerSettings_MissingConfigFile_NoError()
        {
            // Arrange
            GetAnalyzerSettings testSubject = new GetAnalyzerSettings();
            testSubject.AnalysisConfigDir = this.TestContext.DeploymentDirectory;

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

            string[] expectedAnalyzers = new string[] { "c:\\analyzer1.dll", "c:\\analyzer2.dll" };
            string[] expectedAdditionalFiles = new string[] { "c:\\add1.txt", "d:\\add2.txt" };

            AnalysisConfig config = new AnalysisConfig();
            config.AnalyzerSettings = new AnalyzerSettings();
            config.AnalyzerSettings.RuleSetFilePath = "f:\\yyy.ruleset";
            config.AnalyzerSettings.AnalyzerAssemblyPaths = expectedAnalyzers.ToList();
            config.AnalyzerSettings.AdditionalFilePaths = expectedAdditionalFiles.ToList();
            string fullPath = Path.Combine(testDir, FileConstants.ConfigFileName);
            config.Save(fullPath);

            testSubject.AnalysisConfigDir = testDir;

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
