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

using System.ComponentModel;

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

    public virtual bool Execute(AnalysisConfig config, string standardInput, IAnalysisPropertyProvider userCmdLineArguments)
    {
        _ = config ?? throw new ArgumentNullException(nameof(config));
        _ = userCmdLineArguments ?? throw new ArgumentNullException(nameof(userCmdLineArguments));

        var engine = config.EngineJarPath;
        var javaExe = FindJavaExe(config.JavaExePath);
        var javaParams = JavaParams(config, userCmdLineArguments, runtime).Select(x => new ProcessRunnerArguments.Argument(x, true));

        var args = new ProcessRunnerArguments(javaExe, isBatchScript: false)
        {
            CmdLineArgs = javaParams.Any() ? [.. javaParams, "-jar", engine] : ["-jar", engine],
            WorkingDirectory = config.SonarScannerWorkingDirectory,
            OutputToLogMessage = SonarEngineOutput.OutputToLogMessage,
            StandardInput = standardInput,
            ExeMustExists = false, // Allow "java.exe" to be found via %PATH%
        };
        return Execute(args);
    }

    // this is public static so scanner-cli can call it, when it is dropped it can be private and use the runtime field
    public static IEnumerable<string> JavaParams(AnalysisConfig config, IAnalysisPropertyProvider userCmdLineArguments, IRuntime runtime)
    {
        // If there is a value for SONAR_SCANNER_OPTS pass it through explicitly
        var scannerOpts = Environment.GetEnvironmentVariable(EnvironmentVariables.SonarScannerOptsVariableName);
        if (scannerOpts?.Trim() is { Length: > 0 })
        {
            runtime.LogInfo(Resources.MSG_UsingSuppliedSonarScannerOptsValue, EnvironmentVariables.SonarScannerOptsVariableName, scannerOpts.RedactSensitiveData());
            yield return scannerOpts;
        }

        // If trustStorePath is set in the begin step, it will be in the ScannerOptsSettings.
        // If it is not set in the begin step, it could be a default path in unix, or '-Djavax.net.ssl.trustStoreType=Windows-ROOT' on Windows.
        // This is calculated in the TrustStorePreProcessor: https://github.com/SonarSource/sonar-scanner-msbuild/blob/66618b506d3d951e7ca4ca00d9f86dba35b12e48/src/SonarScanner.MSBuild.PreProcessor/AnalysisConfigProcessing/Processors/TruststorePropertiesProcessor.cs#L46
        if (config.ScannerOptsSettings.Any())
        {
            // If there are any duplicates properties, the last one will be used.
            // As of today, properties coming from ScannerOptsSettings are set
            // via the command line, so they should take precedence over the ones
            // set via the environment variable.
            foreach (var property in config.ScannerOptsSettings)
            {
                yield return property.AsSonarScannerArg();
            }
        }

        // We need to map the truststore password to the javax.net.ssl.trustStorePassword property and invoke java with it.
        // If the password is set via CLI we use it.
        // If it is not set, we  use the default value, unless it is already in the SONAR_SCANNER_OPTS.
        if (!userCmdLineArguments.TryGetValue(SonarProperties.TruststorePassword, out var truststorePassword))
        {
            var truststorePath = config.ScannerOptsSettings.FirstOrDefault(x => x.Id == SonarProperties.JavaxNetSslTrustStore);
            truststorePassword = TruststoreUtils.TruststoreDefaultPassword(truststorePath?.Value, runtime.Logger);
        }

        if (!SonarPropertiesDefault.TruststorePasswords.Contains(truststorePassword)
            || scannerOpts is null
            || !scannerOpts.Contains($"-D{SonarProperties.JavaxNetSslTrustStorePassword}="))
        {
            yield return runtime.OperatingSystem.IsUnix()
                ? $"-D{SonarProperties.JavaxNetSslTrustStorePassword}={truststorePassword}"
                : $"-D{SonarProperties.JavaxNetSslTrustStorePassword}=\"{truststorePassword}\"";
        }
    }

    private bool Execute(ProcessRunnerArguments args)
    {
        ProcessResult result;
        try
        {
            result = processRunner.Execute(args);
        }
        // Exceptions listed in
        // https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.start?view=net-9.0#system-diagnostics-process-start(system-diagnostics-processstartinfo)
        catch (Win32Exception ex)
        {
            runtime.LogError(Resources.ERR_ScannerEngineExecutionFailedWithException, ex.GetType().FullName, $"Error Code = {ex.ErrorCode}. {ex.Message}");
            return false;
        }
        catch (PlatformNotSupportedException ex)
        {
            runtime.LogError(Resources.ERR_ScannerEngineExecutionFailedWithException, ex.GetType().FullName, ex.Message);
            return false;
        }
        if (result.Succeeded)
        {
            runtime.LogInfo(Resources.MSG_ScannerEngineCompleted);
        }
        else
        {
            runtime.LogError(Resources.ERR_ScannerEngineExecutionFailed);
        }
        return result.Succeeded;
    }

    private string FindJavaExe(string configJavaExe) =>
        JavaFromConfig(configJavaExe)
        ?? JavaFromJavaHome()
        ?? JavaFromPath();

    private string JavaFromConfig(string configJavaExe)
    {
        if (runtime.File.Exists(configJavaExe))
        {
            runtime.LogInfo(Resources.MSG_JavaExe_Found, "Analysis Config", configJavaExe);
            return configJavaExe;
        }
        else
        {
            runtime.LogInfo(Resources.MSG_JavaExe_NotFound, "Analysis Config", configJavaExe);
            return null;
        }
    }

    private string JavaFromJavaHome()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariables.JavaHomeVariableName) is { Length: > 0 } javaHome)
        {
            runtime.LogInfo(Resources.MSG_JavaHomeSet, javaHome);
            var javaHomeExe = Path.Combine(javaHome, "bin", javaFileName);
            if (runtime.File.Exists(javaHomeExe))
            {
                runtime.LogInfo(Resources.MSG_JavaExe_Found, EnvironmentVariables.JavaHomeVariableName, javaHomeExe);
                return javaHomeExe;
            }
            else
            {
                runtime.LogInfo(Resources.MSG_JavaExe_NotFound, EnvironmentVariables.JavaHomeVariableName, javaHomeExe);
                return null;
            }
        }
        else
        {
            runtime.LogInfo(Resources.MSG_JavaHomeNotSet);
            return null;
        }
    }

    private string JavaFromPath()
    {
        runtime.LogInfo(Resources.MSG_JavaExe_UsePath, javaFileName);
        return javaFileName; // Rely on Proccess inbuilt PATH support
    }
}
