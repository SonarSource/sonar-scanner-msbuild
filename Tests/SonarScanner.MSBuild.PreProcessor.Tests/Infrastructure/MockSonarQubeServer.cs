/*
 * SonarScanner for MSBuild
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.Tests
{
    internal class MockSonarQubeServer : ISonarQubeServer
    {
        private readonly IList<string> calledMethods;

        public MockSonarQubeServer()
        {
            this.calledMethods = new List<string>();
            Data = new ServerDataModel();
        }

        public ServerDataModel Data { get; set; }

        #region Assertions

        public void AssertMethodCalled(string methodName, int callCount)
        {
            var actualCalls = this.calledMethods.Count(n => string.Equals(methodName, n));
            actualCalls.Should().Be(callCount, "Method was not called the expected number of times");
        }

        #endregion Assertions

        #region ISonarQubeServer methods

        IList<ActiveRule> ISonarQubeServer.GetActiveRules(string qprofile)
        {
            LogMethodCalled();

            qprofile.Should().NotBeNullOrEmpty("Quality profile is required");

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
            qprofile.Should().NotBeNullOrEmpty("Quality profile is required");
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

            projectKey.Should().NotBeNullOrEmpty("Project key is required");

            return Data.ServerProperties;
        }

        bool ISonarQubeServer.TryGetQualityProfile(string projectKey, string projectBranch, string organization, string language, out string qualityProfileKey)
        {
            LogMethodCalled();

            projectKey.Should().NotBeNullOrEmpty("Project key is required");
            language.Should().NotBeNullOrEmpty("Language is required");

            var projectId = projectKey;
            if (!string.IsNullOrWhiteSpace(projectBranch))
            {
                projectId = projectKey + ":" + projectBranch;
            }

            var profile = Data.QualityProfiles
                .FirstOrDefault(qp => string.Equals(qp.Language, language) && qp.Projects.Contains(projectId) && string.Equals(qp.Organization, organization));

            qualityProfileKey = profile?.Id;
            return profile != null;
        }

        bool ISonarQubeServer.TryDownloadEmbeddedFile(string pluginKey, string embeddedFileName, string targetDirectory)
        {
            LogMethodCalled();

            pluginKey.Should().NotBeNullOrEmpty("plugin key is required");
            embeddedFileName.Should().NotBeNullOrEmpty("embeddedFileName is required");
            targetDirectory.Should().NotBeNullOrEmpty("targetDirectory is required");

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

        Version ISonarQubeServer.GetServerVersion()
        {
            LogMethodCalled();

            return Data.SonarQubeVersion;
        }

        #endregion ISonarQubeServer methods

        #region Private methods

        private void LogMethodCalled([CallerMemberName] string methodName = null)
        {
            this.calledMethods.Add(methodName);
        }

        #endregion Private methods
    }
}
