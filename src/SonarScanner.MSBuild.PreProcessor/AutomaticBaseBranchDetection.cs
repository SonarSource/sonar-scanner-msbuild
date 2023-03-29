/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
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
using System.Collections.Generic;

namespace SonarScanner.MSBuild.PreProcessor
{
    internal static class AutomaticBaseBranchDetection
    {
        private static readonly List<BaseBranchVariable> Candidates = new()
        {
            new BaseBranchVariable("Jenkins", "ghprbTargetBranch"),
            new BaseBranchVariable("Jenkins", "gitlabTargetBranch"),
            new BaseBranchVariable("Jenkins", "BITBUCKET_TARGET_BRANCH"),
            new BaseBranchVariable("GitHub Actions", "GITHUB_BASE_REF"),
            new BaseBranchVariable("GitLab", "CI_MERGE_REQUEST_TARGET_BRANCH_NAME"),
            new BaseBranchVariable("BitBucket Pipelines", "BITBUCKET_PR_DESTINATION_BRANCH"),
        };

        public static bool TryGetValue(out string branch, out string provider)
        {
            foreach (var candidate in Candidates)
            {
                var value = Environment.GetEnvironmentVariable(candidate.VariableName);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    branch = value;
                    provider = candidate.CiProvider;
                    return true;
                }
            }

            branch = string.Empty;
            provider = string.Empty;
            return false;
        }

        private class BaseBranchVariable
        {
            public string CiProvider { get; private set; }
            public string VariableName { get; private set; }

            public BaseBranchVariable(string ciProvider, string variableName)
            {
                CiProvider = ciProvider;
                VariableName = variableName;
            }
        }
    }
}
