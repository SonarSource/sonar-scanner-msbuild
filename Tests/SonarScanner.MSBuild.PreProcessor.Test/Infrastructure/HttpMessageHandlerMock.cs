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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;

public class HttpMessageHandlerMock(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync, string requiredToken = null) : HttpMessageHandler
{
    private static readonly Task<HttpResponseMessage> DefaultResponse = Task.FromResult(
        new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("test content"),
            RequestMessage = new()
            {
                RequestUri = new("https://www.sonarsource.com/"),
            }
        });

    public List<HttpRequestMessage> Requests { get; private set; } = [];

    public HttpMessageHandlerMock() : this((_, _) => DefaultResponse)
    {
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return requiredToken is null || request.Headers.Any(x => x.Key == "Authorization" && x.Value.Contains($"Bearer {requiredToken}"))
            ? sendAsync(request, cancellationToken)
            : throw new Exception("Request verification failed.");
    }
}
