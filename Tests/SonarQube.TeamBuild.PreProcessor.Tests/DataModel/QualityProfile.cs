//-----------------------------------------------------------------------
// <copyright file="QualityProfile.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class QualityProfile
    {
        private readonly string id;
        private readonly string language;
        private readonly ISet<string> projectIds;

        private readonly IList<string> inactiveRules;
        private readonly IList<ActiveRule> activeRules;

        public QualityProfile(string id, string language)
        {
            this.id = id;
            this.language = language;
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
        public IEnumerable<string> Projects { get { return this.projectIds; } }
        public IList<ActiveRule> ActiveRules { get { return this.activeRules; } }
        public IList<string> InactiveRules { get { return this.inactiveRules; } }

    }
}
