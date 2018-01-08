/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using TestUtilities;

namespace SonarScanner.Shim.Tests
{
    [TestClass]
    public class SonarScannerWrapperTests
    {
        private const string ExpectedConsoleMessagePrefix = "Args passed to dummy scanner: ";

        public TestContext TestContext { get; set; }

        private const int SuccessExitCode = 0;
        private const int FailureExitCode = 4;

        #region Tests

        [TestMethod]
        public void SonarScannerHome_NoMessageIfNotAlreadySet()
        {
            // Arrange
            var testLogger = new TestLogger();
            var exePath = CreateDummarySonarScannerBatchFile();
            var propertiesFilePath = CreateDummySonarScannerPropertiesFile();

            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(SonarScannerWrapper.SonarScannerHomeVariableName, null);
                var config = new AnalysisConfig() { SonarScannerWorkingDirectory = TestContext.DeploymentDirectory };

                // Act
                var success = SonarScannerWrapper.ExecuteJavaRunner(config, Enumerable.Empty<string>(), testLogger, exePath, propertiesFilePath);

                // Assert
                VerifyProcessRunOutcome(testLogger, TestContext.DeploymentDirectory, success, true);
                testLogger.AssertMessageNotLogged(SonarScanner.Shim.Resources.MSG_SonarScannerHomeIsSet);
            }
        }

