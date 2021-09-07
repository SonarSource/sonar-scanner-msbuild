/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2021 SonarSource SA
 * mailto: info AT sonarsource DOT com
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

using System;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.TFS.Classic.XamlBuild;

namespace SonarScanner.MSBuild.TFS.Classic
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var logger = new ConsoleLogger(includeTimestamp: false);
            return Execute(args, logger);
        }

        public static int Execute(string[] args, ILogger logger)
        {
            try
            {
                /* Expected Arguments :
                 * Method : 0
                 * SonarQubeAnalysisConfig.xml path : 1
                 * sonar-project.properties : 2
                 * ranToCompletion : 3
                 */
                if (args.Length < 1)
                {
                    logger.LogError("No argument found. Exiting...");
                    return 1;
                }

                CommandLineArgs commandLineArgs = new CommandLineArgs(logger);
                if(!commandLineArgs.ParseArguments(args))
                {
                    return 1;
                }

                var teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
                AnalysisConfig config = AnalysisConfig.Load(commandLineArgs.SonarQubeAnalysisConfigPath);
                var legacyTeamBuildFactory = new LegacyTeamBuildFactory(logger, config);

                switch (commandLineArgs.ProcessToExecute)
                {
                    case Method.ConvertCoverage:
                        ExecuteCoverageConverter(logger, config, legacyTeamBuildFactory, teamBuildSettings, commandLineArgs.SonarProjectPropertiesPath);
                        break;
                    case Method.SummaryReportBuilder:
                        ExecuteReportBuilder(logger, config, legacyTeamBuildFactory, teamBuildSettings, commandLineArgs.RanToCompletion, commandLineArgs.SonarProjectPropertiesPath);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError("An exception occured while executing the process : " + ex.Message);
                logger.LogError(ex.StackTrace);
            }

            return 0;
        }

        private static void ExecuteReportBuilder(ILogger logger, AnalysisConfig config, ILegacyTeamBuildFactory teamBuildFactory, ITeamBuildSettings teamBuildSettings, bool ranToCompletion, string fullPropertiesFilePath)
        {
            var reportBuilder = new SummaryReportBuilder(teamBuildFactory, logger);
            reportBuilder.GenerateReports(teamBuildSettings, config, ranToCompletion, fullPropertiesFilePath);
        }

        private static void ExecuteCoverageConverter(ILogger logger, AnalysisConfig config, ILegacyTeamBuildFactory teamBuildFactory, ITeamBuildSettings teamBuildSettings, string fullPropertiesFilePath)
        {
            var binaryConverter = new BinaryToXmlCoverageReportConverter(logger, config);
            var coverageReportProcessor = new CoverageReportProcessor(teamBuildFactory, binaryConverter, logger);

            if (coverageReportProcessor.Initialise(config, teamBuildSettings, fullPropertiesFilePath))
            {
                bool success = coverageReportProcessor.ProcessCoverageReports();
                if (success)
                {
                    logger.LogInfo("Coverage report conversion completed successfully.");
                }
                else
                {
                    logger.LogWarning("Coverage report conversion has failed. Skipping...");
                }
            }
        }
    }
}
