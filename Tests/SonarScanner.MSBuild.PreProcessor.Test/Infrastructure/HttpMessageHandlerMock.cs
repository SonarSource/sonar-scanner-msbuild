using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;

public class HttpMessageHandlerMock : HttpMessageHandler
{
    private static readonly HttpResponseMessage DefaultResponse = new()
    {
        StatusCode = HttpStatusCode.OK,
        Content = new StringContent("test content"),
        RequestMessage = new()
        {
            RequestUri = new("https://www.sonarsource.com/"),
        }
    };

    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync;
    private readonly string requiredToken;

    public List<HttpRequestMessage> Requests { get; private set; } = [];

    public HttpMessageHandlerMock() : this((_, _) => Task.FromResult(DefaultResponse), null)
    { }

    public HttpMessageHandlerMock(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync, string requiredToken = null)
    {
        this.sendAsync = sendAsync;
        this.requiredToken = requiredToken;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Send(request, cancellationToken);

    public virtual Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return requiredToken is null || request.Headers.Any(x => x.Key == "Authorization" && x.Value.Contains($"Bearer {requiredToken}"))
            ? sendAsync(request, cancellationToken)
            : throw new Exception("Request verification failed.");
    }
}
