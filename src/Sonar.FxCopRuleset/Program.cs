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
        public static int Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Expected to be called with exactly 4 arguments:");
                Console.WriteLine("  1) SonarQube Server URL");
                Console.WriteLine("  2) SonarQube Username");
                Console.WriteLine("  3) SonarQube Password");
                Console.WriteLine("  4) Dump path");
                return 1;
            }

            var server = args[0];
            var username = args[1];
            var password = args[2];
            var dumpPath = args[3];

            using (WebClient client = new WebClient())
            {
                // TODO What if username/password contains ':' or accents?
                var credentials = string.Format("{0}:{1}", username, password);
                credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
                client.Headers[HttpRequestHeader.Authorization] = "Basic " + credentials;

                // TODO Custom rules
                var url = string.Format("{0}/api/rules/search?activation=true&f=internalKey&ps=500&repositories=fxcop", server);
                var jsonContents = client.DownloadString(url);
                var json = JObject.Parse(jsonContents);

                var ids = json["rules"].Select(r => r["internalKey"].ToString()).ToList();

                File.WriteAllText(dumpPath, RulesetWriter.ToString(ids));
            }

            return 0;
        }
    }
}
