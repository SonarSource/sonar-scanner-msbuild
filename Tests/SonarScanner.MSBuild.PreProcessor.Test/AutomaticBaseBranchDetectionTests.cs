using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Equivalency;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class AutomaticBaseBranchDetectionTests
    {
        [DataTestMethod]
        [DataRow("Jenkins", "ghprbTargetBranch")]
        [DataRow("Jenkins", "gitlabTargetBranch")]
        [DataRow("Jenkins", "BITBUCKET_TARGET_BRANCH")]
        [DataRow("GitHub Actions", "GITHUB_BASE_REF")]
        [DataRow("GitLab", "CI_MERGE_REQUEST_TARGET_BRANCH_NAME")]
        [DataRow("BitBucket Pipelines", "BITBUCKET_PR_DESTINATION_BRANCH")]
        public void TryGetValue_Success(string expectedProvider, string variableName)
        {
            using var environment = new EnvironmentVariableScope();
            environment.SetVariable(variableName, "42");

            var result = AutomaticBaseBranchDetection.TryGetValue(out var branch, out var provider);

            result.Should().BeTrue();
            branch.Should().Be("42");
            provider.Should().Be(expectedProvider);
        }

        public void TryGetValue_Failure()
        {
            var result = AutomaticBaseBranchDetection.TryGetValue(out var branch, out var provider);

            result.Should().BeFalse();
            branch.Should().BeEmpty();
            provider.Should().BeEmpty();
        }
    }
}
