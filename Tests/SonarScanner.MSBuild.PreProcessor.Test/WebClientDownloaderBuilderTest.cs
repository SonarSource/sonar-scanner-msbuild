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

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Test.Certificates;
using TestUtilities;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class WebClientDownloaderBuilderTest
{
    private const string CertificatePath = "certtestsonar.pem";
    private const string CertificatePassword = "dummypw";
    private const string BaseAddress = "https://sonarsource.com/";

    private readonly TimeSpan httpTimeout = TimeSpan.FromSeconds(42);
    private TestLogger logger;

    [TestInitialize]
    public void TestInitialize() =>
        logger = new TestLogger();

    [TestMethod]
    [DataRow(null, null, null)]
    [DataRow(null, "password", null)]
    [DataRow("da39a3ee5e6b4b0d3255bfef95601890afd80709", null, "Basic ZGEzOWEzZWU1ZTZiNGIwZDMyNTViZmVmOTU2MDE4OTBhZmQ4MDcwOTo=")]
    [DataRow("da39a3ee5e6b4b0d3255bfef95601890afd80709", "", "Basic ZGEzOWEzZWU1ZTZiNGIwZDMyNTViZmVmOTU2MDE4OTBhZmQ4MDcwOTo=")]
    [DataRow("admin", "password", "Basic YWRtaW46cGFzc3dvcmQ=")]
    public void Build_WithAuthorization_ShouldHaveAuthorizationHeader(string username, string password, string expected)
    {
        var sut = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger);

        var result = sut.AddAuthorization(username, password).Build();

        GetHeader(result, "Authorization").Should().Be(expected);
    }

    [TestMethod]
    public void Build_BasicBuild_UserAgentShouldBeSet()
    {
        var scannerVersion = typeof(WebClientDownloaderTest).Assembly.GetName().Version.ToDisplayString();
        var sut = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger);

        using var result = sut.Build();

        GetHeader(result, "User-Agent").Should().Be($"SonarScanner-for-.NET/{scannerVersion}");
    }

    [TestMethod]
    public void Build_FullBuild_ShouldSucceed()
    {
        var sut = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger);

        var result = sut.AddCertificate(CertificatePath, CertificatePassword).AddAuthorization("admin", "password").Build();

        result.Should().NotBeNull();
    }

    [TestMethod]
    public void AddAuthorization_UserNameWithSemiColon_ShouldThrow()
    {
        Action act = () => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger).AddAuthorization("admin:name", string.Empty);

        act.Should().ThrowExactly<ArgumentException>().WithMessage("username cannot contain the ':' character due to basic authentication limitations");
    }

    [TestMethod]
    [DataRow("héhé")]
    [DataRow("hàhà")]
    [DataRow("hèhè")]
    [DataRow("hùhù")]
    [DataRow("hûhû")]
    [DataRow("hähä")]
    [DataRow("höhö")]
    public void AddAuthorization_NonAsciiUserName_ShouldThrow(string userName)
    {
        Action act = () => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger).AddAuthorization(userName, "password");

        act.Should().ThrowExactly<ArgumentException>().WithMessage("username and password should contain only ASCII characters due to basic authentication limitations");
    }

    [TestMethod]
    [DataRow("héhé")]
    [DataRow("hàhà")]
    [DataRow("hèhè")]
    [DataRow("hùhù")]
    [DataRow("hûhû")]
    [DataRow("hähä")]
    [DataRow("höhö")]
    public void AddAuthorization_NonAsciiPassword_ShouldThrow(string password)
    {
        Action act = () => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger).AddAuthorization("userName", password);

        act.Should().ThrowExactly<ArgumentException>().WithMessage("username and password should contain only ASCII characters due to basic authentication limitations");
    }

    [TestMethod]
    public void AddCertificate_ExistingCertificateWithValidPassword_ShouldNotThrow() =>
        FluentActions.Invoking(() => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger).AddCertificate(CertificatePath, CertificatePassword)).Should().NotThrow();

    [TestMethod]
    [DataRow(null, "something")]
    [DataRow("something", null)]
    [DataRow(null, null)]
    public void AddCertificate_NullParameter_ShouldNotThrow(string clientCertPath, string clientCertPassword) =>
        FluentActions.Invoking(() => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger).AddCertificate(clientCertPath, clientCertPassword)).Should().NotThrow();

    [TestMethod]
    public void AddCertificate_CertificateDoesNotExist_ShouldThrow() =>
        FluentActions.Invoking(() => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger).AddCertificate("missingcert.pem", "dummypw")).Should().Throw<CryptographicException>();

    [DataTestMethod]
    [DataRow(null, "something")]
    [DataRow("something", null)]
    [DataRow(null, null)]
    public void AddServerCertificate_NullParameter_ShouldNotThrow(string serverCertPath, string serverCertPassword) =>
        FluentActions.Invoking(() => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger).AddServerCertificate(serverCertPath, serverCertPassword)).Should().NotThrow();

    [TestMethod]
    public void AddServerCertificate_CertificateDoesNotExist_ShouldThrow() =>
        FluentActions.Invoking(() => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger).AddServerCertificate("missingcert.pfx", "password")).Should().Throw<CryptographicException>();

    [TestMethod]
    public async Task SelfSignedClientAndServerCertificatesAreSupported()
    {
        // Arrange
        using var serverCert = CertificateBuilder.CreateWebServerCertificate();
        using var serverCertFile = new TempFile("pfx", x => File.WriteAllBytes(x, serverCert.Export(X509ContentType.Pfx)));
        using var server = ServerBuilder.StartServer(serverCert);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        using var clientCert = CertificateBuilder.CreateClientCertificate("test.user@sonarsource.com");
        using var clientCertFile = new TempFile("pfx", x => File.WriteAllBytes(x, clientCert.Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddCertificate(clientCertFile.FileName, string.Empty)
            .AddServerCertificate(serverCertFile.FileName, string.Empty);
        using var client = builder.Build();

        // Intercept the ServerCertificateCustomValidationCallback to assert the server certificate send to the client
        var handler = GetHandler(builder);
        handler.Should().NotBeNull();
        var callbackWasCalled = false;
        var registeredCertificateValidationCallback = handler.ServerCertificateCustomValidationCallback;
        handler.ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) =>
        {
            callbackWasCalled = true;
            certificate.Should().BeEquivalentTo(serverCert);
            return registeredCertificateValidationCallback(message, certificate, chain, errors);
        };

        // Act
        var response = await client.Download(server.Url);

        // Assert
        callbackWasCalled.Should().BeTrue();
        response.Should().Be("Hello World");
        server.LogEntries.Should().ContainSingle().Which.RequestMessage.ClientCertificate.Should().NotBeNull().And.BeEquivalentTo(clientCert);
    }

    [TestMethod]
    public async Task SelfSignedServerCertificatesIsOneOfManyInTruststore_Success()
    {
        // Arrange
        using var serverCert = CertificateBuilder.CreateWebServerCertificate();
        using var server = ServerBuilder.StartServer(serverCert);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        // Some other unrelated certificates are also in the truststore
        using var trustCert1 = CertificateBuilder.CreateWebServerCertificate().WithoutPrivateKey();
        using var trustCert2 = CertificateBuilder.CreateWebServerCertificate().WithoutPrivateKey();
        using var trustStore = new TempFile("pfx", x => File.WriteAllBytes(x, new X509Certificate2Collection(new[] { trustCert1, trustCert2, serverCert.WithoutPrivateKey() }).Export(X509ContentType.Pfx)));

        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStore.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var result = await client.Download(server.Url);

        // Assert
        result.Should().Be("Hello World");
    }

    [TestMethod]
    public async Task SelfSignedServerCertificatesIsNotInTruststore_Fails()
    {
        // Arrange
        using var serverCert = CertificateBuilder.CreateWebServerCertificate();
        using var server = ServerBuilder.StartServer(serverCert);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        using var trustCert1 = CertificateBuilder.CreateWebServerCertificate().WithoutPrivateKey();
        using var trustCert2 = CertificateBuilder.CreateWebServerCertificate().WithoutPrivateKey();
        using var trustStore = new TempFile("pfx", x => File.WriteAllBytes(x, new X509Certificate2Collection(new[] { trustCert1, trustCert2 }).Export(X509ContentType.Pfx)));

        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStore.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var download = async () => await client.Download(server.Url);

        // Assert
        (await download.Should().ThrowAsync<HttpRequestException>()).WithMessage("An error occurred while sending the request.")
            .WithInnerException<WebException>().WithMessage("The underlying connection was closed: Could not establish trust relationship for the SSL/TLS secure channel.")
            .WithInnerException<AuthenticationException>().WithMessage("The remote certificate is invalid according to the validation procedure.");
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(false, "google.com")]
    [DataRow(false, "sonarsource.com", "sonarcloud.io")]
    [DataRow(true, "localhost", "sonarcloud.io")]
    [DataRow(true, "sonarsource.com", "sonarcloud.io", "localhost")]
    public async Task SelfSignedServerCertificate_AlternateDomains(bool valid, params string[] domains)
    {
        // Arrange
        SubjectAlternativeNameBuilder subjectAlternativeNames = null;
        foreach (var domain in domains ?? [])
        {
            subjectAlternativeNames ??= new();
            subjectAlternativeNames.AddDnsName(domain);
        }
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(serverName: "NotLocalHost", subjectAlternativeNames: subjectAlternativeNames);
        using var server = ServerBuilder.StartServer(serverCert);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        using var trustStore = new TempFile("pfx", x => File.WriteAllBytes(x, serverCert.WithoutPrivateKey().Export(X509ContentType.Pfx)));

        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStore.FileName, string.Empty);
        using var client = builder.Build();
        // Intercept the ServerCertificateCustomValidationCallback to assert the server certificate send to the client
        var handler = GetHandler(builder);
        var callbackWasCalled = false;
        var registeredCertificateValidationCallback = handler.ServerCertificateCustomValidationCallback;
        handler.ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) =>
        {
            callbackWasCalled = true;
            certificate.Should().BeEquivalentTo(serverCert);
            return registeredCertificateValidationCallback(message, certificate, chain, errors);
        };

        // Act
        var download = async () => await client.Download(server.Url);

        // Assert
        if (valid)
        {
            await download.Should().NotThrowAsync().WithResult("Hello World");
        }
        else
        {
            (await download.Should().ThrowAsync<HttpRequestException>()).WithMessage("An error occurred while sending the request.")
                .WithInnerException<WebException>().WithMessage("The underlying connection was closed: Could not establish trust relationship for the SSL/TLS secure channel.")
                .WithInnerException<AuthenticationException>().WithMessage("The remote certificate is invalid according to the validation procedure.");
        }
        callbackWasCalled.Should().Be(true);
    }

    [TestMethod]
    public async Task SelfSignedServerCertificate_IgnoredIfServerIsTrusted()
    {
        // Arrange
        using var serverCert = CertificateBuilder.CreateWebServerCertificate();
        using var trustStore = new TempFile("pfx", x => File.WriteAllBytes(x, serverCert.WithoutPrivateKey().Export(X509ContentType.Pfx)));

        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStore.FileName, string.Empty);
        using var client = builder.Build();

        // Intercept the ServerCertificateCustomValidationCallback to assert the server certificate send to the client
        var handler = GetHandler(builder);
        var callbackWasCalled = false;
        var registeredCertificateValidationCallback = handler.ServerCertificateCustomValidationCallback;
        handler.ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) =>
        {
            callbackWasCalled = true;
            errors.Should().Be(SslPolicyErrors.None);
            return registeredCertificateValidationCallback(message, certificate, chain, errors);
        };

        // Act
        var result = await client.Download("https://httpbin.org/user-agent");

        // Assert
        var converted = JsonConvert.DeserializeObject<IDictionary<string, string>>(result);
        converted.Should().ContainSingle().Which.Value.Should().StartWith("SonarScanner-for-.NET/");
        callbackWasCalled.Should().BeTrue();
    }

    [DataTestMethod]
    [DataRow(-5, -2)]
    [DataRow(5, 10)]
    public async Task SelfSignedServerCertificate_InvalidDate_Fails(int notBeforeDays, int notAfterDays)
    {
        var today = DateTimeOffset.Now;
        // Arrange
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(notBefore: today.AddDays(notBeforeDays), notAfter: today.AddDays(notAfterDays));
        using var server = ServerBuilder.StartServer(serverCert);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        using var trustStore = new TempFile("pfx", x => File.WriteAllBytes(x, serverCert.WithoutPrivateKey().Export(X509ContentType.Pfx)));

        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStore.FileName, string.Empty);
        using var client = builder.Build();

        // Intercept the ServerCertificateCustomValidationCallback to assert the server certificate send to the client
        var handler = GetHandler(builder);
        var callbackWasCalled = false;
        var registeredCertificateValidationCallback = handler.ServerCertificateCustomValidationCallback;
        handler.ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) =>
        {
            callbackWasCalled = true;
            return registeredCertificateValidationCallback(message, certificate, chain, errors);
        };

        // Act
        var download = async () => await client.Download(server.Url);

        // Assert
        (await download.Should().ThrowAsync<HttpRequestException>())
            .WithInnerException<WebException>().WithInnerException<AuthenticationException>().WithMessage("The remote certificate is invalid according to the validation procedure.");
        callbackWasCalled.Should().BeTrue();
    }


    [TestMethod]
    public async Task CASignedCertIsTrusted()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA();
        using var caCertFileName = new TempFile("pfx", x => File.WriteAllBytes(x, caCert.WithoutPrivateKey().Export(X509ContentType.Pfx)));
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(caCert);
        using var serverCertFile = new TempFile("pfx", x => File.WriteAllBytes(x, serverCert.Export(X509ContentType.Pfx)));
        using var server = ServerBuilder.StartServer(serverCertFile.FileName);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(caCertFileName.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var response = await client.Download(server.Url);

        // Assert
        response.Should().Be("Hello World");
    }

    [TestMethod]
    public async Task CASignedCertWithCAsDifferentFromTrustStore_Fail()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA();
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(caCert);
        using var serverCertFile = new TempFile("pfx", x => File.WriteAllBytes(x, serverCert.Export(X509ContentType.Pfx)));
        using var server = ServerBuilder.StartServer(serverCertFile.FileName);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        var otherCAs = CertificateBuilder.BuildCollection([
            CertificateBuilder.CreateRootCA(name: "OtherCA1"),
            CertificateBuilder.CreateRootCA(name: "OtherCA2"),
            CertificateBuilder.CreateRootCA(name: "OtherCA3"),]);
        using var trustStoreFile = new TempFile("pfx", x => File.WriteAllBytes(x, otherCAs.Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStoreFile.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var download = async () => await client.Download(server.Url);

        // Assert
        (await download.Should().ThrowAsync<HttpRequestException>())
            .WithInnerException<WebException>().WithInnerException<AuthenticationException>().WithMessage("The remote certificate is invalid according to the validation procedure.");
    }

    [TestMethod]
    public async Task CASignedCertWithCAsDifferentFromTrustStoreButSameIssuerName_Fail()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA(name: "RootCA");
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(caCert);
        using var serverCertFile = new TempFile("pfx", x => File.WriteAllBytes(x, serverCert.Export(X509ContentType.Pfx)));
        using var server = ServerBuilder.StartServer(serverCertFile.FileName);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        var otherCAs = CertificateBuilder.BuildCollection([
            CertificateBuilder.CreateRootCA(name: "OtherCA1"),
            CertificateBuilder.CreateRootCA(name: "RootCA"), // Same name, but different Certificate (serial number and public key)
            CertificateBuilder.CreateRootCA(name: "OtherCA3"),]);
        using var trustStoreFile = new TempFile("pfx", x => File.WriteAllBytes(x, otherCAs.Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStoreFile.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var download = async () => await client.Download(server.Url);

        // Assert
        (await download.Should().ThrowAsync<HttpRequestException>())
            .WithInnerException<WebException>().WithInnerException<AuthenticationException>().WithMessage("The remote certificate is invalid according to the validation procedure.");
    }

    [TestMethod]
    public async Task CASignedCertWithCAsDifferentFromTrustStoreButSameIssuerNameAndSerialNumber_Fail()
    {
        // Arrange
        var serialNumber = Guid.NewGuid();
        using var caCert = CertificateBuilder.CreateRootCA(name: "RootCA", serialNumber: serialNumber);
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(caCert);
        using var serverCertFile = new TempFile("pfx", x => File.WriteAllBytes(x, serverCert.Export(X509ContentType.Pfx)));
        using var server = ServerBuilder.StartServer(serverCertFile.FileName);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        var otherCAs = CertificateBuilder.BuildCollection([
            CertificateBuilder.CreateRootCA(name: "OtherCA1"),
            CertificateBuilder.CreateRootCA(name: "RootCA", serialNumber: serialNumber), // Same name and serial number, but different Certificate (public key)
            CertificateBuilder.CreateRootCA(name: "OtherCA3"),]);
        using var trustStoreFile = new TempFile("pfx", x => File.WriteAllBytes(x, otherCAs.Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStoreFile.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var download = async () => await client.Download(server.Url);

        // Assert
        (await download.Should().ThrowAsync<HttpRequestException>())
            .WithInnerException<WebException>().WithInnerException<AuthenticationException>().WithMessage("The remote certificate is invalid according to the validation procedure.");
    }

    private static string GetHeader(WebClientDownloader downloader, string header)
    {
        var client = (HttpClient)new PrivateObject(downloader).GetField("client");
        return client.DefaultRequestHeaders.Contains(header)
            ? string.Join(";", client.DefaultRequestHeaders.GetValues(header))
            : null;
    }

    private static HttpClientHandler GetHandler(WebClientDownloaderBuilder downloaderBuilder) =>
        (HttpClientHandler)new PrivateObject(downloaderBuilder).GetField("handler");
}
