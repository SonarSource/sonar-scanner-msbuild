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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SonarScanner.MSBuild.PreProcessor.Test.Certificates;

[Flags]
public enum WebServerCertificateExtensions
{
    None = 0,
    DigitalSignature = 1 << 0,
    KeyEncipherment = 1 << 1,
    ServerAuthentication = 1 << 2,
}

internal static class CertificateBuilder
{
    private const string DefaultHostName = "localhost";

    public static X509Certificate2 CreateWebServerCertificate(string severname = DefaultHostName, DateTimeOffset notBefore = default, DateTimeOffset notAfter = default, WebServerCertificateExtensions webServerCertificateExtensions = WebServerCertificateExtensions.DigitalSignature | WebServerCertificateExtensions.KeyEncipherment | WebServerCertificateExtensions.ServerAuthentication)
    {
        using var rsa = RSA.Create();
        var certRequest = CreateWebserverCertifcateRequest(severname, webServerCertificateExtensions, rsa);
        SanitizeNotBeforeNotAfter(ref notBefore, ref notAfter);
        return certRequest.CreateSelfSigned(notBefore, notAfter);
    }

    public static X509Certificate2 CreateWebServerCertificate(X509Certificate2 issuer, string severname = DefaultHostName, DateTimeOffset notBefore = default, DateTimeOffset notAfter = default, WebServerCertificateExtensions webServerCertificateExtensions = WebServerCertificateExtensions.DigitalSignature | WebServerCertificateExtensions.KeyEncipherment | WebServerCertificateExtensions.ServerAuthentication)
    {
        var rsa = RSA.Create();
        var certRequest = CreateWebserverCertifcateRequest(severname, webServerCertificateExtensions, rsa);
        certRequest.CertificateExtensions.Add(new X509AuthorityKeyIdentifierExtension(issuer, false));
        SanitizeNotBeforeNotAfter(ref notBefore, ref notAfter);
        var generatedCert = certRequest.Create(
            issuer,
            notBefore,
            notAfter,
            Guid.NewGuid().ToByteArray());
        return generatedCert.CopyWithPrivateKey(rsa);
    }

    private static CertificateRequest CreateWebserverCertifcateRequest(string severname, WebServerCertificateExtensions webServerCertificateExtensions, RSA rsa)
    {
        var certRequest = new CertificateRequest($"CN={severname}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var keyUsage = webServerCertificateExtensions.HasFlag(WebServerCertificateExtensions.DigitalSignature) ? X509KeyUsageFlags.DigitalSignature : 0;
        keyUsage |= webServerCertificateExtensions.HasFlag(WebServerCertificateExtensions.KeyEncipherment) ? X509KeyUsageFlags.KeyEncipherment : 0;
        if (keyUsage != X509KeyUsageFlags.None)
        {
            certRequest.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, true));
        }
        if (webServerCertificateExtensions.HasFlag(WebServerCertificateExtensions.ServerAuthentication))
        {
            certRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection() { Oid.FromFriendlyName("Server Authentication", OidGroup.EnhancedKeyUsage) }, true));
        }

        return certRequest;
    }

    private static void SanitizeNotBeforeNotAfter(ref DateTimeOffset notBefore, ref DateTimeOffset notAfter)
    {
        if (notBefore == default)
        {
            notBefore = DateTimeOffset.Now;
        }
        if (notAfter == default)
        {
            notAfter = notBefore.AddDays(1);
        }
    }

    // Source: https://shawinnes.com/dotnet-x509-extensions/
    private class X509AuthorityKeyIdentifierExtension : X509Extension
    {
        private static Oid AuthorityKeyIdentifierOid => new Oid("2.5.29.35");
        private static Oid SubjectKeyIdentifierOid => new Oid("2.5.29.14");

        public X509AuthorityKeyIdentifierExtension(X509Certificate2 certificateAuthority, bool critical)
            : base(AuthorityKeyIdentifierOid, EncodeExtension(certificateAuthority), critical) { }

        private static byte[] EncodeExtension(X509Certificate2 certificateAuthority)
        {
            var subjectKeyIdentifier = certificateAuthority.Extensions.Cast<X509Extension>().FirstOrDefault(p => p.Oid?.Value == SubjectKeyIdentifierOid.Value);
            if (subjectKeyIdentifier is null)
            {
                return null;
            }
            var rawData = subjectKeyIdentifier.RawData;
            var segment = rawData.Skip(2).ToArray();
            var authorityKeyIdentifier = new byte[segment.Length + 4];
            // KeyID of the AuthorityKeyIdentifier
            authorityKeyIdentifier[0] = 0x30;
            authorityKeyIdentifier[1] = 0x16;
            authorityKeyIdentifier[2] = 0x80;
            authorityKeyIdentifier[3] = 0x14;
            Array.Copy(segment, 0, authorityKeyIdentifier, 4, segment.Length);
            return authorityKeyIdentifier;
        }
    }
}
