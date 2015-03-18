//-----------------------------------------------------------------------
// <copyright file="SonarWebServices.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json.Linq;

namespace Sonar.TeamBuild.PreProcessor
{
    public class SonarWebService : IDisposable
    {
        public string Server { get; private set; }
        private readonly string Language;
        private readonly string Repository;
        private readonly IDownloader Downloader;

        public SonarWebService(IDownloader downloader, string server, string language, string repository)
        {
            Downloader = downloader;
            Server = server.EndsWith("/") ? server.Substring(0, server.Length - 1) : server;
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
            var ws = GetUrl("/api/rules/search?f=internalKey&ps={0}&repositories={1}", int.MaxValue.ToString(), Repository);
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
            // TODO Add hardcoded default values as a workaround to SonarQube Web Services limitations?
            var ws = GetUrl("/api/properties?resource={0}", projectKey);
            var contents = Downloader.Download(ws);

            var properties = JArray.Parse(contents);

            return properties.ToDictionary(p => p["key"].ToString(), p => p["value"].ToString());
        }

        public void Dispose()
        {
            Downloader.Dispose();
        }

        private string GetUrl(string format, params string[] args)
        {
            var queryString = string.Format(format, args.Select(a => Uri.EscapeUriString(a)).ToArray());
            if (!queryString.StartsWith("/"))
            {
                queryString = '/' + queryString;
            }
            return Server + queryString;
        }
    }
}
