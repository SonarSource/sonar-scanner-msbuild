//-----------------------------------------------------------------------
// <copyright file="WebClientDownloaderTest.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using System;
using System.Text;
using System.Collections.Generic;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarQube.TeamBuild.PreProcessor.UnitTests
{
    [TestClass]
    public class WebClientDownloaderTest
    {
        [TestMethod]
        public void Credentials()
        {
            WebClientDownloader downloader;
            downloader = new WebClientDownloader(null, null);
            Assert.AreEqual(null, downloader.GetHeader(HttpRequestHeader.Authorization));

            downloader = new WebClientDownloader("admin", null);
            Assert.AreEqual(null, downloader.GetHeader(HttpRequestHeader.Authorization));

            downloader = new WebClientDownloader(null, "password");
            Assert.AreEqual(null, downloader.GetHeader(HttpRequestHeader.Authorization));

            downloader = new WebClientDownloader("admin", "password");
            Assert.AreEqual("Basic YWRtaW46cGFzc3dvcmQ=", downloader.GetHeader(HttpRequestHeader.Authorization));
        }

        [TestMethod]
        public void SemicolonInUsername()
        {
            try
            {
                new WebClientDownloader("user:name", "");
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
                new WebClientDownloader("héhé", "password");
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
                new WebClientDownloader("username", "héhé");
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
