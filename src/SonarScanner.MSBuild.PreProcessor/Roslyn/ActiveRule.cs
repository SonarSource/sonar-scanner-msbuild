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

namespace SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

public class SonarRule
{
    public string RepoKey { set; get; }
    public string RuleKey { set; get; }
    public bool IsActive { get; }
    public string TemplateKey { set; get; }
    public string InternalKey { set; get; }
    public Dictionary<string, string> Parameters { set; get; }

    public string InternalKeyOrKey
    {
        get { return InternalKey ?? RuleKey; }
    }

    public SonarRule()
    {
    }

    public SonarRule(string repoKey, string ruleKey)
    {
        RepoKey = repoKey;
        RuleKey = ruleKey;
    }

    public SonarRule(string repoKey, string ruleKey, bool isActive)
    {
        RepoKey = repoKey;
        RuleKey = ruleKey;
        IsActive = isActive;
    }

    public SonarRule(string repoKey, string ruleKey, string internalKey, string templateKey, bool isActive)
    {
        RepoKey = repoKey;
        RuleKey = ruleKey;
        InternalKey = internalKey;
        TemplateKey = templateKey;
        IsActive = isActive;
    }
}
