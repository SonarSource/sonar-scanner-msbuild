/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;

namespace SonarScanner.MSBuild;

public class BootstrapperClass
{
    private const int ErrorCode = 1;
    private const int SuccessCode = 0;

    private readonly IProcessorFactory processorFactory;
    private readonly IBootstrapperSettings bootstrapSettings;
    private readonly ILogger logger;
    private readonly Func<string, Version> getAssemblyVersionFunc;

    public BootstrapperClass(IProcessorFactory processorFactory, IBootstrapperSettings bootstrapSettings, ILogger logger)
        : this(processorFactory, bootstrapSettings, logger, assemblyPath => AssemblyName.GetAssemblyName(assemblyPath).Version)
    {
    }

    internal /* for testing */ BootstrapperClass(IProcessorFactory processorFactory,
                                                 IBootstrapperSettings bootstrapSettings,
                                                 ILogger logger,
                                                 Func<string, Version> getAssemblyVersionFunc)
    {
        this.processorFactory = processorFactory;
        this.bootstrapSettings = bootstrapSettings;
        this.logger = logger;
        this.getAssemblyVersionFunc = getAssemblyVersionFunc;

        Debug.Assert(this.bootstrapSettings != null, "Bootstrapper settings should not be null");
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
        if (phase == AnalysisPhase.PostProcessing)
        {
            logger.WriteTelemetry(BuildSettings.GetSettingsFromEnvironment().SonarOutputDirectory, postProcessing: true);
        }
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
        IBuildSettings teamBuildSettings = BuildSettings.GetSettingsFromEnvironment();
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
        logger.AddTelemetryMessage(TelemetryKeys.ScannerEngineAnalysisResult, succeeded ? "success" : "failure");
        return succeeded ? SuccessCode : ErrorCode;
    }

    /// <summary>
    /// Copies DLLs needed by the targets file that is loaded by MSBuild to the project's .sonarqube directory
    /// </summary>
    private bool CopyDlls()
    {
        var binDirPath = Path.Combine(bootstrapSettings.TempDirectory, "bin");
        Directory.CreateDirectory(binDirPath);
        string[] dllsToCopy = { "SonarScanner.MSBuild.Common.dll", "SonarScanner.MSBuild.Tasks.dll", "Newtonsoft.Json.dll" };

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
}
