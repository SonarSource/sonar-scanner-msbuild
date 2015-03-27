//-----------------------------------------------------------------------
// <copyright file="MockReportUrlProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.Integration.Tests.Infrastructure
{
    internal class MockReportUrlProvider : ICoverageUrlProvider
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
