//-----------------------------------------------------------------------
// <copyright file="WebClientDownloaderTest.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------


using System;
using System.Text;
using System.Collections.Generic;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sonar.TeamBuild.PreProcessor.UnitTests
{
    [TestClass]
    public class WebClientDownloaderTest
    {
        [TestMethod]
        public void Credentials()
        {
            var client = new WebClient();
            new WebClientDownloader(client, null, null);
            Assert.AreEqual(null, client.Headers[HttpRequestHeader.Authorization]);

            new WebClientDownloader(client, "admin", null);
            Assert.AreEqual(null, client.Headers[HttpRequestHeader.Authorization]);

            new WebClientDownloader(client, null, "password");
            Assert.AreEqual(null, client.Headers[HttpRequestHeader.Authorization]);

            new WebClientDownloader(client, "admin", "password");
            Assert.AreEqual("Basic YWRtaW46cGFzc3dvcmQ=", client.Headers[HttpRequestHeader.Authorization]);
        }

        [TestMethod]
        public void SemicolonInUsername()
        {
            try
            {
                new WebClientDownloader(new WebClient(), "user:name", "");
            }
            catch (ArgumentException e)
            {
                if ("username cannot contain the ':' character due to basic authentication limitations".Equals(e.Message))
                {
                    return;
                }
            }

            Assert.Fail();
        }

        [TestMethod]
        public void AccentsInUsername()
        {
            try
            {
                new WebClientDownloader(new WebClient(), "héhé", "password");
            }
            catch (ArgumentException e)
            {
                if ("username and password should contain only ASCII characters due to basic authentication limitations".Equals(e.Message))
                {
                    return;
                }
            }

            Assert.Fail();
        }

        [TestMethod]
        public void AccentsInPassword()
        {
            try
            {
                new WebClientDownloader(new WebClient(), "username", "héhé");
            }
            catch (ArgumentException e)
            {
                if ("username and password should contain only ASCII characters due to basic authentication limitations".Equals(e.Message))
                {
                    return;
                }
            }

            Assert.Fail();
        }
    }
}
