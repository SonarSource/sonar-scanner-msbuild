using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn.Model
{
    public class ActiveRule
    {
        public string RepoKey { set; get; }
        public string RuleKey { set; get; }
        public string TemplateKey { set; get; }
        public string InternalKey { set; get; }
        public Dictionary<string, string> Parameters { set; get; }

        public string InternalKeyOrKey
        {
            get { return InternalKey ?? RuleKey; }
        }

        public ActiveRule()
        {

        }

        public ActiveRule(string repoKey, string ruleKey)
        {
            this.RepoKey = repoKey;
            this.RuleKey = ruleKey;
        }

        public ActiveRule(string repoKey, string ruleKey, string internalKey)
        {
            this.RepoKey = repoKey;
            this.RuleKey = ruleKey;
            this.InternalKey = InternalKey;
        }
    }
}
