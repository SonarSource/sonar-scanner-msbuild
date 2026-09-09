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

using System.Net;
using Newtonsoft.Json.Linq;
using SonarScanner.MSBuild.PreProcessor.EngineResolution;
using SonarScanner.MSBuild.PreProcessor.JreResolution;
using SonarScanner.MSBuild.PreProcessor.Protobuf;

namespace SonarScanner.MSBuild.PreProcessor.SonarQubeClient;

internal class SonarQubeServer : SonarQubeBase
{
    private readonly IRuntime runtime;
    private readonly Version serverVersion;

    public override string ServerVersion => serverVersion.ToString();

    private SonarQubeServer(IDownloader webDownloader, IDownloader apiDownloader, Version serverVersion, IRuntime runtime, string organization)
        : base(webDownloader, apiDownloader, runtime.Logger, organization)
    {
        this.serverVersion = serverVersion;
        this.runtime = runtime;
    }

    public static async Task<SonarQubeServer> Create(IDownloader webDownloader, IDownloader apiDownloader, IRuntime runtime, string organization)
    {
        if (await LoadServerVersion(apiDownloader, runtime.Logger) is { } serverVersion)
        {
            var ret = new SonarQubeServer(webDownloader, apiDownloader, serverVersion, runtime, organization);
            runtime.LogInfo(Resources.MSG_UsingSonarQube, ret.ServerVersion);
            return await ret.IsAllValid() ? ret : null;     // No dispose for ret or downloaders for simplicity. The program ends soon.
        }
        else
        {
            return null;
        }
    }

    public override async Task<IList<SensorCacheEntry>> DownloadCache(ProcessedArgs localSettings)
    {
        _ = localSettings ?? throw new ArgumentNullException(nameof(localSettings));
        var empty = Array.Empty<SensorCacheEntry>();
        if (string.IsNullOrWhiteSpace(localSettings.ProjectKey))
        {
            runtime.LogInfo(Resources.MSG_Processing_PullRequest_NoProjectKey);
            return empty;
        }
        else if (TryGetBaseBranch(localSettings, out var branch))
        {
            try
            {
                runtime.LogInfo(Resources.MSG_DownloadingCache, localSettings.ProjectKey, branch);
                var uri = WebUtils.EscapedUri("api/analysis_cache/get?project={0}&branch={1}", localSettings.ProjectKey, branch);
                using var stream = await webDownloader.DownloadStream(uri);
                return ParseCacheEntries(stream);
            }
            catch (Exception e)
            {
                runtime.LogWarning(Resources.WARN_IncrementalPRCacheEntryRetrieval_Error, e.Message);
                runtime.LogDebug(e.ToString());
                return empty;
            }
        }
        else
        {
            runtime.LogInfo(Resources.MSG_Processing_PullRequest_NoBranch);
            return empty;
        }
    }

    public override async Task<Stream> DownloadJreAsync(JreMetadata metadata)
    {
        var uri = WebUtils.EscapedUri("analysis/jres/{0}", metadata.Id);
        runtime.LogDebug(Resources.MSG_JreDownloadUri, uri);
        return await apiDownloader.DownloadStream(uri, new() { { "Accept", "application/octet-stream" } });
    }

    public override async Task<Stream> DownloadEngineAsync(EngineMetadata metadata)
    {
        const string uri = "analysis/engine";
        runtime.LogDebug(Resources.MSG_EngineDownloadUri, uri);
        return await apiDownloader.DownloadStream(new(uri, UriKind.Relative), new() { { "Accept", "application/octet-stream" } });
    }

    protected override RuleSearchPaging ParseRuleSearchPaging(JObject json) =>
        new(json["paging"]["total"].ToObject<int>(), json["paging"]["pageSize"].ToObject<int>());

    protected override bool IsConfigurationValid() =>
        true;

    protected override bool IsServerVersionSupported()
    {
        Version failHardBelowVersion;
        Version warningBelowVersion;
        runtime.LogDebug(Resources.MSG_CheckingVersionSupported);
        if (serverVersion.Major < 11 || serverVersion.Major >= 2025)    // Commercial editions 8.x, 9.x, 10.x, and 2025.1 onwards
        {
            failHardBelowVersion = new Version(2025, 1);
            warningBelowVersion = new Version(2025, 4);
        }
        else // Community Edition 25.1 onwards
        {
            failHardBelowVersion = new Version(25, 1);
            warningBelowVersion = new Version(26, 1);
        }
        if (serverVersion < failHardBelowVersion)
        {
            runtime.LogError(Resources.ERR_SonarQubeUnsupported, failHardBelowVersion.ToString());
            return false;
        }
        else if (serverVersion < warningBelowVersion)
        {
            runtime.AnalysisWarnings.Log(Resources.WARN_UI_SonarQubeUnsupported);
        }
        return true;
    }

    protected override async Task<bool> IsServerLicenseValid()
    {
        runtime.LogDebug(Resources.MSG_CheckingLicenseValidity);
        var response = await webDownloader.DownloadResource(new("api/editions/is_valid_license", UriKind.Relative));
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            runtime.LogError(Resources.ERR_InvalidCredentials);
            return false;
        }

        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // On other editions than community, if a license was not set, the response is: {"errors":[{"msg":"License not found"}]} and http status code 404 (not found).
            if (json["errors"]?.Any(x => x["msg"]?.Value<string>() == "License not found") == true)
            {
                runtime.LogError(Resources.ERR_UnlicensedServer, webDownloader.BaseUrl);
                return false;
            }

            // On community edition, the API is not present and any call to `api/editions/is_valid_license` will return {"errors":[{"msg":"Unknown url : /api/editions/is_valid_license"}]}.
            runtime.LogDebug(Resources.MSG_CE_Detected_LicenseValid);
            return true;
        }
        else
        {
            if (json["isValidLicense"]?.ToObject<bool>() is true)
            {
                return true;
            }

            runtime.LogError(Resources.ERR_UnlicensedServer, webDownloader.BaseUrl);
            return false;
        }
    }

    private static async Task<Version> LoadServerVersion(IDownloader apiDownloader, ILogger logger)
    {
        logger.LogDebug(Resources.MSG_FetchingVersion);
        try
        {
            if (await apiDownloader.Download(new("analysis/version", UriKind.Relative)) is { } content)
            {
                return new Version(content.Split('-')[0]);
            }
            else
            {
                LogMessages(null);
                return null;
            }
        }
        catch (Exception ex)
        {
            LogMessages(ex);
            return null;
        }

        void LogMessages(Exception exception)
        {
            logger.LogError(Resources.ERR_ErrorWhenQueryingServerVersion);
            if (exception is not null)
            {
                logger.LogError(exception.Message);
            }
            logger.LogWarning(Resources.WARN_DefaultHostUrlChanged);                    // Might have talked to SonarQube Cloud, which doesn't have the endpoint
            logger.LogWarning(Resources.ERR_SonarQubeUnsupported, "2025.1 or 25.1");    // Older SQ might not have the endpoint
        }
    }
}
