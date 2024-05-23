using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;

public sealed class HttpMessageHandlerMock : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync;
    private readonly Func<HttpRequestMessage, CancellationToken, bool> verifyRequest;

    public List<HttpRequestMessage> Request { get; private set; } = [];

    public HttpMessageHandlerMock(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync, Func<HttpRequestMessage, CancellationToken, bool> verifyRequest = null)
    {
        this.sendAsync = sendAsync;
        this.verifyRequest = verifyRequest;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request.Add(request);
        return verifyRequest is not null && !verifyRequest(request, cancellationToken)
            ? throw new Exception("Request verification failed.")
            : sendAsync(request, cancellationToken);
    }
}
