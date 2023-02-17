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
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Protobuf;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.WebService
{
    public abstract class SonarWebService : ISonarWebService
    {
        private const string OldDefaultProjectTestPattern = @"[^\\]*test[^\\]*$";
        private const string TestProjectPattern = "sonar.cs.msbuild.testProjectPattern";

        protected readonly IDownloader downloader;
        protected readonly Version serverVersion;
        protected readonly ILogger logger;
        private readonly Uri serverUri;

        private bool disposed;

        public Version ServerVersion => serverVersion;

        protected SonarWebService(IDownloader downloader, Uri serverUri, Version serverVersion, ILogger logger)
        {
            this.downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
            this.serverUri = serverUri ?? throw new ArgumentNullException(nameof(serverUri));
            this.serverVersion = serverVersion ?? throw new ArgumentNullException(nameof(serverVersion));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!serverUri.ToString().EndsWith("/"))
            {
                throw new ArgumentException($"{nameof(serverUri)} should always end with '/'", nameof(serverUri));
            }
        }

        public async Task<Tuple<bool, string>> TryGetQualityProfile(string projectKey, string projectBranch, string organization, string language)
        {
            var projectId = GetProjectIdentifier(projectKey, projectBranch);
            var uri = AddOrganization(GetUri("api/qualityprofiles/search?project={0}", projectId), organization);
            logger.LogDebug(Resources.MSG_FetchingQualityProfile, projectId, uri);

            var qualityProfileKey = await ExecuteWithLogs(async () =>
            {
                var result = await downloader.TryDownloadIfExists(uri);
                var contents = result.Item2;
                if (!result.Item1)
                {
                    uri = AddOrganization(GetUri("api/qualityprofiles/search?defaults=true"), organization);
                    logger.LogDebug(Resources.MSG_FetchingQualityProfile, projectId, uri);
                    contents = await ExecuteWithLogs(async () => await downloader.Download(uri) ?? throw new AnalysisException(Resources.ERROR_DownloadingQualityProfileFailed), uri);
                }

                var json = JObject.Parse(contents);
                try
                {
                    return json["profiles"]?.Children<JObject>().SingleOrDefault(x => language.Equals(x["language"]?.ToString()))?["key"]?.ToString();
                }
                catch (InvalidOperationException) // As we don't have fail-fast policy for unsupported version for now, we should handle gracefully multi-QPs set for a project, here for SQ < 6.7
                {
                    throw new AnalysisException(Resources.ERROR_UnsupportedSonarQubeVersion);
                }
            }, uri);

            return new Tuple<bool, string>(qualityProfileKey != null, qualityProfileKey);
        }

        public async Task<IList<SonarRule>> GetRules(string qProfile)
        {
            const int limit = 10000;
            var fetched = 0;
            var total = 1;  // Initial value to enter the loop
            var page = 1;
            var allRules = new List<SonarRule>();
            while (fetched < total && fetched < limit)
            {
                var uri = GetUri("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile={0}&p={1}", qProfile, page.ToString());
                logger.LogDebug(Resources.MSG_FetchingRules, qProfile, uri);

                allRules.AddRange(await ExecuteWithLogs(async () =>
                {
                    var contents = await downloader.Download(uri);
                    var json = JObject.Parse(contents);
                    total = json["total"].ToObject<int>();
                    fetched += json["ps"].ToObject<int>();
                    var rules = json["rules"].Children<JObject>();
                    var actives = json["actives"];

                    return rules.Select(x => CreateRule(x, actives));
                }, uri));

                page++;
            }
            return allRules;
        }

        public async Task<IEnumerable<string>> GetAllLanguages()
        {
            var uri = GetUri("api/languages/list");
            return await ExecuteWithLogs(async () =>
            {
                var contents = await downloader.Download(uri);

                var langArray = JObject.Parse(contents).Value<JArray>("languages");
                return langArray.Select(obj => obj["key"].ToString());
            }, uri);
        }

        public async Task<bool> TryDownloadEmbeddedFile(string pluginKey, string embeddedFileName, string targetDirectory)
        {
            Contract.ThrowIfNullOrWhitespace(pluginKey, nameof(pluginKey));
            Contract.ThrowIfNullOrWhitespace(embeddedFileName, nameof(embeddedFileName));
            Contract.ThrowIfNullOrWhitespace(targetDirectory, nameof(targetDirectory));

            var uri = GetUri("static/{0}/{1}", pluginKey, embeddedFileName);
            return await ExecuteWithLogs(async () =>
            {
                var targetFilePath = Path.Combine(targetDirectory, embeddedFileName);

                logger.LogDebug(Resources.MSG_DownloadingZip, embeddedFileName, uri, targetDirectory);
                return await downloader.TryDownloadFileIfExists(uri, targetFilePath);
            }, uri);
        }

        public Task<IList<SensorCacheEntry>> DownloadCache(string projectKey, string branch)
        {
            _ = projectKey ?? throw new ArgumentNullException(nameof(projectKey));
            _ = branch ?? throw new ArgumentNullException(nameof(branch));

            logger.LogDebug(Resources.MSG_DownloadingCache, projectKey, branch);
            var uri = GetUri("api/analysis_cache/get?project={0}&branch={1}", projectKey, branch);
            return downloader.DownloadStream(uri).ContinueWith(ParseCacheEntries);
        }

        public void Dispose() => Dispose(true);

        public abstract Task<bool> IsServerLicenseValid();

        public abstract void WarnIfSonarQubeVersionIsDeprecated();

        public abstract bool IsSonarCloud();

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                downloader.Dispose();
            }

            disposed = true;
        }

        /// <summary>
        /// Retrieves project properties from the server.
        ///
        /// Will fail with an exception if the downloaded return from the server is not a JSON array.
        /// </summary>
        /// <param name="projectKey">The SonarQube project key to retrieve properties for.</param>
        /// <param name="projectBranch">The SonarQube project branch to retrieve properties for (optional).</param>
        /// <returns>A dictionary of key-value property pairs.</returns>
        ///
        public abstract Task<IDictionary<string, string>> GetProperties(string projectKey, string projectBranch);

        protected async Task<T> ExecuteWithLogs<T>(Func<Task<T>> request, Uri logUri)
        {
            try
            {
                return await request();
            }
            catch (Exception e)
            {
                logger.LogError("Failed to request and parse '{0}': {1}", logUri, e.Message);
                throw;
            }
        }

        protected async Task<IDictionary<string, string>> GetComponentProperties(string projectId)
        {
            var uri = GetUri("api/settings/values?component={0}", projectId);
            logger.LogDebug(Resources.MSG_FetchingProjectProperties, projectId, uri);
            var projectFound = await ExecuteWithLogs(async () => await downloader.TryDownloadIfExists(uri, true), uri);
            var contents = projectFound.Item2;
            if (projectFound is { Item1: false })
            {
                uri = GetUri("api/settings/values");
                logger.LogDebug("No settings for project {0}. Getting global settings: {1}", projectId, uri);
                contents = await ExecuteWithLogs(async () => await downloader.Download(uri), uri);
            }

            return await ExecuteWithLogs(() => Task.FromResult(ParseSettingsResponse(contents)), uri);
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

        /// <summary>
        /// Concatenates project key and branch into one string.
        /// </summary>
        /// <param name="projectKey">Unique project key</param>
        /// <param name="projectBranch">Specified branch of the project. Null if no branch to be specified.</param>
        /// <returns>A correctly formatted branch-specific identifier (if appropriate) for a given project.</returns>
        protected Uri GetUri(string query, params string[] args) =>
            new(serverUri, Escape(query, args));

        protected virtual Uri AddOrganization(Uri uri, string organization) =>
            string.IsNullOrEmpty(organization)
                ? uri
                : new Uri(uri + $"&organization={WebUtility.UrlEncode(organization)}");

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

        private IList<SensorCacheEntry> ParseCacheEntries(Task<Stream> task)
        {
            if (task.IsFaulted || task.Result is not { } dataStream)
            {
                return new List<SensorCacheEntry>();
            }

            var cacheEntries = new List<SensorCacheEntry>();
            try
            {
                while (dataStream.Position < dataStream.Length)
                {
                    cacheEntries.Add(SensorCacheEntry.Parser.ParseDelimitedFrom(dataStream));
                }
            }
            catch (Exception e)
            {
                logger.LogDebug(Resources.MSG_IncrementalPRCacheEntryDeserialization, e.Message);
            }

            return cacheEntries;
        }

        protected static string GetProjectIdentifier(string projectKey, string projectBranch = null)
        {
            var projectId = projectKey;
            if (!string.IsNullOrWhiteSpace(projectBranch))
            {
                projectId = projectKey + ":" + projectBranch;
            }

            return projectId;
        }

        private static string ParseRuleKey(string key)
        {
            var pos = key.IndexOf(':');
            return key.Substring(pos + 1);
        }

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

        private static string Escape(string format, params string[] args) =>
            string.Format(format, args.Select(WebUtility.UrlEncode).ToArray());

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
}
