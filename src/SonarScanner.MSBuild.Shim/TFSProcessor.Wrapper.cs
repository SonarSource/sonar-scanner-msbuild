/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

public class TfsProcessorWrapper
{
    private readonly IRuntime runtime;

    public TfsProcessorWrapper(IRuntime runtime) =>
        this.runtime = runtime;

    public virtual bool Execute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments)
    {
        _ = config ?? throw new ArgumentNullException(nameof(config));
        _ = userCmdLineArguments ?? throw new ArgumentNullException(nameof(userCmdLineArguments));

        return InternalExecute(config, userCmdLineArguments);
    }

    public virtual /* for test purposes */ bool ExecuteProcessorRunner(AnalysisConfig config,
                                                                       string exeFileName,
                                                                       IEnumerable<string> userCmdLineArguments,
                                                                       IProcessRunner runner)
    {
        Debug.Assert(File.Exists(exeFileName), "The specified exe file does not exist: " + exeFileName);

        runtime.LogInfo(Resources.MSG_TFSProcessorCalling);

        Debug.Assert(!string.IsNullOrWhiteSpace(config.SonarScannerWorkingDirectory), "The working dir should have been set in the analysis config");
        Debug.Assert(Directory.Exists(config.SonarScannerWorkingDirectory), "The working dir should exist");

        var converterArgs = new ProcessRunnerArguments(exeFileName, !runtime.OperatingSystem.IsWindows())
        {
            CmdLineArgs = userCmdLineArguments.Select(x => new ProcessRunnerArguments.Argument(x)).ToArray(),
            WorkingDirectory = config.SonarScannerWorkingDirectory,
        };

        var result = runner.Execute(converterArgs);
        if (result.Succeeded)
        {
            runtime.LogInfo(Resources.MSG_TFSProcessorCompleted);
        }
        else
        {
            runtime.LogError(Resources.ERR_TFSProcessorExecutionFailed);
        }
        return result.Succeeded;
    }

    private bool InternalExecute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments)
    {
        var exeFileName = FindProcessorExe();
        return ExecuteProcessorRunner(config, exeFileName, userCmdLineArguments, new ProcessRunner(runtime));
    }

    private static string FindProcessorExe()
    {
        var execFolder = Path.GetDirectoryName(typeof(TfsProcessorWrapper).Assembly.Location);
        return Path.Combine(execFolder, "SonarScanner.MSBuild.TFSProcessor.exe");
    }
}
