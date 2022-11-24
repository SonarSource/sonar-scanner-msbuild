/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class SonarWebServiceTest
    {
        private const string ServerUrl = "http://localhost";
        private const string ProjectKey = "project-key";
        private const string ProjectBranch = "project-branch";

        private TestDownloader downloader;
        private SonarWebService ws;
        private TestLogger logger;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Init()
        {
            downloader = new TestDownloader();
            logger = new TestLogger();
            ws = new SonarWebService(downloader, "http://myhost:222", logger);
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = "5.6";
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (ws != null)
            {
                ws.Dispose();
            }
        }

        [TestMethod]
        public void Ctor_NullServer_Throws()
        {
            // Arrange
            Action act = () => new SonarWebService(new TestDownloader(), null, new TestLogger());

            // Act & Assert
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("server");
        }

        [TestMethod]
        public void Ctor_NullLogger_Throws()
        {
            // Arrange
            Action act = () => new SonarWebService(new TestDownloader(), "http://localhost:9000", null);

            // Act & Assert
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
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
        [DataRow("7.9.0.5545", DisplayName ="7.9 LTS")]
        [DataRow("8.0.0.18670", DisplayName = "SonarCloud" )]
        [DataRow("8.8.0.1121")]
        [DataRow("9.0.0.1121")]
        [DataRow("10.15.0.1121")]
        public void WarnIfDeprecated_ShouldNotWarn(string sqVersion)
        {
            ws = new SonarWebService(downloader, "http://myhost:222", logger);
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = sqVersion;

            ws.WarnIfSonarQubeVersionIsDeprecated().Wait();

            logger.Warnings.Should().BeEmpty();
        }

        [DataTestMethod]
        [DataRow("6.7.0.2232")]
        [DataRow("7.0.0.2232")]
        [DataRow("7.8.0.2232")]
        public void WarnIfDeprecated_ShouldWarn(string sqVersion)
        {
            ws = new SonarWebService(downloader, "http://myhost:222", logger);
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = sqVersion;

            ws.WarnIfSonarQubeVersionIsDeprecated().Wait();

            logger.AssertSingleWarningExists("The version of SonarQube you are using is deprecated. Analyses will fail starting 6.0 release of the Scanner for .NET");
        }

        [TestMethod]
        public void IsLicenseValid_IsSonarCloud_ShouldReturnTrue()
        {
            ws = new SonarWebService(downloader, "http://myhost:222", logger);
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = "8.0.0.68001";

            ws.IsServerLicenseValid().Result.Should().BeTrue();
        }

        [TestMethod]
        public void IsLicenseValid_SonarQube_Commercial_AuthNotForced_LicenseIsInvalid()
        {
            ws = new SonarWebService(downloader, "http://myhost:222", logger);
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
            ws = new SonarWebService(downloader, "http://myhost:222", logger);
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
            ws = new SonarWebService(downloader, "http://myhost:222", logger);
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = "8.5.1.34001";
            downloader.ConfigureGetLicenseInformationMock(HttpStatusCode.Unauthorized, string.Empty, false);

            _ = ws.IsServerLicenseValid().Result;

            logger.AssertErrorLogged("The token you provided doesn't have sufficient rights to check license.");
        }

        [TestMethod]
        public void IsLicenseValid_SonarQube_ServerNotLicensed()
        {
            ws = new SonarWebService(downloader, "http://myhost:222", logger);
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = "8.5.1.34001";
            downloader.ConfigureGetLicenseInformationMock(HttpStatusCode.NotFound, @"{
                       ""errors"":[{""msg"":""License not found""}]
                   }", false);

            ws.IsServerLicenseValid().Result.Should().BeFalse();
        }

        [TestMethod]
        public void IsLicenseValid_SonarQube_CE_SkipLicenseCheck()
        {
            ws = new SonarWebService(downloader, "http://myhost:222", logger);
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = "8.5.1.34001";
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
            mockDownloader.Setup(x => x.Download(new Uri($"{ServerUrl}/api/server/version"), false)).Returns(Task.FromResult("8.0.0.22548"));
            mockDownloader.Setup(x => x.TryDownloadIfExists(new Uri($"{ServerUrl}/api/qualityprofiles/search?project={ProjectKey}&organization=ThisIsInvalidValue"), false)).Returns(Task.FromResult(Tuple.Create(false, (string)null)));
            mockDownloader.Setup(x => x.Download(new Uri($"{ServerUrl}/api/qualityprofiles/search?defaults=true&organization=ThisIsInvalidValue"), false)).Returns(Task.FromResult<string>(null));    // SC returns 404, WebClientDownloader returns null
            mockDownloader.Setup(x => x.Dispose());
            using (var service = new SonarWebService(mockDownloader.Object, ServerUrl, logger))
            {
                Action a = () => _ = service.TryGetQualityProfile(ProjectKey, null, "ThisIsInvalidValue", "cs").Result;
                a.Should().Throw<AggregateException>().WithMessage("One or more errors occurred.");
                logger.AssertErrorLogged($"Failed to request and parse 'http://localhost/api/qualityprofiles/search?defaults=true&organization=ThisIsInvalidValue': Cannot download quality profile. Check scanner arguments and the reported URL for more information.");
                logger.AssertErrorLogged($"Failed to request and parse 'http://localhost/api/qualityprofiles/search?project=project-key&organization=ThisIsInvalidValue': Cannot download quality profile. Check scanner arguments and the reported URL for more information.");
            }
        }

        [TestMethod]
        public void TryGetQualityProfile64()
        {
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = "6.4";
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
        public void TryGetQualityProfile56()
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
        public void GetSettingsSq63()
        {
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = "6.3-SNAPSHOT";
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
            var result = ws.GetProperties("comp", null).Result;
            result.Should().HaveCount(7);
#pragma warning disable CollectionShouldHaveElementAt // Simplify Assertion
            result["sonar.exclusions"].Should().Be("myfile,myfile2");
            result["sonar.junit.reportsPath"].Should().Be("testing.xml");
            result["sonar.issue.ignore.multicriteria.1.resourceKey"].Should().Be("prop1");
            result["sonar.issue.ignore.multicriteria.1.ruleKey"].Should().Be(string.Empty);
            result["sonar.issue.ignore.multicriteria.2.resourceKey"].Should().Be("prop2");
            result["sonar.issue.ignore.multicriteria.2.ruleKey"].Should().Be(string.Empty);
#pragma warning restore CollectionShouldHaveElementAt // Simplify Assertion
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
            var testSubject = new SonarWebService(new TestDownloader(), "http://myserver", new TestLogger());
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
            var testSubject = new SonarWebService(new TestDownloader(), "http://myserver", new TestLogger());
            Func<Task> act = async () => await testSubject.TryDownloadEmbeddedFile(null, "filename", "targetDir");

            // Act & Assert
            (await act.Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("pluginKey");
        }

        [TestMethod]
        public async Task TryDownloadEmbeddedFile_NullEmbeddedFileName_Throws()
        {
            // Arrange
            var testSubject = new SonarWebService(new TestDownloader(), "http://myserver", new TestLogger());
            Func<Task> act = async () => await testSubject.TryDownloadEmbeddedFile("key", null, "targetDir");

            // Act & Assert
            (await act.Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("embeddedFileName");
        }

        [TestMethod]
        public async Task TryDownloadEmbeddedFile_NullTargetDirectory_Throws()
        {
            // Arrange
            var testSubject = new SonarWebService(new TestDownloader(), "http://myserver", new TestLogger());
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
                .Setup(x => x.Download(new Uri($"{ServerUrl}/api/server/version"), false))
                .Returns(Task.FromResult("1.2.3.4"));
            downloaderMock
                .Setup(x => x.Download(new Uri($"{ServerUrl}/api/properties?resource={ProjectKey}"), true))
                .Throws(new HttpRequestException("Forbidden"));

            var service = new SonarWebService(downloaderMock.Object, ServerUrl, logger);

            Func<Task> action = async () => await service.GetProperties(ProjectKey, null);
            await action.Should().ThrowAsync<HttpRequestException>();

            logger.Errors.Should().HaveCount(1);
        }

        [TestMethod]
        public void GetProperties_63plus_Forbidden()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock
                .Setup(x => x.Download(new Uri($"{ServerUrl}/api/server/version"), false))
                .Returns(Task.FromResult("6.3.0.0"));

            downloaderMock
                .Setup(x => x.TryDownloadIfExists(new Uri($"{ServerUrl}/api/settings/values?component={ProjectKey}"), true))
                .Throws(new HttpRequestException("Forbidden"));

            var service = new SonarWebService(downloaderMock.Object, ServerUrl, logger);

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
            using var stream = CreateCacheStream(new AnalysisCacheMsg { Map = { { "key", ByteString.CopyFromUtf8("value") } } });
            var sut = new SonarWebService(MockIDownloader(stream), ServerUrl, logger);

            var result = await sut.DownloadCache(ProjectKey, ProjectBranch);

            result.Map.Count.Should().Be(1);
            result.Map["key"].ToStringUtf8().Should().Be("value");
            logger.AssertDebugLogged("Downloading cache. Project key: project-key, branch: project-branch.");
        }

        private static Stream CreateCacheStream(IMessage message)
        {
            var stream = new MemoryStream();
            message.WriteTo(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        private static IDownloader MockIDownloader(Stream stream) =>
            Mock.Of<IDownloader>(x => x.DownloadStream(It.IsAny<Uri>()) == Task.FromResult(stream));

        private sealed class TestDownloader : IDownloader
        {
            public IDictionary<Uri, string> Pages = new Dictionary<Uri, string>();
            public List<Uri> AccessedUrls = new List<Uri>();

            private string expectedReturnMessage;
            private HttpStatusCode expectedHttpStatusCode;
            private bool isCEEdition;

            public Task<Tuple<bool, string>> TryDownloadIfExists(Uri url, bool logPermissionDenied = false)
            {
                AccessedUrls.Add(url);
                return Pages.ContainsKey(url)
                    ? Task.FromResult(new Tuple<bool, string>(true, Pages[url]))
                    : Task.FromResult(new Tuple<bool, string>(false, null));
            }

            public Task<string> Download(Uri url, bool logPermissionDenied = false)
            {
                AccessedUrls.Add(url);
                return Pages.ContainsKey(url)
                    ? Task.FromResult(Pages[url])
                    : throw new ArgumentException("Cannot find URL " + url);
            }

            public Task<Stream> DownloadStream(Uri url) =>
                throw new NotImplementedException();

            public Task<bool> TryDownloadFileIfExists(Uri url, string targetFilePath, bool logPermissionDenied = false)
            {
                AccessedUrls.Add(url);

                if (Pages.ContainsKey(url))
                {
                    File.WriteAllText(targetFilePath, Pages[url]);
                    return Task.FromResult(true);
                }
                else
                {
                    return Task.FromResult(false);
                }
            }

            public void Dispose()
            {
                // Nothing to do here
            }

            public void ConfigureGetLicenseInformationMock(HttpStatusCode expectedStatusCode, string expectedReturnMessage, bool isCEEdition)
            {
                expectedHttpStatusCode = expectedStatusCode;
                this.expectedReturnMessage = expectedReturnMessage;
                this.isCEEdition = isCEEdition;
            }

            public Task<HttpResponseMessage> TryGetLicenseInformation(Uri url)
            {
                AccessedUrls.Add(url);
                if (Pages.ContainsKey(url))
                {
                    // returns 200
                    return Task.FromResult(new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(Pages[url])
                    });
                }
                else
                {
                    // returns either 404 or 401
                    if (expectedHttpStatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new ArgumentException("The token you provided doesn't have sufficient rights to check license.");
                    }

                    if (expectedHttpStatusCode == HttpStatusCode.NotFound)
                    {
                        if (isCEEdition)
                        {
                            return Task.FromResult(new HttpResponseMessage()
                            {
                                StatusCode = HttpStatusCode.NotFound,
                                Content = new StringContent(@"{""errors"":[{""msg"":""Unknown url: /api/editions/is_valid_license""}]} ")
                            });
                        }
                        else
                        {
                            return Task.FromResult(new HttpResponseMessage()
                            {
                                StatusCode = HttpStatusCode.NotFound,
                                Content = new StringContent(@"{ ""errors"" : [ { ""msg"": ""License not found"" } ] } ")
                            });
                        }
                    }

                    return Task.FromResult<HttpResponseMessage>(null);
                }
            }
        }
    }
}
