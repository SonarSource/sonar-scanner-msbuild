//-----------------------------------------------------------------------
// <copyright file="MockSonarQubeServer.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockSonarQubeServer : ISonarQubeServer
    {
        private readonly IList<string> calledMethods;
        private readonly ServerDataModel dataModel;

        public MockSonarQubeServer()
        {
            this.calledMethods = new List<string>();
            this.dataModel = new ServerDataModel();
        }

        public ServerDataModel Data { get { return this.dataModel; } }

        #region Assertions

        public void AssertMethodCalled(string methodName, int callCount)
        {
            int actualCalls = this.calledMethods.Count(n => string.Equals(methodName, n));
            Assert.AreEqual(callCount, actualCalls, "Method was not called the expected number of times");
        }

        #endregion

        #region ISonarQubeServer methods

        string ISonarQubeServer.Server
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IEnumerable<string> ISonarQubeServer.GetActiveRuleKeys(string qualityProfile, string language, string repository)
        {
            if (string.IsNullOrWhiteSpace(qualityProfile))
            {
                throw new ArgumentNullException("qualityProfile");
            }
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentNullException("language");
            }
            if (string.IsNullOrWhiteSpace(repository))
            {
                throw new ArgumentNullException("repository");
            }

            this.LogMethodCalled();

            QualityProfile profile = this.dataModel.QualityProfiles.FirstOrDefault(qp => string.Equals(qp.Name, qualityProfile) && string.Equals(qp.Language, language));

            if (profile == null)
            {
                return null;
            }

            return profile.ActiveRules.Where(r => string.Equals(r.Repository.Key, repository)).Select(r => r.Key);
        }

        IEnumerable<string> ISonarQubeServer.GetInstalledPlugins()
        {
            this.LogMethodCalled();
            return this.dataModel.InstalledPlugins;
        }

        IDictionary<string, string> ISonarQubeServer.GetInternalKeys(string repository)
        {
            this.LogMethodCalled();

            Repository repo = this.dataModel.Repositories.FirstOrDefault(r => string.Equals(r.Key, repository));
            if (repo == null)
            {
                return null;
            }
            return repo.Rules.ToDictionary(r => r.Key, r => r.InternalKey);
        }

        IDictionary<string, string> ISonarQubeServer.GetProperties(string projectKey, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(projectKey))
            {
                throw new ArgumentNullException("projectKey");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            this.LogMethodCalled();

            return this.dataModel.ServerProperties;
        }

        bool ISonarQubeServer.TryGetQualityProfile(string projectKey, string language, out string qualityProfile)
        {
            this.LogMethodCalled();

            QualityProfile profile = this.dataModel.QualityProfiles.FirstOrDefault(qp => string.Equals(qp.Language, language) && qp.Projects.Contains(projectKey));

            qualityProfile = profile == null ? null : profile.Name;
            return profile != null;
        }

        #endregion

        #region Private methods

        private void LogMethodCalled([CallerMemberName] string methodName = null)
        {
            this.calledMethods.Add(methodName);
        }

        #endregion

    }
}
