//-----------------------------------------------------------------------
// <copyright file="SonarWebServiceTest.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor.UnitTests
{
    [TestClass]
    public class SonarWebServiceTest
    {
        private TestDownloader downloader;
        private SonarWebService ws;

        [TestInitialize]
        public void Init()
        {
            downloader = new TestDownloader();
            ws = new SonarWebService(downloader, "http://myhost:222");
        }

        [TestMethod]
        public void TryGetQualityProfile()
        {
            bool result;
            string qualityProfile;

            downloader.Pages["http://myhost:222/api/profiles/list?language=cs&project=foo+bar"] = "[{\"name\":\"profile1\",\"language\":\"cs\",\"default\":true}]";
            result = ws.TryGetQualityProfile("foo bar", "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile1", qualityProfile);

            downloader.Pages["http://myhost:222/api/profiles/list?language=cs"] = "[{\"name\":\"profile1\",\"language\":\"cs\",\"default\":true},{\"name\":\"profile2\",\"language\":\"vbnet\",\"default\":false}]";
            result = ws.TryGetQualityProfile("bar", "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile1", qualityProfile);

            downloader.Pages["http://myhost:222/api/profiles/list?language=vbnet"] = "[{\"name\":\"profile1\",\"language\":\"vbnet\",\"default\":true},{\"name\":\"profile2\",\"language\":\"vbnet\",\"default\":false}]";
            result = ws.TryGetQualityProfile("bar", "vbnet", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile1", qualityProfile);

            downloader.Pages["http://myhost:222/api/profiles/list?language=cs"] = "[{\"name\":\"profile1\",\"language\":\"cs\",\"default\":false},{\"name\":\"profile2\",\"language\":\"cs\",\"default\":true}]";
            result = ws.TryGetQualityProfile("bar", "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile2", qualityProfile);

            downloader.Pages["http://myhost:222/api/profiles/list?language=vbnet"] = "[]";
            result = ws.TryGetQualityProfile("foo", "vbnet", out qualityProfile);
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

            Assert.AreEqual(true, expected.Count == actual.Count && !expected.Except(actual).Any());
        }

        [TestMethod]
        public void GetProperties()
        {
            downloader.Pages["http://myhost:222/api/properties?resource=foo+bar"] = "[{\"key\": \"sonar.property1\",\"value\": \"value1\"},{\"key\": \"sonar.property2\",\"value\": \"value2\"}]";
            var expected = new Dictionary<string, string>();
            expected["sonar.property1"] = "value1";
            expected["sonar.property2"] = "value2";
            expected["sonar.cs.msbuild.testProjectPattern"] = SonarProperties.DefaultTestProjectPattern;
            var actual = ws.GetProperties("foo bar", new TestLogger());

            Assert.AreEqual(true, expected.Count == actual.Count && !expected.Except(actual).Any());
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
            ws = new SonarWebService(downloader, "http://myhost:222/");

            downloader.Pages["http://myhost:222/api/profiles/list?language=cs&project=foo+bar"] = "[{\"name\":\"profile1\",\"language\":\"cs\",\"default\":true}]";
            string qualityProfile;
            bool result = ws.TryGetQualityProfile("foo bar", "cs", out qualityProfile);
            Assert.IsTrue(result);
            Assert.AreEqual("profile1", qualityProfile);

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
            var expected3 = new Dictionary<string, string>();
            expected3["sonar.property1"] = "value1";
            expected3["sonar.property2"] = "value2";
            expected3["sonar.cs.msbuild.testProjectPattern"] = SonarProperties.DefaultTestProjectPattern;
            var actual3 = ws.GetProperties("foo bar", new TestLogger());

            Assert.AreEqual(true, expected3.Count == actual3.Count && !expected3.Except(actual3).Any());

            downloader.Pages["http://myhost:222/api/updatecenter/installed_plugins"] = "[{\"key\":\"visualstudio\",\"name\":\"...\",\"version\":\"1.2\"},{\"key\":\"csharp\",\"name\":\"C#\",\"version\":\"4.0\"}]";
            var expected4 = new List<string>();
            expected4.Add("visualstudio");
            expected4.Add("csharp");
            var actual4 = new List<string>(ws.GetInstalledPlugins());

            Assert.AreEqual(true, expected4.SequenceEqual(actual4));
        }

        private class TestDownloader : IDownloader
        {
            public IDictionary<string, string> Pages = new Dictionary<string, string>();
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

            public void Dispose()
            {
            }
        }
    }
}
