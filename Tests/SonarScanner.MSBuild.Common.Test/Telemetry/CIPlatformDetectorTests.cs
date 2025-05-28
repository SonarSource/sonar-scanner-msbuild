﻿/*
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

namespace SonarScanner.MSBuild.Common.Test.Telemetry;

[TestClass]
public class CIPlatformDetectorTests
{
    [DataTestMethod]
    [DataRow(null, null, CIPlatform.None)]
    [DataRow("GITHUB_ACTIONS", "true", CIPlatform.GitHubActions)]
    [DataRow("TF_BUILD", "true", CIPlatform.AzureDevops)]
    [DataRow("GITLAB_CI", "true", CIPlatform.GitLabCI)]
    [DataRow("TRAVIS", "true", CIPlatform.TravisCI)]
    [DataRow("CIRCLECI", "true", CIPlatform.CircleCI)]
    [DataRow("JENKINS_URL", "http://jenkins/", CIPlatform.Jenkins)]
    [DataRow("JENKINS_HOME", "/var/jenkins_home", CIPlatform.Jenkins)]
    [DataRow("BITBUCKET_BUILD_NUMBER", "123", CIPlatform.BitbucketPipelines)]
    [DataRow("APPVEYOR", "True", CIPlatform.AppVeyor)]
    public void Detect_ReturnsExpectedPlatform_WhenEnvVarSet(string variable, string value, CIPlatform expected)
    {
        using var scope = new EnvironmentVariableScope();
        if (variable is not null)
        {
            scope.SetVariable(variable, value);
        }
        CIPlatformDetector.Detect().Should().Be(expected);
    }
}
