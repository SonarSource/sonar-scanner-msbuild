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

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TestUtilities.Certificates;

[Flags]
public enum WebServerCertificateExtensions
{
    None = 0,
    DigitalSignature = 1 << 0,
    KeyEncipherment = 1 << 1,
    ServerAuthentication = 1 << 2,
}

public static partial class CertificateBuilder
{
    private const string DefaultHostName = "localhost";

    public static X509Certificate2 CreateWebServerCertificate(
        string serverName = DefaultHostName,
        DateTimeOffset notBefore = default,
        DateTimeOffset notAfter = default,
        WebServerCertificateExtensions webServerCertificateExtensions = WebServerCertificateExtensions.DigitalSignature | WebServerCertificateExtensions.KeyEncipherment | WebServerCertificateExtensions.ServerAuthentication,
        SubjectAlternativeNameBuilder subjectAlternativeNames = null,
        int keyLength = 2048)
    {
        using var rsa = RSA.Create(keyLength);
        var request = CreateWebserverCertificateRequest(serverName, webServerCertificateExtensions, rsa, subjectAlternativeNames);
        SanitizeNotBeforeNotAfter(ref notBefore, ref notAfter);
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    public static X509Certificate2 CreateWebServerCertificate(
        X509Certificate2 issuer,
        string serverName = DefaultHostName,
        DateTimeOffset notBefore = default,
        DateTimeOffset notAfter = default,
        WebServerCertificateExtensions webServerCertificateExtensions = WebServerCertificateExtensions.DigitalSignature | WebServerCertificateExtensions.KeyEncipherment | WebServerCertificateExtensions.ServerAuthentication,
        SubjectAlternativeNameBuilder subjectAlternativeNames = null,
        int keyLength = 2048,
        Action<CertificateRequest> configureCertificateRequest = null)
    {
        var rsa = RSA.Create(keyLength);
        var request = CreateWebserverCertificateRequest(serverName, webServerCertificateExtensions, rsa, subjectAlternativeNames);
        request.CertificateExtensions.Add(new X509AuthorityKeyIdentifierExtension(issuer, false));
        configureCertificateRequest?.Invoke(request);
        SanitizeNotBeforeNotAfter(ref notBefore, ref notAfter);
        using var generatedCert = request.Create(issuer, notBefore, notAfter, Guid.NewGuid().ToByteArray());
        return generatedCert.CopyWithPrivateKey(rsa);
    }

    public static X509Certificate2 CreateRootCA(
        string name = "RootCA",
        X509KeyUsageFlags keyUsage = X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
        DateTimeOffset notBefore = default,
        DateTimeOffset notAfter = default,
        int keyLength = 2048,
        Guid serialNumber = default,
        RSA privateKey = null)
    {
        using var rsa = RSA.Create(keyLength);
        var keyToUse = privateKey ?? rsa;
        var request = new CertificateRequest($"CN={name}", keyToUse, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, true));

        SanitizeNotBeforeNotAfter(ref notBefore, ref notAfter);
        serialNumber = serialNumber == default ? Guid.NewGuid() : serialNumber;
        var rootCA = request.Create(request.SubjectName, X509SignatureGenerator.CreateForRSA(keyToUse, RSASignaturePadding.Pkcs1), notBefore, notAfter, serialNumber.ToByteArray());
        rootCA = rootCA.CopyWithPrivateKey(keyToUse);
        return rootCA;
    }

