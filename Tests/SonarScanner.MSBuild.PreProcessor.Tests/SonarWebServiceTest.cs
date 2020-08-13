/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.UnitTests
{
    [TestClass]
    public class SonarWebServiceTest
    {
        private TestDownloader downloader;
        private SonarWebService ws;
        private TestLogger logger;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Init()
        {
            this.downloader = new TestDownloader();
            this.logger = new TestLogger();
            this.ws = new SonarWebService(this.downloader, "http://myhost:222", this.logger);
            this.downloader.Pages["http://myhost:222/api/server/version"] = "5.6";
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (this.ws != null)
            {
                this.ws.Dispose();
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
            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar"] = "trash";
            try
            {
                _ = this.ws.TryGetQualityProfile("foo bar", null, null, "cs").Result;
                Assert.Fail("Exception expected");
            }
            catch (Exception)
            {
                this.logger.AssertErrorLogged("Failed to request and parse 'http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar': Error parsing boolean value. Path '', line 1, position 2.");
            }
        }

        [TestMethod]
        public void TryGetQualityProfile64()
        {
            this.downloader.Pages["http://myhost:222/api/server/version"] = "6.4";
            Tuple<bool, string> result;

            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar"] =
                "{ profiles: [{\"key\":\"profile1k\",\"name\":\"profile1\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar%3AaBranch"] =
                "{ profiles: [{\"key\":\"profile2k\",\"name\":\"profile2\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar%3AanotherBranch"] =
                "{ profiles: [{\"key\":\"profile3k\",\"name\":\"profile3\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";

            // main
            result = this.ws.TryGetQualityProfile("foo bar", null, null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile1k");

            // branch specific
            result = this.ws.TryGetQualityProfile("foo bar", "aBranch", null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile2k");

            result = this.ws.TryGetQualityProfile("foo bar", "anotherBranch", null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile3k");

            // with organizations
            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar&organization=my+org"] =
               "{ profiles: [{\"key\":\"profileOrganization\",\"name\":\"profile1\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = this.ws.TryGetQualityProfile("foo bar", null, "my org", "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profileOrganization");

            // fallback to defaults
            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?defaults=true"] =
                "{ profiles: [{\"key\":\"profileDefault\",\"name\":\"profileDefault\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = this.ws.TryGetQualityProfile("non existing", null, null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profileDefault");

            // defaults with organizations
            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?defaults=true&organization=my+org"] =
                       "{ profiles: [{\"key\":\"profileOrganizationDefault\",\"name\":\"profileDefault\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = this.ws.TryGetQualityProfile("non existing", null, "my org", "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profileOrganizationDefault");

            // no cs in list of profiles
            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=java+foo+bar"] =
                "{ profiles: [{\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = this.ws.TryGetQualityProfile("java foo bar", null, null, "cs").Result;
            result.Item1.Should().BeFalse();
            result.Item2.Should().BeNull();

            // empty
            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=empty+foo+bar"] =
                "{ profiles: []}";
            result = this.ws.TryGetQualityProfile("empty foo bar", null, null, "cs").Result;
            result.Item1.Should().BeFalse();
            result.Item2.Should().BeNull();
        }

        [TestMethod]
        public void TryGetQualityProfile56()
        {
            Tuple<bool, string> result;

            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar"] =
                "{ profiles: [{\"key\":\"profile1k\",\"name\":\"profile1\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar%3AaBranch"] =
                "{ profiles: [{\"key\":\"profile2k\",\"name\":\"profile2\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar%3AanotherBranch"] =
                "{ profiles: [{\"key\":\"profile3k\",\"name\":\"profile3\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";

            // main
            result = this.ws.TryGetQualityProfile("foo bar", null, null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile1k");

            // branch specific
            result = this.ws.TryGetQualityProfile("foo bar", "aBranch", null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile2k");

            result = this.ws.TryGetQualityProfile("foo bar", "anotherBranch", null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile3k");

            // with organizations
            result = this.ws.TryGetQualityProfile("foo bar", null, "my org", "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profile1k");

            // fallback to defaults
            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?defaults=true"] =
                "{ profiles: [{\"key\":\"profileDefault\",\"name\":\"profileDefault\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = this.ws.TryGetQualityProfile("non existing", null, null, "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profileDefault");

            // defaults with organizations
            result = this.ws.TryGetQualityProfile("non existing", null, "my org", "cs").Result;
            result.Item1.Should().BeTrue();
            result.Item2.Should().Be("profileDefault");

            // no cs in list of profiles
            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=java+foo+bar"] =
                "{ profiles: [{\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = this.ws.TryGetQualityProfile("java foo bar", null, null, "cs").Result;
            result.Item1.Should().BeFalse();
            result.Item2.Should().BeNull();

            // empty
            this.downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=empty+foo+bar"] =
                "{ profiles: []}";
            result = this.ws.TryGetQualityProfile("empty foo bar", null, null, "cs").Result;
            result.Item1.Should().BeFalse();
            result.Item2.Should().BeNull();
        }

        [TestMethod]
        public void GetSettingsSq63()
        {
            this.downloader.Pages["http://myhost:222/api/server/version"] = "6.3-SNAPSHOT";
            this.downloader.Pages["http://myhost:222/api/settings/values?component=comp"] =
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
            var result = this.ws.GetProperties("comp", null).Result;
            result.Should().HaveCount(7);
#pragma warning disable CollectionShouldHaveElementAt // Simplify Assertion
            result["sonar.exclusions"].Should().Be("myfile,myfile2");
            result["sonar.junit.reportsPath"].Should().Be("testing.xml");
            result["sonar.issue.ignore.multicriteria.1.resourceKey"].Should().Be("prop1");
            result["sonar.issue.ignore.multicriteria.1.ruleKey"].Should().Be("");
            result["sonar.issue.ignore.multicriteria.2.resourceKey"].Should().Be("prop2");
            result["sonar.issue.ignore.multicriteria.2.ruleKey"].Should().Be("");
#pragma warning restore CollectionShouldHaveElementAt // Simplify Assertion
        }

        [TestMethod]
        public void GetActiveRules_UseParamAsKey()
        {
            this.downloader.Pages["http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&activation=true&qprofile=qp&p=1"] =
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

            var actual = this.ws.GetActiveRules("qp").Result;
            actual.Should().ContainSingle();

            actual[0].RepoKey.Should().Be("vbnet");
            actual[0].RuleKey.Should().Be("OverwrittenId");
            actual[0].InternalKeyOrKey.Should().Be("OverwrittenId");
            actual[0].TemplateKey.Should().BeNull();
            actual[0].Parameters.Should().HaveCount(1);
        }

        [TestMethod]
        public void GetActiveRules()
        {
            this.downloader.Pages["http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&activation=true&qprofile=qp&p=1"] =
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
            }],

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

            this.downloader.Pages["http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&activation=true&qprofile=qp&p=2"] =
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

            var actual = this.ws.GetActiveRules("qp").Result;
            actual.Should().HaveCount(3);

            actual[0].RepoKey.Should().Be("vbnet");
            actual[0].RuleKey.Should().Be("S2368");
            actual[0].InternalKeyOrKey.Should().Be("S2368");
            actual[0].TemplateKey.Should().BeNull();
            actual[0].Parameters.Should().HaveCount(0);

            actual[1].RepoKey.Should().Be("common-vbnet");
            actual[1].RuleKey.Should().Be("InsufficientCommentDensity");
            actual[1].InternalKeyOrKey.Should().Be("InsufficientCommentDensity.internal");
            actual[1].TemplateKey.Should().Be("dummy.template.key");
            actual[1].Parameters.Should().HaveCount(1);
            actual[1].Parameters.First().Should().Be(new KeyValuePair<string, string>("minimumCommentDensity", "50"));

            actual[2].RepoKey.Should().Be("vbnet");
            actual[2].RuleKey.Should().Be("S2346");
            actual[2].InternalKeyOrKey.Should().Be("S2346");
            actual[2].TemplateKey.Should().BeNull();
            actual[2].Parameters.Should().HaveCount(0);
        }

        [TestMethod]
        public void GetActiveRules_WhenActivesDoesNotContainRule_ThrowsJsonException()
        {
            // Arrange
            this.downloader.Pages["http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&activation=true&qprofile=qp&p=1"] =
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
                ""key2"": [
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

            Func<Task> act = async() => await this.ws.GetActiveRules("qp");

            // Act &  Assert
            act.Should().ThrowExactly<JsonException>().WithMessage("Malformed json response, \"actives\" field should contain rule 'key1'");
        }

        [TestMethod]
        public void GetActiveRules_WhenActivesContainsRuleWithEmptyBody_ThrowsJsonException()
        {
            // Arrange
            this.downloader.Pages["http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&activation=true&qprofile=qp&p=1"] =
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
                ""key1"": []
            }
            }";

            Func<Task> act = async() => await this.ws.GetActiveRules("qp");

            // Act &  Assert
            act.Should().ThrowExactly<JsonException>().WithMessage("Malformed json response, \"actives\" field should contain rule 'key1'");
        }

        [TestMethod]
        public void GetActiveRules_WhenActivesContainsRuleWithMultipleBodies_ThrowsJsonException()
        {
            // Arrange
            this.downloader.Pages["http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&activation=true&qprofile=qp&p=1"] =
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
                      value: ""OverwrittenId"",
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
                      value: ""OverwrittenId"",
                      type: ""FLOAT""
                    }
                    ]
                }
                ]
            }
            }";

            Func<Task> act = async() => await this.ws.GetActiveRules("qp");

            // Act &  Assert
            act.Should().ThrowExactly<JsonException>().WithMessage("Malformed json response, \"actives\" field should contain rule 'key1'");
        }

        [TestMethod]
        public void GetInactiveRulesAndEscapeUrl()
        {
            this.downloader.Pages["http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params&ps=500&activation=false&qprofile=my%23qp&p=1&languages=cs"] = @"
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
                },
                {
                    ""key"": ""csharpsquid:S1764"",
                    ""repo"": ""csharpsquid"",
                    ""type"": ""BUG""
                }
            ]}";

            var rules = this.ws.GetInactiveRules("my#qp", "cs").Result;

            rules.Should().HaveCount(3);

            rules[0].RepoKey.Should().Be("csharpsquid");
            rules[0].RuleKey.Should().Be("S2757");
            rules[0].InternalKeyOrKey.Should().Be("S2757");

            rules[1].RepoKey.Should().Be("csharpsquid");
            rules[1].RuleKey.Should().Be("S1117");
            rules[1].InternalKeyOrKey.Should().Be("S1117");

            rules[2].RepoKey.Should().Be("csharpsquid");
            rules[2].RuleKey.Should().Be("S1764");
            rules[2].InternalKeyOrKey.Should().Be("S1764");
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
            this.downloader.Pages["http://myhost:222/api/properties?resource=foo+bar"] =
                "[{\"key\": \"sonar.property1\",\"value\": \"value1\"},{\"key\": \"sonar.property2\",\"value\": \"value2\"},{\"key\": \"sonar.cs.msbuild.testProjectPattern\",\"value\": \"pattern\"}]";
            this.downloader.Pages["http://myhost:222/api/properties?resource=foo+bar%3AaBranch"] =
                "[{\"key\": \"sonar.property1\",\"value\": \"anotherValue1\"},{\"key\": \"sonar.property2\",\"value\": \"anotherValue2\"}]";

            // default
            var expected1 = new Dictionary<string, string>
            {
                ["sonar.property1"] = "value1",
                ["sonar.property2"] = "value2",
                ["sonar.msbuild.testProjectPattern"] = "pattern"
            };
            var actual1 = this.ws.GetProperties("foo bar", null).Result;

            actual1.Should().HaveCount(expected1.Count);
            actual1.Should().NotBeSameAs(expected1);

            // branch specific
            var expected2 = new Dictionary<string, string>
            {
                ["sonar.property1"] = "anotherValue1",
                ["sonar.property2"] = "anotherValue2"
            };
            var actual2 = this.ws.GetProperties("foo bar", "aBranch").Result;

            actual2.Should().HaveCount(expected2.Count);
            actual2.Should().NotBeSameAs(expected2);
        }

        [TestMethod]
        public void GetInstalledPlugins()
        {
            this.downloader.Pages["http://myhost:222/api/languages/list"] = "{ languages: [{ key: \"cs\", name: \"C#\" }, { key: \"flex\", name: \"Flex\" } ]}";
            var expected = new List<string>
            {
                "cs",
                "flex"
            };
            var actual = new List<string>(this.ws.GetAllLanguages().Result);

            expected.SequenceEqual(actual).Should().BeTrue();
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_NullPluginKey_Throws()
        {
            // Arrange
            var testSubject = new SonarWebService(new TestDownloader(), "http://myserver", new TestLogger());
            Func<Task> act = async() => await testSubject.TryDownloadEmbeddedFile(null, "filename", "targetDir");

            // Act & Assert
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("pluginKey");
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_NullEmbeddedFileName_Throws()
        {
            // Arrange
            var testSubject = new SonarWebService(new TestDownloader(), "http://myserver", new TestLogger());
            Func<Task> act = async() => await testSubject.TryDownloadEmbeddedFile("key", null, "targetDir");

            // Act & Assert
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("embeddedFileName");
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_NullTargetDirectory_Throws()
        {
            // Arrange
            var testSubject = new SonarWebService(new TestDownloader(), "http://myserver", new TestLogger());
            Func<Task> act = async() => await testSubject.TryDownloadEmbeddedFile("pluginKey", "filename", null);

            // Act & Assert
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("targetDirectory");
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_RequestedFileExists()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            this.downloader.Pages["http://myhost:222/static/csharp/dummy.txt"] = "dummy file content";

            // Act
            var success = this.ws.TryDownloadEmbeddedFile("csharp", "dummy.txt", testDir).Result;

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
            var success = this.ws.TryDownloadEmbeddedFile("csharp", "dummy.txt", testDir).Result;

            // Assert
            success.Should().BeFalse("Expected failure");
            var expectedFilePath = Path.Combine(testDir, "dummy.txt");
            File.Exists(expectedFilePath).Should().BeFalse("File should not be created");
        }

        [TestMethod]
        public void GetProperties_Old_Forbidden()
        {
            const string serverUrl = "http://localhost";
            const string projectKey = "my-project";

            var responseMock = new Mock<HttpWebResponse>();
            responseMock.SetupGet(x => x.StatusCode).Returns(HttpStatusCode.Forbidden);

            var downloaderMock = new Mock<IDownloader>();
            downloaderMock
                .Setup(x => x.Download($"{serverUrl}/api/server/version", false))
                .Returns(Task.FromResult("1.2.3.4"));
            downloaderMock
                .Setup(x => x.Download($"{serverUrl}/api/properties?resource={projectKey}", true))
                .Throws(new HttpRequestException("Forbidden"));

            var service = new SonarWebService(downloaderMock.Object, serverUrl, this.logger);

            Func<Task> action = async() => await service.GetProperties(projectKey, null);
            action.Should().Throw<HttpRequestException>();

            this.logger.Errors.Should().HaveCount(1);
        }

        [TestMethod]
        public void GetProperties_63plus_Forbidden()
        {
            const string serverUrl = "http://localhost";
            const string projectKey = "my-project";

            var downloaderMock = new Mock<IDownloader>();
            downloaderMock
                .Setup(x => x.Download($"{serverUrl}/api/server/version", false))
                .Returns(Task.FromResult("6.3.0.0"));

            downloaderMock
                .Setup(x => x.TryDownloadIfExists($"{serverUrl}/api/settings/values?component={projectKey}", true))
                .Throws(new HttpRequestException("Forbidden"));

            var service = new SonarWebService(downloaderMock.Object, serverUrl, this.logger);

            Action action = () => _ = service.GetProperties(projectKey, null).Result;
            action.Should().Throw<HttpRequestException>();

            this.logger.Errors.Should().HaveCount(1);
        }

        private sealed class TestDownloader : IDownloader
        {
            public IDictionary<string, string> Pages = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            public List<string> AccessedUrls = new List<string>();

            public Task<Tuple<bool, string>> TryDownloadIfExists(string url, bool logPermissionDenied = false)
            {
                this.AccessedUrls.Add(url);
                if (this.Pages.ContainsKey(url))
                {
                    return Task.FromResult(new Tuple<bool, string>(true, this.Pages[url]));
                }
                else
                {
                    return Task.FromResult(new Tuple<bool, string>(false, null));
                }
            }

            public Task<string> Download(string url, bool logPermissionDenied = false)
            {
                this.AccessedUrls.Add(url);
                if (this.Pages.ContainsKey(url))
                {
                    return Task.FromResult(this.Pages[url]);
                }
                throw new ArgumentException("Cannot find URL " + url);
            }

            public Task<bool> TryDownloadFileIfExists(string url, string targetFilePath, bool logPermissionDenied = false)
            {
                this.AccessedUrls.Add(url);

                if (this.Pages.ContainsKey(url))
                {
                    File.WriteAllText(targetFilePath, this.Pages[url]);
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
        }
    }
}
