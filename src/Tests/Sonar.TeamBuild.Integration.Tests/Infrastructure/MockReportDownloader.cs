//-----------------------------------------------------------------------
// <copyright file="MockReportDownloader.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sonar.Common;
using System;
using System.IO;

namespace Sonar.TeamBuild.Integration.Tests.Infrastructure
{
    internal class MockReportDownloader : ICoverageReportDownloader
    {
        private int callCount;

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
        
        #endregion

        #region ICoverageReportDownloader interface

        bool ICoverageReportDownloader.DownloadReport(string reportUrl, string newFullFileName, ILogger logger)
        {
            this.callCount++;

            if (this.CreateFileOnDownloadRequest)
            {
                File.WriteAllText(newFullFileName, string.Empty);
            }

            return this.CreateFileOnDownloadRequest;
        }

        #endregion
    }
}
