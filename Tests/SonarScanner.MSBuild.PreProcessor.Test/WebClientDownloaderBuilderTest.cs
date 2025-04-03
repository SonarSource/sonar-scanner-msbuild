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
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using SonarScanner.MSBuild.PreProcessor.Test.Certificates;
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
    public void AddCertificate_CertificateDoesNotExist_ShouldThrow()
    {
        Action action = () => _ = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger).AddCertificate("missingcert.pem", "password");
        action.Should().ThrowOSBased<CryptographicException, CryptographicException, FileNotFoundException>();
    }

    [DataTestMethod]
    [DataRow(null, "something")]
    [DataRow("something", null)]
    [DataRow(null, null)]
    public void AddServerCertificate_NullParameter_ShouldNotThrow(string serverCertPath, string serverCertPassword) =>
        FluentActions.Invoking(() => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger).AddServerCertificate(serverCertPath, serverCertPassword)).Should().NotThrow();

    [TestMethod]
    public void AddServerCertificate_CertificateDoesNotExist_ShouldThrow()
    {
        Action action = () => _ = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger).AddServerCertificate("missingcert.pfx", "password");
        action.Should().ThrowOSBased<CryptographicException, CryptographicException, FileNotFoundException>();
    }

    [TestMethod]
    public void AddServerCertificate_InvalidPassword()
    {
        using var serverCert = CertificateBuilder.CreateWebServerCertificate();
        using var trustStore = new TempFile("pfx", x => File.WriteAllBytes(x, serverCert.WithoutPrivateKey().Export(X509ContentType.Pfx, "trustStoreCredential")));
        var builder = () => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStore.FileName, "wrongTrustStoreCredential");
        var reason = OSBasedMessage(
            windows: "The specified network password is not correct.",
            linux: "The certificate data cannot be read with the provided password, the password may be incorrect.",
            macos: "The certificate data cannot be read with the provided password, the password may be incorrect.");
        builder.Should().Throw<CryptographicException>().WithMessage(reason);
        logger.AssertErrorLogged($"Failed to import the sonar.scanner.truststorePath file {trustStore.FileName}: {reason}");
    }

    [TestMethod]
    public void AddServerCertificate_InvalidFileFormat()
    {
        using var brokenTrustStore = new TempFile("pfx", x => File.WriteAllText(x, "InvalidDummyContent"));
        var builder = () => new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(brokenTrustStore.FileName, string.Empty);
        var reason = OSBasedMessage(
            windows: "Cannot find the requested object.",
            linux: "ASN1 corrupted data.",
            macos: "Unknown format in import.");
        builder.Should().Throw<CryptographicException>().WithMessage(reason);
        logger.AssertErrorLogged($"Failed to import the sonar.scanner.truststorePath file {brokenTrustStore.FileName}: {reason}");
    }

