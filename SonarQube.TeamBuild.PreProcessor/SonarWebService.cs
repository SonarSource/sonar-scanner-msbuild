//-----------------------------------------------------------------------
// <copyright file="SonarWebServices.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Newtonsoft.Json.Linq;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace SonarQube.TeamBuild.PreProcessor
{
    public sealed class SonarWebService : ISonarQubeServer, IDisposable
    {
        private readonly string server;
        private readonly IDownloader downloader;
        
        public SonarWebService(IDownloader downloader, string server)
        {
            if (downloader == null)
            {
                throw new ArgumentNullException("downloader");
            }
            if (string.IsNullOrWhiteSpace(server))
            {
                throw new ArgumentNullException("server");
            }
            
            this.downloader = downloader;
            this.server = server.EndsWith("/", StringComparison.OrdinalIgnoreCase) ? server.Substring(0, server.Length - 1) : server;
        }

        public bool TryGetQualityProfile(string projectKey, string language, out string qualityProfile)
        {
            string contents;
            var ws = GetUrl("/api/profiles/list?language={0}&project={1}", language, projectKey);
            if (!downloader.TryDownloadIfExists(ws, out contents))
            {
                ws = GetUrl("/api/profiles/list?language={0}", language);
                contents = downloader.Download(ws);
            }
            var profiles = JArray.Parse(contents);

            if (!profiles.Any())
            {
                qualityProfile = null;
                return false;
            }

            var profile = profiles.Count > 1 ? profiles.Where(p => "True".Equals(p["default"].ToString())).Single() : profiles.Single();
            qualityProfile = profile["name"].ToString();
            return true;
        }

        public IEnumerable<string> GetActiveRuleKeys(string qualityProfile, string language, string repository)
        {
            var ws = GetUrl("/api/profiles/index?language={0}&name={1}", language, qualityProfile);
            var contents = downloader.Download(ws);

            var profiles = JArray.Parse(contents);
            var rules = profiles.Single()["rules"];
            if (rules == null) {
                return Enumerable.Empty<string>();
            }
            
            return rules
                .Where(r => repository.Equals(r["repo"].ToString()))
                .Select(
                r =>
                {
                    var checkIdParameter = r["params"] == null ? null : r["params"].Where(p => "CheckId".Equals(p["key"].ToString())).SingleOrDefault();
                    return checkIdParameter == null ? r["key"].ToString() : checkIdParameter["value"].ToString();
                });
        }

        public IDictionary<string, string> GetInternalKeys(string repository)
        {
            var ws = GetUrl("/api/rules/search?f=internalKey&ps={0}&repositories={1}", int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), repository);
            var contents = downloader.Download(ws);

            var rules = JObject.Parse(contents);
            var keysToIds = rules["rules"]
                .Where(r => r["internalKey"] != null)
                .ToDictionary(r => r["key"].ToString(), r => r["internalKey"].ToString());

            return keysToIds;
        }

        public IDictionary<string, string> GetProperties(string projectKey, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(projectKey))
            {
                throw new ArgumentNullException("projectKey");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
           
            string ws = GetUrl("/api/properties?resource={0}", projectKey);
            logger.LogDebug(Resources.MSG_FetchingProjectProperties, projectKey, ws);
            var contents = downloader.Download(ws);

            var properties = JArray.Parse(contents);
            var result = properties.ToDictionary(p => p["key"].ToString(), p => p["value"].ToString());

            // http://jira.sonarsource.com/browse/SONAR-5891 
            if (!result.ContainsKey("sonar.cs.msbuild.testProjectPattern"))
            {
                result["sonar.cs.msbuild.testProjectPattern"] = SonarProperties.DefaultTestProjectPattern;
            }

            return result;
        }

        // TODO Should be replaced by calls to api/languages/list after min(SQ version) >= 5.1
        public IEnumerable<string> GetInstalledPlugins()
        {
            var ws = GetUrl("/api/updatecenter/installed_plugins");
            var contents = downloader.Download(ws);

            var plugins = JArray.Parse(contents);

            return plugins.Select(plugin => plugin["key"].ToString());
        }

        private string GetUrl(string format, params string[] args)
        {
            var queryString = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args.Select(a => WebUtility.UrlEncode(a)).ToArray());
            if (!queryString.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                queryString = '/' + queryString;
            }
            return this.server + queryString;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Utilities.SafeDispose(this.downloader);
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

    }
}
