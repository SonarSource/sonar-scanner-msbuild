/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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
using System.Linq;
using System.Net;
using System.Net.Http;
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
    private WebClientDownloader sut;

    [TestInitialize]
    public void Init()
    {
        testLogger = new TestLogger();
        sut = CreateSut();
    }

    [TestMethod]
    public void Ctor_NullArguments()
    {
        FluentActions.Invoking(() => new WebClientDownloader(null, null, null)).Should()
            .Throw<ArgumentNullException>().And.ParamName.Should().Be("client");

        FluentActions.Invoking(() => new WebClientDownloader(new HttpClient(), null, null)).Should()
            .Throw<ArgumentNullException>().And.ParamName.Should().Be("baseUri");

        FluentActions.Invoking(() => new WebClientDownloader(new HttpClient(), string.Empty, null)).Should()
            .Throw<ArgumentNullException>().And.ParamName.Should().Be("baseUri");

        FluentActions.Invoking(() => new WebClientDownloader(new HttpClient(), BaseUrl, null)).Should()
            .Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void GetBaseUrl_ReturnsTheBaseUrl() =>
        sut.GetBaseUrl().Should().Be(BaseUrl);

    [TestMethod]
    [DataRow("https://sonarsource.com/", "https://sonarsource.com/")]
    [DataRow("https://sonarsource.com", "https://sonarsource.com/")]
    [DataRow("https://sonarsource.com/sonarlint", "https://sonarsource.com/sonarlint/")]
    [DataRow("https://sonarsource.com/sonarlint/", "https://sonarsource.com/sonarlint/")]
    public void Ctor_ValidBaseUrl_ShouldAlwaysEndsWithSlash(string baseUrl, string expectedUrl)
    {
        var http = Substitute.For<HttpClient>();
        CreateSut(http, baseUrl);

        http.BaseAddress.ToString().Should().Be(expectedUrl);
    }

    [TestMethod]
    public void Implements_Dispose()
    {
        var handler = new HttpMessageHandlerMock();
        sut = new WebClientDownloader(new HttpClient(handler), BaseUrl, testLogger);

        sut.Dispose();

        handler.IsDisposed.Should().BeTrue();
    }

    [TestMethod]
    public async Task DownloadStream_Success()
    {
        using var stream = await sut.DownloadStream(RelativeUrl);
        using var reader = new StreamReader(stream);

        var text = await reader.ReadToEndAsync();
        text.Should().Be(TestContent);
        testLogger.Should().HaveDebugs("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveDebugs("Response received from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveNoErrors();
    }

    [TestMethod]
    public async Task DownloadStream_ContainsHeaders_Success()
    {
        var handler = new HttpMessageHandlerMock();
        sut = CreateSut(handler);

        using var stream = await sut.DownloadStream(RelativeUrl, new() { { "One", "Two" } });

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Headers.Should().ContainSingle();
        handler.Requests[0].Headers.First().Key.Should().Be("One");
        handler.Requests[0].Headers.First().Value.Should().ContainSingle("Two");
    }

    [TestMethod]
    public async Task DownloadStream_Fail()
    {
        sut = CreateSut(HttpStatusCode.NotFound);

        using var stream = await sut.DownloadStream(RelativeUrl);

        stream.Should().BeNull();
        testLogger.Should().HaveDebugs("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveInfos("Downloading from https://www.sonarsource.com/api/relative failed. Http status code is NotFound.");
        testLogger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public async Task DownloadResource_HttpCodeOk_ReturnsTheResponse()
    {
        var expected = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(TestContent),
            RequestMessage = new()
            {
                RequestUri = new("https://www.sonarsource.com/"),
            }
        };
        sut = CreateSut(new HttpMessageHandlerMock((_, _) => Task.FromResult(expected)));

        var responseMessage = await sut.DownloadResource(RelativeUrl);

        responseMessage.Should().Be(expected);
        testLogger.Should().HaveDebugs("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public async Task Download_Success()
    {
        var text = await sut.Download(RelativeUrl);

        text.Should().Be(TestContent);
        testLogger.Should().HaveDebugs("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveNoWarnings();
        testLogger.Should().HaveNoErrors();
    }

    [TestMethod]
    public async Task Download_Fail_NoLog()
    {
        sut = CreateSut(HttpStatusCode.Forbidden);

        var text = await sut.Download(RelativeUrl);

        text.Should().BeNull();
        testLogger.Should().HaveDebugs("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveInfos("Downloading from https://www.sonarsource.com/api/relative failed. Http status code is Forbidden.");
        testLogger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public async Task Download_Fail_ForbiddenAndLogsWarning()
    {
        sut = CreateSut(HttpStatusCode.Forbidden);

        await sut.Invoking(async x => await x.Download(RelativeUrl, true)).Should().ThrowAsync<HttpRequestException>();

        testLogger.Should().HaveDebugs("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveWarnings("To analyze private projects make sure the scanner user has 'Browse' permission.");
    }

    [TestMethod]
    public async Task Download_Fail_NotForbidden()
    {
        sut = CreateSut(HttpStatusCode.NotFound);

        var text = await sut.Download(RelativeUrl, true);

        text.Should().BeNull();
        testLogger.Should().HaveDebugs("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveNoWarnings();
    }

    [TestMethod]
    [DataRow("https://sonarsource.com/", "https://sonarsource.com/api/relative")]
    [DataRow("https://sonarsource.com", "https://sonarsource.com/api/relative")]
    [DataRow("https://sonarsource.com/sonarlint", "https://sonarsource.com/sonarlint/api/relative")]
    [DataRow("https://sonarsource.com/sonarlint/", "https://sonarsource.com/sonarlint/api/relative")]
    public async Task Download_CorrectAbsoluteUrl_ShouldSucceed(string baseUrl, string expectedAbsoluteUrl)
    {
        // We want to make sure the request uri is the expected absolute url
        var handlerMock = new HttpMessageHandlerMock((request, _) =>
            request.RequestUri == new Uri(expectedAbsoluteUrl)
                ? Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(TestContent),
                    RequestMessage = new HttpRequestMessage { RequestUri = new Uri(expectedAbsoluteUrl) }
                })
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        sut = CreateSut(handlerMock, baseUrl);

        _ = await sut.Download("api/relative", true);

        handlerMock.Requests.Should().ContainSingle(x => x.RequestUri == new Uri(expectedAbsoluteUrl));
        testLogger.Should().HaveDebugs(string.Format(Resources.MSG_Downloading, expectedAbsoluteUrl));
    }

    [TestMethod]
    [DataRow("https://sonarsource.com/")]
    [DataRow("https://sonarsource.com")]
    public async Task Download_RelativeUrl_SlashPrefix_ShouldThrow(string baseUrl)
    {
        sut = CreateSut(baseUrl: baseUrl);

        await FluentActions.Invoking(async () => await sut.Download("/starts/with/slash"))
            .Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("The BaseAddress always ends in '/'. Please call this method with a url that does not start with '/'.");
    }

    [TestMethod]
    public async Task Download_HttpClientThrowAnyException_ShouldThrowAndLogError()
    {
        var handler = new HttpMessageHandlerMock((_, _) => Task.FromException<HttpResponseMessage>(new Exception("error")));
        sut = CreateSut(handler);

        Func<Task> act = async () => await sut.Download("api/relative", true);

        await act.Should().ThrowAsync<Exception>();
        testLogger.Should().HaveErrorOnce("Unable to connect to server. Please check if the server is running and if the address is correct. Url: 'https://www.sonarsource.com/api/relative'.");
    }

    [TestMethod]
    public async Task Download_HttpClientThrowConnectionFailure_ShouldThrowAndLogError()
    {
        var exception = new HttpRequestException(string.Empty, new WebException(string.Empty, WebExceptionStatus.ConnectFailure));
        var handler = new HttpMessageHandlerMock((_, _) => Task.FromException<HttpResponseMessage>(exception));
        sut = CreateSut(handler);

        Func<Task> act = async () => await sut.Download("api/relative", true);

        await act.Should().ThrowAsync<HttpRequestException>();
        testLogger.Should().HaveErrorOnce("Unable to connect to server. Please check if the server is running and if the address is correct. Url: 'https://www.sonarsource.com/api/relative'.");
    }

    [TestMethod]
    public async Task Download_FailureStatusCodeGetsLogged_Debug()
    {
        sut = CreateSut(HttpStatusCode.NotFound);

        await sut.Download(RelativeUrl, failureVerbosity: LoggerVerbosity.Debug);

        testLogger.DebugMessages.Should().BeEquivalentTo(
            "Downloading from https://www.sonarsource.com/api/relative...",
            "Response received from https://www.sonarsource.com/api/relative...",
            "Downloading from https://www.sonarsource.com/api/relative failed. Http status code is NotFound.");
        testLogger.InfoMessages.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Download_FailureStatusCodeGetsLogged_Info()
    {
        sut = CreateSut(HttpStatusCode.NotFound);

        await sut.Download(RelativeUrl, failureVerbosity: LoggerVerbosity.Info);

        testLogger.DebugMessages.Should().BeEquivalentTo(
            "Downloading from https://www.sonarsource.com/api/relative...",
            "Response received from https://www.sonarsource.com/api/relative...");
        testLogger.InfoMessages.Should().ContainSingle("Downloading from https://www.sonarsource.com/api/relative failed. Http status code is NotFound.");
    }

    [TestMethod]
    public async Task TryDownloadIfExists_HttpCodeOk_Succeed()
    {
        var result = await sut.TryDownloadIfExists(RelativeUrl);

        result.Item1.Should().BeTrue();
        result.Item2.Should().Be(TestContent);
        testLogger.Should().HaveDebugs("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public async Task TryDownloadIfExists_HttpCodeNotFound_Succeed()
    {
        var sut = CreateSut(HttpStatusCode.NotFound);

        var result = await sut.TryDownloadIfExists(RelativeUrl);

        result.Item1.Should().BeFalse();
        result.Item2.Should().BeNull();
        testLogger.Should().HaveDebugs("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public async Task TryDownloadIfExists_HttpCodeForbidden_ShouldThrow()
    {
        var sut = CreateSut(HttpStatusCode.Forbidden);

        Func<Task> act = async () => await sut.TryDownloadIfExists(RelativeUrl);

        await act.Should().ThrowExactlyAsync<HttpRequestException>();
        testLogger.Should().HaveDebugs("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public async Task TryDownloadIfExists_HttpCodeForbiddenWithWarnLog_ShouldThrow()
    {
        sut = CreateSut(HttpStatusCode.Forbidden);

        Func<Task> act = async () => await sut.TryDownloadIfExists(RelativeUrl, true);

        await act.Should().ThrowExactlyAsync<HttpRequestException>();
        testLogger.Should().HaveDebugs("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveWarnings("To analyze private projects make sure the scanner user has 'Browse' permission.");
    }

    [TestMethod]
    [DataRow(HttpStatusCode.BadRequest)]
    [DataRow(HttpStatusCode.Unauthorized)]
    [DataRow(HttpStatusCode.InternalServerError)]
    public async Task TryDownloadIfExists_UnsuccessfulHttpCode_ShouldThrow(HttpStatusCode code)
    {
        sut = CreateSut(code);

        Func<Task> act = async () => await sut.TryDownloadIfExists(RelativeUrl);

        await act.Should().ThrowExactlyAsync<HttpRequestException>();
        testLogger.Should().HaveDebugs("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public async Task TryDownloadFileIfExists_HttpCodeOk_Succeed()
    {
        using var file = new TempFile();

        var result = await sut.TryDownloadFileIfExists(RelativeUrl, file.FileName);

        result.Should().BeTrue();
        testLogger.Should().HaveDebugs($"Downloading file to {file.FileName}...");
        testLogger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public async Task TryDownloadFileIfExists_HttpCodeNotFound_Succeed()
    {
        sut = CreateSut(HttpStatusCode.NotFound);
        using var file = new TempFile();

        var result = await sut.TryDownloadFileIfExists(RelativeUrl, file.FileName);

        result.Should().BeFalse();
        testLogger.Should().HaveDebugs($"Downloading file to {file.FileName}...");
        testLogger.Should().HaveDebugs("Downloading from https://www.sonarsource.com/api/relative...");
        testLogger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public async Task TryDownloadFileIfExists_HttpCodeForbidden_ShouldThrow()
    {
        sut = CreateSut(HttpStatusCode.Forbidden);
        var fileName = Path.GetRandomFileName();

        Func<Task> act = async () => await sut.TryDownloadFileIfExists(RelativeUrl, fileName);

        await act.Should().ThrowExactlyAsync<HttpRequestException>();
        testLogger.Should().HaveDebugs($"Downloading file to {fileName}...");
        testLogger.Should().HaveNoWarnings();
    }

    [TestMethod]
    public async Task TryDownloadFileIfExists_HttpCodeForbiddenWithWarnLog_ShouldThrowAndLogsWarning()
    {
        sut = CreateSut(HttpStatusCode.Forbidden);
        var fileName = Path.GetRandomFileName();

        Func<Task> act = async () => await sut.TryDownloadFileIfExists(RelativeUrl, fileName, true);

        await act.Should().ThrowExactlyAsync<HttpRequestException>();
        testLogger.Should().HaveDebugs($"Downloading file to {fileName}...");
        testLogger.Should().HaveWarnings("To analyze private projects make sure the scanner user has 'Browse' permission.");
    }

    [TestMethod]
    [DataRow(HttpStatusCode.BadRequest)]
    [DataRow(HttpStatusCode.Unauthorized)]
    [DataRow(HttpStatusCode.InternalServerError)]
    public async Task TryDownloadFileIfExists_UnsuccessfulHttpCode_ShouldThrow(HttpStatusCode code)
    {
        sut = CreateSut(code);
        var fileName = Path.GetRandomFileName();

        Func<Task> act = async () => await sut.TryDownloadFileIfExists(RelativeUrl, fileName);

        await act.Should().ThrowExactlyAsync<HttpRequestException>();
        testLogger.Should().HaveDebugs($"Downloading file to {fileName}...");
        testLogger.Should().HaveNoWarnings();
    }

    private WebClientDownloader CreateSut(HttpStatusCode statusCode = HttpStatusCode.OK, string baseUrl = null)
    {
        var message = new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(TestContent),
            RequestMessage = new()
            {
                RequestUri = new("https://www.sonarsource.com/api/relative")
            }
        };

        var handler = new HttpMessageHandlerMock((_, _) => Task.FromResult(message));
        return CreateSut(handler, baseUrl);
    }

    private WebClientDownloader CreateSut(HttpMessageHandlerMock handler, string baseUrl = null) =>
        CreateSut(new HttpClient(handler), baseUrl);

    private WebClientDownloader CreateSut(HttpClient handler, string baseUrl = null) =>
        new(handler, baseUrl ?? BaseUrl, testLogger);
}
