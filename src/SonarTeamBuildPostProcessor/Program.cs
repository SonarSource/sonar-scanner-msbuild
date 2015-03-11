//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using Sonar.TeamBuild.Integration;
using System;
using System.Collections;
using System.IO;

namespace SonarTeamBuildPostProcessor
{
    class Program
    {
        private const int ErrorCode = 1;

        static int Main(string[] args)
        {
            ILogger logger = new ConsoleLogger(includeTimestamp: true);

            // TODO: consider using command line arguments if supplied
            AnalysisContext context = CreateAnalysisContext(logger);
            if (context == null)
            {
                logger.LogError("Sonar post-processing cannot be performed - required settings are missing");
                return ErrorCode;
            }

            // Handle code coverage reports
            CoverageReportProcessor coverageProcessor = new CoverageReportProcessor();
            bool success = coverageProcessor.ProcessCoverageReports(context);

            if (!success)
            {
                return ErrorCode;
            }

            return 0;
        }

        private static AnalysisContext CreateAnalysisContext(ILogger logger)
        {
            AnalysisContext context = new AnalysisContext();

            CheckRequiredEnvironmentVariablesExist(logger,
                TeamBuildEnvironmentVariables.TfsCollectionUri,
                TeamBuildEnvironmentVariables.BuildDirectory,
                TeamBuildEnvironmentVariables.BuildUri);

            // TODO: validate environment variables
            context.BuildUri = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.BuildUri);
            context.TfsUri = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.TfsCollectionUri);
            string rootBuildDir = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.BuildDirectory);

            context.SonarConfigDir = Path.Combine(rootBuildDir, "SonarTemp", "Config");
            context.SonarOutputDir = Path.Combine(rootBuildDir, "SonarTemp", "Output");

            context.Logger = logger;
            return context;
        }

        private static bool CheckRequiredEnvironmentVariablesExist(ILogger logger, params string[] required)
        {
            IDictionary allVars = Environment.GetEnvironmentVariables();

            bool allFound = true;
            foreach (string requiredVar in required)
            {
                string value = allVars[requiredVar] as string;
                if (value == null || string.IsNullOrEmpty(value))
                {
                    logger.LogError("Required environment variable could not be found: {0}", requiredVar);
                    allFound = false;
                }
            }

            return allFound;
        }

    }
}
