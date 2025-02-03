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

using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using WireMock.Server;
using WireMock.Settings;
using WireMock.Types;

namespace SonarScanner.MSBuild.PreProcessor.Test.Certificates;

internal static class ServerBuilder
{
    private const string FriendlyNameIdentifier = "S4NET WireMockServer certificate";

    /// <summary>
    /// Runs an SSL mock server on the next available port with the given webserver certificate.
    /// </summary>
    public static WireMockServer StartServer(X509Certificate2 certificate) =>
        StartServer(new X509Certificate2Collection(certificate));

    /// <summary>
    /// Runs an SSL mock server on the next available port with the given webserver certificates.
    /// </summary>
    public static WireMockServer StartServer(X509Certificate2Collection certificates)
    {
        var newCertificates = AddCertificatesToStore(certificates);
        var port = GetNextAvailablePort();
        var settings = new WireMockServerSettings
        {
            Urls = [$"https://localhost:{port}/"],
            UseSSL = true,
            AcceptAnyClientCertificate = true,
            ClientCertificateMode = ClientCertificateMode.AllowCertificate,
            CertificateSettings = new WireMockCertificateSettings
            {
                X509StoreName = StoreName.My.ToString(),
                X509StoreLocation = StoreLocation.CurrentUser.ToString(),
                X509StoreThumbprintOrSubjectName = newCertificates[0].Thumbprint,
            }
        };
        return new CertificateMockServer(settings);
    }

    private static X509Certificate2Collection AddCertificatesToStore(X509Certificate2Collection certificates)
    {
        RemoveTestCertificatesFromStores();
        var newCertificates = new X509Certificate2Collection();

        // Flags are needed because of occasional 0x8009030d errors https://stackoverflow.com/a/46091100
        newCertificates.Import(certificates.Export(X509ContentType.Pfx), string.Empty, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
        foreach (var newCertificate in newCertificates)
        {
            newCertificate.FriendlyName = FriendlyNameIdentifier; // This is used to identify the certificate later so we can remove it
            var isCA = newCertificate.Extensions.OfType<X509BasicConstraintsExtension>().Any(x => x.CertificateAuthority);
            if (isCA)
            {
                if (newCertificate.Issuer != newCertificate.Subject) // We only install intermediate CAs but not root CAs
                {
                    AddCertificateToStore(StoreName.CertificateAuthority, newCertificate); // The "Intermediate Certification Authorities" folder in mmc.exe -> Certificates -> Current User
                }
            }
            else
            {
                AddCertificateToStore(StoreName.My, newCertificate);
            }
        }
        return newCertificates;
    }

    private static void AddCertificateToStore(StoreName storeName, X509Certificate2 certificate)
    {
        using var store = new X509Store(storeName, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(certificate);
        store.Close();
    }

    private static void RemoveTestCertificatesFromStores()
    {
        var storeNames = new[] { StoreName.CertificateAuthority, StoreName.My }; // Remove from the intermediate CA store and from the personal store
        foreach (var storeName in storeNames)
        {
            using var store = new X509Store(storeName, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            var existingCertificates = new X509Certificate2Collection(store.Certificates.Cast<X509Certificate2>().Where(x => x.FriendlyName == FriendlyNameIdentifier).ToArray());
            store.RemoveRange(existingCertificates);
        }
    }

    private static int GetNextAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private class CertificateMockServer : WireMockServer
    {
        public CertificateMockServer(WireMockServerSettings settings) : base(settings) { }

        protected override void Dispose(bool disposing)
        {
            RemoveTestCertificatesFromStores();
            base.Dispose(disposing);
        }
    }
}
