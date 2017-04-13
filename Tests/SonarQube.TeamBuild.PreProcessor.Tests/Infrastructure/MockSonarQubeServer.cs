/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
 
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

        IEnumerable<string> ISonarQubeServer.GetAllLanguages()
        {
            this.LogMethodCalled();
            return this.Data.Languages;
        }

        IDictionary<string, string> ISonarQubeServer.GetProperties(string projectKey, string projectBranch)
        {
            this.LogMethodCalled();

            Assert.IsFalse(string.IsNullOrEmpty(projectKey), "Project key is required");

            return this.Data.ServerProperties;
        }

        bool ISonarQubeServer.TryGetQualityProfile(string projectKey, string projectBranch, string organization, string language, out string qualityProfile)
        {
            this.LogMethodCalled();

            Assert.IsFalse(string.IsNullOrEmpty(projectKey), "Project key is required");
            Assert.IsFalse(string.IsNullOrEmpty(language), "Language is required");

            string projectId = projectKey;
            if (!String.IsNullOrWhiteSpace(projectBranch))
            {
                projectId = projectKey + ":" + projectBranch;
            }

            QualityProfile profile = this.Data.QualityProfiles
                .FirstOrDefault(qp => string.Equals(qp.Language, language) && qp.Projects.Contains(projectId) && string.Equals(qp.Organization, organization));

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
