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
            ProcessRunnerArguments scannerArgs = new ProcessRunnerArguments(typeof(V0_9UpgradeMessageExe.Program).Assembly.Location, false, logger)
            {
                WorkingDirectory = this.TestContext.DeploymentDirectory
            };

            // Act
            ProcessRunner runner = new ProcessRunner();
            bool success = runner.Execute(scannerArgs);

            // Assert
            Assert.IsFalse(success);
            logger.AssertSingleErrorExists(SonarQube.V0_9UpgradeMessageExe.Resources.UpgradeMessage);
            logger.AssertErrorsLogged(1);
        }

    }
}
