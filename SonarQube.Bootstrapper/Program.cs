//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System.Diagnostics;
using System.IO;

namespace SonarQube.Bootstrapper
{
    public static class Program
    {
        public const int ErrorCode = 1;

        public static int Main(string[] args)
        {
            var logger = new ConsoleLogger();
            Utilities.LogAssemblyVersion(logger, typeof(Program).Assembly, Resources.AssemblyDescription);
            BuildAgentUpdater updater = new BuildAgentUpdater();
            return Execute(args, updater, logger);
        }

        public static int Execute(string[] args, IBuildAgentUpdater updater, ILogger logger)
        {
            int exitCode;

            IBootstrapperSettings settings;
            if (ArgumentProcessor.TryProcessArgs(args, logger, out settings))
            {
                Debug.Assert(settings != null, "Bootstrapper settings should not be null");
                Debug.Assert(settings.Phase != AnalysisPhase.Unspecified, "Expecting the processing phase to be specified");

                logger.Verbosity = settings.LoggingVerbosity;

                AnalysisPhase phase = settings.Phase;
                LogProcessingStarted(phase, logger);

                if (phase == AnalysisPhase.PreProcessing)
                {
                    exitCode = PreProcess(updater, settings, logger);
                }
                else
                {
                    exitCode = PostProcess(settings, logger);
                }

                LogProcessingCompleted(phase, exitCode, logger);
            }
            else
            {
                // The argument processor will have logged errors
                exitCode = ErrorCode;
            }
            return exitCode;
        }

        private static int PreProcess(IBuildAgentUpdater updater, IBootstrapperSettings settings, ILogger logger)
        {
            string downloadBinPath = settings.DownloadDirectory;

            logger.LogInfo(Resources.MSG_PreparingDirectories);
            if (!Utilities.TryEnsureEmptyDirectories(logger,
                settings.TempDirectory,
                downloadBinPath))
            {
                return ErrorCode;
            }

            string server = settings.SonarQubeUrl;
            Debug.Assert(!string.IsNullOrWhiteSpace(server), "Not expecting the server url to be null/empty");
            logger.LogDebug(Resources.MSG_ServerUrl, server);

            logger.LogInfo(Resources.MSG_CheckingForUpdates);
            if (!updater.TryUpdate(server, downloadBinPath, logger))
            {
                logger.LogError(Resources.ERROR_FailedToUpdateRunnerBinaries);
                return ErrorCode;
            }

            if (!updater.CheckBootstrapperApiVersion(settings.SupportedBootstrapperVersionsFilePath, settings.BootstrapperVersion))
            {
                logger.LogError(Resources.ERROR_VersionMismatch);
                return ErrorCode;
            }

            var preprocessorFilePath = settings.PreProcessorFilePath;

            ProcessRunnerArguments runnerArgs = new ProcessRunnerArguments(preprocessorFilePath, false, logger)
            {
                CmdLineArgs = settings.ChildCmdLineArgs,
                WorkingDirectory = settings.TempDirectory,
            };
            ProcessRunner runner = new ProcessRunner();
            runner.Execute(runnerArgs);

            return runner.ExitCode;
        }

        private static int PostProcess(IBootstrapperSettings settings, ILogger logger)
        {

            if (!File.Exists(settings.PostProcessorFilePath))
            {
                logger.LogError(Resources.ERROR_PostProcessExeNotFound, settings.PostProcessorFilePath);
                return ErrorCode;
            }

            ProcessRunnerArguments runnerArgs = new ProcessRunnerArguments(settings.PostProcessorFilePath, false, logger)
            {
                CmdLineArgs = settings.ChildCmdLineArgs,
                WorkingDirectory = settings.TempDirectory
            };

            ProcessRunner runner = new ProcessRunner();
            runner.Execute(runnerArgs);

            return runner.ExitCode;
        }

        private static void LogProcessingStarted(AnalysisPhase phase, ILogger logger)
        {
            string phaseLabel = phase == AnalysisPhase.PreProcessing ? Resources.PhaseLabel_PreProcessing : Resources.PhaseLabel_PostProcessing;
            logger.LogInfo(Resources.MSG_ProcessingStarted, phaseLabel);
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
                logger.LogInfo(Resources.MSG_ProcessingSucceeded, phaseLabel);
            }
        }
    }
}