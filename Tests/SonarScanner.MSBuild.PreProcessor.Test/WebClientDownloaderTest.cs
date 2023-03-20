/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
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
        private static readonly Uri TestUri = new("https://www.sonarsource.com/");

        [TestMethod]
        public void Ctor_NullArguments()
        {
            FluentActions.Invoking(() => new WebClientDownloader(null, null, null)).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("client");
            FluentActions.Invoking(() => new WebClientDownloader(new HttpClient(), null, null)).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("baseUri");
            FluentActions.Invoking(() => new WebClientDownloader(new HttpClient(), string.Empty, null)).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("baseUri");
            FluentActions.Invoking(() => new WebClientDownloader(new HttpClient(), TestUri.OriginalString, null)).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void Implements_Dispose()
        {
            var httpClient = new Mock<HttpClient>();
            httpClient.Protected().Setup("Dispose", ItExpr.IsAny<bool>()).Verifiable();

            var sut = new WebClientDownloader(httpClient.Object, TestUri.OriginalString, new TestLogger());

            sut.Dispose();

            httpClient.Verify();
        }

        [TestMethod]
        public async Task DownloadStream_Success()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.OK);

            using var stream = await sut.DownloadStream(TestUri);
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

            using var stream = await sut.DownloadStream(TestUri);

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

            var text = await sut.Download(TestUri);

            text.Should().Be(TestContent);
            logger.AssertDebugLogged("Downloading from https://www.sonarsource.com/...");
        }

        [TestMethod]
        public async Task Download_Fail_NoLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.Forbidden);

            var text = await sut.Download(TestUri);

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

            await sut.Invoking(async x => await x.Download(TestUri, true)).Should().ThrowAsync<HttpRequestException>();

            logger.AssertDebugLogged("Downloading from https://www.sonarsource.com/...");
            logger.AssertWarningLogged("To analyze private projects make sure the scanner user has 'Browse' permission.");
        }

        [TestMethod]
        public async Task Download_Fail_NotForbidden_WithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.NotFound);

            var text = await sut.Download(TestUri, true);

            text.Should().BeNull();
            logger.AssertDebugLogged("Downloading from https://www.sonarsource.com/...");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        [DataRow("https://sonarsource.com/")]
        [DataRow("https://sonarsource.com")]
        [DataRow("https://sonarsource.com/sonarlint")]
        [DataRow("https://sonarsource.com/sonarlint/")]
        public void GetBaseUri_ValidUrl_ShouldAlwaysEndsWithSlash(string baseUrl)
        {
            var expectedUrl = baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
            var sut = new WebClientDownloader(new HttpClient(), baseUrl, Mock.Of<ILogger>());

            var result = sut.GetBaseUri();

            result.ToString().Should().EndWith("/");
            result.ToString().Should().Be(expectedUrl);
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

        private static WebClientDownloader CreateSut(ILogger logger, HttpStatusCode statusCode) =>
            new(MockHttpClient(new HttpResponseMessage { StatusCode = statusCode, Content = new StringContent(TestContent) }), TestUri.OriginalString, logger);
    }
}
