using System;
using System.Collections.Generic;

namespace SonarScanner.MSBuild.PreProcessor
{
    internal class AutomaticBaseBranchDetection
    {
        private static readonly List<Tuple<string, string>> Candidates = new()
        {
            new Tuple<string, string>("Jenkins", "ghprbTargetBranch"),
            new Tuple<string, string>("Jenkins", "gitlabTargetBranch"),
            new Tuple<string, string>("Jenkins", "BITBUCKET_TARGET_BRANCH"),
            new Tuple<string, string>("GitHub Actions", "GITHUB_BASE_REF"),
            new Tuple<string, string>("GitLab", "CI_MERGE_REQUEST_TARGET_BRANCH_NAME"),
            new Tuple<string, string>("BitBucket Pipelines", "BITBUCKET_PR_DESTINATION_BRANCH"),
        };

        public static bool TryGetValue(out string branch, out string provider)
        {
            foreach (var candidate in Candidates)
            {
                var value = Environment.GetEnvironmentVariable(candidate.Item2);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    branch = value;
                    provider = candidate.Item1;
                    return true;
                }
            }

            branch = string.Empty;
            provider = string.Empty;
            return false;
        }
    }
}
