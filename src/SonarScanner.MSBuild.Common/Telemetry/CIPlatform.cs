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

public enum CIPlatform
{
    None,
    GitHubActions,
    AzureDevops,
    GitLabCI,
    TravisCI,
    CircleCI,
    Jenkins,
    BitbucketPipelines,
    AppVeyor,
    TeamCity,
    Bamboo,
    CodeBuild,
    CloudBuild,
    Drone,
    Buildkite
}

public static class CIPlatformDetector
{
    private static readonly Dictionary<CIPlatform, Func<bool>> PlatformDetectors = new()
    {
        // https://docs.github.com/en/actions/reference/workflows-and-actions/variables#default-environment-variables
        { CIPlatform.GitHubActions,      () => EnvironmentVariablePresent("GITHUB_ACTIONS") },
        // https://learn.microsoft.com/en-us/azure/devops/pipelines/build/variables?view=azure-devops&tabs=yaml#system-variables-devops-services
        { CIPlatform.AzureDevops,        () => EnvironmentVariablePresent("TF_BUILD") },
        // https://docs.gitlab.com/ci/variables/predefined_variables/
        { CIPlatform.GitLabCI,           () => EnvironmentVariablePresent("GITLAB_CI") },
        // https://docs.travis-ci.com/user/environment-variables/#default-environment-variables
        { CIPlatform.TravisCI,           () => EnvironmentVariablePresent("TRAVIS") },
        // https://circleci.com/docs/variables/#built-in-environment-variables
        { CIPlatform.CircleCI,           () => EnvironmentVariablePresent("CIRCLECI") },
        // https://www.jenkins.io/doc/book/pipeline/jenkinsfile/#using-environment-variables
        { CIPlatform.Jenkins,            () => EnvironmentVariablePresent("JENKINS_URL") || EnvironmentVariablePresent("JENKINS_HOME") },
        // https://support.atlassian.com/bitbucket-cloud/docs/variables-and-secrets/
        { CIPlatform.BitbucketPipelines, () => EnvironmentVariablePresent("BITBUCKET_BUILD_NUMBER") },
        // https://www.appveyor.com/docs/environment-variables/
        { CIPlatform.AppVeyor,           () => EnvironmentVariablePresent("APPVEYOR") },
        // https://www.jetbrains.com/help/teamcity/predefined-build-parameters.html
        { CIPlatform.TeamCity,           () => EnvironmentVariablePresent("TEAMCITY_VERSION") },
        // https://confluence.atlassian.com/bamboo/bamboo-variables-289277087.html
        { CIPlatform.Bamboo,             () => EnvironmentVariablePresent("bamboo_buildKey") },
        // https://docs.aws.amazon.com/codebuild/latest/userguide/build-env-ref-env-vars.html
        { CIPlatform.CodeBuild,          () => EnvironmentVariablePresent("CODEBUILD_BUILD_ID") },
        // https://cloud.google.com/build/docs/configuring-builds/substitute-variable-values
        { CIPlatform.CloudBuild,         () => EnvironmentVariablePresent("BUILD_ID") && EnvironmentVariablePresent("PROJECT_ID") },
        // https://docs.drone.io/pipeline/environment/reference/
        { CIPlatform.Drone,              () => EnvironmentVariablePresent("DRONE") },
        // https://buildkite.com/docs/pipelines/environment-variables
        { CIPlatform.Buildkite,          () => EnvironmentVariablePresent("BUILDKITE") }
    };

    public static CIPlatform Detect() =>
        PlatformDetectors.FirstOrDefault(x => x.Value()).Key;

    private static bool EnvironmentVariablePresent(string variableName) =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variableName));
}
