//-----------------------------------------------------------------------
// <copyright file="SonarWebServiceTest.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
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

            // Check that profiles are correctly defaulted as well as branch-specific
            downloader.Pages["http://myhost:222/api/profiles/list?language=cs&project=foo+bar"] = "[{\"name\":\"profile1\",\"language\":\"cs\",\"default\":true}]";
            downloader.Pages["http://myhost:222/api/profiles/list?language=cs&project=foo+bar%3AaBranch"] = "[{\"name\":\"profile2\",\"language\":\"cs\",\"default\":false}]";
            downloader.Pages["http://myhost:222/api/profiles/list?language=cs&project=foo+bar%3AanotherBranch"] = "[{\"name\":\"profile3\",\"language\":\"cs\",\"default\":false}]";
            // default
            result = ws.TryGetQualityProfile("foo bar", null, "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile1", qualityProfile);
            // branch specific
            result = ws.TryGetQualityProfile("foo bar", "aBranch", "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile2", qualityProfile);
            result = ws.TryGetQualityProfile("foo bar", "anotherBranch", "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile3", qualityProfile);

            downloader.Pages["http://myhost:222/api/profiles/list?language=cs"] = "[{\"name\":\"profile1\",\"language\":\"cs\",\"default\":true},{\"name\":\"profile2\",\"language\":\"vbnet\",\"default\":false}]";
            result = ws.TryGetQualityProfile("bar", null, "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile1", qualityProfile);

            downloader.Pages["http://myhost:222/api/profiles/list?language=vbnet"] = "[{\"name\":\"profile1\",\"language\":\"vbnet\",\"default\":true},{\"name\":\"profile2\",\"language\":\"vbnet\",\"default\":false}]";
            result = ws.TryGetQualityProfile("bar", null, "vbnet", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile1", qualityProfile);

            downloader.Pages["http://myhost:222/api/profiles/list?language=cs"] = "[{\"name\":\"profile1\",\"language\":\"cs\",\"default\":false},{\"name\":\"profile2\",\"language\":\"cs\",\"default\":true}]";
            result = ws.TryGetQualityProfile("bar", null, "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile2", qualityProfile);

            downloader.Pages["http://myhost:222/api/profiles/list?language=vbnet"] = "[]";
            result = ws.TryGetQualityProfile("foo", null, "vbnet", out qualityProfile);
            Assert.IsFalse(result);
            Assert.IsNull(qualityProfile);
        }

        [TestMethod]
        public void GetActiveRuleKeys()
        {
            downloader.Pages["http://myhost:222/api/profiles/index?language=cs&name=Sonar+way"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true}]";
            var expected = new List<string>();
            var actual = new List<string>(ws.GetActiveRuleKeys("Sonar way", "cs", "fxcop"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));

            downloader.Pages["http://myhost:222/api/profiles/index?language=cs&name=Sonar+way"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true,\"rules\":[{\"key\":\"SomeFxCopKey1\",\"repo\":\"fxcop\",\"severity\":\"MAJOR\"}]}]";
            expected = new List<string>();
            expected.Add("SomeFxCopKey1");
            actual = new List<string>(ws.GetActiveRuleKeys("Sonar way", "cs", "fxcop"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));

            downloader.Pages["http://myhost:222/api/profiles/index?language=cs&name=My+quality+profile"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true,\"rules\":[{\"key\":\"SomeFxCopKey1\",\"repo\":\"fxcop\",\"severity\":\"MAJOR\"}]}]";
            expected = new List<string>();
            expected.Add("SomeFxCopKey1");
            actual = new List<string>(ws.GetActiveRuleKeys("My quality profile", "cs", "fxcop"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));

            downloader.Pages["http://myhost:222/api/profiles/index?language=cs&name=Sonar+way"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true,\"rules\":[{\"key\":\"SomeFxCopKey1\",\"repo\":\"fxcop\",\"severity\":\"MAJOR\"},{\"key\":\"SomeSonarKey1\",\"repo\":\"cs\",\"severity\":\"MAJOR\"}]}]";
            expected = new List<string>();
            expected.Add("SomeFxCopKey1");
            actual = new List<string>(ws.GetActiveRuleKeys("Sonar way", "cs", "fxcop"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));

            downloader.Pages["http://myhost:222/api/profiles/index?language=cs&name=Sonar+way"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true,\"rules\":[{\"key\":\"SomeFxCopKey1\",\"repo\":\"fxcop\",\"severity\":\"MAJOR\"},{\"key\":\"SomeFxCopKey2\",\"repo\":\"fxcop\",\"severity\":\"MAJOR\"}]}]";
            expected = new List<string>();
            expected.Add("SomeFxCopKey1");
            expected.Add("SomeFxCopKey2");
            actual = new List<string>(ws.GetActiveRuleKeys("Sonar way", "cs", "fxcop"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));

            downloader.Pages["http://myhost:222/api/profiles/index?language=vbnet&name=Sonar+way"] = "[{\"name\":\"Sonar way\",\"language\":\"vbnet\",\"default\":true,\"rules\":[{\"key\":\"SomeFxCopKey1\",\"repo\":\"fxcop-vbnet\",\"severity\":\"MAJOR\"},{\"key\":\"SomeFxCopKey2\",\"repo\":\"fxcop-vbnet\",\"severity\":\"MAJOR\"}]}]";
            expected = new List<string>();
            expected.Add("SomeFxCopKey1");
            expected.Add("SomeFxCopKey2");
            actual = new List<string>(ws.GetActiveRuleKeys("Sonar way", "vbnet", "fxcop-vbnet"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));

            downloader.Pages["http://myhost:222/api/profiles/index?language=cs&name=Sonar+way"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true,\"rules\":[{\"key\":\"My_Own_FxCop_Rule\",\"repo\":\"fxcop\",\"severity\":\"MAJOR\",\"params\":[{\"key\":\"CheckId\",\"value\":\"CA_MyOwnCustomRule\"}]}]}]";
            expected = new List<string>();
            expected.Add("CA_MyOwnCustomRule");
            actual = new List<string>(ws.GetActiveRuleKeys("Sonar way", "cs", "fxcop"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));
        }

        [TestMethod]
        public void GetActiveRuleKeysShouldUrlEscapeQualityProfile()
        {
            downloader.Pages["http://myhost:222/api/profiles/index?language=cs&name=Sonar%23way"] = "[{\"name\":\"Sonar#way\",\"language\":\"cs\",\"default\":true}]";
            var expected = new List<string>();
            var actual = new List<string>(ws.GetActiveRuleKeys("Sonar#way", "cs", "fxcop"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));
        }

        [TestMethod]
        public void GetInternalKeys()
        {
            downloader.Pages["http://myhost:222/api/rules/search?f=internalKey&ps=" + int.MaxValue + "&repositories=fxcop"] = "{\"total\":2,\"p\":1,\"ps\":10,\"rules\":[{\"key\":\"fxcop:My_Own_FxCop_Rule\"},{\"key\":\"fxcop:UriParametersShouldNotBeStrings\",\"internalKey\":\"CA1054\"}]}";
            var expected = new Dictionary<string, string>();
            expected["fxcop:UriParametersShouldNotBeStrings"] = "CA1054";
            var actual = ws.GetInternalKeys("fxcop");

            // default
            var expected1 = new Dictionary<string, string>();
            expected1["sonar.property1"] = "value1";
            expected1["sonar.property2"] = "value2";
            expected1["sonar.cs.msbuild.testProjectPattern"] = SonarProperties.DefaultTestProjectPattern;
            var actual1 = ws.GetProperties("foo bar", null);

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
        public void GetProperties()
        {
            // Check that properties are correctly defaulted as well as branch-specific
            downloader.Pages["http://myhost:222/api/properties?resource=foo+bar"] = "[{\"key\": \"sonar.property1\",\"value\": \"value1\"},{\"key\": \"sonar.property2\",\"value\": \"value2\"}]";
            downloader.Pages["http://myhost:222/api/properties?resource=foo+bar%3AaBranch"] = "[{\"key\": \"sonar.property1\",\"value\": \"anotherValue1\"},{\"key\": \"sonar.property2\",\"value\": \"anotherValue2\"}]";

            // default
            var expected1 = new Dictionary<string, string>();
            expected1["sonar.property1"] = "value1";
            expected1["sonar.property2"] = "value2";
            expected1["sonar.cs.msbuild.testProjectPattern"] = SonarProperties.DefaultTestProjectPattern;
            var actual1 = ws.GetProperties("foo bar", null);

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

        // TODO Rewrite this as a data driven unit test to not duplicate the test contents?
        [TestMethod]
        public void ServerUrlWithTrailingSlash()
        {
            ws = new SonarWebService(downloader, "http://myhost:222/", new TestLogger());

            // Check that profiles are correctly defaulted as well as branch-specific
            downloader.Pages["http://myhost:222/api/profiles/list?language=cs&project=foo+bar"] = "[{\"name\":\"profile1\",\"language\":\"cs\",\"default\":true}]";
            downloader.Pages["http://myhost:222/api/profiles/list?language=cs&project=foo+bar%3AaBranch"] = "[{\"name\":\"profile2\",\"language\":\"cs\",\"default\":false}]";
            downloader.Pages["http://myhost:222/api/profiles/list?language=cs&project=foo+bar%3AanotherBranch"] = "[{\"name\":\"profile3\",\"language\":\"cs\",\"default\":false}]";
            string qualityProfile1;
            string qualityProfile2;
            string qualityProfile3;
            bool result1 = ws.TryGetQualityProfile("foo bar", null, "cs", out qualityProfile1);
            bool result2 = ws.TryGetQualityProfile("foo bar", "aBranch", "cs", out qualityProfile2);
            bool result3 = ws.TryGetQualityProfile("foo bar", "anotherBranch", "cs", out qualityProfile3);

            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
            Assert.IsTrue(result3);
            Assert.AreEqual("profile1", qualityProfile1);
            Assert.AreEqual("profile2", qualityProfile2);
            Assert.AreEqual("profile3", qualityProfile3);

            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
            Assert.IsTrue(result3);
            Assert.AreEqual("profile1", qualityProfile1);
            Assert.AreEqual("profile2", qualityProfile2);
            Assert.AreEqual("profile3", qualityProfile3);

            downloader.Pages["http://myhost:222/api/profiles/index?language=cs&name=Sonar+way"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true}]";
            var expected1 = new List<string>();
            var actual1 = new List<string>(ws.GetActiveRuleKeys("Sonar way", "cs", "foo"));
            Assert.AreEqual(true, expected1.SequenceEqual(actual1));

            downloader.Pages["http://myhost:222/api/rules/search?f=internalKey&ps=" + int.MaxValue + "&repositories=fxcop"] = "{\"total\":2,\"p\":1,\"ps\":10,\"rules\":[{\"key\":\"fxcop:My_Own_FxCop_Rule\"},{\"key\":\"fxcop:UriParametersShouldNotBeStrings\",\"internalKey\":\"CA1054\"}]}";
            var expected2 = new Dictionary<string, string>();
            expected2["fxcop:UriParametersShouldNotBeStrings"] = "CA1054";
            var actual2 = ws.GetInternalKeys("fxcop");

            Assert.AreEqual(true, expected2.Count == actual2.Count && !expected2.Except(actual2).Any());

            downloader.Pages["http://myhost:222/api/properties?resource=foo+bar"] = "[{\"key\": \"sonar.property1\",\"value\": \"value1\"},{\"key\": \"sonar.property2\",\"value\": \"value2\"}]";
            downloader.Pages["http://myhost:222/api/properties?resource=foo+bar%3AaBranch"] = "[{\"key\": \"sonar.property1\",\"value\": \"anotherValue1\"},{\"key\": \"sonar.property2\",\"value\": \"anotherValue2\"}]";

            var expected3_1 = new Dictionary<string, string>();
            expected3_1["sonar.property1"] = "value1";
            expected3_1["sonar.property2"] = "value2";
            expected3_1["sonar.cs.msbuild.testProjectPattern"] = SonarProperties.DefaultTestProjectPattern;
            var actual3_1 = ws.GetProperties("foo bar", null);

            var expected3_2 = new Dictionary<string, string>();
            expected3_2["sonar.property1"] = "anotherValue1";
            expected3_2["sonar.property2"] = "anotherValue2";
            expected3_2["sonar.cs.msbuild.testProjectPattern"] = SonarProperties.DefaultTestProjectPattern;
            var actual3_2 = ws.GetProperties("foo bar", "aBranch");

            Assert.AreEqual(true, expected3_1.Count == actual3_1.Count && !expected3_1.Except(actual3_1).Any());
            Assert.AreEqual(true, expected3_2.Count == actual3_2.Count && !expected3_2.Except(actual3_2).Any());

            downloader.Pages["http://myhost:222/api/updatecenter/installed_plugins"] = "[{\"key\":\"visualstudio\",\"name\":\"...\",\"version\":\"1.2\"},{\"key\":\"csharp\",\"name\":\"C#\",\"version\":\"4.0\"}]";
            var expected4 = new List<string>();
            expected4.Add("visualstudio");
            expected4.Add("csharp");
            var actual4 = new List<string>(ws.GetInstalledPlugins());

            Assert.AreEqual(true, expected4.SequenceEqual(actual4));
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
