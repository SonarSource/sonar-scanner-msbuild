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

namespace Sonar.TeamBuild.PreProcessor.UnitTests
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
            ws = new SonarWebService(downloader, "http://localhost:9000", "cs", "fxcop");
        }

        [TestMethod]
        public void GetQualityProfile()
        {
            downloader.Pages["http://localhost:9000/api/profiles/list?language=cs&project=foo%20bar"] = "[{\"name\":\"profile1\",\"language\":\"cs\",\"default\":true}]";
            Assert.AreEqual("profile1", ws.GetQualityProfile("foo bar"));

            downloader.Pages["http://localhost:9000/api/profiles/list?language=cs"] = "[{\"name\":\"profile1\",\"language\":\"cs\",\"default\":true},{\"name\":\"profile2\",\"language\":\"cs\",\"default\":false}]";
            Assert.AreEqual("profile1", ws.GetQualityProfile("bar"));

            downloader.Pages["http://localhost:9000/api/profiles/list?language=cs"] = "[{\"name\":\"profile1\",\"language\":\"cs\",\"default\":false},{\"name\":\"profile2\",\"language\":\"cs\",\"default\":true}]";
            Assert.AreEqual("profile2", ws.GetQualityProfile("bar"));
        }

        [TestMethod]
        public void GetActiveRuleKeys()
        {
            downloader.Pages["http://localhost:9000/api/profiles/index?language=cs&name=Sonar%20way"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true}]";
            var expected = new List<string>();
            var actual = new List<string>(ws.GetActiveRuleKeys("Sonar way"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));

            downloader.Pages["http://localhost:9000/api/profiles/index?language=cs&name=Sonar%20way"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true,\"rules\":[{\"key\":\"SomeFxCopKey1\",\"repo\":\"fxcop\",\"severity\":\"MAJOR\"}]}]";
            expected = new List<string>();
            expected.Add("SomeFxCopKey1");
            actual = new List<string>(ws.GetActiveRuleKeys("Sonar way"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));

            downloader.Pages["http://localhost:9000/api/profiles/index?language=cs&name=My%20quality%20profile"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true,\"rules\":[{\"key\":\"SomeFxCopKey1\",\"repo\":\"fxcop\",\"severity\":\"MAJOR\"}]}]";
            expected = new List<string>();
            expected.Add("SomeFxCopKey1");
            actual = new List<string>(ws.GetActiveRuleKeys("My quality profile"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));

            downloader.Pages["http://localhost:9000/api/profiles/index?language=cs&name=Sonar%20way"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true,\"rules\":[{\"key\":\"SomeFxCopKey1\",\"repo\":\"fxcop\",\"severity\":\"MAJOR\"},{\"key\":\"SomeSonarKey1\",\"repo\":\"cs\",\"severity\":\"MAJOR\"}]}]";
            expected = new List<string>();
            expected.Add("SomeFxCopKey1");
            actual = new List<string>(ws.GetActiveRuleKeys("Sonar way"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));

            downloader.Pages["http://localhost:9000/api/profiles/index?language=cs&name=Sonar%20way"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true,\"rules\":[{\"key\":\"SomeFxCopKey1\",\"repo\":\"fxcop\",\"severity\":\"MAJOR\"},{\"key\":\"SomeFxCopKey2\",\"repo\":\"fxcop\",\"severity\":\"MAJOR\"}]}]";
            expected = new List<string>();
            expected.Add("SomeFxCopKey1");
            expected.Add("SomeFxCopKey2");
            actual = new List<string>(ws.GetActiveRuleKeys("Sonar way"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));

            downloader.Pages["http://localhost:9000/api/profiles/index?language=cs&name=Sonar%20way"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true,\"rules\":[{\"key\":\"My_Own_FxCop_Rule\",\"repo\":\"fxcop\",\"severity\":\"MAJOR\",\"params\":[{\"key\":\"CheckId\",\"value\":\"CA_MyOwnCustomRule\"}]}]}]";
            expected = new List<string>();
            expected.Add("CA_MyOwnCustomRule");
            actual = new List<string>(ws.GetActiveRuleKeys("Sonar way"));
            Assert.AreEqual(true, expected.SequenceEqual(actual));
        }

        [TestMethod]
        public void GetInternalKeys()
        {
            downloader.Pages["http://localhost:9000/api/rules/search?f=internalKey&ps=" + int.MaxValue + "&repositories=fxcop"] = "{\"total\":2,\"p\":1,\"ps\":10,\"rules\":[{\"key\":\"fxcop:My_Own_FxCop_Rule\"},{\"key\":\"fxcop:UriParametersShouldNotBeStrings\",\"internalKey\":\"CA1054\"}]}";
            var expected = new Dictionary<string, string>();
            expected["fxcop:My_Own_FxCop_Rule"] = null;
            expected["fxcop:UriParametersShouldNotBeStrings"] = "CA1054";
            var actual = ws.GetInternalKeys();

            Assert.AreEqual(true, expected.Count == actual.Count && !expected.Except(actual).Any());
        }

        [TestMethod]
        public void GetProperties()
        {
            downloader.Pages["http://localhost:9000/api/properties?resource=foo%20bar"] = "[{\"key\": \"sonar.property1\",\"value\": \"value1\"},{\"key\": \"sonar.property2\",\"value\": \"value2\"}]";
            var expected = new Dictionary<string, string>();
            expected["sonar.property1"] = "value1";
            expected["sonar.property2"] = "value2";
            var actual = ws.GetProperties("foo bar");

            Assert.AreEqual(true, expected.Count == actual.Count && !expected.Except(actual).Any());
        }

        // TODO Rewrite this as a data driven unit test to not duplicate the test contents?
        [TestMethod]
        public void ServerUrlWithTrailingSlash()
        {
            ws = new SonarWebService(downloader, "http://localhost:9000/", "cs", "fxcop");

            downloader.Pages["http://localhost:9000/api/profiles/list?language=cs&project=foo%20bar"] = "[{\"name\":\"profile1\",\"language\":\"cs\",\"default\":true}]";
            Assert.AreEqual("profile1", ws.GetQualityProfile("foo bar"));

            downloader.Pages["http://localhost:9000/api/profiles/index?language=cs&name=Sonar%20way"] = "[{\"name\":\"Sonar way\",\"language\":\"cs\",\"default\":true}]";
            var expected1 = new List<string>();
            var actual1 = new List<string>(ws.GetActiveRuleKeys("Sonar way"));
            Assert.AreEqual(true, expected1.SequenceEqual(actual1));

            downloader.Pages["http://localhost:9000/api/rules/search?f=internalKey&ps=" + int.MaxValue + "&repositories=fxcop"] = "{\"total\":2,\"p\":1,\"ps\":10,\"rules\":[{\"key\":\"fxcop:My_Own_FxCop_Rule\"},{\"key\":\"fxcop:UriParametersShouldNotBeStrings\",\"internalKey\":\"CA1054\"}]}";
            var expected2 = new Dictionary<string, string>();
            expected2["fxcop:My_Own_FxCop_Rule"] = null;
            expected2["fxcop:UriParametersShouldNotBeStrings"] = "CA1054";
            var actual2 = ws.GetInternalKeys();

            Assert.AreEqual(true, expected2.Count == actual2.Count && !expected2.Except(actual2).Any());

            downloader.Pages["http://localhost:9000/api/properties?resource=foo%20bar"] = "[{\"key\": \"sonar.property1\",\"value\": \"value1\"},{\"key\": \"sonar.property2\",\"value\": \"value2\"}]";
            var expected3 = new Dictionary<string, string>();
            expected3["sonar.property1"] = "value1";
            expected3["sonar.property2"] = "value2";
            var actual3 = ws.GetProperties("foo bar");

            Assert.AreEqual(true, expected3.Count == actual3.Count && !expected3.Except(actual3).Any());
        }

        [TestMethod]
        public void Dispose()
        {
            Assert.AreEqual(false, downloader.Disposed);
            ws.Dispose();
            Assert.AreEqual(true, downloader.Disposed);
        }

        private class TestDownloader : IDownloader
        {
            public IDictionary<string, string> Pages = new Dictionary<string, string>();
            public List<string> AccessedUrls = new List<string>();
            public bool Disposed = false;

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
                Disposed = true;
            }
        }
    }
}
