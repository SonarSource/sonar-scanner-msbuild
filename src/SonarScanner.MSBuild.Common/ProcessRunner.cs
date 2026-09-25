/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

public sealed class ProcessRunner : IProcessRunner
{
    public const int ErrorCode = 1;

    private readonly IRuntime runtime;

    public ProcessRunner(IRuntime runtime) =>
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    /// <summary>
    /// Runs the specified executable and returns a boolean indicating success or failure.
    /// </summary>
    /// <remarks>The standard and error output will be streamed to the logger. Child processes do not inherit the env variables from the parent automatically.</remarks>
    public ProcessResult Execute(ProcessRunnerArguments runnerArgs)
    {
        _ = runnerArgs ?? throw new ArgumentNullException(nameof(runnerArgs));
        Debug.Assert(!string.IsNullOrWhiteSpace(runnerArgs.ExeName), "Process runner exe name should not be null/empty");
        if (runnerArgs.ExeMustExists && !File.Exists(runnerArgs.ExeName))
        {
            runtime.LogError(Resources.ERROR_ProcessRunner_ExeNotFound, runnerArgs.ExeName);
            return new ProcessResult(false);
        }
        using var standardOutputStream = new MemoryStream();
        using var errorOutputStream = new MemoryStream();
        using var standardOutputWriter = new StreamWriter(standardOutputStream);
        using var errorOutputWriter = new StreamWriter(errorOutputStream);
        var psi = new ProcessStartInfo
        {
            // ShortName avoids long path issues: https://github.com/dotnet/runtime/issues/58492#issue-984992485
            FileName = runtime.File.ShortName(runtime.OperatingSystem.OperatingSystem(), runnerArgs.ExeName),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = runnerArgs.StandardInput is not null,
            UseShellExecute = false, // required if we want to capture the error output
            ErrorDialog = false,
            CreateNoWindow = true,
            Arguments = runnerArgs.EscapedArguments,
            WorkingDirectory = runnerArgs.WorkingDirectory
        };
        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => HandleProcessOutput(e.Data, stdOut: true, runnerArgs.LogOutput, standardOutputWriter, runnerArgs.OutputToLogMessage);
        process.ErrorDataReceived += (_, e) => HandleProcessOutput(e.Data, stdOut: false, runnerArgs.LogOutput, errorOutputWriter, runnerArgs.OutputToLogMessage);
        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        // Warning: do not log the raw command line args as they may contain sensitive data
        // AsLogText() returns the CmdLineArgs, but we invoke the process with 'EscapedArguments'
        runtime.LogDebug(
            Resources.MSG_ExecutingFile,
            psi.FileName,
            runnerArgs.AsLogText(),
            runnerArgs.WorkingDirectory,
            process.Id);
        if (runnerArgs.StandardInput is { } input)
        {
            // We need to write to the underlying stream directly, so we can control the encoding used for writing.
            // Without this, the encodings like https://en.wikipedia.org/wiki/Code_page_437 might be used, which can lead to issues if the input contains non-ASCII characters.
            // This is under test by IT ScannerEngineTest.scannerInput_UTF8
            using var utf8Writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)); // StreamWriter closes StandardInput on dispose
            utf8Writer.Write(input);
        }
        process.WaitForExit();
        runtime.LogDebug(Resources.MSG_ExecutionExitCode, process.ExitCode);
        errorOutputWriter.Flush();
        standardOutputWriter.Flush();
        errorOutputStream.Seek(0, SeekOrigin.Begin);
        standardOutputStream.Seek(0, SeekOrigin.Begin);
        return new ProcessResult(process.ExitCode == 0, new StreamReader(standardOutputStream).ReadToEnd(), new StreamReader(errorOutputStream).ReadToEnd());
    }

    private void HandleProcessOutput(string data, bool stdOut, bool logOutput, TextWriter outputWriter, OutputToLogMessage outputToLogMessage)
    {
        if (data is not null
            && outputToLogMessage?.Invoke(stdOut, data) is { } logMessage
            && logMessage.Message.RedactSensitiveData() is { } redactedMsg)
        {
            if (logOutput)
            {
                switch (logMessage.Level)
                {
                    case LogLevel.Info:
                        runtime.LogInfo(redactedMsg);
                        break;
                    case LogLevel.Warning:
                        runtime.LogWarning(redactedMsg);
                        break;
                    case LogLevel.Error:
                        runtime.LogError(redactedMsg);
                        break;
                }
            }
            outputWriter.WriteLine(redactedMsg);
        }
    }
}
