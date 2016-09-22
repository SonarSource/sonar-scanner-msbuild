//-----------------------------------------------------------------------
// <copyright file="SonarWebServices.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Newtonsoft.Json.Linq;
using SonarQube.Common;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace SonarQube.TeamBuild.PreProcessor
{
    public sealed class SonarWebService : ISonarQubeServer, IDisposable
    {
        private readonly string server;
        private readonly IDownloader downloader;
        private readonly ILogger logger;

        public SonarWebService(IDownloader downloader, string server, ILogger logger)
        {
            if (downloader == null)
            {
                throw new ArgumentNullException("downloader");
            }
            if (string.IsNullOrWhiteSpace(server))
            {
                throw new ArgumentNullException("server");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            this.downloader = downloader;
            this.server = server.EndsWith("/", StringComparison.OrdinalIgnoreCase) ? server.Substring(0, server.Length - 1) : server;
            this.logger = logger;
        }

        #region ISonarQubeServer interface

        public bool TryGetQualityProfile(string projectKey, string projectBranch, string language, out string qualityProfileKey)
        {
            string projectId = GetProjectIdentifier(projectKey, projectBranch);

            string contents;
            var ws = GetUrl("/api/qualityprofiles/search?projectKey={0}", projectId);
            if (!this.downloader.TryDownloadIfExists(ws, out contents))
            {
                ws = GetUrl("/api/qualityprofiles/search?defaults=true");
                contents = this.downloader.Download(ws);
            }
            var profiles = JArray.Parse(contents);

            var profile = profiles.SingleOrDefault(p => language.Equals(p["language"].ToString()));
            if(profile == null)
            {
                qualityProfileKey = null;
                return false;
            }

            qualityProfileKey = profile["key"].ToString();
            return true;
        }

        /// <summary>
        /// Retrieves rule keys of rules having a given language and that are not activated in a given profile.
        /// 
        /// </summary>
        /// <param name="qprofile">Quality profile key.</param>
        /// <param name="language">Rule Language.</param>
        /// <returns>Non-activated rule keys, including repo. Example: csharpsquid:S1100</returns>
        public IList<string> GetInactiveRules(string qprofile, string language)
        {
            int fetched = 0;
            int page = 1;
            int total;
            var ruleList = new List<string>();

            do
            {
                var ws = GetUrl("/api/rules/search?f=internalKey&ps=500&activation=false&qprofile={0}&p={1}&languages={2}", qprofile, page.ToString(), language);
                var contents = this.downloader.Download(ws);
                var json = JObject.Parse(contents);
                total = Convert.ToInt32(json["total"]);
                fetched += Convert.ToInt32(json["ps"]);
                page++;
                var rules = json["rules"].Children<JObject>();

                ruleList.AddRange(rules.Select(r => r["key"].ToString()));
            } while (fetched < total);

            return ruleList;
        }

        /// <summary>
        /// Retrieves active rules from the quality profile with the given ID, including their parameters and template keys.
        /// 
        /// </summary>
        /// <param name="qprofile">Quality profile id.</param>
        /// <returns>List of active rules</returns>
        public IList<ActiveRule> GetActiveRules(string qprofile)
        {
            int fetched = 0;
            int page = 1;
            int total;
            var activeRuleList = new List<ActiveRule>();

            do
            {
                var ws = GetUrl("/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&activation=true&qprofile={0}&p={1}", qprofile, page.ToString());
                var contents = this.downloader.Download(ws);
                var json = JObject.Parse(contents);
                total = Convert.ToInt32(json["total"]);
                fetched += Convert.ToInt32(json["ps"]);
                page++;
                var rules = json["rules"].Children<JObject>();
                var actives = json["actives"];

                activeRuleList.AddRange(rules.Select(r =>
                {
                    ActiveRule activeRule = new ActiveRule(r["repo"].ToString(), parseRuleKey(r["key"].ToString()));
                    if (r["internalKey"] != null)
                    {
                        activeRule.InternalKey = r["internalKey"].ToString();
                    }
                    if (r["templateKey"] != null)
                    {
                        activeRule.TemplateKey = r["templateKey"].ToString();
                    }

                    var active = actives[r["key"].ToString()];
                    var listParams = active.Single()["params"].Children<JObject>();
                    activeRule.Parameters = listParams.ToDictionary(pair => pair["key"].ToString(), pair => pair["value"].ToString());
                    if (activeRule.Parameters.ContainsKey("CheckId"))
                    {
                        activeRule.RuleKey = activeRule.Parameters["CheckId"];
                    }
                    return activeRule;
                }));
            } while (fetched < total);

            return activeRuleList;
        }

        /// <summary>
        /// Retrieves project properties from the server.
        /// 
        /// Will fail with an exception if the downloaded return from the server is not a JSON array.
        /// </summary>
        /// <param name="projectKey">The SonarQube project key to retrieve properties for.</param>
        /// <param name="projectBranch">The SonarQube project branch to retrieve properties for (optional).</param>
        /// <returns>A dictionary of key-value property pairs.</returns>
        public IDictionary<string, string> GetProperties(string projectKey, string projectBranch = null)
        {
            if (string.IsNullOrWhiteSpace(projectKey))
            {
                throw new ArgumentNullException("projectKey");
            }

            string projectId = GetProjectIdentifier(projectKey, projectBranch);

            string ws = GetUrl("/api/properties?resource={0}", projectId);
            this.logger.LogDebug(Resources.MSG_FetchingProjectProperties, projectId, ws);
            var contents = this.downloader.Download(ws);

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
            var contents = this.downloader.Download(ws);

            var plugins = JArray.Parse(contents);

            return plugins.Select(plugin => plugin["key"].ToString());
        }

        public bool TryDownloadEmbeddedFile(string pluginKey, string embeddedFileName, string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(pluginKey))
            {
                throw new ArgumentNullException("pluginKey");
            }
            if (string.IsNullOrWhiteSpace(embeddedFileName))
            {
                throw new ArgumentNullException("embeddedFileName");
            }
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new ArgumentNullException("targetDirectory");
            }

            string url = GetUrl("/static/{0}/{1}", pluginKey, embeddedFileName);

            string targetFilePath = Path.Combine(targetDirectory, embeddedFileName);

            logger.LogDebug(Resources.MSG_DownloadingZip, embeddedFileName, url, targetDirectory);
            bool success = this.downloader.TryDownloadFileIfExists(url, targetFilePath);
            return success;
        }

        #endregion

        #region Private methods

        private static string parseRuleKey(string key)
        {
            int pos = key.IndexOf(':');
            return key.Substring(pos + 1);
        }

        /// <summary>
        /// Concatenates project key and branch into one string.
        /// </summary>
        /// <param name="projectKey">Unique project key</param>
        /// <param name="projectBranch">Specified branch of the project. Null if no branch to be specified.</param>
        /// <returns>A correctly formatted branch-specific identifier (if appropriate) for a given project.</returns>
        private static string GetProjectIdentifier(string projectKey, string projectBranch = null)
        {
            string projectId = projectKey;
            if (!string.IsNullOrWhiteSpace(projectBranch))
            {
                projectId = projectKey + ":" + projectBranch;
            }

            return projectId;
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

        #endregion

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
