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

using System.Security.Cryptography.X509Certificates;

namespace TestUtilities.Certificates;

public static class X509Certificate2Extensions
{
    public static X509Certificate2 WithoutPrivateKey(this X509Certificate2 certificate) =>
#if NET
        X509CertificateLoader.LoadCertificate(certificate.RawData);
#else
        new(certificate.RawData);
#endif

    public static void ToPfx(this X509Certificate2 certificate, string filePath, string password)
    {
        // Export the certificate with its private key to a PFX file
        var pfxData = certificate.Export(X509ContentType.Pfx, password);

        // Write the PFX data to the specified file
        File.WriteAllBytes(filePath, pfxData);
    }
}
