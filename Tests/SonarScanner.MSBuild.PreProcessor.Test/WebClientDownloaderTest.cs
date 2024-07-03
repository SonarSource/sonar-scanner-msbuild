/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class WebClientDownloaderTest
{
    private const string TestContent = "test content";
    private const string BaseUrl = "https://www.sonarsource.com/";
    private const string RelativeUrl = "api/relative";

    private TestLogger testLogger;

    [TestInitialize]
    public void Init() =>
        testLogger = new TestLogger();

    [TestMethod]
    public void Ctor_NullArguments()
    {
        FluentActions.Invoking(() => new WebClientDownloader(null, null, null)).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("client");
        FluentActions.Invoking(() => new WebClientDownloader(new HttpClient(), null, null)).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("baseUri");
        FluentActions.Invoking(() => new WebClientDownloader(new HttpClient(), string.Empty, null)).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("baseUri");
        FluentActions.Invoking(() => new WebClientDownloader(new HttpClient(), BaseUrl, null)).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void GetBaseUrl_ReturnsTheBaseUrl() =>
        new WebClientDownloader(new HttpClient(), BaseUrl, testLogger).GetBaseUrl().Should().Be(BaseUrl);

    [TestMethod]
    [DataRow("https://sonarsource.com/", "https://sonarsource.com/")]
    [DataRow("https://sonarsource.com", "https://sonarsource.com/")]
    [DataRow("https://sonarsource.com/sonarlint", "https://sonarsource.com/sonarlint/")]
    [DataRow("https://sonarsource.com/sonarlint/", "https://sonarsource.com/sonarlint/")]
    public void Ctor_ValidBaseUrl_ShouldAlwaysEndsWithSlash(string baseUrl, string expectedUrl)
    {
        var httpClient = new HttpClient();

        _ = new WebClientDownloader(httpClient, baseUrl, Substitute.For<ILogger>());

        httpClient.BaseAddress.ToString().Should().Be(expectedUrl);
    }

    [TestMethod]
    public void Implements_Dispose()
    {
        var httpClient = Substitute.For<HttpClient>();
        var sut = new WebClientDownloader(httpClient, BaseUrl, Substitute.For<ILogger>());

        sut.Dispose();

        httpClient.Received().Dispose();
    }

    [TestMethod]
    public async Task DownloadStream_Success()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.OK);

        using var stream = await sut.DownloadStream(RelativeUrl);
        using var reader = new StreamReader(stream);

        var text = await reader.ReadToEndAsync();
        text.Should().Be(TestContent);
        testLogger.AssertDebugLogged("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.AssertDebugLogged("Response received from https://www.sonarsource.com/api/relative...");
        testLogger.AssertNoErrorsLogged();
    }

    [TestMethod]
    public async Task DownloadStream_Fail()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.NotFound);

        using var stream = await sut.DownloadStream(RelativeUrl);

        stream.Should().BeNull();
        testLogger.AssertDebugLogged("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.AssertInfoLogged("Downloading from https://www.sonarsource.com/api/relative failed. Http status code is NotFound.");
        testLogger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task DownloadResource_HttpCodeOk_ReturnsTheResponse()
    {
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(TestContent), RequestMessage = new() { RequestUri = new("https://www.sonarsource.com/api/relative") }};
        var sut = new WebClientDownloader(MockHttpClient(response), BaseUrl, testLogger);

        var responseMessage = await sut.DownloadResource(RelativeUrl);

        responseMessage.Should().Be(response);
        testLogger.AssertDebugLogged("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task Download_Success()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.OK);

        var text = await sut.Download(RelativeUrl);

        text.Should().Be(TestContent);
        testLogger.AssertDebugLogged("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.AssertNoWarningsLogged();
        testLogger.AssertNoErrorsLogged();
    }

    [TestMethod]
    public async Task Download_Fail_NoLog()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.Forbidden);

        var text = await sut.Download(RelativeUrl);

        text.Should().BeNull();
        testLogger.AssertDebugLogged("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.AssertInfoLogged("Downloading from https://www.sonarsource.com/api/relative failed. Http status code is Forbidden.");
        testLogger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task Download_Fail_ForbiddenAndLogsWarning()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.Forbidden);

        await sut.Invoking(async x => await x.Download(RelativeUrl, true)).Should().ThrowAsync<HttpRequestException>();

        testLogger.AssertDebugLogged("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.AssertWarningLogged("To analyze private projects make sure the scanner user has 'Browse' permission.");
    }

    [TestMethod]
    public async Task Download_Fail_NotForbidden()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.NotFound);

        var text = await sut.Download(RelativeUrl, true);

        text.Should().BeNull();
        testLogger.AssertDebugLogged("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.AssertNoWarningsLogged();
    }

    [TestMethod]
    [DataRow("https://sonarsource.com/", "https://sonarsource.com/api/relative")]
    [DataRow("https://sonarsource.com", "https://sonarsource.com/api/relative")]
    [DataRow("https://sonarsource.com/sonarlint", "https://sonarsource.com/sonarlint/api/relative")]
    [DataRow("https://sonarsource.com/sonarlint/", "https://sonarsource.com/sonarlint/api/relative")]
    public async Task Download_CorrectAbsoluteUrl_ShouldSucceed(string baseUrl, string expectedAbsoluteUrl)
    {
        // We want to make sure the request uri is the expected absolute url
        var httpMessageHandlerMock = new HttpMessageHandlerMock((request, cancel) =>
            request.RequestUri == new Uri(expectedAbsoluteUrl)
                ? Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(TestContent),
                    RequestMessage = new HttpRequestMessage { RequestUri = new Uri(expectedAbsoluteUrl) }
                })
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var sut = new WebClientDownloader(new HttpClient(httpMessageHandlerMock), baseUrl, testLogger);

        _ = await sut.Download("api/relative", true);

        httpMessageHandlerMock.Requests.Should().ContainSingle(x => x.RequestUri == new Uri(expectedAbsoluteUrl));
        testLogger.AssertDebugLogged(string.Format(Resources.MSG_Downloading, expectedAbsoluteUrl));
    }

    [TestMethod]
    [DataRow("https://sonarsource.com/")]
    [DataRow("https://sonarsource.com")]
    public async Task Download_RelativeUrl_SlashPrefix_ShouldThrow(string baseUrl)
    {
        var sut = new WebClientDownloader(new HttpClient(), baseUrl, testLogger);

        await FluentActions.Invoking(async () => await sut.Download("/starts/with/slash"))
            .Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("The BaseAddress always ends in '/'. Please call this method with a url that does not start with '/'.");
    }

    [TestMethod]
    public async Task Download_HttpClientThrowAnyException_ShouldThrowAndLogError()
    {
        var httpMessageHandlerMock = new HttpMessageHandlerMock((request, cancel) => Task.FromException<HttpResponseMessage>(new Exception("error")));
        var sut = new WebClientDownloader(new HttpClient(httpMessageHandlerMock), BaseUrl, testLogger);

        Func<Task> act = async () =>  await sut.Download("api/relative", true);

        await act.Should().ThrowAsync<Exception>();
        testLogger.AssertSingleErrorExists("Unable to connect to server. Please check if the server is running and if the address is correct. Url: 'https://www.sonarsource.com/api/relative'.");
    }

    [TestMethod]
    public async Task Download_HttpClientThrowConnectionFailure_ShouldThrowAndLogError()
    {
        var exception = new HttpRequestException(string.Empty, new WebException(string.Empty, WebExceptionStatus.ConnectFailure));

        var httpMessageHandlerMock = new HttpMessageHandlerMock((request, cancel) => Task.FromException<HttpResponseMessage>(exception));

        var sut = new WebClientDownloader(new HttpClient(httpMessageHandlerMock), BaseUrl, testLogger);

        Func<Task> act = async () =>  await sut.Download("api/relative", true);

        await act.Should().ThrowAsync<HttpRequestException>();
        testLogger.AssertSingleErrorExists("Unable to connect to server. Please check if the server is running and if the address is correct. Url: 'https://www.sonarsource.com/api/relative'.");
    }

    [TestMethod]
    public async Task TryDownloadIfExists_HttpCodeOk_Succeed()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.OK);

        var result = await sut.TryDownloadIfExists(RelativeUrl);

        result.Item1.Should().BeTrue();
        result.Item2.Should().Be(TestContent);
        testLogger.AssertDebugLogged("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task TryDownloadIfExists_HttpCodeNotFound_Succeed()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.NotFound);

        var result = await sut.TryDownloadIfExists(RelativeUrl);

        result.Item1.Should().BeFalse();
        result.Item2.Should().BeNull();
        testLogger.AssertDebugLogged("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task TryDownloadIfExists_HttpCodeForbidden_ShouldThrow()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.Forbidden);

        Func<Task> act = async () => await sut.TryDownloadIfExists(RelativeUrl);

        await act.Should().ThrowExactlyAsync<HttpRequestException>();
        testLogger.AssertDebugLogged("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task TryDownloadIfExists_HttpCodeForbiddenWithWarnLog_ShouldThrow()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.Forbidden);

        Func<Task> act = async () => await sut.TryDownloadIfExists(RelativeUrl, true);

        await act.Should().ThrowExactlyAsync<HttpRequestException>();
        testLogger.AssertDebugLogged("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.AssertWarningLogged("To analyze private projects make sure the scanner user has 'Browse' permission.");
    }

    [TestMethod]
    [DataRow(HttpStatusCode.BadRequest)]
    [DataRow(HttpStatusCode.Unauthorized)]
    [DataRow(HttpStatusCode.InternalServerError)]
    public async Task TryDownloadIfExists_UnsuccessfulHttpCode_ShouldThrow(HttpStatusCode code)
    {
        var sut = CreateSut(testLogger, code);

        Func<Task> act = async () => await sut.TryDownloadIfExists(RelativeUrl);

        await act.Should().ThrowExactlyAsync<HttpRequestException>();
        testLogger.AssertDebugLogged("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task TryDownloadFileIfExists_HttpCodeOk_Succeed()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.OK);
        using var file = new TempFile();

        var result = await sut.TryDownloadFileIfExists(RelativeUrl, file.FileName);

        result.Should().BeTrue();
        testLogger.AssertDebugLogged($"Downloading file to {file.FileName}...");
        testLogger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task TryDownloadFileIfExists_HttpCodeNotFound_Succeed()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.NotFound);
        using var file = new TempFile();

        var result = await sut.TryDownloadFileIfExists(RelativeUrl, file.FileName);

        result.Should().BeFalse();
        testLogger.AssertDebugLogged($"Downloading file to {file.FileName}...");
        testLogger.AssertDebugLogged("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task TryDownloadFileIfExists_HttpCodeForbidden_ShouldThrow()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.Forbidden);
        var fileName = Path.GetRandomFileName();

        Func<Task> act = async () => await sut.TryDownloadFileIfExists(RelativeUrl, fileName);

        await act.Should().ThrowExactlyAsync<HttpRequestException>();
        testLogger.AssertDebugLogged($"Downloading file to {fileName}...");
        testLogger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task TryDownloadFileIfExists_HttpCodeForbiddenWithWarnLog_ShouldThrowAndLogsWarning()
    {
        var sut = CreateSut(testLogger, HttpStatusCode.Forbidden);
        var fileName = Path.GetRandomFileName();

        Func<Task> act = async () => await sut.TryDownloadFileIfExists(RelativeUrl, fileName, true);

        await act.Should().ThrowExactlyAsync<HttpRequestException>();
        testLogger.AssertDebugLogged($"Downloading file to {fileName}...");
        testLogger.AssertWarningLogged("To analyze private projects make sure the scanner user has 'Browse' permission.");
    }

    [TestMethod]
    [DataRow(HttpStatusCode.BadRequest)]
    [DataRow(HttpStatusCode.Unauthorized)]
    [DataRow(HttpStatusCode.InternalServerError)]
    public async Task TryDownloadFileIfExists_UnsuccessfulHttpCode_ShouldThrow(HttpStatusCode code)
    {
        var sut = CreateSut(testLogger, code);
        var fileName = Path.GetRandomFileName();

        Func<Task> act = async () => await sut.TryDownloadFileIfExists(RelativeUrl, fileName);

        await act.Should().ThrowExactlyAsync<HttpRequestException>();
        testLogger.AssertDebugLogged($"Downloading file to {fileName}...");
        testLogger.AssertNoWarningsLogged();
    }

    private static HttpClient MockHttpClient(HttpResponseMessage responseMessage) =>
        new(new HttpMessageHandlerMock((request, cancel) => Task.FromResult(responseMessage)));

    private static WebClientDownloader CreateSut(ILogger logger, HttpStatusCode statusCode) =>
        new(MockHttpClient(new HttpResponseMessage { StatusCode = statusCode, Content = new StringContent(TestContent), RequestMessage = new() { RequestUri = new("https://www.sonarsource.com/api/relative") }}), BaseUrl, logger);
}