        [TestMethod]
        public void SonarScannerrHome_MessageLoggedIfAlreadySet()
        {
            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(SonarScannerWrapper.SonarScannerHomeVariableName, "some_path");

                // Arrange
                var testLogger = new TestLogger();
                var exePath = CreateDummarySonarScannerBatchFile();
                var propertiesFilePath = CreateDummySonarScannerPropertiesFile();
                var config = new AnalysisConfig() { SonarScannerWorkingDirectory = TestContext.DeploymentDirectory };

                // Act
                var success = SonarScannerWrapper.ExecuteJavaRunner(config, Enumerable.Empty<string>(), testLogger, exePath, propertiesFilePath);

                // Assert
                VerifyProcessRunOutcome(testLogger, TestContext.DeploymentDirectory, success, true);
            }
        }

        [TestMethod]
        public void SonarScanner_StandardAdditionalArgumentsPassed()
        {
            // Arrange
            var logger = new TestLogger();
            var exePath = CreateDummarySonarScannerBatchFile();
            var propertiesFilePath = CreateDummySonarScannerPropertiesFile();
            var config = new AnalysisConfig() { SonarScannerWorkingDirectory = TestContext.DeploymentDirectory };

            // Act
            var success = SonarScannerWrapper.ExecuteJavaRunner(config, Enumerable.Empty<string>(), logger, exePath, propertiesFilePath);

            // Assert
            VerifyProcessRunOutcome(logger, TestContext.DeploymentDirectory, success, true);
        }

        [TestMethod]
        public void SonarScanner_CmdLineArgsOrdering()
        {
            // Check that user arguments are passed through to the wrapper and that they appear first

            // Arrange
            var logger = new TestLogger();

            var exePath = CreateDummarySonarScannerBatchFile();
            var propertiesFilePath = CreateDummySonarScannerPropertiesFile();

            var userArgs = new string[] { "-Dsonar.login=me", "-Dsonar.password=my.pwd" };

            // Act
            var success = SonarScannerWrapper.ExecuteJavaRunner(
                new AnalysisConfig() { SonarScannerWorkingDirectory = TestContext.DeploymentDirectory },
                userArgs,
                logger,
                exePath,
                propertiesFilePath);

            // Assert
            VerifyProcessRunOutcome(logger, TestContext.DeploymentDirectory, success, true);

            var actualCmdLineArgs = CheckStandardArgsPassed(logger, propertiesFilePath);

            var loginIndex = CheckArgExists("-Dsonar.login=me", actualCmdLineArgs);
            var pwdIndex = CheckArgExists("-Dsonar.password=my.pwd", actualCmdLineArgs);

            var propertiesFileIndex = CheckArgExists(SonarScannerWrapper.ProjectSettingsFileArgName, actualCmdLineArgs);

            Assert.IsTrue(loginIndex < propertiesFileIndex, "User arguments should appear first");
            Assert.IsTrue(pwdIndex < propertiesFileIndex, "User arguments should appear first");
        }

        [TestMethod]
        public void SonarScanner_SensitiveArgsPassedOnCommandLine()
        {
            // Check that sensitive arguments from the config are passed on the command line

            // Arrange
            var logger = new TestLogger();

            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);

            var exePath = CreateDummarySonarScannerBatchFile();
            var propertiesFilePath = CreateDummySonarScannerPropertiesFile();

            var userArgs = new string[] { "-Dxxx=yyy", "-Dsonar.password=cmdline.password" };

            // Create a config file containing sensitive arguments
            var fileSettings = new AnalysisProperties
            {
                new Property() { Id = SonarProperties.DbPassword, Value = "file db pwd" },
                new Property() { Id = SonarProperties.SonarPassword, Value = "file.password - should not be returned" },
                new Property() { Id = "file.not.sensitive.key", Value = "not sensitive value" }
            };
            var settingsFilePath = Path.Combine(testDir, "fileSettings.txt");
            fileSettings.Save(settingsFilePath);

            var config = new AnalysisConfig() { SonarScannerWorkingDirectory = TestContext.DeploymentDirectory };
            config.SetSettingsFilePath(settingsFilePath);

            // Act
            var success = SonarScannerWrapper.ExecuteJavaRunner(config, userArgs, logger, exePath, propertiesFilePath);

            // Assert
            VerifyProcessRunOutcome(logger, TestContext.DeploymentDirectory, success, true);
            var actualCmdLineArgs = CheckStandardArgsPassed(logger, propertiesFilePath);

            // Non-sensitive values from the file should not be passed on the command line
            CheckArgDoesNotExist("file.not.sensitive.key", actualCmdLineArgs);

            var dbPwdIndex = CheckArgExists("-Dsonar.jdbc.password=file db pwd", actualCmdLineArgs); // sensitive value from file
            var userPwdIndex = CheckArgExists("-Dsonar.password=cmdline.password", actualCmdLineArgs); // sensitive value from cmd line: overrides file value

            var propertiesFileIndex = CheckArgExists(SonarScannerWrapper.ProjectSettingsFileArgName, actualCmdLineArgs);

            Assert.IsTrue(dbPwdIndex < propertiesFileIndex, "User arguments should appear first");
            Assert.IsTrue(userPwdIndex < propertiesFileIndex, "User arguments should appear first");
        }

        [TestMethod]
        public void WrapperError_Success_NoStdErr()
        {
            TestWrapperErrorHandling(exitCode: SuccessExitCode, addMessageToStdErr: false, expectedOutcome: true);
        }

        [TestMethod]
        [WorkItem(202)] //SONARMSBRU-202
        public void WrapperError_Success_StdErr()
        {
            TestWrapperErrorHandling(exitCode: SuccessExitCode, addMessageToStdErr: true, expectedOutcome: true);
        }

        [TestMethod]
        public void WrapperError_None_NoStdErr()
        {
            TestWrapperErrorHandling(exitCode: null, addMessageToStdErr: false, expectedOutcome: true);
        }

        [TestMethod]
        public void WrapperError_None_StdErr()
        {
            TestWrapperErrorHandling(exitCode: null, addMessageToStdErr: true, expectedOutcome: true);
        }

        [TestMethod]
        public void WrapperError_Fail_NoStdErr()
        {
            TestWrapperErrorHandling(exitCode: FailureExitCode, addMessageToStdErr: false, expectedOutcome: false);
        }

        [TestMethod]
        public void WrapperError_Fail_StdErr()
        {
            TestWrapperErrorHandling(exitCode: FailureExitCode, addMessageToStdErr: true, expectedOutcome: false);
        }

        private void TestWrapperErrorHandling(int? exitCode, bool addMessageToStdErr, bool expectedOutcome)
        {
            // Arrange
            var logger = new TestLogger();
            var exePath = CreateDummarySonarScannerBatchFile(addMessageToStdErr, exitCode);
            var propertiesFilePath = CreateDummySonarScannerPropertiesFile();
            var config = new AnalysisConfig() { SonarScannerWorkingDirectory = TestContext.DeploymentDirectory };

            // Act
            var success = SonarScannerWrapper.ExecuteJavaRunner(config, Enumerable.Empty<string>(), logger, exePath, propertiesFilePath);

            // Assert
            VerifyProcessRunOutcome(logger, TestContext.DeploymentDirectory, success, expectedOutcome);
        }

        #endregion Tests

        #region Private methods

        private string CreateDummarySonarScannerBatchFile(bool addMessageToStdErr = false, int? exitCode = null)
        {
            // Create a batch file that echoes the command line args to the console
            // so they can be captured and checked
            var testDir = TestUtils.EnsureTestSpecificFolder(TestContext);
            var exePath = Path.Combine(testDir, "dummy.scanner.bat");

            Assert.IsFalse(File.Exists(exePath), "Not expecting a batch file to already exist: {0}", exePath);

            var sb = new StringBuilder();
            sb.AppendLine("@echo " + ExpectedConsoleMessagePrefix + " %* \n @echo WorkingDir: %cd%");

            if (addMessageToStdErr)
            {
                sb.AppendLine("echo some_error_message 1>&2");
            }

            if (exitCode.HasValue)
            {
                sb.AppendLine("exit " + exitCode.Value);
            }

            File.WriteAllText(exePath, sb.ToString());
            return exePath;
        }

        private string CreateDummySonarScannerPropertiesFile()
        {
            var testDir = TestUtils.EnsureTestSpecificFolder(TestContext);
            var propertiesFilePath = Path.Combine(testDir, "analysis.properties");
            File.CreateText(propertiesFilePath);
            return propertiesFilePath;
        }

        #endregion Private methods

        #region Checks

        private static void VerifyProcessRunOutcome(TestLogger testLogger, string expectedWorkingDir, bool actualOutcome, bool expectedOutcome)
        {
            Assert.AreEqual(actualOutcome, expectedOutcome, "Expecting execution to succeed");
            testLogger.AssertInfoMessageExists(ExpectedConsoleMessagePrefix);
            testLogger.AssertInfoMessageExists(expectedWorkingDir);

            if (actualOutcome == false)
            {
                testLogger.AssertErrorsLogged();
            }
        }

        private static string CheckStandardArgsPassed(TestLogger logger, string expectedPropertiesFilePath)
        {
            var message = logger.AssertSingleInfoMessageExists(ExpectedConsoleMessagePrefix);

            CheckArgExists("-Dproject.settings=" + expectedPropertiesFilePath, message); // should always be passing the properties file

            return message;
        }

        private static int CheckArgExists(string expectedArg, string allArgs)
        {
            var index = allArgs.IndexOf(expectedArg);
            Assert.IsTrue(index > -1, "Expected argument was not found. Arg: '{0}', all args: '{1}'", expectedArg, allArgs);
            return index;
        }

        private static void CheckArgDoesNotExist(string argToCheck, string allArgs)
        {
            var index = allArgs.IndexOf(argToCheck);
            Assert.IsTrue(index == -1, "Not expecting to find the argument. Arg: '{0}', all args: '{1}'", argToCheck, allArgs);
        }

        #endregion Checks
    }
}
