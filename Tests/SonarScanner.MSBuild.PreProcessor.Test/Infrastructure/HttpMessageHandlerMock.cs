using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;

internal class HttpMessageHandlerMock : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync;
    public HashSet<HttpRequestMessage> Request { get; private set; } = [];

    public HttpMessageHandlerMock(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
    {
        this.sendAsync = sendAsync;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request.Add(request);
        return sendAsync(request, cancellationToken);
    }
}
