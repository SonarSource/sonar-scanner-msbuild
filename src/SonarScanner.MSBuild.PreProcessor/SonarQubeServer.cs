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
using System.Net;
using System.Net.Http;
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
        private readonly IFileWrapper fileWrapper;
        private readonly ILogger logger;
        private readonly Task connect;

        public SonarQubeServer(ISonarQubeService sonarQubeService, ConnectionInformation connectionInformation, IFileWrapper fileWrapper, ILogger logger)
        {
            this.sonarQubeService = sonarQubeService ?? throw new ArgumentNullException(nameof(sonarQubeService));
            this.connectionInformation = connectionInformation ?? throw new ArgumentNullException(nameof(connectionInformation));
            this.fileWrapper = fileWrapper ?? throw new ArgumentNullException(nameof(fileWrapper));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // This task will be awaited before each service call to ensure the service is connected.
            this.connect = sonarQubeService.ConnectAsync(connectionInformation, CancellationToken.None);
        }

        public Version GetServerVersion() =>
            SynchronousServiceCall(
                () => Task.FromResult(this.sonarQubeService.SonarQubeVersion));

        public IList<SonarQubeRule> GetActiveRules(string qprofile) =>
            SynchronousServiceCall(
                () => this.sonarQubeService.GetRulesAsync(true, qprofile, CancellationToken.None));

        public IList<SonarQubeRule> GetInactiveRules(string qprofile, string language) =>
            SynchronousServiceCall(
                () => this.sonarQubeService.GetRulesAsync(false, qprofile, CancellationToken.None));

        public IEnumerable<string> GetAllLanguages()
        {
            var languages = SynchronousServiceCall(
                () => this.sonarQubeService.GetAllLanguagesAsync(CancellationToken.None));

            // This should never happen, the SonarQubeService should never return null
            if (languages == null)
            {
                return null;
            }

            return languages.Select(l => l.Key);
        }

        public IDictionary<string, string> GetProperties(string projectKey, string projectBranch)
        {
            var compositeProjectKey = GetCompositeProjectKey(projectKey, projectBranch);

            var properties = SynchronousServiceCall(
                () => this.sonarQubeService.GetAllPropertiesAsync(compositeProjectKey, CancellationToken.None),
                LogForbidden);

            // This should never happen, the SonarQubeService should never return null
            if (properties == null)
            {
                return null;
            }

            var propertiesDictionary = properties.ToDictionary(p => p.Key, p => p.Value);

            ValidateTestProjectPattern(propertiesDictionary);

            return propertiesDictionary;
        }

        public bool TryDownloadEmbeddedFile(string pluginKey, string embeddedFileName, string targetDirectory)
        {
            var stream = SynchronousServiceCall(
                () => this.sonarQubeService.DownloadStaticFileAsync(pluginKey, embeddedFileName, CancellationToken.None),
                IgnoreNotFound);

            // When an exception is ignored, 404-NotFound in this case, SynchronousServiceCall returns default(T)
            if (stream == null)
            {
                return false;
            }

            using (var fileStream = fileWrapper.Create(Path.Combine(targetDirectory, embeddedFileName)))
            {
                stream.CopyTo(fileStream);
            }

            return true;
        }

        public bool TryGetQualityProfile(string projectKey, string projectBranch, string organization,
            string language, out string qualityProfileKey)
        {
            var compositeProjectKey = GetCompositeProjectKey(projectKey, projectBranch);
            var sonarLanguage = new SonarQubeLanguage(language, language);

            var qualityProfile = SynchronousServiceCall(
                () => this.sonarQubeService.GetQualityProfileAsync(compositeProjectKey, organization,
                        sonarLanguage, CancellationToken.None),
                IgnoreNotFound);

            // When an exception is ignored, 404-NotFound in this case, SynchronousServiceCall returns default(T)
            qualityProfileKey = qualityProfile?.Key;

            return qualityProfileKey != null;
        }

        private bool LogForbidden(HttpRequestException exception)
        {
            if (StatusCodeIs(exception, HttpStatusCode.Forbidden))
            {
                this.logger.LogWarning("To analyze private projects make sure the scanner user has 'Browse' permission.");
            }
            return false; // Do not ignore exception
        }

        private static bool StatusCodeIs(HttpRequestException exception, HttpStatusCode statusCode) =>
            exception.InnerException is WebException we &&
            we.Response is HttpWebResponse response &&
            response.StatusCode == statusCode;

        private static string GetCompositeProjectKey(string projectKey, string projectBranch) =>
            string.IsNullOrEmpty(projectBranch)
                ? projectKey
                : $"{projectKey}:{projectBranch}";

        private bool IgnoreNotFound(HttpRequestException exception) =>
            StatusCodeIs(exception, HttpStatusCode.NotFound);

        /// <summary>
        /// Executes an async ISonarQubeService method synchronously and handles exceptions. In case
        /// the service is not connected the method first connects.
        /// </summary>
        /// <remarks>
        ///
        /// </remarks>
        private T SynchronousServiceCall<T>(Func<Task<T>> func, Func<HttpRequestException, bool> ignoreError = null)
        {
            try
            {
                return AsyncHelper.RunSync(async () =>
                {
                    // In case we are not connected yet, wait for the connection task to finish.
                    await this.connect;

                    return await func();
                });
            }
            catch (HttpRequestException e) when (ignoreError != null && ignoreError(e))
            {
                return default(T);
            }
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
