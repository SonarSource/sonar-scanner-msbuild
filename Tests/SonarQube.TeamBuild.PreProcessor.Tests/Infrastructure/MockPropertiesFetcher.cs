//-----------------------------------------------------------------------
// <copyright file="MockPropertiesFetcher.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockPropertiesFetcher : IPropertiesFetcher
    {
        private bool fetchPropertiesCalled;

        private string actualKey;
        private SonarWebService actualWs;

        #region Assertions

        public void AssertFetchPropertiesCalled()
        {
            Assert.IsTrue(this.fetchPropertiesCalled, "Expecting FetchProperties to have been called");
        }

        public void CheckFetcherArguments(string expectedWsServer, string expectedKey)
        {
            Assert.AreEqual(expectedKey, this.actualKey);
            Assert.AreEqual(expectedWsServer, this.actualWs.Server);
        }

        #endregion

        #region IPropertiesFetcher interface

        IDictionary<string, string> IPropertiesFetcher.FetchProperties(SonarWebService ws, string sonarProjectKey)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(sonarProjectKey), "Supplied project key should not be null");

            this.fetchPropertiesCalled = true;

            this.actualKey = sonarProjectKey;
            this.actualWs = ws;

            return new Dictionary<string, string>();
        }

        #endregion
    }
}
