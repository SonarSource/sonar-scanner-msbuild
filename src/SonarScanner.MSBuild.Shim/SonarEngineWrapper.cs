﻿/*
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

namespace SonarScanner.MSBuild.Shim;

public class SonarEngineWrapper
{
    private readonly IRuntime runtime;
    private readonly IProcessRunner processRunner;

    public SonarEngineWrapper(IRuntime runtime, IProcessRunner processRunner)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public virtual bool Execute(AnalysisConfig config, string standardInput)
    {
        _ = config ?? throw new ArgumentNullException(nameof(config));

        var engine = config.EngineJarPath;
        var javaExe = FindJavaExe(config.JavaExePath);
        var args = new ProcessRunnerArguments(javaExe, isBatchScript: false)
        {
            CmdLineArgs = ["-jar", engine],
            OutputToLogMessage = SonarEngineOutput.OutputToLogMessage,
            StandardInput = standardInput,
        };
        var result = processRunner.Execute(args);
        if (result.Succeeded)
        {
            runtime.Logger.LogInfo(Resources.MSG_ScannerEngineCompleted);
        }
        else
        {
            runtime.Logger.LogError(Resources.ERR_ScannerEngineExecutionFailed);
        }
        return result.Succeeded;
    }

    private string FindJavaExe(string configJavaExe)
    {
        if (runtime.File.Exists(configJavaExe))
        {
            runtime.Logger.LogInfo(Resources.MSG_JavaExe_Found, "Analysis Config", configJavaExe);
            return configJavaExe;
        }
        runtime.Logger.LogInfo(Resources.MSG_JavaExe_NotFound, "Analysis Config", configJavaExe);

        var javaExe = runtime.OperatingSystem.IsUnix() ? "java" : "java.exe";
        if (Environment.GetEnvironmentVariable(EnvironmentVariables.JavaHomeVariableName) is { } javaHome
            && !string.IsNullOrEmpty(javaHome))
        {
            runtime.Logger.LogInfo(Resources.MSG_JavaHomeSet, javaHome);
            var javaHomeExe = Path.Combine(javaHome, "bin", javaExe);
            if (runtime.File.Exists(javaHomeExe))
            {
                runtime.Logger.LogInfo(Resources.MSG_JavaExe_Found, EnvironmentVariables.JavaHomeVariableName, javaHomeExe);
                return javaHomeExe;
            }
            runtime.Logger.LogInfo(Resources.MSG_JavaExe_NotFound, EnvironmentVariables.JavaHomeVariableName, javaHomeExe);
        }
        else
        {
            runtime.Logger.LogInfo(Resources.MSG_JavaHomeNotSet);
        }

        runtime.Logger.LogInfo(Resources.MSG_JavaExe_UsePath, javaExe);
        return javaExe; // Rely on Proccess inbuilt PATH support
    }
}
