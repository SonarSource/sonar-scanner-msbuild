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
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using TestUtilities;

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
            var actual = AssertException.Expects<ArgumentException>(() => new WebClientDownloader("user:name", "", new TestLogger()));
            Assert.AreEqual("username cannot contain the ':' character due to basic authentication limitations", actual.Message);
        }

        [TestMethod]
        public void AccentsInUsername()
        {
            var actual = AssertException.Expects<ArgumentException>(() => new WebClientDownloader("héhé", "password", new TestLogger()));
            Assert.AreEqual("username and password should contain only ASCII characters due to basic authentication limitations", actual.Message);
        }

        [TestMethod]
        public void AccentsInPassword()
        {
            var actual = AssertException.Expects<ArgumentException>(() => new WebClientDownloader("username", "héhé", new TestLogger()));
            Assert.AreEqual("username and password should contain only ASCII characters due to basic authentication limitations", actual.Message);
        }
    }
}
