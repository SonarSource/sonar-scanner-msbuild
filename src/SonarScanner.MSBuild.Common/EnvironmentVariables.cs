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

namespace SonarScanner.MSBuild.Common;

public static class EnvironmentVariables
{
    /// <summary>
    /// Env variable used to pass analysis parameters from AzDo extension to the scanner.
    /// </summary>
    public const string SonarQubeScannerParams = "SONARQUBE_SCANNER_PARAMS";

    /// <summary>
    /// Env variable that locates the sonar-scanner.
    /// </summary>
    /// <remarks>Existing values set by the user might cause failures.</remarks>
    public const string SonarScannerHomeVariableName = "SONAR_SCANNER_HOME";

    /// <summary>
    /// Env variable used to specify options to the JVM for the sonar-scanner.
    /// </summary>
    /// <remarks>Large projects error out with OutOfMemoryException if not set.</remarks>
    public const string SonarScannerOptsVariableName = "SONAR_SCANNER_OPTS";

    public const string JavaHomeVariableName = "JAVA_HOME";
    public const string SonarUserHome = "SONAR_USER_HOME";

    /// <summary>
    /// Name of the environment variable that specifies whether the processing of code coverage reports in legacy TeamBuild cases should be skipped.
    /// </summary>
    public const string SkipLegacyCodeCoverage = "SQ_SkipLegacyCodeCoverage";

    /// <summary>
    /// Name of the environment variable that specifies how long to spend attempting to retrieve code coverage reports in legacy TeamBuild cases.
    /// </summary>
    public const string LegacyCodeCoverageTimeoutInMs = "SQ_LegacyCodeCoverageInMs";

    public const string AgentTempDirectory = "AGENT_TEMPDIRECTORY";

    public const string IsInTeamFoundationBuild = "TF_BUILD"; // Common to legacy and non-legacy TeamBuilds

    // Legacy TeamBuild environment variables (XAML Builds)
    public const string TfsCollectionUriLegacy = "TF_BUILD_COLLECTIONURI";

    public const string BuildUriLegacy = "TF_BUILD_BUILDURI";
    public const string BuildDirectoryLegacy = "TF_BUILD_BUILDDIRECTORY";       // Legacy TeamBuild directory (TFS2013 and earlier)
    public const string SourcesDirectoryLegacy = "TF_BUILD_SOURCESDIRECTORY";

    // TFS 2015 (TFS Build) Environment variables
    public const string TfsCollectionUriTfs2015 = "SYSTEM_TEAMFOUNDATIONCOLLECTIONURI";

    public const string BuildUriTfs2015 = "BUILD_BUILDURI";
    public const string BuildDirectoryTfs2015 = "AGENT_BUILDDIRECTORY";         // TeamBuild 2015 and later build directory
    public const string SourcesDirectoryTfs2015 = "BUILD_SOURCESDIRECTORY";

    // This env variable can be set by the VSTest platform installer tool available on AzDo. This will be also used if the user want to set a custom location.
    // https://github.com/microsoft/azure-pipelines-tasks/blob/1538fd6fdb8efd93539b7fe65b00df900d963c1a/Tasks/VsTestPlatformToolInstallerV1/helpers.ts#L8
    public const string VsTestToolCustomInstall = "VsTestToolsInstallerInstalledToolLocation";

    public static class BaseBranch
    {
        public const string JenkingsGitHubPullRequestBuilder = "ghprbTargetBranch";
        public const string JenkingsGitLab = "gitlabTargetBranch";
        public const string JenkingsBitBucket = "BITBUCKET_TARGET_BRANCH";
        public const string GitHub = "GITHUB_BASE_REF";
        public const string GitLab = "CI_MERGE_REQUEST_TARGET_BRANCH_NAME";
        public const string BitBucket = "BITBUCKET_PR_DESTINATION_BRANCH";
    }

    public static class System  // These are not reset during UT initialization
    {
        public const string Path = "PATH";
    }
}
