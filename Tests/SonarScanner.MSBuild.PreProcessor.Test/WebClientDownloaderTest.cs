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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
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
        private const string BaseUrl = "https://www.sonarsource.com/";
        private const string RelativeUrl = "api/relative";

        [TestMethod]
        public void Ctor_NullArguments()
        {
            FluentActions.Invoking(() => new WebClientDownloader(null, null, null)).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("client");
            FluentActions.Invoking(() => new WebClientDownloader(new HttpClient(), null, null)).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("baseUri");
            FluentActions.Invoking(() => new WebClientDownloader(new HttpClient(), string.Empty, null)).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("baseUri");
            FluentActions.Invoking(() => new WebClientDownloader(new HttpClient(), BaseUrl, null)).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void Implements_Dispose()
        {
            var httpClient = new Mock<HttpClient>();
            httpClient.Protected().Setup("Dispose", ItExpr.IsAny<bool>()).Verifiable();

            var sut = new WebClientDownloader(httpClient.Object, BaseUrl, new TestLogger());

            sut.Dispose();

            httpClient.Verify();
        }

        [TestMethod]
        public async Task DownloadStream_Success()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.OK);

            using var stream = await sut.DownloadStream(RelativeUrl);
            using var reader = new StreamReader(stream);

            var text = await reader.ReadToEndAsync();
            text.Should().Be(TestContent);
            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
        }

        [TestMethod]
        public async Task DownloadStream_Fail()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.NotFound);

            using var stream = await sut.DownloadStream(RelativeUrl);

            stream.Should().BeNull();
            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
            logger.AssertInfoLogged($"Downloading from {BaseUrl}{RelativeUrl} failed. Http status code is NotFound.");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task DownloadResource_ReturnsTheResponse()
        {
            var logger = new TestLogger();
            var response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(TestContent) };
            var sut = new WebClientDownloader(MockHttpClient(response), BaseUrl, logger);

            var responseMessage = await sut.DownloadResource(RelativeUrl);

            responseMessage.Should().Be(response);
            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
        }

        [TestMethod]
        public async Task Download_Success()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.OK);

            var text = await sut.Download(RelativeUrl);

            text.Should().Be(TestContent);
            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
        }

        [TestMethod]
        public async Task Download_Fail_NoLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.Forbidden);

            var text = await sut.Download(RelativeUrl);

            text.Should().BeNull();
            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
            logger.AssertInfoLogged($"Downloading from {BaseUrl}{RelativeUrl} failed. Http status code is Forbidden.");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task Download_Fail_Forbidden_WithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.Forbidden);

            await sut.Invoking(async x => await x.Download(RelativeUrl, true)).Should().ThrowAsync<HttpRequestException>();

            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
            logger.AssertWarningLogged("To analyze private projects make sure the scanner user has 'Browse' permission.");
        }

        [TestMethod]
        public async Task Download_Fail_NotForbidden_WithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.NotFound);

            var text = await sut.Download(RelativeUrl, true);

            text.Should().BeNull();
            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task TryGetLicenseInformation_HttpCodeOk_SucceedWithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.OK);

            var text = await sut.TryGetLicenseInformation(RelativeUrl);

            text.Should().NotBeNull();
            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task TryGetLicenseInformation_HttpCodeUnauthorized_ShouldThrowWithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.Unauthorized);

            Func<Task> act = async () => await sut.TryGetLicenseInformation(RelativeUrl);

            await act.Should().ThrowExactlyAsync<ArgumentException>().WithMessage("The token you provided doesn't have sufficient rights to check license.");
            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task TryDownloadIfExists_HttpCodeOk_SucceedWithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.OK);

            var result = await sut.TryDownloadIfExists(RelativeUrl);

            result.Item1.Should().BeTrue();
            result.Item2.Should().Be(TestContent);
            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task TryDownloadIfExists_HttpCodeNotFound_SucceedWithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.NotFound);

            var result = await sut.TryDownloadIfExists(RelativeUrl);

            result.Item1.Should().BeFalse();
            result.Item2.Should().BeNull();
            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task TryDownloadIfExists_HttpCodeForbidden_ShouldThrowWithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.Forbidden);

            Func<Task> act = async () => await sut.TryDownloadIfExists(RelativeUrl);

            await act.Should().ThrowExactlyAsync<HttpRequestException>();
            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task TryDownloadIfExists_HttpCodeForbiddenWithWarnLog_ShouldThrowWithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.Forbidden);

            Func<Task> act = async () => await sut.TryDownloadIfExists(RelativeUrl, true);

            await act.Should().ThrowExactlyAsync<HttpRequestException>();
            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
            logger.AssertWarningLogged("To analyze private projects make sure the scanner user has 'Browse' permission.");
        }

        public static IEnumerable<object[]> UnsuccessfulHttpCodeData =>
            Enum.GetValues(typeof(HttpStatusCode))
                .Cast<HttpStatusCode>()
                .Where(x => ((int)x >= 300 || (int)x < 200) && x != HttpStatusCode.Forbidden && x != HttpStatusCode.NotFound) // Those have specific tested behavior
                .Select(x => new object[] { x });

        [TestMethod]
        [DynamicData(nameof(UnsuccessfulHttpCodeData))]
        public async Task TryDownloadIfExists_UnsuccessfulHttpCode_ShouldThrowWithLog(HttpStatusCode code)
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, code);

            Func<Task> act = async () => await sut.TryDownloadIfExists(RelativeUrl);

            await act.Should().ThrowExactlyAsync<HttpRequestException>();
            logger.AssertDebugLogged($"Downloading from {BaseUrl}{RelativeUrl}...");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task TryDownloadFileIfExists_HttpCodeOk_SucceedWithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.OK);
            var fileName = Path.GetRandomFileName();

            var result = await sut.TryDownloadFileIfExists(RelativeUrl, fileName);

            result.Should().BeTrue();
            logger.AssertDebugLogged($"Downloading file from {BaseUrl}{RelativeUrl} to {fileName}...");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task TryDownloadFileIfExists_HttpCodeNotFound_SucceedWithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.NotFound);
            var fileName = Path.GetRandomFileName();

            var result = await sut.TryDownloadFileIfExists(RelativeUrl, fileName);

            result.Should().BeFalse();
            logger.AssertDebugLogged($"Downloading file from {BaseUrl}{RelativeUrl} to {fileName}...");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task TryDownloadFileIfExists_HttpCodeForbidden_ShouldThrowWithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.Forbidden);
            var fileName = Path.GetRandomFileName();

            Func<Task> act = async () => await sut.TryDownloadFileIfExists(RelativeUrl, fileName);

            await act.Should().ThrowExactlyAsync<HttpRequestException>();
            logger.AssertDebugLogged($"Downloading file from {BaseUrl}{RelativeUrl} to {fileName}...");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task TryDownloadFileIfExists_HttpCodeForbiddenWithWarnLog_ShouldThrowWithLog()
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, HttpStatusCode.Forbidden);
            var fileName = Path.GetRandomFileName();

            Func<Task> act = async () => await sut.TryDownloadFileIfExists(RelativeUrl, fileName, true);

            await act.Should().ThrowExactlyAsync<HttpRequestException>();
            logger.AssertDebugLogged($"Downloading file from {BaseUrl}{RelativeUrl} to {fileName}...");
            logger.AssertWarningLogged("To analyze private projects make sure the scanner user has 'Browse' permission.");
        }

        [TestMethod]
        [DynamicData(nameof(UnsuccessfulHttpCodeData))]
        public async Task TryDownloadFileIfExists_UnsuccessfulHttpCode_ShouldThrowWithLog(HttpStatusCode code)
        {
            var logger = new TestLogger();
            var sut = CreateSut(logger, code);
            var fileName = Path.GetRandomFileName();

            Func<Task> act = async () => await sut.TryDownloadFileIfExists(RelativeUrl, fileName);

            await act.Should().ThrowExactlyAsync<HttpRequestException>();
            logger.AssertDebugLogged($"Downloading file from {BaseUrl}{RelativeUrl} to {fileName}...");
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

        private static WebClientDownloader CreateSut(ILogger logger, HttpStatusCode statusCode) =>
            new(MockHttpClient(new HttpResponseMessage { StatusCode = statusCode, Content = new StringContent(TestContent) }), BaseUrl, logger);
    }
}
