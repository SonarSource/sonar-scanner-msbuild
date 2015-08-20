//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarRunner.Shim;
using System.Diagnostics;
using System.IO;

namespace SonarQube.TeamBuild.PostProcessor
{
    internal static class Program
    {
        private const int ErrorCode = 1;
        private const int SuccessCode = 0;

        private static int Main()
        {
            ConsoleLogger logger = new ConsoleLogger(includeTimestamp: true);

            TeamBuildSettings settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            Debug.Assert(settings != null, "Settings should not be null");

            AnalysisConfig config = GetAnalysisConfig(settings, logger);

            MSBuildPostProcessor postProcessor = new MSBuildPostProcessor(new CoverageReportProcessor(), new SonarRunnerWrapper(), new SummaryReportBuilder());

            bool succeeded = postProcessor.Execute(config, settings, logger);
            return succeeded ? SuccessCode : ErrorCode;
        }

        /// <summary>
        /// Attempts to load the analysis config file. The location of the file is
        /// calculated from TeamBuild-specific environment variables.
        /// Returns null if the required environment variables are not available.
        /// </summary>
        private static AnalysisConfig GetAnalysisConfig(TeamBuildSettings teamBuildSettings, ILogger logger)
        {
            AnalysisConfig config = null;

            if (teamBuildSettings != null)
            {
                string configFilePath = teamBuildSettings.AnalysisConfigFilePath;
                Debug.Assert(!string.IsNullOrWhiteSpace(configFilePath), "Expecting the analysis config file path to be set");

                if (File.Exists(configFilePath))
                {
                    config = AnalysisConfig.Load(teamBuildSettings.AnalysisConfigFilePath);
                }
                else
                {
                    logger.LogError(Resources.ERROR_ConfigFileNotFound, configFilePath);
                }
            }
            return config;
        }

    }
}