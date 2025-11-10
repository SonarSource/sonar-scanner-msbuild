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

using System;
using System.Globalization;
using System.IO;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.Common.TFS;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Provides access to TeamBuild-specific settings and settings calculated
/// from those settings
/// </summary>
public class BuildSettings : IBuildSettings
{
    public const int DefaultLegacyCodeCoverageTimeout = 30000; // ms

    public static bool IsInTeamBuild => TryGetBoolEnvironmentVariable(EnvironmentVariables.IsInTeamFoundationBuild, false);
    public static bool SkipLegacyCodeCoverageProcessing => TryGetBoolEnvironmentVariable(EnvironmentVariables.SkipLegacyCodeCoverage, false);
    public static int LegacyCodeCoverageProcessingTimeout => TryGetIntEnvironmentVariable(EnvironmentVariables.LegacyCodeCoverageTimeoutInMs, DefaultLegacyCodeCoverageTimeout);
    public BuildEnvironment BuildEnvironment { get; private set; }
    public string TfsUri { get; private set; }
    public string BuildUri { get; private set; }
    public string SourcesDirectory { get; private set; }
    public string CoverageToolUserSuppliedPath { get; private set; }
    public string SonarConfigDirectory => Path.Combine(AnalysisBaseDirectory, "conf");
    public string SonarOutputDirectory => Path.Combine(AnalysisBaseDirectory, "out");
    public string SonarBinDirectory => Path.Combine(AnalysisBaseDirectory, "bin");
    public string AnalysisConfigFilePath => Path.Combine(SonarConfigDirectory, FileConstants.ConfigFileName);

    /// <summary>
    /// The base working directory under which the various analysis
    /// sub-directories (bin, conf, out) should be created
    /// </summary>
    public string AnalysisBaseDirectory { get; private set; }

    /// <summary>
    /// The build directory as specified by the build system
    /// </summary>
    public string BuildDirectory { get; private set; }

    /// <summary>
    /// The working directory that will be set when the sonar-scanner will be spawned
    /// </summary>
    public string SonarScannerWorkingDirectory { get; private set; }

    /// <summary>
    /// Private constructor to prevent direct creation
    /// </summary>
    private BuildSettings() { }

    /// <summary>
    /// Factory method to create and return a new set of team build settings
    /// calculated from environment variables.
    /// Returns null if all the required environment variables are not present.
    /// </summary>
    public static BuildSettings GetSettingsFromEnvironment()
    {
        var env = GetBuildEnvironment();
        var settings = env switch
        {
            BuildEnvironment.LegacyTeamBuild => new BuildSettings
            {
                BuildEnvironment = env,
                BuildUri = Environment.GetEnvironmentVariable(EnvironmentVariables.BuildUriLegacy),
                TfsUri = Environment.GetEnvironmentVariable(EnvironmentVariables.TfsCollectionUriLegacy),
                BuildDirectory = Environment.GetEnvironmentVariable(EnvironmentVariables.BuildDirectoryLegacy),
                SourcesDirectory = Environment.GetEnvironmentVariable(EnvironmentVariables.SourcesDirectoryLegacy),
            },
            BuildEnvironment.TeamBuild => new BuildSettings
            {
                BuildEnvironment = env,
                BuildUri = Environment.GetEnvironmentVariable(EnvironmentVariables.BuildUriTfs2015),
                TfsUri = Environment.GetEnvironmentVariable(EnvironmentVariables.TfsCollectionUriTfs2015),
                BuildDirectory = Environment.GetEnvironmentVariable(EnvironmentVariables.BuildDirectoryTfs2015),
                SourcesDirectory = Environment.GetEnvironmentVariable(EnvironmentVariables.SourcesDirectoryTfs2015),
                CoverageToolUserSuppliedPath = Environment.GetEnvironmentVariable(EnvironmentVariables.VsTestToolCustomInstall)
            },
            _ => new BuildSettings
            {
                BuildEnvironment = env,
                // there's no reliable of way of finding the SourcesDirectory, except after the build
                CoverageToolUserSuppliedPath = Environment.GetEnvironmentVariable(EnvironmentVariables.VsTestToolCustomInstall)
            }
        };

        // We expect the bootstrapper to have set the WorkingDir of the processors to be the temp dir (i.e. .sonarqube)
        settings.AnalysisBaseDirectory = Directory.GetCurrentDirectory();

        // https://jira.sonarsource.com/browse/SONARMSBRU-100 the sonar-scanner should be able to locate files such as the resharper output
        // via relative paths, at least in the msbuild scenario, so the working directory should be The directory from which the user issued the command
        // Note that this will not work for TFS Build / XAML Build as the sources directory is more difficult to compute
        settings.SonarScannerWorkingDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;

        return settings;
    }

    /// <summary>
    /// Creates and returns settings for a non-TeamBuild environment - for testing purposes. Use <see cref="GetSettingsFromEnvironment(ILogger)"/>
    /// in product code.
    /// </summary>
    public static BuildSettings CreateSettingsForTesting(string analysisBaseDirectory, BuildEnvironment buildEnvironment = BuildEnvironment.NotTeamBuild)
    {
        if (string.IsNullOrWhiteSpace(analysisBaseDirectory))
        {
            throw new ArgumentNullException(nameof(analysisBaseDirectory));
        }

        var workingDirectory = Directory.GetParent(analysisBaseDirectory)?.FullName ?? throw new ArgumentException("Invalid analysis base directory");
        return new BuildSettings
        {
            BuildEnvironment = buildEnvironment,
            AnalysisBaseDirectory = analysisBaseDirectory,
            SonarScannerWorkingDirectory = workingDirectory,
            SourcesDirectory = workingDirectory,
        };
    }

    /// <summary>
    /// Returns the type of the current build environment: not under TeamBuild, legacy TeamBuild, "new" TeamBuild
    /// </summary>
    private static BuildEnvironment GetBuildEnvironment()
    {
        var env = BuildEnvironment.NotTeamBuild;

        if (IsInTeamBuild)
        {
            // Work out which flavor of TeamBuild
            var buildUri = Environment.GetEnvironmentVariable(EnvironmentVariables.BuildUriLegacy);
            if (string.IsNullOrEmpty(buildUri))
            {
                buildUri = Environment.GetEnvironmentVariable(EnvironmentVariables.BuildUriTfs2015);
                if (!string.IsNullOrEmpty(buildUri))
                {
                    env = BuildEnvironment.TeamBuild;
                }
            }
            else
            {
                env = BuildEnvironment.LegacyTeamBuild;
            }
        }
        return env;
    }

    private static bool TryGetBoolEnvironmentVariable(string envVar, bool defaultValue) =>
        Environment.GetEnvironmentVariable(envVar) is { } value && bool.TryParse(value, out var result)
            ? result
            : defaultValue;

    private static int TryGetIntEnvironmentVariable(string envVar, int defaultValue) =>
        Environment.GetEnvironmentVariable(envVar) is { } value
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;
}
