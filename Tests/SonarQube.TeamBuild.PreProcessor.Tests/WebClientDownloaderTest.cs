/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
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
