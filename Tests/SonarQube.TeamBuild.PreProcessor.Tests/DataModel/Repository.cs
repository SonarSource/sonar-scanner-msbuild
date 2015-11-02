//-----------------------------------------------------------------------
// <copyright file="Repository.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class Repository
    {
        private readonly string key;
        private readonly string language;
        private readonly ISet<Rule> rules;

        public Repository(string key, string language)
        {
            this.key = key;
            this.language = language;
            this.rules = new HashSet<Rule>();
        }

        public Repository AddRule(string ruleKey, string internalKey)
        {
            Rule newRule = new Rule(ruleKey, internalKey, this);
            this.rules.Add(newRule);
            return this;
        }

        public string Key { get { return this.key; } }
        public string Language { get { return this.language; } }
        public IEnumerable<Rule> Rules { get { return this.rules; } }
    }

}
