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

using SonarQube.TeamBuild.Integration;
using SonarQube.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.Integration.Interfaces;

namespace SonarQube.TeamBuild.PostProcessor.Tests
{
    internal class MockCodeCoverageProcessor : ICoverageReportProcessor
    {
        private bool processCoverageMethodCalled;
        private bool initalisedCalled;

        #region Test helpers

        public bool ProcessValueToReturn { get; set; }
        public bool InitialiseValueToReturn { get; set; }

        #endregion

        #region ICoverageReportProcessor interface

        public bool Initialise(AnalysisConfig context, ITeamBuildSettings settings, ILogger logger)
        {
            Assert.IsFalse(this.initalisedCalled, "Expecting Initialise to be called only once");
            this.initalisedCalled = true;
            return InitialiseValueToReturn;
        }

        public bool ProcessCoverageReports()
        {
            Assert.IsFalse(this.processCoverageMethodCalled, "Expecting ProcessCoverageReports to be called only once");
            Assert.IsTrue(this.initalisedCalled, "Expecting Initialise to be called first");
            this.processCoverageMethodCalled = true;
            return ProcessValueToReturn;
        }

        #endregion

        #region Checks

        public void AssertExecuteCalled()
        {
            Assert.IsTrue(this.processCoverageMethodCalled, "Expecting the sonar-scanner to have been called");
        }

        public void AssertExecuteNotCalled()
        {
            Assert.IsFalse(this.processCoverageMethodCalled, "Not expecting the sonar-scanner to have been called");
        }

        public void AssertInitializedCalled()
        {
            Assert.IsTrue(this.initalisedCalled, "Expecting the sonar-scanner to have been called");
        }

        public void AssertInitialisedNotCalled()
        {
            Assert.IsFalse(this.initalisedCalled, "Not expecting the sonar-scanner to have been called");
        }


        #endregion

    }
}
