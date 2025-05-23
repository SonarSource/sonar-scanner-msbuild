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

using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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
        try
        {
            trustStore.Import(serverCertPath, serverCertPassword, X509KeyStorageFlags.DefaultKeySet);
        }
        // On MacOS, if the certificate is not found on disk, the import will fail with FileNotFoundException
        catch (Exception ex) when (ex is CryptographicException or FileNotFoundException)
        {
            logger.LogError(Resources.ERROR_CertificateImportFailed, SonarProperties.TruststorePath, serverCertPath, ex.Message.TrimEnd());
            throw;
        }
        handler.ServerCertificateCustomValidationCallback = (_, certificate, chain, errors) =>
            ServerCertificateCustomValidationCallback(this.trustStore, this.logger, serverCertPath, certificate, chain, errors);

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
                                                                  ILogger logger,
                                                                  string trustStoreFile,
                                                                  X509Certificate2 certificate,
                                                                  X509Chain chain,
                                                                  SslPolicyErrors errors)
    {
        if (errors is SslPolicyErrors.None)
        {
            logger.LogDebug(Resources.MSG_CertificateTrustedBySystem);
            return true;
        }
        else if (errors is SslPolicyErrors.RemoteCertificateChainErrors) // Don't do HasFlags. Any other errors than RemoteCertificateChainErrors should fail the handshake.
        {
            logger.LogDebug(Resources.MSG_TrustStore_CertificateChainErrors, trustStoreFile, SonarProperties.TruststorePath);
            if (chain.ChainStatus.All(x => x.Status is X509ChainStatusFlags.UntrustedRoot)) // Self-signed certificate cause this error
            {
                return ServerCertificateValidationSelfSigned(trustStore, logger, trustStoreFile, certificate);
            }
            else if (chain.ChainStatus.All(x => x.Status is X509ChainStatusFlags.PartialChain))
            {
                return ServerCertificateValidationChain(trustStore, logger, trustStoreFile, certificate);
            }
            else
            {
                logger.LogWarning(Resources.WARN_TrustStore_OtherChainStatus, SonarProperties.TruststorePath, ChainStatusAsBulletList(chain));
                return false;
            }
        }
        else
        {
            logger.LogWarning(Resources.WARN_TrustStore_PolicyErrors, errors);
            return false;
        }
    }

    private static string ChainStatusAsBulletList(X509Chain chain) =>
        chain.ChainStatus.Aggregate(new StringBuilder(), (sb, x) => sb.Append($"""

            * {x.StatusInformation.TrimEnd()}
            """), x => x.ToString());

    private static bool ServerCertificateValidationChain(X509Certificate2Collection trustStore, ILogger logger, string trustStoreFile, X509Certificate2 certificate)
    {
        // Build a chain of certificates including the CAs and Intermediate CAs found in the trust store
        using var testChain = new X509Chain();
        testChain.ChainPolicy.ExtraStore.AddRange(trustStore);

        // The ExtraStore is only consulted when building the chain and the CAs in ExtraStore are not consider trustworthy CAs.
        // .Net 5 adds testChain.ChainPolicy.CustomTrustStore which provides full support for chain validation with custom trustworthy CA roots.
        testChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        testChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // CRL and OCSP are not checked. Net5 is required for CRL support.
        var valid = testChain.Build(certificate);
        if (valid && testChain.ChainStatus.All(x => x.Status is X509ChainStatusFlags.UntrustedRoot))
        {
            // The testchain should now contain a certificate from the trust store as it's root
            var rootInChain = testChain.ChainElements.Cast<X509ChainElement>().Last();
            var foundInTrustStore = trustStore.Find(X509FindType.FindBySerialNumber, rootInChain.Certificate.SerialNumber, validOnly: false);
            // Check if the certificates found by serial number in the trust store really contain the root certificate of the chain by doing a proper equality check
            if (IsCertificateInTrustStore(rootInChain.Certificate, foundInTrustStore))
            {
                return true;
            }
            else
            {
                // Untestable code path. The combination of ChainStatus.All(PartialChain) in ServerCertificateCustomValidationCallback
                // and ChainStatus.All(UntrustedRoot) here require the root certificate being in the trust store file.
                // If we ever end up here, something very unique happened. The logging was tested once manually so we know the logging
                // does not throw.
                logger.LogWarning(Resources.WARN_TrustStore_Chain_RootCertificateNotFound,
                                  rootInChain.Certificate.Issuer,
                                  rootInChain.Certificate.Thumbprint,
                                  trustStoreFile,
                                  SonarProperties.TruststorePath);
                return false;
            }
        }
        else
        {
            logger.LogWarning(Resources.WARN_TrustStore_Chain_Invalid, ChainStatusAsBulletList(testChain), trustStoreFile, SonarProperties.TruststorePath);
            return false;
        }
    }

    private static bool ServerCertificateValidationSelfSigned(X509Certificate2Collection trustStore, ILogger logger, string trustStoreFile, X509Certificate2 certificate)
    {
        if (trustStore.Find(X509FindType.FindBySerialNumber, certificate.SerialNumber, validOnly: false) is { Count: > 0 } certificatesInTrustStore
            && IsCertificateInTrustStore(certificate, certificatesInTrustStore))
        {
            return true;
        }
        else
        {
            logger.LogWarning(Resources.WARN_TrustStore_SelfSignedCertificateNotFound, certificate.Issuer, certificate.Thumbprint, trustStoreFile, SonarProperties.TruststorePath);
            return false;
        }
    }

    private static bool IsCertificateInTrustStore(X509Certificate2 certificate, X509Certificate2Collection trustStore)
    {
        // see also the Remark section in https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509certificate.equals
        var thumbprint = certificate.Thumbprint;
        return trustStore.Cast<X509Certificate2>().Any(x => x.Thumbprint == thumbprint); // The certificates must match with all properties. Do not use certificate.Equals.
    }

    private static bool IsAscii(string value) =>
        string.IsNullOrWhiteSpace(value) || !value.Any(x => x > sbyte.MaxValue);
}
