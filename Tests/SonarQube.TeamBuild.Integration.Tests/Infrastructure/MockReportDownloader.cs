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
using System.Linq;

namespace SonarQube.TeamBuild.Integration.Tests.Infrastructure
{
    internal class MockReportDownloader : ICoverageReportDownloader
    {
        private int callCount;
        private readonly IList<string> requestedUrls = new List<string>();
        private readonly IList<string> targetFileNames  = new List<string>();

        #region Test helpers

        public bool CreateFileOnDownloadRequest { get; set; }

        #endregion

        #region Assertions

        public void AssertExpectedDownloads(int expected)
        {
            Assert.AreEqual(expected, callCount, "DownloadReport called an unexpected number of times");
        }

        public void AssertDownloadNotCalled()
        {
            Assert.AreEqual(0, this.callCount, "Not expecting DownloadReport to have been called");
        }
        
        public void AssertExpectedUrlsRequested(params string[] urls)
        {
            CollectionAssert.AreEqual(urls, this.requestedUrls.ToArray(), "Unexpected urls requested");
        }

        public void AssertExpectedTargetFileNamesSupplied(params string[] urls)
        {
            CollectionAssert.AreEqual(urls, this.targetFileNames.ToArray(), "Unexpected target files names supplied");
        }

        #endregion

        #region ICoverageReportDownloader interface

        bool ICoverageReportDownloader.DownloadReport(string tfsUri, string reportUrl, string newFullFileName, ILogger logger)
        {
            this.callCount++;
            this.requestedUrls.Add(reportUrl);
            this.targetFileNames.Add(newFullFileName);

            if (this.CreateFileOnDownloadRequest)
            {
                File.WriteAllText(newFullFileName, string.Empty);
            }

            return this.CreateFileOnDownloadRequest;
        }

        #endregion
    }
}
