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

namespace SonarScanner.MSBuild;

public class BootstrapperSettings : IBootstrapperSettings
{
    public const string RelativePathToTempDir = @".sonarqube";
    public const string RelativePathToDownloadDir = @"bin";

    private readonly IRuntime runtime;

    public AnalysisPhase Phase { get; }
    public IEnumerable<string> ChildCmdLineArgs { get; }
    public LoggerVerbosity LoggingVerbosity { get; }
    public string ScannerBinaryDirPath => Path.GetDirectoryName(typeof(BootstrapperSettings).Assembly.Location);
    public string TempDirectory => field ??= CalculateTempDir();

    public BootstrapperSettings(AnalysisPhase phase, IEnumerable<string> childCmdLineArgs, LoggerVerbosity verbosity, IRuntime runtime)
    {
        Phase = phase;
        ChildCmdLineArgs = childCmdLineArgs;
        LoggingVerbosity = verbosity;
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    private string CalculateTempDir()
    {
        runtime.LogDebug(Resources.MSG_UsingEnvVarToGetDirectory);
        var rootDir = FirstEnvironmentVariable(EnvironmentVariables.BuildDirectoryLegacy, EnvironmentVariables.BuildDirectoryTfs2015);
        if (string.IsNullOrWhiteSpace(rootDir))
        {
            rootDir = runtime.Directory.GetCurrentDirectory();
        }
        return Path.Combine(rootDir, RelativePathToTempDir);
    }

    private string FirstEnvironmentVariable(params string[] environmentVariables)
    {
        foreach (var name in environmentVariables)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                runtime.LogDebug(Resources.MSG_UsingBuildEnvironmentVariable, name, value);
                return value;
            }
        }
        return null;
    }
}
