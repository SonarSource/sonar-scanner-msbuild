//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using Sonar.TeamBuild.Integration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SonarTeamBuildPostProcessor
{
    class Program
    {
        static int Main(string[] args)
        {
            ILogger logger = new ConsoleLogger(includeTimestamp: true);

            // TODO: consider using command line arguments if supplied
            AnalysisContext context = CreateAnalysisContext(logger);
            if (context == null)
            {
                logger.LogError("Sonar post-processing cannot be performed - required settings are missing");
                return -1;
            }

            // Handle code coverage reports
            CoverageReportProcessor coverageProcesser = CoverageReportProcessor.CreateHandler(logger);

            if (coverageProcesser != null)
            {
                IEnumerable<string> xmlReports = coverageProcesser.DownloadCoverageReports(context.TfsUri, context.BuildUri, context.SonarOutputDir, logger);

                // TODO: update the project info files with the information
                
                if (xmlReports != null)
                {
                    if (xmlReports.Count() == 1)
                    {
                        logger.LogMessage("Updating project info files with code coverage information...");
                        InsertCoverageAnalysisResults(context.SonarOutputDir, xmlReports.First());
                    }
                    else
                    {
                        logger.LogError("More than one code coverage result file was created. Only one report can be uploaded to Sonar. Please modify the build definition so either Sonar analysis is disabled or the only platform/flavor is built");
                        return 1;
                    }
                }
                
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

        /// <summary>
        /// Insert code coverage results information into each projectinfo file
        /// </summary>
        private static void InsertCoverageAnalysisResults(string sonarOutputDir, string coverageFilePath)
        {
            foreach (string projectFolderPath in Directory.GetDirectories(sonarOutputDir))
            {
                ProjectInfo projectInfo = null;

                string projectInfoPath = Path.Combine(projectFolderPath, FileConstants.ProjectInfoFileName);

                if (File.Exists(projectInfoPath))
                {
                    projectInfo = ProjectInfo.Load(projectInfoPath);
                    projectInfo.AnalysisResults.Add(new AnalysisResult() { Id = AnalysisType.VisualStudioCodeCoverage.ToString(), Location = coverageFilePath });
                    projectInfo.Save(projectInfoPath);
                }
            }
        }
    }
}
