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
 
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.Integration.Interfaces;
using SonarQube.TeamBuild.PostProcessor.Interfaces;
using SonarQube.TeamBuild.PreProcessor;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SonarQube.Bootstrapper
{
    public class BootstrapperClass
    {
        public const int ErrorCode = 1;
        public const int SuccessCode = 0;

        private readonly IProcessorFactory ProcessorFactory;
        private readonly IBootstrapperSettings BootstrapSettings;
        private readonly ILogger Logger;

        public BootstrapperClass(IProcessorFactory processorFactory, IBootstrapperSettings bootstrapSettings, ILogger logger)
        {
            this.ProcessorFactory = processorFactory;
            this.BootstrapSettings = bootstrapSettings;
            this.Logger = logger;

            Debug.Assert(BootstrapSettings != null, "Bootstrapper settings should not be null");
            Debug.Assert(BootstrapSettings.Phase != AnalysisPhase.Unspecified, "Expecting the processing phase to be specified");
        }

        /// <summary>
        /// Bootstraps a begin or end step, based on the bootstrap settings.
        /// </summary>
        public int Execute()
        {
            int exitCode;

            Logger.Verbosity = BootstrapSettings.LoggingVerbosity;
            Logger.ResumeOutput();

            AnalysisPhase phase = BootstrapSettings.Phase;
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
                Logger.LogError(ex.Message);
                exitCode = ErrorCode;
            }

            LogProcessingCompleted(phase, exitCode);
            return exitCode;
        }

        private int PreProcess()
        {
            Logger.LogInfo(Resources.MSG_PreparingDirectories);
            if (!Utilities.TryEnsureEmptyDirectories(Logger, BootstrapSettings.TempDirectory))
            {
                return ErrorCode;
            }

            CopyDLLs();
            Logger.IncludeTimestamp = true;

            ITeamBuildPreProcessor preProcessor = ProcessorFactory.CreatePreProcessor();
            Directory.SetCurrentDirectory(BootstrapSettings.TempDirectory);
            bool success = preProcessor.Execute(BootstrapSettings.ChildCmdLineArgs.ToArray());

            return success ? SuccessCode : ErrorCode;
        }

        private int PostProcess()
        {
            Logger.IncludeTimestamp = true;

            if (!Directory.Exists(BootstrapSettings.TempDirectory))
            {
                Logger.LogError(Resources.ERROR_TempDirDoesNotExist);
                return ErrorCode;
            }

            Directory.SetCurrentDirectory(BootstrapSettings.TempDirectory);
            ITeamBuildSettings teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(Logger);
            AnalysisConfig config = GetAnalysisConfig(teamBuildSettings.AnalysisConfigFilePath);

            bool succeeded;
            if (config == null)
            {
                succeeded = false;
            }
            else
            {
                IMSBuildPostProcessor postProcessor = ProcessorFactory.CreatePostProcessor();
                succeeded = postProcessor.Execute(BootstrapSettings.ChildCmdLineArgs.ToArray(), config, teamBuildSettings);
            }

            return succeeded ? SuccessCode : ErrorCode;
        }

        /// <summary>
        /// Copies DLLs needed by the targets file that is loaded by MSBuild to the project's .sonarqube directory
        /// </summary>
        private void CopyDLLs()
        {
            string binDirPath = Path.Combine(BootstrapSettings.TempDirectory, "bin");
            Directory.CreateDirectory(binDirPath);
            string[] dllsToCopy = { "SonarQube.Common.dll", "SonarQube.Integration.Tasks.dll" };

            foreach (string dll in dllsToCopy)
            {
                string dllPath = Path.Combine(BootstrapSettings.ScannerBinaryDirPath, dll);
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
                }
                else
                {
                    Logger.LogError(Resources.ERROR_ConfigFileNotFound, configFilePath);
                }
            }
            return config;
        }

        private void LogProcessingStarted(AnalysisPhase phase)
        {
            string phaseLabel = phase == AnalysisPhase.PreProcessing ? Resources.PhaseLabel_PreProcessing : Resources.PhaseLabel_PostProcessing;
            Logger.LogInfo(Resources.MSG_ProcessingStarted, phaseLabel);
        }

        private void LogProcessingCompleted(AnalysisPhase phase, int exitCode)
        {
            string phaseLabel = phase == AnalysisPhase.PreProcessing ? Resources.PhaseLabel_PreProcessing : Resources.PhaseLabel_PostProcessing;
            if (exitCode == ProcessRunner.ErrorCode)
            {
                Logger.LogError(Resources.ERROR_ProcessingFailed, phaseLabel, exitCode);
            }
            else
            {
                Logger.LogInfo(Resources.MSG_ProcessingSucceeded, phaseLabel);
            }
        }
    }
}
