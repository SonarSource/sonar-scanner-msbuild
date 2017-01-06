/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.IO;
using System.Linq;
using System.Text;
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
            TestLogger testLogger = new TestLogger();
            string exePath = CreateDummarySonarScannerBatchFile();
            string propertiesFilePath = CreateDummySonarScannerPropertiesFile();

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(SonarScannerWrapper.SonarScannerHomeVariableName, null);
                AnalysisConfig config = new AnalysisConfig() { SonarScannerWorkingDirectory = this.TestContext.DeploymentDirectory };

                // Act
                bool success = SonarScannerWrapper.ExecuteJavaRunner(config, Enumerable.Empty<string>(), testLogger, exePath, propertiesFilePath);

                // Assert
                VerifyProcessRunOutcome(testLogger, this.TestContext.DeploymentDirectory, success, true);
                testLogger.AssertMessageNotLogged(SonarScanner.Shim.Resources.MSG_SonarScannerHomeIsSet);
            }
        }

        [TestMethod]
        public void SonarScannerrHome_MessageLoggedIfAlreadySet()
        {
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(SonarScannerWrapper.SonarScannerHomeVariableName, "some_path");

                // Arrange
                TestLogger testLogger = new TestLogger();
                string exePath = CreateDummarySonarScannerBatchFile();
                string propertiesFilePath = CreateDummySonarScannerPropertiesFile();
                AnalysisConfig config = new AnalysisConfig() { SonarScannerWorkingDirectory = this.TestContext.DeploymentDirectory };

                // Act
                bool success = SonarScannerWrapper.ExecuteJavaRunner(config, Enumerable.Empty<string>(), testLogger, exePath, propertiesFilePath);

                // Assert
                VerifyProcessRunOutcome(testLogger, this.TestContext.DeploymentDirectory, success, true);
            }
        }

        [TestMethod]
        public void SonarScanner_StandardAdditionalArgumentsPassed()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string exePath = CreateDummarySonarScannerBatchFile();
            string propertiesFilePath = CreateDummySonarScannerPropertiesFile();
            AnalysisConfig config = new AnalysisConfig() { SonarScannerWorkingDirectory = this.TestContext.DeploymentDirectory };

            // Act
            bool success = SonarScannerWrapper.ExecuteJavaRunner(config, Enumerable.Empty<string>(), logger, exePath, propertiesFilePath);

            // Assert
            VerifyProcessRunOutcome(logger, this.TestContext.DeploymentDirectory, success, true);
        }

        [TestMethod]
        public void SonarScanner_CmdLineArgsOrdering()
        {
            // Check that user arguments are passed through to the wrapper and that they appear first

            // Arrange
            TestLogger logger = new TestLogger();

            string exePath = CreateDummarySonarScannerBatchFile();
            string propertiesFilePath = CreateDummySonarScannerPropertiesFile();

            string[] userArgs = new string[] { "-Dsonar.login=me", "-Dsonar.password=my.pwd" };

            // Act
            bool success = SonarScannerWrapper.ExecuteJavaRunner(
                new AnalysisConfig() { SonarScannerWorkingDirectory = this.TestContext.DeploymentDirectory },
                userArgs,
                logger,
                exePath,
                propertiesFilePath);

            // Assert
            VerifyProcessRunOutcome(logger, this.TestContext.DeploymentDirectory, success, true);

            string actualCmdLineArgs = CheckStandardArgsPassed(logger, propertiesFilePath);

            int loginIndex = CheckArgExists("-Dsonar.login=me", actualCmdLineArgs);
            int pwdIndex = CheckArgExists("-Dsonar.password=my.pwd", actualCmdLineArgs);

            int propertiesFileIndex = CheckArgExists(SonarScannerWrapper.ProjectSettingsFileArgName, actualCmdLineArgs);

            Assert.IsTrue(loginIndex < propertiesFileIndex, "User arguments should appear first");
            Assert.IsTrue(pwdIndex < propertiesFileIndex, "User arguments should appear first");
        }

        [TestMethod]
        public void SonarScanner_SensitiveArgsPassedOnCommandLine()
        {
            // Check that sensitive arguments from the config are passed on the command line

            // Arrange
            TestLogger logger = new TestLogger();

            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string exePath = CreateDummarySonarScannerBatchFile();
            string propertiesFilePath = CreateDummySonarScannerPropertiesFile();

            string[] userArgs = new string[] { "-Dxxx=yyy", "-Dsonar.password=cmdline.password" };

            // Create a config file containing sensitive arguments
            AnalysisProperties fileSettings = new AnalysisProperties();
            fileSettings.Add(new Property() { Id = SonarProperties.DbPassword, Value = "file db pwd" });
            fileSettings.Add(new Property() { Id = SonarProperties.SonarPassword, Value = "file.password - should not be returned" });
            fileSettings.Add(new Property() { Id = "file.not.sensitive.key", Value = "not sensitive value" });
            string settingsFilePath = Path.Combine(testDir, "fileSettings.txt");
            fileSettings.Save(settingsFilePath);

            AnalysisConfig config = new AnalysisConfig() { SonarScannerWorkingDirectory = this.TestContext.DeploymentDirectory };
            config.SetSettingsFilePath(settingsFilePath);

            // Act
            bool success = SonarScannerWrapper.ExecuteJavaRunner(config, userArgs, logger, exePath, propertiesFilePath);

            // Assert
            VerifyProcessRunOutcome(logger, this.TestContext.DeploymentDirectory, success, true);
            string actualCmdLineArgs = CheckStandardArgsPassed(logger, propertiesFilePath);

            // Non-sensitive values from the file should not be passed on the command line
            CheckArgDoesNotExist("file.not.sensitive.key", actualCmdLineArgs);

            int dbPwdIndex = CheckArgExists("-Dsonar.jdbc.password=file db pwd", actualCmdLineArgs); // sensitive value from file
            int userPwdIndex = CheckArgExists("-Dsonar.password=cmdline.password", actualCmdLineArgs); // sensitive value from cmd line: overrides file value

            int propertiesFileIndex = CheckArgExists(SonarScannerWrapper.ProjectSettingsFileArgName, actualCmdLineArgs);

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
            TestLogger logger = new TestLogger();
            string exePath = CreateDummarySonarScannerBatchFile(addMessageToStdErr, exitCode);
            string propertiesFilePath = CreateDummySonarScannerPropertiesFile();
            AnalysisConfig config = new AnalysisConfig() { SonarScannerWorkingDirectory = this.TestContext.DeploymentDirectory };

            // Act
            bool success = SonarScannerWrapper.ExecuteJavaRunner(config, Enumerable.Empty<string>(), logger, exePath, propertiesFilePath);

            // Assert
            VerifyProcessRunOutcome(logger, this.TestContext.DeploymentDirectory, success, expectedOutcome);
        }

        #endregion Tests

        #region Private methods

        private string CreateDummarySonarScannerBatchFile(bool addMessageToStdErr = false, int? exitCode = null)
        {
            // Create a batch file that echoes the command line args to the console
            // so they can be captured and checked
            string testDir = TestUtils.EnsureTestSpecificFolder(this.TestContext);
            string exePath = Path.Combine(testDir, "dummy.scanner.bat");

            Assert.IsFalse(File.Exists(exePath), "Not expecting a batch file to already exist: {0}", exePath);

            StringBuilder sb = new StringBuilder();
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
            string testDir = TestUtils.EnsureTestSpecificFolder(this.TestContext);
            string propertiesFilePath = Path.Combine(testDir, "analysis.properties");
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
            string message = logger.AssertSingleInfoMessageExists(ExpectedConsoleMessagePrefix);

            CheckArgExists("-Dproject.settings=" + expectedPropertiesFilePath, message); // should always be passing the properties file

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

        #endregion Checks
    }
}