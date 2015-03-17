//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using Sonar.TeamBuild.Integration;
using System.IO;

namespace Sonar.TeamBuild.PreProcessor
{
    internal class Program
    {
        private const int ErrorCode = 1;

        static int Main(string[] args)
        {
            ILogger logger = new ConsoleLogger(includeTimestamp: true);

            // Process the input args
            logger.LogMessage(Resources.Diag_ProcessingCommandLine);

            AnalysisConfig config = new AnalysisConfig();
            bool validArgs = ProcessCommandLineArgs(logger, config, args);

            TeamBuildSettings teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(logger);

            // We're checking the args and environment variables so we can report all
            // config errors to the user at once
            if (teamBuildSettings == null || !validArgs)
            {
                logger.LogError(Resources.Error_CannotPerformProcessing);
                return ErrorCode;
            }

            config.SetBuildUri(teamBuildSettings.BuildUri);
            config.SetTfsUri(teamBuildSettings.TfsUri);
            config.SonarConfigDir = teamBuildSettings.SonarConfigDir;
            config.SonarOutputDir = teamBuildSettings.SonarOutputDir;

            // Create the directories
            logger.LogMessage(Resources.Diag_CreatingFolders);
            EnsureEmptyDirectory(logger, config.SonarConfigDir);
            EnsureEmptyDirectory(logger, config.SonarOutputDir);

            // Save the config file
            logger.LogMessage(Resources.Diag_SavingConfigFile, teamBuildSettings.AnalysisConfigFilePath);
            config.Save(teamBuildSettings.AnalysisConfigFilePath);

            return 0;
        }

        private static bool ProcessCommandLineArgs(ILogger logger, AnalysisConfig config, string[] args)
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
