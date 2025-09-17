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

using System.Net;
using System.Net.Http;

namespace SonarScanner.MSBuild.PreProcessor;

public sealed class WebClientDownloader : IDownloader
{
    private readonly ILogger logger;
    private readonly HttpClient client;

    public string BaseUrl => client.BaseAddress.ToString();

    public WebClientDownloader(HttpClient client, string baseUri, ILogger logger)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        Contract.ThrowIfNullOrWhitespace(baseUri, nameof(baseUri));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        client.BaseAddress = WebUtils.CreateUri(baseUri);
    }


    public async Task<HttpResponseMessage> DownloadResource(Uri url) =>
        await AsyncGet(url);

    public async Task<Tuple<bool, string>> TryDownloadIfExists(Uri url, bool logPermissionDenied = false)
    {
        var response = await AsyncGet(url);

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return new Tuple<bool, string>(false, null);
            case HttpStatusCode.Forbidden:
                {
                    if (logPermissionDenied)
                    {
                        logger.LogWarning(Resources.MSG_Forbidden_BrowsePermission);
                    }

                    response.EnsureSuccessStatusCode();
                    break;
                }
            default:
                response.EnsureSuccessStatusCode();
                break;
        }

        return new Tuple<bool, string>(true, await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> TryDownloadFileIfExists(Uri url, string targetFilePath, bool logPermissionDenied = false)
    {
        logger.LogDebug(Resources.MSG_DownloadingFile, targetFilePath);
        var response = await AsyncGet(url);

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return false;
            case HttpStatusCode.Forbidden:
                if (logPermissionDenied)
                {
                    logger.LogWarning(Resources.MSG_Forbidden_BrowsePermission);
                }
                response.EnsureSuccessStatusCode();
                break;
            default:
                response.EnsureSuccessStatusCode();
                break;
        }

        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write);
        await contentStream.CopyToAsync(fileStream);

        return true;
    }

    public async Task<string> Download(Uri url, bool logPermissionDenied = false, LoggerVerbosity failureVerbosity = LoggerVerbosity.Info)
    {
        Contract.ThrowIfNullOrWhitespace(url.OriginalString, nameof(url));

        if (url.OriginalString.StartsWith("/"))
        {
            throw new NotSupportedException("The BaseAddress always ends in '/'. Please call this method with a url that does not start with '/'.");
        }

        var response = await AsyncGet(url);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync();
        }
        else
        {
            logger.Log(failureVerbosity, Resources.MSG_DownloadFailed, response.RequestMessage.RequestUri, response.StatusCode);
        }

        if (logPermissionDenied && response.StatusCode == HttpStatusCode.Forbidden)
        {
            logger.LogWarning(Resources.MSG_Forbidden_BrowsePermission);
            response.EnsureSuccessStatusCode();
        }

        return null;
    }

    public async Task<Stream> DownloadStream(Uri url, Dictionary<string, string> headers = null)
    {
        var response = await AsyncGet(url, headers);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStreamAsync();
        }
        else
        {
            logger.LogInfo(Resources.MSG_DownloadFailed, response.RequestMessage.RequestUri, response.StatusCode);
            return null;
        }
    }

    public void Dispose() =>
        client.Dispose();

    private async Task<HttpResponseMessage> AsyncGet(Uri url, Dictionary<string, string> headers = null)
    {
        try
        {
            logger.LogDebug(Resources.MSG_Downloading, $"{client.BaseAddress}{url}");

            var message = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyHeaders(message, headers);
            var response = await client.SendAsync(message);
            logger.LogDebug(Resources.MSG_ResponseReceived, response.RequestMessage.RequestUri);
            return response;
        }
        catch (Exception e)
        {
            logger.LogError(Resources.ERR_UnableToConnectToServer, $"{client.BaseAddress}{url}");
            logger.LogDebug((e.InnerException ?? e).ToString());
            throw;
        }
    }

    private static void ApplyHeaders(HttpRequestMessage message, Dictionary<string, string> headers)
    {
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                message.Headers.Add(header.Key, header.Value);
            }
        }
    }
}
