/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Shim.Tests
{
    [TestClass]
    public class SonarScannerWrapperTests
    {
        private const string ExpectedConsoleMessagePrefix = "Args passed to dummy scanner: ";

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void Execute_WhenConfigIsNull_Throws()
        {
            // Arrange
            var testSubject = new SonarScannerWrapper(new TestLogger());
            Action act = () => testSubject.Execute(null, new string[] { });

            // Act & Assert
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
        }

        [TestMethod]
        public void Execute_WhenUserCmdLineArgumentsIsNull_Throws()
        {
            // Arrange
            var testSubject = new SonarScannerWrapper(new TestLogger());
            Action act = () => testSubject.Execute(new AnalysisConfig(), null);

            // Act & Assert
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("userCmdLineArguments");
        }

        [TestMethod]
        public void Ctor_WhenLoggerIsNull_Throws()
        {
            // Arrange
            Action act = () => new SonarScannerWrapper(null);

            // Act & Assert
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void SonarScannerHome_NoMessageIfNotAlreadySet()
        {
            // Arrange
            var testLogger = new TestLogger();

            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(SonarScannerWrapper.SonarScannerHomeVariableName, null);
                var config = new AnalysisConfig() { SonarScannerWorkingDirectory = "C:\\working\\dir" };
                var mockRunner = new MockProcessRunner(executeResult: true);

                // Act
                var success = ExecuteJavaRunnerIgnoringAsserts(config, Enumerable.Empty<string>(), testLogger, "c:\\file.exe", "d:\\properties.prop", mockRunner);

                // Assert
                VerifyProcessRunOutcome(mockRunner, testLogger, "C:\\working\\dir", success, true);
                testLogger.AssertMessageNotLogged(SonarScanner.MSBuild.Shim.Resources.MSG_SonarScannerHomeIsSet);
            }
        }

        [TestMethod]
        public void SonarScannerHome_MessageLoggedIfAlreadySet()
        {
            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(SonarScannerWrapper.SonarScannerHomeVariableName, "some_path");

                // Arrange
                var testLogger = new TestLogger();
                var mockRunner = new MockProcessRunner(executeResult: true);
                var config = new AnalysisConfig() { SonarScannerWorkingDirectory = "c:\\workingDir" };

                // Act
                var success = ExecuteJavaRunnerIgnoringAsserts(config, Enumerable.Empty<string>(), testLogger, "c:\\exePath", "f:\\props.txt", mockRunner);

                // Assert
                VerifyProcessRunOutcome(mockRunner, testLogger, "c:\\workingDir", success, true);
                testLogger.AssertInfoMessageExists(SonarScanner.MSBuild.Shim.Resources.MSG_SonarScannerHomeIsSet);
            }
        }

        [TestMethod]
        public void SonarScanner_StandardAdditionalArgumentsPassed()
        {
            // Arrange
            var logger = new TestLogger();
            var mockRunner = new MockProcessRunner(executeResult: true);
            var config = new AnalysisConfig() { SonarScannerWorkingDirectory = "c:\\work" };

            // Act
            var success = ExecuteJavaRunnerIgnoringAsserts(config, Enumerable.Empty<string>(), logger, "c:\\exe.Path", "d:\\propertiesFile.Path", mockRunner);

            // Assert
            VerifyProcessRunOutcome(mockRunner, logger, "c:\\work", success, true);
        }

        [TestMethod]
        public void SonarScanner_CmdLineArgsOrdering()
        {
            // Check that user arguments are passed through to the wrapper and that they appear first

            // Arrange
            var logger = new TestLogger();
            var userArgs = new string[] { "-Dsonar.login=me", "-Dsonar.password=my.pwd" };

            var mockRunner = new MockProcessRunner(executeResult: true);

            // Act
            var success = ExecuteJavaRunnerIgnoringAsserts(
                new AnalysisConfig() { SonarScannerWorkingDirectory = "D:\\dummyWorkingDirectory" },
                userArgs,
                logger,
                "c:\\dummy.exe",
                "c:\\foo.properties",
                mockRunner);

            // Assert
            VerifyProcessRunOutcome(mockRunner, logger, "D:\\dummyWorkingDirectory", success, true);

            CheckStandardArgsPassed(mockRunner, "c:\\foo.properties");

            var loginIndex = CheckArgExists("-Dsonar.login=me", mockRunner);
            var pwdIndex = CheckArgExists("-Dsonar.password=my.pwd", mockRunner);

            var propertiesFileIndex = CheckArgExists(SonarScannerWrapper.ProjectSettingsFileArgName, mockRunner);

            propertiesFileIndex.Should().BeGreaterThan(loginIndex, "User arguments should appear first");
            propertiesFileIndex.Should().BeGreaterThan(pwdIndex, "User arguments should appear first");
        }

        [TestMethod]
        public void SonarScanner_SensitiveArgsPassedOnCommandLine()
        {
            // Check that sensitive arguments from the config are passed on the command line

            // Arrange
            var logger = new TestLogger();
            var mockRunner = new MockProcessRunner(executeResult: true);
            var userArgs = new string[] { "-Dxxx=yyy", "-Dsonar.password=cmdline.password" };

            // Create a config file containing sensitive arguments
            var fileSettings = new AnalysisProperties
            {
                new Property() { Id = SonarProperties.DbPassword, Value = "file db pwd" },
                new Property() { Id = SonarProperties.SonarPassword, Value = "file.password - should not be returned" },
                new Property() { Id = "file.not.sensitive.key", Value = "not sensitive value" }
            };

            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var settingsFilePath = Path.Combine(testDir, "fileSettings.txt");
            fileSettings.Save(settingsFilePath);

            var config = new AnalysisConfig() { SonarScannerWorkingDirectory = testDir };
            config.SetSettingsFilePath(settingsFilePath);

            // Act
            var success = ExecuteJavaRunnerIgnoringAsserts(config, userArgs, logger, "c:\\foo.exe", "c:\\foo.props", mockRunner);

            // Assert
            VerifyProcessRunOutcome(mockRunner, logger, testDir, success, true);

            CheckStandardArgsPassed(mockRunner, "c:\\foo.props");

            // Non-sensitive values from the file should not be passed on the command line
            CheckArgDoesNotExist("file.not.sensitive.key", mockRunner);

            var dbPwdIndex = CheckArgExists("-Dsonar.jdbc.password=file db pwd", mockRunner); // sensitive value from file
            var userPwdIndex = CheckArgExists("-Dsonar.password=cmdline.password", mockRunner); // sensitive value from cmd line: overrides file value

            var propertiesFileIndex = CheckArgExists(SonarScannerWrapper.ProjectSettingsFileArgName, mockRunner);

            propertiesFileIndex.Should().BeGreaterThan(dbPwdIndex, "User arguments should appear first");
            propertiesFileIndex.Should().BeGreaterThan(userPwdIndex, "User arguments should appear first");
        }

        [TestMethod]
        public void SonarScanner_NoUserSpecifiedEnvVars_SONARSCANNEROPTSIsNotPassed()
        {
            // Arrange
            var logger = new TestLogger();
            var mockRunner = new MockProcessRunner(executeResult: true);
            var config = new AnalysisConfig() { SonarScannerWorkingDirectory = "c:\\work" };

            // Act
            var success = ExecuteJavaRunnerIgnoringAsserts(config, Enumerable.Empty<string>(), logger, "c:\\exe.Path", "d:\\propertiesFile.Path", mockRunner);

            // Assert
            VerifyProcessRunOutcome(mockRunner, logger, "c:\\work", success, true);

            mockRunner.SuppliedArguments.EnvironmentVariables.Count.Should().Be(0);

            // #656: Check that the JVM size is not set by default
            // https://github.com/SonarSource/sonar-scanner-msbuild/issues/656
            logger.InfoMessages.Should().NotContain(msg => msg.Contains("SONAR_SCANNER_OPTS"));
        }

        [TestMethod]
        public void SonarScanner_UserSpecifiedEnvVars_OnlySONARSCANNEROPTSIsPassed()
        {
            // Arrange
            var logger = new TestLogger();
            var mockRunner = new MockProcessRunner(executeResult: true);
            var config = new AnalysisConfig() { SonarScannerWorkingDirectory = "c:\\work" };

            using (new EnvironmentVariableScope())
            {
                // the SONAR_SCANNER_OPTS variable should be passed through explicitly,
                // but not other variables
                Environment.SetEnvironmentVariable("Foo", "xxx");
                Environment.SetEnvironmentVariable("SONAR_SCANNER_OPTS", "-Xmx2048m");
                Environment.SetEnvironmentVariable("Bar", "yyy");

                // Act
                var success = ExecuteJavaRunnerIgnoringAsserts(config, Enumerable.Empty<string>(), logger, "c:\\exe.Path", "d:\\propertiesFile.Path", mockRunner);

                // Assert
                VerifyProcessRunOutcome(mockRunner, logger, "c:\\work", success, true);
            }

            CheckEnvVarExists("SONAR_SCANNER_OPTS", "-Xmx2048m", mockRunner);
            mockRunner.SuppliedArguments.EnvironmentVariables.Count.Should().Be(1);
            logger.InfoMessages.Should().Contain(msg => msg.Contains("SONAR_SCANNER_OPTS"));
            logger.InfoMessages.Should().Contain(msg => msg.Contains("-Xmx2048m"));
        }

        [TestMethod]
        public void WrapperError_Success_NoStdErr()
        {
            TestWrapperErrorHandling(executeResult: true, addMessageToStdErr: false, expectedOutcome: true);
        }

        [TestMethod]
        [WorkItem(202)] //SONARMSBRU-202
        public void WrapperError_Success_StdErr()
        {
            TestWrapperErrorHandling(executeResult: true, addMessageToStdErr: true, expectedOutcome: true);
        }

        [TestMethod]
        public void WrapperError_Fail_NoStdErr()
        {
            TestWrapperErrorHandling(executeResult: false, addMessageToStdErr: false, expectedOutcome: false);
        }

        [TestMethod]
        public void WrapperError_Fail_StdErr()
        {
            TestWrapperErrorHandling(executeResult: false, addMessageToStdErr: true, expectedOutcome: false);
        }

        private void TestWrapperErrorHandling(bool executeResult, bool addMessageToStdErr, bool expectedOutcome)
        {
            // Arrange
            var logger = new TestLogger();
            var mockRunner = new MockProcessRunner(executeResult);

            var config = new AnalysisConfig() { SonarScannerWorkingDirectory = "C:\\working" };

            if (addMessageToStdErr)
            {
                logger.LogError("Dummy error");
            }

            // Act
            var success = ExecuteJavaRunnerIgnoringAsserts(config, Enumerable.Empty<string>(), logger, "c:\\bar.exe", "c:\\props.xml", mockRunner);

            // Assert
            VerifyProcessRunOutcome(mockRunner, logger, "C:\\working", success, expectedOutcome);
        }

        #endregion Tests

        #region Private methods

        private static bool ExecuteJavaRunnerIgnoringAsserts(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger, string exeFileName, string propertiesFileName, IProcessRunner runner)
        {
            using (new AssertIgnoreScope())
            {
                return SonarScannerWrapper.ExecuteJavaRunner(config, userCmdLineArguments, logger, exeFileName, propertiesFileName, runner);
            }
        }

        #endregion Private methods

        #region Checks

        private static void VerifyProcessRunOutcome(MockProcessRunner mockRunner, TestLogger testLogger, string expectedWorkingDir, bool actualOutcome, bool expectedOutcome)
        {
            actualOutcome.Should().Be(expectedOutcome);

            mockRunner.SuppliedArguments.WorkingDirectory.Should().Be(expectedWorkingDir);

            if (actualOutcome)
            {
                // Errors can still be logged when the process completes successfully, so
                // we don't check the error log in this case
                testLogger.AssertInfoMessageExists(Resources.MSG_SonarScannerCompleted);
            }
            else
            {
                testLogger.AssertErrorsLogged();
                testLogger.AssertErrorLogged(Resources.ERR_SonarScannerExecutionFailed);
            }
        }

        private void CheckStandardArgsPassed(MockProcessRunner mockRunner, string expectedPropertiesFilePath)
        {
            CheckArgExists("-Dproject.settings=" + expectedPropertiesFilePath, mockRunner); // should always be passing the properties file
        }

        /// <summary>
        /// Checks that the argument exists, and returns the start position of the argument in the list of
        /// concatenated arguments so we can check that the arguments are passed in the correct order
        /// </summary>
        private int CheckArgExists(string expectedArg, MockProcessRunner mockRunner)
        {
            var allArgs = string.Join(" ", mockRunner.SuppliedArguments.CmdLineArgs);
            var index = allArgs.IndexOf(expectedArg);
            index.Should().BeGreaterThan(-1, "Expected argument was not found. Arg: '{0}', all args: '{1}'", expectedArg, allArgs);
            return index;

        }

        private static void CheckArgDoesNotExist(string argToCheck, MockProcessRunner mockRunner)
        {
            string allArgs = mockRunner.SuppliedArguments.GetEscapedArguments();
            var index = allArgs.IndexOf(argToCheck);
            index.Should().Be(-1, "Not expecting to find the argument. Arg: '{0}', all args: '{1}'", argToCheck, allArgs);
        }

        private static void CheckEnvVarExists(string varName, string expectedValue, MockProcessRunner mockRunner)
        {
            mockRunner.SuppliedArguments.EnvironmentVariables.ContainsKey(varName).Should().BeTrue();
            mockRunner.SuppliedArguments.EnvironmentVariables[varName].Should().Be(expectedValue);
        }

        #endregion Checks
    }
}
