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

using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarScanner.MSBuild.PreProcessor;

internal static class AutomaticBaseBranchDetection
{
    private static readonly List<Tuple<string, string>> Candidates = new()
        new("Jenkins", EnvironmentVariables.BaseBranch.JenkingsGitHubPullRequestBuilder),
        new("Jenkins", EnvironmentVariables.BaseBranch.JenkingsGitLab),
        new("Jenkins", EnvironmentVariables.BaseBranch.JenkingsBitBucket),
        new("GitHub Actions", EnvironmentVariables.BaseBranch.GitHub),
        new("GitLab", EnvironmentVariables.BaseBranch.GitLab),
        new("BitBucket Pipelines", EnvironmentVariables.BaseBranch.BitBucket),
    ];

    public static CIProperty GetValue() =>
        Candidates.Select(x => new CIProperty(x.Item1, Environment.GetEnvironmentVariable(x.Item2))).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Value));

    public class CIProperty
    {
        public string CiProvider { get; }
        public string Value { get; }

        public CIProperty(string ciProvider, string value)
        {
            CiProvider = ciProvider;
            Value = value;
        }
    }
}
