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

#if NET

using System.Security.Cryptography.X509Certificates;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace SonarScanner.MSBuild.PreProcessor.Test.Certificates;

public partial class CertificateBuilderTests
{
    [TestMethod]
    public async Task CrlListIsRequestedWithCustomTrustStore()
    {
        WireMockServer crlServer = null;
        using var rootCert = CertificateBuilder.CreateRootCA();
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(rootCert, configureCertificateRequest: x =>
        {
            (var crlExtension, crlServer, _) = CertificateBuilder.CreateCrlExtension(rootCert);
            x.CertificateExtensions.Add(crlExtension);
        });
        using var crlServerDispose = crlServer;
        using var server = ServerBuilder.StartServer(serverCert);
        server.Given(Request.Create().WithPath("/").UsingGet()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, cert, _, _) =>
        {
            using var testChain = new X509Chain();
            testChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            testChain.ChainPolicy.CustomTrustStore.Add(rootCert);
            var valid = testChain.Build(cert);
            valid.Should().BeTrue();
            testChain.ChainStatus.Should().BeEmpty();
            return valid;
        };
        using var client = new HttpClient(handler);
        var result = await client.GetStringAsync(server.Url);
        result.Should().Be("Hello World");
        crlServer.LogEntries.Should().Contain(x => x.RequestMessage.Path == $"/Revoked.crl");
    }

    [TestMethod]
    public async Task CrlListIsRequestedAndRevokedCertificateIsDetected()
    {
        (WireMockServer crlServer, CertificateRevocationListBuilder crlBuilder) = (null, null);
        using var rootCert = CertificateBuilder.CreateRootCA();
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(rootCert, configureCertificateRequest: x =>
        {
            (var crlExtension, crlServer, crlBuilder) = CertificateBuilder.CreateCrlExtension(rootCert);
            x.CertificateExtensions.Add(crlExtension);
        });
        crlBuilder.AddEntry(serverCert); // Revoke the server certificate
        using var crlServerDispose = crlServer;

        using var server = ServerBuilder.StartServer(serverCert);
        server.Given(Request.Create().WithPath("/").UsingGet()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, cert, _, _) =>
        {
            using var testChain = new X509Chain();
            testChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            testChain.ChainPolicy.CustomTrustStore.Add(rootCert);
            var valid = testChain.Build(cert);
            valid.Should().BeFalse();
            testChain.ChainStatus.Should().BeEquivalentTo([new X509ChainStatus() { Status = X509ChainStatusFlags.Revoked, StatusInformation = "The certificate is revoked." }]);
            return valid;
        };
        using var client = new HttpClient(handler);
        var download = async () => await client.GetStringAsync(server.Url);
        await download.Should().ThrowAsync<HttpRequestException>();
        crlServer.LogEntries.Should().Contain(x => x.RequestMessage.Path == $"/Revoked.crl");
    }
}

#endif
