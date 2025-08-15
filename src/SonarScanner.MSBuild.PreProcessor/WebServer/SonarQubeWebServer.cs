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

using System.Net;
using Newtonsoft.Json.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.EngineResolution;
using SonarScanner.MSBuild.PreProcessor.JreResolution;
using SonarScanner.MSBuild.PreProcessor.Protobuf;

namespace SonarScanner.MSBuild.PreProcessor.WebServer;

internal class SonarQubeWebServer : SonarWebServer
{
    public override bool SupportsJreProvisioning => serverVersion >= new Version(10, 6);

    public SonarQubeWebServer(IDownloader webDownloader, IDownloader apiDownloader, Version serverVersion, ILogger logger, string organization)
        : base(webDownloader, apiDownloader, serverVersion, logger, organization) =>
        logger.LogInfo(Resources.MSG_UsingSonarQube, serverVersion);

    public override bool IsServerVersionSupported()
    {
        logger.LogDebug(Resources.MSG_CheckingVersionSupported);
        if (serverVersion.CompareTo(new Version(8, 9)) < 0)
        {
            logger.LogError(Resources.ERR_SonarQubeUnsupported);
            return false;
        }
        else if (serverVersion.CompareTo(new Version(2025, 1)) < 0)
        {
            logger.LogUIWarning(Resources.WARN_UI_SonarQubeUnsupported);
        }
        return true;
    }

    public override async Task<bool> IsServerLicenseValid()
    {
        logger.LogDebug(Resources.MSG_CheckingLicenseValidity);
        var response = await webDownloader.DownloadResource("api/editions/is_valid_license");
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogError(Resources.ERR_InvalidCredentials);
            return false;
        }

        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // On other editions than community, if a license was not set, the response is: {"errors":[{"msg":"License not found"}]} and http status code 404 (not found).
            if (json["errors"]?.Any(x => x["msg"]?.Value<string>() == "License not found") == true)
            {
                logger.LogError(Resources.ERR_UnlicensedServer, webDownloader.GetBaseUrl());
                return false;
            }

            // On community edition, the API is not present and any call to `api/editions/is_valid_license` will return {"errors":[{"msg":"Unknown url : /api/editions/is_valid_license"}]}.
            logger.LogDebug(Resources.MSG_CE_Detected_LicenseValid);
            return true;
        }
        else
        {
            if (json["isValidLicense"]?.ToObject<bool>() is true)
            {
                return true;
            }

            logger.LogError(Resources.ERR_UnlicensedServer, webDownloader.GetBaseUrl());
            return false;
        }
    }

    public override async Task<IList<SensorCacheEntry>> DownloadCache(ProcessedArgs localSettings)
    {
        var empty = Array.Empty<SensorCacheEntry>();
        _ = localSettings ?? throw new ArgumentNullException(nameof(localSettings));

        // SonarQube cache web API is available starting with v9.9
        if (ServerVersion.CompareTo(new Version(9, 9)) < 0)
        {
            logger.LogInfo(Resources.MSG_IncrementalPRAnalysisUpdateSonarQube);
            return empty;
        }
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

        try
        {
            logger.LogInfo(Resources.MSG_DownloadingCache, localSettings.ProjectKey, branch);
            var uri = WebUtils.Escape("api/analysis_cache/get?project={0}&branch={1}", localSettings.ProjectKey, branch);
            using var stream = await webDownloader.DownloadStream(uri);
            return ParseCacheEntries(stream);
        }
        catch (Exception e)
        {
            logger.LogWarning(Resources.WARN_IncrementalPRCacheEntryRetrieval_Error, e.Message);
            logger.LogDebug(e.ToString());
            return empty;
        }
    }

    public override async Task<Stream> DownloadJreAsync(JreMetadata metadata)
    {
        var uri = WebUtils.Escape("analysis/jres/{0}", metadata.Id);
        logger.LogDebug(Resources.MSG_JreDownloadUri, uri);
        return await apiDownloader.DownloadStream(uri, new() { { "Accept", "application/octet-stream" } });
    }

    public override async Task<Stream> DownloadEngineAsync(EngineMetadata metadata)
    {
        var uri = WebUtils.Escape("analysis/engine");
        logger.LogDebug(Resources.MSG_EngineDownloadUri, uri);
        return await apiDownloader.DownloadStream(uri, new() { { "Accept", "application/octet-stream" } });
    }

    protected override async Task<IDictionary<string, string>> DownloadComponentProperties(string component) =>
        serverVersion.CompareTo(new Version(6, 3)) >= 0
            ? await base.DownloadComponentProperties(component)
            : await DownloadComponentPropertiesLegacy(component);

    protected override string AddOrganization(string uri) =>
        serverVersion.CompareTo(new Version(6, 3)) < 0 ? uri : base.AddOrganization(uri);

    protected override RuleSearchPaging ParseRuleSearchPaging(JObject json) =>
        serverVersion.CompareTo(new Version(9, 8)) < 0
            ? base.ParseRuleSearchPaging(json)
            : new(json["paging"]["total"].ToObject<int>(), json["paging"]["pageSize"].ToObject<int>());

    private async Task<IDictionary<string, string>> DownloadComponentPropertiesLegacy(string projectId)
    {
        var uri = WebUtils.Escape("api/properties?resource={0}", projectId);
        logger.LogDebug(Resources.MSG_FetchingProjectProperties, projectId);
        var contents = await webDownloader.Download(uri, true);
        var properties = JArray.Parse(contents);
        return CheckTestProjectPattern(properties.ToDictionary(p => p["key"].ToString(), p => p["value"].ToString()));
    }
}
