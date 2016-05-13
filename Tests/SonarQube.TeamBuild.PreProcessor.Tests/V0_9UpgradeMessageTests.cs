//-----------------------------------------------------------------------
// <copyright file="V0_9UpgradeMessageTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class V0_9UpgradeMessageTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [Description("Checks that the upgrade helper exe logs an error and returns an error code")]
        public void V0_9Upgrade_CalledFromV0_9()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            ProcessRunnerArguments runnerArgs = new ProcessRunnerArguments(typeof(V0_9UpgradeMessageExe.Program).Assembly.Location, false, logger)
            {
                WorkingDirectory = this.TestContext.DeploymentDirectory
            };

            // Act
            ProcessRunner runner = new ProcessRunner();
            bool success = runner.Execute(runnerArgs);

            // Assert
            Assert.IsFalse(success);
            logger.AssertSingleErrorExists(SonarQube.V0_9UpgradeMessageExe.Resources.UpgradeMessage);
            logger.AssertErrorsLogged(1);
        }

    }
}
