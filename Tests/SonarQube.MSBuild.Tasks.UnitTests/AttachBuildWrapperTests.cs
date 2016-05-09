//-----------------------------------------------------------------------
// <copyright file="AttachBuildWrapperTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
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
        /// <summary>
        /// Name of the C++ plugin property that specifies where the build wrapper output is written
        /// </summary>
        private const string BuildOutputSettingName = "sonar.cfamily.build-wrapper-output";

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void AttachBW_MissingBinaryFiles_TaskFails()
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
                TaskExecutionContext taskContext = new TaskExecutionContext(testFolder, testFolder, testFolder, 0 /* returns failure code */ );

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
        public void AttachBW_MissingConfigFile_TaskFails()
        {
            // Arrange
            string binFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string configFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "config");
            DummyBuildEngine dummyEngine = new DummyBuildEngine();

            TaskExecutionContext taskContext = new TaskExecutionContext(binFolder, configFolder, null /* output folder */, 0 /* returns success code */ );

            // Remove the config file
            File.Delete(taskContext.ConfigFilePath);

            AttachBuildWrapper testSubject = CreateTestSubject(dummyEngine, binFolder, binFolder);

            // Act
            bool result = testSubject.Execute();

            // Assert
            Assert.IsFalse(result, "Expecting task to fail");

            dummyEngine.AssertSingleMessageExists(SonarQube.MSBuild.Tasks.Resources.Shared_ConfigFileNotFound);
            dummyEngine.AssertNoWarnings();

            AssertLogFileDoesNotExist(taskContext);
        }

        [TestMethod]
        public void AttachBW_MissingOutputDirectorySetting_TaskFails()
        {
            // Arrange
            string binFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string configFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "config");
            string outputFolder = Path.Combine(binFolder, "output");
            DummyBuildEngine dummyEngine = new DummyBuildEngine();

            TaskExecutionContext taskContext = new TaskExecutionContext(binFolder, configFolder, null /* no output dir set */, 0 /* returns success code */ );

            AttachBuildWrapper testSubject = CreateTestSubject(dummyEngine, binFolder, configFolder);

            // Act
            bool result = testSubject.Execute();

            // Assert
            Assert.IsFalse(result, "Expecting task to fail");
            Assert.IsFalse(Directory.Exists(outputFolder), "Not expecting the output folder to have been created");

            dummyEngine.AssertSingleErrorExists(SonarQube.MSBuild.Tasks.Resources.BuildWrapper_MissingOutputDirectory);
            dummyEngine.AssertNoWarnings();

            AssertLogFileDoesNotExist(taskContext);
        }

        [TestMethod]
        public void AttachBW_NotAttached_ExeReturnsSuccess_TaskSucceeds()
        {
            // Arrange
            string binFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string configFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "config");
            string outputFolder = Path.Combine(binFolder, "output"); // expected location: does not exist
            DummyBuildEngine dummyEngine = new DummyBuildEngine();

            TaskExecutionContext taskContext = new TaskExecutionContext(binFolder, configFolder, outputFolder, 0 /* returns success code */ );

            AttachBuildWrapper testSubject = CreateTestSubject(dummyEngine, binFolder, configFolder);

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
            string binFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string configFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "config");
            string outputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "bw_output");
            DummyBuildEngine dummyEngine = new DummyBuildEngine();

            TaskExecutionContext taskContext = new TaskExecutionContext(binFolder, configFolder, outputFolder, 1 /* returns failure code */ );

            AttachBuildWrapper testSubject = CreateTestSubject(dummyEngine, binFolder, configFolder);

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
            string outputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "bw_output");
            DummyBuildEngine dummyEngine = new DummyBuildEngine();

            TaskExecutionContext dummy = new TaskExecutionContext(testFolder, testFolder, outputFolder, 0 /* returns success code */,
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
            string outputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "bw_output");

            DummyBuildEngine dummyEngine = new DummyBuildEngine();

            TaskExecutionContext taskContext = new TaskExecutionContext(testFolder, testFolder, outputFolder, 0 /* returns success code */,
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
            string outputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "bw_output");
            DummyBuildEngine dummyEngine = new DummyBuildEngine();

            TaskExecutionContext taskContext = new TaskExecutionContext(testFolder, testFolder, outputFolder, 0 /* returns success code */,
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

        private static AttachBuildWrapper CreateTestSubject(DummyBuildEngine engine, string binPath, string configDir)
        {
            AttachBuildWrapper task = new AttachBuildWrapper();
            InitializeTask(task, engine, binPath, configDir);
            return task;
        }

        private static void InitializeTask(AttachBuildWrapper task, DummyBuildEngine engine, string binPath, string configDir)
        {
            task.BuildEngine = engine;
            task.BinDirectoryPath = binPath;
            task.AnalysisConfigDir = configDir;
        }

        private string AssertLogFileExists(TaskExecutionContext taskContext)
        {
            return DummyExeHelper.AssertLogFileExists(taskContext.ExpectedLogFilePath, this.TestContext);
        }

        private void AssertLogFileDoesNotExist(TaskExecutionContext taskContext)
        {
            Assert.IsFalse(File.Exists(taskContext.ExpectedLogFilePath), "Not expecting the log file to exist - the dummy exe should not have been executed");
        }

        #endregion

        /// <summary>
        /// Helper class that sets up the directories and files required
        /// to run the task sucessfully
        /// </summary>
        private class TaskExecutionContext
        {
            // Required binary files:
            private const string BuildWrapperExeName32 = "build-wrapper-win-x86-32.exe";
            private const string BuildWrapperExeName64 = "build-wrapper-win-x86-64.exe";
            public static readonly string[] RequiredFileNames = new string[] { BuildWrapperExeName32, BuildWrapperExeName64, "interceptor32.dll", "interceptor64.dll" };

            private readonly IList<string> requiredFilePaths;
            private readonly string logFilePath;
            private readonly string configFilePath;

            public TaskExecutionContext(string taskBinDir, string configDir, string outputDir, int exeReturnCode)
                : this(taskBinDir, configDir, outputDir, exeReturnCode, null)
            {
            }

            /// <summary>
            /// Create the required files on disk
            /// </summary>
            /// <param name="taskBinDir">The root bin directory for the task</param>
            /// <param name="configDir">The diectory in which the config file should be created</param>
            /// <param name="outputDir">The directory to which build wrapper output should be written. Can be null/empty.</param>
            /// <param name="exeReturnCode">The exit code that should be returned by the build wrapper executable</param>
            /// <param name="additionalCode">Any additional code to be embedded in the dummy build wrapper exectuable</param>
            public TaskExecutionContext(string taskBinDir, string configDir, string outputDir, int exeReturnCode, string additionalCode)
            {
                 this.configFilePath = CreateConfigFile(configDir, outputDir);

                // Work out the full paths to all of the required files
                this.requiredFilePaths = new List<string>();
                foreach (string requiredFileName in RequiredFileNames)
                {
                    this.requiredFilePaths.Add(Path.Combine(taskBinDir, requiredFileName));
                }

                // Create a dummy monitor exe - this file will actually be executed
                string exeName = Environment.Is64BitOperatingSystem ? BuildWrapperExeName64 : BuildWrapperExeName32;

                DummyExeHelper.CreateDummyExe(taskBinDir, exeName, exeReturnCode, additionalCode);
                this.logFilePath = DummyExeHelper.GetLogFilePath(taskBinDir, exeName);

                // Finally, create dummy files for all of the other required for which the content doesn't matter
                CreateMissingRequiredFiles();
            }

            public IList<string> RequiredFilePaths { get { return this.requiredFilePaths; } }

            public string ExpectedLogFilePath { get { return this.logFilePath; } }

            public string ConfigFilePath {  get { return this.configFilePath; } }

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

            /// <summary>
            /// Creates a configuration file in the specified directory.
            /// If an output directory is supplied then the value of the build wrapper output property
            /// will be set to that value.
            /// </summary>
            private static string CreateConfigFile(string configDir, string buildWrapperOutputDir)
            {
                AnalysisConfig config = new AnalysisConfig();
                config.LocalSettings = new AnalysisProperties();

                if (buildWrapperOutputDir != null)
                {
                    config.LocalSettings.Add(new Property() { Id = BuildOutputSettingName, Value = buildWrapperOutputDir });
                }

                string filePath = Path.Combine(configDir, FileConstants.ConfigFileName);
                config.Save(filePath);

                return filePath;
            }

            #endregion

        }

    }
}
