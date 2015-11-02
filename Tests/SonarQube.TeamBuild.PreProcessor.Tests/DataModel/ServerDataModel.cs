//-----------------------------------------------------------------------
// <copyright file="ServerDataModel.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class ServerDataModel
    {
        private readonly IList<Repository> repos;
        private readonly IList<QualityProfile> qualityProfiles;

        public ServerDataModel()
        {
            this.repos = new List<Repository>();
            this.qualityProfiles = new List<QualityProfile>();
            this.ServerProperties = new Dictionary<string, string>();
            this.InstalledPlugins = new List<string>();
        }

        public IEnumerable<Repository> Repositories {  get { return this.repos; } }
        public IEnumerable<QualityProfile> QualityProfiles { get { return this.qualityProfiles; } }

        public IDictionary<string, string> ServerProperties { get; set; }

        public IList<string> InstalledPlugins { get; set; }

        #region Builder methods

        public Repository AddRepository(string repositoryKey, string language)
        {
            Repository repo = new Repository(repositoryKey, language);
            this.repos.Add(repo);
            return repo;
        }

        public QualityProfile AddQualityProfile(string name, string language)
        {
            QualityProfile profile = new QualityProfile(name, language);
            this.qualityProfiles.Add(profile);
            return profile;
        }

        public void AddRuleToProfile(string ruleId, string profileName)
        {
            // We're assuming rule ids are unique across repositories
            Rule rule = this.repos.SelectMany(repo => repo.Rules.Where(r => string.Equals(ruleId, r.Key))).Single();

            // Multiple profiles can have the same name; look for a profile where the language matches the rule language
            QualityProfile profile = this.qualityProfiles.Single(qp => string.Equals(qp.Name, profileName) && string.Equals(qp.Language, rule.Language));

            profile.AddRule(rule);
        }

        #endregion

    }
}