    public static X509Certificate2 CreateIntermediateCA(
        X509Certificate2 issuer,
        string name = "IntermediateCA",
        DateTimeOffset notBefore = default,
        DateTimeOffset notAfter = default,
        int keyLength = 2048)
    {
        var rsa = RSA.Create(keyLength);
        var request = new CertificateRequest($"CN={name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        // Set the certificate extensions
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(new X509AuthorityKeyIdentifierExtension(issuer, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        // .Net 5 only:
        // request.CertificateExtensions.Add(CertificateRevocationListBuilder.BuildCrlDistributionPointExtension((string[])["http://localhost:9999/Root.crl"]));

        // Sign the certificate with the issuer certificate
        SanitizeNotBeforeNotAfter(ref notBefore, ref notAfter);
        using var intermediateCert = request.Create(issuer, notBefore, notAfter, Guid.NewGuid().ToByteArray());
        return intermediateCert.CopyWithPrivateKey(rsa);
    }

    public static X509Certificate2 CreateClientCertificate(string subject, DateTimeOffset notBefore = default, DateTimeOffset notAfter = default, int keyLength = 2048)
    {
        using var rsa = RSA.Create(keyLength);
        var request = CreateClientCertifcateRequest(subject, rsa);
        SanitizeNotBeforeNotAfter(ref notBefore, ref notAfter);
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    public static X509Certificate2Collection BuildCollection(X509Certificate2 webServerCertificate, X509Certificate2[] issuer) =>
        [webServerCertificate, .. issuer.Select(x => new X509Certificate2(x.RawData))];

    public static X509Certificate2Collection BuildCollection(X509Certificate2[] issuer) =>
        [.. issuer.Select(x => new X509Certificate2(x.RawData))];

    private static CertificateRequest CreateWebserverCertificateRequest(string serverName, WebServerCertificateExtensions webServerCertificateExtensions, RSA rsa, SubjectAlternativeNameBuilder subjectAlternativeNames)
    {
        var request = new CertificateRequest($"CN={serverName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        var keyUsage = webServerCertificateExtensions.HasFlag(WebServerCertificateExtensions.DigitalSignature) ? X509KeyUsageFlags.DigitalSignature : 0;
        keyUsage |= webServerCertificateExtensions.HasFlag(WebServerCertificateExtensions.KeyEncipherment) ? X509KeyUsageFlags.KeyEncipherment : 0;
        if (keyUsage != X509KeyUsageFlags.None)
        {
            request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, true));
        }
        if (webServerCertificateExtensions.HasFlag(WebServerCertificateExtensions.ServerAuthentication))
        {
            AddServerAuthenticationOid(request);
        }
        if (subjectAlternativeNames is null)
        {
            subjectAlternativeNames = new SubjectAlternativeNameBuilder();
            subjectAlternativeNames.AddDnsName(serverName);
        }
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        return request;
    }

    private static CertificateRequest CreateClientCertifcateRequest(string subject, RSA rsa)
    {
        var request = new CertificateRequest($"CN={subject}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        var keyUsage = X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment;
        request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, true));
        AddClientAuthenticationOid(request);
        return request;
    }

    private static void SanitizeNotBeforeNotAfter(ref DateTimeOffset notBefore, ref DateTimeOffset notAfter)
    {
        var defaultReferenceDate = DateTimeOffset.Now.Date;
        if (notBefore == default)
        {
            notBefore = defaultReferenceDate.AddDays(-2);
        }
        if (notAfter == default)
        {
            notAfter = defaultReferenceDate.AddDays(2);
        }
    }

    private static void AddServerAuthenticationOid(CertificateRequest request) =>
        request.CertificateExtensions.Add(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new X509EnhancedKeyUsageExtension([Oid.FromFriendlyName("Server Authentication", OidGroup.EnhancedKeyUsage)], true)
            // OidGroup is not available on Unix
            // https://oidref.com/1.3.6.1.5.5.7.3.1
            : new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], true));

    private static void AddClientAuthenticationOid(CertificateRequest request) =>
        request.CertificateExtensions.Add(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new X509EnhancedKeyUsageExtension([Oid.FromFriendlyName("Client Authentication", OidGroup.EnhancedKeyUsage)], true)
            // OidGroup is not available on Unix
            // https://oidref.com/1.3.6.1.5.5.7.3.2
            : new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.2")], true));

    // Source: https://shawinnes.com/dotnet-x509-extensions/
    // .Net 5: Use https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509authoritykeyidentifierextension
    private class X509AuthorityKeyIdentifierExtension : X509Extension
    {
        private static Oid authorityKeyIdentifierOid = new Oid("2.5.29.35");
        private static Oid subjectKeyIdentifierOid = new Oid("2.5.29.14");

        public X509AuthorityKeyIdentifierExtension(X509Certificate2 certificateAuthority, bool critical)
            : base(authorityKeyIdentifierOid, EncodeExtension(certificateAuthority), critical) { }

        private static byte[] EncodeExtension(X509Certificate2 certificateAuthority)
        {
            var subjectKeyIdentifier = certificateAuthority.Extensions.Cast<X509Extension>().FirstOrDefault(x => x.Oid?.Value == subjectKeyIdentifierOid.Value);
            if (subjectKeyIdentifier is null)
            {
                return null;
            }
            var segment = subjectKeyIdentifier.RawData.Skip(2).ToArray();
            var authorityKeyIdentifier = new byte[segment.Length + 4];
            // KeyID of the AuthorityKeyIdentifier
            authorityKeyIdentifier[0] = 0x30;
            authorityKeyIdentifier[1] = 0x16;
            authorityKeyIdentifier[2] = 0x80;
            authorityKeyIdentifier[3] = 0x14;
            Array.Copy(sourceArray: segment, sourceIndex: 0, destinationArray: authorityKeyIdentifier, destinationIndex: 4, segment.Length);
            return authorityKeyIdentifier;
        }
    }
}
