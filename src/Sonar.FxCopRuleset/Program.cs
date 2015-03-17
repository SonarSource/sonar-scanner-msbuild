//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using System;

namespace Sonar.FxCopRuleset
{
    static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine(Resources.ERROR_InvalidCommandLineArgs);
                return 1;
            }

            var sonarRunnerProperties = new FilePropertiesProvider(args[0]);
            var server = sonarRunnerProperties.GetProperty(SonarProperties.HostUrl, "http://localhost:9000");
            var username = sonarRunnerProperties.GetProperty(SonarProperties.SonarUserName, null);
            var password = sonarRunnerProperties.GetProperty(SonarProperties.SonarPassword, null);
            var projectKey = args[1];
            var dumpPath = args[2];

            RulesetGenerator generator = new RulesetGenerator();
            generator.Generate(projectKey, dumpPath, server, username, password);

            return 0;
        }
    }
}
