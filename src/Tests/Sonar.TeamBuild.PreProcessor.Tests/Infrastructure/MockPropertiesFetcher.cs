//-----------------------------------------------------------------------
// <copyright file="MockPropertiesFetcher.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Collections.Generic;

namespace Sonar.TeamBuild.PreProcessor.Tests
{
    internal class MockPropertiesFetcher : IPropertiesFetcher
    {
        private bool fetchPropertiesCalled;

        private string actualKey, actualUrl, actualUserName, actualPassword;

        #region Assertions

        public void AssertFetchPropertiesCalled()
        {
            Assert.IsTrue(this.fetchPropertiesCalled, "Expecting FetchProperties to have been called");
        }

        public void CheckFetcherArguments(string expectedKey, string expectedUrl, string expectedUserName, string expectedPassword)
        {
            Assert.AreEqual(expectedKey, this.actualKey);
            Assert.AreEqual(expectedUrl, this.actualUrl);
            Assert.AreEqual(expectedUserName, this.actualUserName);
            Assert.AreEqual(expectedPassword, this.actualPassword);
        }

        #endregion

        #region IPropertiesFetcher interface

        IDictionary<string, string> IPropertiesFetcher.FetchProperties(string sonarProjectKey, string sonarUrl, string userName, string password)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(sonarProjectKey), "Supplied project key should not be null");
            Assert.IsFalse(string.IsNullOrWhiteSpace(sonarUrl), "Supplied sonar url should not be null");

            this.fetchPropertiesCalled = true;

            this.actualKey = sonarProjectKey;
            this.actualUrl = sonarUrl;
            this.actualUserName = userName;
            this.actualPassword = password;

            return new Dictionary<string, string>();
        }

        #endregion
    }
}
