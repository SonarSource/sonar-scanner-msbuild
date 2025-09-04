/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

using Google.Protobuf;
using NSubstitute.ExceptionExtensions;
using SonarScanner.MSBuild.PreProcessor.EngineResolution;
using SonarScanner.MSBuild.PreProcessor.JreResolution;
using SonarScanner.MSBuild.PreProcessor.Protobuf;
using SonarScanner.MSBuild.PreProcessor.WebServer;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class SonarQubeWebServerTest
{
    private const string ProjectKey = "project-key";
    private const string ProjectBranch = "project-branch";

    [TestMethod]
    public void Ctor_LogsServerTypeAndVersion()
    {
        var context = new Context();
        _ = context.Server;
        context.Logger.AssertInfoExists("Using SonarQube v9.9.");
    }

    [TestMethod]
    [DataRow("7.9.0.5545")]
    [DataRow("8.0.0.18670")]
    [DataRow("8.8.9.999")]
    public void IsServerVersionSupported_FailHard_LogError(string sqVersion)
    {
        var context = new Context(sqVersion);
        context.Server.IsServerVersionSupported().Should().BeFalse();
        context.Logger.AssertErrorLogged("SonarQube versions below 8.9 are not supported anymore by the SonarScanner for .NET. Please upgrade your SonarQube version to 8.9 or above or use an older version of the scanner (< 6.0.0), to be able to run the analysis.");
    }

    [TestMethod]
    [DataRow("8.9.0.0")]
    [DataRow("9.0.0.1121")]
    [DataRow("9.8.9.999")]
    [DataRow("9.9.0.0")]
    [DataRow("10.15.0.1121")]
    [DataRow("2024.12.0.100206")]
    public void IsServerVersionSupported_OutOfSupport_LogWarning(string sqVersion)
    {
        var context = new Context(sqVersion);
        context.Server.IsServerVersionSupported().Should().BeTrue();
        context.Logger.AssertUIWarningLogged("You're using an unsupported version of SonarQube. The next major version release of SonarScanner for .NET will not work with this version. Please upgrade to a newer SonarQube version.");
        context.Logger.AssertNoErrorsLogged();
    }

    [TestMethod]
    [DataRow("2025.1.0.0")]
    [DataRow("2025.1.0.102122")]
    [DataRow("2026.1.0.0")]
    public void IsServerVersionSupported_Supported_NoLogs(string sqVersion)
    {
        var context = new Context(sqVersion);
        context.Server.IsServerVersionSupported().Should().BeTrue();
        context.Logger.AssertNoUIWarningsLogged();
        context.Logger.AssertNoErrorsLogged();
    }

    [TestMethod]
    [DataRow("{ }")]
    [DataRow(@"{ ""isValidLicense"": false }")]
    public async Task IsServerLicenseValid_Commercial_AuthNotForced_LicenseIsInvalid(string responseContent)
    {
        var context = new Context();
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(responseContent) };
        context.WebDownloader.DownloadResource(Arg.Any<string>()).Returns(Task.FromResult(response));
        context.WebDownloader.GetBaseUrl().Returns("host");
        var isValid = await context.Server.IsServerLicenseValid();

        isValid.Should().BeFalse();
        context.Logger.AssertSingleErrorExists("Your SonarQube instance seems to have an invalid license. Please check it. Server url: host");
        context.Logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_Commercial_AuthNotForced_LicenseIsValid()
    {
        var context = new Context();
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(@"{ ""isValidLicense"": true }") };
        context.WebDownloader.DownloadResource(Arg.Any<string>()).Returns(Task.FromResult(response));
        var isValid = await context.Server.IsServerLicenseValid();

        isValid.Should().BeTrue();
        context.Logger.AssertNoErrorsLogged();
        context.Logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_Commercial_AuthForced_WithoutCredentials_ShouldReturnFalseAndLogError()
    {
        var context = new Context();
        context.WebDownloader.DownloadResource("api/editions/is_valid_license").Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized }));
        var result = await context.Server.IsServerLicenseValid();

        result.Should().BeFalse();
        context.Logger.AssertSingleErrorExists("Unauthorized: Access is denied due to invalid credentials. Please check the authentication parameters.");
        context.Logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_ServerNotLicensed()
    {
        var context = new Context();
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound, Content = new StringContent(@"{""errors"":[{""msg"":""License not found""}]}") };
        context.WebDownloader.DownloadResource(Arg.Any<string>()).Returns(Task.FromResult(response));
        context.WebDownloader.GetBaseUrl().Returns("host");
        var result = await context.Server.IsServerLicenseValid();

        result.Should().BeFalse();
        context.Logger.AssertSingleErrorExists("Your SonarQube instance seems to have an invalid license. Please check it. Server url: host");
        context.Logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_CE_SkipLicenseCheck()
    {
        var context = new Context();
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound, Content = new StringContent(@"{""errors"":[{""msg"":""Unknown url: /api/editions/is_valid_license""}]}") };
        context.WebDownloader.DownloadResource(Arg.Any<string>()).Returns(Task.FromResult(response));
        var result = await context.Server.IsServerLicenseValid();

        result.Should().BeTrue();
        context.Logger.AssertNoErrorsLogged();
        context.Logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_RequestUrl()
    {
        var context = new Context();
        context.WebDownloader.DownloadResource("api/editions/is_valid_license")
            .Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(@"{ ""isValidLicense"": true }") }));
        var isValid = await context.Server.IsServerLicenseValid();

        isValid.Should().BeTrue();
        await context.WebDownloader.Received().DownloadResource("api/editions/is_valid_license");
    }

    [TestMethod]
    [DataRow("7.9.0.5545", false)]
    [DataRow("8.0.0.18670", false)]
    [DataRow("8.8.0.1121", false)]
    [DataRow("8.9.0.0", false)]
    [DataRow("9.0.0.1121", false)]
    [DataRow("10.5.1.90531", false)]
    [DataRow("10.6.0.92166", true)] // First version with JRE provisioning
    [DataRow("10.15.0.1121", true)]
    public void SupportsJreProvisioningVersionSupported(string sqVersion, bool expected) =>
        new Context(sqVersion).Server.SupportsJreProvisioning.Should().Be(expected);

    [TestMethod]
    [DataRow("someKey", "my org")]
    public async Task DownloadQualityProfile_OrganizationProfile_QualityProfileUrlContainsOrganization(string projectKey, string organization)
    {
        var context = new Context(organization: "organization");
        const string profileKey = "orgProfile";
        const string language = "cs";
        var downloadResult = Tuple.Create(true, $$"""{ profiles: [{"key":"{{profileKey}}","name":"profile1","language":"{{language}}"}]}""");
        context.WebDownloader.TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
        var result = await context.Server.DownloadQualityProfile(projectKey, null, language);

        result.Should().Be(profileKey);
    }

    [TestMethod]
    [DataRow("someKey", "my org")]
    public async Task DownloadQualityProfile_SQ62OrganizationProfile_QualityProfileUrlDoesNotContainsOrganization(string projectKey, string organization)
    {
        const string profileKey = "orgProfile";
        const string language = "cs";
        var context = new Context("6.2", organization);
        var qualityProfileUrl = $"api/qualityprofiles/search?project={WebUtility.UrlEncode($"{projectKey}")}";
        var downloadResult = Tuple.Create(true, $$"""{ profiles: [{"key":"{{profileKey}}","name":"profile1","language":"{{language}}"}]}""");
        context.WebDownloader.TryDownloadIfExists(qualityProfileUrl, Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
        var result = await context.Server.DownloadQualityProfile(projectKey, null, language);

        result.Should().Be(profileKey);
    }

    [TestMethod]
    public void DownloadQualityProfile_MultipleQPForSameLanguage_ShouldThrow()
    {
        var context = new Context();
        var downloadResult = Tuple.Create(true, """
            { profiles: [
                {"key":"profile1k","name":"profile1","language":"cs", "isDefault": false},
                {"key":"profile4k","name":"profile4","language":"cs", "isDefault": true}
                ]}
            """);
        context.WebDownloader.TryDownloadIfExists("api/qualityprofiles/search?project=someKey", Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));

        // ToDo: This behavior is confusing, and not all the parsing errors should lead to this. See: https://sonarsource.atlassian.net/browse/SCAN4NET-578
        ((Func<string>)(() => context.Server.DownloadQualityProfile("someKey", null, "cs").Result))
            .Should()
            .ThrowExactly<AggregateException>()
            .WithInnerExceptionExactly<AnalysisException>()
            .WithMessage("It seems that you are using an old version of SonarQube which is not supported anymore. Please update to at least 6.7.");
    }

    [TestMethod]
    public void DownloadProperties_Sq63()
    {
        var context = new Context("6.3");
        context.WebDownloader.TryDownloadIfExists("api/settings/values?component=comp", Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(true, """
                {settings: [
                    {
                        key: "sonar.core.id",
                        value: "AVrrKaIfChAsLlov22f0",
                        inherited: true
                    },
                    {
                        key: "sonar.exclusions",
                        values: [ "myfile", "myfile2" ]
                    },
                    {
                        key: "sonar.junit.reportsPath",
                        value: "testing.xml"
                    },
                    {
                        key: "sonar.issue.ignore.multicriteria",
                        fieldValues: [
                            {
                                resourceKey: "prop1",
                                ruleKey: ""
                            },
                            {
                                resourceKey: "prop2",
                                ruleKey: ""
                            }]
                    }]
                }
                """)));
        var result = context.Server.DownloadProperties("comp", null).Result;

        result.Should().HaveCount(7);
        result["sonar.exclusions"].Should().Be("myfile,myfile2");
        result["sonar.junit.reportsPath"].Should().Be("testing.xml");
        result["sonar.issue.ignore.multicriteria.1.resourceKey"].Should().Be("prop1");
        result["sonar.issue.ignore.multicriteria.1.ruleKey"].Should().Be(string.Empty);
        result["sonar.issue.ignore.multicriteria.2.resourceKey"].Should().Be("prop2");
        result["sonar.issue.ignore.multicriteria.2.ruleKey"].Should().Be(string.Empty);
    }

    [TestMethod]
    public async Task DownloadProperties_Sq63_NoComponentSettings_FallsBackToCommon()
    {
        var context = new Context("6.3");
        const string componentName = "nonexistent-component";
        context.WebDownloader.TryDownloadIfExists($"api/settings/values?component={componentName}", Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(false, (string)null)));
        context.WebDownloader.Download("api/settings/values", Arg.Any<bool>())
            .Returns(Task.FromResult(@"{ settings: [ { key: ""key"", value: ""42"" } ] }"));
        var result = await context.Server.DownloadProperties(componentName, null);

        result.Should().ContainSingle().And.ContainKey("key");
        result["key"].Should().Be("42");
    }

    [TestMethod]
    public async Task DownloadProperties_Sq63_MissingValue_Throws()
    {
        var context = new Context("6.3");
        const string componentName = "nonexistent-component";
        context.WebDownloader.TryDownloadIfExists($"api/settings/values?component={componentName}", Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(false, (string)null)));
        context.WebDownloader.Download("api/settings/values", Arg.Any<bool>())
            .Returns(Task.FromResult(@"{ settings: [ { key: ""key"" } ] }"));

        await context.Server.Invoking(async x => await x.DownloadProperties(componentName, null))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid property");
    }

    [TestMethod]
    public async Task DownloadProperties_NullProjectKey_Throws() =>
        (await new Context().Server.Invoking(x => x.DownloadProperties(null, null)).Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("projectKey");

    [TestMethod]
    public async Task DownloadProperties_ProjectWithBranch_SuccessfullyRetrieveProperties()
    {
        var context = new Context("5.6");
        context.WebDownloader.Download("api/properties?resource=someKey%3AaBranch", Arg.Any<bool>())
            .Returns(Task.FromResult("""[{"key": "sonar.property1","value": "anotherValue1"},{"key": "sonar.property2","value": "anotherValue2"}]"""));
        var result = await context.Server.DownloadProperties("someKey", "aBranch");

        result.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["sonar.property1"] = "anotherValue1",
            ["sonar.property2"] = "anotherValue2"
        });
        await context.WebDownloader.Received().Download("api/properties?resource=someKey%3AaBranch", Arg.Any<bool>());
    }

    [TestMethod]
    public async Task DownloadProperties_ProjectWithoutBranch_SuccessfullyRetrieveProperties()
    {
        var context = new Context("5.6");
        context.WebDownloader.Download("api/properties?resource=someKey", Arg.Any<bool>())
            .Returns(Task.FromResult("""[{"key": "sonar.property1","value": "anotherValue1"},{"key": "sonar.property2","value": "anotherValue2"}]"""));
        var result = await context.Server.DownloadProperties("someKey", null);

        result.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["sonar.property1"] = "anotherValue1",
            ["sonar.property2"] = "anotherValue2"
        });
        await context.WebDownloader.Received().Download("api/properties?resource=someKey", Arg.Any<bool>());
    }

    [TestMethod]
    public async Task DownloadProperties_Old_Forbidden()
    {
        var context = new Context("1.2.3.4");
        context.WebDownloader.Download($"api/properties?resource={ProjectKey}", Arg.Any<bool>())
            .Returns(Task.FromException<string>(new HttpRequestException("Forbidden")));
        Func<Task> action = async () => await context.Server.DownloadProperties(ProjectKey, null);

        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [TestMethod]
    public async Task DownloadProperties_Sq63plus_Forbidden()
    {
        var context = new Context("6.3.0.0");
        context.WebDownloader.TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(Task.FromException<Tuple<bool, string>>(new HttpRequestException("Forbidden")));

        await context.Server.Invoking(x => x.DownloadProperties(ProjectKey, null)).Should().ThrowAsync<HttpRequestException>();
    }

    [TestMethod]
    public async Task DownloadProperties_SQ63AndHigherWithProject_ShouldBeEmpty()
    {
        var context = new Context("6.3");
        context.WebDownloader.TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(Tuple.Create(true, "{ settings: [ ] }")));
        var properties = await context.Server.DownloadProperties("key", null);

        properties.Should().BeEmpty();
        await context.WebDownloader.Received().TryDownloadIfExists("api/settings/values?component=key", true);
    }

    [TestMethod]
    public async Task DownloadProperties_OlderThanSQ63_ShouldBeEmpty()
    {
        var context = new Context("6.2.9");
        context.WebDownloader.Download(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult("[]"));
        var properties = await context.Server.DownloadProperties("key", null);

        properties.Should().BeEmpty();
        await context.WebDownloader.Received().Download(Arg.Any<string>(), Arg.Any<bool>());
    }

    [TestMethod]
    public async Task DownloadProperties_SQ63AndHigherWithoutProject_ShouldBeEmpty()
    {
        var context = new Context("6.3");
        context.WebDownloader.TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(Tuple.Create(false, (string)null)));
        context.WebDownloader.Download(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult("{ settings: [ ] }"));
        var properties = await context.Server.DownloadProperties("key", null);

        properties.Should().BeEmpty();
        await context.WebDownloader.Received().TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>());
        await context.WebDownloader.Received().Download(Arg.Any<string>(), Arg.Any<bool>());
    }

    [TestMethod]
    public async Task DownloadCache_NullArgument() =>
        (await new Context().Server.Invoking(x => x.DownloadCache(null)).Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("localSettings");

    [TestMethod]
    [DataRow("9.8", "", "", "Incremental PR analysis is available starting with SonarQube 9.9 or later.")]
    [DataRow("9.9", "", "", "Incremental PR analysis: ProjectKey parameter was not provided.")]
    [DataRow("9.9", "BestProject", "", "Incremental PR analysis: Base branch parameter was not provided.")]
    public async Task DownloadCache_InvalidArguments(string version, string projectKey, string branch, string debugMessage)
    {
        var context = new Context(version);
        var result = await context.Server.DownloadCache(CreateLocalSettings(projectKey, branch));

        result.Should().BeEmpty();
        context.Logger.AssertSingleInfoExists(debugMessage);
    }

    [TestMethod]
    [DataRow("Jenkins", "ghprbTargetBranch")]
    [DataRow("Jenkins", "gitlabTargetBranch")]
    [DataRow("Jenkins", "BITBUCKET_TARGET_BRANCH")]
    [DataRow("GitHub Actions", "GITHUB_BASE_REF")]
    [DataRow("GitLab", "CI_MERGE_REQUEST_TARGET_BRANCH_NAME")]
    [DataRow("BitBucket Pipelines", "BITBUCKET_PR_DESTINATION_BRANCH")]
    public async Task DownloadCache_AutomaticallyDeduceBaseBranch(string provider, string variableName)
    {
        var context = new Context();
        using var environment = new EnvironmentVariableScope().SetVariable(variableName, "branch-42");
        context.MockStreamWebDownload(new MemoryStream());
        await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, null));

        context.Logger.AssertInfoExists($"Incremental PR analysis: Automatically detected base branch 'branch-42' from CI Provider '{provider}'.");
    }

    [TestMethod]
    [DataRow("ghprbTargetBranch")]
    [DataRow("gitlabTargetBranch")]
    [DataRow("BITBUCKET_TARGET_BRANCH")]
    [DataRow("GITHUB_BASE_REF")]
    [DataRow("CI_MERGE_REQUEST_TARGET_BRANCH_NAME")]
    [DataRow("BITBUCKET_PR_DESTINATION_BRANCH")]
    public async Task DownloadCache_UserInputSupersedesAutomaticDetection(string variableName)
    {
        var context = new Context();
        using var environment = new EnvironmentVariableScope().SetVariable(variableName, "wrong_branch");
        context.MockStreamWebDownload(new MemoryStream());
        await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch));

        context.Logger.AssertSingleInfoExists("Downloading cache. Project key: project-key, branch: project-branch.");
    }

    [TestMethod]
    public async Task DownloadCache_RequestUrl()
    {
        var context = new Context();
        using Stream stream = new MemoryStream();
        context.WebDownloader.DownloadStream("api/analysis_cache/get?project=project-key&branch=project-branch").Returns(Task.FromResult(stream));
        var result = await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch));

        result.Should().BeEmpty();
        await context.WebDownloader.Received().DownloadStream("api/analysis_cache/get?project=project-key&branch=project-branch");
    }

    [TestMethod]
    public async Task DownloadCache_DeserializesMessage()
    {
        var context = new Context();
        using var stream = CreateCacheStream(new SensorCacheEntry { Key = "key", Data = ByteString.CopyFromUtf8("value") });
        context.MockStreamWebDownload(stream);
        var result = await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch));

        result.Should().ContainSingle();
        result.Single(x => x.Key == "key").Data.ToStringUtf8().Should().Be("value");
        context.Logger.AssertInfoLogged("Downloading cache. Project key: project-key, branch: project-branch.");
    }

    [TestMethod]
    public async Task DownloadCache_WhenDownloadStreamReturnsNull_ReturnsEmpty()
    {
        var context = new Context();
        context.MockStreamWebDownload(null);
        var result = await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch));

        result.Should().BeEmpty();
        context.Logger.AssertNoWarningsLogged();
        context.Logger.AssertNoErrorsLogged(); // There are no errors or warnings logs but we will display an info message in the caller: "Cache data is empty. A full analysis will be performed."
    }

    [TestMethod]
    public async Task DownloadCache_WhenDownloadStreamReturnsEmpty_ReturnsEmpty()
    {
        var context = new Context();
        context.MockStreamWebDownload(new MemoryStream());
        var result = await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch));

        result.Should().BeEmpty();
        context.Logger.DebugMessages.Should().BeEmpty();
    }

    [TestMethod]
    public async Task DownloadCache_WhenDownloadStreamThrows_ReturnsEmptyAndLogsException()
    {
        var context = new Context();
        context.WebDownloader.DownloadStream(Arg.Any<string>()).Returns(Task.FromException<Stream>(new HttpRequestException()));
        var result = await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch));

        result.Should().BeEmpty();
        context.Logger.AssertSingleWarningExists("Incremental PR analysis: an error occurred while retrieving the cache entries! Exception of type 'System.Net.Http.HttpRequestException' was thrown.");
    }

    [TestMethod]
    public async Task DownloadCache_WhenCacheStreamReadThrows_ReturnsEmptyAndLogsException()
    {
        var context = new Context();
        var stream = Substitute.For<Stream>();
        stream.Length.Returns(x => throw new InvalidOperationException());
        context.MockStreamWebDownload(stream);
        var result = await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch));

        result.Should().BeEmpty();
        context.Logger.AssertSingleWarningExists("Incremental PR analysis: an error occurred while retrieving the cache entries! Operation is not valid due to the current state of the object.");
    }

    [TestMethod]
    public async Task DownloadCache_WhenCacheStreamDeserializeThrows_ReturnsEmptyAndLogsException()
    {
        var context = new Context();
        context.MockStreamWebDownload(new MemoryStream([42, 42])); // this is a random byte array that fails deserialization
        var result = await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch));

        result.Should().BeEmpty();
        context.Logger.AssertSingleWarningExists("Incremental PR analysis: an error occurred while retrieving the cache entries! While parsing a protocol message, the input ended unexpectedly in the middle of a field.  This could mean either that the input has been truncated or that an embedded message misreported its own length.");
    }

    [TestMethod]
    public async Task DownloadRules_SonarQubeVersion98()
    {
        var context = new Context("9.8");
        context.WebDownloader
            .Download("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1")
            .Returns("""
                {
                    paging: {
                        total: 3,
                        pageIndex: 1,
                        pageSize: 500
                    },
                    rules: [
                        {
                            "key": "csharpsquid:S2757",
                            "repo": "csharpsquid",
                            "type": "BUG"
                    }]
                }
                """);
        var rules = await context.Server.DownloadRules("qp");

        rules.Should().ContainSingle();
        rules[0].RepoKey.Should().Be("csharpsquid");
        rules[0].RuleKey.Should().Be("S2757");
        rules[0].InternalKeyOrKey.Should().Be("S2757");
        rules[0].Parameters.Should().BeNull();
        rules[0].IsActive.Should().BeFalse();
    }

    [TestMethod]
    public async Task DownloadRules_SonarQubeVersion89()
    {
        var context = new Context("8.9");
        context.WebDownloader
            .Download("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1")
            .Returns("""
                {
                    "total": 3,
                    "p": 1,
                    "ps": 500,
                    "rules": [
                        {
                            "key": "csharpsquid:S2757",
                            "repo": "csharpsquid",
                            "type": "BUG"
                        }]
                }
                """);
        var rules = await context.Server.DownloadRules("qp");

        rules.Should().ContainSingle();
        rules[0].RepoKey.Should().Be("csharpsquid");
        rules[0].RuleKey.Should().Be("S2757");
        rules[0].InternalKeyOrKey.Should().Be("S2757");
        rules[0].Parameters.Should().BeNull();
        rules[0].IsActive.Should().BeFalse();
    }

    [TestMethod]
    public async Task DownloadJreAsync_Success()
    {
        var context = new Context();
        Stream expected = new MemoryStream([1, 2, 3]);
        context.ApiDownloader
            .DownloadStream(
                "analysis/jres/someId",
                Arg.Is<Dictionary<string, string>>(x => x.Single().Key == "Accept" && x.Single().Value == "application/octet-stream"))
            .Returns(Task.FromResult(expected));
        var actual = await context.Server.DownloadJreAsync(new JreMetadata("someId", null, null, null, null));

        ((MemoryStream)actual).ToArray().Should().BeEquivalentTo([1, 2, 3]);
        context.Logger.AssertDebugLogged("Downloading Java JRE from analysis/jres/someId.");
    }

    [TestMethod]
    public async Task DownloadJreAsync_DownloadThrows_Failure()
    {
        var context = new Context();
        context.ApiDownloader
            .DownloadStream(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .ThrowsAsync<HttpRequestException>();
        await context.Server.Invoking(async x => await x.DownloadJreAsync(new(null, null, null, null, null)))
            .Should().ThrowAsync<HttpRequestException>();
    }

    [TestMethod]
    public async Task DownloadJreAsync_NullMetadata_Failure() =>
        await new Context().Server.Invoking(async x => await x.DownloadJreAsync(null)).Should().ThrowAsync<NullReferenceException>();

    [TestMethod]
    public async Task DownloadEngineAsync_Success()
    {
        var context = new Context();
        Stream expected = new MemoryStream([1, 2, 3]);
        context.ApiDownloader
            .DownloadStream(
                "analysis/engine",
                Arg.Is<Dictionary<string, string>>(x => x.Single().Key == "Accept" && x.Single().Value == "application/octet-stream"))
            .Returns(Task.FromResult(expected));
        var actual = await context.Server.DownloadEngineAsync(new EngineMetadata(null, null, null));

        ((MemoryStream)actual).ToArray().Should().BeEquivalentTo([1, 2, 3]);
        context.Logger.AssertDebugLogged("Downloading Scanner Engine from analysis/engine");
    }

    [TestMethod]
    public async Task DownloadEngineAsync_DownloadThrows_Failure()
    {
        var context = new Context();
        context.ApiDownloader
            .DownloadStream(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .ThrowsAsync<HttpRequestException>();
        await context.Server.Invoking(async x => await x.DownloadEngineAsync(new EngineMetadata(null, null, null)))
            .Should().ThrowAsync<HttpRequestException>();
    }

    private static MemoryStream CreateCacheStream(IMessage message)
    {
        var stream = new MemoryStream();
        message.WriteDelimitedTo(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private static ProcessedArgs CreateLocalSettings(string projectKey, string branch, string organization = "placeholder", string token = "placeholder")
    {
        var args = Substitute.For<ProcessedArgs>();
        args.ProjectKey.Returns(projectKey);
        args.Organization.Returns(organization);
        args.TryGetSetting(SonarProperties.PullRequestBase, out Arg.Any<string>()).Returns(x =>
            {
                x[1] = branch;
                return !string.IsNullOrWhiteSpace(branch);
            });
        args.TryGetSetting(SonarProperties.SonarUserName, out Arg.Any<string>()).Returns(x =>
            {
                x[1] = token;
                return !string.IsNullOrWhiteSpace(token);
            });
        return args;
    }

    private sealed class Context
    {
        public readonly IDownloader WebDownloader = Substitute.For<IDownloader>();
        public readonly IDownloader ApiDownloader = Substitute.For<IDownloader>();
        public readonly TestLogger Logger = new();
        private readonly Lazy<SonarQubeWebServer> server;

        public SonarQubeWebServer Server => server.Value;

        public Context(string version = "9.9", string organization = null)
        {
            server = new Lazy<SonarQubeWebServer>(() => new SonarQubeWebServer(WebDownloader, ApiDownloader, new(version), Logger, organization));
        }

        public void MockStreamWebDownload(Stream stream) =>
            WebDownloader.DownloadStream(Arg.Any<string>()).Returns(Task.FromResult(stream));
    }
}
