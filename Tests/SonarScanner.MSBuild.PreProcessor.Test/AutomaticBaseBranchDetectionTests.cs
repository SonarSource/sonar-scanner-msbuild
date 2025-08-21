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

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class AutomaticBaseBranchDetectionTests
{
    [TestMethod]
    [DataRow("Jenkins", "ghprbTargetBranch")]
    [DataRow("Jenkins", "gitlabTargetBranch")]
    [DataRow("Jenkins", "BITBUCKET_TARGET_BRANCH")]
    [DataRow("GitHub Actions", "GITHUB_BASE_REF")]
    [DataRow("GitLab", "CI_MERGE_REQUEST_TARGET_BRANCH_NAME")]
    [DataRow("BitBucket Pipelines", "BITBUCKET_PR_DESTINATION_BRANCH")]
    public void TryGetValue_Success(string expectedProvider, string variableName)
    {
        using var environment = new EnvironmentVariableScope().SetVariable(variableName, "42");

        var result = AutomaticBaseBranchDetection.Current();

        result.Should().NotBeNull();
        result.Value.Should().Be("42");
        result.Provider.Should().Be(expectedProvider);
    }

    [TestMethod]
    public void TryGetValue_Failure() =>
        AutomaticBaseBranchDetection.Current().Should().BeNull();
}
