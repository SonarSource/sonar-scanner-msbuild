//-----------------------------------------------------------------------
// <copyright file="SonarRunnerWrapperTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarRunner.Shim.Tests
{
    [TestClass]
    public class SonarRunnerWrapperTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void SonarRunnerHome_NoMessageIfNotAlreadySet()
        {
            // Arrange
            TestLogger testLogger = new TestLogger();
            string exePath = TestUtils.WriteBatchFileForTest(TestContext, "exit 0");
            string path = Path.Combine(TestUtils.CreateTestSpecificFolder(TestContext), "analysis.properties");
            File.CreateText(path);

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(SonarRunnerWrapper.SonarRunnerHomeVariableName, null);

                // Act
                bool success = SonarRunnerWrapper.ExecuteJavaRunner(testLogger, exePath, exePath, Enumerable.Empty<string>());
                Assert.IsTrue(success, "Expecting execution to succeed");

                // Assert
                testLogger.AssertMessageNotLogged(SonarRunner.Shim.Resources.DIAG_SonarRunnerHomeIsSet);
            }
        }

        [TestMethod]
        public void SonarRunnerHome_MessageLoggedIfAlreadySet()
        {
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(SonarRunnerWrapper.SonarRunnerHomeVariableName, "some_path");

                // Arrange
                TestLogger testLogger = new TestLogger();
                string exePath = TestUtils.WriteBatchFileForTest(TestContext, "exit 0");
                string path = Path.Combine(TestUtils.CreateTestSpecificFolder(TestContext), "analysis.properties");
                File.CreateText(path);

                // Act
                bool success = SonarRunnerWrapper.ExecuteJavaRunner(testLogger, exePath, exePath, Enumerable.Empty<string>());

                // Assert
                Assert.IsTrue(success, "Expecting execution to succeed");
                testLogger.AssertMessageExists(SonarRunner.Shim.Resources.DIAG_SonarRunnerHomeIsSet);
            }
        }

        [TestMethod]
        public void SonarRunner_StandardAdditionalArgumentsPassed()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string exePath = TestUtils.WriteBatchFileForTest(this.TestContext, "@echo Command line args: %*");
            string propertiesFilePath = Path.Combine(TestUtils.CreateTestSpecificFolder(this.TestContext), "analysis.properties");
            File.CreateText(propertiesFilePath);

            // Act
            bool success = SonarRunnerWrapper.ExecuteJavaRunner(logger, exePath, propertiesFilePath, Enumerable.Empty<string>());
            Assert.IsTrue(success, "Expecting execution to succeed");
            logger.AssertSingleMessageExists("Command line args:", SonarRunnerWrapper.StandardAdditionalRunnerArguments);
        }
        
        [TestMethod]
        public void SonarRunner_CmdLineArgsOrdering()
        {
            // Check that user arguments are passed through to the wrapper and that they appear first

            // Arrange
            TestLogger logger = new TestLogger();
            string exePath = TestUtils.WriteBatchFileForTest(this.TestContext, "@echo Command line args: %*");
            string propertiesFilePath = Path.Combine(TestUtils.CreateTestSpecificFolder(this.TestContext), "analysis.properties");
            File.CreateText(propertiesFilePath);

            string[] userArgs = new string[] { "-Dsonar.login=me", "-Dsonar.password=my.pwd" };

            // Act
            bool success = SonarRunnerWrapper.ExecuteJavaRunner(logger, exePath, propertiesFilePath, userArgs);
            Assert.IsTrue(success, "Expecting execution to succeed");
            string message = logger.AssertSingleMessageExists("Command line args:",
                "-Dsonar.login=me", "-Dsonar.password=my.pwd",
                "-Dproject.settings=\"" + propertiesFilePath + "\"",
                SonarRunnerWrapper.StandardAdditionalRunnerArguments);

            int loginIndex = message.IndexOf("login=me");
            int pwdIndex = message.IndexOf("password=my.pwd");

            int standardArgsIndex = message.IndexOf(SonarRunnerWrapper.StandardAdditionalRunnerArguments);
            int propertiesFileIndex = message.IndexOf(SonarRunnerWrapper.ProjectSettingsFileArgName);

            Assert.IsTrue(loginIndex < standardArgsIndex && loginIndex < propertiesFileIndex, "User arguments should appear first");
            Assert.IsTrue(pwdIndex < standardArgsIndex && pwdIndex < propertiesFileIndex, "User arguments should appear first");
        }


        #endregion Tests
    }
}