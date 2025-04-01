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

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace SonarScanner.MSBuild.PreProcessor.Test.Certificates;

[TestClass]
[DoNotParallelize]
public partial class CertificateBuilderTests
{
    [TestMethod]
    public async Task MockServerReturnsSelfSignedCertificate()
    {
        using var selfSigned = CertificateBuilder.CreateWebServerCertificate();
        using var server = ServerBuilder.StartServer(selfSigned);
        server.Given(Request.Create().WithPath("/").UsingGet()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));
        using var handler = new HttpClientHandler();
        using var client = new HttpClient(handler);
        bool serverCertificateValidation = false;
        handler.ServerCertificateCustomValidationCallback = (_, cert, _, _) =>
        {
            cert.Should().BeEquivalentTo(selfSigned);
            serverCertificateValidation = true;
            return true;
        };
        var result = await client.GetStringAsync(server.Url);
        result.Should().Be("Hello World");
        serverCertificateValidation.Should().BeTrue();
    }

    [TestMethod]
    public async Task MockServerReturnsRootCASignedCert()
    {
        using var rootCA = CertificateBuilder.CreateRootCA();
        using var webServerCert = CertificateBuilder.CreateWebServerCertificate(rootCA);
        var collection = CertificateBuilder.BuildCollection(webServerCert, [rootCA]);
        using var server = ServerBuilder.StartServer(collection);
        server.Given(Request.Create().WithPath("/").UsingGet()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));
        using var handler = new HttpClientHandler();
        using var client = new HttpClient(handler);
        bool serverCertificateValidation = false;
        handler.ServerCertificateCustomValidationCallback = (_, cert, chain, _) =>
        {
            cert.Should().BeEquivalentTo(webServerCert);
            chain.ChainElements.Count.Should().Be(1, because: "A web server only serves the certificate, intermediate CAs, but not the Root CA.");
            serverCertificateValidation = true;
            return true;
        };
        var result = await client.GetStringAsync(server.Url);
        result.Should().Be("Hello World");
        serverCertificateValidation.Should().BeTrue();
    }

    [TestMethod]
    public async Task MockServerReturnsIntermediateCASignedCert()
    {
        using var rootCA = CertificateBuilder.CreateRootCA();
        using var intermediate = CertificateBuilder.CreateIntermediateCA(rootCA);
        using var webServerCert = CertificateBuilder.CreateWebServerCertificate(intermediate);
        var collection = CertificateBuilder.BuildCollection(webServerCert, [intermediate, rootCA]);
        using var server = ServerBuilder.StartServer(collection);
        server.Given(Request.Create().WithPath("/").UsingGet()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));
        using var handler = new HttpClientHandler();
        using var client = new HttpClient(handler);
        bool serverCertificateValidation = false;
        handler.ServerCertificateCustomValidationCallback = (_, cert, chain, _) =>
        {
            cert.Should().BeEquivalentTo(webServerCert);
            chain.ChainElements.Count.Should().Be(2, because: "A web server serves the certificate and the intermediate CAs. Can also be confirmed via 'openssl.exe s_client -connect localhost:8443'");
            serverCertificateValidation = true;
            return true;
        };
        var result = await client.GetStringAsync(server.Url);
        result.Should().Be("Hello World");
        serverCertificateValidation.Should().BeTrue();
    }

    [DataTestMethod]
    [DataRow("localhost", false)]
    [DataRow(null, true)]
    [DataRow("error.org", true)]
    public async Task MockServerReturnsSelfSignedCertificateWithAlternativeDNS(string additionalHostName, bool nameMissmatch)
    {
        SubjectAlternativeNameBuilder alternatives = null;
        if (additionalHostName is not null)
        {
            alternatives = new SubjectAlternativeNameBuilder();
            alternatives.AddDnsName(additionalHostName);
        }
        using var selfSigned = CertificateBuilder.CreateWebServerCertificate(serverName: "dummy.org", subjectAlternativeNames: alternatives);
        using var server = ServerBuilder.StartServer(selfSigned);
        server.Given(Request.Create().WithPath("/").UsingGet()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));
        using var handler = new HttpClientHandler();
        using var client = new HttpClient(handler);
        bool serverCertificateValidation = false;
        handler.ServerCertificateCustomValidationCallback = (_, cert, _, errors) =>
        {
            cert.Should().BeEquivalentTo(selfSigned);
            errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors).Should().BeTrue();
            errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch).Should().Be(nameMissmatch);
            serverCertificateValidation = true;
            return true;
        };
        var result = await client.GetStringAsync(server.Url);
        result.Should().Be("Hello World");
        serverCertificateValidation.Should().BeTrue();
    }
}
