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
        private static readonly List<Tuple<string, string>> Candidates = new()
        {
            new Tuple<string, string>("Jenkins", "ghprbTargetBranch"),
            new Tuple<string, string>("Jenkins", "gitlabTargetBranch"),
            new Tuple<string, string>("Jenkins", "BITBUCKET_TARGET_BRANCH"),
            new Tuple<string, string>("GitHub Actions", "GITHUB_BASE_REF"),
            new Tuple<string, string>("GitLab", "CI_MERGE_REQUEST_TARGET_BRANCH_NAME"),
            new Tuple<string, string>("BitBucket Pipelines", "BITBUCKET_PR_DESTINATION_BRANCH"),
        };

        public static bool TryGetValue(out string branch, out string provider)
        {
            foreach (var candidate in Candidates)
            {
                var value = Environment.GetEnvironmentVariable(candidate.Item2);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    branch = value;
                    provider = candidate.Item1;
                    return true;
                }
            }

            branch = string.Empty;
            provider = string.Empty;
            return false;
        }
    }
}
