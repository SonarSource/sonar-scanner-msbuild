//-----------------------------------------------------------------------
// <copyright file="SonarWebServiceTest.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.UnitTests
{
    [TestClass]
    public class SonarWebServiceTest
    {
        private TestDownloader downloader;
        private SonarWebService ws;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Init()
        {
            downloader = new TestDownloader();
            ws = new SonarWebService(downloader, "http://myhost:222", new TestLogger());
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
        public void TryGetQualityProfile()
        {
            bool result;
            string qualityProfile;

            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar"] = 
                "{ profiles: [{\"key\":\"profile1k\",\"name\":\"profile1\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar%3AaBranch"] =
                "{ profiles: [{\"key\":\"profile2k\",\"name\":\"profile2\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=foo+bar%3AanotherBranch"] =
                "{ profiles: [{\"key\":\"profile3k\",\"name\":\"profile3\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";

            // main
            result = ws.TryGetQualityProfile("foo bar", null, "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile1k", qualityProfile);

            // branch specific
            result = ws.TryGetQualityProfile("foo bar", "aBranch", "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile2k", qualityProfile);

            result = ws.TryGetQualityProfile("foo bar", "anotherBranch", "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile3k", qualityProfile);

            // fallback to defaults
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?defaults=true"] =
                "{ profiles: [{\"key\":\"profileDefault\",\"name\":\"profileDefault\",\"language\":\"cs\"}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("non existing", null, "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profileDefault", qualityProfile);

            // no cs in list of profiles
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=java+foo+bar"] = 
                "{ profiles: [{\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"java\"}]}";
            result = ws.TryGetQualityProfile("java foo bar", null, "cs", out qualityProfile);
            Assert.IsFalse(result);
            Assert.IsNull(qualityProfile);

            // empty
            downloader.Pages["http://myhost:222/api/qualityprofiles/search?projectKey=empty+foo+bar"] = 
                "{ profiles: []}";
            result = ws.TryGetQualityProfile("empty foo bar", null, "cs", out qualityProfile);
            Assert.IsFalse(result);
            Assert.IsNull(qualityProfile);
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

            IList<ActiveRule> actual = ws.GetActiveRules("qp");
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

            IList<ActiveRule> actual = ws.GetActiveRules("qp");
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

            IList<string> rules = ws.GetInactiveRules("my#qp", "cs");
            string[] expected = { "csharpsquid:S2757", "csharpsquid:S1117", "csharpsquid:S1764" };
            CollectionAssert.AreEqual(rules.ToArray(), expected);
        }

        [TestMethod]
        public void GetProperties()
        {
            // This test includes a regression scenario for SONARMSBRU-187:
            // Requesting properties for project:branch should return branch-specific data

            // Check that properties are correctly defaulted as well as branch-specific
            downloader.Pages["http://myhost:222/api/properties?resource=foo+bar"] = "[{\"key\": \"sonar.property1\",\"value\": \"value1\"},{\"key\": \"sonar.property2\",\"value\": \"value2\"}]";
            downloader.Pages["http://myhost:222/api/properties?resource=foo+bar%3AaBranch"] = "[{\"key\": \"sonar.property1\",\"value\": \"anotherValue1\"},{\"key\": \"sonar.property2\",\"value\": \"anotherValue2\"}]";

            // default
            var expected1 = new Dictionary<string, string>();
            expected1["sonar.property1"] = "value1";
            expected1["sonar.property2"] = "value2";
            expected1["sonar.cs.msbuild.testProjectPattern"] = SonarProperties.DefaultTestProjectPattern;
            var actual1 = ws.GetProperties("foo bar");

            Assert.AreEqual(true, expected1.Count == actual1.Count && !expected1.Except(actual1).Any());

            // branch specific
            var expected2 = new Dictionary<string, string>();
            expected2["sonar.property1"] = "anotherValue1";
            expected2["sonar.property2"] = "anotherValue2";
            expected2["sonar.cs.msbuild.testProjectPattern"] = SonarProperties.DefaultTestProjectPattern;
            var actual2 = ws.GetProperties("foo bar", "aBranch");

            Assert.AreEqual(true, expected2.Count == actual2.Count && !expected2.Except(actual2).Any());
        }

        [TestMethod]
        public void GetInstalledPlugins()
        {
            downloader.Pages["http://myhost:222/api/updatecenter/installed_plugins"] = "[{\"key\":\"visualstudio\",\"name\":\"...\",\"version\":\"1.2\"},{\"key\":\"csharp\",\"name\":\"C#\",\"version\":\"4.0\"}]";
            var expected = new List<string>();
            expected.Add("visualstudio");
            expected.Add("csharp");
            var actual = new List<string>(ws.GetInstalledPlugins());

            Assert.AreEqual(true, expected.SequenceEqual(actual));
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_RequestedFileExists()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            downloader.Pages["http://myhost:222/static/csharp/dummy.txt"] = "dummy file content";

            // Act
            bool success = ws.TryDownloadEmbeddedFile("csharp", "dummy.txt", testDir);

            // Assert
            Assert.IsTrue(success, "Expected success");
            string expectedFilePath = Path.Combine(testDir, "dummy.txt");
            Assert.IsTrue(File.Exists(expectedFilePath), "Failed to download the expected file");
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_RequestedFileDoesNotExist()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // Act
            bool success = ws.TryDownloadEmbeddedFile("csharp", "dummy.txt", testDir);

            // Assert
            Assert.IsFalse(success, "Expected failure");
            string expectedFilePath = Path.Combine(testDir, "dummy.txt");
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
