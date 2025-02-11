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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
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
[DoNotParallelize]
public partial class WebClientDownloaderBuilderTest
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
    public void AddServerCertificate_InvalidPassword()
    {
        using var serverCert = CertificateBuilder.CreateWebServerCertificate();
        using var trustStore = new TempFile("pfx", x => File.WriteAllBytes(x, serverCert.WithoutPrivateKey().Export(X509ContentType.Pfx, "trustStoreCredential")));
        var builder = () => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStore.FileName, "wrongTrustStoreCredential");
        builder.Should().Throw<CryptographicException>().WithMessage("The specified network password is not correct.");
        logger.AssertErrorLogged($"Failed to import the sonar.scanner.truststorePath file {trustStore.FileName}: The specified network password is not correct.");
    }

    [TestMethod]
    public void AddServerCertificate_InvalidFileFormat()
    {
        using var brokenTrustStore = new TempFile("pfx", x => File.WriteAllText(x, "InvalidDummyContent"));
        var builder = () => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(brokenTrustStore.FileName, string.Empty);
        builder.Should().Throw<CryptographicException>().WithMessage("Cannot find the requested object.");
        logger.AssertErrorLogged($"Failed to import the sonar.scanner.truststorePath file {brokenTrustStore.FileName}: Cannot find the requested object.");
    }

#if NET

    [TestMethod]
    public void AddServerCertificate_PemFormatSupportedInNet()
    {
        using var serverCert = CertificateBuilder.CreateWebServerCertificate();
        using var trustStore = new TempFile("pem", x => File.WriteAllText(x, serverCert.WithoutPrivateKey().ExportCertificatePem()));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStore.FileName, string.Empty);
        using var server = ServerBuilder.StartServer(serverCert);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        using var client = builder.Build();
        var response = client.Download(server.Url).Result;
        response.Should().Be("Hello World");
    }

