/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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
using SonarScanner.MSBuild.PreProcessor.EngineResolution;
using SonarScanner.MSBuild.PreProcessor.JreResolution;
using SonarScanner.MSBuild.PreProcessor.Protobuf;

namespace SonarScanner.MSBuild.PreProcessor.WebServer;

internal class SonarCloudWebServer : SonarWebServer
{
    private readonly Dictionary<string, IDictionary<string, string>> propertiesCache = new();

    private readonly HttpClient cacheClient;

    public SonarCloudWebServer(IDownloader webDownloader,
                               IDownloader apiDownloader,
                               Version serverVersion,
                               ILogger logger,
                               string organization,
                               TimeSpan httpTimeout,
                               HttpMessageHandler handler = null)
        : base(webDownloader, apiDownloader, serverVersion, logger, organization)
    {
        Contract.ThrowIfNullOrWhitespace(organization, nameof(organization));

        cacheClient = handler is null ? new HttpClient() : new HttpClient(handler, true);
        cacheClient.Timeout = httpTimeout;
        logger.LogInfo(Resources.MSG_UsingSonarCloud);
    }

    public override bool IsServerVersionSupported()
    {
        logger.LogDebug(Resources.MSG_SonarCloudDetected_SkipVersionCheck);
        return true;
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
        if (!TryGetBaseBranch(localSettings, out var branch))
        {
            logger.LogInfo(Resources.MSG_Processing_PullRequest_NoBranch);
            return empty;
        }
        if (GetToken(localSettings) is not { } token)
        {
            logger.LogInfo(Resources.MSG_Processing_PullRequest_NoToken);
            return empty;
        }
        var serverSettings = await DownloadProperties(localSettings.ProjectKey, branch);
        if (!serverSettings.TryGetValue(SonarProperties.CacheBaseUrl, out var cacheBaseUrl))
        {
            logger.LogInfo(Resources.MSG_Processing_PullRequest_NoCacheBaseUrl);
            return empty;
        }

        try
        {
            logger.LogInfo(Resources.MSG_DownloadingCache, localSettings.ProjectKey, branch);
            var ephemeralUrl = await DownloadEphemeralUrl(localSettings.Organization, localSettings.ProjectKey, branch, token, cacheBaseUrl);
            if (ephemeralUrl is null)
            {
                return empty;
            }
            using var stream = await DownloadCacheStream(ephemeralUrl);
            return ParseCacheEntries(stream);
        }
        catch (Exception e)
        {
            logger.LogWarning(Resources.WARN_IncrementalPRCacheEntryRetrieval_Error, e.Message);
            logger.LogDebug(e.ToString());
            return empty;
        }
    }

    // Do not use the downloaders here, as this is an unauthenticated request
    public override async Task<Stream> DownloadJreAsync(JreMetadata metadata)
    {
        var uri = new Uri(metadata.DownloadUrl);
        logger.LogDebug(Resources.MSG_JreDownloadUri, uri);
        return await cacheClient.GetStreamAsync(uri);
    }

    public override async Task<Stream> DownloadEngineAsync(EngineMetadata metadata)
    {
        logger.LogDebug(Resources.MSG_EngineDownloadUri, metadata.DownloadUrl);
        return await cacheClient.GetStreamAsync(metadata.DownloadUrl);
    }

    protected override async Task<IDictionary<string, string>> DownloadComponentProperties(string component)
    {
        if (!propertiesCache.ContainsKey(component))
        {
            propertiesCache.Add(component, await base.DownloadComponentProperties(component));
        }
        return propertiesCache[component];
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            cacheClient.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task<Uri> DownloadEphemeralUrl(string organization, string projectKey, string branch, string token, string cacheBaseUrl)
    {
        var uri = new Uri(WebUtils.CreateUri(cacheBaseUrl), WebUtils.Escape("sensor-cache/prepare-read?organization={0}&project={1}&branch={2}", organization, projectKey, branch));
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("Authorization", $"Bearer {token}");
        logger.LogDebug(Resources.MSG_Processing_PullRequest_RequestPrepareRead, uri);

        using var response = await cacheClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogDebug(Resources.WARN_IncrementalPRCacheEntryRetrieval_Error, "'prepare_read' did not respond successfully.");
            return null;
        }
        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            logger.LogDebug(Resources.WARN_IncrementalPRCacheEntryRetrieval_Error, "'prepare_read' response was empty.");
            return null;
        }
        var deserialized = JsonConvert.DeserializeAnonymousType(content, new { Enabled = false, Url = string.Empty });
        if (!deserialized.Enabled || string.IsNullOrWhiteSpace(deserialized.Url))
        {
            logger.LogDebug(Resources.WARN_IncrementalPRCacheEntryRetrieval_Error, $"'prepare_read' response: {deserialized}.");
            return null;
        }

        return new Uri(deserialized.Url);
    }

    private async Task<Stream> DownloadCacheStream(Uri uri)
    {
        var compressed = await cacheClient.GetStreamAsync(uri);
        using var decompressor = new GZipStream(compressed, CompressionMode.Decompress);
        var decompressed = new MemoryStream();
        await decompressor.CopyToAsync(decompressed);
        decompressed.Position = 0;
        return decompressed;
    }

    private static string GetToken(ProcessedArgs localSettings)
    {
        if (localSettings.TryGetSetting(SonarProperties.SonarUserName, out var login))
        {
            return login;
        }
        else if (localSettings.TryGetSetting(SonarProperties.SonarToken, out var token))
        {
            return token;
        }
        return null;
    }
}
