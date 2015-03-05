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
using Sonar.Common;

namespace Sonar.FxCopRuleset
{
    static class Program
    {
        private const string Language = "cs";
        private const string Repository = "fxcop";

        public static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Expected to be called with exactly 3 arguments:");
                Console.WriteLine("  1) Path to sonar-runner.properties");
                Console.WriteLine("  2) SonarQube Project Key");
                Console.WriteLine("  3) Dump path");
                return 1;
            }

            var sonarRunnerProperties = new FilePropertiesProvider(args[0]);
            var server = sonarRunnerProperties.GetProperty(SonarProperties.HostUrl, "http://localhost:9000");
            var username = sonarRunnerProperties.GetProperty(SonarProperties.SonarUserName, null);
            var password = sonarRunnerProperties.GetProperty(SonarProperties.SonarPassword, null);
            var projectKey = args[1];
            var dumpPath = args[2];

            using (SonarWebService ws = new SonarWebService(server, username, password, Language, Repository))
            {
                var qualityProfile = ws.GetQualityProfile(projectKey);
                var activeRuleKeys = ws.GetActiveRuleKeys(qualityProfile);
                if (activeRuleKeys.Any())
                {
                    var internalKeys = ws.GetInternalKeys(activeRuleKeys);
                    var ids = activeRuleKeys.Select(k => internalKeys[Repository + ':' + k]);
                    File.WriteAllText(dumpPath, RulesetWriter.ToString(ids));
                }
                else
                {
                    File.Delete(dumpPath);
                }
            }

            return 0;
        }
    }
}
