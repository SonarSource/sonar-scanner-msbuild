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

namespace SonarScanner.MSBuild.Shim;

public class SonarEngineWrapper
{
    private readonly IRuntime runtime;
    private readonly IProcessRunner processRunner;
    private readonly string javaFileName;

    public SonarEngineWrapper(IRuntime runtime, IProcessRunner processRunner)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        javaFileName = runtime.OperatingSystem.IsUnix() ? "java" : "java.exe";
    }

    public virtual bool Execute(AnalysisConfig config, string standardInput)
    {
        _ = config ?? throw new ArgumentNullException(nameof(config));

        var engine = config.EngineJarPath;
        var javaExe = FindJavaExe(config.JavaExePath);
        var javaParams = JavaParams(config);

        var args = new ProcessRunnerArguments(javaExe, isBatchScript: false)
        {
            CmdLineArgs = javaParams.Any() ? [..javaParams, "-jar", engine] : ["-jar", engine],
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

    private static IEnumerable<string> JavaParams(AnalysisConfig config)
    {
        var sb = new StringBuilder();
        if (Environment.GetEnvironmentVariable(EnvironmentVariables.SonarScannerOptsVariableName)?.Trim() is { Length: > 0 } scannerOpts)
        {
            sb.Append(scannerOpts);
        }

        if (config.ScannerOptsSettings.Any())
        {
            // If there are any duplicates properties, the last one will be used.
            // As of today, properties coming from ScannerOptsSettings are set
            // via the command line, so they should take precedence over the ones
            // set via the environment variable.
            foreach (var property in config.ScannerOptsSettings)
            {
                sb.Append($" {property.AsSonarScannerArg()}");
            }
        }
        return sb.ToString().Split([' '], StringSplitOptions.RemoveEmptyEntries);
    }

    private string FindJavaExe(string configJavaExe) =>
        JavaFromConfig(configJavaExe)
        ?? JavaFromJavaHome()
        ?? JavaFromPath();

    private string JavaFromConfig(string configJavaExe)
    {
        if (runtime.File.Exists(configJavaExe))
        {
            runtime.Logger.LogInfo(Resources.MSG_JavaExe_Found, "Analysis Config", configJavaExe);
            return configJavaExe;
        }
        else
        {
            runtime.Logger.LogInfo(Resources.MSG_JavaExe_NotFound, "Analysis Config", configJavaExe);
            return null;
        }
    }

    private string JavaFromJavaHome()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariables.JavaHomeVariableName) is { Length: > 0 } javaHome)
        {
            runtime.Logger.LogInfo(Resources.MSG_JavaHomeSet, javaHome);
            var javaHomeExe = Path.Combine(javaHome, "bin", javaFileName);
            if (runtime.File.Exists(javaHomeExe))
            {
                runtime.Logger.LogInfo(Resources.MSG_JavaExe_Found, EnvironmentVariables.JavaHomeVariableName, javaHomeExe);
                return javaHomeExe;
            }
            else
            {
                runtime.Logger.LogInfo(Resources.MSG_JavaExe_NotFound, EnvironmentVariables.JavaHomeVariableName, javaHomeExe);
                return null;
            }
        }
        else
        {
            runtime.Logger.LogInfo(Resources.MSG_JavaHomeNotSet);
            return null;
        }
    }

    private string JavaFromPath()
    {
        runtime.Logger.LogInfo(Resources.MSG_JavaExe_UsePath, javaFileName);
        return javaFileName; // Rely on Proccess inbuilt PATH support
    }
}
