/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
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
using System.Threading.Tasks;
using FluentAssertions;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.Tests
{
    internal class MockSonarQubeServer : ISonarQubeServer
    {
        private readonly IList<string> calledMethods = new List<string>();

        private readonly IList<string> warnings = new List<string>();

        public ServerDataModel Data { get; set; } = new ServerDataModel();

        #region Assertions

        public void AssertMethodCalled(string methodName, int callCount)
        {
            var actualCalls = this.calledMethods.Count(n => string.Equals(methodName, n));
            actualCalls.Should().Be(callCount, "Method was not called the expected number of times");
        }

        public void AssertWarningWritten(string warning)
        {
            this.warnings.Should().Contain(warning);
        }

        public void AssertNoWarningWritten()
        {
            this.warnings.Should().BeEmpty();
        }

        #endregion Assertions

        #region ISonarQubeServer methods

        Task<bool> ISonarQubeServer.IsServerLicenseValid()
        {
            LogMethodCalled();
            return Task.FromResult(true);
        }

        Task ISonarQubeServer.WarnIfSonarQubeVersionIsDeprecated()
        {
            LogMethodCalled();

            if (Data.SonarQubeVersion != null && Data.SonarQubeVersion.CompareTo(new Version(7, 9)) < 0)
            {
                this.warnings.Add("version is below supported");
            }

            return Task.CompletedTask;
        }

        Task<IList<SonarRule>> ISonarQubeServer.GetRules(string qProfile)
        {
            LogMethodCalled();
            qProfile.Should().NotBeNullOrEmpty("Quality profile is required");
            var profile = Data.QualityProfiles.FirstOrDefault(qp => string.Equals(qp.Id, qProfile));
            if (profile == null)
            {
                return Task.FromResult<IList<SonarRule>>(null);
            }

            return Task.FromResult(profile.Rules);
        }

        Task<IEnumerable<string>> ISonarQubeServer.GetAllLanguages()
        {
            LogMethodCalled();
            return Task.FromResult(Data.Languages.AsEnumerable());
        }

        Task<IDictionary<string, string>> ISonarQubeServer.GetProperties(string projectKey, string projectBranch)
        {
            LogMethodCalled();

            projectKey.Should().NotBeNullOrEmpty("Project key is required");

            return Task.FromResult(Data.ServerProperties);
        }

        Task<Tuple<bool, string>> ISonarQubeServer.TryGetQualityProfile(string projectKey, string projectBranch, string organization, string language)
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

            var qualityProfileKey = profile?.Id;
            return Task.FromResult(new Tuple<bool, string>(profile != null, qualityProfileKey));
        }

        Task<bool> ISonarQubeServer.TryDownloadEmbeddedFile(string pluginKey, string embeddedFileName, string targetDirectory)
        {
            LogMethodCalled();

            pluginKey.Should().NotBeNullOrEmpty("plugin key is required");
            embeddedFileName.Should().NotBeNullOrEmpty("embeddedFileName is required");
            targetDirectory.Should().NotBeNullOrEmpty("targetDirectory is required");

            var data = Data.FindEmbeddedFile(pluginKey, embeddedFileName);
            if (data == null)
            {
                return Task.FromResult(false);
            }
            else
            {
                var targetFilePath = Path.Combine(targetDirectory, embeddedFileName);
                File.WriteAllBytes(targetFilePath, data);
                return Task.FromResult(true);
            }
        }

        Task<Version> ISonarQubeServer.GetServerVersion()
        {
            LogMethodCalled();

            return Task.FromResult(Data.SonarQubeVersion);
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
