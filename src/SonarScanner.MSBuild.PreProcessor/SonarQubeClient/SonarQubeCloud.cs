/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SonarScanner.MSBuild.PreProcessor.SonarQubeClient;

internal class SonarQubeCloud : SonarQubeBase
{
    private readonly HttpClient unauthenticatedClient;

    public override string ServerVersion => "Cloud";    // Well-known value recognized by the analyzer

    private SonarQubeCloud(IDownloader webDownloader, IDownloader apiDownloader, IRuntime runtime, string organization, HttpClient unauthenticatedClient)
        : base(webDownloader, apiDownloader, runtime, organization) =>
        this.unauthenticatedClient = unauthenticatedClient;

    public static async Task<SonarQubeCloud> Create(IDownloader webDownloader,
                                                    IDownloader apiDownloader,
                                                    IRuntime runtime,
                                                    string organization,
                                                    TimeSpan httpTimeout,
                                                    HttpMessageHandler handler = null)
    {
        var unauthenticatedClient = handler is null ? new HttpClient { Timeout = httpTimeout } : new HttpClient(handler, true) { Timeout = httpTimeout };
        var ret = new SonarQubeCloud(webDownloader, apiDownloader, runtime, organization, unauthenticatedClient);
        runtime.LogInfo(Resources.MSG_UsingSonarQubeCloud);
        return await ret.IsAllValid() ? ret : null;     // No dispose for ret or downloaders for simplicity. The program ends soon.
    }

    public override async Task<IList<SensorCacheEntry>> DownloadCache(ProcessedArgs localSettings)
    {
        _ = localSettings ?? throw new ArgumentNullException(nameof(localSettings));
        if (string.IsNullOrWhiteSpace(localSettings.ProjectKey))
        {
            runtime.LogInfo(Resources.MSG_Processing_PullRequest_NoProjectKey);
            return [];
        }
        if (!TryGetBaseBranch(localSettings, out var branch))
        {
            runtime.LogInfo(Resources.MSG_Processing_PullRequest_NoBranch);
            return [];
        }
        if (AuthToken(localSettings) is { } token)
        {
            var serverSettings = await DownloadProperties(localSettings.ProjectKey, branch);
            if (!serverSettings.TryGetValue(SonarProperties.CacheBaseUrl, out var cacheBaseUrl))
            {
                runtime.LogInfo(Resources.MSG_Processing_PullRequest_NoCacheBaseUrl);
                return [];
            }

            try
            {
                runtime.LogInfo(Resources.MSG_DownloadingCache, localSettings.ProjectKey, branch);
                var ephemeralUrl = await DownloadEphemeralUrl(localSettings.Organization, localSettings.ProjectKey, branch, token, cacheBaseUrl);
                if (ephemeralUrl is null)
                {
                    return [];
                }
                using var stream = await DownloadCacheStream(ephemeralUrl);
                return ParseCacheEntries(stream);
            }
            catch (Exception e)
            {
                runtime.LogWarning(Resources.WARN_IncrementalPRCacheEntryRetrieval_Error, e.Message);
                runtime.LogDebug(e.ToString());
                return [];
            }
        }
        else
        {
            runtime.LogInfo(Resources.MSG_Processing_PullRequest_NoToken);
            return [];
        }
    }

    // Do not use the downloaders here, as this is an unauthenticated request
    public override async Task<Stream> DownloadJreAsync(JreMetadata metadata)
    {
        _ = metadata.DownloadUrl ?? throw new AnalysisException($"{nameof(JreMetadata)} must contain a valid download URL.");
        runtime.LogDebug(Resources.MSG_JreDownloadUri, metadata.DownloadUrl);
        return await unauthenticatedClient.GetStreamAsync(metadata.DownloadUrl);
    }

    public override async Task<Stream> DownloadEngineAsync(EngineMetadata metadata)
    {
        _ = metadata.DownloadUrl ?? throw new AnalysisException($"{nameof(EngineMetadata)} must contain a valid download URL.");
        runtime.LogDebug(Resources.MSG_EngineDownloadUri, metadata.DownloadUrl);
        return await unauthenticatedClient.GetStreamAsync(metadata.DownloadUrl);
    }

    protected override RuleSearchPaging ParseRuleSearchPaging(JObject json) =>
        new(json["total"].ToObject<int>(), json["ps"].ToObject<int>());

    protected override bool IsConfigurationValid()
    {
        if (string.IsNullOrWhiteSpace(organization))
        {
            runtime.LogError(Resources.ERR_MissingOrganization);
            runtime.LogWarning(Resources.WARN_DefaultHostUrlChanged);
            return false;
        }
        else
        {
            return true;
        }
    }

    protected override bool IsServerVersionSupported()
    {
        runtime.LogDebug(Resources.MSG_CloudDetected_SkipVersionCheck);
        return true;
    }

    protected override Task<bool> IsServerLicenseValid()
    {
        runtime.LogDebug(Resources.MSG_CloudDetected_SkipLicenseCheck);
        return Task.FromResult(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            unauthenticatedClient.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task<Uri> DownloadEphemeralUrl(string organization, string projectKey, string branch, string token, string cacheBaseUrl)
    {
        var uri = new Uri(WebUtils.CreateUri(cacheBaseUrl), WebUtils.EscapedUri("sensor-cache/prepare-read?organization={0}&project={1}&branch={2}", organization, projectKey, branch));
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("Authorization", $"Bearer {token}");
        runtime.LogDebug(Resources.MSG_Processing_PullRequest_RequestPrepareRead, uri);

        using var response = await unauthenticatedClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            runtime.LogDebug(Resources.WARN_IncrementalPRCacheEntryRetrieval_Error, "'prepare_read' did not respond successfully.");
            return null;
        }
        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            runtime.LogDebug(Resources.WARN_IncrementalPRCacheEntryRetrieval_Error, "'prepare_read' response was empty.");
            return null;
        }
        var deserialized = JsonConvert.DeserializeAnonymousType(content, new { Enabled = false, Url = string.Empty });
        if (!deserialized.Enabled || string.IsNullOrWhiteSpace(deserialized.Url))
        {
            runtime.LogDebug(Resources.WARN_IncrementalPRCacheEntryRetrieval_Error, $"'prepare_read' response: {deserialized}.");
            return null;
        }

        return new Uri(deserialized.Url);
    }

    private async Task<Stream> DownloadCacheStream(Uri uri)
    {
        var compressed = await unauthenticatedClient.GetStreamAsync(uri);
        using var decompressor = new GZipStream(compressed, CompressionMode.Decompress);
        var decompressed = new MemoryStream();
        await decompressor.CopyToAsync(decompressed);
        decompressed.Position = 0;
        return decompressed;
    }

    private static string AuthToken(ProcessedArgs localSettings)
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
