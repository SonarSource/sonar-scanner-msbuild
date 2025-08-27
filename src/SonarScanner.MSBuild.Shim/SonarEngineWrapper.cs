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

    public SonarEngineWrapper(IRuntime runtime, IProcessRunner processRunner)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public virtual bool Execute(AnalysisConfig config, string propertiesJsonInput)
    {
        _ = config ?? throw new ArgumentNullException(nameof(config));

        return InternalExecute(config, propertiesJsonInput);
    }

    private bool InternalExecute(AnalysisConfig config, string propertiesJsonInput)
    {
        var engine = config.EngineJarPath;
        var javaExe = config.JavaExePath;
        var args = new ProcessRunnerArguments(javaExe, isBatchScript: false)
        {
            CmdLineArgs = ["-jar", engine],
            OutputToLogMessage = SonarEngineOutput.OutputToLogMessage,
            InputWriter = x => x.Write(propertiesJsonInput),
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
}
