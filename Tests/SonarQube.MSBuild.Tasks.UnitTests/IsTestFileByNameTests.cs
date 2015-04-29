//-----------------------------------------------------------------------
// <copyright file="IsTestFileByNameTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.Diagnostics;
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
        public void IsTestFile_NoRegex()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // 1. Check file names
            CheckFilePathIsNotTest(testFolder, "test"); // file name alone, no extension
            CheckFilePathIsNotTest(testFolder, "test.csproj"); // file name alone
            CheckFilePathIsNotTest(testFolder, "proj.test"); // ".test" extension
            CheckFilePathIsNotTest(testFolder, "proj.AtestB"); // part of the extension

            CheckFilePathIsNotTest(testFolder, "..\\..\\abc\\test.csproj"); // with relative path
            CheckFilePathIsNotTest(testFolder, "f:\\abc\\test.csproj"); // with absolute path
            CheckFilePathIsNotTest(testFolder, "d:\\abc\\TEST.csproj"); // case-sensitivity
            CheckFilePathIsNotTest(testFolder, "d:\\abc\\Another.test.vbproj"); // training "test"
            CheckFilePathIsNotTest(testFolder, "d:\\abc\\test.foo.proj"); // leading "test"
            CheckFilePathIsNotTest(testFolder, "d:\\abc\\XXXTesTyyy.proj"); // contained "test"

            CheckFilePathIsNotTest(testFolder, "c:\\aFile.csproj"); // doesn't contain "test"
            CheckFilePathIsNotTest(testFolder, "c:\\notATesFile.csproj"); // doesn't contain "test"

            // 2. Check for directory called "test"
            CheckFilePathIsNotTest(testFolder, "c:\\test\\my.csproj");
            CheckFilePathIsNotTest(testFolder, "c:\\aaa\\test\\bbb\\my.csproj"); // embedded in path
            CheckFilePathIsNotTest(testFolder, "..\\test\\bbb\\my.csproj"); // relative
            CheckFilePathIsNotTest(testFolder, ".\\TesT\\bbb\\my.csproj"); // case-sensitivity

            CheckFilePathIsNotTest(testFolder, "..\\Atest\\a.b"); // prefixed
            CheckFilePathIsNotTest(testFolder, "..\\testX\\a.b"); // suffixed
            CheckFilePathIsNotTest(testFolder, "..\\XXXtestYYY\\a.b"); // suffixed

            // 3. Check for directory called "tests"
            CheckFilePathIsNotTest(testFolder, "c:\\tests\\my.csproj");
            CheckFilePathIsNotTest(testFolder, "c:\\aaa\\tests\\bbb\\my.csproj"); // embedded in path
            CheckFilePathIsNotTest(testFolder, "..\\tests\\bbb\\my.csproj"); // relative
            CheckFilePathIsNotTest(testFolder, ".\\TesTs\\bbb\\my.csproj"); // case-sensitivity

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
            dummyEngine.AssertSingleErrorExists(invalidRegEx); // expecting the invalid expression to appear in the error
        }

        [TestMethod]
        [TestCategory("IsTest")] // Regression test for bug http://jira.codehaus.org/browse/SONARMSBRU-11
        public void IsTestFile_RetryIfConfigLocked()
        {
            // Arrange
            // We'll lock the file and sleep for long enough for the retry period to occur, but 
            // not so long that the task times out
            int lockPeriodInMilliseconds = 1000;
            Assert.IsTrue(lockPeriodInMilliseconds < IsTestFileByName.MaxConfigRetryPeriodInMilliseconds, "Test setup error: the test is sleeping for too long");

            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string configFile = EnsureAnalysisConfig(testFolder, ".XX.");

            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            IsTestFileByName task = new IsTestFileByName();
            task.BuildEngine = dummyEngine;
            task.FullFilePath = "XXX.proj";
            task.AnalysisConfigDir = testFolder;

            bool result;

            Stopwatch testDuration = Stopwatch.StartNew();

            using (FileStream lockingStream = File.OpenWrite(configFile))
            {
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                    {
                        System.Threading.Thread.Sleep(lockPeriodInMilliseconds); // unlock the file after a short delay
                        lockingStream.Close();
                    });

                result = task.Execute();
            }

            testDuration.Stop();
            Assert.IsTrue(testDuration.ElapsedMilliseconds > 1000, "Test error: expecting the test to have taken at least {0} milliseconds to run. Actual: {1}",
                lockPeriodInMilliseconds, testDuration.ElapsedMilliseconds);

            Assert.IsTrue(result, "Expecting the task to succeed");
            Assert.IsTrue(task.IsTest, "Expecting the file to be recognised as a test");

            dummyEngine.AssertMessageExists(IsTestFileByName.MaxConfigRetryPeriodInMilliseconds.ToString(), IsTestFileByName.DelayBetweenRetriesInMilliseconds.ToString());
            dummyEngine.AssertNoErrors();
            dummyEngine.AssertNoWarnings();
        }

        [TestMethod]
        [TestCategory("IsTest")] // Regression test for bug http://jira.codehaus.org/browse/SONARMSBRU-11
        public void IsTestFile_TimeoutIfConfigLocked()
        {
            // Arrange
            // We'll lock the file and sleep for long enough for the task to timeout
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string configFile = EnsureAnalysisConfig(testFolder, ".XX.");

            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            IsTestFileByName task = new IsTestFileByName();
            task.BuildEngine = dummyEngine;
            task.FullFilePath = "XXX.proj";
            task.AnalysisConfigDir = testFolder;

            bool result;

            using (FileStream lockingStream = File.OpenWrite(configFile))
            {
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    System.Threading.Thread.Sleep(IsTestFileByName.MaxConfigRetryPeriodInMilliseconds + 600); // sleep for longer than the timeout period
                    lockingStream.Close();
                });

                result = task.Execute();
            }

            Assert.IsFalse(result, "Expecting the task to fail");

            dummyEngine.AssertMessageExists(IsTestFileByName.MaxConfigRetryPeriodInMilliseconds.ToString(), IsTestFileByName.DelayBetweenRetriesInMilliseconds.ToString());
            dummyEngine.AssertNoWarnings();
            dummyEngine.AssertSingleErrorExists();
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void IsTestFile_RegExFromConfig()
        {
            // 0. Setup
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // 1a. Check the config setting is used if valid
            EnsureAnalysisConfig(testFolder, ".A.");
            CheckFilePathIsNotTest(testFolder, "c:\\test\\mytest.proj");
            CheckFilePathIsTest(testFolder, "c:\\aProject.proj");

            // 1b. Check another config valid config setting
            EnsureAnalysisConfig(testFolder, ".TEST.");
            CheckFilePathIsTest(testFolder, "c:\\test\\mytest.proj");
            CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");

            // 2. Check the default is used if the setting is missing
            EnsureAnalysisConfig(testFolder, null);
            CheckFilePathIsNotTest(testFolder, "c:\\test\\mytest.proj");
            CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");

            // 3a. Check the default is used if the setting is empty
            EnsureAnalysisConfig(testFolder, "");
            CheckFilePathIsNotTest(testFolder, "c:\\test\\mytest.proj");
            CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");

            // 3b. Check the default is used if the setting contains only whitespaces
            EnsureAnalysisConfig(testFolder, " ");
            CheckFilePathIsNotTest(testFolder, "c:\\test\\mytest.proj");
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
        private static string EnsureAnalysisConfig(string parentDir, string regExExpression)
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
            return fullPath;
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
