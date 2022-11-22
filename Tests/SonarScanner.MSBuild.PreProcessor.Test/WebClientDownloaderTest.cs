/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class WebClientDownloaderTest
    {
        private const string TestContent = "test content";

        private readonly Uri testUri = new("https://www.sonarsource.com/");

        [TestMethod]
        public void Credentials()
        {
            ILogger logger = new TestLogger();

            WebClientDownloader downloader;
            downloader = new WebClientDownloader(null, null, logger);
            downloader.GetHeader(HttpRequestHeader.Authorization).Should().BeNull();

            downloader = new WebClientDownloader("da39a3ee5e6b4b0d3255bfef95601890afd80709", null, logger);
            downloader.GetHeader(HttpRequestHeader.Authorization).Should().Be("Basic ZGEzOWEzZWU1ZTZiNGIwZDMyNTViZmVmOTU2MDE4OTBhZmQ4MDcwOTo=");

            downloader = new WebClientDownloader(null, "password", logger);
            downloader.GetHeader(HttpRequestHeader.Authorization).Should().BeNull();

            downloader = new WebClientDownloader("admin", "password", logger);
            downloader.GetHeader(HttpRequestHeader.Authorization).Should().Be("Basic YWRtaW46cGFzc3dvcmQ=");
        }

        [TestMethod]
        public void UserAgent()
        {
            // Arrange
            var downloader = new WebClientDownloader(null, null, new TestLogger());

            // Act
            var userAgent = downloader.GetHeader(HttpRequestHeader.UserAgent);

            // Assert
            var scannerVersion = typeof(WebClientDownloaderTest).Assembly.GetName().Version.ToDisplayString();
            userAgent.Should().Be($"ScannerMSBuild/{scannerVersion}");
        }

        [TestMethod]
        public void UserAgent_OnSubsequentCalls()
        {
            // Arrange
            var expectedUserAgent = string.Format("ScannerMSBuild/{0}",
                typeof(WebClientDownloaderTest).Assembly.GetName().Version.ToDisplayString());
            var downloader = new WebClientDownloader(null, null, new TestLogger());

            // Act & Assert
            var userAgent = downloader.GetHeader(HttpRequestHeader.UserAgent);
            userAgent.Should().Be(expectedUserAgent);

            try
            {
                downloader.Download(new Uri("http://DoesntMatterThisMayNotExistAndItsFine.com"));
            }
            catch (Exception)
            {
                // It doesn't matter if the request is successful or not.
            }

            // Check if the user agent is still present after the request.
            userAgent = downloader.GetHeader(HttpRequestHeader.UserAgent);
            userAgent.Should().Be(expectedUserAgent);
        }

        [TestMethod]
        public void SemicolonInUsername()
        {
            Action act = () => new WebClientDownloader("user:name", "", new TestLogger());
            act.Should().ThrowExactly<ArgumentException>().WithMessage("username cannot contain the ':' character due to basic authentication limitations");
        }

        [TestMethod]
        public void AccentsInUsername()
        {
            Action act = () => new WebClientDownloader("héhé", "password", new TestLogger());
            act.Should().ThrowExactly<ArgumentException>().WithMessage("username and password should contain only ASCII characters due to basic authentication limitations");
        }

        [TestMethod]
        public void AccentsInPassword()
        {
            Action act = () => new WebClientDownloader("username", "héhé", new TestLogger());
            act.Should().ThrowExactly<ArgumentException>().WithMessage("username and password should contain only ASCII characters due to basic authentication limitations");
        }

        [TestMethod]
        public void UsingClientCert()
        {
            Action act = () => new WebClientDownloader(null, null, new TestLogger(), "certtestsonar.pem", "dummypw");
            act.Should().NotThrow();
        }

        [TestMethod]
        public void Implements_Dispose()
        {
            // Arrange
            var testDownloader = new TestDownloader(null, null, new TestLogger(), null, null);

            // Act
            testDownloader.Dispose();

            // Assert
            testDownloader.IsDisposedCalled.Should().BeTrue();
        }

        [TestMethod]
        public void MultipleDisposeCallsNotFailing()
        {
            // Arrange
            var testDownloader = new TestDownloader(null, null, new TestLogger(), null, null);

            // Act
            testDownloader.Dispose();
            testDownloader.Dispose();

            // Assert
            testDownloader.IsDisposedCalled.Should().BeTrue();
        }

        [TestMethod]
        public async Task DownloadStream_Success()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.OK);

            using var stream = await sut.DownloadStream(testUri);
            using var reader = new StreamReader(stream);

            var text = await reader.ReadToEndAsync();
            text.Should().Be(TestContent);
            logger.AssertDebugLogged("Downloading from https://www.sonarsource.com/...");
        }

        [TestMethod]
        public async Task DownloadStream_Fail()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.NotFound);

            using var stream = await sut.DownloadStream(testUri);

            stream.Should().BeNull();
            logger.AssertDebugLogged("Downloading from https://www.sonarsource.com/...");
            logger.AssertInfoLogged("Downloading from https://www.sonarsource.com/ failed. Http status code is NotFound.");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task Download_Success()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.OK);

            var text = await sut.Download(testUri);

            text.Should().Be(TestContent);
            logger.AssertDebugLogged("Downloading from https://www.sonarsource.com/...");
        }

        [TestMethod]
        public async Task Download_Fail_NoLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.Forbidden);

            var text = await sut.Download(testUri);

            text.Should().BeNull();
            logger.AssertDebugLogged("Downloading from https://www.sonarsource.com/...");
            logger.AssertInfoLogged("Downloading from https://www.sonarsource.com/ failed. Http status code is Forbidden.");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task Download_Fail_Forbidden_WithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.Forbidden);

            await sut.Invoking(async x => await x.Download(testUri, true)).Should().ThrowAsync<HttpRequestException>();

            logger.AssertDebugLogged("Downloading from https://www.sonarsource.com/...");
            logger.AssertWarningLogged("To analyze private projects make sure the scanner user has 'Browse' permission.");
        }

        [TestMethod]
        public async Task Download_Fail_NotForbidden_WithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.NotFound);

            var text = await sut.Download(testUri, true);

            text.Should().BeNull();
            logger.AssertDebugLogged("Downloading from https://www.sonarsource.com/...");
            logger.AssertNoWarningsLogged();
        }

        private static HttpClient MockHttpClient(HttpResponseMessage responseMessage)
        {
            var httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            return new HttpClient(httpMessageHandlerMock.Object);
        }

        private static WebClientDownloader CreateSut(ILogger logger, HttpStatusCode statusCode)
        {
            var client = MockHttpClient(new HttpResponseMessage { StatusCode = statusCode, Content = new StringContent(TestContent) });
            return new("username", "password", logger, client: client);
        }

        private sealed class TestDownloader : WebClientDownloader
        {
            public bool IsDisposedCalled { get; private set; }

            public TestDownloader(string userName, string password, ILogger logger, string clientCertPath = null, string clientCertPassword = null) : base(userName, password, logger,  clientCertPath, clientCertPassword) { }

            protected override void Dispose(bool disposing)
            {
                disposing.Should().BeTrue();
                base.Dispose(disposing);
                IsDisposedCalled = true;
            }
        }
    }
}
