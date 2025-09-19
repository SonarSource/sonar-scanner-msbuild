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

using SonarScanner.MSBuild.PreProcessor.EngineResolution;
using SonarScanner.MSBuild.PreProcessor.JreResolution;
using SonarScanner.MSBuild.PreProcessor.Protobuf;
using SonarScanner.MSBuild.PreProcessor.WebServer;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class SonarWebServerTest
{
    private const string ProjectKey = "project-key";

    private TestLogger logger;
    private IDownloader downloader;
    private Version version;
    private SonarWebServerStub sut;

    [TestInitialize]
    public void Init()
    {
        version = new Version("9.9");
        logger = new();
        downloader = Substitute.For<IDownloader>();
        sut = CreateServer(version);
    }

    [TestCleanup]
    public void Cleanup() =>
        sut?.Dispose();

    [TestMethod]
    public void Ctor_Null_Throws()
    {
        ((Func<SonarWebServerStub>)(() => new SonarWebServerStub(null, null, version, logger, null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("webDownloader");
        ((Func<SonarWebServerStub>)(() => new SonarWebServerStub(downloader, null, version, logger, null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("apiDownloader");
        ((Func<SonarWebServerStub>)(() => new SonarWebServerStub(downloader, downloader, null, logger, null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("serverVersion");
        ((Func<SonarWebServerStub>)(() => new SonarWebServerStub(downloader, downloader, version, null, null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public async Task DownloadQualityProfile_LogHttpError()
    {
        downloader
            .TryDownloadIfExists(new($"api/qualityprofiles/search?project={ProjectKey}", UriKind.Relative), Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(true, "trash")));
        Func<Task> action = async () => await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        await action.Should().ThrowAsync<Exception>();
    }

    [TestMethod]
    public async Task DownloadQualityProfile_InvalidOrganizationKey_After_Version63()
    {
        downloader
            .TryDownloadIfExists(WebUtils.EscapedUri("api/qualityprofiles/search?project={0}&organization=ThisIsInvalidValue", ProjectKey), false)
            .Returns(Task.FromResult(Tuple.Create(false, (string)null)));
        // SonarCloud returns 404, WebClientDownloader returns null
        downloader
            .Download(WebUtils.EscapedUri("api/qualityprofiles/search?defaults=true&organization=ThisIsInvalidValue"), false)
            .Returns(Task.FromResult<string>(null));
        Func<Task> act = async () => await CreateServer(new Version("6.4"), "ThisIsInvalidValue").DownloadQualityProfile(ProjectKey, null, "cs");

        await act.Should().ThrowAsync<AnalysisException>().WithMessage("Cannot download quality profile. Check scanner arguments and the reported URL for more information.");
        logger.Should().HaveErrors("Cannot download quality profile. Check scanner arguments and the reported URL for more information.");
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("aBranch", null)]
    [DataRow(null, "my Org")]
    [DataRow("aBranch", "my Org")]
    public async Task DownloadQualityProfile_QualityProfileFound(string branchName, string organization)
    {
        const string profileKey = "profile1k";
        const string language = "cs";
        var projectKey = "someKey";
        var projectTarget = branchName is null ? projectKey : $"{projectKey}:{branchName}";
        var baseQualityProfileUrl = WebUtils.EscapedUri("api/qualityprofiles/search?project={0}", projectTarget);
        var qualityProfileUrl = organization is null ? baseQualityProfileUrl : WebUtils.EscapedUri($"{baseQualityProfileUrl}&organization={{0}}", organization);
        var downloadResult = Tuple.Create(true, $$"""{ profiles: [{"key":"{{profileKey}}","name":"profile1","language":"{{language}}"}]}""");
        downloader
            .TryDownloadIfExists(qualityProfileUrl, Arg.Any<bool>())
            .Returns(Task.FromResult(downloadResult));
        var result = await CreateServer(null, organization).DownloadQualityProfile(projectKey, branchName, language);

        result.Should().Be(profileKey);
    }

    [TestMethod]
    public async Task DownloadQualityProfile_FallBackDefaultProfile_QualityProfileFound()
    {
        const string profileKey = "defaultProfile";
        const string language = "cs";
        var projectKey = "someKey";
        downloader
            .TryDownloadIfExists(WebUtils.EscapedUri("api/qualityprofiles/search?project={0}", projectKey), Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(false, (string)null)));
        downloader
            .Download(new("api/qualityprofiles/search?defaults=true", UriKind.Relative), Arg.Any<bool>())
            .Returns(Task.FromResult($$"""{ profiles: [{"key":"{{profileKey}}","name":"profile1","language":"{{language}}"}]}"""));

        var result = await sut.DownloadQualityProfile(projectKey, null, language);

        result.Should().Be(profileKey);
    }

    [TestMethod]
    public async Task DownloadQualityProfile_NoProfileForLanguage_QualityProfileNotFound()
    {
        const string profileKey = "defaultProfile";
        const string language = "cs";
        var projectKey = "someKey";

        var qualityProfileUrl = WebUtils.EscapedUri("api/qualityprofiles/search?project={0}", projectKey);
        var downloadResult = Tuple.Create(true, $$"""{ profiles: [{"key":"{{profileKey}}","name":"profile1","language":"{{language}}"}]}""");
        downloader
            .TryDownloadIfExists(qualityProfileUrl, Arg.Any<bool>())
            .Returns(Task.FromResult(downloadResult));

        var result = await sut.DownloadQualityProfile(projectKey, null, "java");

        result.Should().BeNull();
    }

    [TestMethod]
    public async Task DownloadQualityProfile_NoProfileForProject_QualityProfileNotFound()
    {
        const string language = "cs";
        var projectKey = "someKey";
        var downloadResult = Tuple.Create(true, "{ profiles: []}");
        var qualityProfileUrl = WebUtils.EscapedUri("api/qualityprofiles/search?project={0}", projectKey);
        downloader
            .TryDownloadIfExists(qualityProfileUrl, Arg.Any<bool>())
            .Returns(Task.FromResult(downloadResult));

        var result = await sut.DownloadQualityProfile(projectKey, null, language);

        result.Should().BeNull();
    }

    [TestMethod]
    public async Task DownloadQualityProfile_MissingProfiles_ReturnsFalseAndEmptyContent()
    {
        var downloadResult = Tuple.Create(true, """{"unexpected": "valid json"}""");
        downloader
            .TryDownloadIfExists(new($"api/qualityprofiles/search?project={ProjectKey}", UriKind.Relative), Arg.Any<bool>())
            .Returns(Task.FromResult(downloadResult));

        var qualityProfile = await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        qualityProfile.Should().BeNull();
    }

    [TestMethod]
    public async Task DownloadQualityProfile_MissingKey_ReturnsFalseAndEmptyContent()
    {
        var downloadResult = Tuple.Create(true, """{ profiles: [ { "language":"cs" } ] }""");
        downloader
            .TryDownloadIfExists(new($"api/qualityprofiles/search?project={ProjectKey}", UriKind.Relative), Arg.Any<bool>())
            .Returns(Task.FromResult(downloadResult));

        var qualityProfile = await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        qualityProfile.Should().BeNull();
    }

    [TestMethod]
    public async Task DownloadQualityProfile_MissingLanguage_ReturnsFalseAndEmptyContent()
    {
        var downloadResult = Tuple.Create(true, @"{ profiles: [ { ""key"":""p1"" } ] }");
        downloader
            .TryDownloadIfExists(new($"api/qualityprofiles/search?project={ProjectKey}", UriKind.Relative), Arg.Any<bool>())
            .Returns(Task.FromResult(downloadResult));

        var qualityProfile = await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        qualityProfile.Should().BeNull();
    }

    // This scenario is unlikely to happen but still needs to be covered
    // The behavior needs to be update according to the comment in the method.
    // The exception raised is not the correct one.
    [TestMethod]
    public async Task DownloadQualityProfile_MultipleProfileWithSameLanguage_ShouldThrow()
    {
        var downloadResult = Tuple.Create(true, @"{ profiles: [ { ""key"":""p2"", ""language"":""cs"" }, { ""key"":""p1"", ""language"":""cs"" } ] }");
        downloader
            .TryDownloadIfExists(new($"api/qualityprofiles/search?project={ProjectKey}", UriKind.Relative), Arg.Any<bool>())
            .Returns(Task.FromResult(downloadResult));

        Func<Task> act = async () => await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        await act.Should().ThrowAsync<AnalysisException>();
    }

    [TestMethod]
    public async Task DownloadQualityProfile_SpecificProfileRequestUrl_QualityProfileFound()
    {
        var downloadResult = Tuple.Create(true, @"{ profiles: [ { ""key"":""p1"", ""name"":""p1"", ""language"":""cs"", ""isDefault"": false } ] }");
        downloader
            .TryDownloadIfExists(Arg.Any<Uri>(), Arg.Any<bool>())
            .Returns(Task.FromResult(downloadResult));

        var qualityProfile = await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        qualityProfile.Should().Be("p1");
    }

    [TestMethod]
    public async Task DownloadQualityProfile_DefaultProfileRequestUrl_QualityProfileFound()
    {
        downloader
            .TryDownloadIfExists(Arg.Any<Uri>(), Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(false, (string)null)));
        downloader
            .Download(Arg.Any<Uri>(), Arg.Any<bool>())
            .Returns(Task.FromResult(@"{ profiles: [ { ""key"":""p1"", ""name"":""p1"", ""language"":""cs"", ""isDefault"": false } ] }"));

        var qualityProfile = await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        qualityProfile.Should().Be("p1");
    }

    [TestMethod]
    public void DownloadRules_UseParamAsKey()
    {
        downloader
            .Download(Arg.Any<Uri>(), Arg.Any<bool>())
            .Returns(Task.FromResult("""
                { total: 1, p: 1, ps: 1,
                  rules: [{
                    key: "vbnet:S2368",
                    repo: "vbnet",
                    name: "Public methods should not have multidimensional array parameters",
                    severity: "MAJOR",
                    lang: "vbnet",
                    params: [ ],
                    type: "CODE_SMELL" }],
                  actives: {
                    "vbnet:S2368": [{
                      qProfile: "qp",
                      inherit: "NONE",
                      severity: "MAJOR",
                      params: [{
                        key: "CheckId",
                        value: "OverwrittenId",
                        type: "FLOAT" }]
                    }]
                  }
                }
                """));

        var actual = sut.DownloadRules("qp").Result;

        actual.Should().ContainSingle();
        actual[0].RepoKey.Should().Be("vbnet");
        actual[0].RuleKey.Should().Be("OverwrittenId");
        actual[0].InternalKeyOrKey.Should().Be("OverwrittenId");
        actual[0].TemplateKey.Should().BeNull();
        actual[0].Parameters.Should().ContainSingle();
    }

    [TestMethod]
    public void DownloadRules_ShouldNotGoBeyond_10k_Results()
    {
        for (var page = 1; page <= 21; page++)
        {
            downloader
                .Download(new($"api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p={page}", UriKind.Relative))
                .Returns(
                    $$"""
                    {
                        total: 10500,
                        p: {{page}},
                        ps: 500,
                        rules: [{
                            key: "vbnet:S2368",
                            repo: "vbnet",
                            name: "Public methods should not have multidimensional array parameters",
                            severity: "MAJOR",
                            lang: "vbnet",
                            params: [ ],
                            type: "CODE_SMELL"
                        },
                        {
                            key: "common-vbnet:InsufficientCommentDensity",
                            repo: "common-vbnet",
                            internalKey: "InsufficientCommentDensity.internal",
                            templateKey: "dummy.template.key",
                            name: "Source files should have a sufficient density of comment lines",
                            severity: "MAJOR",
                            lang: "vbnet",
                            params: [
                                {
                                    key: "minimumCommentDensity",
                                    defaultValue: "25",
                                    type: "FLOAT"
                                }],
                            type: "CODE_SMELL"
                        }],
                        actives: {
                            "vbnet:S2368": [
                                {
                                    qProfile:"vbnet - sonar - way - 34825",
                                    inherit: "NONE",
                                    severity:"MAJOR",
                                    params: []
                                }],
                            "common-vbnet:InsufficientCommentDensity": [
                            {
                                qProfile: "vbnet - sonar - way - 34825",
                                inherit:"NONE",
                                severity:"MAJOR",
                                params: [
                                {
                                    key:"minimumCommentDensity",
                                    value:"50"
                                }]
                            }]
                        }
                    }
                    """);
        }

        var rules = sut.DownloadRules("qp").Result;

        rules.Should().HaveCount(40);
    }

    [TestMethod]
    public void DownloadRules()
    {
        downloader
            .Download(new("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1", UriKind.Relative))
            .Returns("""
                {
                    total: 3, p: 1, ps: 2,
                    rules: [{
                        key: "vbnet:S2368",
                        repo: "vbnet",
                        name: "Public methods should not have multidimensional array parameters",
                        severity: "MAJOR",
                        lang: "vbnet",
                        params: [ ],
                        type: "CODE_SMELL"
                    },
                    {
                        key: "common-vbnet:InsufficientCommentDensity",
                        repo: "common-vbnet",
                        internalKey: "InsufficientCommentDensity.internal",
                        templateKey: "dummy.template.key",
                        name: "Source files should have a sufficient density of comment lines",
                        severity: "MAJOR",
                        lang: "vbnet",
                        params: [
                        {
                            key: "minimumCommentDensity",
                            defaultValue: "25",
                            type: "FLOAT"
                        }
                        ],
                        type: "CODE_SMELL"
                    },
                    {
                        key: "vbnet:S1234",
                        repo: "vbnet",
                        name: "This rule is not active",
                        severity: "MAJOR",
                        lang: "vbnet",
                        params: [ ],
                        type: "CODE_SMELL"
                    }],
                    actives: {
                        "vbnet:S2368": [
                        {
                            qProfile: "vbnet - sonar - way - 34825",
                            inherit: "NONE",
                            severity: "MAJOR",
                            params: [ ]
                        }],
                        "common-vbnet:InsufficientCommentDensity": [
                        {
                            qProfile: "vbnet - sonar - way - 34825",
                            inherit: "NONE",
                            severity: "MAJOR",
                            params: [
                            {
                                key: "minimumCommentDensity",
                                value: "50"
                            }]
                        }]
                    }
                }
                """);

        downloader
            .Download(new("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=2", UriKind.Relative))
            .Returns(
                """
                {
                    total: 3, p: 2, ps: 2,
                    rules: [{
                        key: "vbnet:S2346",
                        repo: "vbnet",
                        name: "Flags enumerations zero-value members should be named \"None\"",
                        severity: "MAJOR",
                        lang: "vbnet",
                        params: [ ],
                        type: "CODE_SMELL"
                    }],
                    actives: {
                        "vbnet:S2346": [
                        {
                            qProfile: "vbnet - sonar - way - 34825",
                            inherit: "NONE",
                            severity: "MAJOR",
                            params: [ ]
                        }]
                    }
                }
                """);

        var actual = sut.DownloadRules("qp").Result;

        actual.Should().HaveCount(4);

        actual[0].RepoKey.Should().Be("vbnet");
        actual[0].RuleKey.Should().Be("S2368");
        actual[0].InternalKeyOrKey.Should().Be("S2368");
        actual[0].TemplateKey.Should().BeNull();
        actual[0].Parameters.Should().BeEmpty();
        actual[0].IsActive.Should().BeTrue();

        actual[1].RepoKey.Should().Be("common-vbnet");
        actual[1].RuleKey.Should().Be("InsufficientCommentDensity");
        actual[1].InternalKeyOrKey.Should().Be("InsufficientCommentDensity.internal");
        actual[1].TemplateKey.Should().Be("dummy.template.key");
        actual[1].Parameters.Should().ContainSingle();
        actual[1].Parameters.First().Should().Be(new KeyValuePair<string, string>("minimumCommentDensity", "50"));
        actual[1].IsActive.Should().BeTrue();

        actual[2].RepoKey.Should().Be("vbnet");
        actual[2].RuleKey.Should().Be("S1234");
        actual[2].InternalKeyOrKey.Should().Be("S1234");
        actual[2].TemplateKey.Should().BeNull();
        actual[2].Parameters.Should().BeNull();
        actual[2].IsActive.Should().BeFalse();

        actual[3].RepoKey.Should().Be("vbnet");
        actual[3].RuleKey.Should().Be("S2346");
        actual[3].InternalKeyOrKey.Should().Be("S2346");
        actual[3].TemplateKey.Should().BeNull();
        actual[3].Parameters.Should().BeEmpty();
        actual[3].IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void DownloadRules_Active_WhenActivesContainsRuleWithMultipleBodies_UseFirst()
    {
        downloader
            .Download(new("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1", UriKind.Relative))
            .Returns("""
                { total: 1, p: 1, ps: 1,
                    rules: [{
                        key: "key1",
                        repo: "vbnet",
                        name: "Public methods should not have multidimensional array parameters",
                        severity: "MAJOR",
                        lang: "vbnet",
                        params: [ ],
                        type: "CODE_SMELL"
                    }],
                    actives: {
                        "key1": [
                        {
                            qProfile: "qp",
                            inherit: "NONE",
                            severity: "MAJOR",
                            params: [
                            {
                                key: "CheckId",
                                value: "OverwrittenId-First",
                                type: "FLOAT"
                            }]
                        },
                        {
                            qProfile: "qp",
                            inherit: "NONE",
                            severity: "MAJOR",
                            params: [
                            {
                                key: "CheckId",
                                value: "OverwrittenId-Second",
                                type: "FLOAT"
                            }]
                        }]
                    }
                }
                """);

        var actual = sut.DownloadRules("qp").Result;

        actual.Should().ContainSingle();
        actual.Single().IsActive.Should().BeTrue();
        actual.Single().RuleKey.Should().Be("OverwrittenId-First");
    }

    [TestMethod]
    public void DownloadRules_NoActives()
    {
        downloader
            .Download(new("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1", UriKind.Relative))
            .Returns("""
                { total: 3, p: 1, ps: 500,
                    rules: [
                    {
                        "key": "csharpsquid:S2757",
                        "repo": "csharpsquid",
                        "type": "BUG"
                    },
                    {
                        "key": "csharpsquid:S1117",
                        "repo": "csharpsquid",
                        "type": "CODE_SMELL"
                    }]
                }
                """);

        var rules = sut.DownloadRules("qp").Result;

        rules.Should().HaveCount(2);

        rules[0].RepoKey.Should().Be("csharpsquid");
        rules[0].RuleKey.Should().Be("S2757");
        rules[0].InternalKeyOrKey.Should().Be("S2757");
        rules[0].Parameters.Should().BeNull();
        rules[0].IsActive.Should().BeFalse();

        rules[1].RepoKey.Should().Be("csharpsquid");
        rules[1].RuleKey.Should().Be("S1117");
        rules[1].InternalKeyOrKey.Should().Be("S1117");
        rules[1].Parameters.Should().BeNull();
        rules[1].IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void DownloadRules_EmptyActives()
    {
        downloader
            .Download(new("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1", UriKind.Relative))
            .Returns("""
                { total: 3, p: 1, ps: 500,
                    rules: [
                        {
                            "key": "csharpsquid:S2757",
                            "repo": "csharpsquid",
                            "type": "BUG"
                        },
                        {
                            "key": "csharpsquid:S1117",
                            "repo": "csharpsquid",
                            "type": "CODE_SMELL"
                        }],
                    actives: {}
                }
                """);

        var rules = sut.DownloadRules("qp").Result;

        rules.Should().HaveCount(2);

        rules[0].RepoKey.Should().Be("csharpsquid");
        rules[0].RuleKey.Should().Be("S2757");
        rules[0].InternalKeyOrKey.Should().Be("S2757");
        rules[0].Parameters.Should().BeNull();
        rules[0].IsActive.Should().BeFalse();

        rules[1].RepoKey.Should().Be("csharpsquid");
        rules[1].RuleKey.Should().Be("S1117");
        rules[1].InternalKeyOrKey.Should().Be("S1117");
        rules[1].Parameters.Should().BeNull();
        rules[1].IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void DownloadRules_EscapeUrl()
    {
        downloader
            .Download(new("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=my%23qp&p=1", UriKind.Relative))
            .Returns("""
                { total: 3, p: 1, ps: 500,
                    rules: [
                        {
                            "key": "csharpsquid:S2757",
                            "repo": "csharpsquid",
                            "type": "BUG"
                        }]
                }
                """);

        var rules = sut.DownloadRules("my#qp").Result;

        rules.Should().ContainSingle();

        rules[0].RepoKey.Should().Be("csharpsquid");
        rules[0].RuleKey.Should().Be("S2757");
        rules[0].InternalKeyOrKey.Should().Be("S2757");
        rules[0].IsActive.Should().BeFalse();
    }

    [TestMethod]
    public async Task DownloadRules_RequestUrl()
    {
        downloader
            .Download(new("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=profile&p=1", UriKind.Relative))
            .Returns("""{ total: 1, p: 1, ps: 1, rules: [] }""");

        var rules = await sut.DownloadRules("profile");

        rules.Should().BeEmpty();
    }

    [TestMethod]
    public async Task DownloadAllLanguages_Succeeds()
    {
        downloader
            .Download(new("api/languages/list", UriKind.Relative), Arg.Any<bool>())
            .Returns(Task.FromResult("""{ languages: [{ key: "cs", name: "C#" }, { key: "flex", name: "Flex" } ]}"""));

        var expected = new List<string> { "cs", "flex" };

        var actual = (await sut.DownloadAllLanguages()).ToList();

        expected.SequenceEqual(actual).Should().BeTrue();
    }

    [TestMethod]
    public async Task TryDownloadEmbeddedFile_NullPluginKey_Throws()
    {
        Func<Task> act = async () => await sut.TryDownloadEmbeddedFile(null, "filename", "targetDir");

        (await act.Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("pluginKey");
    }

    [TestMethod]
    public async Task TryDownloadEmbeddedFile_NullEmbeddedFileName_Throws()
    {
        Func<Task> act = async () => await sut.TryDownloadEmbeddedFile("key", null, "targetDir");

        (await act.Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("embeddedFileName");
    }

    [TestMethod]
    public async Task TryDownloadEmbeddedFile_NullTargetDirectory_Throws()
    {
        Func<Task> act = async () => await sut.TryDownloadEmbeddedFile("pluginKey", "filename", null);

        (await act.Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("targetDirectory");
    }

    [TestMethod]
    public async Task TryDownloadEmbeddedFile_RequestedFileExist_ReturnsTrue()
    {
        downloader
            .TryDownloadFileIfExists(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(Task.FromResult(true));

        var success = await sut.TryDownloadEmbeddedFile("csharp", "dummy.txt", Path.GetRandomFileName());

        success.Should().BeTrue("Expected success");
    }

    [TestMethod]
    public async Task TryDownloadEmbeddedFile_RequestedFileDoesNotExist_ReturnsFalse()
    {
        downloader.TryDownloadFileIfExists(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(false));

        var success = await sut.TryDownloadEmbeddedFile("csharp", "dummy.txt", Path.GetRandomFileName());

        success.Should().BeFalse("Expected failure");
    }

    [TestMethod]
    public void GetServerVersion_ReturnsVersion()
    {
        const string expected = "4.2";
        sut = CreateServer(new Version(expected));

        sut.ServerVersion.ToString().Should().Be(expected);
    }

    [TestMethod]
    public async Task DownloadJreMetadataAsync_NullOperatingSystem_Throws()
    {
        Func<Task> act = async () => await sut.DownloadJreMetadataAsync(null, "whatever");
        (await act.Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("operatingSystem");
    }

    [TestMethod]
    public async Task DownloadJreMetadataAsync_NullArchitecture_Throws()
    {
        Func<Task> act = async () => await sut.DownloadJreMetadataAsync("whatever", null);
        (await act.Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("architecture");
    }

    [TestMethod]
    public async Task DownloadJreMetadataAsync_Throws_Warning()
    {
        downloader
            .When(x => x.Download(new("analysis/jres?os=what&arch=ever", UriKind.Relative)))
            .Throw(new Exception());

        (await sut.DownloadJreMetadataAsync("what", "ever")).Should().BeNull();
        logger.Should().HaveWarnings("JRE Metadata could not be retrieved from analysis/jres?os=what&arch=ever.");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("{broken json")]
    [DataRow("[]")]
    public async Task DownloadJreMetadataAsync_ReturnsInvalid_Warning(string jresResponse)
    {
        downloader
            .Download(new("analysis/jres?os=what&arch=ever", UriKind.Relative))
            .Returns(jresResponse);

        (await sut.DownloadJreMetadataAsync("what", "ever")).Should().BeNull();
        logger.Should().HaveWarnings("JRE Metadata could not be retrieved from analysis/jres?os=what&arch=ever.");
    }

    [TestMethod]
    public async Task DownloadJreMetadataAsync_ReturnsSingle_Success()
    {
        downloader
            .Download(new("analysis/jres?os=what&arch=ever", UriKind.Relative))
            .Returns("""
                [{
                    "id": "someId",
                    "filename": "file42.txt",
                    "sha256": "42==",
                    "javaPath": "best/language/java.exe",
                    "os": "lunix",
                    "arch": "manjaro"
                }]
                """);

        var jreMetadata = await sut.DownloadJreMetadataAsync("what", "ever");

        jreMetadata.Should().NotBeNull();
        jreMetadata.Id.Should().Be("someId");
        jreMetadata.Filename.Should().Be("file42.txt");
        jreMetadata.Sha256.Should().Be("42==");
        jreMetadata.JavaPath.Should().Be("best/language/java.exe");
        jreMetadata.DownloadUrl.Should().BeNull();
        logger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public async Task DownloadJreMetadataAsync_ReturnsMultiple_Success_ReturnsFirst()
    {
        downloader
            .Download(new("analysis/jres?os=what&arch=ever", UriKind.Relative))
            .Returns("""
                [
                    { "id": "first" },
                    { "id": "second" },
                ]
                """);

        var jreMetadata = await sut.DownloadJreMetadataAsync("what", "ever");

        jreMetadata.Should().NotBeNull();
        jreMetadata.Id.Should().Be("first");
        logger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public async Task DownloadEngineMetadataAsync_Throws_Warning()
    {
        downloader
            .When(x => x.Download(new("analysis/engine", UriKind.Relative)))
            .Throw(new Exception());

        (await sut.DownloadEngineMetadataAsync()).Should().BeNull();
        logger.Should().HaveWarnings("Sonar Engine Metadata could not be retrieved from analysis/engine.");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("{broken json")]
    [DataRow("[]")]
    [DataRow("""
        [{
          "filename": "sonarcloud-scanner-engine-11.14.1.763.jar",
          "sha256": "907f676d488af266431bafd3bc26f58408db2d9e73efc66c882c203f275c739b"
        }]
        """)]
    public async Task DownloadEngineMetadataAsync_ReturnsInvalid_Warning(string jresResponse)
    {
        downloader
            .Download(new("analysis/engine", UriKind.Relative))
            .Returns(jresResponse);

        (await sut.DownloadEngineMetadataAsync()).Should().BeNull();
        logger.Should().HaveWarnings("Sonar Engine Metadata could not be retrieved from analysis/engine.");
    }

    [TestMethod]
    public async Task DownloadEngineMetadataAsync_SonarQubeCloud_Success()
    {
        downloader
            .Download(new("analysis/engine", UriKind.Relative)) // returns a downloadUrl
            .Returns("""
                {
                  "filename": "sonarcloud-scanner-engine-11.14.1.763.jar",
                  "sha256": "907f676d488af266431bafd3bc26f58408db2d9e73efc66c882c203f275c739b",
                  "downloadUrl": "https://scanner.sonarcloud.io/engines/sonarcloud-scanner-engine-11.14.1.763.jar"
                }
                """);

        var jreMetadata = await sut.DownloadEngineMetadataAsync();

        jreMetadata.Should().BeEquivalentTo(new
        {
            Filename = "sonarcloud-scanner-engine-11.14.1.763.jar",
            Sha256 = "907f676d488af266431bafd3bc26f58408db2d9e73efc66c882c203f275c739b",
            DownloadUrl = new Uri("https://scanner.sonarcloud.io/engines/sonarcloud-scanner-engine-11.14.1.763.jar")
        });
        logger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public async Task DownloadEngineMetadataAsync_SonarQubeServer_Success()
    {
        downloader
            .Download(new("analysis/engine", UriKind.Relative)) // returns no downloadUrl
            .Returns("""
                {
                  "filename": "sonarcloud-scanner-engine-11.14.1.763.jar",
                  "sha256": "907f676d488af266431bafd3bc26f58408db2d9e73efc66c882c203f275c739b",
                }
                """);

        var jreMetadata = await sut.DownloadEngineMetadataAsync();

        jreMetadata.Should().BeEquivalentTo(new
        {
            Filename = "sonarcloud-scanner-engine-11.14.1.763.jar",
            Sha256 = "907f676d488af266431bafd3bc26f58408db2d9e73efc66c882c203f275c739b",
            DownloadUrl = (Uri)null,
        });
        logger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public void SupportsJreProvisioning_Defaults_True() =>
        sut.SupportsJreProvisioning.Should().BeTrue("it defaults to true");

    [TestMethod]
    public async Task DownloadAllLanguages_RequestUrl()
    {
        downloader
            .Download(new("api/languages/list", UriKind.Relative), Arg.Any<bool>())
            .Returns(Task.FromResult("{ languages: [ ] }"));

        var languages = await sut.DownloadAllLanguages();

        languages.Should().BeEmpty();
    }

    private SonarWebServerStub CreateServer(Version version = null, string organization = null) =>
        new(downloader, downloader, version ?? this.version, logger, organization);

    private class SonarWebServerStub : SonarWebServerBase, ISonarWebServer
    {
        public SonarWebServerStub(IDownloader webDownloader, IDownloader apiDownloader, Version serverVersion, ILogger logger, string organization)
            : base(webDownloader, apiDownloader, serverVersion, logger, organization)
        { }

        public Task<IList<SensorCacheEntry>> DownloadCache(ProcessedArgs localSettings) => throw new NotImplementedException();

        public bool IsServerVersionSupported() => throw new NotImplementedException();

        public Task<bool> IsServerLicenseValid() => throw new NotImplementedException();

        public Task<Stream> DownloadJreAsync(JreMetadata metadata) => throw new NotImplementedException();

        public Task<Stream> DownloadEngineAsync(EngineMetadata metadata) => throw new NotImplementedException();
    }
}
