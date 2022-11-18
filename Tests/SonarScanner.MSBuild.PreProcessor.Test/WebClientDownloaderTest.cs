﻿/*
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
            const string testContent = "test content";
            var logger = new TestLogger();
            var httpClient = MockHttpClient(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(testContent) });

            var sut = new WebClientDownloader("username", "password", logger, httpClient: httpClient);
            var stream = await sut.DownloadStream(new Uri("https://test.com"));

            var text = await new StreamReader(stream).ReadToEndAsync();
            text.Should().Be(testContent);

            logger.AssertDebugLogged("Downloading from https://test.com/...");
        }

        [TestMethod]
        public async Task DownloadStream_Fail_ForbiddenResponse_WithLog()
        {
            const string testContent = "test content";
            var logger = new TestLogger();
            var httpClient = MockHttpClient(new HttpResponseMessage { StatusCode = HttpStatusCode.Forbidden, Content = new StringContent(testContent) });

            var sut = new WebClientDownloader("username", "password", logger, httpClient: httpClient);
            await new Func<Task<Stream>>(async () => await sut.DownloadStream(new Uri("https://test.com"), true))
                  .Should().ThrowAsync<HttpRequestException>();

            logger.AssertDebugLogged("Downloading from https://test.com/...");
            logger.AssertWarningLogged("To analyze private projects make sure the scanner user has 'Browse' permission.");
        }

        [TestMethod]
        public async Task DownloadStream_Fail_NotForbidden_WithLog()
        {
            const string testContent = "test content";
            var logger = new TestLogger();
            var httpClient = MockHttpClient(new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound, Content = new StringContent(testContent) });

            var sut = new WebClientDownloader("username", "password", logger, httpClient: httpClient);
            var stream = await sut.DownloadStream(new Uri("https://test.com"), true);

            stream.Should().BeNull();

            logger.AssertDebugLogged("Downloading from https://test.com/...");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task DownloadStream_Fail_NoLog()
        {
            const string testContent = "test content";
            var logger = new TestLogger();
            var httpClient = MockHttpClient(new HttpResponseMessage { StatusCode = HttpStatusCode.Forbidden, Content = new StringContent(testContent) });

            var sut = new WebClientDownloader("username", "password", logger, httpClient: httpClient);
            var stream = await sut.DownloadStream(new Uri("https://test.com"));

            stream.Should().BeNull();

            logger.AssertDebugLogged("Downloading from https://test.com/...");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task Download_Success()
        {
            const string testContent = "test content";
            var logger = new TestLogger();
            var httpClient = MockHttpClient(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(testContent) });

            var sut = new WebClientDownloader("username", "password", logger, httpClient: httpClient);
            var text = await sut.Download(new Uri("https://test.com"));

            text.Should().Be(testContent);

            logger.AssertDebugLogged("Downloading from https://test.com/...");
        }

        [TestMethod]
        public async Task Download_Fail_NoLog()
        {
            const string testContent = "test content";
            var logger = new TestLogger();
            var httpClient = MockHttpClient(new HttpResponseMessage { StatusCode = HttpStatusCode.Forbidden, Content = new StringContent(testContent) });

            var sut = new WebClientDownloader("username", "password", logger, httpClient: httpClient);
            var text = await sut.Download(new Uri("https://test.com"));

            text.Should().BeNull();

            logger.AssertDebugLogged("Downloading from https://test.com/...");
            logger.AssertNoWarningsLogged();
        }

        private static HttpClient MockHttpClient(HttpResponseMessage responseMessage)
        {
            var httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage)
                .Verifiable();

            return new HttpClient(httpMessageHandlerMock.Object);
        }

        private sealed class TestDownloader : WebClientDownloader
        {
            public bool IsDisposedCalled { get; private set; } = false;

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
