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
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Net.Http.Headers;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Types;
using WireMock.Util;

namespace SonarScanner.MSBuild.PreProcessor.Test.Certificates;

internal static partial class CertificateBuilder
{
    public static (WireMockServer CrlServer, X509Extension CrlExtension, CertificateRevocationListBuilder RevocationListBuilder) CreateCrlExtension(X509Certificate2 issuer)
    {
        var crlBuilder = new CertificateRevocationListBuilder();
        var path = $"Revoked.crl";
        var crlServer = WireMockServer.Start();
        var crl = crlBuilder.Build(issuer, 1, DateTimeOffset.Now.AddYears(99), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        crlServer.Given(Request.Create().WithPath($"/{path}")).RespondWith(Response.Create().WithCallback(_ =>
        {
            var crlResponse = crlBuilder.Build(issuer, 1, DateTimeOffset.Now.AddYears(99), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return new ResponseMessage()
            {
                StatusCode = 200,
                Headers = new Dictionary<string, WireMockList<string>>()
                {
                    { HeaderNames.ContentType, "application/pkix-crl" },
                    { HeaderNames.ContentDisposition, $"attachment; filename={path}" },
                    { HeaderNames.ContentLength, crlResponse.Length.ToString() }
                },
                BodyDestination = BodyDestinationFormat.Bytes,
                BodyData = new BodyData()
                {
                    DetectedBodyType = BodyType.Bytes,
                    BodyAsBytes = crlResponse
                },
            };
        }));
        var extension = CertificateRevocationListBuilder.BuildCrlDistributionPointExtension((string[])[$"{crlServer.Url}/{path}"]);
        return (crlServer, extension, crlBuilder);
    }
}

#endif
