/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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
        context.Runtime.Logger.Should().HaveInfos("Using SonarQube v2026.1.");
    }

    [TestMethod]
    [DataRow("7.9.0.5545")]
    [DataRow("8.0.0.18670")]
    [DataRow("8.8.9.999")]
    [DataRow("9.9.9.999")]
    [DataRow("10.9.9.999")]
    [DataRow("2025.0.9.999")]   // Fake number, as there are no hardfail commercial editions with 2025.x version (yet)
    public void IsServerVersionSupported_FailHard_CommercialEdition(string sqVersion)
    {
        var context = new Context(sqVersion);
        context.Server.IsServerVersionSupported().Should().BeFalse();
        context.Runtime.Logger.Should().HaveErrors("SonarQube versions below 2025.1 are not supported anymore by the SonarScanner for .NET. Please upgrade your SonarQube version or use an older version of the scanner.");
    }

    [TestMethod]
    [DataRow("24.12.0.100206")]
    [DataRow("24.12.1.100206")]
    public void IsServerVersionSupported_FailHard_CommunityEdition(string sqVersion)
    {
        var context = new Context(sqVersion);
        context.Server.IsServerVersionSupported().Should().BeFalse();
        context.Runtime.Logger.Should().HaveErrors("SonarQube versions below 25.1 are not supported anymore by the SonarScanner for .NET. Please upgrade your SonarQube version or use an older version of the scanner.");
    }

    [TestMethod]
    [DataRow("25.1.0.1121")]
    [DataRow("25.12.0.9999")]
    [DataRow("2025.1.8.123366")]
    public void IsServerVersionSupported_OutOfSupport_LogWarning(string sqVersion)
    {
        var context = new Context(sqVersion);
        context.Server.IsServerVersionSupported().Should().BeTrue();
        context.Runtime.AnalysisWarnings.Should().HaveMessage("You're using an unsupported version of SonarQube. The next major version release of SonarScanner for .NET will not work with this version. Please upgrade to a newer SonarQube version.");
        context.Runtime.Logger.Should().HaveNoErrors();
    }

    [TestMethod]
    [DataRow("26.1.0.111")]
    [DataRow("27.1.0.111")]
    [DataRow("28.1.0.111")]
    [DataRow("2025.4.0.111")]
    [DataRow("2026.1.0.111")]
    [DataRow("2027.1.0.111")]
    [DataRow("2028.1.0.111")]
    public void IsServerVersionSupported_Supported_NoLogs(string sqVersion)
    {
        var context = new Context(sqVersion);
        context.Server.IsServerVersionSupported().Should().BeTrue();
        context.Runtime.AnalysisWarnings.Should().HaveNoMessages();
        context.Runtime.Logger.Should().HaveNoErrors();
    }

    [TestMethod]
    [DataRow("{ }")]
    [DataRow(@"{ ""isValidLicense"": false }")]
    public async Task IsServerLicenseValid_Commercial_AuthNotForced_LicenseIsInvalid(string responseContent)
    {
        var context = new Context();
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(responseContent) };
        context.WebDownloader.DownloadResource(Arg.Any<Uri>()).Returns(Task.FromResult(response));
        context.WebDownloader.BaseUrl.Returns(new Uri("host", UriKind.Relative));
        var isValid = await context.Server.IsServerLicenseValid();

        isValid.Should().BeFalse();
        context.Runtime.Logger.Should().HaveErrorOnce("Your SonarQube instance seems to have an invalid license. Please check it. Server url: host")
            .And.HaveNoWarnings();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_Commercial_AuthNotForced_LicenseIsValid()
    {
        var context = new Context();
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(@"{ ""isValidLicense"": true }") };
        context.WebDownloader.DownloadResource(Arg.Any<Uri>()).Returns(Task.FromResult(response));
        var isValid = await context.Server.IsServerLicenseValid();

        isValid.Should().BeTrue();
        context.Runtime.Logger.Should().HaveNoErrors()
            .And.HaveNoWarnings();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_Commercial_AuthForced_WithoutCredentials_ShouldReturnFalseAndLogError()
    {
        var context = new Context();
        context.WebDownloader.DownloadResource(new("api/editions/is_valid_license", UriKind.Relative)).Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized }));
        var result = await context.Server.IsServerLicenseValid();

        result.Should().BeFalse();
        context.Runtime.Logger.Should().HaveErrorOnce("Unauthorized: Access is denied due to invalid credentials. Please check the authentication parameters.")
            .And.HaveNoWarnings();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_ServerNotLicensed()
    {
        var context = new Context();
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound, Content = new StringContent(@"{""errors"":[{""msg"":""License not found""}]}") };
        context.WebDownloader.DownloadResource(Arg.Any<Uri>()).Returns(Task.FromResult(response));
        context.WebDownloader.BaseUrl.Returns(new Uri("host", UriKind.Relative));
        var result = await context.Server.IsServerLicenseValid();

        result.Should().BeFalse();
        context.Runtime.Logger.Should().HaveErrorOnce("Your SonarQube instance seems to have an invalid license. Please check it. Server url: host")
            .And.HaveNoWarnings();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_CE_SkipLicenseCheck()
    {
        var context = new Context();
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound, Content = new StringContent(@"{""errors"":[{""msg"":""Unknown url: /api/editions/is_valid_license""}]}") };
        context.WebDownloader.DownloadResource(Arg.Any<Uri>()).Returns(Task.FromResult(response));
        var result = await context.Server.IsServerLicenseValid();

        result.Should().BeTrue();
        context.Runtime.Logger.Should().HaveNoErrors()
            .And.HaveNoWarnings();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_RequestUrl()
    {
        var context = new Context();
        context.WebDownloader.DownloadResource(new("api/editions/is_valid_license", UriKind.Relative))
            .Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(@"{ ""isValidLicense"": true }") }));
        var isValid = await context.Server.IsServerLicenseValid();

        isValid.Should().BeTrue();
        await context.WebDownloader.Received().DownloadResource(new("api/editions/is_valid_license", UriKind.Relative));
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
        context.WebDownloader.TryDownloadIfExists(Arg.Any<Uri>(), Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
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
        var qualityProfileUrl = WebUtils.EscapedUri("api/qualityprofiles/search?project={0}", projectKey);
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
        context.WebDownloader.TryDownloadIfExists(new("api/qualityprofiles/search?project=someKey", UriKind.Relative), Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));

        // ToDo: This behavior is confusing, and not all the parsing errors should lead to this. See: https://sonarsource.atlassian.net/browse/SCAN4NET-578
        ((Func<string>)(() => context.Server.DownloadQualityProfile("someKey", null, "cs").Result))
            .Should()
            .ThrowExactly<AggregateException>()
            .WithInnerExceptionExactly<AnalysisException>()
            .WithMessage("It seems that you are using an old version of SonarQube which is not supported anymore. Please update to at least 6.7.");
    }

    [TestMethod]
    [DataRow(null, "api/settings/values?component=componentName")]
    [DataRow("someBranch", "api/settings/values?component=componentName%3AsomeBranch")]
    public void DownloadProperties(string projectBranch, string escapedUri)
    {
        var context = new Context();
        context.WebDownloader.TryDownloadIfExists(new(escapedUri, UriKind.Relative), Arg.Any<bool>())
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
        var result = context.Server.DownloadProperties("componentName", projectBranch).Result;

        result.Should().HaveCount(7);
        result["sonar.exclusions"].Should().Be("myfile,myfile2");
        result["sonar.junit.reportsPath"].Should().Be("testing.xml");
        result["sonar.issue.ignore.multicriteria.1.resourceKey"].Should().Be("prop1");
        result["sonar.issue.ignore.multicriteria.1.ruleKey"].Should().Be(string.Empty);
        result["sonar.issue.ignore.multicriteria.2.resourceKey"].Should().Be("prop2");
        result["sonar.issue.ignore.multicriteria.2.ruleKey"].Should().Be(string.Empty);
    }

    [TestMethod]
    public async Task DownloadProperties_NoComponentSettings_FallsBackToCommon()
    {
        var context = new Context();
        const string componentName = "nonexistent-component";
        context.WebDownloader.TryDownloadIfExists(new($"api/settings/values?component={componentName}", UriKind.Relative), Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(false, (string)null)));
        context.WebDownloader.Download(new("api/settings/values", UriKind.Relative), Arg.Any<bool>())
            .Returns(Task.FromResult(@"{ settings: [ { key: ""key"", value: ""42"" } ] }"));
        var result = await context.Server.DownloadProperties(componentName, null);

        result.Should().ContainSingle().And.ContainKey("key");
        result["key"].Should().Be("42");
    }

    [TestMethod]
    public async Task DownloadProperties_NullProjectKey_Throws() =>
        (await new Context().Server.Invoking(x => x.DownloadProperties(null, null)).Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("projectKey");

    [TestMethod]
    public async Task DownloadProperties_Sq63plus_Forbidden()
    {
        var context = new Context("6.3.0.0");
        context.WebDownloader.TryDownloadIfExists(Arg.Any<Uri>(), Arg.Any<bool>())
            .Returns(Task.FromException<Tuple<bool, string>>(new HttpRequestException("Forbidden")));

        await context.Server.Invoking(x => x.DownloadProperties(ProjectKey, null)).Should().ThrowAsync<HttpRequestException>();
    }

    [TestMethod]
    public async Task DownloadProperties_Empty()
    {
        var context = new Context();
        context.WebDownloader.TryDownloadIfExists(Arg.Any<Uri>(), Arg.Any<bool>()).Returns(Task.FromResult(Tuple.Create(true, "{ settings: [ ] }")));
        var properties = await context.Server.DownloadProperties("key", null);

        properties.Should().BeEmpty();
        await context.WebDownloader.Received().TryDownloadIfExists(new("api/settings/values?component=key", UriKind.Relative), true);
    }

    [TestMethod]
    public async Task DownloadCache_NullArgument() =>
        (await new Context().Server.Invoking(x => x.DownloadCache(null)).Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("localSettings");

    [TestMethod]
    [DataRow("", "", "Incremental PR analysis: ProjectKey parameter was not provided.")]
    [DataRow("BestProject", "", "Incremental PR analysis: Base branch parameter was not provided.")]
    public async Task DownloadCache_InvalidArguments(string projectKey, string branch, string debugMessage)
    {
        var context = new Context();
        var result = await context.Server.DownloadCache(CreateLocalSettings(projectKey, branch));

        result.Should().BeEmpty();
        context.Runtime.Logger.Should().HaveInfoOnce(debugMessage);
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

        context.Runtime.Logger.Should().HaveInfos($"Incremental PR analysis: Automatically detected base branch 'branch-42' from CI Provider '{provider}'.");
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

        context.Runtime.Logger.Should().HaveInfoOnce("Downloading cache. Project key: project-key, branch: project-branch.");
    }

    [TestMethod]
    public async Task DownloadCache_RequestUrl()
    {
        var context = new Context();
        using Stream stream = new MemoryStream();
        context.WebDownloader.DownloadStream(new("api/analysis_cache/get?project=project-key&branch=project-branch", UriKind.Relative)).Returns(Task.FromResult(stream));
        var result = await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch));

        result.Should().BeEmpty();
        await context.WebDownloader.Received().DownloadStream(new("api/analysis_cache/get?project=project-key&branch=project-branch", UriKind.Relative));
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
        context.Runtime.Logger.Should().HaveInfos("Downloading cache. Project key: project-key, branch: project-branch.");
    }

    [TestMethod]
    public async Task DownloadCache_WhenDownloadStreamReturnsNull_ReturnsEmpty()
    {
        var context = new Context();
        context.MockStreamWebDownload(null);
        var result = await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch));

        result.Should().BeEmpty();
        context.Runtime.Logger.Should().HaveNoWarnings()
            .And.HaveNoErrors(); // There are no errors or warnings logs but we will display an info message in the caller: "Cache data is empty. A full analysis will be performed."
    }

    [TestMethod]
    public async Task DownloadCache_WhenDownloadStreamReturnsEmpty_ReturnsEmpty()
    {
        var context = new Context();
        context.MockStreamWebDownload(new MemoryStream());
        var result = await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch));

        result.Should().BeEmpty();
        context.Runtime.Logger.Should().HaveNoDebugs();
    }

    [TestMethod]
    public async Task DownloadCache_WhenDownloadStreamThrows_ReturnsEmptyAndLogsException()
    {
        var context = new Context();
        context.WebDownloader.DownloadStream(Arg.Any<Uri>()).Returns(Task.FromException<Stream>(new HttpRequestException()));
        var result = await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch));

        result.Should().BeEmpty();
        context.Runtime.Logger.Should()
            .HaveWarningOnce("Incremental PR analysis: an error occurred while retrieving the cache entries! Exception of type 'System.Net.Http.HttpRequestException' was thrown.");
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
        context.Runtime.Logger.Should()
            .HaveWarningOnce("Incremental PR analysis: an error occurred while retrieving the cache entries! Operation is not valid due to the current state of the object.");
    }

    [TestMethod]
    public async Task DownloadCache_WhenCacheStreamDeserializeThrows_ReturnsEmptyAndLogsException()
    {
        var context = new Context();
        context.MockStreamWebDownload(new MemoryStream([42, 42])); // this is a random byte array that fails deserialization
        var result = await context.Server.DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch));

        result.Should().BeEmpty();
        context.Runtime.Logger.Should().HaveWarningOnce("Incremental PR analysis: an error occurred while retrieving the cache entries! While parsing a protocol message, the input ended unexpectedly in the middle of a field.  This could mean either that the input has been truncated or that an embedded message misreported its own length.");
    }

    [TestMethod]
    public async Task DownloadRules_SonarQubeVersion98()
    {
        var context = new Context("9.8");
        context.WebDownloader
            .Download(new("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1", UriKind.Relative))
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
            .Download(new("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1", UriKind.Relative))
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
                new("analysis/jres/someId", UriKind.Relative),
                Arg.Is<Dictionary<string, string>>(x => x.Single().Key == "Accept" && x.Single().Value == "application/octet-stream"))
            .Returns(Task.FromResult(expected));
        var actual = await context.Server.DownloadJreAsync(new JreMetadata("someId", null, null, null, null));

        ((MemoryStream)actual).ToArray().Should().BeEquivalentTo([1, 2, 3]);
        context.Runtime.Logger.Should().HaveDebugs("Downloading Java JRE from analysis/jres/someId.");
    }

    [TestMethod]
    public async Task DownloadJreAsync_DownloadThrows_Failure()
    {
        var context = new Context();
        context.ApiDownloader
            .DownloadStream(Arg.Any<Uri>(), Arg.Any<Dictionary<string, string>>())
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
                new("analysis/engine", UriKind.Relative),
                Arg.Is<Dictionary<string, string>>(x => x.Single().Key == "Accept" && x.Single().Value == "application/octet-stream"))
            .Returns(Task.FromResult(expected));
        var actual = await context.Server.DownloadEngineAsync(new EngineMetadata(null, null, null));

        ((MemoryStream)actual).ToArray().Should().BeEquivalentTo([1, 2, 3]);
        context.Runtime.Logger.Should().HaveDebugs("Downloading Scanner Engine from analysis/engine");
    }

    [TestMethod]
    public async Task DownloadEngineAsync_DownloadThrows_Failure()
    {
        var context = new Context();
        context.ApiDownloader
            .DownloadStream(Arg.Any<Uri>(), Arg.Any<Dictionary<string, string>>())
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
        public readonly TestRuntime Runtime = new();
        private readonly Lazy<SonarQubeWebServer> server;

        public SonarQubeWebServer Server => server.Value;

        public Context(string version = "2026.1", string organization = null)
        {
            server = new Lazy<SonarQubeWebServer>(() => new SonarQubeWebServer(WebDownloader, ApiDownloader, new(version), Runtime, organization));
        }

        public void MockStreamWebDownload(Stream stream) =>
            WebDownloader.DownloadStream(Arg.Any<Uri>()).Returns(Task.FromResult(stream));
    }
}
