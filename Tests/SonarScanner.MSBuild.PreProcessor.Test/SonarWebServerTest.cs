/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Protobuf;
using SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;
using SonarScanner.MSBuild.PreProcessor.WebServer;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class SonarWebServerTest
    {
        private const string ProjectKey = "project-key";

        private readonly TestDownloader downloader;
        private readonly TestLogger logger;
        private readonly Version version;

        private SonarWebServerStub sut;

        public SonarWebServerTest()
        {
            downloader = new TestDownloader();
            version = new Version("9.9");
            logger = new TestLogger();
        }

        [TestInitialize]
        public void Init()
        {
            sut = new SonarWebServerStub(downloader, version, logger, null);
        }

        [TestCleanup]
        public void Cleanup() =>
            sut?.Dispose();

        [TestMethod]
        public void Ctor_Null_Throws()
        {
            ((Func<SonarWebServerStub>)(() => new SonarWebServerStub(null, version, logger, null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("downloader");
            ((Func<SonarWebServerStub>)(() => new SonarWebServerStub(Mock.Of<IDownloader>(), null, logger, null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("serverVersion");
            ((Func<SonarWebServerStub>)(() => new SonarWebServerStub(Mock.Of<IDownloader>(), version, null, null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public async Task TryGetQualityProfile_LogHttpError()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.TryDownloadIfExists($"api/qualityprofiles/search?project={ProjectKey}", It.IsAny<bool>())).ReturnsAsync(Tuple.Create(true, "trash"));
            sut = new SonarWebServerStub(downloaderMock.Object, version, logger, null);

            Func<Task> action = async () => await sut.TryGetQualityProfile(ProjectKey, null, "cs");

            await action.Should().ThrowAsync<Exception>();
        }

        [TestMethod]
        public void TryGetQualityProfile_InvalidOrganizationKey_After_Version63()
        {
            var mockDownloader = new Mock<IDownloader>(MockBehavior.Strict);
            mockDownloader.Setup(x => x.TryDownloadIfExists($"api/qualityprofiles/search?project={ProjectKey}&organization=ThisIsInvalidValue", false)).Returns(Task.FromResult(Tuple.Create(false, (string)null)));
            // SonarCloud returns 404, WebClientDownloader returns null
            mockDownloader.Setup(x => x.Download("api/qualityprofiles/search?defaults=true&organization=ThisIsInvalidValue", false)).Returns(Task.FromResult<string>(null));
            mockDownloader.Setup(x => x.Dispose());

            sut = new SonarWebServerStub(mockDownloader.Object, new Version("6.4"), logger, "ThisIsInvalidValue");
            Action a = () => _ = sut.TryGetQualityProfile(ProjectKey, null, "cs").Result;

            a.Should().Throw<AggregateException>().WithMessage("One or more errors occurred.");
            logger.AssertErrorLogged("Failed to request and parse: Cannot download quality profile. Check scanner arguments and the reported URL for more information.");
            logger.AssertErrorLogged("Failed to request and parse: Cannot download quality profile. Check scanner arguments and the reported URL for more information.");
        }

        [TestMethod]
        [DataRow("foo bar")]
        public async Task TryGetQualityProfile_MainProjectProfile_QualityProfileFound(string projectKey)
        {
            const string profileKey = "profile1k";
            const string language = "cs";
            var qualityProfileUri = $"api/qualityprofiles/search?project={WebUtility.UrlEncode(projectKey)}";
            var profileResponse = $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}";
            var mockDownloader = new Mock<IDownloader>();
            mockDownloader.Setup(x => x.TryDownloadIfExists(qualityProfileUri, It.IsAny<bool>())).ReturnsAsync(Tuple.Create(true, profileResponse));
            sut = new SonarWebServerStub(mockDownloader.Object, new Version("9.9"), logger, null);

            var result = await sut.TryGetQualityProfile(projectKey, null, language);

            result.Item1.Should().BeTrue();
            result.Item2.Should().Be(profileKey);
        }

        [TestMethod]
        [DataRow("foo bar", "aBranch")]
        public async Task TryGetQualityProfile_BranchSpecificProfile_QualityProfileFound(string projectKey, string branchName)
        {
            const string profileKey = "profile1k";
            const string language = "cs";
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.TryDownloadIfExists($"api/qualityprofiles/search?project={WebUtility.UrlEncode($"{projectKey}:{branchName}")}", It.IsAny<bool>()))
                          .ReturnsAsync(Tuple.Create(true, $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}"));
            sut = new SonarWebServerStub(downloaderMock.Object, new Version("9.9"), logger, null);

            var result = await sut.TryGetQualityProfile(projectKey, branchName, language);

            result.Item1.Should().BeTrue();
            result.Item2.Should().Be(profileKey);
        }

        [TestMethod]
        [DataRow("foo bar", "my org")]
        public async Task TryGetQualityProfile_OrganizationProfile_QualityProfileFound(string projectKey, string organization)
        {
            const string profileKey = "orgProfile";
            const string language = "cs";
            var qualityProfileUri = $"api/qualityprofiles/search?project={WebUtility.UrlEncode($"{projectKey}")}&organization={WebUtility.UrlEncode($"{organization}")}";
            var mockDownloader = new Mock<IDownloader>();
            mockDownloader.Setup(x => x.TryDownloadIfExists(qualityProfileUri, It.IsAny<bool>()))
                          .ReturnsAsync(Tuple.Create(true, $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}"));
            sut = new SonarWebServerStub(mockDownloader.Object, version, logger, organization);

            var result = await sut.TryGetQualityProfile(projectKey, null, language);

            result.Item1.Should().BeTrue();
            result.Item2.Should().Be(profileKey);
        }

        [TestMethod]
        [DataRow("foo bar")]
        public async Task TryGetQualityProfile_FallBackDefaultProfile_QualityProfileFound(string projectKey)
        {
            const string profileKey = "defaultProfile";
            const string language = "cs";
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.TryDownloadIfExists($"api/qualityprofiles/search?project={WebUtility.UrlEncode(projectKey)}", It.IsAny<bool>()))
                          .ReturnsAsync(Tuple.Create(false, (string)null));
            downloaderMock.Setup(x => x.Download("api/qualityprofiles/search?defaults=true", It.IsAny<bool>()))
                          .ReturnsAsync($"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}");
            sut = new SonarWebServerStub(downloaderMock.Object, new Version("9.9"), logger, null);

            var result = await sut.TryGetQualityProfile(projectKey, null, language);

            result.Item1.Should().BeTrue();
            result.Item2.Should().Be(profileKey);
        }

        [TestMethod]
        [DataRow("foo bar", "java")]
        public async Task TryGetQualityProfile_NoProfileForLanguage_QualityProfileNotFound(string projectKey, string missingLanguage)
        {
            const string profileKey = "defaultProfile";
            const string language = "cs";
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.TryDownloadIfExists($"api/qualityprofiles/search?project={WebUtility.UrlEncode(projectKey)}", It.IsAny<bool>()))
                          .ReturnsAsync(Tuple.Create(true, $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}"));
            sut = new SonarWebServerStub(downloaderMock.Object, new Version("9.9"), logger, null);

            var result = await sut.TryGetQualityProfile(projectKey, null, missingLanguage);

            result.Item1.Should().BeFalse();
            result.Item2.Should().BeNull();
        }

        [TestMethod]
        [DataRow("foo bar")]
        public async Task TryGetQualityProfile_NoProfileForProject_QualityProfileNotFound(string projectKey)
        {
            const string language = "cs";
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.TryDownloadIfExists($"api/qualityprofiles/search?project={WebUtility.UrlEncode(projectKey)}", It.IsAny<bool>()))
                          .ReturnsAsync(Tuple.Create(true, "{ profiles: []}"));
            sut = new SonarWebServerStub(downloaderMock.Object, new Version("9.9"), logger, null);

            var result = await sut.TryGetQualityProfile(projectKey, null, language);

            result.Item1.Should().BeFalse();
            result.Item2.Should().BeNull();
        }

        [TestMethod]
        public async Task TryGetQualityProfile_MissingProfiles()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.TryDownloadIfExists($"api/qualityprofiles/search?project={ProjectKey}", It.IsAny<bool>()))
                          .ReturnsAsync(Tuple.Create(true, @"{""unexpected"": ""valid json""}"));
            sut = new SonarWebServerStub(downloaderMock.Object, new Version("9.9"), logger, null);

            var (success, content) = await sut.TryGetQualityProfile(ProjectKey, null, "cs");

            success.Should().BeFalse();
            content.Should().BeNull();
        }

        [TestMethod]
        public async Task TryGetQualityProfile_MissingKey()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.TryDownloadIfExists($"api/qualityprofiles/search?project={ProjectKey}", It.IsAny<bool>()))
                          .ReturnsAsync(Tuple.Create(true, @"{""language"":""cs""}"));
            sut = new SonarWebServerStub(downloaderMock.Object, new Version("9.9"), logger, null);

            var (success, content) = await sut.TryGetQualityProfile(ProjectKey, null, "cs");

            success.Should().BeFalse();
            content.Should().BeNull();
        }

        [DataTestMethod]
        public async Task TryGetQualityProfile_SpecificProfileRequestUrl()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.TryDownloadIfExists(It.IsAny<string>(), It.IsAny<bool>()))
                          .ReturnsAsync(Tuple.Create(true, @"{ profiles: [ { ""key"":""p1"", ""name"":""p1"", ""language"":""cs"", ""isDefault"": false } ] }"));
            sut = new SonarWebServerStub(downloaderMock.Object, version, logger, null);

            var (result, profile) = await sut.TryGetQualityProfile(ProjectKey, null, "cs");

            result.Should().BeTrue();
            profile.Should().Be("p1");
        }

        [DataTestMethod]
        public async Task TryGetQualityProfile_DefaultProfileRequestUrl()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.TryDownloadIfExists(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(Tuple.Create(false, (string)null));
            downloaderMock.Setup(x => x.Download(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(@"{ profiles: [ { ""key"":""p1"", ""name"":""p1"", ""language"":""cs"", ""isDefault"": false } ] }");
            sut = new SonarWebServerStub(downloaderMock.Object, version, logger, null);

            var (result, profile) = await sut.TryGetQualityProfile(ProjectKey, null, "cs");

            result.Should().BeTrue();
            profile.Should().Be("p1");
        }

        [TestMethod]
        public void GetRules_UseParamAsKey()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.Download(It.IsAny<string>(), It.IsAny<bool>()))
                         .ReturnsAsync(@"{ total: 1, p: 1, ps: 1,
            rules: [{
                key: ""vbnet:S2368"",
                repo: ""vbnet"",
                name: ""Public methods should not have multidimensional array parameters"",
                severity: ""MAJOR"",
                lang: ""vbnet"",
                params: [ ],
                type: ""CODE_SMELL""
            }],

            actives: {
                ""vbnet:S2368"": [
                {
                    qProfile: ""qp"",
                    inherit: ""NONE"",
                    severity: ""MAJOR"",
                    params: [
                    {
                      key: ""CheckId"",
                      value: ""OverwrittenId"",
                      type: ""FLOAT""
                    }
                    ]
                }
                ]
            }
            }");
            sut = new SonarWebServerStub(downloaderMock.Object, version, logger, null);

            var actual = sut.GetRules("qp").Result;
            actual.Should().ContainSingle();

            actual[0].RepoKey.Should().Be("vbnet");
            actual[0].RuleKey.Should().Be("OverwrittenId");
            actual[0].InternalKeyOrKey.Should().Be("OverwrittenId");
            actual[0].TemplateKey.Should().BeNull();
            actual[0].Parameters.Should().HaveCount(1);
        }

        [TestMethod]
        public void GetRules_ShouldNotGoBeyond_10k_Results()
        {
            for (var page = 1; page <= 21; page++)
            {
                downloader.Pages[$"api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p={page}"] = $@"
                    {{
                    total: 10500,
                    p: {page},
                    ps: 500,
                    rules: [{{
                        key: ""vbnet:S2368"",
                        repo: ""vbnet"",
                        name: ""Public methods should not have multidimensional array parameters"",
                        severity: ""MAJOR"",
                        lang: ""vbnet"",
                        params: [ ],
                        type: ""CODE_SMELL""
                    }},
                    {{
                        key: ""common-vbnet:InsufficientCommentDensity"",
                        repo: ""common-vbnet"",
                        internalKey: ""InsufficientCommentDensity.internal"",
                        templateKey: ""dummy.template.key"",
                        name: ""Source files should have a sufficient density of comment lines"",
                        severity: ""MAJOR"",
                        lang: ""vbnet"",
                        params: [
                            {{
                                key: ""minimumCommentDensity"",
                                defaultValue: ""25"",
                                type: ""FLOAT""
                            }}
                        ],
                        type: ""CODE_SMELL""
                    }}],
                    actives: {{
                        ""vbnet:S2368"": [
                            {{
                                qProfile:""vbnet - sonar - way - 34825"",
                                inherit: ""NONE"",
                                severity:""MAJOR"",
                                params: []
                            }}
                        ],
                    ""common-vbnet:InsufficientCommentDensity"": [
                        {{
                            qProfile: ""vbnet - sonar - way - 34825"",
                            inherit:""NONE"",
                            severity:""MAJOR"",
                            params: [
                            {{
                                key:""minimumCommentDensity"",
                                value:""50""
                            }}
                            ]
                        }}
                    ]
                    }}
                }}";
            }

            var rules = sut.GetRules("qp").Result;

            rules.Should().HaveCount(40);
        }

        [TestMethod]
        public void GetRules()
        {
            downloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1"] =
                @" { total: 3, p: 1, ps: 2,
            rules: [{
                key: ""vbnet:S2368"",
                repo: ""vbnet"",
                name: ""Public methods should not have multidimensional array parameters"",
                severity: ""MAJOR"",
                lang: ""vbnet"",
                params: [ ],
                type: ""CODE_SMELL""
            },
            {
                key: ""common-vbnet:InsufficientCommentDensity"",
                repo: ""common-vbnet"",
                internalKey: ""InsufficientCommentDensity.internal"",
                templateKey: ""dummy.template.key"",
                name: ""Source files should have a sufficient density of comment lines"",
                severity: ""MAJOR"",
                lang: ""vbnet"",
                params: [
                {
                    key: ""minimumCommentDensity"",
                    defaultValue: ""25"",
                    type: ""FLOAT""
                }
                ],
                type: ""CODE_SMELL""
            },
            {
                key: ""vbnet:S1234"",
                repo: ""vbnet"",
                name: ""This rule is not active"",
                severity: ""MAJOR"",
                lang: ""vbnet"",
                params: [ ],
                type: ""CODE_SMELL""
            },],

            actives: {
                ""vbnet:S2368"": [
                {
                    qProfile: ""vbnet - sonar - way - 34825"",
                    inherit: ""NONE"",
                    severity: ""MAJOR"",
                    params: [ ]
                }
                ],
                ""common-vbnet:InsufficientCommentDensity"": [
                {
                    qProfile: ""vbnet - sonar - way - 34825"",
                    inherit: ""NONE"",
                    severity: ""MAJOR"",
                    params: [
                    {
                        key: ""minimumCommentDensity"",
                        value: ""50""
                    }
                    ]
                }
                ]
            }
            }";

            downloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=2"] =
                @" { total: 3, p: 2, ps: 2,
            rules: [{
                key: ""vbnet:S2346"",
                repo: ""vbnet"",
                name: ""Flags enumerations zero-value members should be named \""None\"""",
                severity: ""MAJOR"",
                lang: ""vbnet"",
                params: [ ],
                type: ""CODE_SMELL""
            }],

            actives: {
                ""vbnet:S2346"": [
                {
                    qProfile: ""vbnet - sonar - way - 34825"",
                    inherit: ""NONE"",
                    severity: ""MAJOR"",
                    params: [ ]
                }
                ]
            }
            }";

            var actual = sut.GetRules("qp").Result;
            actual.Should().HaveCount(4);

            actual[0].RepoKey.Should().Be("vbnet");
            actual[0].RuleKey.Should().Be("S2368");
            actual[0].InternalKeyOrKey.Should().Be("S2368");
            actual[0].TemplateKey.Should().BeNull();
            actual[0].Parameters.Should().HaveCount(0);
            actual[0].IsActive.Should().BeTrue();

            actual[1].RepoKey.Should().Be("common-vbnet");
            actual[1].RuleKey.Should().Be("InsufficientCommentDensity");
            actual[1].InternalKeyOrKey.Should().Be("InsufficientCommentDensity.internal");
            actual[1].TemplateKey.Should().Be("dummy.template.key");
            actual[1].Parameters.Should().HaveCount(1);
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
            actual[3].Parameters.Should().HaveCount(0);
            actual[3].IsActive.Should().BeTrue();
        }

        [TestMethod]
        public void GetRules_Active_WhenActivesContainsRuleWithMultipleBodies_UseFirst()
        {
            // Arrange
            downloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1"] =
                @"{ total: 1, p: 1, ps: 1,
            rules: [{
                key: ""key1"",
                repo: ""vbnet"",
                name: ""Public methods should not have multidimensional array parameters"",
                severity: ""MAJOR"",
                lang: ""vbnet"",
                params: [ ],
                type: ""CODE_SMELL""
            }],

            actives: {
                ""key1"": [
                {
                    qProfile: ""qp"",
                    inherit: ""NONE"",
                    severity: ""MAJOR"",
                    params: [
                    {
                      key: ""CheckId"",
                      value: ""OverwrittenId-First"",
                      type: ""FLOAT""
                    }
                    ]
                },
                {
                    qProfile: ""qp"",
                    inherit: ""NONE"",
                    severity: ""MAJOR"",
                    params: [
                    {
                      key: ""CheckId"",
                      value: ""OverwrittenId-Second"",
                      type: ""FLOAT""
                    }
                    ]
                }
                ]
            }
            }";

            var actual = sut.GetRules("qp").Result;

            // Assert
            actual.Should().HaveCount(1);
            actual.Single().IsActive.Should().BeTrue();
            actual.Single().RuleKey.Should().Be("OverwrittenId-First");
        }

        [TestMethod]
        public void GetRules_NoActives()
        {
            downloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1"] = @"
            {
            total: 3,
            p: 1,
            ps: 500,
            rules: [
                {
                    ""key"": ""csharpsquid:S2757"",
                    ""repo"": ""csharpsquid"",
                    ""type"": ""BUG""
                },
                {
                    ""key"": ""csharpsquid:S1117"",
                    ""repo"": ""csharpsquid"",
                    ""type"": ""CODE_SMELL""
                }
            ]}";

            var rules = sut.GetRules("qp").Result;

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
        public void GetRules_EmptyActives()
        {
            downloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1"] = @"
            {
            total: 3,
            p: 1,
            ps: 500,
            rules: [
                {
                    ""key"": ""csharpsquid:S2757"",
                    ""repo"": ""csharpsquid"",
                    ""type"": ""BUG""
                },
                {
                    ""key"": ""csharpsquid:S1117"",
                    ""repo"": ""csharpsquid"",
                    ""type"": ""CODE_SMELL""
                }
            ],

            actives: {}
            }";

            var rules = sut.GetRules("qp").Result;

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
        public void GetRules_EscapeUrl()
        {
            downloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=my%23qp&p=1"] = @"
            {
            total: 3,
            p: 1,
            ps: 500,
            rules: [
                {
                    ""key"": ""csharpsquid:S2757"",
                    ""repo"": ""csharpsquid"",
                    ""type"": ""BUG""
                },
            ]}";

            var rules = sut.GetRules("my#qp").Result;

            rules.Should().ContainSingle();

            rules[0].RepoKey.Should().Be("csharpsquid");
            rules[0].RuleKey.Should().Be("S2757");
            rules[0].InternalKeyOrKey.Should().Be("S2757");
            rules[0].IsActive.Should().BeFalse();
        }

        [TestMethod]
        public async Task GetRules_RequestUrl()
        {
            var testDownloader = new TestDownloader();
            testDownloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=profile&p=1"] = "{ total: 1, p: 1, ps: 1, rules: [] }";
            sut = new SonarWebServerStub(testDownloader, version, logger, null);

            var rules = await sut.GetRules("profile");

            rules.Should().BeEmpty();
        }

        [TestMethod]
        public async Task GetInstalledPlugins()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.Download("api/languages/list", It.IsAny<bool>()))
                          .ReturnsAsync("{ languages: [{ key: \"cs\", name: \"C#\" }, { key: \"flex\", name: \"Flex\" } ]}");
            sut = new SonarWebServerStub(downloaderMock.Object, version, logger, null);
            var expected = new List<string> { "cs", "flex" };

            var actual = (await sut.GetAllLanguages()).ToList();

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
        public async Task TryDownloadEmbeddedFile_RequestedFileExists()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.TryDownloadFileIfExists(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(true);
            sut = new SonarWebServerStub(downloaderMock.Object, version, logger, null);

            var success = await sut.TryDownloadEmbeddedFile("csharp", "dummy.txt", Path.GetRandomFileName());

            success.Should().BeTrue("Expected success");
        }

        [TestMethod]
        public async Task TryDownloadEmbeddedFile_RequestedFileDoesNotExist()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.TryDownloadFileIfExists(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(false);
            sut = new SonarWebServerStub(downloaderMock.Object, version, logger, null);

            var success = await sut.TryDownloadEmbeddedFile("csharp", "dummy.txt", Path.GetRandomFileName());

            success.Should().BeFalse("Expected failure");
        }

        [TestMethod]
        public void GetServerVersion_ReturnsVersion()
        {
            const string expected = "4.2";
            sut = new SonarWebServerStub(downloader, new Version(expected), logger, null);

            sut.ServerVersion.ToString().Should().Be(expected);
        }

        [TestMethod]
        public async Task GetAllLanguages_RequestUrl()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.Download("api/languages/list", It.IsAny<bool>())).ReturnsAsync("{ languages: [ ] }");
            sut = new SonarWebServerStub(downloaderMock.Object, version, logger, null);

            var languages = await sut.GetAllLanguages();

            languages.Should().BeEmpty();
        }

        [TestMethod]
        [DataRow("http://myhost:222/")]
        [DataRow("http://myhost:222/sonar/")]
        public async Task TryDownloadEmbeddedFile_RequestUrl(string hostUrl)
        {
            const string pluginKey = "csharp";
            const string fileName = "file.txt";
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.TryDownloadFileIfExists(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(true);
            sut = new SonarWebServerStub(downloaderMock.Object, version, logger, null);

            var result = await sut.TryDownloadEmbeddedFile(pluginKey, fileName, Path.GetRandomFileName());

            result.Should().BeTrue();
        }

        private class SonarWebServerStub : SonarWebServer
        {
            public SonarWebServerStub(IDownloader downloader, Version serverVersion, ILogger logger, string organization)
                : base(downloader, serverVersion, logger, organization)
            {
            }

            public override Task<IList<SensorCacheEntry>> DownloadCache(ProcessedArgs localSettings) => throw new NotImplementedException();
            public override Task<bool> IsServerLicenseValid() => throw new NotImplementedException();
        }
    }
}
