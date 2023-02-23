/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Protobuf;

namespace SonarScanner.MSBuild.PreProcessor.WebService
{
    internal class SonarCloudWebService : SonarWebService
    {
        private readonly Dictionary<string, IDictionary<string, string>> propertiesCache = new();

        private readonly HttpClient cacheClient;

        public SonarCloudWebService(IDownloader downloader, Uri serverUri, Version serverVersion, ILogger logger, HttpMessageHandler handler = null)
            : base(downloader, serverUri, serverVersion, logger)
        {
            cacheClient = handler is null ? new HttpClient() : new HttpClient(handler, true);
        }

        public override async Task<IDictionary<string, string>> GetProperties(string projectKey, string projectBranch)
        {
            Contract.ThrowIfNullOrWhitespace(projectKey, nameof(projectKey));
            var projectId = ComponentIdentifier(projectKey, projectBranch);

            if (!propertiesCache.ContainsKey(projectId))
            {
                propertiesCache.Add(projectId, await DownloadComponentProperties(projectId));
            }
            return propertiesCache[projectId];
        }

        public override Task<bool> IsServerLicenseValid()
        {
            logger.LogDebug(Resources.MSG_SonarCloudDetected_SkipLicenseCheck);
            return Task.FromResult(true);
        }

        public override async Task<IList<SensorCacheEntry>> DownloadCache(ProcessedArgs localSettings)
        {
            _ = localSettings ?? throw new ArgumentNullException(nameof(localSettings));
            var empty = new List<SensorCacheEntry>();

            if (string.IsNullOrWhiteSpace(localSettings.ProjectKey))
            {
                logger.LogInfo(Resources.MSG_Processing_PullRequest_NoProjectKey);
                return empty;
            }
            if (string.IsNullOrWhiteSpace(localSettings.Organization))
            {
                logger.LogInfo(Resources.MSG_Processing_PullRequest_NoOrganization);
                return empty;
            }
            if (!localSettings.TryGetSetting(SonarProperties.PullRequestBase, out var branch))
            {
                logger.LogInfo(Resources.MSG_Processing_PullRequest_NoBranch);
                return empty;
            }
            if (!localSettings.TryGetSetting(SonarProperties.SonarUserName, out var token))
            {
                logger.LogInfo(Resources.MSG_Processing_PullRequest_NoToken);
                return empty;
            }

            var serverSettings = await GetProperties(localSettings.ProjectKey, branch);
            if (!serverSettings.TryGetValue(SonarProperties.CacheBaseUrl, out var cacheBaseUrl))
            {
                logger.LogInfo(Resources.MSG_Processing_PullRequest_NoCacheBaseUrl);
                return empty;
            }

            try
            {
                logger.LogInfo(Resources.MSG_DownloadingCache, localSettings.ProjectKey, branch);
                var ephemeralUrl = await GetEphemeralCacheUrl(localSettings.Organization, localSettings.ProjectKey, branch, token, cacheBaseUrl);
                using var stream = await GetCacheStream(ephemeralUrl);
                return ParseCacheEntries(stream);
            }
            catch (Exception e)
            {
                logger.LogWarning(Resources.MSG_IncrementalPRCacheEntryDeserialization, e.Message);
                return empty;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                cacheClient.Dispose();
            }

            base.Dispose(disposing);
        }

        private async Task<Uri> GetEphemeralCacheUrl(string organization, string projectKey, string branch, string token, string cacheBaseUrl)
        {
            var uri = new Uri(WebUtils.CreateUri(cacheBaseUrl), WebUtils.Escape("v1/sensor_cache/prepare_read?organization={0}&project={1}&branch={2}", organization, projectKey, branch));
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("Authorization", $"Bearer {token}");

            using var response = await cacheClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var deserialized = JsonConvert.DeserializeAnonymousType(content, new { Enabled = false, Url = "placeholder" });
            return new Uri(deserialized.Url);
        }

        private async Task<Stream> GetCacheStream(Uri uri)
        {
            var compressed = await cacheClient.GetStreamAsync(uri);
            using var decompressor = new GZipStream(compressed, CompressionMode.Decompress);
            var decompressed = new MemoryStream();
            decompressor.CopyTo(decompressed);
            decompressed.Position = 0;
            return decompressed;
        }
    }
}
