using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;

public sealed class HttpMessageHandlerMock : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync;
    private readonly string requiredToken;

    public List<HttpRequestMessage> Requests { get; private set; } = [];

    public HttpMessageHandlerMock(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync, string requiredToken = null)
    {
        this.sendAsync = sendAsync;
        this.requiredToken = requiredToken;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return requiredToken is not null && !request.Headers.Any(x => x.Key == "Authorization" && x.Value.Contains($"Bearer {requiredToken}"))
            ? throw new Exception("Request verification failed.")
            : sendAsync(request, cancellationToken);
    }
}
