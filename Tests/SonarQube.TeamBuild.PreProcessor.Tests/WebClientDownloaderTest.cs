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
using TestUtilities;
using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor.UnitTests
{
    [TestClass]
    public class WebClientDownloaderTest
    {
        [TestMethod]
        public void Credentials()
        {
            ILogger logger = new TestLogger();

            WebClientDownloader downloader;
            downloader = new WebClientDownloader(null, null, logger);
            Assert.AreEqual(null, downloader.GetHeader(HttpRequestHeader.Authorization));

            downloader = new WebClientDownloader("da39a3ee5e6b4b0d3255bfef95601890afd80709", null, logger);
            Assert.AreEqual("Basic ZGEzOWEzZWU1ZTZiNGIwZDMyNTViZmVmOTU2MDE4OTBhZmQ4MDcwOTo=", downloader.GetHeader(HttpRequestHeader.Authorization));

            downloader = new WebClientDownloader(null, "password", logger);
            Assert.AreEqual(null, downloader.GetHeader(HttpRequestHeader.Authorization));

            downloader = new WebClientDownloader("admin", "password", logger);
            Assert.AreEqual("Basic YWRtaW46cGFzc3dvcmQ=", downloader.GetHeader(HttpRequestHeader.Authorization));
        }

        [TestMethod]
        public void SemicolonInUsername()
        {
            ArgumentException actual = AssertException.Expects<ArgumentException>(() => new WebClientDownloader("user:name", "", new TestLogger()));
            Assert.AreEqual(Resources.WCD_UserNameCannotContainColon, actual.Message);
        }

        [TestMethod]
        public void AccentsInUsername()
        {
            ArgumentException actual = AssertException.Expects<ArgumentException>(() => new WebClientDownloader("héhé", "password", new TestLogger()));
            Assert.AreEqual(Resources.WCD_UserNameMustBeAscii, actual.Message);
        }

        [TestMethod]
        public void AccentsInPassword()
        {
            ArgumentException actual = AssertException.Expects<ArgumentException>(() => new WebClientDownloader("username", "héhé", new TestLogger()));
            Assert.AreEqual(Resources.WCD_UserNameMustBeAscii, actual.Message);
        }
    }
}
