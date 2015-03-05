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

namespace Sonar.FxCopRuleset
{
    public class SonarWebService : IDisposable
    {
        private readonly string Server;
        private readonly string Username;
        private readonly string Password;
        private readonly string Language;
        private readonly string Repository;
        private readonly WebClient Client;

        public SonarWebService(string server, string username, string password, string language, string repository)
        {
            Server = server;
            Username = username;
            Password = password;
            Language = language;
            Repository = repository;

            Client = new WebClient();
            if (username != null && password != null)
            {
                if (username.Contains(':'))
                {
                    throw new ArgumentException("username cannot contain the ':' character due to basic authentication limitations");
                }
                if (!IsAscii(username) || !IsAscii(password))
                {
                    throw new ArgumentException("username and password should contain only ASCII characters due to basic authentication limitations");
                }

                var credentials = string.Format("{0}:{1}", username, password);
                credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
                Client.Headers[HttpRequestHeader.Authorization] = "Basic " + credentials;
            }
        }

        private static bool IsAscii(string s)
        {
            return !s.Any(c => c > sbyte.MaxValue);
        }

        /// <summary>
        /// Get the name of the quality profile (of the given language) to be used by the given project key
        /// </summary>
        public string GetQualityProfile(string projectKey)
        {
            string contents;
            var ws = GetUrl("/api/profiles/list?language={0}&project={1}", Language, projectKey);
            if (!TryDownloadIfExists(ws, out contents))
            {
                ws = GetUrl("/api/profiles/list?language={0}", Language);
                contents = Download(ws);
            }
            var profiles = JArray.Parse(contents);
            var profile = profiles.Count > 1 ? profiles.Where(p => "True".Equals(p["default"].ToString())).Single() : profiles[0];
            return profile["name"].ToString();
        }

        /// <summary>
        /// Get all the active rules (of the given language and repository) in the given quality profile name
        /// </summary>
        public List<string> GetActiveRuleKeys(string qualityProfile)
        {
            var ws = GetUrl("/api/profiles/index?language={0}&name={1}", Language, qualityProfile);
            var contents = Download(ws);

            var profiles = JArray.Parse(contents);
            var keys = profiles.Single()["rules"].Where(r => Repository.Equals(r["repo"].ToString())).Select(r => r["key"].ToString()).ToList();

            return keys;
        }

        /// <summary>
        /// Get the internal keys corresponding to the given keys
        /// </summary>
        public IDictionary<string, string> GetInternalKeys(List<string> keys)
        {
            var ws = GetUrl("/api/rules/search?f=internalKey&ps={0}&repositories={1}", int.MaxValue.ToString(), Repository);
            var contents = Download(ws);

            var rules = JObject.Parse(contents);
            var keysToIds = rules["rules"].ToDictionary(r => r["key"].ToString(), r => r["internalKey"] != null ? r["internalKey"].ToString() : null);

            return keysToIds;
        }

        public void Dispose()
        {
            Client.Dispose();
        }

        private string GetUrl(string format, params string[] args)
        {
            // TODO Should support trailing slash, see http://jira.codehaus.org/browse/SONARUNNER-57
            return Server + string.Format(format, args.Select(a => Uri.EscapeUriString(a)).ToArray());
        }

        private bool TryDownloadIfExists(string url, out string contents)
        {
            try
            {
                contents = Client.DownloadString(url);
                return true;
            }
            catch (WebException e)
            {
                var response = e.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    contents = null;
                    return false;
                }

                throw;
            }
        }

        private string Download(string url)
        {
            return Client.DownloadString(url);
        }
    }
}
