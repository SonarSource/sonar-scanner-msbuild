/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System.Diagnostics;
using System.IO;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarScanner.Shim;

namespace SonarQube.TeamBuild.PostProcessor
{
    internal static class Program
    {
        private const int ErrorCode = 1;
        private const int SuccessCode = 0;

        private static int Main(string[] args)
        {
            var logger = new ConsoleLogger(includeTimestamp: false);
            Utilities.LogAssemblyVersion(logger, typeof(Program).Assembly, Resources.AssemblyDescription);
            logger.IncludeTimestamp = true;

            var settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            Debug.Assert(settings != null, "Settings should not be null");

            var config = GetAnalysisConfig(settings, logger);

            bool succeeded;
            if (config == null)
            {
                succeeded = false;
            }
            else
            {
                var postProcessor = new MSBuildPostProcessor(
                    new CoverageReportProcessor(),
                    new SonarScannerWrapper(),
                    new SummaryReportBuilder(),
                    logger,
                    new TargetsUninstaller());

                succeeded = postProcessor.Execute(args, config, settings);
            }

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
                var configFilePath = teamBuildSettings.AnalysisConfigFilePath;
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