#endif

    [TestMethod]
    public void AddServerCertificate_FileNotFound()
    {
        var nonExisitentFile = Path.GetRandomFileName();
        var builder = () => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(nonExisitentFile, string.Empty);
        builder.Should().Throw<CryptographicException>().WithMessage("The system cannot find the file specified.");
        logger.AssertErrorLogged($"Failed to import the sonar.scanner.truststorePath file {nonExisitentFile}: The system cannot find the file specified.");
    }

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
        logger.AssertDebugLogged($"""
        The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{serverCertFile.FileName}' (specified via the sonar.scanner.truststorePath parameter or it's default value).
        """);
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
        await ShouldThrowServerValidationFailed(download);
        logger.AssertDebugLogged($"""
        The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{trustStore.FileName}' (specified via the sonar.scanner.truststorePath parameter or it's default value).
        """);
        logger.AssertWarningLogged($"""
            The self-signed server certificate (Issuer: CN=localhost, Thumbprint: {serverCert.Thumbprint}) could not be found in the truststore file '{trustStore.FileName}' specified by sonar.scanner.truststorePath.
            """);
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
            // Assert
            await ShouldThrowServerValidationFailed(download);
            logger.AssertDebugLogged("""
                The webserver returned an invalid certificate. Error details: RemoteCertificateNameMismatch, RemoteCertificateChainErrors
                """);
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
        await ShouldThrowServerValidationFailed(download);
        callbackWasCalled.Should().BeTrue();
        logger.AssertDebugLogged("""
            The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the sonar.scanner.truststorePath file.
            """);
        logger.AssertDebugLogged("""
            The webserver returned an invalid certificate which could not be validated against the truststore file specified in sonar.scanner.truststorePath. The validation failed with these errors: 
            * A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.
            * A required certificate is not within its validity period when verifying against the current system clock or the timestamp in the signed file.
            """);
    }

    [TestMethod]
    public async Task CASignedCertIsTrusted()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA();
        using var caCertFileName = new TempFile("pfx", x => File.WriteAllBytes(x, caCert.WithoutPrivateKey().Export(X509ContentType.Pfx)));
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(caCert);
        using var server = ServerBuilder.StartServer(serverCert);
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
        using var server = ServerBuilder.StartServer(serverCert);
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
        await ShouldThrowServerValidationFailed(download);
    }

    [TestMethod]
    public async Task CASignedCertWithCAsDifferentFromTrustStoreButSameIssuerName_Fail()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA(name: "RootCA");
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(caCert);
        using var server = ServerBuilder.StartServer(serverCert);
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
        await ShouldThrowServerValidationFailed(download);
    }

    [TestMethod]
    public async Task CASignedCertWithCAsDifferentFromTrustStoreButSameIssuerNameAndSerialNumber_Fail()
    {
        // Arrange
        var serialNumber = Guid.NewGuid();
        using var caCert = CertificateBuilder.CreateRootCA(name: "RootCA", serialNumber: serialNumber);
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(caCert);
        using var server = ServerBuilder.StartServer(serverCert);
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
        await ShouldThrowServerValidationFailed(download);
    }

    [TestMethod]
    public async Task IntermediateCAWithTrustedRoot()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA();
        using var intermediateCA = CertificateBuilder.CreateIntermediateCA(caCert);
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(intermediateCA);
        using var server = ServerBuilder.StartServer(CertificateBuilder.BuildCollection(serverCert, [intermediateCA]));
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        using var trustStoreFile = new TempFile("pfx", x => File.WriteAllBytes(x, caCert.Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStoreFile.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var result = await client.Download(server.Url);

        // Assert
        result.Should().Be("Hello World");
    }

    [TestMethod]
    public async Task IntermediateCAWithTrustedIntermediateCA_Fail()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA();
        using var intermediateCA = CertificateBuilder.CreateIntermediateCA(caCert);
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(intermediateCA);
        using var server = ServerBuilder.StartServer(CertificateBuilder.BuildCollection(serverCert, [intermediateCA]));
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        using var trustStoreFile = new TempFile("pfx", x => File.WriteAllBytes(x, intermediateCA.WithoutPrivateKey().Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStoreFile.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var downloader = async () => await client.Download(server.Url);

        // Assert
        // This is also how HttpClient behaves: it only trusts complete chains that end in a Root CA. An Intermediate CA is not enough
        await ShouldThrowServerValidationFailed(downloader);
    }

    [TestMethod]
    public async Task IntermediateCAWithTrustedWebseverCertificate_Fail()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA();
        using var intermediateCA = CertificateBuilder.CreateIntermediateCA(caCert);
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(intermediateCA);
        using var server = ServerBuilder.StartServer(CertificateBuilder.BuildCollection(serverCert, [intermediateCA]));
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        using var trustStoreFile = new TempFile("pfx", x => File.WriteAllBytes(x, serverCert.WithoutPrivateKey().Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStoreFile.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var downloader = async () => await client.Download(server.Url);

        // Assert
        // This is also how HttpClient behaves: If the web server certificate is not self-signed, the chain must end in a Root-CA
        await ShouldThrowServerValidationFailed(downloader);
    }

    [TestMethod]
    public async Task IntermediateCAsWithPartialChainInTrustStore()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA(); // only in TrustStore
        using var intermediateCAfromTrustStore = CertificateBuilder.CreateIntermediateCA(caCert, name: "IntermediateTrustStore"); // only in TrustStore
        using var intermediateCAfromServer = CertificateBuilder.CreateIntermediateCA(intermediateCAfromTrustStore, name: "IntermediateServer"); // only send by the server
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(intermediateCAfromServer);  // only send by the server
        using var server = ServerBuilder.StartServer(CertificateBuilder.BuildCollection(serverCert, [intermediateCAfromServer]));
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        using var trustStoreFile = new TempFile("pfx", x => File.WriteAllBytes(x, CertificateBuilder.BuildCollection([
            caCert.WithoutPrivateKey(),
            intermediateCAfromTrustStore.WithoutPrivateKey(),
            ]).Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStoreFile.FileName, string.Empty);
        using var client = builder.Build();
        // Intercept the ServerCertificateCustomValidationCallback to assert the certificate chain send to the client
        var handler = GetHandler(builder);
        List<X509Certificate2> chainSendByServer = null;
        var registeredCertificateValidationCallback = handler.ServerCertificateCustomValidationCallback;
        handler.ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) =>
        {
            chainSendByServer = chain.ChainElements.Cast<X509ChainElement>().Select(x => x.Certificate.WithoutPrivateKey()).ToList(); // Make a copy of the certificates before they get disposed
            return registeredCertificateValidationCallback(message, certificate, chain, errors);
        };

        // Act
        var result = await client.Download(server.Url);

        // Assert
        result.Should().Be("Hello World");
        chainSendByServer.Should().NotBeNull().And.BeEquivalentTo([serverCert, intermediateCAfromServer]);
    }

    [TestMethod]
    public async Task IntermediateCAsWithPartialChainInTrustStoreWithOverlaps()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA(); // only in TrustStore
        using var intermediateCAfromTrustStore = CertificateBuilder.CreateIntermediateCA(caCert, name: "IntermediateTrustStore"); // only in TrustStore
        using var intermediateCAfromServerAndTrustStore = CertificateBuilder.CreateIntermediateCA(intermediateCAfromTrustStore, name: "IntermediateTrustStoreServer"); // in TrustStore and send by the server
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(intermediateCAfromServerAndTrustStore);  // only send by the server
        using var server = ServerBuilder.StartServer(CertificateBuilder.BuildCollection(serverCert, [intermediateCAfromServerAndTrustStore]));
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        using var trustStoreFile = new TempFile("pfx", x => File.WriteAllBytes(x, CertificateBuilder.BuildCollection([
            caCert.WithoutPrivateKey(),
            intermediateCAfromTrustStore.WithoutPrivateKey(),
            intermediateCAfromServerAndTrustStore.WithoutPrivateKey(),
            ]).Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStoreFile.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var result = await client.Download(server.Url);

        // Assert
        result.Should().Be("Hello World");
    }

    [TestMethod]
    public async Task IntermediateCAsWithDifferentIntermediates_Fail()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA(); // only in TrustStore
        using var intermediateCAServer = CertificateBuilder.CreateIntermediateCA(caCert); // used as intermediate for the web server certificate but not send by the server
        using var intermediateCATrustStore = CertificateBuilder.CreateIntermediateCA(caCert); // a different intermediate with the same name found in TrustStore
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(intermediateCAServer);  // only send by the server
        using var server = ServerBuilder.StartServer(serverCert);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        using var trustStoreFile = new TempFile("pfx", x => File.WriteAllBytes(x, CertificateBuilder.BuildCollection([
            caCert.WithoutPrivateKey(),
            intermediateCATrustStore.WithoutPrivateKey(),
            ]).Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStoreFile.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var downloader = async () => await client.Download(server.Url);

        // Assert
        // A trusted chain can not be build, because intermediateCAServer is not send by the server. The intermediateCATrustStore found in the trust store is used to build
        // the chain, but the chain status contains the error "The signature of the certificate cannot be verified.".
        await ShouldThrowServerValidationFailed(downloader);
    }

    private static string GetHeader(WebClientDownloader downloader, string header)
    {
        var client = (HttpClient)downloader.GetType().GetField("client", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(downloader);
        return client.DefaultRequestHeaders.Contains(header)
            ? string.Join(";", client.DefaultRequestHeaders.GetValues(header))
            : null;
    }

    private static HttpClientHandler GetHandler(WebClientDownloaderBuilder downloaderBuilder) =>
        (HttpClientHandler)downloaderBuilder.GetType().GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(downloaderBuilder);

    private static async Task ShouldThrowServerValidationFailed(Func<Task<string>> downloader)
    {
        (await downloader.Should().ThrowAsync<HttpRequestException>())
#if NET
           .WithMessage("The SSL connection could not be established, see inner exception.")
           .WithInnerException<AuthenticationException>().WithMessage("The remote certificate was rejected by the provided RemoteCertificateValidationCallback.");
#else
           .WithMessage("An error occurred while sending the request.")
           .WithInnerException<WebException>().WithMessage("The underlying connection was closed: Could not establish trust relationship for the SSL/TLS secure channel.")
           .WithInnerException<AuthenticationException>().WithMessage("The remote certificate is invalid according to the validation procedure.");
#endif
    }
}
