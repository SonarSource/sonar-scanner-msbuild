/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using System.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.TFS;
using SonarScanner.MSBuild.TFS.Interfaces;

namespace SonarScanner.MSBuild
{
    public class BootstrapperClass
    {
        public const int ErrorCode = 1;
        public const int SuccessCode = 0;

        private readonly IProcessorFactory processorFactory;
        private readonly IBootstrapperSettings bootstrapSettings;
        private readonly ILogger logger;

        public BootstrapperClass(IProcessorFactory processorFactory, IBootstrapperSettings bootstrapSettings, ILogger logger)
        {
            this.processorFactory = processorFactory;
            this.bootstrapSettings = bootstrapSettings;
            this.logger = logger;

            Debug.Assert(this.bootstrapSettings != null, "Bootstrapper settings should not be null");
            Debug.Assert(this.bootstrapSettings.Phase != AnalysisPhase.Unspecified, "Expecting the processing phase to be specified");
        }

        /// <summary>
        /// Bootstraps a begin or end step, based on the bootstrap settings.
        /// </summary>
        public int Execute()
        {
            int exitCode;

            logger.Verbosity = bootstrapSettings.LoggingVerbosity;
            logger.ResumeOutput();

            var phase = bootstrapSettings.Phase;
            LogProcessingStarted(phase);

            try
            {
                if (phase == AnalysisPhase.PreProcessing)
                {
                    exitCode = PreProcess();
                }
                else
                {
                    exitCode = PostProcess();
                }
            }
            catch (AnalysisException ex)
            {
                logger.LogError(ex.Message);
                exitCode = ErrorCode;
            }

            LogProcessingCompleted(phase, exitCode);
            return exitCode;
        }

        private int PreProcess()
        {
            logger.LogInfo(Resources.MSG_PreparingDirectories);
            if (!Utilities.TryEnsureEmptyDirectories(logger, bootstrapSettings.TempDirectory))
            {
                return ErrorCode;
            }

            CopyDLLs();
            logger.IncludeTimestamp = true;

            var preProcessor = processorFactory.CreatePreProcessor();
            Directory.SetCurrentDirectory(bootstrapSettings.TempDirectory);
            var success = preProcessor.Execute(bootstrapSettings.ChildCmdLineArgs.ToArray());

            return success ? SuccessCode : ErrorCode;
        }

        private int PostProcess()
        {
            logger.IncludeTimestamp = true;

            if (!Directory.Exists(bootstrapSettings.TempDirectory))
            {
                logger.LogError(Resources.ERROR_TempDirDoesNotExist);
                return ErrorCode;
            }

            Directory.SetCurrentDirectory(bootstrapSettings.TempDirectory);
            ITeamBuildSettings teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            var config = GetAnalysisConfig(teamBuildSettings.AnalysisConfigFilePath);

            bool succeeded;
            if (config == null)
            {
                succeeded = false;
            }
            else
            {
                var postProcessor = processorFactory.CreatePostProcessor();
                succeeded = postProcessor.Execute(bootstrapSettings.ChildCmdLineArgs.ToArray(), config, teamBuildSettings);
            }

            return succeeded ? SuccessCode : ErrorCode;
        }

        /// <summary>
        /// Copies DLLs needed by the targets file that is loaded by MSBuild to the project's .sonarqube directory
        /// </summary>
        private void CopyDLLs()
        {
            var binDirPath = Path.Combine(bootstrapSettings.TempDirectory, "bin");
            Directory.CreateDirectory(binDirPath);
            string[] dllsToCopy = { "SonarScanner.MSBuild.Common.dll", "SonarQube.Integration.Tasks.dll" };

            foreach (var dll in dllsToCopy)
            {
                var dllPath = Path.Combine(bootstrapSettings.ScannerBinaryDirPath, dll);
                File.Copy(dllPath, Path.Combine(binDirPath, dll));
            }
        }

        /// <summary>
        /// Attempts to load the analysis config file. The location of the file is
        /// calculated from TeamBuild-specific environment variables.
        /// Returns null if the required environment variables are not available.
        /// </summary>
        private AnalysisConfig GetAnalysisConfig(string configFilePath)
        {
            AnalysisConfig config = null;

            if (configFilePath != null)
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(configFilePath), "Expecting the analysis config file path to be set");

                if (File.Exists(configFilePath))
                {
                    config = AnalysisConfig.Load(configFilePath);
                    if (config.LocalSettings == null)
                    {
                        config.LocalSettings = new AnalysisProperties();
                    }
                }
                else
                {
                    logger.LogError(Resources.ERROR_ConfigFileNotFound, configFilePath);
                }
            }
            return config;
        }

        private void LogProcessingStarted(AnalysisPhase phase)
        {
            var phaseLabel = phase == AnalysisPhase.PreProcessing ? Resources.PhaseLabel_PreProcessing : Resources.PhaseLabel_PostProcessing;
            logger.LogInfo(Resources.MSG_ProcessingStarted, phaseLabel);
        }

        private void LogProcessingCompleted(AnalysisPhase phase, int exitCode)
        {
            var phaseLabel = phase == AnalysisPhase.PreProcessing ? Resources.PhaseLabel_PreProcessing : Resources.PhaseLabel_PostProcessing;
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
