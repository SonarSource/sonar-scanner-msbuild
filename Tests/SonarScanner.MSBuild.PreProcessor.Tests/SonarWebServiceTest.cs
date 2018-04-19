/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
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
            downloader = new TestDownloader();
            logger = new TestLogger();
            ws = new SonarWebService(downloader, "http://myhost:222", logger);
            downloader.Pages["http://myhost:222/api/server/version"] = "5.6";
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
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar"] = "trash";
            try
            {
                var result = ws.TryGetQualityProfile("foo bar", null, null, "cs", out string qualityProfile);
                Assert.Fail("Exception expected");
            }
            catch (Exception)
            {
                logger.AssertErrorLogged("Failed to request and parse 'http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar': Error parsing boolean value. Path '', line 1, position 2.");
            }
        }

        [TestMethod]
        public void TryGetQualityProfile64()
        {
            downloader.Pages["http://myhost:222/api/server/version"] = "6.4";
            bool result;

            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar"] =
                "{ profiles: [{\"key\":\"profile1k\",\"name\":\"profile1\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar%3AaBranch"] =
                "{ profiles: [{\"key\":\"profile2k\",\"name\":\"profile2\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar%3AanotherBranch"] =
                "{ profiles: [{\"key\":\"profile3k\",\"name\":\"profile3\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";

            // main
            result = ws.TryGetQualityProfile("foo bar", null, null, "cs", out string qualityProfile);
            result.Should().BeTrue();
            qualityProfile.Should().Be("profile1k");

            // branch specific
            result = ws.TryGetQualityProfile("foo bar", "aBranch", null, "cs", out qualityProfile);
            result.Should().BeTrue();
            qualityProfile.Should().Be("profile2k");

            result = ws.TryGetQualityProfile("foo bar", "anotherBranch", null, "cs", out qualityProfile);
            result.Should().BeTrue();
            qualityProfile.Should().Be("profile3k");

            // with organizations
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar&organization=my+org"] =
               "{ profiles: [{\"key\":\"profileOrganization\",\"name\":\"profile1\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("foo bar", null, "my org", "cs", out qualityProfile);
            result.Should().BeTrue();
            qualityProfile.Should().Be("profileOrganization");

            // fallback to defaults
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?defaults=true"] =
                "{ profiles: [{\"key\":\"profileDefault\",\"name\":\"profileDefault\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("non existing", null, null, "cs", out qualityProfile);
            result.Should().BeTrue();
            qualityProfile.Should().Be("profileDefault");

            // defaults with organizations
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?defaults=true&organization=my+org"] =
                       "{ profiles: [{\"key\":\"profileOrganizationDefault\",\"name\":\"profileDefault\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("non existing", null, "my org", "cs", out qualityProfile);
            result.Should().BeTrue();
            qualityProfile.Should().Be("profileOrganizationDefault");

            // no cs in list of profiles
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=java+foo+bar"] =
                "{ profiles: [{\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("java foo bar", null, null, "cs", out qualityProfile);
            result.Should().BeFalse();
            qualityProfile.Should().BeNull();

            // empty
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=empty+foo+bar"] =
                "{ profiles: []}";
            result = ws.TryGetQualityProfile("empty foo bar", null, null, "cs", out qualityProfile);
            result.Should().BeFalse();
            qualityProfile.Should().BeNull();
        }

        [TestMethod]
        public void TryGetQualityProfile56()
        {
            bool result;

            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar"] =
                "{ profiles: [{\"key\":\"profile1k\",\"name\":\"profile1\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar%3AaBranch"] =
                "{ profiles: [{\"key\":\"profile2k\",\"name\":\"profile2\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar%3AanotherBranch"] =
                "{ profiles: [{\"key\":\"profile3k\",\"name\":\"profile3\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";

            // main
            result = ws.TryGetQualityProfile("foo bar", null, null, "cs", out string qualityProfile);
            result.Should().BeTrue();
            qualityProfile.Should().Be("profile1k");

            // branch specific
            result = ws.TryGetQualityProfile("foo bar", "aBranch", null, "cs", out qualityProfile);
            result.Should().BeTrue();
            qualityProfile.Should().Be("profile2k");

            result = ws.TryGetQualityProfile("foo bar", "anotherBranch", null, "cs", out qualityProfile);
            result.Should().BeTrue();
            qualityProfile.Should().Be("profile3k");

            // with organizations
            result = ws.TryGetQualityProfile("foo bar", null, "my org", "cs", out qualityProfile);
            result.Should().BeTrue();
            qualityProfile.Should().Be("profile1k");

            // fallback to defaults
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?defaults=true"] =
                "{ profiles: [{\"key\":\"profileDefault\",\"name\":\"profileDefault\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("non existing", null, null, "cs", out qualityProfile);
            result.Should().BeTrue();
            qualityProfile.Should().Be("profileDefault");

            // defaults with organizations
            result = ws.TryGetQualityProfile("non existing", null, "my org", "cs", out qualityProfile);
            result.Should().BeTrue();
            qualityProfile.Should().Be("profileDefault");

            // no cs in list of profiles
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=java+foo+bar"] =
                "{ profiles: [{\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("java foo bar", null, null, "cs", out qualityProfile);
            result.Should().BeFalse();
            qualityProfile.Should().BeNull();

            // empty
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=empty+foo+bar"] =
                "{ profiles: []}";
            result = ws.TryGetQualityProfile("empty foo bar", null, null, "cs", out qualityProfile);
            result.Should().BeFalse();
            qualityProfile.Should().BeNull();
        }

        [TestMethod]
        public void GetSettingsSq63()
        {
            downloader.Pages["http://myhost:222/api/server/version"] = "6.3-SNAPSHOT";
            downloader.Pages["http://myhost:222/api/settings/values?component=comp"] =
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
            var result = ws.GetProperties("comp");
            result.Count.Should().Be(7);
            result["sonar.exclusions"].Should().Be("myfile,myfile2");
            result["sonar.junit.reportsPath"].Should().Be("testing.xml");
            result["sonar.issue.ignore.multicriteria.1.resourceKey"].Should().Be("prop1");
            result["sonar.issue.ignore.multicriteria.1.ruleKey"].Should().Be("");
            result["sonar.issue.ignore.multicriteria.2.resourceKey"].Should().Be("prop2");
            result["sonar.issue.ignore.multicriteria.2.ruleKey"].Should().Be("");
        }

        [TestMethod]
        public void GetActiveRules_UseParamAsKey()
        {
            downloader.Pages["http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&activation=true&qprofile=qp&p=1"] =
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

            var actual = ws.GetActiveRules("qp");
            actual.Should().HaveCount(1);

            actual[0].RepoKey.Should().Be("vbnet");
            actual[0].RuleKey.Should().Be("OverwrittenId");
            actual[0].InternalKeyOrKey.Should().Be("OverwrittenId");
            actual[0].TemplateKey.Should().BeNull();
            actual[0].Parameters.Should().HaveCount(1);
        }

        [TestMethod]
        public void GetActiveRules()
        {
            downloader.Pages["http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&activation=true&qprofile=qp&p=1"] =
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

            downloader.Pages["http://myhost:222/api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&activation=true&qprofile=qp&p=2"] =
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

            var actual = ws.GetActiveRules("qp");
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
        public void GetInactiveRulesAndEscapeUrl()
        {
            downloader.Pages["http://myhost:222/api/rules/search?f=internalKey&ps=500&activation=false&qprofile=my%23qp&p=1&languages=cs"] = @"
            {
            total: 3,
            p: 1,
            ps: 500,
            rules: [
                {
                    key: ""csharpsquid:S2757"",
                    type: ""BUG""
                },
                {
                    key: ""csharpsquid:S1117"",
                    type: ""CODE_SMELL""
                },
                {
                    key: ""csharpsquid:S1764"",
                    type: ""BUG""
                }
            ]}";

            var rules = ws.GetInactiveRules("my#qp", "cs");
            string[] expected = { "csharpsquid:S2757", "csharpsquid:S1117", "csharpsquid:S1764" };
            rules.Should().BeEquivalentTo(expected);
        }

        [TestMethod]
        public void GetProperties_NullProjectKey_Throws()
        {
            // Arrange
            var testSubject = new SonarWebService(new TestDownloader(), "http://myserver", new TestLogger());
            Action act = () => testSubject.GetProperties(null, null);

            // Act & Assert
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("projectKey");
        }

        [TestMethod]
        public void GetProperties()
        {
            // This test includes a regression scenario for SONARMSBRU-187:
            // Requesting properties for project:branch should return branch-specific data

            // Check that properties are correctly defaulted as well as branch-specific
            downloader.Pages["http://myhost:222/api/properties?resource=foo+bar"] =
                "[{\"key\": \"sonar.property1\",\"value\": \"value1\"},{\"key\": \"sonar.property2\",\"value\": \"value2\"},{\"key\": \"sonar.cs.msbuild.testProjectPattern\",\"value\": \"pattern\"}]";
            downloader.Pages["http://myhost:222/api/properties?resource=foo+bar%3AaBranch"] =
                "[{\"key\": \"sonar.property1\",\"value\": \"anotherValue1\"},{\"key\": \"sonar.property2\",\"value\": \"anotherValue2\"}]";

            // default
            var expected1 = new Dictionary<string, string>
            {
                ["sonar.property1"] = "value1",
                ["sonar.property2"] = "value2",
                ["sonar.msbuild.testProjectPattern"] = "pattern"
            };
            var actual1 = ws.GetProperties("foo bar");

            actual1.Should().HaveCount(expected1.Count);
            actual1.Should().NotBeSameAs(expected1);

            // branch specific
            var expected2 = new Dictionary<string, string>
            {
                ["sonar.property1"] = "anotherValue1",
                ["sonar.property2"] = "anotherValue2"
            };
            var actual2 = ws.GetProperties("foo bar", "aBranch");

            actual2.Should().HaveCount(expected2.Count);
            actual2.Should().NotBeSameAs(expected2);
        }

        [TestMethod]
        public void GetInstalledPlugins()
        {
            downloader.Pages["http://myhost:222/api/languages/list"] = "{ languages: [{ key: \"cs\", name: \"C#\" }, { key: \"flex\", name: \"Flex\" } ]}";
            var expected = new List<string>
            {
                "cs",
                "flex"
            };
            var actual = new List<string>(ws.GetAllLanguages());

            expected.SequenceEqual(actual).Should().BeTrue();
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_NullPluginKey_Throws()
        {
            // Arrange
            var testSubject = new SonarWebService(new TestDownloader(), "http://myserver", new TestLogger());
            Action act = () => testSubject.TryDownloadEmbeddedFile(null, "filename", "targetDir");

            // Act & Assert
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("pluginKey");
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_NullEmbeddedFileName_Throws()
        {
            // Arrange
            var testSubject = new SonarWebService(new TestDownloader(), "http://myserver", new TestLogger());
            Action act = () => testSubject.TryDownloadEmbeddedFile("key", null, "targetDir");

            // Act & Assert
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("embeddedFileName");
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_NullTargetDirectory_Throws()
        {
            // Arrange
            var testSubject = new SonarWebService(new TestDownloader(), "http://myserver", new TestLogger());
            Action act = () => testSubject.TryDownloadEmbeddedFile("pluginKey", "filename", null);

            // Act & Assert
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("targetDirectory");
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_RequestedFileExists()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            downloader.Pages["http://myhost:222/static/csharp/dummy.txt"] = "dummy file content";

            // Act
            var success = ws.TryDownloadEmbeddedFile("csharp", "dummy.txt", testDir);

            // Assert
            success.Should().BeTrue("Expected success");
            var expectedFilePath = Path.Combine(testDir, "dummy.txt");
            File.Exists(expectedFilePath).Should().BeTrue("Failed to download the expected file");
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_RequestedFileDoesNotExist()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);

            // Act
            var success = ws.TryDownloadEmbeddedFile("csharp", "dummy.txt", testDir);

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
                .Setup(x => x.Download($"{serverUrl}/api/server/version"))
                .Returns("1.2.3.4");
            downloaderMock
                .Setup(x => x.Download($"{serverUrl}/api/properties?resource={projectKey}"))
                .Throws(new WebException("Forbidden", new Exception(), WebExceptionStatus.ConnectionClosed, responseMock.Object));

            var service = new SonarWebService(downloaderMock.Object, serverUrl, logger);

            Action action = () => service.GetProperties(projectKey);
            action.Should().Throw<WebException>();

            logger.Errors.Should().HaveCount(1);
            logger.Warnings.Should().HaveCount(1);
            logger.Warnings[0].Should().Be("To analyze private projects make sure the scanner user has 'Browse' permission.");
        }

        [TestMethod]
        public void GetProperties_63plus_Forbidden()
        {
            const string serverUrl = "http://localhost";
            const string projectKey = "my-project";

            var responseMock = new Mock<HttpWebResponse>();
            responseMock.SetupGet(x => x.StatusCode).Returns(HttpStatusCode.Forbidden);

            var downloaderMock = new Mock<IDownloader>();
            downloaderMock
                .Setup(x => x.Download($"{serverUrl}/api/server/version"))
                .Returns("6.3.0.0");

            var content = string.Empty;
            downloaderMock
                .Setup(x => x.TryDownloadIfExists($"{serverUrl}/api/settings/values?component={projectKey}", out content))
                .Throws(new WebException("Forbidden", new Exception(), WebExceptionStatus.ConnectionClosed, responseMock.Object));

            var service = new SonarWebService(downloaderMock.Object, serverUrl, logger);

            Action action = () => service.GetProperties(projectKey);
            action.Should().Throw<WebException>();

            logger.Errors.Should().HaveCount(1);
            logger.Warnings.Should().HaveCount(1);
            logger.Warnings[0].Should().Be("To analyze private projects make sure the scanner user has 'Browse' permission.");
        }

        private class TestDownloader : IDownloader
        {
            public IDictionary<string, string> Pages = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            public List<string> AccessedUrls = new List<string>();

            public bool TryDownloadIfExists(string url, out string contents)
            {
                AccessedUrls.Add(url);
                if (Pages.ContainsKey(url))
                {
                    contents = Pages[url];
                    return true;
                }
                else
                {
                    contents = null;
                    return false;
                }
            }

            public string Download(string url)
            {
                AccessedUrls.Add(url);
                if (Pages.ContainsKey(url))
                {
                    return Pages[url];
                }
                throw new ArgumentException("Cannot find URL " + url);
            }

            public bool TryDownloadFileIfExists(string url, string targetFilePath)
            {
                AccessedUrls.Add(url);

                if (Pages.ContainsKey(url))
                {
                    File.WriteAllText(targetFilePath, Pages[url]);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public void Dispose()
            {
                // Nothing to do here
            }
        }
    }
}
