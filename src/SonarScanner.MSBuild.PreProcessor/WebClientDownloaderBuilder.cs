﻿/*
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
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor;

public sealed class WebClientDownloaderBuilder : IDisposable
{
    private readonly string baseAddress;
    private readonly TimeSpan httpTimeout;
    private readonly ILogger logger;
    private AuthenticationHeaderValue authenticationHeader;
    private HttpClientHandler handler;
    private X509Certificate2Collection trustStore; // Trusted server certificates

    public WebClientDownloaderBuilder(string baseAddress, TimeSpan httpTimeout, ILogger logger)
    {
        this.baseAddress = baseAddress;
        this.httpTimeout = httpTimeout;
        this.logger = logger;
    }

    public void Dispose() =>
        handler.Dispose();

    public WebClientDownloaderBuilder AddAuthorization(string userName, string password)
    {
        if (userName == null)
        {
            return this;
        }

        if (userName.Contains(':'))
        {
            throw new ArgumentException(Resources.WCD_UserNameCannotContainColon);
        }
        if (!IsAscii(userName) || !IsAscii(password))
        {
            throw new ArgumentException(Resources.WCD_UserNameMustBeAscii);
        }

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}"));
        authenticationHeader = new AuthenticationHeaderValue("Basic", credentials);

        return this;
    }

    public WebClientDownloaderBuilder AddCertificate(string clientCertPath, string clientCertPassword)
    {
        if (clientCertPath is null || clientCertPassword is null)
        {
            return this;
        }

        handler ??= new HttpClientHandler();
        handler.ClientCertificateOptions = ClientCertificateOption.Manual;
        handler.ClientCertificates.Add(new X509Certificate2(clientCertPath, clientCertPassword));

        return this;
    }

    public WebClientDownloaderBuilder AddServerCertificate(string serverCertPath, string serverCertPassword)
    {
        if (serverCertPath is null || serverCertPassword is null)
        {
            return this;
        }

        handler ??= new();
        trustStore ??= new();
        trustStore.Import(serverCertPath, serverCertPassword, X509KeyStorageFlags.DefaultKeySet);
        handler.ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) =>
            ServerCertificateCustomValidationCallback(this.trustStore, message, certificate, chain, errors);

        return this;
    }

    public WebClientDownloader Build()
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = httpTimeout;
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SonarScanner-for-.NET", Utilities.ScannerVersion));
        if (authenticationHeader is not null)
        {
            client.DefaultRequestHeaders.Authorization = authenticationHeader;
        }

        return new WebClientDownloader(client, baseAddress, logger);
    }

    private static bool ServerCertificateCustomValidationCallback(X509Certificate2Collection trustStore,
        HttpRequestMessage message, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors errors)
    {
        if (errors is SslPolicyErrors.None)
        {
            return true;
        }
        if (errors is SslPolicyErrors.RemoteCertificateChainErrors // Don't do HasFlags. Any other errors than RemoteCertificateChainErrors should fail the handshake.
            && chain.ChainStatus.All(x => x.Status is X509ChainStatusFlags.UntrustedRoot)) // Any other violations than UntrustedRoot should fail the handshake.
        {
            if (trustStore.Find(X509FindType.FindBySerialNumber, certificate.SerialNumber, validOnly: false) is { Count: > 0 } certificatesInTrustStore)
            {
                return certificatesInTrustStore.Cast<X509Certificate2>().Any(x => x.RawData.SequenceEqual(certificate.RawData)); // public keys must match
            }
        }
        return false;
    }

    private static bool IsAscii(string value) =>
        string.IsNullOrWhiteSpace(value) || !value.Any(x => x > sbyte.MaxValue);
}
