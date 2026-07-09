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
    internal static readonly Dictionary<string[], CIPlatform> PlatformVariables = new()
    {
        // https://docs.github.com/en/actions/reference/workflows-and-actions/variables#default-environment-variables
        { ["GITHUB_ACTIONS"], CIPlatform.GitHubActions },
        // https://learn.microsoft.com/en-us/azure/devops/pipelines/build/variables?view=azure-devops&tabs=yaml#system-variables-devops-services
        { ["TF_BUILD"], CIPlatform.AzureDevops },
        // https://docs.gitlab.com/ci/variables/predefined_variables/
        { ["GITLAB_CI"], CIPlatform.GitLabCI },
        // https://docs.travis-ci.com/user/environment-variables/#default-environment-variables
        { ["TRAVIS"], CIPlatform.TravisCI },
        // https://circleci.com/docs/variables/#built-in-environment-variables
        { ["CIRCLECI"], CIPlatform.CircleCI },
        // https://www.jenkins.io/doc/book/pipeline/jenkinsfile/#using-environment-variables
        { ["JENKINS_URL"], CIPlatform.Jenkins },
        { ["JENKINS_HOME"], CIPlatform.Jenkins },
        // https://support.atlassian.com/bitbucket-cloud/docs/variables-and-secrets/
        { ["BITBUCKET_BUILD_NUMBER"], CIPlatform.BitbucketPipelines },
        // https://www.appveyor.com/docs/environment-variables/
        { ["APPVEYOR"], CIPlatform.AppVeyor },
        // https://www.jetbrains.com/help/teamcity/predefined-build-parameters.html
        { ["TEAMCITY_VERSION"], CIPlatform.TeamCity },
        // https://confluence.atlassian.com/bamboo/bamboo-variables-289277087.html
        { ["bamboo_buildKey"], CIPlatform.Bamboo },
        // https://docs.aws.amazon.com/codebuild/latest/userguide/build-env-ref-env-vars.html
        { ["CODEBUILD_BUILD_ID"], CIPlatform.CodeBuild },
        // https://cloud.google.com/build/docs/configuring-builds/substitute-variable-values
        { ["BUILD_ID", "PROJECT_ID"], CIPlatform.CloudBuild },
        // https://docs.drone.io/pipeline/environment/reference/
        { ["DRONE"], CIPlatform.Drone },
        // https://buildkite.com/docs/pipelines/environment-variables
        { ["BUILDKITE"], CIPlatform.Buildkite }
    };

    public static CIPlatform Detect() =>
        PlatformVariables.FirstOrDefault(x => x.Key.All(EnvironmentVariablePresent)).Value;

    private static bool EnvironmentVariablePresent(string variableName) =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variableName));
}
