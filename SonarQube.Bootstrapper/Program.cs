//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System.Diagnostics;
using System.Linq;

namespace SonarQube.Bootstrapper
{
    public static class Program
    {
        public const int ErrorCode = 1;

        private static int Main(string[] args)
        {
            var logger = new ConsoleLogger();
            BuildAgentUpdater updater = new BuildAgentUpdater();

            return Execute(args, updater, logger);
        }

        public static int Execute(string[] args, IBuildAgentUpdater updater, ILogger logger)
        {
            int exitCode;

            IBootstrapperSettings settings;
            if (ArgumentProcessor.TryProcessArgs(args, logger, out settings))
            {
                Debug.Assert(settings.Phase != AnalysisPhase.Unspecified, "Expecting the processing phase to be specified");

                string[] strippedArgs = ArgumentProcessor.RemoveBootstrapperArgs(args);
                string cmdLineParams = GetChildProcCmdLineParams(strippedArgs);

                LogProcessingStarted(settings.Phase, strippedArgs.Length, logger);

                if (settings.Phase == AnalysisPhase.PreProcessing)
                {
                    exitCode = PreProcess(cmdLineParams, updater, settings, logger);
                    
                }
                else
                {
                    exitCode = PostProcess(cmdLineParams, settings, logger);
                }

                LogProcessingCompleted(settings.Phase, exitCode, logger);
            }
            else
            {
                // The argument processor will have logged errors
                exitCode = ErrorCode;
            }
            return exitCode;
        }

        private static int PreProcess(string args, IBuildAgentUpdater updater, IBootstrapperSettings settings, ILogger logger)
        {
            string downloadBinPath = settings.DownloadDirectory;

            Utilities.EnsureEmptyDirectory(settings.TempDirectory, logger);
            Utilities.EnsureEmptyDirectory(downloadBinPath, logger);

            string server = settings.SonarQubeUrl;
            Debug.Assert(!string.IsNullOrWhiteSpace(server), "Not expecting the server url to be null/empty");
            logger.LogMessage(Resources.INFO_ServerUrl, server);

            if (!updater.TryUpdate(server, downloadBinPath, logger))
            {
                logger.LogError(Resources.ERROR_FailedToUpdateRunnerBinaries);
                return 1;
            }

            if (!updater.CheckBootstrapperApiVersion(settings.SupportedBootstrapperVersionsFilePath, settings.BootstrapperVersion))
            {
                logger.LogError(Resources.ERROR_VersionMismatch);
                return 1;
            }

            var preprocessorFilePath = settings.PreProcessorFilePath;
            var processRunner = new ProcessRunner();
            processRunner.Execute(preprocessorFilePath, args, settings.TempDirectory, logger);

            return processRunner.ExitCode;
        }

        private static int PostProcess(string args, IBootstrapperSettings settings, ILogger logger)
        {
            var postprocessorFilePath = settings.PostProcessorFilePath;

            var processRunner = new ProcessRunner();
            processRunner.Execute(postprocessorFilePath, args , settings.TempDirectory, logger);
            return processRunner.ExitCode;
        }

        private static string GetChildProcCmdLineParams(params string[] args)
        {
            return string.Join(" ", args.Select(a => "\"" + a + "\""));
        }

        private static void LogProcessingStarted(AnalysisPhase phase, int argumentCount, ILogger logger)
        {
            string phaseLabel = phase == AnalysisPhase.PreProcessing ? Resources.PhaseLabel_PreProcessing : Resources.PhaseLabel_PostProcessing;
            logger.LogMessage(Resources.INFO_ProcessingStarted, phaseLabel, argumentCount);
        }

        private static void LogProcessingCompleted(AnalysisPhase phase, int exitCode, ILogger logger)
        {
            string phaseLabel = phase == AnalysisPhase.PreProcessing ? Resources.PhaseLabel_PreProcessing : Resources.PhaseLabel_PostProcessing;
            if (exitCode == ProcessRunner.ErrorCode)
            {
                logger.LogError(Resources.ERROR_ProcessingFailed, phaseLabel, exitCode);
            }
            else
            {
                logger.LogMessage(Resources.INFO_ProcessingSucceeded, phaseLabel);
            }
        }

    }
}
