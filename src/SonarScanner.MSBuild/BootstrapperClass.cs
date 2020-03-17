/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        private readonly Func<string, Version> getAssemblyVersionFunc;

        public BootstrapperClass(IProcessorFactory processorFactory, IBootstrapperSettings bootstrapSettings, ILogger logger)
            : this(processorFactory, bootstrapSettings, logger, assemblyPath => AssemblyName.GetAssemblyName(assemblyPath).Version)
        {
        }

        public BootstrapperClass(IProcessorFactory processorFactory, IBootstrapperSettings bootstrapSettings, ILogger logger,
            Func<string, Version> getAssemblyVersionFunc)
        {
            this.processorFactory = processorFactory;
            this.bootstrapSettings = bootstrapSettings;
            this.logger = logger;
            this.getAssemblyVersionFunc = getAssemblyVersionFunc;

            Debug.Assert(this.bootstrapSettings != null, "Bootstrapper settings should not be null");
            Debug.Assert(this.bootstrapSettings.Phase != AnalysisPhase.Unspecified, "Expecting the processing phase to be specified");
        }

        /// <summary>
        /// Bootstraps a begin or end step, based on the bootstrap settings.
        /// </summary>
        public async Task<int> Execute()
        {
            int exitCode;

            this.logger.Verbosity = this.bootstrapSettings.LoggingVerbosity;
            this.logger.ResumeOutput();

            var phase = this.bootstrapSettings.Phase;
            LogProcessingStarted(phase);

            try
            {
                if (phase == AnalysisPhase.PreProcessing)
                {
                    exitCode = await PreProcess();
                }
                else
                {
                    exitCode = PostProcess();
                }
            }
            catch (AnalysisException ex)
            {
                this.logger.LogError(ex.Message);
                exitCode = ErrorCode;
            }

            LogProcessingCompleted(phase, exitCode);
            return exitCode;
        }

        private async Task<int> PreProcess()
        {
            this.logger.LogInfo(Resources.MSG_PreparingDirectories);

            CleanSonarQubeDirectory();
            if (!CopyDlls())
            {
                return ErrorCode;
            }

            this.logger.IncludeTimestamp = true;

            var preProcessor = this.processorFactory.CreatePreProcessor();
            Directory.SetCurrentDirectory(this.bootstrapSettings.TempDirectory);
            var success = await preProcessor.Execute(this.bootstrapSettings.ChildCmdLineArgs.ToArray());

            return success ? SuccessCode : ErrorCode;
        }

        private void CleanSonarQubeDirectory()
        {
            var rootDirectory = new DirectoryInfo(this.bootstrapSettings.TempDirectory);

            if (!rootDirectory.Exists)
            {
                return;
            }

            foreach (var directory in rootDirectory.GetDirectories())
            {
                try
                {
                    directory.Delete(true);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                {
                    this.logger.LogDebug($"Cannot delete directory: '{directory.FullName}' because {ex.Message}.");
                }
            }

            foreach (var file in rootDirectory.GetFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    file.Delete();
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                {
                    this.logger.LogDebug($"Cannot delete file: '{file.FullName}' because {ex.Message}.");
                }
            }
        }

        private int PostProcess()
        {
            this.logger.IncludeTimestamp = true;

            if (!Directory.Exists(this.bootstrapSettings.TempDirectory))
            {
                this.logger.LogError(Resources.ERROR_TempDirDoesNotExist);
                return ErrorCode;
            }

            Directory.SetCurrentDirectory(this.bootstrapSettings.TempDirectory);
            ITeamBuildSettings teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(this.logger);
            var config = GetAnalysisConfig(teamBuildSettings.AnalysisConfigFilePath);

            bool succeeded;
            if (config == null)
            {
                succeeded = false;
            }
            else
            {
                var postProcessor = this.processorFactory.CreatePostProcessor();
                succeeded = postProcessor.Execute(this.bootstrapSettings.ChildCmdLineArgs.ToArray(), config, teamBuildSettings);
            }

            return succeeded ? SuccessCode : ErrorCode;
        }

        /// <summary>
        /// Copies DLLs needed by the targets file that is loaded by MSBuild to the project's .sonarqube directory
        /// </summary>
        private bool CopyDlls()
        {
            var binDirPath = Path.Combine(this.bootstrapSettings.TempDirectory, "bin");
            Directory.CreateDirectory(binDirPath);
            string[] dllsToCopy = { "SonarScanner.MSBuild.Common.dll", "SonarScanner.MSBuild.Tasks.dll" };

            foreach (var dll in dllsToCopy)
            {
                var from = Path.Combine(this.bootstrapSettings.ScannerBinaryDirPath, dll);
                var to = Path.Combine(binDirPath, dll);

                if (!File.Exists(to))
                {
                    File.Copy(from, to);
                }
                else if (this.getAssemblyVersionFunc(from).CompareTo(this.getAssemblyVersionFunc(to)) != 0)
                {
                    this.logger.LogError(Resources.ERROR_DllLockedMultipleScanners);
                    return false;
                }
                else
                {
                    // do nothing
                }
            }

            return true;
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
                    this.logger.LogError(Resources.ERROR_ConfigFileNotFound, configFilePath);
                }
            }
            return config;
        }

        private void LogProcessingStarted(AnalysisPhase phase)
        {
            var phaseLabel = phase == AnalysisPhase.PreProcessing ? Resources.PhaseLabel_PreProcessing : Resources.PhaseLabel_PostProcessing;
            this.logger.LogInfo(Resources.MSG_ProcessingStarted, phaseLabel);
        }

        private void LogProcessingCompleted(AnalysisPhase phase, int exitCode)
        {
            var phaseLabel = phase == AnalysisPhase.PreProcessing ? Resources.PhaseLabel_PreProcessing : Resources.PhaseLabel_PostProcessing;
            if (exitCode == ProcessRunner.ErrorCode)
            {
                this.logger.LogError(Resources.ERROR_ProcessingFailed, phaseLabel, exitCode);
            }
            else
            {
                this.logger.LogInfo(Resources.MSG_ProcessingSucceeded, phaseLabel);
            }
        }
    }
}
