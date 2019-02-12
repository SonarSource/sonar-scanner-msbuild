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
using System.Threading;
using System.Threading.Tasks;
using SonarQube.Client;
using SonarQube.Client.Models;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor
{
    public class SonarQubeServer : ISonarQubeServer
    {
        private const string oldDefaultProjectTestPattern = @"[^\\]*test[^\\]*$";

        private readonly ISonarQubeService sonarQubeService;
        private readonly ConnectionInformation connectionInformation;
        private readonly ILogger logger;

        private bool isConnected;

        public SonarQubeServer(ISonarQubeService sonarQubeService, ConnectionInformation connectionInformation, ILogger logger)
        {
            this.sonarQubeService = sonarQubeService;
            this.connectionInformation = connectionInformation;
            this.logger = logger;
        }

        public IList<SonarQubeRule> GetActiveRules(string qprofile) =>
            EnsureConnected(
                () => sonarQubeService.GetRulesAsync(true, qprofile, CancellationToken.None));

        public IEnumerable<string> GetAllLanguages() =>
            EnsureConnected(
                () => sonarQubeService.GetAllLanguagesAsync(CancellationToken.None)).Select(l => l.Key);

        public IList<SonarQubeRule> GetInactiveRules(string qprofile, string language) =>
            EnsureConnected(
                () => sonarQubeService.GetRulesAsync(false, qprofile, CancellationToken.None));

        public IDictionary<string, string> GetProperties(string projectKey, string projectBranch)
        {
            var properties = EnsureConnected(
                () => sonarQubeService.GetAllPropertiesAsync(GetCompositeProjectKey(projectKey, projectBranch), CancellationToken.None))
                .ToDictionary(p => p.Key, p => p.Value);

            ValidateTestProjectPattern(properties);

            return properties;
        }

        private static string GetCompositeProjectKey(string projectKey, string projectBranch) =>
            string.IsNullOrEmpty(projectBranch)
                ? projectKey
                : $"{projectKey}:{projectBranch}";

        public Version GetServerVersion() =>
            sonarQubeService.SonarQubeVersion;

        public bool TryDownloadEmbeddedFile(string pluginKey, string embeddedFileName, string targetDirectory)
        {
            var stream = EnsureConnected(
                () => sonarQubeService.DownloadStaticFileAsync(pluginKey, embeddedFileName, CancellationToken.None));

            using (var file = File.Create(Path.Combine(targetDirectory, embeddedFileName)))
            {
                stream.CopyTo(file);
                return true;
            }
        }

        public bool TryGetQualityProfile(string projectKey, string projectBranch, string organization,
            string language, out string qualityProfileKey)
        {
            var qualityProfile = EnsureConnected(
                () => sonarQubeService.GetQualityProfileAsync(
                    GetCompositeProjectKey(projectKey, projectBranch),
                    organization,
                    new SonarQubeLanguage(language, language),
                    CancellationToken.None));

            qualityProfileKey = qualityProfile.Key;
            return true;
        }

        private T EnsureConnected<T>(Func<Task<T>> func)
        {
            if (!isConnected)
            {
                sonarQubeService
                    .ConnectAsync(connectionInformation, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                isConnected = true;
            }
            return func().GetAwaiter().GetResult();
        }

        private Dictionary<string, string> ValidateTestProjectPattern(Dictionary<string, string> settings)
        {
            // http://jira.sonarsource.com/browse/SONAR-5891 and https://jira.sonarsource.com/browse/SONARMSBRU-285
            if (settings.ContainsKey("sonar.cs.msbuild.testProjectPattern"))
            {
                var value = settings["sonar.cs.msbuild.testProjectPattern"];
                if (value != oldDefaultProjectTestPattern)
                {
                    this.logger.LogWarning("The property 'sonar.cs.msbuild.testProjectPattern' defined in SonarQube is deprecated. Set the property 'sonar.msbuild.testProjectPattern' in the scanner instead.");
                }
                settings["sonar.msbuild.testProjectPattern"] = value;
                settings.Remove("sonar.cs.msbuild.testProjectPattern");
            }
            return settings;
        }
    }
}
