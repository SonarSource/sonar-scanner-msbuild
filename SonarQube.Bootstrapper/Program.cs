//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.Integration.Interfaces;
using SonarQube.TeamBuild.PostProcessor;
using SonarQube.TeamBuild.PostProcessor.Interfaces;
using SonarQube.TeamBuild.PreProcessor;
using SonarScanner.Shim;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SonarQube.Bootstrapper
{
    public static class Program
    {
        public const int ErrorCode = 1;
        public const int SuccessCode = 0;

        public static int Main(string[] args)
        {
            var logger = new ConsoleLogger();
            Utilities.LogAssemblyVersion(logger, typeof(Program).Assembly, Resources.AssemblyDescription);
            ITeamBuildSettings teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            IProcessFactory processorFactory = new ProcessorFactory(logger);
            return Execute(args, processorFactory, teamBuildSettings, logger);
        }

        public static int Execute(string[] args, IProcessFactory processorFactory, ITeamBuildSettings teamBuildSettings, ILogger logger)
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
                    exitCode = PreProcess(settings, processorFactory, logger);
                }
                else
                {
                    exitCode = PostProcess(settings, processorFactory, teamBuildSettings, logger);
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

        private static int PreProcess(IBootstrapperSettings settings, IProcessFactory processorFactory, ILogger logger)
        {
            logger.LogInfo(Resources.MSG_PreparingDirectories);
            if (!Utilities.TryEnsureEmptyDirectories(logger, settings.TempDirectory))
            {
                return ErrorCode;
            }

            string server = settings.SonarQubeUrl;
            Debug.Assert(!string.IsNullOrWhiteSpace(server), "Not expecting the server URL to be null/empty");
            logger.LogDebug(Resources.MSG_ServerUrl, server);

            Utilities.LogAssemblyVersion(logger, typeof(Program).Assembly, Resources.AssemblyDescription);
            logger.IncludeTimestamp = true;

            ITeamBuildPreProcessor preProcessor = processorFactory.createPreProcessor();
            Directory.SetCurrentDirectory(settings.TempDirectory);
            bool success = preProcessor.Execute(settings.ChildCmdLineArgs.ToArray());

            return success ? SuccessCode : ErrorCode;
        }

        private static int PostProcess(IBootstrapperSettings settings, IProcessFactory processorFactory, ITeamBuildSettings teamBuildSettings, ILogger logger)
        {
            Utilities.LogAssemblyVersion(logger, typeof(Program).Assembly, Resources.AssemblyDescription);
            logger.IncludeTimestamp = true;

            
            Debug.Assert(settings != null, "Settings should not be null");

            AnalysisConfig config = GetAnalysisConfig(teamBuildSettings, logger);

            bool succeeded;
            if (config == null)
            {
                succeeded = false;
            }
            else
            {
                IMSBuildPostProcessor postProcessor = processorFactory.createPostProcessor();
                succeeded = postProcessor.Execute(settings.ChildCmdLineArgs.ToArray(), config, teamBuildSettings);
            }

            return succeeded ? SuccessCode : ErrorCode;
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

        /// <summary>
        /// Attempts to load the analysis config file. The location of the file is
        /// calculated from TeamBuild-specific environment variables.
        /// Returns null if the required environment variables are not available.
        /// </summary>
        private static AnalysisConfig GetAnalysisConfig(ITeamBuildSettings teamBuildSettings, ILogger logger)
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

        public interface IProcessFactory
        {
            IMSBuildPostProcessor createPostProcessor();
            ITeamBuildPreProcessor createPreProcessor();
        }

        public class ProcessorFactory : IProcessFactory
        {
            private ILogger logger;

            public ProcessorFactory(ILogger logger)
            {
                this.logger = logger;
            }
            public IMSBuildPostProcessor createPostProcessor()
            {
                return new MSBuildPostProcessor(new CoverageReportProcessor(), new SonarScannerWrapper(), new SummaryReportBuilder(), logger);
            }

            public ITeamBuildPreProcessor createPreProcessor()
            {
                IPreprocessorObjectFactory factory = new PreprocessorObjectFactory();
                return new TeamBuildPreProcessor(factory, logger);
            }
        }
    }
}