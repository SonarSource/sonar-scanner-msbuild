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
using System.Collections.Generic;

namespace SonarQube.TeamBuild.Integration.Tests.Infrastructure
{
    internal class MockReportUrlProvider : ICoverageUrlProvider // was internal
    {
        private bool getUrlsCalled;

        #region Test helpers

        public IEnumerable<string> UrlsToReturn { get; set; }

        #endregion

        #region Assertions

        public void AssertGetUrlsCalled()
        {
            Assert.IsTrue(this.getUrlsCalled, "Expecting GetCodeCoverageReportUrls to have been called");
        }

        public void AssertGetUrlsNotCalled()
        {
            Assert.IsFalse(this.getUrlsCalled, "Not expecting GetCodeCoverageReportUrls to have been called");
        }

        #endregion

        #region ICoverageUrlProvider interface

        public IEnumerable<string> GetCodeCoverageReportUrls(string tfsUri, string buildUri, ILogger logger)
        {
            this.getUrlsCalled = true;
            return this.UrlsToReturn;
        }

        #endregion
    }
}
