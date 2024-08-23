/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

using System.Collections.Generic;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.Test;

internal class QualityProfile
{
    private readonly ISet<string> projectIds;

    public QualityProfile(string id, string language, string organization)
    {
        Id = id;
        Language = language;
        Organization = organization;
        this.projectIds = new HashSet<string>();
        Rules = new List<SonarRule>();
    }

    public QualityProfile AddProject(string projectKey, string projectBranch = null)
    {
        var projectId = projectKey;
        if (!string.IsNullOrWhiteSpace(projectBranch))
        {
            projectId = projectKey + ":" + projectBranch;
        }

        this.projectIds.Add(projectId);
        return this;
    }

    public QualityProfile AddRule(SonarRule rule)
    {
        Rules.Add(rule);
        return this;
    }

    public string Id { get; }
    public string Language { get; }
    public string Organization { get; }
    public IEnumerable<string> Projects { get { return this.projectIds; } }
    public IList<SonarRule> Rules { get; }
}
