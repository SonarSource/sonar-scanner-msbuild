/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
 
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class QualityProfile
    {
        private readonly string id;
        private readonly string language;
        private readonly string organization;
        private readonly ISet<string> projectIds;

        private readonly IList<string> inactiveRules;
        private readonly IList<ActiveRule> activeRules;

        public QualityProfile(string id, string language, string organization)
        {
            this.id = id;
            this.language = language;
            this.organization = organization;
            this.projectIds = new HashSet<string>();
            this.inactiveRules = new List<string>();
            this.activeRules = new List<ActiveRule>();
        }

        public QualityProfile AddProject(string projectKey, string projectBranch = null)
        {
            string projectId = projectKey;
            if (!String.IsNullOrWhiteSpace(projectBranch))
            {
                projectId = projectKey + ":" + projectBranch;
            }

            this.projectIds.Add(projectId);
            return this;
        }

        public QualityProfile AddRule(ActiveRule rule)
        {
            this.activeRules.Add(rule);
            return this;
        }

        public QualityProfile AddInactiveRule(string ruleKey)
        {
            this.inactiveRules.Add(ruleKey);
            return this;
        }

        public string Id { get { return this.id; } }
        public string Language { get { return this.language; } }
        public string Organization { get { return this.organization; } }
        public IEnumerable<string> Projects { get { return this.projectIds; } }
        public IList<ActiveRule> ActiveRules { get { return this.activeRules; } }
        public IList<string> InactiveRules { get { return this.inactiveRules; } }

    }
}
