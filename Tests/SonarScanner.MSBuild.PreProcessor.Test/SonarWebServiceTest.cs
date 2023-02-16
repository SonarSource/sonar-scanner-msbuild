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
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.PreProcessor.Protobuf;
using SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;
using SonarScanner.MSBuild.PreProcessor.WebService;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    // TODO Rename this, applies to Sonarqube
    [TestClass]
    public class SonarQubeWebServiceTest
    {
        private const string ProjectKey = "project-key";
        private const string ProjectBranch = "project-branch";

        private Uri serverUrl;
        private SonarQubeWebService ws;
        private TestDownloader downloader;
        private Uri uri;
        private Version version;
        private TestLogger logger;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Init()
        {
            serverUrl = new Uri("http://localhost/relative/");

            downloader = new TestDownloader();
            uri = new Uri("http://myhost:222");
            version = new Version("5.6");
            logger = new TestLogger();
            ws = new SonarQubeWebService(downloader, uri, version, logger);
        }

        [TestCleanup]
        public void Cleanup() =>
            ws?.Dispose();

        [TestMethod]
        public void Ctor_Null_Throws()
        {
            ((Func<SonarQubeWebService>)(() => new SonarQubeWebService(null, uri, version, logger))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("downloader");
            ((Func<SonarQubeWebService>)(() => new SonarQubeWebService(downloader, null, version, logger))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("serverUri");
            ((Func<SonarQubeWebService>)(() => new SonarQubeWebService(downloader, uri, null, logger))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("serverVersion");
            ((Func<SonarQubeWebService>)(() => new SonarQubeWebService(downloader, uri, version, null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void LogWSOnError()
        {
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=foo+bar")] = "trash";
            try
            {
                _ = ws.TryGetQualityProfile("foo bar", null, null, "cs").Result;
                Assert.Fail("Exception expected");
            }
            catch (Exception)
            {
                logger.AssertErrorLogged("Failed to request and parse 'http://myhost:222/api/qualityprofiles/search?project=foo+bar': Error parsing boolean value. Path '', line 1, position 2.");
            }
        }

        [DataTestMethod]
        [DataRow("7.9.0.5545", DisplayName = "7.9 LTS")]
        [DataRow("8.0.0.18670", DisplayName = "SonarCloud")]
        [DataRow("8.8.0.1121")]
        [DataRow("9.0.0.1121")]
        [DataRow("10.15.0.1121")]
        public void WarnIfDeprecated_ShouldNotWarn(string sqVersion)
        {
            ws = new SonarQubeWebService(downloader, uri, new Version(sqVersion), logger);

            ws.WarnIfSonarQubeVersionIsDeprecated();

            logger.Warnings.Should().BeEmpty();
        }

        [DataTestMethod]
        [DataRow("6.7.0.2232")]
        [DataRow("7.0.0.2232")]
        [DataRow("7.8.0.2232")]
        public void WarnIfDeprecated_ShouldWarn(string sqVersion)
        {
            ws = new SonarQubeWebService(downloader, uri, version, logger);
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = sqVersion;

            ws.WarnIfSonarQubeVersionIsDeprecated();

            logger.AssertSingleWarningExists("The version of SonarQube you are using is deprecated. Analyses will fail starting 6.0 release of the Scanner for .NET");
        }

        [TestMethod]
        public void IsLicenseValid_SonarQube_Commercial_AuthNotForced_LicenseIsInvalid()
        {
            ws = new SonarQubeWebService(downloader, uri, version, logger);
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = "8.5.1.34001";
            downloader.Pages[new Uri("http://myhost:222/api/editions/is_valid_license")] =
                @"{
                       ""isValidLicense"": false
                   }";

            ws.IsServerLicenseValid().Result.Should().BeFalse();
        }

        [TestMethod]
        public void IsLicenseValid_SonarQube_Commercial_AuthNotForced_LicenseIsValid()
        {
            ws = new SonarQubeWebService(downloader, uri, version, logger);
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = "8.5.1.34001";
            downloader.Pages[new Uri("http://myhost:222/api/editions/is_valid_license")] =
                @"{
                       ""isValidLicense"": true
                   }";

            ws.IsServerLicenseValid().Result.Should().BeTrue();
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void IsLicenseValid_SonarQube_Commercial_AuthForced_WithoutCredentials_ShouldThrow()
        {
            ws = new SonarQubeWebService(downloader, uri, version, logger);
            downloader.ConfigureGetLicenseInformationMock(HttpStatusCode.Unauthorized, string.Empty, false);

            _ = ws.IsServerLicenseValid().Result;

            logger.AssertErrorLogged("The token you provided doesn't have sufficient rights to check license.");
        }

        [TestMethod]
        public void IsLicenseValid_SonarQube_ServerNotLicensed()
        {
            ws = new SonarQubeWebService(downloader, uri, version, logger);
            downloader.ConfigureGetLicenseInformationMock(HttpStatusCode.NotFound, @"{
                       ""errors"":[{""msg"":""License not found""}]
                   }", false);

            ws.IsServerLicenseValid().Result.Should().BeFalse();
        }

        [TestMethod]
        public void IsLicenseValid_SonarQube_CE_SkipLicenseCheck()
        {
            ws = new SonarQubeWebService(downloader, uri, version, logger);
            downloader.ConfigureGetLicenseInformationMock(HttpStatusCode.NotFound, @"{""errors"":[{""msg"":""Unknown url: /api/editions/is_valid_license""}]}", true);

            ws.IsServerLicenseValid().Result.Should().BeTrue();
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException),
                "It seems that you are using an old version of SonarQube which is not supported anymore. Please update to at least 6.7.")]
        public void TryGetQualityProfile_MultipleQPForSameLanguage_ShouldThrow()
        {
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = "6.4";

            // Multiple QPs for a project, taking the default one.
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=foo+bar")] =
               "{ profiles: [{\"key\":\"profile1k\",\"name\":\"profile1\",\"language\":\"cs\", \"isDefault\": false}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"cs\", \"isDefault\": true}]}";
            _ = ws.TryGetQualityProfile("foo bar", null, null, "cs").Result;
        }

        [TestMethod]
        public void TryGetQualityProfile_SonarCloud_InvalidOrganizationKey()
        {
            var mockDownloader = new Mock<IDownloader>(MockBehavior.Strict);
            mockDownloader.Setup(x => x.TryDownloadIfExists(new Uri(serverUrl, $"api/qualityprofiles/search?project={ProjectKey}&organization=ThisIsInvalidValue"), false)).Returns(Task.FromResult(Tuple.Create(false, (string)null)));
            mockDownloader.Setup(x => x.Download(new Uri(serverUrl, "api/qualityprofiles/search?defaults=true&organization=ThisIsInvalidValue"), false)).Returns(Task.FromResult<string>(null));    // SC returns 404, WebClientDownloader returns null
            mockDownloader.Setup(x => x.Dispose());
            using (var service = new SonarQubeWebService(mockDownloader.Object, serverUrl, new Version("6.4"), logger))
            {
                Action a = () => _ = service.TryGetQualityProfile(ProjectKey, null, "ThisIsInvalidValue", "cs").Result;
                a.Should().Throw<AggregateException>().WithMessage("One or more errors occurred.");
                logger.AssertErrorLogged($"Failed to request and parse 'http://localhost/relative/api/qualityprofiles/search?defaults=true&organization=ThisIsInvalidValue': Cannot download quality profile. Check scanner arguments and the reported URL for more information.");
                logger.AssertErrorLogged($"Failed to request and parse 'http://localhost/relative/api/qualityprofiles/search?project=project-key&organization=ThisIsInvalidValue': Cannot download quality profile. Check scanner arguments and the reported URL for more information.");
            }
        }

        [TestMethod]
        public void TryGetQualityProfile_Sq64()
        {
            ws = new SonarQubeWebService(downloader, uri, new Version("6.4"), logger);
            Tuple<bool, string> result;

            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=foo+bar")] =
                "{ profiles: [{\"key\":\"profile1k\",\"name\":\"profile1\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=foo+bar%3AaBranch")] =
                "{ profiles: [{\"key\":\"profile2k\",\"name\":\"profile2\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=foo+bar%3AanotherBranch")] =
                "{ profiles: [{\"key\":\"profile3k\",\"name\":\"profile3\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";

            // main
            result = ws.TryGetQualityProfile("foo bar", null, null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile1k");

            // branch specific
            result = ws.TryGetQualityProfile("foo bar", "aBranch", null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile2k");

            result = ws.TryGetQualityProfile("foo bar", "anotherBranch", null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile3k");

            // with organizations
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=foo+bar&organization=my+org")] =
               "{ profiles: [{\"key\":\"profileOrganization\",\"name\":\"profile1\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("foo bar", null, "my org", "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profileOrganization");

            // fallback to defaults
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?defaults=true")] =
                "{ profiles: [{\"key\":\"profileDefault\",\"name\":\"profileDefault\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("non existing", null, null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profileDefault");

            // defaults with organizations
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?defaults=true&organization=my+org")] =
                       "{ profiles: [{\"key\":\"profileOrganizationDefault\",\"name\":\"profileDefault\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("non existing", null, "my org", "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profileOrganizationDefault");

            // no cs in list of profiles
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=java+foo+bar")] =
                "{ profiles: [{\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("java foo bar", null, null, "cs").Result;
            result.Item1.Should().BeFalse();
            result.Item2.Should().BeNull();

            // empty
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=empty+foo+bar")] =
                "{ profiles: []}";
            result = ws.TryGetQualityProfile("empty foo bar", null, null, "cs").Result;
            result.Item1.Should().BeFalse();
            result.Item2.Should().BeNull();
        }

        [TestMethod]
        public void TryGetQualityProfile_Sq56()
        {
            Tuple<bool, string> result;

            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=foo+bar")] =
                "{ profiles: [{\"key\":\"profile1k\",\"name\":\"profile1\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=foo+bar%3AaBranch")] =
                "{ profiles: [{\"key\":\"profile2k\",\"name\":\"profile2\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=foo+bar%3AanotherBranch")] =
                "{ profiles: [{\"key\":\"profile3k\",\"name\":\"profile3\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";

            // main
            result = ws.TryGetQualityProfile("foo bar", null, null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile1k");

            // branch specific
            result = ws.TryGetQualityProfile("foo bar", "aBranch", null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile2k");

            result = ws.TryGetQualityProfile("foo bar", "anotherBranch", null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile3k");

            // with organizations
            result = ws.TryGetQualityProfile("foo bar", null, "my org", "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile1k");

            // fallback to defaults
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?defaults=true")] =
                "{ profiles: [{\"key\":\"profileDefault\",\"name\":\"profileDefault\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("non existing", null, null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profileDefault");

            // defaults with organizations
            result = ws.TryGetQualityProfile("non existing", null, "my org", "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profileDefault");

            // no cs in list of profiles
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=java+foo+bar")] =
                "{ profiles: [{\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("java foo bar", null, null, "cs").Result;
            result.Item1.Should().BeFalse();
            result.Item2.Should().BeNull();

            // empty
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=empty+foo+bar")] =
                "{ profiles: []}";
            result = ws.TryGetQualityProfile("empty foo bar", null, null, "cs").Result;
            result.Item1.Should().BeFalse();
            result.Item2.Should().BeNull();
        }

        [TestMethod]
        public async Task TryGetQualityProfile_MissingProfiles()
        {
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=key")] = @"{""unexpected"": ""valid json""}";
            var (success, content) = await ws.TryGetQualityProfile("key", null, null, "cs");
            success.Should().BeFalse();
            content.Should().BeNull();
        }

        [TestMethod]
        public async Task TryGetQualityProfile_MissingKey()
        {
            downloader.Pages[new Uri("http://myhost:222/api/qualityprofiles/search?project=key")] = @"{ ""profiles"": [{""language"":""cs""}] }";
            var (success, content) = await ws.TryGetQualityProfile("key", null, null, "cs");
            success.Should().BeFalse();
            content.Should().BeNull();
        }

        [TestMethod]
        public void GetProperties_Sq63()
        {
            downloader.Pages[new Uri("http://myhost:222/api/settings/values?component=comp")] =
                @"{ settings: [
                  {
                    key: ""sonar.core.id"",
                    value: ""AVrrKaIfChAsLlov22f0"",
                    inherited: true
                  },
                  {
                    key: ""sonar.exclusions"",
                    values: [
                      ""myfile"",
                      ""myfile2""
                    ]
                  },
                  {
                    key: ""sonar.junit.reportsPath"",
                    value: ""testing.xml""
                  },
                  {
                    key: ""sonar.issue.ignore.multicriteria"",
                    fieldValues: [
                        {
                            resourceKey: ""prop1"",
                            ruleKey: """"
                        },
                        {
                            resourceKey: ""prop2"",
                            ruleKey: """"
                        }
                    ]
                  }
                ]}";

            ws = new SonarQubeWebService(downloader, uri, new Version("6.3"), logger);
            var result = ws.GetProperties("comp", null).Result;
            result.Should().HaveCount(7);
            result["sonar.exclusions"].Should().Be("myfile,myfile2");
            result["sonar.junit.reportsPath"].Should().Be("testing.xml");
            result["sonar.issue.ignore.multicriteria.1.resourceKey"].Should().Be("prop1");
            result["sonar.issue.ignore.multicriteria.1.ruleKey"].Should().Be(string.Empty);
            result["sonar.issue.ignore.multicriteria.2.resourceKey"].Should().Be("prop2");
            result["sonar.issue.ignore.multicriteria.2.ruleKey"].Should().Be(string.Empty);
        }

        [TestMethod]
        public async Task GetProperties_Sq63_NoComponentSettings_FallsBackToCommon()
        {
            downloader.Pages[new Uri("http://myhost:222/api/settings/values")] = @"{ settings: [ { key: ""key"", value: ""42"" } ]}";

            ws = new SonarQubeWebService(downloader, uri, new Version("8.9"), logger);
            var result = await ws.GetProperties("nonexistent-component", null);
            result.Should().ContainSingle().And.ContainKey("key");
            result["key"].Should().Be("42");
        }

        [TestMethod]
        public async Task GetProperties_Sq63_MissingValue_Throws()
        {
            downloader.Pages[new Uri("http://myhost:222/api/settings/values")] = @"{ settings: [ { key: ""key"" } ]}";

            ws = new SonarQubeWebService(downloader, uri, new Version("8.9"), logger);
            await ws.Invoking(async x => await x.GetProperties("nonexistent-component", null)).Should().ThrowAsync<ArgumentException>().WithMessage("Invalid property");
        }

        [TestMethod]
        public void GetRules_UseParamAsKey()
        {
            downloader.Pages[new Uri("http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1")] =
                @"{ total: 1, p: 1, ps: 1,
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
            }";

            var actual = ws.GetRules("qp").Result;
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
                downloader.Pages[new Uri($"http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p={page}")] = $@"
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

            var rules = ws.GetRules("qp").Result;

            rules.Should().HaveCount(40);
        }

        [TestMethod]
        public void GetRules()
        {
            downloader.Pages[new Uri("http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1")] =
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

            downloader.Pages[new Uri("http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=2")] =
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

            var actual = ws.GetRules("qp").Result;
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
        public void GetActiveRules_WhenActivesContainsRuleWithMultipleBodies_UseFirst()
        {
            // Arrange
            downloader.Pages[new Uri("http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1")] =
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

            var actual = ws.GetRules("qp").Result;

            // Assert
            actual.Should().HaveCount(1);
            actual.Single().IsActive.Should().BeTrue();
            actual.Single().RuleKey.Should().Be("OverwrittenId-First");
        }

        [TestMethod]
        public void GetRules_NoActives()
        {
            downloader.Pages[new Uri("http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1")] = @"
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

            var rules = ws.GetRules("qp").Result;

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
            downloader.Pages[new Uri("http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1")] = @"
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

            var rules = ws.GetRules("qp").Result;

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
            downloader.Pages[new Uri("http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=my%23qp&p=1")] = @"
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

            var rules = ws.GetRules("my#qp").Result;

            rules.Should().ContainSingle();

            rules[0].RepoKey.Should().Be("csharpsquid");
            rules[0].RuleKey.Should().Be("S2757");
            rules[0].InternalKeyOrKey.Should().Be("S2757");
            rules[0].IsActive.Should().BeFalse();
        }

        [TestMethod]
        public void GetProperties_NullProjectKey_Throws()
        {
            // Arrange
            var testSubject = new SonarQubeWebService(new TestDownloader(), uri, version, logger);
            Action act = () => _ = testSubject.GetProperties(null, null).Result;

            // Act & Assert
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("projectKey");
        }

        [TestMethod]
        public void GetProperties()
        {
            // This test includes a regression scenario for SONARMSBRU-187:
            // Requesting properties for project:branch should return branch-specific data

            // Check that properties are correctly defaulted as well as branch-specific
            downloader.Pages[new Uri("http://myhost:222/api/properties?resource=foo+bar")] =
                "[{\"key\": \"sonar.property1\",\"value\": \"value1\"},{\"key\": \"sonar.property2\",\"value\": \"value2\"},{\"key\": \"sonar.cs.msbuild.testProjectPattern\",\"value\": \"pattern\"}]";
            downloader.Pages[new Uri("http://myhost:222/api/properties?resource=foo+bar%3AaBranch")] =
                "[{\"key\": \"sonar.property1\",\"value\": \"anotherValue1\"},{\"key\": \"sonar.property2\",\"value\": \"anotherValue2\"}]";

            // default
            var expected1 = new Dictionary<string, string>
            {
                ["sonar.property1"] = "value1",
                ["sonar.property2"] = "value2",
                ["sonar.msbuild.testProjectPattern"] = "pattern"
            };
            var actual1 = ws.GetProperties("foo bar", null).Result;

            actual1.Should().HaveCount(expected1.Count);
            actual1.Should().NotBeSameAs(expected1);

            // branch specific
            var expected2 = new Dictionary<string, string>
            {
                ["sonar.property1"] = "anotherValue1",
                ["sonar.property2"] = "anotherValue2"
            };
            var actual2 = ws.GetProperties("foo bar", "aBranch").Result;

            actual2.Should().HaveCount(expected2.Count);
            actual2.Should().NotBeSameAs(expected2);
        }

        [TestMethod]
        public void GetInstalledPlugins()
        {
            downloader.Pages[new Uri("http://myhost:222/api/languages/list")] = "{ languages: [{ key: \"cs\", name: \"C#\" }, { key: \"flex\", name: \"Flex\" } ]}";
            var expected = new List<string>
            {
                "cs",
                "flex"
            };
            var actual = new List<string>(ws.GetAllLanguages().Result);

            expected.SequenceEqual(actual).Should().BeTrue();
        }

        [TestMethod]
        public async Task TryDownloadEmbeddedFile_NullPluginKey_Throws()
        {
            // Arrange
            var testSubject = new SonarQubeWebService(new TestDownloader(), uri, version, logger);
            Func<Task> act = async () => await testSubject.TryDownloadEmbeddedFile(null, "filename", "targetDir");

            // Act & Assert
            (await act.Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("pluginKey");
        }

        [TestMethod]
        public async Task TryDownloadEmbeddedFile_NullEmbeddedFileName_Throws()
        {
            // Arrange
            var testSubject = new SonarQubeWebService(new TestDownloader(), uri, version, logger);
            Func<Task> act = async () => await testSubject.TryDownloadEmbeddedFile("key", null, "targetDir");

            // Act & Assert
            (await act.Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("embeddedFileName");
        }

        [TestMethod]
        public async Task TryDownloadEmbeddedFile_NullTargetDirectory_Throws()
        {
            // Arrange
            var testSubject = new SonarQubeWebService(new TestDownloader(), uri, version, logger);
            Func<Task> act = async () => await testSubject.TryDownloadEmbeddedFile("pluginKey", "filename", null);

            // Act & Assert
            (await act.Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("targetDirectory");
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_RequestedFileExists()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            downloader.Pages[new Uri("http://myhost:222/static/csharp/dummy.txt")] = "dummy file content";

            // Act
            var success = ws.TryDownloadEmbeddedFile("csharp", "dummy.txt", testDir).Result;

            // Assert
            success.Should().BeTrue("Expected success");
            var expectedFilePath = Path.Combine(testDir, "dummy.txt");
            File.Exists(expectedFilePath).Should().BeTrue("Failed to download the expected file");
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_RequestedFileDoesNotExist()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            // Act
            var success = ws.TryDownloadEmbeddedFile("csharp", "dummy.txt", testDir).Result;

            // Assert
            success.Should().BeFalse("Expected failure");
            var expectedFilePath = Path.Combine(testDir, "dummy.txt");
            File.Exists(expectedFilePath).Should().BeFalse("File should not be created");
        }

        [TestMethod]
        public async Task GetProperties_Old_Forbidden()
        {
            var responseMock = new Mock<HttpWebResponse>();
            responseMock.SetupGet(x => x.StatusCode).Returns(HttpStatusCode.Forbidden);

            var downloaderMock = new Mock<IDownloader>();
            downloaderMock
                .Setup(x => x.Download(new Uri(serverUrl,$"api/properties?resource={ProjectKey}"), true))
                .Throws(new HttpRequestException("Forbidden"));

            var service = new SonarQubeWebService(downloaderMock.Object, serverUrl, new Version("1.2.3.4"), logger);

            Func<Task> action = async () => await service.GetProperties(ProjectKey, null);
            await action.Should().ThrowAsync<HttpRequestException>();

            logger.Errors.Should().HaveCount(1);
        }

        [TestMethod]
        public void GetProperties_Sq63plus_Forbidden()
        {
            var downloaderMock = new Mock<IDownloader>();

            downloaderMock
                .Setup(x => x.TryDownloadIfExists(new Uri(serverUrl, $"api/settings/values?component={ProjectKey}"), true))
                .Throws(new HttpRequestException("Forbidden"));

            var service = new SonarQubeWebService(downloaderMock.Object, serverUrl, new Version("6.3.0.0"), logger);

            Action action = () => _ = service.GetProperties(ProjectKey, null).Result;
            action.Should().Throw<HttpRequestException>();

            logger.Errors.Should().HaveCount(1);
        }

        [TestMethod]
        public async Task DownloadCache_NullArguments()
        {
            (await ws.Invoking(x => x.DownloadCache(null, "branch")).Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("projectKey");
            (await ws.Invoking(x => x.DownloadCache("key123", null)).Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("branch");
        }

        [TestMethod]
        public async Task DownloadCache_DeserializesMessage()
        {
            using var stream = CreateCacheStream(new SensorCacheEntry { Key = "key", Data = ByteString.CopyFromUtf8("value") });
            var sut = new SonarQubeWebService(MockIDownloader(stream), serverUrl, version, logger);

            var result = await sut.DownloadCache(ProjectKey, ProjectBranch);

            result.Should().ContainSingle();
            result.Single(x => x.Key == "key").Data.ToStringUtf8().Should().Be("value");
            logger.AssertDebugLogged("Downloading cache. Project key: project-key, branch: project-branch.");
        }

        [TestMethod]
        public async Task DownloadCache_WhenCacheStreamReadThrows_ReturnsEmptyCollection()
        {
            var streamMock = new Mock<Stream>();
            streamMock.Setup(x => x.Length).Throws<InvalidOperationException>();
            var sut = new SonarQubeWebService(MockIDownloader(streamMock.Object), serverUrl, version, logger);

            var result = await sut.DownloadCache(ProjectKey, ProjectBranch);

            result.Should().BeEmpty();
            logger.AssertDebugLogged("Incremental PR analysis: an error occurred while deserializing the cache entries! Operation is not valid due to the current state of the object.");
        }

        [TestMethod]
        public async Task DownloadCache_WhenDownloadStreamReturnsNull_ReturnsEmptyCollection()
        {
            var sut = new SonarQubeWebService(MockIDownloader(null), serverUrl, version, logger);

            var result = await sut.DownloadCache(ProjectKey, ProjectBranch);

            result.Should().BeEmpty();
        }

        [TestMethod]
        public async Task DownloadCache_WhenDownloadStreamThrows_ReturnsEmptyCollection()
        {
            var downloaderMock = Mock.Of<IDownloader>(x => x.DownloadStream(It.IsAny<Uri>()) == Task.FromException<Stream>(new HttpRequestException()));
            var sut = new SonarQubeWebService(downloaderMock, serverUrl, version, logger);

            var result = await sut.DownloadCache(ProjectKey, ProjectBranch);

            result.Should().BeEmpty();
        }

        [TestMethod]
        public void GetServerVersion_ReturnsVersion()
        {
            const string expected = "4.2";
            var sut = new SonarQubeWebService(MockIDownloader(null), uri, new Version(expected), logger);

            var actual = sut.GetServerVersion();

            actual.ToString().Should().Be(expected);
        }

        [DataTestMethod]
        // Specific profile
        [DataRow("http://myhost:222/", "http://myhost:222/api/qualityprofiles/search?project=foo")]
        [DataRow("http://myhost:222/sonar/", "http://myhost:222/sonar/api/qualityprofiles/search?project=foo")]
        // Default profile
        [DataRow("http://myhost:222/", "http://myhost:222/api/qualityprofiles/search?defaults=true")]
        [DataRow("http://myhost:222/sonar/", "http://myhost:222/sonar/api/qualityprofiles/search?defaults=true")]
        public async Task TryGetQualityProfile_RequestUrl(string hostUrl, string profileUrl)
        {
            var testDownloader = new TestDownloader();
            testDownloader.Pages[new Uri(profileUrl)] = @"{ profiles: [ { ""key"":""p1"", ""name"":""p1"", ""language"":""cs"", ""isDefault"": false } ] }";
            var sut = new SonarQubeWebService(testDownloader, new Uri(hostUrl), version, logger);

            var (result, profile) = await sut.TryGetQualityProfile("foo", null, null, "cs");

            result.Should().BeTrue();
            profile.Should().Be("p1");
        }

        [TestMethod]
        [DataRow("http://myhost:222/", "http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=profile&p=1")]
        [DataRow("http://myhost:222/sonar/", "http://myhost:222/sonar/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=profile&p=1")]
        public async Task GetRules_RequestUrl(string hostUrl, string qualityProfileUrl)
        {
            var testDownloader = new TestDownloader();
            testDownloader.Pages[new Uri(qualityProfileUrl)] = "{ total: 1, p: 1, ps: 1, rules: [] }";
            var sut = new SonarQubeWebService(testDownloader, new Uri(hostUrl), version, logger);

            var rules = await sut.GetRules("profile");

            rules.Should().BeEmpty();
        }

        [TestMethod]
        [DataRow("http://myhost:222/", "http://myhost:222/api/editions/is_valid_license")]
        [DataRow("http://myhost:222/sonar/", "http://myhost:222/sonar/api/editions/is_valid_license")]
        public async Task IsServerLicenseValid_RequestUrl(string hostUrl, string licenseUrl)
        {
            var testDownloader = new TestDownloader();
            testDownloader.Pages[new Uri(licenseUrl)] = @"{ ""isValidLicense"": true }";
            var sut = new SonarQubeWebService(testDownloader, new Uri(hostUrl), version, logger);

            var isValid = await sut.IsServerLicenseValid();

            isValid.Should().BeTrue();
        }

        [TestMethod]
        [DataRow("http://myhost:222/", "http://myhost:222/api/languages/list")]
        [DataRow("http://myhost:222/sonar/", "http://myhost:222/sonar/api/languages/list")]
        public async Task GetAllLanguages_RequestUrl(string hostUrl, string languagesUrl)
        {
            var testDownloader = new TestDownloader();
            testDownloader.Pages[new Uri(languagesUrl)] = "{ languages: [ ] }";
            var sut = new SonarQubeWebService(testDownloader, new Uri(hostUrl), version, logger);

            var languages = await sut.GetAllLanguages();

            languages.Should().BeEmpty();
        }

        [TestMethod]
        [DataRow("http://myhost:222/", "http://myhost:222/static/csharp/file.txt")]
        [DataRow("http://myhost:222/sonar/", "http://myhost:222/sonar/static/csharp/file.txt")]
        public async Task TryDownloadEmbeddedFile_RequestUrl(string hostUrl, string downloadUrl)
        {
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var testDownloader = new TestDownloader();
            testDownloader.Pages[new Uri(downloadUrl)] = "file content";
            var sut = new SonarQubeWebService(testDownloader, new Uri(hostUrl), version, logger);

            var result = await sut.TryDownloadEmbeddedFile("csharp", "file.txt", testDir);

            result.Should().BeTrue();
        }

        // TODO Check this test for urls without /
        [TestMethod]
        [DataRow("http://myhost:222", "http://myhost:222/api/analysis_cache/get?project=project-key&branch=project-branch")]
        [DataRow("http://myhost:222/", "http://myhost:222/api/analysis_cache/get?project=project-key&branch=project-branch")]
        [DataRow("http://myhost:222/sonar", "http://myhost:222/sonar/api/analysis_cache/get?project=project-key&branch=project-branch")]
        [DataRow("http://myhost:222/sonar/", "http://myhost:222/sonar/api/analysis_cache/get?project=project-key&branch=project-branch")]
        public async Task DownloadCache_RequestUrl(string hostUrl, string downloadUrl)
        {
            using Stream stream = new MemoryStream();
            var mockDownloader = Mock.Of<IDownloader>(x => x.DownloadStream(It.Is<Uri>(uri => uri.ToString() == downloadUrl)) == Task.FromResult(stream));
            var sut = new SonarQubeWebService(mockDownloader, new Uri(hostUrl), version, logger);

            var result = await sut.DownloadCache(ProjectKey, ProjectBranch);

            result.Should().BeEmpty();
        }

        [TestMethod]
        // Version newer or equal to 6.3, with project related properties
        [DataRow("http://myhost:222/", "6.3", "http://myhost:222/api/settings/values?component=key", "{ settings: [ ] }")]
        [DataRow("http://myhost:222/sonar/", "6.3", "http://myhost:222/sonar/api/settings/values?component=key", "{ settings: [ ] }")]
        // Version newer or equal to 6.3, without project related properties
        [DataRow("http://myhost:222/", "6.3", "http://myhost:222/api/settings/values", "{ settings: [ ] }")]
        [DataRow("http://myhost:222/sonar/", "6.3", "http://myhost:222/sonar/api/settings/values", "{ settings: [ ] }")]
        // Version older than 6.3
        [DataRow("http://myhost:222/", "6.2.9", "http://myhost:222/api/properties?resource=key", "[ ]")]
        [DataRow("http://myhost:222/sonar/", "6.2.9", "http://myhost:222/sonar/api/properties?resource=key", "[ ]")]
        public async Task GetProperties_RequestUrl(string hostUrl, string version, string propertiesUrl, string propertiesContent)
        {
            var testDownloader = new TestDownloader();
            testDownloader.Pages[new Uri(propertiesUrl)] = propertiesContent;
            var sut = new SonarQubeWebService(testDownloader, new Uri(hostUrl), new Version(version), logger);

            var properties = await sut.GetProperties("key", null);

            properties.Should().BeEmpty();
        }

        private static Stream CreateCacheStream(IMessage message)
        {
            var stream = new MemoryStream();
            message.WriteDelimitedTo(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        private static IDownloader MockIDownloader(Stream stream) =>
            Mock.Of<IDownloader>(x => x.DownloadStream(It.IsAny<Uri>()) == Task.FromResult(stream));
    }
}
