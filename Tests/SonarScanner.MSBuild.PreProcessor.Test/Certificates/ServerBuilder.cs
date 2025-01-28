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

using System.IO;
using WireMock.Server;
using WireMock.Settings;

namespace SonarScanner.MSBuild.PreProcessor.Test.Certificates;

internal class ServerBuilder
{
    /// <summary>
    /// Runs an SSL mock server on port 8443 with the given webserver certificate file.
    /// </summary>
    public WireMockServer StartServer(string certificateFileName)
    {
        var settings = new WireMockServerSettings
        {
            Urls = ["https://localhost:8443/"],
            UseSSL = true,
            CertificateSettings = new WireMockCertificateSettings
            {
                X509CertificateFilePath = new FileInfo(certificateFileName).FullName,
            }
        };
        return WireMockServer.Start(settings);
    }
}
