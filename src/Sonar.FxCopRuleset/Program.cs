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
    static class Program
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

            using (SonarWebService ws = new SonarWebService(server, username, password, Language, Repository))
            {
                var qualityProfile = ws.GetQualityProfile(projectKey);
                var activeRuleKeys = ws.GetActiveRuleKeys(qualityProfile);
                var internalKeys = ws.GetInternalKeys(activeRuleKeys);

                var ids = activeRuleKeys.Select(k => internalKeys[Repository + ':' + k]);

                File.WriteAllText(dumpPath, RulesetWriter.ToString(ids));
            }

            return 0;
        }
    }
}
