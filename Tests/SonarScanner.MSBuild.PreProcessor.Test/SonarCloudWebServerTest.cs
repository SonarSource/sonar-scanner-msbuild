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

using System.IO;
using System.IO.Compression;
using Google.Protobuf;
using SonarScanner.MSBuild.PreProcessor.EngineResolution;
using SonarScanner.MSBuild.PreProcessor.JreResolution;
using SonarScanner.MSBuild.PreProcessor.Protobuf;
using SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;
using SonarScanner.MSBuild.PreProcessor.WebServer;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class SonarCloudWebServerTest
{
    private const string ProjectKey = "project-key";
    private const string ProjectBranch = "project-branch";
    private const string Token = "42";
    private const string Organization = "org42";
    private const string CacheBaseUrl = "https://www.cacheBaseUrl.com";
    private const string CacheFullUrl = $"{CacheBaseUrl}/sensor-cache/prepare-read?organization={Organization}&project={ProjectKey}&branch={ProjectBranch}";
    private static readonly Version Version = new("5.6");
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(42);

    [TestMethod]
    public void Ctor_OrganizationNull_ShouldThrow() =>
        ((Func<SonarCloudWebServer>)(() => new SonarCloudWebServer(Substitute.For<IDownloader>(), Substitute.For<IDownloader>(), Version, new TestLogger(), null, HttpTimeout)))
            .Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("organization");

    [TestMethod]
    public void Ctor_LogsServerType()
    {
        var logger = new TestLogger();
        _ = new SonarCloudWebServer(Substitute.For<IDownloader>(), Substitute.For<IDownloader>(), Version, logger, Organization, HttpTimeout);
        logger.AssertInfoMessageExists("Using SonarCloud.");
    }

    [TestMethod]
    public void IsServerVersionSupported_IsSonarCloud_ShouldReturnTrue() =>
        new Context().Server.IsServerVersionSupported().Should().BeTrue();

    [TestMethod]
    public async Task IsLicenseValid_IsSonarCloud_ShouldReturnTrue() =>
        (await new Context().Server.IsServerLicenseValid()).Should().BeTrue();

    [TestMethod]
    public async Task IsLicenseValid_AlwaysValid()
    {
        var context = new Context();
        context.WebDownloader
            .Download("api/editions/is_valid_license")
            .Returns("""{ "isValidLicense": false }""");

        (await context.Server.IsServerLicenseValid()).Should().BeTrue();
    }

    [TestMethod]
    public async Task DownloadProperties_Success()
    {
        var context = new Context();
        context.WebDownloader
            .TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(true, """
                {
                  settings: [
                    {
                      key: "sonar.core.id",
                      value: "AVrrKaIfChAsLlov22f0",
                      inherited: true
                    },
                    {
                      key: "sonar.exclusions",
                      values: [
                        "myfile",
                        "myfile2" ]
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

        var result = await context.Server.DownloadProperties("comp", null);

        result.Should().HaveCount(7);
        result["sonar.exclusions"].Should().Be("myfile,myfile2");
        result["sonar.junit.reportsPath"].Should().Be("testing.xml");
        result["sonar.issue.ignore.multicriteria.1.resourceKey"].Should().Be("prop1");
        result["sonar.issue.ignore.multicriteria.1.ruleKey"].Should().Be(string.Empty);
        result["sonar.issue.ignore.multicriteria.2.resourceKey"].Should().Be("prop2");
        result["sonar.issue.ignore.multicriteria.2.ruleKey"].Should().Be(string.Empty);
    }

    [TestMethod]
    public void DownloadProperties_NullProjectKey_Throws() =>
        ((Action)(() => new Context().Server.DownloadProperties(null, null).GetAwaiter().GetResult())).Should()
            .Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("projectKey");

    [TestMethod]
    public async Task IsServerLicenseValid_AlwaysTrue()
    {
        var context = new Context();
        var isValid = await context.Server.IsServerLicenseValid();

        isValid.Should().BeTrue();
        context.Logger.AssertDebugMessageExists("SonarCloud detected, skipping license check.");
    }

    [TestMethod]
    public async Task DownloadCache_NullArgument() =>
        (await new Context().Server.Invoking(x => x.DownloadCache(null)).Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("localSettings");

    [TestMethod]
    [DataRow("", "", "", "Incremental PR analysis: ProjectKey parameter was not provided.")]
    [DataRow("project", "", "", "Incremental PR analysis: Base branch parameter was not provided.")]
    [DataRow("project", "branch", "", "Incremental PR analysis: Token parameter was not provided.")]
    [DataRow("project", "branch", "token", "Incremental PR analysis: CacheBaseUrl was not successfully retrieved.")]
    public async Task DownloadCache_InvalidArguments(string projectKey, string branch, string token, string infoMessage)
    {
        var context = new Context();
        var res = await context.Server
            .DownloadCache(CreateLocalSettings(projectKey, branch, Organization, token));

        res.Should().BeEmpty();
        context.Logger.AssertSingleInfoMessageExists(infoMessage);
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
        using var environment = new EnvironmentVariableScope().SetVariable(variableName, "branch-42");
        using var stream = new MemoryStream();
        var handler = MockHttpHandler(CacheFullUrl, "https://www.ephemeralUrl.com", stream);
        var context = new Context(handler, CacheBaseUrl);

        await context.Server
            .DownloadCache(CreateLocalSettings(ProjectKey, null, Organization, Token));

        context.Logger.AssertInfoMessageExists($"Incremental PR analysis: Automatically detected base branch 'branch-42' from CI Provider '{provider}'.");
        handler.Requests.Should().NotBeEmpty();
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
        using var environment = new EnvironmentVariableScope().SetVariable(variableName, "wrong-branch");
        using var stream = new MemoryStream();
        var handler = MockHttpHandler(CacheFullUrl, "https://www.ephemeralUrl.com", stream);
        var context = new Context(handler, CacheBaseUrl);

        await context.Server
            .DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch, Organization, Token));

        context.Logger.AssertSingleInfoMessageExists("Downloading cache. Project key: project-key, branch: project-branch.");
        handler.Requests.Should().NotBeEmpty();
    }

    [TestMethod]
    [DataRow("http://cacheBaseUrl:222", "http://cachebaseurl:222/sensor-cache/prepare-read?organization=org42&project=project-key&branch=project-branch")]
    [DataRow("http://cacheBaseUrl:222/", "http://cachebaseurl:222/sensor-cache/prepare-read?organization=org42&project=project-key&branch=project-branch")]
    [DataRow("http://cacheBaseUrl:222/sonar/", "http://cachebaseurl:222/sonar/sensor-cache/prepare-read?organization=org42&project=project-key&branch=project-branch")]
    public async Task DownloadCache_RequestUrl(string cacheBaseUrl, string cacheFullUrl)
    {
        using var stream = new MemoryStream();
        var handler = MockHttpHandler(cacheFullUrl, "https://www.ephemeralUrl.com", stream);
        var context = new Context(handler, cacheBaseUrl);

        var result = await context.Server
            .DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch, Organization, Token));

        result.Should().BeEmpty();
        context.Logger.AssertSingleDebugMessageExists($"Incremental PR Analysis: Requesting 'prepare_read' from {cacheFullUrl}");
        handler.Requests.Should().NotBeEmpty();
    }

    [TestMethod]
    [DataRow(SonarProperties.SonarUserName)]
    [DataRow(SonarProperties.SonarToken)]
    public async Task DownloadCache_CacheHit(string tokenKey)
    {
        using var stream = CreateCacheStream(new SensorCacheEntry { Key = "key", Data = ByteString.CopyFromUtf8("value") });
        var handler = MockHttpHandler(CacheFullUrl, "https://www.ephemeralUrl.com", stream);
        var context = new Context(handler, CacheBaseUrl);

        var result = await context.Server
            .DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch, Organization, Token, tokenKey));

        result.Should().ContainSingle();
        result.Single(x => x.Key == "key").Data.ToStringUtf8().Should().Be("value");
        context.Logger.AssertInfoLogged("Downloading cache. Project key: project-key, branch: project-branch.");
        handler.Requests.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task DownloadCache_PrepareRead_UnsuccessfulResponse()
    {
        var handler = MockHttpHandler(CacheFullUrl, "irrelevant", HttpStatusCode.Forbidden);
        var context = new Context(handler, CacheBaseUrl);

        var result = await context.Server
            .DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch, Organization, Token));

        result.Should().BeEmpty();
        context.Logger.AssertSingleDebugMessageExists("Incremental PR analysis: an error occurred while retrieving the cache entries! 'prepare_read' did not respond successfully.");
        handler.Requests.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task DownloadCache_PrepareRead_EmptyResponse()
    {
        var handler = MockHttpHandler(CacheFullUrl, string.Empty);
        var context = new Context(handler, CacheBaseUrl);

        var result = await context.Server
            .DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch, Organization, Token));

        result.Should().BeEmpty();
        context.Logger.AssertSingleDebugMessageExists("Incremental PR analysis: an error occurred while retrieving the cache entries! 'prepare_read' response was empty.");
        handler.Requests.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task DownloadCache_PrepareRead_CacheDisabled()
    {
        var handler = MockHttpHandler(CacheFullUrl, @"{ ""enabled"": ""false"", ""url"":""https://www.sonarsource.com"" }");
        var context = new Context(handler, CacheBaseUrl);

        var result = await context.Server
            .DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch, Organization, Token));

        result.Should().BeEmpty();
        context.Logger.AssertSingleDebugMessageExists(
            "Incremental PR analysis: an error occurred while retrieving the cache entries! 'prepare_read' response: { Enabled = False, Url = https://www.sonarsource.com }");
        handler.Requests.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task DownloadCache_PrepareRead_CacheEnabledButUrlMissing()
    {
        var handler = MockHttpHandler(CacheFullUrl, @"{ ""enabled"": ""true"" }");
        var context = new Context(handler, CacheBaseUrl);

        var result = await context.Server
            .DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch, Organization, Token));

        result.Should().BeEmpty();
        context.Logger.AssertSingleDebugMessageExists("Incremental PR analysis: an error occurred while retrieving the cache entries! 'prepare_read' response: { Enabled = True, Url =  }");
        handler.Requests.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task DownloadCache_ThrowException()
    {
        using var stream = new MemoryStream([42, 42]); // this is a random byte array that fails deserialization
        var handler = MockHttpHandler(CacheFullUrl, "https://www.ephemeralUrl.com", stream);
        var context = new Context(handler, CacheBaseUrl);

        var result = await context.Server
            .DownloadCache(CreateLocalSettings(ProjectKey, ProjectBranch, Organization, Token));

        result.Should().BeEmpty();
        var warningDetails =
#if NET
            "The archive entry was compressed using an unsupported compression method.";
#else
            "Found invalid data while decoding.";
#endif
        context.Logger.AssertSingleWarningExists($"Incremental PR analysis: an error occurred while retrieving the cache entries! {warningDetails}");
        context.Logger.AssertNoErrorsLogged();
        handler.Requests.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task DownloadRules_SonarCloud()
    {
        var context = new Context();
        context.WebDownloader
            .Download("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1")
            .Returns(
                """
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
        using var responseStream = new MemoryStream([1, 2, 3]);
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StreamContent(responseStream) };
        var context = new Context(new HttpMessageHandlerMock((_, _) => Task.FromResult(response)));

        var actual = await context.Server.DownloadJreAsync(CreateJreMetadata("http://localhost/path-to-jre"));

        using var stream = new MemoryStream(); // actual is not a memory stream because of how HttpClient reads it from the handler.
        await actual.CopyToAsync(stream);
        stream.ToArray().Should().BeEquivalentTo([1, 2, 3]);
        context.WebDownloader.ReceivedCalls().Should().BeEmpty();
        context.Logger.AssertDebugLogged("Downloading Java JRE from http://localhost/path-to-jre.");
    }

    [TestMethod]
    public async Task DownloadJreAsync_DownloadThrows_Failure() =>
        await new Context(new HttpMessageHandlerMock((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException()))).Server
            .Invoking(async x => await x.DownloadJreAsync(CreateJreMetadata("http://local")))
            .Should().ThrowAsync<HttpRequestException>();

    [TestMethod]
    public async Task DownloadJreAsync_NullMetadata_Failure() =>
        await new Context().Server.Invoking(async x => await x.DownloadJreAsync(null)).Should().ThrowAsync<NullReferenceException>();

    [TestMethod]
    public async Task DownloadJreAsync_NullDownloadUrl_Failure() =>
        await new Context().Server.Invoking(async x => await x.DownloadJreAsync(CreateJreMetadata(null))).Should().ThrowAsync<AnalysisException>()
            .WithMessage("JreMetadata must contain a valid download URL.");

    [TestMethod]
    public async Task DownloadEngineAsync_Success()
    {
        using var responseStream = new MemoryStream([1, 2, 3]);
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StreamContent(responseStream) };
        var context = new Context(new HttpMessageHandlerMock((_, _) => Task.FromResult(response)));

        var actual = await context.Server
            .DownloadEngineAsync(CreateEngineMetadata("http://localhost/path-to-engine"));

        using var stream = new MemoryStream(); // actual is not a memory stream because of how HttpClient reads it from the handler.
        await actual.CopyToAsync(stream);
        stream.ToArray().Should().BeEquivalentTo([1, 2, 3]);
        context.WebDownloader.ReceivedCalls().Should().BeEmpty();
        context.Logger.AssertDebugLogged("Downloading Scanner Engine from http://localhost/path-to-engine");
    }

    [TestMethod]
    public async Task DownloadEngineAsync_DownloadThrows_Failure() =>
        await new Context(new HttpMessageHandlerMock((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException()))).Server
            .Invoking(async x => await x.DownloadEngineAsync(CreateEngineMetadata("http://localhost/path-to-engine")))
            .Should().ThrowAsync<HttpRequestException>();

    [TestMethod]
    public async Task DownloadEngineAsync_NullDownloadUrl_Failure() =>
        await new Context().Server.Invoking(async x => await x.DownloadEngineAsync(CreateEngineMetadata(null))).Should().ThrowAsync<AnalysisException>()
            .WithMessage("EngineMetadata must contain a valid download URL.");

    private static MemoryStream CreateCacheStream(IMessage message)
    {
        using var stream = new MemoryStream();
        message.WriteDelimitedTo(stream);
        stream.Seek(0, SeekOrigin.Begin);

        var compressed = new MemoryStream();
        using var compressor = new GZipStream(compressed, CompressionMode.Compress, true);
        stream.CopyTo(compressor);

        compressor.Close();
        compressed.Seek(0, SeekOrigin.Begin);
        return compressed;
    }

    private static HttpMessageHandlerMock MockHttpHandler(string cacheFullUrl, string prepareReadResponse, HttpStatusCode prepareReadResponseCode = HttpStatusCode.OK) =>
        new(
            (request, _) => Task.FromResult(request.RequestUri == new Uri(cacheFullUrl)
                                ? new HttpResponseMessage { StatusCode = prepareReadResponseCode, Content = new StringContent(prepareReadResponse) }
                                : new HttpResponseMessage(HttpStatusCode.NotFound)),
            Token);

    private static HttpMessageHandlerMock MockHttpHandler(string cacheFullUrl, string ephemeralCacheUrl, Stream cacheData) =>
        new((request, _) => Task.FromResult(request.RequestUri switch
        {
            var url when url == new Uri(cacheFullUrl) => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($$"""{ "enabled": "true", "url":"{{ephemeralCacheUrl}}" }""")
            },
            var url when url == new Uri(ephemeralCacheUrl) => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StreamContent(cacheData),
            },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        }));

    private static ProcessedArgs CreateLocalSettings(string projectKey,
                                                     string branch,
                                                     string organization = "placeholder",
                                                     string token = "placeholder",
                                                     string tokenKey = SonarProperties.SonarToken)
    {
        var args = Substitute.For<ProcessedArgs>();
        args.ProjectKey.Returns(projectKey);
        args.Organization.Returns(organization);
        args.TryGetSetting(SonarProperties.PullRequestBase, out Arg.Any<string>())
            .Returns(x =>
                {
                    x[1] = branch;
                    return !string.IsNullOrWhiteSpace(branch);
                });
        args.TryGetSetting(tokenKey, out Arg.Any<string>())
            .Returns(x =>
                {
                    x[1] = token;
                    return !string.IsNullOrWhiteSpace(token);
                });
        return args;
    }

    private static JreMetadata CreateJreMetadata(string url) =>
        new(null, null, null, url, null);

    private static EngineMetadata CreateEngineMetadata(Uri url) =>
        new(null, null, url);

    private sealed class Context
    {
        public readonly IDownloader WebDownloader = Substitute.For<IDownloader>();
        public readonly IDownloader ApiDownloader = Substitute.For<IDownloader>();
        public readonly TestLogger Logger = new();

        private readonly HttpMessageHandlerMock handler;

        public SonarCloudWebServer Server => new(WebDownloader, ApiDownloader, Version, Logger, Organization, HttpTimeout, handler);

        public Context(HttpMessageHandlerMock handler = null, string cacheBase = null)
        {
            this.handler = handler;
            MockDownloaderServerSettings(cacheBase);
        }

        private void MockDownloaderServerSettings(string cacheBase)
        {
            var serverSettingsJson = cacheBase is null
                ? """{"settings":[]}"""
                : $$"""{"settings":[{ "key":"sonar.sensor.cache.baseUrl","value": "{{cacheBase}}" }]}""";
            WebDownloader.Download(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(serverSettingsJson));
            WebDownloader.TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(new Tuple<bool, string>(false, string.Empty)));
        }
    }
}
