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

namespace Sonar.TeamBuild.PreProcessor
{
    class Program
    {
        private const int ErrorCode = 1;

        private const string ConfigFileName = "SonarAnalysisConfig.xml";

        static int Main(string[] args)
        {
            ILogger logger = new ConsoleLogger(includeTimestamp: true);

            // Process the input args
            logger.LogMessage(Resources.Diag_ProcessingCommandLine);
            SonarAnalysisConfig config = CreateAnalysisContext(logger, args);
            if (config == null)
            {
                logger.LogError(Resources.Error_CannotPerformProcessing);
                return ErrorCode;
            }

            // Create the directories
            logger.LogMessage(Resources.Diag_CreatingFolders);
            EnsureEmptyDirectory(logger, config.SonarConfigDir);
            EnsureEmptyDirectory(logger, config.SonarOutputDir);

            // Save the config file
            string configFile = Path.Combine(config.SonarConfigDir, ConfigFileName);
            logger.LogMessage(Resources.Diag_SavingConfigFile, configFile);
            config.Save(configFile);

            return 0;
        }

        private static SonarAnalysisConfig CreateAnalysisContext(ILogger logger, string[] args)
        {
            SonarAnalysisConfig context = new SonarAnalysisConfig();

            CheckRequiredEnvironmentVariablesExist(logger,
                TeamBuildEnvironmentVariables.TfsCollectionUri,
                TeamBuildEnvironmentVariables.BuildDirectory,
                TeamBuildEnvironmentVariables.BuildUri);

            bool commandLineOk = ProcessCommandLineArgs(logger, context, args);
            if (!commandLineOk)
            {
                return null;
            }

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

        private static bool ProcessCommandLineArgs(ILogger logger, SonarAnalysisConfig config, string[] args)
        {
            if (args.Length != 4)
            {
                logger.LogError(Resources.Error_InvalidCommandLineArgs);
                return false;
            }

            config.SonarProjectKey = args[0];
            config.SonarProjectName = args[1];
            config.SonarProjectVersion = args[2];
            config.SonarRunnerPropertiesPath = args[3];
            return true;
        }
    
        private static void EnsureEmptyDirectory(ILogger logger, string directory)
        {
            if (Directory.Exists(directory))
            {
                logger.LogMessage(Resources.Diag_DeletingDirectory, directory);
                Directory.Delete(directory, true);
            }
            logger.LogMessage(Resources.Diag_CreatingDirectory, directory);
            Directory.CreateDirectory(directory);
        }
    }
}
