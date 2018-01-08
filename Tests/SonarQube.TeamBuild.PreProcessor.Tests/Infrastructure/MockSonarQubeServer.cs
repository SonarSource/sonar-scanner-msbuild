/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockSonarQubeServer : ISonarQubeServer
    {
        private readonly IList<string> calledMethods;

        public MockSonarQubeServer()
        {
            calledMethods = new List<string>();
            Data = new ServerDataModel();
        }

        public ServerDataModel Data { get; set; }

        #region Assertions

        public void AssertMethodCalled(string methodName, int callCount)
        {
            var actualCalls = calledMethods.Count(n => string.Equals(methodName, n));
            Assert.AreEqual(callCount, actualCalls, "Method was not called the expected number of times");
        }

        #endregion Assertions

        #region ISonarQubeServer methods

        IList<ActiveRule> ISonarQubeServer.GetActiveRules(string qprofile)
        {
            LogMethodCalled();

            Assert.IsFalse(string.IsNullOrEmpty(qprofile), "Quality profile is required");

            var profile = Data.QualityProfiles.FirstOrDefault(qp => string.Equals(qp.Id, qprofile));
            if (profile == null)
            {
                return null;
            }
            return profile.ActiveRules;
        }

        IList<string> ISonarQubeServer.GetInactiveRules(string qprofile, string language)
        {
            LogMethodCalled();
            Assert.IsFalse(string.IsNullOrEmpty(qprofile), "Quality profile is required");
            var profile = Data.QualityProfiles.FirstOrDefault(qp => string.Equals(qp.Id, qprofile));
            if (profile == null)
            {
                return null;
            }

            return profile.InactiveRules;
        }

        IEnumerable<string> ISonarQubeServer.GetAllLanguages()
        {
            LogMethodCalled();
            return Data.Languages;
        }

        IDictionary<string, string> ISonarQubeServer.GetProperties(string projectKey, string projectBranch)
        {
            LogMethodCalled();

            Assert.IsFalse(string.IsNullOrEmpty(projectKey), "Project key is required");

            return Data.ServerProperties;
        }

        bool ISonarQubeServer.TryGetQualityProfile(string projectKey, string projectBranch, string organization, string language, out string qualityProfile)
        {
            LogMethodCalled();

            Assert.IsFalse(string.IsNullOrEmpty(projectKey), "Project key is required");
            Assert.IsFalse(string.IsNullOrEmpty(language), "Language is required");

            var projectId = projectKey;
            if (!string.IsNullOrWhiteSpace(projectBranch))
            {
                projectId = projectKey + ":" + projectBranch;
            }

            var profile = Data.QualityProfiles
                .FirstOrDefault(qp => string.Equals(qp.Language, language) && qp.Projects.Contains(projectId) && string.Equals(qp.Organization, organization));

            qualityProfile = profile?.Id;
            return profile != null;
        }

        bool ISonarQubeServer.TryDownloadEmbeddedFile(string pluginKey, string embeddedFileName, string targetDirectory)
        {
            LogMethodCalled();

            Assert.IsFalse(string.IsNullOrEmpty(pluginKey), "plugin key is required");
            Assert.IsFalse(string.IsNullOrEmpty(embeddedFileName), "embeddedFileName is required");
            Assert.IsFalse(string.IsNullOrEmpty(targetDirectory), "targetDirectory is required");

            var data = Data.FindEmbeddedFile(pluginKey, embeddedFileName);
            if (data == null)
            {
                return false;
            }
            else
            {
                var targetFilePath = Path.Combine(targetDirectory, embeddedFileName);
                File.WriteAllBytes(targetFilePath, data);
                return true;
            }
        }

        #endregion ISonarQubeServer methods

        #region Private methods

        private void LogMethodCalled([CallerMemberName] string methodName = null)
        {
            calledMethods.Add(methodName);
        }

        #endregion Private methods
    }
}
