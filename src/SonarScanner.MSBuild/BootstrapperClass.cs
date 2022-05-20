/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
#if NETFRAMEWORK
using System.Security.Cryptography;
using SonarScanner.MSBuild.AnalysisWarning;
#endif
using System.Threading.Tasks;
#if NETCOREAPP2_1
using SonarScanner.MSBuild.AnalysisWarning;
#endif
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;

namespace SonarScanner.MSBuild
{
    public class BootstrapperClass
    {
        private const int ErrorCode = 1;
        private const int SuccessCode = 0;

#if NETFRAMEWORK

        private const string WarningMessage = "From [Date], new versions of this scanner will no longer support .NET framework runtime environments less than .NET Framework 4.6.2." +
            " For more information see https://community.sonarsource.com/t/54684";

#endif

        private readonly IProcessorFactory processorFactory;
        private readonly IBootstrapperSettings bootstrapSettings;
        private readonly ILogger logger;
        private readonly Func<string, Version> getAssemblyVersionFunc;

        public BootstrapperClass(IProcessorFactory processorFactory, IBootstrapperSettings bootstrapSettings, ILogger logger)
            : this(processorFactory, bootstrapSettings, logger, assemblyPath => AssemblyName.GetAssemblyName(assemblyPath).Version)
        {
        }

        public BootstrapperClass(IProcessorFactory processorFactory, IBootstrapperSettings bootstrapSettings, ILogger logger, Func<string, Version> getAssemblyVersionFunc)
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

            logger.Verbosity = bootstrapSettings.LoggingVerbosity;
            logger.ResumeOutput();

            var phase = bootstrapSettings.Phase;
            LogProcessingStarted(phase);

            try
            {
                exitCode = phase == AnalysisPhase.PreProcessing
                    ? await PreProcess()
                    : PostProcess();
            }
            catch (AnalysisException ex)
            {
                logger.LogError(ex.Message);
                exitCode = ErrorCode;
            }

            LogProcessingCompleted(phase, exitCode);
            return exitCode;
        }

        private async Task<int> PreProcess()
        {
            logger.LogInfo(Resources.MSG_PreparingDirectories);

            CleanSonarQubeDirectory();
            if (!CopyDlls())
            {
                return ErrorCode;
            }

            logger.IncludeTimestamp = true;

            var preProcessor = processorFactory.CreatePreProcessor();
            Directory.SetCurrentDirectory(bootstrapSettings.TempDirectory);
            var success = await preProcessor.Execute(bootstrapSettings.ChildCmdLineArgs.ToArray());

            return success ? SuccessCode : ErrorCode;
        }

        private void CleanSonarQubeDirectory()
        {
            var rootDirectory = new DirectoryInfo(bootstrapSettings.TempDirectory);

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
                    logger.LogDebug($"Cannot delete directory: '{directory.FullName}' because {ex.Message}.");
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
                    logger.LogDebug($"Cannot delete file: '{file.FullName}' because {ex.Message}.");
                }
            }
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
#if NETFRAMEWORK

                if (IsOlderThan462FrameworkVersion())
                {
                    WarningsSerializer.Serialize(
                        new[] { new Warning(WarningMessage) },
                        Path.Combine(teamBuildSettings.SonarOutputDirectory, "AnalysisWarnings.Scanner.json"));
                }

#endif

#if NETCOREAPP2_1

                const string netcore2Warning =
                    "From the 6th of July 2022, we will no longer release new Scanner for .NET versions that target .NET Core 2.1." +
                    " If you are using the .NET Core Global Tool you will need to use a supported .NET runtime environment." +
                    " For more information see https://community.sonarsource.com/t/54684";
                WarningsSerializer.Serialize(
                    new[] { new Warning(netcore2Warning) },
                    Path.Combine(teamBuildSettings.SonarOutputDirectory, "AnalysisWarnings.Scanner.json"));

#endif
                var postProcessor = processorFactory.CreatePostProcessor();
                succeeded = postProcessor.Execute(bootstrapSettings.ChildCmdLineArgs.ToArray(), config, teamBuildSettings);
            }

            return succeeded ? SuccessCode : ErrorCode;
        }

        /// <summary>
        /// Copies DLLs needed by the targets file that is loaded by MSBuild to the project's .sonarqube directory
        /// </summary>
        private bool CopyDlls()
        {
            var binDirPath = Path.Combine(bootstrapSettings.TempDirectory, "bin");
            Directory.CreateDirectory(binDirPath);
            string[] dllsToCopy = { "SonarScanner.MSBuild.Common.dll", "SonarScanner.MSBuild.Tasks.dll" };

            foreach (var dll in dllsToCopy)
            {
                var from = Path.Combine(bootstrapSettings.ScannerBinaryDirPath, dll);
                var to = Path.Combine(binDirPath, dll);

                if (!File.Exists(to))
                {
                    File.Copy(from, to);
                }
                else if (getAssemblyVersionFunc(from).CompareTo(getAssemblyVersionFunc(to)) != 0)
                {
                    logger.LogError(Resources.ERROR_DllLockedMultipleScanners);
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
                    config.LocalSettings = config.LocalSettings ?? new AnalysisProperties();
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

#if NETFRAMEWORK

        private static bool IsOlderThan462FrameworkVersion() =>
            // This class was introduced in 4.6.2, so if it exists it means the runtime is >= 4.6.2
            typeof(AesManaged).Assembly.GetType("System.Security.Cryptography.DSACng", false) == null;

#endif

    }
}
