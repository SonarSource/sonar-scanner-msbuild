//-----------------------------------------------------------------------
// <copyright file="AttachBuildWrapperTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.UnitTests
{
    [TestClass]
    public class AttachBuildWrapperTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void AttachBW_MissingFiles_Fails()
        {
            // Create a valid setup, then delete one of the required files each time
            // and check that the task fails

            // Arrange
            string testFolder = TestUtils.GetTestSpecificFolderName(this.TestContext);

            for (int i = 0; i < TaskExecutionContext.RequiredFileNames.Length; i++)
            {
                // Clean up after the previous iteration
                if (Directory.Exists(testFolder))
                {
                    Directory.Delete(testFolder, true);
                }
                Directory.CreateDirectory(testFolder);
                DummyBuildEngine dummyEngine = new DummyBuildEngine();
                TaskExecutionContext taskContext = new TaskExecutionContext(testFolder, 0 /* returns failure code */ );

                string missingFilePath = taskContext.RequiredFilePaths[i];

                File.Delete(missingFilePath); // delete one of the required files

                AttachBuildWrapper testSubject = CreateTestSubject(dummyEngine, testFolder, testFolder);

                // Act and assert
                bool success = testSubject.Execute();
                Assert.IsFalse(success, "Expecting the task execution to fail");
                dummyEngine.AssertSingleErrorExists(missingFilePath);
            }
        }

        [TestMethod]
        public void AttachBW_NotAttached_ExeReturnsSuccess_TaskSucceeds()
        {
            // Arrange
            string rootBinFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string outputFolder = Path.Combine(rootBinFolder, "output"); // expected location: does not exist
            DummyBuildEngine dummyEngine = new DummyBuildEngine();

            TaskExecutionContext taskContext = new TaskExecutionContext(rootBinFolder, 0 /* returns success code */ );

            AttachBuildWrapper testSubject = CreateTestSubject(dummyEngine, rootBinFolder, outputFolder);

            // Act
            bool result = testSubject.Execute();

            // Assert
            Assert.IsTrue(result, "Expecting task to succeed");
            Assert.IsTrue(Directory.Exists(outputFolder), "Expecting the output folder to have been created");

            dummyEngine.AssertNoErrors();
            dummyEngine.AssertNoWarnings();
            dummyEngine.AssertSingleMessageExists(SonarQube.MSBuild.Tasks.Resources.BuildWrapper_AttachedSuccessfully);

            // Check the parameters passed to the exe
            string logPath = AssertLogFileExists(taskContext);
            DummyExeHelper.AssertExpectedLogContents(logPath,
                "--msbuild-task",
                Process.GetCurrentProcess().Id.ToString(),
                outputFolder);
        }

        [TestMethod]
        public void AttachBW_NotAttached_ExeReturnsFailure_TaskFails()
        {
            // Arrange
            string rootBinFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string outputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "output");
            DummyBuildEngine dummyEngine = new DummyBuildEngine();

            TaskExecutionContext taskContext = new TaskExecutionContext(rootBinFolder, 1 /* returns failure code */ );

            AttachBuildWrapper testSubject = CreateTestSubject(dummyEngine, rootBinFolder, outputFolder);

            // Act
            bool result = testSubject.Execute();

            // Assert
            Assert.IsFalse(result, "Not expecting the task to succeed");

            dummyEngine.AssertSingleErrorExists(SonarQube.MSBuild.Tasks.Resources.BuildWrapper_FailedToAttach);
            dummyEngine.AssertNoWarnings();

            // Check the parameters passed to the exe
            string logPath = AssertLogFileExists(taskContext);
            DummyExeHelper.AssertExpectedLogContents(logPath,
                "--msbuild-task",
                Process.GetCurrentProcess().Id.ToString(),
                outputFolder);
        }

        [TestMethod]
        public void AttachBW_NotAttached_ExeThrows_TaskFails()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            DummyBuildEngine dummyEngine = new DummyBuildEngine();

            TaskExecutionContext dummy = new TaskExecutionContext(testFolder, 0 /* returns success code */,
                // Embed additional code in the dummy exe
                @"throw new System.Exception(""XXX thrown error should be captured"");");

            AttachBuildWrapper testSubject = CreateTestSubject(dummyEngine, testFolder, testFolder);

            // Act
            bool result = testSubject.Execute();

            // Assert
            Assert.IsFalse(result, "Not expecting the task to succeed");

            dummyEngine.AssertSingleErrorExists(SonarQube.MSBuild.Tasks.Resources.BuildWrapper_FailedToAttach);
            dummyEngine.AssertNoWarnings();

            dummyEngine.AssertSingleErrorExists("XXX thrown error should be captured");
        }

        [TestMethod]
        public void AttachBW_NotAttached_ConsoleOutputIsCaptured()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            DummyBuildEngine dummyEngine = new DummyBuildEngine();

            TaskExecutionContext taskContext = new TaskExecutionContext(testFolder, 0 /* returns success code */,
                // Embed additional code in the dummy exe
                @"System.Console.WriteLine(""AAA standard output should be captured"");
                  System.Console.Error.WriteLine(""BBB standard error should be captured"");");

            AttachBuildWrapper testSubject = CreateTestSubject(dummyEngine, testFolder, testFolder);

            // Act
            bool result = testSubject.Execute();

            // Assert
            Assert.IsTrue(result, "Expecting the task to succeed");

            dummyEngine.AssertNoWarnings();

            dummyEngine.AssertSingleMessageExists("AAA standard output should be captured");
            dummyEngine.AssertSingleErrorExists("BBB standard error should be captured");
        }

        [TestMethod]
        public void AttachBW_NotAttached_Timeout_TaskFails()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            DummyBuildEngine dummyEngine = new DummyBuildEngine();

            TaskExecutionContext taskContext = new TaskExecutionContext(testFolder, 0 /* returns success code */,
                // Embed additional code in the dummy exe - pause execution for 100 ms
                @"System.Threading.Thread.Sleep(200);");

            AttachBuildWrapper testSubject = new AttachBuildWrapper(11 /* timeout after 11 ms */);
            InitializeTask(testSubject, dummyEngine, testFolder, testFolder);

            // Act
            bool result = testSubject.Execute();

            // Assert
            Assert.IsFalse(result, "Not expecting the task to succeed - should have timed out");

            dummyEngine.AssertSingleErrorExists(SonarQube.MSBuild.Tasks.Resources.BuildWrapper_FailedToAttach);
            dummyEngine.AssertSingleWarningExists("11"); // expecting a warning with the timeout value
        }

        #endregion

        #region Private methods

        private static AttachBuildWrapper CreateTestSubject(DummyBuildEngine engine, string binPath, string outputPath)
        {
            AttachBuildWrapper task = new AttachBuildWrapper();
            InitializeTask(task, engine, binPath, outputPath);
            return task;
        }

        private static void InitializeTask(AttachBuildWrapper task, DummyBuildEngine engine, string binPath, string outputPath)
        {
            task.BuildEngine = engine;
            task.BinDirectoryPath = binPath;
            task.OutputDirectoryPath = outputPath;
        }

        private string AssertLogFileExists(TaskExecutionContext taskContext)
        {
            return DummyExeHelper.AssertLogFileExists(taskContext.ExpectedLogFilePath, this.TestContext);
        }

        #endregion

        /// <summary>
        /// Helper class that sets up the directories and files required
        /// to run the task sucessfully
        /// </summary>
        private class TaskExecutionContext
        {
            private const string BuildWrapperSubDirName = "build-wrapper-win-x86";

            // Required files:
            private const string BuildWrapperExeName32 = "build-wrapper-win-x86-32.exe";
            private const string BuildWrapperExeName64 = "build-wrapper-win-x86-64.exe";
            public static readonly string[] RequiredFileNames = new string[] { BuildWrapperExeName32, BuildWrapperExeName64, "interceptor32.dll", "interceptor64.dll" };

            private readonly string buildWrapperBinPath;
            private readonly IList<string> requiredFilePaths;
            private readonly string logFilePath;

            public TaskExecutionContext(string rootDir, int exeReturnCode)
                : this(rootDir, exeReturnCode, null)
            {
            }

            /// <summary>
            /// Create the required files on disk
            /// </summary>
            /// <param name="rootDir">The root bin directory for the task</param>
            /// <param name="exeReturnCode">The exit code that should be returned by the build wrapper executable</param>
            /// <param name="additionalCode">Any additional code to be embedded in the dummy build wrapper exectuable</param>
            public TaskExecutionContext(string rootDir, int exeReturnCode, string additionalCode)
            {
                this.buildWrapperBinPath = Path.Combine(rootDir, BuildWrapperSubDirName);
                Directory.CreateDirectory(buildWrapperBinPath);

                // Work out the full paths to all of the required files
                this.requiredFilePaths = new List<string>();
                foreach (string requiredFileName in RequiredFileNames)
                {
                    this.requiredFilePaths.Add(Path.Combine(this.BuildWrapperBinPath, requiredFileName));
                }

                // Create a dummy monitor exe - this file will actually be executed
                string exeName = Environment.Is64BitProcess ? BuildWrapperExeName64 : BuildWrapperExeName32;

                DummyExeHelper.CreateDummyExe(buildWrapperBinPath, exeName, exeReturnCode, additionalCode);
                this.logFilePath = DummyExeHelper.GetLogFilePath(buildWrapperBinPath, exeName);

                // Finally, create dummy files for all of the other required for which the content doesn't matter
                CreateMissingRequiredFiles();
            }

            public string BuildWrapperBinPath { get { return this.buildWrapperBinPath; } }

            public IList<string> RequiredFilePaths { get { return this.requiredFilePaths; } }

            public string ExpectedLogFilePath { get { return this.logFilePath; } }

            #region Private methods

            private void CreateMissingRequiredFiles()
            {
                foreach(string filePath in this.RequiredFilePaths)
                {
                    if (!File.Exists(filePath))
                    {
                        File.WriteAllText(filePath, "dummy file content");
                    }
                }
            }

            #endregion

        }

    }
}
