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

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Helper class to run an executable and capture the output.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public const int ErrorCode = 1;

    private readonly ILogger logger;

    public int ExitCode { get; private set; }

    public ProcessRunner(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs the specified executable and returns a boolean indicating success or failure.
    /// </summary>
    /// <remarks>The standard and error output will be streamed to the logger. Child processes do not inherit the env variables from the parent automatically.</remarks>
    public ProcessResult Execute(ProcessRunnerArguments runnerArgs)
    {
        if (runnerArgs is null)
        {
            throw new ArgumentNullException(nameof(runnerArgs));
        }
        Debug.Assert(!string.IsNullOrWhiteSpace(runnerArgs.ExeName), "Process runner exe name should not be null/empty");

        if (!File.Exists(runnerArgs.ExeName))
        {
            logger.LogError(Resources.ERROR_ProcessRunner_ExeNotFound, runnerArgs.ExeName);
            ExitCode = ErrorCode;
            return new ProcessResult(false);
        }

        var psi = new ProcessStartInfo
        {
            FileName = runnerArgs.ExeName,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false, // required if we want to capture the error output
            ErrorDialog = false,
            CreateNoWindow = true,
            Arguments = runnerArgs.EscapedArguments,
            WorkingDirectory = runnerArgs.WorkingDirectory
        };

        SetEnvironmentVariables(psi, runnerArgs.EnvironmentVariables);

        using MemoryStream standardOutputStream = new();
        using MemoryStream errorOutputStream = new();
        using var standardOutputWriter = new StreamWriter(standardOutputStream);
        using var errorOutputWriter = new StreamWriter(errorOutputStream);

        using var process = new Process();
        process.StartInfo = psi;
        process.ErrorDataReceived += (_, data) => OnErrorDataReceived(data, runnerArgs.LogOutput, errorOutputWriter);
        process.OutputDataReceived += (_, data) => OnOutputDataReceived(data, runnerArgs.LogOutput, standardOutputWriter);

        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        // Warning: do not log the raw command line args as they
        // may contain sensitive data
        logger.LogDebug(Resources.MSG_ExecutingFile,
            runnerArgs.ExeName,
            runnerArgs.AsLogText(),
            runnerArgs.WorkingDirectory,
            runnerArgs.TimeoutInMilliseconds,
            process.Id);

        var succeeded = process.WaitForExit(runnerArgs.TimeoutInMilliseconds);
        // false means we asked the process to stop but it didn't.
        // true: we might still have timed out, but the process ended when we asked it to
        if (succeeded)
        {
            process.WaitForExit(); // Give any asynchronous events the chance to complete
            logger.LogDebug(Resources.MSG_ExecutionExitCode, process.ExitCode);
            ExitCode = process.ExitCode;
        }
        else
        {
            ExitCode = ErrorCode;

            try
            {
                process.Kill();
                logger.LogWarning(Resources.WARN_ExecutionTimedOutKilled, runnerArgs.TimeoutInMilliseconds, runnerArgs.ExeName);
            }
            catch
            {
                logger.LogWarning(Resources.WARN_ExecutionTimedOutNotKilled, runnerArgs.TimeoutInMilliseconds, runnerArgs.ExeName);
            }
        }

        succeeded = succeeded && (ExitCode == 0);

        errorOutputWriter.Flush();
        standardOutputWriter.Flush();
        errorOutputStream.Seek(0, SeekOrigin.Begin);
        standardOutputStream.Seek(0, SeekOrigin.Begin);

        return new ProcessResult(succeeded, new StreamReader(standardOutputStream).ReadToEnd(), new StreamReader(errorOutputStream).ReadToEnd());
    }

    private void SetEnvironmentVariables(ProcessStartInfo psi, IDictionary<string, string> envVariables)
    {
        if (envVariables is null)
        {
            return;
        }

        foreach (var envVariable in envVariables)
        {
            Debug.Assert(!string.IsNullOrEmpty(envVariable.Key), "Env variable name cannot be null or empty");

            if (psi.EnvironmentVariables.ContainsKey(envVariable.Key))
            {
                logger.LogDebug(Resources.MSG_Runner_OverwritingEnvVar, envVariable.Key, psi.EnvironmentVariables[envVariable.Key].RedactSensitiveData(), envVariable.Value.RedactSensitiveData());
            }
            else
            {
                logger.LogDebug(Resources.MSG_Runner_SettingEnvVar, envVariable.Key, envVariable.Value.RedactSensitiveData());
            }
            psi.EnvironmentVariables[envVariable.Key] = envVariable.Value;
        }
    }

    private void OnOutputDataReceived(DataReceivedEventArgs e, bool logOutput, TextWriter standardOutputWriter)
    {
        if (e.Data is not null)
        {
            if (logOutput)
            {
                // It's important to log this as an important message because
                // this the log redirection pipeline of the child process
                logger.LogInfo(e.Data.RedactSensitiveData());
            }
            standardOutputWriter.WriteLine(e.Data.RedactSensitiveData());
        }
    }

    private void OnErrorDataReceived(DataReceivedEventArgs e, bool logOutput, TextWriter errorOutputWriter)
    {
        if (e.Data is not null)
        {
            if (logOutput && e.Data.StartsWith("WARN"))
            {
                logger.LogWarning(e.Data.RedactSensitiveData());
            }
            else if (logOutput)
            {
                logger.LogError(e.Data.RedactSensitiveData());
            }
            errorOutputWriter.WriteLine(e.Data.RedactSensitiveData());
        }
    }
}
