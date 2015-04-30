//-----------------------------------------------------------------------
// <copyright file="SonarWebServices.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json.Linq;

namespace SonarQube.TeamBuild.PreProcessor
{
    public sealed class SonarWebService : IDisposable
    {
        public string Server { get; private set; }
        private readonly string Language;
        private readonly string Repository;
        private readonly IDownloader Downloader;

        public SonarWebService(IDownloader downloader, string server, string language, string repository)
        {
            if (downloader == null)
            {
                throw new ArgumentNullException("downloader");
            }
            if (string.IsNullOrWhiteSpace(server))
            {
                throw new ArgumentNullException("server");
            }
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentNullException("language");
            }
            if (string.IsNullOrWhiteSpace(repository))
            {
                throw new ArgumentNullException("repository");
            }
            
            Downloader = downloader;
            Server = server.EndsWith("/", StringComparison.OrdinalIgnoreCase) ? server.Substring(0, server.Length - 1) : server;
            Language = language;
            Repository = repository;
        }

        /// <summary>
        /// Get the name of the quality profile (of the given language) to be used by the given project key
        /// </summary>
        public string GetQualityProfile(string projectKey)
        {
            string contents;
            var ws = GetUrl("/api/profiles/list?language={0}&project={1}", Language, projectKey);
            if (!Downloader.TryDownloadIfExists(ws, out contents))
            {
                ws = GetUrl("/api/profiles/list?language={0}", Language);
                contents = Downloader.Download(ws);
            }
            var profiles = JArray.Parse(contents);
            // TODO What is profiles is empty?
            var profile = profiles.Count > 1 ? profiles.Where(p => "True".Equals(p["default"].ToString())).Single() : profiles[0];
            return profile["name"].ToString();
        }

        /// <summary>
        /// Get all the active rules (of the given language and repository) in the given quality profile name
        /// </summary>
        public IEnumerable<string> GetActiveRuleKeys(string qualityProfile)
        {
            var ws = GetUrl("/api/profiles/index?language={0}&name={1}", Language, qualityProfile);
            var contents = Downloader.Download(ws);

            var profiles = JArray.Parse(contents);
            var rules = profiles.Single()["rules"];
            if (rules == null) {
                return Enumerable.Empty<string>();
            }
            
            return rules
                .Where(r => Repository.Equals(r["repo"].ToString()))
                .Select(
                r =>
                {
                    var checkIdParameter = r["params"] == null ? null : r["params"].Where(p => "CheckId".Equals(p["key"].ToString())).SingleOrDefault();
                    return checkIdParameter == null ? r["key"].ToString() : checkIdParameter["value"].ToString();
                });
        }

        /// <summary>
        /// Get the key -> internal keys mapping (of the given language and repository)
        /// </summary>
        public IDictionary<string, string> GetInternalKeys()
        {
            var ws = GetUrl("/api/rules/search?f=internalKey&ps={0}&repositories={1}", int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), Repository);
            var contents = Downloader.Download(ws);

            var rules = JObject.Parse(contents);
            var keysToIds = rules["rules"].ToDictionary(r => r["key"].ToString(), r => r["internalKey"] != null ? r["internalKey"].ToString() : null);

            return keysToIds;
        }

        /// <summary>
        /// Get all the properties of a project
        /// </summary>
        public IDictionary<string, string> GetProperties(string projectKey)
        {
            var ws = GetUrl("/api/properties?resource={0}", projectKey);
            var contents = Downloader.Download(ws);

            var properties = JArray.Parse(contents);
            var result = properties.ToDictionary(p => p["key"].ToString(), p => p["value"].ToString());

            // http://jira.codehaus.org/browse/SONAR-5891
            if (!result.ContainsKey("sonar.cs.msbuild.testProjectPattern"))
            {
                result["sonar.cs.msbuild.testProjectPattern"] = ".*test.*";
            }

            return result;
        }

        private string GetUrl(string format, params string[] args)
        {
            var queryString = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args.Select(a => Uri.EscapeUriString(a)).ToArray());
            if (!queryString.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                queryString = '/' + queryString;
            }
            return Server + queryString;
        }

        #region IDispose implementation

        private bool disposed;

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!this.disposed && disposing)
            {
                if (this.Downloader != null)
                {
                    this.Downloader.Dispose();
                }
            }

            this.disposed = true;
        }

        #endregion
    }
}
