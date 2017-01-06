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
using System.Diagnostics;
using System.IO;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.UnitTests
{
    [TestClass]
    public class TaskUtilitiesTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [TestCategory("Task")] // Regression test for bug http://jira.codehaus.org/browse/SONARMSBRU-11
        public void TaskUtils_LoadConfig_RetryIfConfigLocked_ValueReturned()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string configFile = CreateAnalysisConfig(testFolder);

            TestLogger logger = new TestLogger();

            AnalysisConfig result = null;
            PerformOpOnLockedFile(configFile, () => result = TaskUtilities.TryGetConfig(testFolder, logger), shouldTimeoutReadingConfig: false);

            Assert.IsNotNull(result, "Expecting the config to have been loaded");

            AssertRetryAttempted(logger);
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        [TestCategory("Task")] // Regression test for bug http://jira.codehaus.org/browse/SONARMSBRU-11
        public void TaskUtils_LoadConfig_TimeoutIfConfigLocked_NullReturned()
        {
            // Arrange
            // We'll lock the file and sleep for long enough for the task to timeout
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string configFile = CreateAnalysisConfig(testFolder);

            TestLogger logger = new TestLogger();

            AnalysisConfig result = null;

            PerformOpOnLockedFile(configFile, () => result = TaskUtilities.TryGetConfig(testFolder, logger), shouldTimeoutReadingConfig: true);

            Assert.IsNull(result, "Not expecting the config to be retrieved");

            AssertRetryAttempted(logger);
            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(1);
        }

        [TestMethod]
        public void TaskUtils_TryGetMissingConfig_NoError()
        {
            // Arrange
            ILogger logger = new TestLogger();

            // 1. Null -> no error
            AnalysisConfig actual = TaskUtilities.TryGetConfig(null, logger);
            Assert.IsNull(actual);

            // 2. Empty -> no error
            actual = TaskUtilities.TryGetConfig(string.Empty, logger);
            Assert.IsNull(actual);

            // 3. Missing -> no error
            actual = TaskUtilities.TryGetConfig("c:\\missing\\dir", logger);
            Assert.IsNull(actual);
        }

        #endregion

        #region Public test helpers

        /// <summary>
        /// Performs the specified operation against a locked analysis config file
        /// </summary>
        /// <param name="configFile">The config file to be read</param>
        /// <param name="op">The test operation to perform against the locked file</param>
        /// <param name="shouldTimeoutReadingConfig">When the operation should timeout or not</param>
        public static void PerformOpOnLockedFile(string configFile, System.Action op, bool shouldTimeoutReadingConfig)
        {
            Assert.IsTrue(File.Exists(configFile), "Test setup error: specified config file should exist: {0}", configFile);

            int lockPeriodInMilliseconds;
            if (shouldTimeoutReadingConfig)
            {
                lockPeriodInMilliseconds = TaskUtilities.MaxConfigRetryPeriodInMilliseconds + 600; // sleep for longer than the timeout period
            }
            else
            {
                // We'll lock the file and sleep for long enough for the retry period to occur, but
                // not so long that the task times out
                lockPeriodInMilliseconds = 1000;
                Assert.IsTrue(lockPeriodInMilliseconds < TaskUtilities.MaxConfigRetryPeriodInMilliseconds, "Test setup error: the test is sleeping for too long");
            }

            Stopwatch testDuration = Stopwatch.StartNew();

            using (FileStream lockingStream = File.OpenWrite(configFile))
            {
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    System.Threading.Thread.Sleep(lockPeriodInMilliseconds);
                    lockingStream.Close();
                });

                // Perform the operation against the locked file
                op();
            }

            // Sanity check for our test code
            testDuration.Stop();
            int expectedMinimumLockPeriod = System.Math.Min(TaskUtilities.MaxConfigRetryPeriodInMilliseconds, lockPeriodInMilliseconds);
            Assert.IsTrue(testDuration.ElapsedMilliseconds >= expectedMinimumLockPeriod, "Test error: expecting the test to have taken at least {0} milliseconds to run. Actual: {1}",
                expectedMinimumLockPeriod, testDuration.ElapsedMilliseconds);
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Ensures an analysis config file exists in the specified directory,
        /// replacing one if it already exists.
        /// If the supplied "regExExpression" is not null then the appropriate setting
        /// entry will be created in the file
        /// </summary>
        private static string CreateAnalysisConfig(string parentDir)
        {
            string fullPath = Path.Combine(parentDir, FileConstants.ConfigFileName);

            AnalysisConfig config = new AnalysisConfig();
            config.Save(fullPath);
            return fullPath;
        }

        private static void AssertRetryAttempted(TestLogger logger)
        {
            // We'll assume retry has been attempted if there is a message containing
            // both of the timeout values
            logger.AssertSingleDebugMessageExists(TaskUtilities.MaxConfigRetryPeriodInMilliseconds.ToString(), TaskUtilities.DelayBetweenRetriesInMilliseconds.ToString());
        }

        #endregion
    }
}
