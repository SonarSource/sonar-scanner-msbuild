/*
 * SonarQube Scanner for MSBuild
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.UnitTests
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
            Assert.IsTrue(result);
            Assert.AreEqual("profile1k", qualityProfile);

            // branch specific
            result = ws.TryGetQualityProfile("foo bar", "aBranch", null, "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile2k", qualityProfile);

            result = ws.TryGetQualityProfile("foo bar", "anotherBranch", null, "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile3k", qualityProfile);

            // with organizations
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar&organization=my+org"] =
               "{ profiles: [{\"key\":\"profileOrganization\",\"name\":\"profile1\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("foo bar", null, "my org", "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profileOrganization", qualityProfile);

            // fallback to defaults
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?defaults=true"] =
                "{ profiles: [{\"key\":\"profileDefault\",\"name\":\"profileDefault\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("non existing", null, null, "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profileDefault", qualityProfile);

            // defaults with organizations
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?defaults=true&organization=my+org"] =
                       "{ profiles: [{\"key\":\"profileOrganizationDefault\",\"name\":\"profileDefault\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("non existing", null, "my org", "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profileOrganizationDefault", qualityProfile);

            // no cs in list of profiles
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=java+foo+bar"] =
                "{ profiles: [{\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("java foo bar", null, null, "cs", out qualityProfile);
            Assert.IsFalse(result);
            Assert.IsNull(qualityProfile);

            // empty
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=empty+foo+bar"] =
                "{ profiles: []}";
            result = ws.TryGetQualityProfile("empty foo bar", null, null, "cs", out qualityProfile);
            Assert.IsFalse(result);
            Assert.IsNull(qualityProfile);
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
            Assert.IsTrue(result);
            Assert.AreEqual("profile1k", qualityProfile);

            // branch specific
            result = ws.TryGetQualityProfile("foo bar", "aBranch", null, "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile2k", qualityProfile);

            result = ws.TryGetQualityProfile("foo bar", "anotherBranch", null, "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile3k", qualityProfile);

            // with organizations
            result = ws.TryGetQualityProfile("foo bar", null, "my org", "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile1k", qualityProfile);

            // fallback to defaults
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?defaults=true"] =
                "{ profiles: [{\"key\":\"profileDefault\",\"name\":\"profileDefault\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("non existing", null, null, "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profileDefault", qualityProfile);

            // defaults with organizations
            result = ws.TryGetQualityProfile("non existing", null, "my org", "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profileDefault", qualityProfile);

            // no cs in list of profiles
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=java+foo+bar"] =
                "{ profiles: [{\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("java foo bar", null, null, "cs", out qualityProfile);
            Assert.IsFalse(result);
            Assert.IsNull(qualityProfile);

            // empty
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=empty+foo+bar"] =
                "{ profiles: []}";
            result = ws.TryGetQualityProfile("empty foo bar", null, null, "cs", out qualityProfile);
            Assert.IsFalse(result);
            Assert.IsNull(qualityProfile);
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
            Assert.AreEqual(7, result.Count);
            Assert.AreEqual("myfile,myfile2", result["sonar.exclusions"]);
            Assert.AreEqual("testing.xml", result["sonar.junit.reportsPath"]);
            Assert.AreEqual("prop1", result["sonar.issue.ignore.multicriteria.1.resourceKey"]);
            Assert.AreEqual("", result["sonar.issue.ignore.multicriteria.1.ruleKey"]);
            Assert.AreEqual("prop2", result["sonar.issue.ignore.multicriteria.2.resourceKey"]);
            Assert.AreEqual("", result["sonar.issue.ignore.multicriteria.2.ruleKey"]);
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
            Assert.AreEqual(1, actual.Count());

            Assert.AreEqual("vbnet", actual[0].RepoKey);
            Assert.AreEqual("OverwrittenId", actual[0].RuleKey);
            Assert.AreEqual("OverwrittenId", actual[0].InternalKeyOrKey);
            Assert.AreEqual(null, actual[0].TemplateKey);
            Assert.AreEqual(1, actual[0].Parameters.Count());
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
            Assert.AreEqual(3, actual.Count());

            Assert.AreEqual("vbnet", actual[0].RepoKey);
            Assert.AreEqual("S2368", actual[0].RuleKey);
            Assert.AreEqual("S2368", actual[0].InternalKeyOrKey);
            Assert.AreEqual(null, actual[0].TemplateKey);
            Assert.AreEqual(0, actual[0].Parameters.Count());

            Assert.AreEqual("common-vbnet", actual[1].RepoKey);
            Assert.AreEqual("InsufficientCommentDensity", actual[1].RuleKey);
            Assert.AreEqual("InsufficientCommentDensity.internal", actual[1].InternalKeyOrKey);
            Assert.AreEqual(null, actual[1].TemplateKey);
            Assert.AreEqual(1, actual[1].Parameters.Count());
            Assert.IsTrue(actual[1].Parameters.First().Equals(new KeyValuePair<string, string>("minimumCommentDensity", "50")));

            Assert.AreEqual("vbnet", actual[2].RepoKey);
            Assert.AreEqual("S2346", actual[2].RuleKey);
            Assert.AreEqual("S2346", actual[2].InternalKeyOrKey);
            Assert.AreEqual(null, actual[2].TemplateKey);
            Assert.AreEqual(0, actual[2].Parameters.Count());
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
            CollectionAssert.AreEqual(rules.ToArray(), expected);
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

            Assert.AreEqual(true, expected1.Count == actual1.Count && !expected1.Except(actual1).Any());

            // branch specific
            var expected2 = new Dictionary<string, string>
            {
                ["sonar.property1"] = "anotherValue1",
                ["sonar.property2"] = "anotherValue2"
            };
            var actual2 = ws.GetProperties("foo bar", "aBranch");

            Assert.AreEqual(true, expected2.Count == actual2.Count && !expected2.Except(actual2).Any());
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

            Assert.AreEqual(true, expected.SequenceEqual(actual));
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
            Assert.IsTrue(success, "Expected success");
            var expectedFilePath = Path.Combine(testDir, "dummy.txt");
            Assert.IsTrue(File.Exists(expectedFilePath), "Failed to download the expected file");
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_RequestedFileDoesNotExist()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);

            // Act
            var success = ws.TryDownloadEmbeddedFile("csharp", "dummy.txt", testDir);

            // Assert
            Assert.IsFalse(success, "Expected failure");
            var expectedFilePath = Path.Combine(testDir, "dummy.txt");
            Assert.IsFalse(File.Exists(expectedFilePath), "File should not be created");
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
