//-----------------------------------------------------------------------
// <copyright file="QualityProfile.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class QualityProfile
    {
        private readonly string name;
        private readonly string language;
        private readonly ISet<string> projectKeys;

        private readonly ISet<Rule> activeRules;

        public QualityProfile(string name, string language)
        {
            this.name = name;
            this.language = language;
            this.projectKeys = new HashSet<string>();
            this.activeRules = new HashSet<Rule>();
        }

        public QualityProfile AddProject(string projectKey)
        {
            this.projectKeys.Add(projectKey);
            return this;
        }

        public QualityProfile AddRule(Rule rule)
        {
            this.activeRules.Add(rule);
            return this;
        }

        public string Name { get { return this.name; } }
        public string Language { get { return this.language; } }
        public IEnumerable<string> Projects { get { return this.projectKeys; } }
        public IEnumerable<Rule> ActiveRules { get { return this.activeRules; } }
        
    }
}
