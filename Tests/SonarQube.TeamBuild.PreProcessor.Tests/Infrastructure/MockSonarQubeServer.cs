//-----------------------------------------------------------------------
// <copyright file="MockSonarQubeServer.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockSonarQubeServer : ISonarQubeServer
    {
        private readonly IList<string> calledMethods;

        public MockSonarQubeServer()
        {
            this.calledMethods = new List<string>();
            this.Data = new ServerDataModel();
        }

        public ServerDataModel Data { get; set; }

        #region Assertions

        public void AssertMethodCalled(string methodName, int callCount)
        {
            int actualCalls = this.calledMethods.Count(n => string.Equals(methodName, n));
            Assert.AreEqual(callCount, actualCalls, "Method was not called the expected number of times");
        }

        #endregion

        #region ISonarQubeServer methods

        IList<ActiveRule> ISonarQubeServer.GetActiveRules(string qprofile)
        {
            this.LogMethodCalled();

            Assert.IsFalse(string.IsNullOrEmpty(qprofile), "Quality profile is required");

            QualityProfile profile = this.Data.QualityProfiles.FirstOrDefault(qp => string.Equals(qp.Id, qprofile));
            if (profile == null)
            {
                return null;
            }
            return profile.ActiveRules;
        }

        IList<string> ISonarQubeServer.GetInactiveRules(string qprofile, string language)
        {
            this.LogMethodCalled();
            Assert.IsFalse(string.IsNullOrEmpty(qprofile), "Quality profile is required");
            QualityProfile profile = this.Data.QualityProfiles.FirstOrDefault(qp => string.Equals(qp.Id, qprofile));
            if (profile == null)
            {
                return null;
            }

            return profile.InactiveRules;
        }

        IEnumerable<string> ISonarQubeServer.GetInstalledPlugins()
        {
            this.LogMethodCalled();
            return this.Data.InstalledPlugins;
        }

        IDictionary<string, string> ISonarQubeServer.GetProperties(string projectKey, string projectBranch)
        {
            this.LogMethodCalled();

            Assert.IsFalse(string.IsNullOrEmpty(projectKey), "Project key is required");

            return this.Data.ServerProperties;
        }

        bool ISonarQubeServer.TryGetQualityProfile(string projectKey, string projectBranch, string language, out string qualityProfile)
        {
            this.LogMethodCalled();

            Assert.IsFalse(string.IsNullOrEmpty(projectKey), "Project key is required");
            Assert.IsFalse(string.IsNullOrEmpty(language), "Language is required");

            string projectId = projectKey;
            if (!String.IsNullOrWhiteSpace(projectBranch))
            {
                projectId = projectKey + ":" + projectBranch;
            }

            QualityProfile profile = this.Data.QualityProfiles.FirstOrDefault(qp => string.Equals(qp.Language, language) && qp.Projects.Contains(projectId));

            qualityProfile = profile == null ? null : profile.Id;
            return profile != null;
        }

        bool ISonarQubeServer.TryDownloadEmbeddedFile(string pluginKey, string embeddedFileName, string targetDirectory)
        {
            this.LogMethodCalled();

            Assert.IsFalse(string.IsNullOrEmpty(pluginKey), "plugin key is required");
            Assert.IsFalse(string.IsNullOrEmpty(embeddedFileName), "embeddedFileName is required");
            Assert.IsFalse(string.IsNullOrEmpty(targetDirectory), "targetDirectory is required");

            byte[] data = this.Data.FindEmbeddedFile(pluginKey, embeddedFileName);
            if (data == null)
            {
                return false;
            }
            else
            {
                string targetFilePath = Path.Combine(targetDirectory, embeddedFileName);
                File.WriteAllBytes(targetFilePath, data);
                return true;
            }
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
