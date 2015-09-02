//-----------------------------------------------------------------------
// <copyright file="SonarRunnerWrapperTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarRunner.Shim.Tests
{
    [TestClass]
    public class SonarRunnerWrapperTests
    {
        private const string ExpectedConsoleMessagePrefix = "Args passed to dummy runner: ";

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void SonarRunnerHome_NoMessageIfNotAlreadySet()
        {
            // Arrange
            TestLogger testLogger = new TestLogger();
            string exePath = CreateDummarySonarRunnerBatchFile();
            string propertiesFilePath = CreateDummySonarRunnerPropertiesFile();

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(SonarRunnerWrapper.SonarRunnerHomeVariableName, null);

                // Act
                bool success = SonarRunnerWrapper.ExecuteJavaRunner(new AnalysisConfig(), Enumerable.Empty<string>(), testLogger, exePath, propertiesFilePath);
                Assert.IsTrue(success, "Expecting execution to succeed");

                // Assert
                testLogger.AssertMessageNotLogged(SonarRunner.Shim.Resources.MSG_SonarRunnerHomeIsSet);
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
                string exePath = CreateDummarySonarRunnerBatchFile();
                string propertiesFilePath = CreateDummySonarRunnerPropertiesFile();

                // Act
                bool success = SonarRunnerWrapper.ExecuteJavaRunner(new AnalysisConfig(), Enumerable.Empty<string>(), testLogger, exePath, propertiesFilePath);

                // Assert
                Assert.IsTrue(success, "Expecting execution to succeed");
                testLogger.AssertInfoMessageExists(SonarRunner.Shim.Resources.MSG_SonarRunnerHomeIsSet);
            }
        }

        [TestMethod]
        public void SonarRunner_StandardAdditionalArgumentsPassed()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string exePath = CreateDummarySonarRunnerBatchFile();
            string propertiesFilePath = CreateDummySonarRunnerPropertiesFile();

            // Act
            bool success = SonarRunnerWrapper.ExecuteJavaRunner(new AnalysisConfig(), Enumerable.Empty<string>(), logger, exePath, propertiesFilePath);
            Assert.IsTrue(success, "Expecting execution to succeed");
            CheckStandardArgsPassed(logger, propertiesFilePath);
        }

        [TestMethod]
        public void SonarRunner_CmdLineArgsOrdering()
        {
            // Check that user arguments are passed through to the wrapper and that they appear first

            // Arrange
            TestLogger logger = new TestLogger();

            string exePath = CreateDummarySonarRunnerBatchFile();
            string propertiesFilePath = CreateDummySonarRunnerPropertiesFile();

            string[] userArgs = new string[] { "-Dsonar.login=me", "-Dsonar.password=my.pwd" };

            // Act
            bool success = SonarRunnerWrapper.ExecuteJavaRunner(new AnalysisConfig(), userArgs, logger, exePath, propertiesFilePath);
            Assert.IsTrue(success, "Expecting execution to succeed");

            string actualCmdLineArgs = CheckStandardArgsPassed(logger, propertiesFilePath);
            
            int loginIndex = CheckArgExists("-Dsonar.login=me", actualCmdLineArgs);
            int pwdIndex = CheckArgExists("-Dsonar.password=my.pwd", actualCmdLineArgs);

            int standardArgsIndex = CheckArgExists(SonarRunnerWrapper.StandardAdditionalRunnerArguments, actualCmdLineArgs);
            int propertiesFileIndex = CheckArgExists(SonarRunnerWrapper.ProjectSettingsFileArgName, actualCmdLineArgs);

            Assert.IsTrue(loginIndex < standardArgsIndex && loginIndex < propertiesFileIndex, "User arguments should appear first");
            Assert.IsTrue(pwdIndex < standardArgsIndex && pwdIndex < propertiesFileIndex, "User arguments should appear first");
        }

        // SONARMSBRU-136: TODO - re-enable the following test:
        [Ignore]
        [TestMethod]
        public void SonarRunner_SensitiveArgsPassedOnCommandLine()
        {
            // Check that sensitive arguments from the config are passed on the command line

            // Arrange
            TestLogger logger = new TestLogger();

            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string exePath = CreateDummarySonarRunnerBatchFile();
            string propertiesFilePath = CreateDummySonarRunnerPropertiesFile();

            string[] userArgs = new string[] { "-Dxxx=yyy", "-Dsonar.password=cmdline.password" };

            // Create a config file containing sensitive arguments
            AnalysisProperties fileSettings = new AnalysisProperties();
            fileSettings.Add(new Property() { Id = SonarProperties.DbPassword, Value = "file db pwd" });
            fileSettings.Add(new Property() { Id = SonarProperties.SonarPassword, Value = "file.password - should not be returned" });
            fileSettings.Add(new Property() { Id = "file.not.sensitive.key", Value = "not sensitive value" });
            string settingsFilePath = Path.Combine(testDir, "fileSettings.txt");
            fileSettings.Save(settingsFilePath);

            AnalysisConfig config = new AnalysisConfig();
            config.SetSettingsFilePath(settingsFilePath);

            // Act
            bool success = SonarRunnerWrapper.ExecuteJavaRunner(config, userArgs, logger, exePath, propertiesFilePath);
            Assert.IsTrue(success, "Expecting execution to succeed");

            string actualCmdLineArgs = CheckStandardArgsPassed(logger, propertiesFilePath);
             
            // Assert
            // Non-sensitive values from the file should not be passed on the command line
            CheckArgDoesNotExist("file.not.sensitive.key", actualCmdLineArgs);

            int dbPwdIndex = CheckArgExists("-Dsonar.jdbc.password=\"file db pwd\"", actualCmdLineArgs); // sensitive value from file
            int userPwdIndex = CheckArgExists("-Dsonar.password=cmdline.password", actualCmdLineArgs); // sensitive value from cmd line: overrides file value

            int standardArgsIndex = CheckArgExists(SonarRunnerWrapper.StandardAdditionalRunnerArguments, actualCmdLineArgs);
            int propertiesFileIndex = CheckArgExists(SonarRunnerWrapper.ProjectSettingsFileArgName, actualCmdLineArgs);

            Assert.IsTrue(dbPwdIndex < standardArgsIndex && dbPwdIndex < propertiesFileIndex, "User arguments should appear first");
            Assert.IsTrue(userPwdIndex < standardArgsIndex && userPwdIndex < propertiesFileIndex, "User arguments should appear first");
        }

        #endregion Tests

        #region Private methods

        private string CreateDummarySonarRunnerBatchFile()
        {
            // Create a batch file that echoes the command line args to the console 
            // so they can be captured and checked
            string testDir = TestUtils.EnsureTestSpecificFolder(this.TestContext);
            string exePath = Path.Combine(testDir, "dummy.runner.bat");

            Assert.IsFalse(File.Exists(exePath), "Not expecting a batch file to already exist: {0}", exePath);

            File.WriteAllText(exePath, "@echo " + ExpectedConsoleMessagePrefix + " %*");
            return exePath;
        }

        private string CreateDummySonarRunnerPropertiesFile()
        {
            string testDir = TestUtils.EnsureTestSpecificFolder(this.TestContext);
            string propertiesFilePath = Path.Combine(testDir, "analysis.properties");
            File.CreateText(propertiesFilePath);
            return propertiesFilePath;
        }

        #endregion

        #region Checks

        private static string CheckStandardArgsPassed(TestLogger logger, string expectedPropertiesFilePath)
        {
            string message = logger.AssertSingleInfoMessageExists(ExpectedConsoleMessagePrefix);

            CheckArgExists("-Dproject.settings=\"" + expectedPropertiesFilePath + "\"", message); // should always be passing the properties file 
            CheckArgExists(SonarRunnerWrapper.StandardAdditionalRunnerArguments, message); // standard args should always be passed

            return message;
        }

        private static int CheckArgExists(string expectedArg, string allArgs)
        {
            int index = allArgs.IndexOf(expectedArg);
            Assert.IsTrue(index > -1, "Expected argument was not found. Arg: '{0}', all args: '{1}'", expectedArg, allArgs);
            return index;
        }

        private static void CheckArgDoesNotExist(string argToCheck, string allArgs)
        {
            int index = allArgs.IndexOf(argToCheck);
            Assert.IsTrue(index == -1, "Not expecting to find the argument. Arg: '{0}', all args: '{1}'", argToCheck, allArgs);
        }

        #endregion
    }
}