﻿/*
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
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.JreResolution;
using SonarScanner.MSBuild.PreProcessor.Protobuf;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.WebServer;

public abstract class SonarWebServer : ISonarWebServer
{
    private const string OldDefaultProjectTestPattern = @"[^\\]*test[^\\]*$";
    private const string TestProjectPattern = "sonar.cs.msbuild.testProjectPattern";

    protected readonly IDownloader webDownloader;
    protected readonly IDownloader apiDownloader;
    protected readonly Version serverVersion;
    protected readonly ILogger logger;

    private readonly string organization;
    private bool disposed;

    public abstract Task<IList<SensorCacheEntry>> DownloadCache(ProcessedArgs localSettings);

    public abstract Task<Stream> DownloadJreAsync(JreMetadata metadata);
    public abstract bool IsServerVersionSupported();

    public abstract Task<bool> IsServerLicenseValid();

    public Version ServerVersion => serverVersion;
    public virtual bool SupportsJreProvisioning => true;

    protected SonarWebServer(IDownloader webDownloader, IDownloader apiDownloader, Version serverVersion, ILogger logger, string organization)
    {
        this.webDownloader = webDownloader ?? throw new ArgumentNullException(nameof(webDownloader));
        this.apiDownloader = apiDownloader ?? throw new ArgumentNullException(nameof(apiDownloader));
        this.serverVersion = serverVersion ?? throw new ArgumentNullException(nameof(serverVersion));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.organization = organization;
    }

    public async Task<string> DownloadQualityProfile(string projectKey, string projectBranch, string language)
    {
        var component = ComponentIdentifier(projectKey, projectBranch);
        var uri = AddOrganization(WebUtils.Escape("api/qualityprofiles/search?project={0}", component));
        logger.LogDebug(Resources.MSG_FetchingQualityProfile, component);

        var result = await webDownloader.TryDownloadIfExists(uri);
        var contents = result.Item2;
        if (!result.Item1)
        {
            uri = AddOrganization("api/qualityprofiles/search?defaults=true");
            contents = await webDownloader.Download(uri);
            if (contents is null)
            {
                logger.LogError(Resources.ERROR_DownloadingQualityProfileFailed);
                throw new AnalysisException(Resources.ERROR_DownloadingQualityProfileFailed);
            }
        }

        var json = JObject.Parse(contents);
        try
        {
            return json["profiles"]?.Children<JObject>().SingleOrDefault(x => language.Equals(x["language"]?.ToString()))?["key"]?.ToString();
        }
        // ToDo: This behavior is confusing, and not all the parsing errors should lead to this. See: https://github.com/SonarSource/sonar-scanner-msbuild/issues/1468
        catch (InvalidOperationException) // As we don't have fail-fast policy for unsupported version for now, we should handle gracefully multi-QPs set for a project, here for SQ < 6.7
        {
            throw new AnalysisException(Resources.ERROR_UnsupportedSonarQubeVersion);
        }
    }

    public async Task<IList<SonarRule>> DownloadRules(string qProfile)
    {
        const int limit = 10000;
        var fetched = 0;
        var total = 1;  // Initial value to enter the loop
        var page = 1;
        var allRules = new List<SonarRule>();
        while (fetched < total && fetched < limit)
        {
            var uri = WebUtils.Escape("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile={0}&p={1}", qProfile, page.ToString());
            logger.LogDebug(Resources.MSG_FetchingRules, qProfile);

            var contents = await webDownloader.Download(uri);
            var json = JObject.Parse(contents);
            var paging = ParseRuleSearchPaging(json);
            total = paging.Total;
            fetched += paging.Fetched;
            var rules = json["rules"].Children<JObject>();
            var actives = json["actives"];

            allRules.AddRange(rules.Select(x => CreateRule(x, actives)));

            page++;
        }
        return allRules;
    }

    public async Task<IEnumerable<string>> DownloadAllLanguages()
    {
        var contents = await webDownloader.Download("api/languages/list");
        var langArray = JObject.Parse(contents).Value<JArray>("languages");
        return langArray.Select(obj => obj["key"].ToString());
    }

    public async Task<bool> TryDownloadEmbeddedFile(string pluginKey, string embeddedFileName, string targetDirectory)
    {
        Contract.ThrowIfNullOrWhitespace(pluginKey, nameof(pluginKey));
        Contract.ThrowIfNullOrWhitespace(embeddedFileName, nameof(embeddedFileName));
        Contract.ThrowIfNullOrWhitespace(targetDirectory, nameof(targetDirectory));

        var uri = WebUtils.Escape("static/{0}/{1}", pluginKey, embeddedFileName);
        var targetFilePath = Path.Combine(targetDirectory, embeddedFileName);

        logger.LogDebug(Resources.MSG_DownloadingZip, embeddedFileName, targetDirectory);
        return await webDownloader.TryDownloadFileIfExists(uri, targetFilePath);
    }

    public async Task<JreMetadata> DownloadJreMetadataAsync(string operatingSystem, string architecture)
    {
        Contract.ThrowIfNullOrWhitespace(operatingSystem, nameof(operatingSystem));
        Contract.ThrowIfNullOrWhitespace(architecture, nameof(architecture));

        var uri = WebUtils.Escape("analysis/jres?os={0}&arch={1}", operatingSystem, architecture);
        try
        {
            var result = await apiDownloader.Download(uri);
            var jres = JsonConvert.DeserializeObject<List<JreMetadata>>(result);
            // Spec: The API can return multiple results, and the first result should be used.
            return jres[0];
        }
        catch (Exception)
        {
            logger.LogWarning(Resources.WARN_JreMetadataNotRetrieved, uri);
            return null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed && disposing)
        {
            webDownloader.Dispose();
            apiDownloader.Dispose();
            disposed = true;
        }
    }

    /// <summary>
    /// Retrieves project properties from the server.
    ///
    /// Will fail with an exception if the downloaded return from the server is not a JSON array.
    /// </summary>
    /// <param name="projectKey">The project key to retrieve properties for.</param>
    /// <param name="projectBranch">The project branch to retrieve properties for (optional).</param>
    /// <returns>A dictionary of key-value property pairs.</returns>
    ///
    public async Task<IDictionary<string, string>> DownloadProperties(string projectKey, string projectBranch)
    {
        Contract.ThrowIfNullOrWhitespace(projectKey, nameof(projectKey));
        var component = ComponentIdentifier(projectKey, projectBranch);

        return await DownloadComponentProperties(component);
    }

    protected virtual async Task<IDictionary<string, string>> DownloadComponentProperties(string component)
    {
        var uri = WebUtils.Escape("api/settings/values?component={0}", component);
        logger.LogDebug(Resources.MSG_FetchingProjectProperties, component);
        var projectFound = await webDownloader.TryDownloadIfExists(uri, true);
        var contents = projectFound.Item2;
        if (projectFound is { Item1: false })
        {
            logger.LogDebug("No settings for project {0}. Getting global settings...", component);
            contents = await webDownloader.Download("api/settings/values");
        }

        return ParseSettingsResponse(contents);
    }

    protected Dictionary<string, string> CheckTestProjectPattern(Dictionary<string, string> settings)
    {
        // http://jira.sonarsource.com/browse/SONAR-5891 and https://jira.sonarsource.com/browse/SONARMSBRU-285
        if (settings.ContainsKey(TestProjectPattern))
        {
            var value = settings[TestProjectPattern];
            if (value != OldDefaultProjectTestPattern)
            {
                logger.LogWarning(Resources.WARN_TestProjectPattern, TestProjectPattern);
            }
            settings["sonar.msbuild.testProjectPattern"] = value;
            settings.Remove(TestProjectPattern);
        }
        return settings;
    }

    protected virtual string AddOrganization(string uri) =>
        string.IsNullOrEmpty(organization)
            ? uri
            : $"{uri}&organization={WebUtility.UrlEncode(organization)}";

    private Dictionary<string, string> ParseSettingsResponse(string contents)
    {
        var settings = new Dictionary<string, string>();
        var settingsArray = JObject.Parse(contents).Value<JArray>("settings");
        foreach (var t in settingsArray)
        {
            GetPropertyValue(settings, t);
        }

        return CheckTestProjectPattern(settings);
    }

    protected bool TryGetBaseBranch(ProcessedArgs localSettings, out string branch)
    {
        if (localSettings.TryGetSetting(SonarProperties.PullRequestBase, out branch))
        {
            return true;
        }
        if (AutomaticBaseBranchDetection.GetValue() is { } ciProperty)
        {
            branch = ciProperty.Value;
            logger.LogInfo(Resources.MSG_Processing_PullRequest_AutomaticBranchDetection, ciProperty.Value, ciProperty.CiProvider);
            return true;
        }
        return false;
    }

    protected static IList<SensorCacheEntry> ParseCacheEntries(Stream dataStream)
    {
        if (dataStream == null)
        {
            return Array.Empty<SensorCacheEntry>();
        }
        var cacheEntries = new List<SensorCacheEntry>();
        while (dataStream.Position < dataStream.Length)
        {
            cacheEntries.Add(SensorCacheEntry.Parser.ParseDelimitedFrom(dataStream));
        }
        return cacheEntries;
    }

    protected virtual RuleSearchPaging ParseRuleSearchPaging(JObject json) =>
        new(json["total"].ToObject<int>(), json["ps"].ToObject<int>());

    /// <summary>
    /// Concatenates project key and branch into one string.
    /// </summary>
    /// <param name="projectKey">Unique project key</param>
    /// <param name="projectBranch">Specified branch of the project. Null if no branch to be specified.</param>
    /// <returns>A correctly formatted branch-specific identifier (if appropriate) for a given project.</returns>
    private static string ComponentIdentifier(string projectKey, string projectBranch = null) =>
        string.IsNullOrWhiteSpace(projectBranch)
            ? projectKey
            : projectKey + ":" + projectBranch;

    private static string ParseRuleKey(string key) =>
        key.Substring(key.IndexOf(':') + 1);

    private static SonarRule CreateRule(JObject r, JToken actives)
    {
        var active = actives?.Value<JArray>(r["key"].ToString())?.FirstOrDefault();
        var rule = new SonarRule(r["repo"].ToString(), ParseRuleKey(r["key"].ToString()), r["internalKey"]?.ToString(), r["templateKey"]?.ToString(), active != null);
        if (active is { })
        {
            rule.Parameters = active["params"].Children<JObject>().ToDictionary(pair => pair["key"].ToString(), pair => pair["value"].ToString());
            if (rule.Parameters.ContainsKey("CheckId"))
            {
                rule.RuleKey = rule.Parameters["CheckId"];
            }
        }
        return rule;
    }

    private static void MultivalueToProps(Dictionary<string, string> props, string settingKey, JArray array)
    {
        var id = 1;
        foreach (var obj in array.Children<JObject>())
        {
            foreach (var prop in obj.Properties())
            {
                var key = string.Concat(settingKey, ".", id, ".", prop.Name);
                var value = prop.Value.ToString();
                props.Add(key, value);
            }
            id++;
        }
    }

    private static void GetPropertyValue(Dictionary<string, string> settings, JToken p)
    {
        var key = p["key"].ToString();
        if (p["value"] != null)
        {
            var value = p["value"].ToString();
            settings.Add(key, value);
        }
        else if (p["fieldValues"] != null)
        {
            MultivalueToProps(settings, key, (JArray)p["fieldValues"]);
        }
        else if (p["values"] != null)
        {
            var array = (JArray)p["values"];
            var value = string.Join(",", array.Values<string>());
            settings.Add(key, value);
        }
        else
        {
            throw new ArgumentException("Invalid property");
        }
    }
}
