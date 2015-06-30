//-----------------------------------------------------------------------
// <copyright file="ProcessRunnerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using TestUtilities;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class ProcessRunnerTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void ProcRunner_ExecutionFailed()
        {
            // Arrange
            string exeName = WriteBatchFileForTest("exit -2");

            TestLogger logger = new TestLogger();
            ProcessRunner runner = new ProcessRunner();

            // Act
            bool success = runner.Execute(exeName, null, null, 5000, logger);

            // Assert
            Assert.IsFalse(success, "Expecting the process to have failed");
            Assert.AreEqual(-2, runner.ExitCode, "Unexpected exit code");
        }

        [TestMethod]
        public void ProcRunner_ExecutionSucceeded()
        {
            // Arrange
            string exeName = WriteBatchFileForTest(
@"@echo Hello world
xxx yyy
@echo Testing 1,2,3...
");

            TestLogger logger = new TestLogger();
            ProcessRunner runner = new ProcessRunner();

            // Act
            bool success = runner.Execute(exeName, null, null, 1000, logger);

            // Assert
            Assert.IsTrue(success, "Expecting the process to have succeeded");
            Assert.AreEqual(0, runner.ExitCode, "Unexpected exit code");

            logger.AssertMessageLogged("Hello world"); // Check output message are passed to the logger
            logger.AssertMessageLogged("Testing 1,2,3...");
            logger.AssertErrorLogged("'xxx' is not recognized as an internal or external command,"); // Check error messages are passed to the logger
        }

        [TestMethod]
        public void ProcRunner_FailsOnTimeout()
        {
            // Arrange
            string exeName = WriteBatchFileForTest(
@"TIMEOUT 1
@echo Hello world
");

            TestLogger logger = new TestLogger();
            ProcessRunner runner = new ProcessRunner();

            // Act
            bool success = runner.Execute(exeName, null, null, 100, logger);

            // Assert
            Assert.IsFalse(success, "Expecting the process to have failed");
            Assert.AreEqual(0, runner.ExitCode, "Unexpected exit code");
            logger.AssertMessageNotLogged("Hello world");

            // Give the spawned process a chance to terminate.
            // This isn't essential (and having a Sleep in the test isn't ideal), but it stops
            // the test framework outputting this warning which appears in the TeamBuild summary:
            // "System.AppDomainUnloadedException: Attempted to access an unloaded AppDomain. This can happen 
            // if the test(s) started a thread but did not stop it. Make sure that all the threads started by 
            // the test(s) are stopped before completion."
            System.Threading.Thread.Sleep(1100);
        }

        [TestMethod]
        public void ProcRunner_PassesEnvVariables()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            ProcessRunner runner = new ProcessRunner();

            string exeName = WriteBatchFileForTest(
@"echo %PROCESS_VAR%
@echo %PROCESS_VAR2%
@echo %PROCESS_VAR3%
");
            var envVariables = new Dictionary<string, string>() {
                { "PROCESS_VAR", "PROCESS_VAR value" },
                { "PROCESS_VAR2", "PROCESS_VAR2 value" },
                { "PROCESS_VAR3", "PROCESS_VAR3 value" } };

            // Act
            bool success = runner.Execute(exeName, null, null, 150, envVariables, logger);

            // Assert
            Assert.IsTrue(success, "Expecting the process to have succeeded");
            Assert.AreEqual(0, runner.ExitCode, "Unexpected exit code");

            logger.AssertMessageLogged("PROCESS_VAR value");
            logger.AssertMessageLogged("PROCESS_VAR2 value");
            logger.AssertMessageLogged("PROCESS_VAR3 value");
        }

        [TestMethod]
        public void ProcRunner_PassesEnvVariables_OverrideExisting()
        {
            // Tests that existing environment variables will be overwritten successfully

            // Arrange
            TestLogger logger = new TestLogger();
            ProcessRunner runner = new ProcessRunner();

            try
            {
                // It's possible the user won't be have permissions to set machine level variables
                // (e.g. when running on a build agent). Carry on with testing the other variables.
                SafeSetEnvironmentVariable("proc.runner.test.machine", "existing machine value", EnvironmentVariableTarget.Machine, logger);
                Environment.SetEnvironmentVariable("proc.runner.test.process", "existing process value", EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("proc.runner.test.user", "existing user value", EnvironmentVariableTarget.User);

                string exeName = WriteBatchFileForTest(
@"@echo file: %proc.runner.test.machine%
@echo file: %proc.runner.test.process%
@echo file: %proc.runner.test.user%
");

                var envVariables = new Dictionary<string, string>() {
                    { "proc.runner.test.machine", "machine override" },
                    { "proc.runner.test.process", "process override" },
                    { "proc.runner.test.user", "user override" } };
  
                // Act
                bool success = runner.Execute(exeName, null, null, 200, envVariables, logger);

                // Assert
                Assert.IsTrue(success, "Expecting the process to have succeeded");
                Assert.AreEqual(0, runner.ExitCode, "Unexpected exit code");
            }
            finally
            {
                SafeSetEnvironmentVariable("proc.runner.test.machine", null, EnvironmentVariableTarget.Machine, logger);
                Environment.SetEnvironmentVariable("proc.runner.test.process", null, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("proc.runner.test.user", null, EnvironmentVariableTarget.User);
            }

            // Check the child process used expected values
            logger.AssertMessageLogged("file: machine override");
            logger.AssertMessageLogged("file: process override");
            logger.AssertMessageLogged("file: user override");

            // Check the runner reported it was overwriting existing variables
            // Note: the existing non-process values won't be visible to the child process
            // unless they were set *before* the test host launched, which won't be the case.
            logger.AssertSingleMessageExists("proc.runner.test.process", "existing process value", "process override");
        }

        #endregion


        #region Private methods

        /// <summary>
        /// Creates a batch file with the name of the current test
        /// </summary>
        /// <returns>Returns the full file name of the new file</returns>
        private string WriteBatchFileForTest(string content)
        {
            string fileName = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName + ".bat");
            Assert.IsFalse(File.Exists(fileName), "Not expecting a batch file to already exist: {0}", fileName);
            File.WriteAllText(fileName, content);
            return fileName;
        }

        private static void SafeSetEnvironmentVariable(string key, string value, EnvironmentVariableTarget target, ILogger logger)
        {
            try
            {
                Environment.SetEnvironmentVariable(key, value, target);
            }
            catch (System.Security.SecurityException)
            {
                logger.LogWarning("Test setup error: user running the test doesn't have the permissions to set the environment variable. Key: {0}, value: {1}, target: {2}",
                    key, value, target);
            }
        }

        #endregion
    }
}
