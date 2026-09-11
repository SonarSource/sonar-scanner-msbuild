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

/// <summary>
/// Provides access to TeamBuild-specific settings and settings calculated from those settings.
/// </summary>
public class BuildSettings
{
    public bool IsAzureDevOps { get; private set; }
    public string TfsUri { get; private set; }
    public string BuildUri { get; private set; }
    public string SourcesDirectory { get; private set; }
    public string CoverageToolUserSuppliedPath { get; private set; }
    public string SonarConfigDirectory => Path.Combine(AnalysisBaseDirectory, "conf");
    public string SonarOutputDirectory => Path.Combine(AnalysisBaseDirectory, "out");
    public string SonarBinDirectory => Path.Combine(AnalysisBaseDirectory, "bin");
    public string AnalysisConfigFilePath => Path.Combine(SonarConfigDirectory, FileConstants.ConfigFileName);

    /// <summary>
    /// The base working directory under which the various analysis sub-directories (bin, conf, out) should be created.
    /// </summary>
    public string AnalysisBaseDirectory { get; private set; }

    /// <summary>
    /// The build directory as specified by the build system.
    /// </summary>
    public string BuildDirectory { get; private set; }

    /// <summary>
    /// The working directory that will be set when the sonar-scanner will be spawned.
    /// </summary>
    public string SonarScannerWorkingDirectory { get; private set; }

    /// <summary>
    /// Private constructor to prevent direct creation.
    /// </summary>
    private BuildSettings() { }

    /// <summary>
    /// Factory method to create and return a new set of team build settings calculated from environment variables.
    /// </summary>
    public static BuildSettings CreateFromEnvironment(ILogger logger)
    {
        bool isAzDo;
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariables.BuildUriTfs2015)))
        {
            isAzDo = true;
        }
        else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariables.BuildUriLegacy)))
        {
            isAzDo = false;
        }
        else
        {
            logger.LogError(Resources.ERROR_TFSLegacyNotSupported);
            return null;
        }

        var settings = new BuildSettings
        {
            IsAzureDevOps = isAzDo,
            BuildUri = ReadEnvVariable(isAzDo, EnvironmentVariables.BuildUriTfs2015),
            TfsUri = ReadEnvVariable(isAzDo, EnvironmentVariables.TfsCollectionUriTfs2015),
            BuildDirectory = ReadEnvVariable(isAzDo, EnvironmentVariables.BuildDirectoryTfs2015),
            SourcesDirectory = ReadEnvVariable(isAzDo, EnvironmentVariables.SourcesDirectoryTfs2015),
            CoverageToolUserSuppliedPath = ReadEnvVariable(isAzDo, EnvironmentVariables.VsTestToolCustomInstall),
            // We expect the bootstrapper to have set the WorkingDir of the processors to be the temp dir (i.e. .sonarqube)
            AnalysisBaseDirectory = Directory.GetCurrentDirectory(),
            // https://jira.sonarsource.com/browse/SONARMSBRU-100 the sonar-scanner should be able to locate files such as the resharper output
            // via relative paths, at least in the msbuild scenario, so the working directory should be The directory from which the user issued the command
            // Note that this will not work for TFS Build / XAML Build as the sources directory is more difficult to compute
            SonarScannerWorkingDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).FullName
        };

        return settings;
    }

    /// <summary>
    /// Creates and returns settings for a non-TeamBuild environment - for testing purposes. Use <see cref="CreateFromEnvironment(ILogger)"/> in product code.
    /// </summary>
    public static BuildSettings CreateForTesting(string analysisBaseDirectory = null, bool isAzDo = false, string buildDirectory = null, string sourcesDirectory = null, string buildUri = null)
    {
        var workingDirectory = string.IsNullOrEmpty(analysisBaseDirectory) ? null : Directory.GetParent(analysisBaseDirectory)?.FullName;
        return new BuildSettings
        {
            IsAzureDevOps = isAzDo,
            AnalysisBaseDirectory = analysisBaseDirectory,
            SonarScannerWorkingDirectory = workingDirectory,
            SourcesDirectory = sourcesDirectory ?? workingDirectory,
            BuildDirectory = buildDirectory,
            BuildUri = buildUri,
        };
    }

    private static string ReadEnvVariable(bool isAzDo, string variableName) =>
        isAzDo ? Environment.GetEnvironmentVariable(variableName) : null;
}
