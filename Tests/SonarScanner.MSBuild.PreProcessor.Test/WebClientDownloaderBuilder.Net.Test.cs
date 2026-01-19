/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

#if NET

using System.Security.Cryptography.X509Certificates;
using SonarScanner.MSBuild.PreProcessor.Test.Certificates;
using TestUtilities.Certificates;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace SonarScanner.MSBuild.PreProcessor.Test;

public partial class WebClientDownloaderBuilderTest
{
    [TestMethod]
    public async Task CrlIsNotQueriedByValidation()
    {
        // Arrange
        WireMockServer crlServer = null;
        using var caCert = CertificateBuilder.CreateRootCA();
        using var caCertFileName = new TempFile("pfx", x => File.WriteAllBytes(x, caCert.WithoutPrivateKey().Export(X509ContentType.Pfx)));
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(caCert, configureCertificateRequest: x =>
        {
            (var crlExtension, crlServer, _) = CertificateBuilder.CreateCrlExtension(caCert);
            x.CertificateExtensions.Add(crlExtension);
        });
        using var crlServerDispose = crlServer;
        using var server = ServerBuilder.StartServer(serverCert);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(caCertFileName.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var response = await client.Download(new(server.Url));

        // Assert
        response.Should().Be("Hello World");
        crlServerDispose.LogEntries.Should().BeEmpty(because: "X509ChainPolicy.CustomTrustStore is needed to have crl support, but is only available in .Net5+");
    }

    [TestMethod]
    public async Task CrlRevokedCertificateIsNotDetectedByValidation()
    {
        // Arrange
        (WireMockServer crlServer, CertificateRevocationListBuilder revocationListBuilder) = (null, null);
        using var caCert = CertificateBuilder.CreateRootCA();
        using var caCertFileName = new TempFile("pfx", x => File.WriteAllBytes(x, caCert.WithoutPrivateKey().Export(X509ContentType.Pfx)));
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(caCert, configureCertificateRequest: x =>
        {
            (var crlExtension, crlServer, revocationListBuilder) = CertificateBuilder.CreateCrlExtension(caCert);
            x.CertificateExtensions.Add(crlExtension);
        });
        using var crlServerDispose = crlServer;
        revocationListBuilder.AddEntry(serverCert); // Revoke the server certificate
        using var server = ServerBuilder.StartServer(serverCert);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(caCertFileName.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var response = await client.Download(new(server.Url)); // ChainPolicy.RevocationMode can not be forced to query CRLs for certificates in X509ChainPolicy.ExtraStore.
                                                               // X509ChainPolicy.CustomTrustStore (.Net5+) is needed to support CRLs.

        // Assert
        response.Should().Be("Hello World", because: ".Net5+ support is required");
        crlServerDispose.LogEntries.Should().BeEmpty(because: "X509ChainPolicy.CustomTrustStore is needed to have crl support, but is only available in .Net5+");
    }
}

#endif
