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
using System.Collections.Generic;
using System.IO;
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class BootstrapperSettingsTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void BootSettings_InvalidArguments()
        {
            string validUrl = "http://myserver";
            ILogger validLogger = new TestLogger();
            IList<string> validArgs = null;

            AssertException.Expects<ArgumentNullException>(() => new BootstrapperSettings(AnalysisPhase.PostProcessing, validArgs, null, LoggerVerbosity.Debug, validLogger));
            AssertException.Expects<ArgumentNullException>(() => new BootstrapperSettings(AnalysisPhase.PreProcessing, validArgs, validUrl, LoggerVerbosity.Debug, null));
        }

        [TestMethod]
        public void BootSettings_Properties()
        {
            // Check the properties values and that relative paths are turned into absolute paths

            // 0. Setup
            TestLogger logger = new TestLogger();

            using (EnvironmentVariableScope envScope = new EnvironmentVariableScope())
            {
                envScope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, @"c:\temp");

                // 1. Default value -> relative to download dir
                IBootstrapperSettings settings = new BootstrapperSettings(AnalysisPhase.PreProcessing, null, "http://sq", LoggerVerbosity.Debug, logger);
                AssertExpectedServerUrl("http://sq", settings);

            }
        }

        #endregion Tests

        #region Checks

        private static void AssertExpectedServerUrl(string expected, IBootstrapperSettings settings)
        {
            string actual = settings.SonarQubeUrl;
            Assert.AreEqual(expected, actual, true /* ignore case */, "Unexpected server url");
        }

        #endregion Checks
    }
}