//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Sonar.FxCopRuleset
{
    class Program
    {
        private const string Language = "cs";
        private const string Repository = "fxcop";

        public static int Main(string[] args)
        {
            if (args.Length != 5)
            {
                Console.WriteLine("Expected to be called with exactly 5 arguments:");
                Console.WriteLine("  1) SonarQube Server URL");
                Console.WriteLine("  2) SonarQube Username");
                Console.WriteLine("  3) SonarQube Password");
                Console.WriteLine("  4) SonarQube Project Key");
                Console.WriteLine("  5) Dump path");
                return 1;
            }

            // TODO Should support trailing slash, see http://jira.codehaus.org/browse/SONARUNNER-57
            var server = args[0];
            var username = args[1];
            var password = args[2];
            var projectKey = args[3];
            var dumpPath = args[4];

            using (WebClient client = new WebClient())
            {
                // TODO What if username/password contains ':' or accents?
                var credentials = string.Format("{0}:{1}", username, password);
                credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
                client.Headers[HttpRequestHeader.Authorization] = "Basic " + credentials;

                var projectProfileUrl = string.Format("{0}/api/profiles/list?language={1}&project={2}", server, Language, Uri.EscapeUriString(projectKey));
                string projectProfileJsonContents;
                try
                {
                    projectProfileJsonContents = client.DownloadString(projectProfileUrl);
                }
                catch (WebException)
                {
                    // TODO Better 404 handling
                    // Fall back to default quality profile
                    projectProfileUrl = string.Format("{0}/api/profiles/list?language={1}", server, Language);
                    projectProfileJsonContents = client.DownloadString(projectProfileUrl);
                }
                var projectProfilesJson = JArray.Parse(projectProfileJsonContents);
                var projectProfileJson = projectProfilesJson.Count > 1 ? projectProfilesJson.Where(p => "True".Equals(p["default"].ToString())).Single() : projectProfilesJson[0];
                var projectProfileName = projectProfileJson["name"].ToString();

                // TODO Custom rules
                var profileUrl = string.Format("{0}/api/profiles/index?language={1}&name={2}", server, Language, Uri.EscapeUriString(projectProfileName));
                var profileJsonContents = client.DownloadString(profileUrl);
                var profileJson = JArray.Parse(profileJsonContents);
                var keys = profileJson.Single()["rules"].Where(r => Repository.Equals(r["repo"].ToString())).Select(r => r["key"].ToString());

                var rulesUrl = string.Format("{0}/api/rules/search?activation=true&f=internalKey&ps={1}&repositories={2}", server, int.MaxValue, Repository);
                var rulesJsonContents = client.DownloadString(rulesUrl);
                var rulesJson = JObject.Parse(rulesJsonContents);
                var keysToIds = rulesJson["rules"].ToDictionary(r => r["key"].ToString(), r => r["internalKey"].ToString());

                var ids = keys.Select(k => keysToIds[Repository + ':' + k]);

                File.WriteAllText(dumpPath, RulesetWriter.ToString(ids));
            }

            return 0;
        }
    }
}