#if NET

    [TestCategory(TestCategories.NoMacOS)]
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
        Action builder = () => _ = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(nonExisitentFile, string.Empty);
        var reason = OSBasedMessage(
            windows: "The system cannot find the file specified.",
            linux: "error:10000080:BIO routines::no such file",
            macos: $"Could not find file '{Path.Combine(Directory.GetCurrentDirectory(), nonExisitentFile)}'.");
        builder.Should().ThrowOSBased<CryptographicException, CryptographicException, FileNotFoundException>().WithMessage(reason);
        logger.AssertErrorLogged($"Failed to import the sonar.scanner.truststorePath file {nonExisitentFile}: {reason}");
    }

    [TestCategory(TestCategories.NoMacOS)]
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
            The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{serverCertFile.FileName}' (specified via the sonar.scanner.truststorePath parameter or its default value).
            """);
    }

    [TestCategory(TestCategories.NoMacOS)]
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
        using var trustStore = new TempFile("pfx",
            x => File.WriteAllBytes(x, new X509Certificate2Collection(new[] { trustCert1, trustCert2, serverCert.WithoutPrivateKey() }).Export(X509ContentType.Pfx)));

        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStore.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var result = await client.Download(server.Url);

        // Assert
        result.Should().Be("Hello World");
    }

    [TestCategory(TestCategories.NoMacOS)]
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
            The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{trustStore.FileName}' (specified via the sonar.scanner.truststorePath parameter or its default value).
            """);
        logger.AssertWarningLogged($"""
            The self-signed server certificate (Issuer: CN=localhost, Thumbprint: {serverCert.Thumbprint}) could not be found in the truststore file '{trustStore.FileName}' specified by parameter sonar.scanner.truststorePath or its default value.
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
            logger.AssertWarningLogged("""
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
        var result = await client.Download("https://www.cloudflarestatus.com/api/v2/status.json");

        // Assert
        var expected = new { Page = new { Name = "Cloudflare" } };
        var converted = JsonConvert.DeserializeAnonymousType(result, expected);
        converted.Should().BeEquivalentTo(expected);
        callbackWasCalled.Should().BeTrue();
    }

    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public async Task SelfSignedServerCertificate_NotYetValid_Fails()
    {
        await SelfSignedServerCertificate_InvalidDate(5, 10);
        AssertTrustStoreOtherChainStatusMessages(
            windows:
            [
                "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.",
                "A required certificate is not within its validity period when verifying against the current system clock or the timestamp in the signed file."
            ],
            linux: ["certificate is not yet valid", "self-signed certificate"],
            macos: []);
    }

    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public async Task SelfSignedServerCertificate_Expired_Fails()
    {
        await SelfSignedServerCertificate_InvalidDate(-5, -2);
        AssertTrustStoreOtherChainStatusMessages(
            windows:
            [
                "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.",
                "A required certificate is not within its validity period when verifying against the current system clock or the timestamp in the signed file."
            ],
            linux: ["certificate has expired", "self-signed certificate"],
            macos: []);
    }

    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public async Task CASignedCertIsTrusted()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA();
        using var caCertFile = new TempFile("pfx", x => File.WriteAllBytes(x, caCert.WithoutPrivateKey().Export(X509ContentType.Pfx)));
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(caCert);
        using var server = ServerBuilder.StartServer(serverCert);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(caCertFile.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var response = await client.Download(server.Url);

        // Assert
        response.Should().Be("Hello World");
        logger.AssertDebugLogged($"""
            The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{caCertFile.FileName}' (specified via the sonar.scanner.truststorePath parameter or its default value).
            """);
    }

    [TestCategory(TestCategories.NoMacOS)]
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
            CertificateBuilder.CreateRootCA(name: "OtherCA3")]);
        using var trustStoreFile = new TempFile("pfx", x => File.WriteAllBytes(x, otherCAs.Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStoreFile.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var download = async () => await client.Download(server.Url);

        // Assert
        await ShouldThrowServerValidationFailed(download);
        logger.AssertDebugLogged($"""
            The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{trustStoreFile.FileName}' (specified via the sonar.scanner.truststorePath parameter or its default value).
            """);
        AssertInvalidTrustStoreChainMessages(
            trustStoreFile.FileName,
            windows: ["A certificate chain could not be built to a trusted root authority."],
            linux: ["unable to get local issuer certificate"],
            macos: []);
    }

    [TestCategory(TestCategories.NoMacOS)]
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
            CertificateBuilder.CreateRootCA(name: "OtherCA3")]);
        using var trustStoreFile = new TempFile("pfx", x => File.WriteAllBytes(x, otherCAs.Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStoreFile.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var download = async () => await client.Download(server.Url);

        // Assert
        await ShouldThrowServerValidationFailed(download);
        logger.AssertDebugLogged($"""
            The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{trustStoreFile.FileName}' (specified via the sonar.scanner.truststorePath parameter or its default value).
            """);
        AssertInvalidTrustStoreChainMessages(
            trustStoreFile.FileName,
            windows: ["The signature of the certificate cannot be verified.", "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider."],
            linux: ["unable to get local issuer certificate"],
            macos: []);
    }

    [TestCategory(TestCategories.NoMacOS)]
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
            CertificateBuilder.CreateRootCA(name: "OtherCA3")]);
        using var trustStoreFile = new TempFile("pfx", x => File.WriteAllBytes(x, otherCAs.Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStoreFile.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var download = async () => await client.Download(server.Url);

        // Assert
        await ShouldThrowServerValidationFailed(download);
        logger.AssertDebugLogged($"""
            The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{trustStoreFile.FileName}' (specified via the sonar.scanner.truststorePath parameter or its default value).
            """);
        AssertInvalidTrustStoreChainMessages(
            trustStoreFile.FileName,
            windows: ["The signature of the certificate cannot be verified.", "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider."],
            linux: ["unable to get local issuer certificate"],
            macos: []);
    }

    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public async Task CASignedCertWithInvalidCA_Fail()
    {
        // Arrange
        var now = DateTimeOffset.Now;
        using var rsa = RSA.Create(2048);
        using var caCert = CertificateBuilder.CreateRootCA(name: "RootCA", privateKey: rsa); // Used for creating the webserver certificate
        // caCertInvalid is the same CA (same private key) but with an invalid timespan. This is used in the truststore.
        // It can not be used to create the web server certificate, because we want the webserver certificate to have a valid timespan here.
        using var caCertInvalid = CertificateBuilder.CreateRootCA(name: "RootCA", privateKey: rsa, notBefore: now.AddDays(1), notAfter: now.AddDays(2));
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(caCert);
        using var server = ServerBuilder.StartServer(serverCert);
        server.Given(Request.Create().WithPath("/").UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello World"));

        using var trustStoreFile = new TempFile("pfx", x => File.WriteAllBytes(x, caCertInvalid.Export(X509ContentType.Pfx)));
        var builder = new WebClientDownloaderBuilder(BaseAddress, httpTimeout, logger)
            .AddServerCertificate(trustStoreFile.FileName, string.Empty);
        using var client = builder.Build();

        // Act
        var download = async () => await client.Download(server.Url);

        // Assert
        await ShouldThrowServerValidationFailed(download);
        logger.AssertDebugLogged($"""
            The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{trustStoreFile.FileName}' (specified via the sonar.scanner.truststorePath parameter or its default value).
            """);
        AssertInvalidTrustStoreChainMessages(
            trustStoreFile.FileName,
            windows:
            [
                "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.",
                "A required certificate is not within its validity period when verifying against the current system clock or the timestamp in the signed file."
            ],
            linux: ["certificate is not yet valid", "self-signed certificate in certificate chain"],
            macos: []);
    }

    [TestCategory(TestCategories.NoMacOS)]
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
        logger.AssertDebugLogged($"""
            The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{trustStoreFile.FileName}' (specified via the sonar.scanner.truststorePath parameter or its default value).
            """);
    }

    [TestCategory(TestCategories.NoMacOS)]
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
        logger.AssertDebugLogged($"""
            The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{trustStoreFile.FileName}' (specified via the sonar.scanner.truststorePath parameter or its default value).
            """);
        AssertInvalidTrustStoreChainMessages(
            trustStoreFile.FileName,
            windows: ["A certificate chain could not be built to a trusted root authority."],
            linux: ["unable to get local issuer certificate"],
            macos: []);
    }

    [TestCategory(TestCategories.NoMacOS)]
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
        logger.AssertDebugLogged($"""
            The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{trustStoreFile.FileName}' (specified via the sonar.scanner.truststorePath parameter or its default value).
            """);
        AssertInvalidTrustStoreChainMessages(
            trustStoreFile.FileName,
            windows: ["A certificate chain could not be built to a trusted root authority."],
            linux: ["unable to get local issuer certificate"],
            macos: []);
    }

    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public async Task IntermediateCAsWithPartialChainInTrustStore()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA(); // only in TrustStore
        using var intermediateCAfromTrustStore = CertificateBuilder.CreateIntermediateCA(caCert, name: "IntermediateTrustStore"); // only in TrustStore
        using var intermediateCAfromServer = CertificateBuilder.CreateIntermediateCA(intermediateCAfromTrustStore, name: "IntermediateServer"); // only send by the server
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(intermediateCAfromServer); // only send by the server
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

    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public async Task IntermediateCAsWithPartialChainInTrustStoreWithOverlaps()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA(); // only in TrustStore
        using var intermediateCAfromTrustStore = CertificateBuilder.CreateIntermediateCA(caCert, name: "IntermediateTrustStore"); // only in TrustStore
        using var intermediateCAfromServerAndTrustStore = CertificateBuilder.CreateIntermediateCA(intermediateCAfromTrustStore, name: "IntermediateTrustStoreServer"); // in TrustStore and send by the server
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(intermediateCAfromServerAndTrustStore); // only send by the server
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

    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public async Task IntermediateCAsWithDifferentIntermediates_Fail()
    {
        // Arrange
        using var caCert = CertificateBuilder.CreateRootCA(); // only in TrustStore
        using var intermediateCAServer = CertificateBuilder.CreateIntermediateCA(caCert); // used as intermediate for the web server certificate but not send by the server
        using var intermediateCATrustStore = CertificateBuilder.CreateIntermediateCA(caCert); // a different intermediate with the same name found in TrustStore
        using var serverCert = CertificateBuilder.CreateWebServerCertificate(intermediateCAServer); // only send by the server
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
        logger.AssertDebugLogged($"""
            The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{trustStoreFile.FileName}' (specified via the sonar.scanner.truststorePath parameter or its default value).
            """);
        logger.Warnings.Should().ContainSingle(because: "the warning is either WARN_TrustStore_Chain_Invalid or WARN_TrustStore_OtherChainStatus depending on the environment.");
    }

    private async Task SelfSignedServerCertificate_InvalidDate(int notBeforeDays, int notAfterDays)
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
        logger.AssertDebugLogged($"""
            The remote server certificate is not trusted by the operating system. The scanner is checking the certificate against the certificates provided by the file '{trustStore.FileName}' (specified via the sonar.scanner.truststorePath parameter or its default value).
            """);
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

    private void AssertInvalidTrustStoreChainMessages(string truststorePath, string[] windows, string[] linux, string[] macos)
    {
        var messages = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? linux
                : macos;

        logger.AssertWarningLogged($"""
            The certificate chain of the web server certificate is invalid. The validation errors are
            {string.Join(Environment.NewLine, messages.Select(x => $"* {x}"))}
            Check the certificates in the truststore file '{truststorePath}' specified via the sonar.scanner.truststorePath parameter or its default value.
            """);
    }

    private void AssertTrustStoreOtherChainStatusMessages(string[] windows, string[] linux, string[] macos)
    {
        var messages = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? linux
                : macos;

        logger.AssertWarningLogged($"""
            The webserver returned an invalid certificate which could not be validated against the truststore file specified in sonar.scanner.truststorePath. The validation failed with these errors:
            {string.Join(Environment.NewLine, messages.Select(x => $"* {x}"))}
            """);
    }

    private string OSBasedMessage(string windows, string linux, string macos) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? linux
                : macos;

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
