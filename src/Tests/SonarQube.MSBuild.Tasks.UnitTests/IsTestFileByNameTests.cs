//-----------------------------------------------------------------------
// <copyright file="IsTestFileByNameTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.IO;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.UnitTests
{   
    [TestClass]
    public class IsTestFileByNameTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [TestCategory("IsTest")]
        public void IsTestFile_TestFilesAreRecognised_DefaultRegEx()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // 1. Check file names
            CheckFilePathIsTest(testFolder, "test"); // file name alone, no extension
            CheckFilePathIsTest(testFolder, "test.csproj"); // file name alone
            CheckFilePathIsTest(testFolder, "proj.test"); // ".test" extension
            CheckFilePathIsTest(testFolder, "proj.AtestB"); // part of the extension

            CheckFilePathIsTest(testFolder, "..\\..\\abc\\test.csproj"); // with relative path
            CheckFilePathIsTest(testFolder, "f:\\abc\\test.csproj"); // with absolute path
            CheckFilePathIsTest(testFolder, "d:\\abc\\TEST.csproj"); // case-sensitivity
            CheckFilePathIsTest(testFolder, "d:\\abc\\Another.test.vbproj"); // training "test"
            CheckFilePathIsTest(testFolder, "d:\\abc\\test.foo.proj"); // leading "test"
            CheckFilePathIsTest(testFolder, "d:\\abc\\XXXTesTyyy.proj"); // contained "test"

            // 2. Check for directory called "test"
            CheckFilePathIsTest(testFolder, "c:\\test\\my.csproj");
            CheckFilePathIsTest(testFolder, "c:\\aaa\\test\\bbb\\my.csproj"); // embedded in path
            CheckFilePathIsTest(testFolder, "..\\test\\bbb\\my.csproj"); // relative
            CheckFilePathIsTest(testFolder, ".\\TesT\\bbb\\my.csproj"); // case-sensitivity

            // 3. Check for directory called "tests"
            CheckFilePathIsTest(testFolder, "c:\\tests\\my.csproj");
            CheckFilePathIsTest(testFolder, "c:\\aaa\\tests\\bbb\\my.csproj"); // embedded in path
            CheckFilePathIsTest(testFolder, "..\\tests\\bbb\\my.csproj"); // relative
            CheckFilePathIsTest(testFolder, ".\\TesTs\\bbb\\my.csproj"); // case-sensitivity
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void IsTestFile_NonTestFilesAreRecognised_DefaultRegEx()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // 1. Check file names
            CheckFilePathIsNotTest(testFolder, "c:\\aFile.csproj"); // doesn't contain "test"
            CheckFilePathIsNotTest(testFolder, "c:\\notATesFile.csproj"); // doesn't contain "test"

            // 2. Directory names are not "test"
            CheckFilePathIsNotTest(testFolder, "..\\Atest\\a.b"); // prefixed
            CheckFilePathIsNotTest(testFolder, "..\\testX\\a.b"); // suffixed
            CheckFilePathIsNotTest(testFolder, "..\\XXXtestYYY\\a.b"); // suffixed

            // 3. Directory names are not "tests"
            CheckFilePathIsNotTest(testFolder, "..\\Atests\\a.b"); // prefixed
            CheckFilePathIsNotTest(testFolder, "..\\testsX\\a.b"); // suffixed
            CheckFilePathIsNotTest(testFolder, "..\\XXXtestsYYY\\a.b"); // suffixed
        }


        [TestMethod]
        [TestCategory("IsTest")]
        public void IsTestFile_InvalidRegexInConfig()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string invalidRegEx = "Invalid regex ((";
            EnsureAnalysisConfig(testFolder, invalidRegEx);

            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            IsTestFileByName task = new IsTestFileByName();
            task.BuildEngine = dummyEngine;
            task.FullFilePath = "Path";
            task.AnalysisConfigDir = testFolder;

            bool result = task.Execute();

            Assert.IsFalse(result, "Expecting the task to fail");
            dummyEngine.AssertErrorExists(invalidRegEx); // expecting the invalid expression to appear in the error
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void IsTestFile_RegExFromConfig()
        {
            // 0. Setup
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // 1. Check the config setting is used if valid
            EnsureAnalysisConfig(testFolder, ".A.");
            CheckFilePathIsNotTest(testFolder, "c:\\test\\mytest.proj");
            CheckFilePathIsTest(testFolder, "c:\\aProject.proj");

            // 2. Check the default is used if the setting is missing
            EnsureAnalysisConfig(testFolder, null);
            CheckFilePathIsTest(testFolder, "c:\\test\\mytest.proj");
            CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");

            // 3a. Check the default is used if the setting is empty
            EnsureAnalysisConfig(testFolder, "");
            CheckFilePathIsTest(testFolder, "c:\\test\\mytest.proj");
            CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");

            // 3b. Whitespace
            EnsureAnalysisConfig(testFolder, " ");
            CheckFilePathIsTest(testFolder, "c:\\test\\mytest.proj");
            CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Ensures an analysis config file exists in the specified directory,
        /// replacing one if it already exists.
        /// If the supplied "regExExpression" is not null then the appropriate setting
        /// entry will be created in the file
        /// </summary>
        private static void EnsureAnalysisConfig(string parentDir, string regExExpression)
        {
            AnalysisConfig config = new AnalysisConfig();
            if (regExExpression != null)
            {
                config.SetValue(IsTestFileByName.TestRegExSettingId, regExExpression);
            }

            string fullPath = Path.Combine(parentDir, FileConstants.ConfigFileName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            config.Save(fullPath);
        }

        #endregion

        #region Checks

        private static void CheckFilePathIsTest(string analysisDir, string fullFileName)
        {
            bool isTest = ExecuteAndCheckSuccess(analysisDir, fullFileName);
            Assert.IsTrue(isTest, "Expecting the file name to be recognised as a test file. Name: {0}", fullFileName);
        }

        private static void CheckFilePathIsNotTest(string analysisDir, string fullFileName)
        {
            bool isTest = ExecuteAndCheckSuccess(analysisDir, fullFileName);
            Assert.IsFalse(isTest, "Not expecting the file name to be recognised as a test file. Name: {0}", fullFileName);
        }

        private static bool ExecuteAndCheckSuccess(string analysisDir, string fullFileName)
        {
            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            IsTestFileByName task = new IsTestFileByName();
            task.BuildEngine = dummyEngine;
            task.FullFilePath = fullFileName;
            task.AnalysisConfigDir = analysisDir;

            bool taskSucess = task.Execute();
            Assert.IsTrue(taskSucess, "Expecting the task to succeed");
            dummyEngine.AssertNoErrors();
            dummyEngine.AssertNoWarnings();

            return task.IsTest;
        }

        #endregion

    }
}
