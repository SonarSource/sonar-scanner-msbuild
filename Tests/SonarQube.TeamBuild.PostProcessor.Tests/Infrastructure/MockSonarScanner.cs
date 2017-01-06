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
using SonarScanner.Shim;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.PostProcessor.Tests
{
    internal class MockSonarScanner : ISonarScanner
    {
        private bool methodCalled;

        #region Test Helpers

        public string ErrorToLog { get; set; }

        public ProjectInfoAnalysisResult ValueToReturn { get; set; }

        public IEnumerable<string> SuppliedCommandLineArgs { get; set; }

        #endregion

        #region ISonarScanner interface

        public ProjectInfoAnalysisResult Execute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger)
        {
            Assert.IsFalse(this.methodCalled, "Scanner should only be called once");
            this.methodCalled = true;
            this.SuppliedCommandLineArgs = userCmdLineArguments;
            if (ErrorToLog != null)
            {
                logger.LogError(this.ErrorToLog);
            }

            return this.ValueToReturn;
        }

        #endregion

        #region Checks

        public void AssertExecuted()
        {
            Assert.IsTrue(this.methodCalled, "Expecting the sonar-scanner to have been called");
        }

        public void AssertNotExecuted()
        {
            Assert.IsFalse(this.methodCalled, "Not expecting the sonar-scanner to have been called");
        }

        #endregion
    }
}
