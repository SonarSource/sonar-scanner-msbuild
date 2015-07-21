//-----------------------------------------------------------------------
// <copyright file="SonarRunnerWrapperTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
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
                SonarRunnerWrapper.ExecuteJavaRunner(testLogger, exePath, exePath);
            }

            // Assert
            testLogger.AssertMessageNotLogged(SonarRunner.Shim.Resources.DIAG_SonarRunnerHomeIsSet);
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
                SonarRunnerWrapper.ExecuteJavaRunner(testLogger, exePath, exePath);

                // Assert
                testLogger.AssertMessageExists(SonarRunner.Shim.Resources.DIAG_SonarRunnerHomeIsSet);
            }
        }

        #endregion Tests
    }
}