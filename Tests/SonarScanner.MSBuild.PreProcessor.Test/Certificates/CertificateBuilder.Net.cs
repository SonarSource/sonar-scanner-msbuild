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

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Net.Http.Headers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace SonarScanner.MSBuild.PreProcessor.Test.Certificates;

internal static partial class CertificateBuilder
{
    public static (X509Extension CrlExtension, WireMockServer CrlServer, CertificateRevocationListBuilder RevocationListBuilder) CreateCrlExtension(X509Certificate2 issuer)
    {
        var crlBuilder = new CertificateRevocationListBuilder();
        var path = $"Revoked.crl";
        var crl = crlBuilder.Build(issuer, 1, DateTimeOffset.Now.AddYears(99), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var crlServer = WireMockServer.Start();
        crlServer.Given(Request.Create().WithPath($"/{path}")).RespondWith(Response.Create().WithCallback(_ =>
        {
            var crlResponse = crlBuilder.Build(issuer, 1, DateTimeOffset.Now.AddYears(99), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var response = Response.Create()
                .WithStatusCode(200)
                .WithHeader(HeaderNames.ContentType, "application/pkix-crl")
                .WithHeader(HeaderNames.ContentDisposition, $"attachment; filename={path}")
                .WithHeader(HeaderNames.ContentLength, crlResponse.Length.ToString())
                .WithBody(crlResponse);
            return ((Response)response).ResponseMessage;
        }));
        var extension = CertificateRevocationListBuilder.BuildCrlDistributionPointExtension((string[])[$"{crlServer.Url}/{path}"]);
        return (extension, crlServer, crlBuilder);
    }
}

#endif
