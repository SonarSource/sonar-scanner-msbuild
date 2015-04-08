//-----------------------------------------------------------------------
// <copyright file="ProcessRunnerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using Microsoft.VisualStudio.TestTools.UnitTesting;
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
@"TIMEOUT 2
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
        }

        #endregion


        #region Private methods

        /// <summary>
        /// Creates a batch file with the name of the current test
        /// </summary>
        /// <returns>Returns the full file name of the new file</returns>
        private string WriteBatchFileForTest(string content)
        {
            string fileName = Path.Combine(this.TestContext.TestDeploymentDir, this.TestContext.TestName + ".bat");
            Assert.IsFalse(File.Exists(fileName), "Not expecting a batch file to already exist: {0}", fileName);
            File.WriteAllText(fileName, content);
            return fileName;
        }

        #endregion
    }
}
