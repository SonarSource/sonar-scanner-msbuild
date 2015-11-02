//-----------------------------------------------------------------------
// <copyright file="Rule.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class Rule
    {
        private readonly string key;
        private readonly string internalKey;
        private readonly Repository repo;

        public Rule(string key, string internalKey, Repository repo)
        {
            this.key = key;
            this.internalKey = key;
            this.repo = repo; ;
        }

        public string Key { get { return this.key; } }
        public string InternalKey { get { return this.internalKey; } }
        public Repository Repository { get { return this.repo; } }
        public string Language { get { return this.repo.Language; } }
    }
}
