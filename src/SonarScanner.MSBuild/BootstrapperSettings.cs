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
    #region Working directory

    public const string RelativePathToTempDir = @".sonarqube";
    public const string RelativePathToDownloadDir = @"bin";

    #endregion Working directory

    private readonly ILogger logger;

    private string tempDir;

    #region Constructor(s)

    public BootstrapperSettings(AnalysisPhase phase, IEnumerable<string> childCmdLineArgs, LoggerVerbosity verbosity,
        ILogger logger)
    {
        Phase = phase;
        ChildCmdLineArgs = childCmdLineArgs;
        LoggingVerbosity = verbosity;
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion Constructor(s)

    #region IBootstrapperSettings

    public string TempDirectory
    {
        get
        {
            if (tempDir == null)
            {
                tempDir = CalculateTempDir();
            }
            return tempDir;
        }
    }

    public AnalysisPhase Phase { get; }

    public IEnumerable<string> ChildCmdLineArgs { get; }

    public LoggerVerbosity LoggingVerbosity { get; }

    public string ScannerBinaryDirPath => Path.GetDirectoryName(typeof(BootstrapperSettings).Assembly.Location);

    #endregion IBootstrapperSettings

    #region Private methods

    private string CalculateTempDir()
    {
        logger.LogDebug(Resources.MSG_UsingEnvVarToGetDirectory);
        var rootDir = GetFirstEnvironmentVariable(EnvironmentVariables.BuildDirectoryLegacy, EnvironmentVariables.BuildDirectoryTfs2015);

        if (string.IsNullOrWhiteSpace(rootDir))
        {
            rootDir = Directory.GetCurrentDirectory();
        }

        return Path.Combine(rootDir, RelativePathToTempDir);
    }

    /// <summary>
    /// Returns the value of the first environment variable from the supplied
    /// list that return a non-empty value
    /// </summary>
    private string GetFirstEnvironmentVariable(params string[] varNames)
    {
        string result = null;
        foreach (var varName in varNames)
        {
            var value = Environment.GetEnvironmentVariable(varName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                logger.LogDebug(Resources.MSG_UsingBuildEnvironmentVariable, varName, value);
                result = value;
                break;
            }
        }
        return result;
    }

    #endregion Private methods
}
