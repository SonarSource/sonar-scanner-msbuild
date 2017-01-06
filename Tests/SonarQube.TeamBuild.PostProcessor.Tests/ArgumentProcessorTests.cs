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
using System;
using TestUtilities;

namespace SonarQube.TeamBuild.PostProcessor.Tests
{
    [TestClass]
    public class ArgumentProcessorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PostArgProc_NoArgs()
        {
            // 0. Setup
            TestLogger logger = new TestLogger();
            IAnalysisPropertyProvider provider;

            // 1. Null input
            AssertException.Expects<ArgumentNullException>(() => ArgumentProcessor.TryProcessArgs(null, logger, out provider));

            // 2. Empty array input
            provider = CheckProcessingSucceeds(logger, new string[] { });
            provider.AssertExpectedPropertyCount(0);
        }


        [TestMethod]
        public void PostArgProc_Unrecognised()
        {
            // 0. Setup
            TestLogger logger;

            // 1. Unrecognised args
            logger = CheckProcessingFails("begin"); // bootstrapper verbs aren't meaningful to the post-processor
            logger.AssertSingleErrorExists("begin");

            logger = CheckProcessingFails("end");
            logger.AssertSingleErrorExists("end");

            logger = CheckProcessingFails("AAA", "BBB", "CCC");
            logger.AssertSingleErrorExists("AAA");
            logger.AssertSingleErrorExists("BBB");
            logger.AssertSingleErrorExists("CCC");
        }

        [TestMethod]
        public void PostArgProc_PermittedArguments()
        {
            // 0. Setup
            TestLogger logger = new TestLogger();
            string[] args = new string[]
            {
                "/d:sonar.login=user name",
                "/d:sonar.password=pwd",
                "/d:sonar.jdbc.username=db user name",
                "/d:sonar.jdbc.password=db pwd"
            };

            // 1. All valid args
            IAnalysisPropertyProvider provider = CheckProcessingSucceeds(logger, args);

            provider.AssertExpectedPropertyCount(4);
            provider.AssertExpectedPropertyValue("sonar.login", "user name");
            provider.AssertExpectedPropertyValue("sonar.password", "pwd");
            provider.AssertExpectedPropertyValue("sonar.jdbc.username", "db user name");
            provider.AssertExpectedPropertyValue("sonar.jdbc.password", "db pwd");
        }

        [TestMethod]
        public void PostArgProc_NotPermittedArguments()
        {
            // 0. Setup
            TestLogger logger;

            // 1. Valid /d: arguments, but not the permitted ones
            logger = CheckProcessingFails("/d:sonar.visualstudio.enable=false");
            logger.AssertSingleErrorExists("sonar.visualstudio.enable");

            logger = CheckProcessingFails("/d:aaa=bbb", "/d:xxx=yyy");
            logger.AssertSingleErrorExists("aaa");
            logger.AssertSingleErrorExists("xxx");


            // 2. Incorrect versions of permitted /d: arguments
            logger = CheckProcessingFails("/D:sonar.login=user name"); // wrong case for "/d:"
            logger.AssertSingleErrorExists("sonar.login");

            logger = CheckProcessingFails("/d:SONAR.login=user name"); // wrong case for argument name
            logger.AssertSingleErrorExists("SONAR.login");
        }

        #endregion

        #region Checks

        private static IAnalysisPropertyProvider CheckProcessingSucceeds(TestLogger logger, string[] input)
        {
            IAnalysisPropertyProvider provider;
            bool success = ArgumentProcessor.TryProcessArgs(input, logger, out provider);

            Assert.IsTrue(success, "Expecting processing to have succeeded");
            Assert.IsNotNull(provider, "Returned provider should not be null");
            logger.AssertErrorsLogged(0);

            return provider;
        }

        private static TestLogger CheckProcessingFails(params string[] input)
        {
            TestLogger logger = new TestLogger();

            IAnalysisPropertyProvider provider;
            bool success = ArgumentProcessor.TryProcessArgs(input, logger, out provider);

            Assert.IsFalse(success, "Not expecting processing to have succeeded");
            Assert.IsNull(provider, "Provider should be null if processing fails");
            logger.AssertErrorsLogged(); // expecting errors if processing failed

            return logger;
        }

        #endregion
    }
}
