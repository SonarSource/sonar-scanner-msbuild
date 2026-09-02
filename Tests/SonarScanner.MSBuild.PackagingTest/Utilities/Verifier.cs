/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

using System.IO.Compression;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Pkcs;

namespace SonarScanner.MSBuild.PackagingTest.Utilities;

public static class Verifier
{
    public const string PackageSignatureEntryName = ".signature.p7s";

    private const int HeaderSize = 8; // WIN_CERTIFICATE header: dwLength(4) + wRevision(2) + wCertificateType(2) = 8 bytes, followed by the PKCS#7 blob

    public static ZipArchive UnzipFile(string directoryName, string pattern)
    {
        var path = directoryName is null ? Paths.BinariesRoot : Path.Combine(Paths.BinariesRoot, directoryName);
        var file = Directory.GetFiles(path, pattern).Should().ContainSingle().Subject;
        return new(File.OpenRead(file), ZipArchiveMode.Read);
    }

    public static string[] UnzippedFileList(string directoryName, string pattern)
    {
        using var archive = UnzipFile(directoryName, pattern);
        return archive.Entries.Select(x => x.FullName).ToArray();
    }

    public static bool IsSonarBinary(ZipArchiveEntry entry) =>
        entry.Name.StartsWith("Sonar", StringComparison.OrdinalIgnoreCase)
        && (entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

    public static void ValidateDllSignature(ZipArchiveEntry entry)
    {
        using var peReader = new PEReader(ImmutableCollectionsMarshal.AsImmutableArray(ReadBytes(entry)));
        var certificates = peReader.PEHeaders.PEHeader.CertificateTableDirectory;
        certificates.Size.Should().NotBe(0, $"file {entry.FullName} should contain signature");
        var cms = new SignedCms();
        cms.Decode(peReader.GetEntireImage().GetContent().AsSpan(certificates.RelativeVirtualAddress + HeaderSize, certificates.Size - HeaderSize));
        ValidateSignerCertificate(cms, entry.FullName);
    }

    public static void ValidatePackageSignature(ZipArchive archive)
    {
        var entry = archive.GetEntry(PackageSignatureEntryName);
        entry.Should().NotBeNull($"the package should contain the '{PackageSignatureEntryName}' entry");
        var cms = new SignedCms();
        cms.Decode(ReadBytes(entry));
        ValidateSignerCertificate(cms, entry.FullName);
    }

    private static void ValidateSignerCertificate(SignedCms cms, string entryName) =>
        cms.Certificates.Should().ContainSingle(
            x => x.Subject == """CN="SonarSource US, Inc.", O="SonarSource US, Inc.", L=Austin, S=Texas, C=US"""                   // Azure Trusted Signing (release)
                    || x.Subject == """CN="SonarSource US, Inc.(TEST ONLY)", O="SonarSource US, Inc.", L=Austin, S=Texas, C=US"""  // Azure Trusted Signing (test, PR builds)
                    || x.Subject == "CN=SonarSource SA, O=SonarSource SA, L=Vernier, S=Genève, C=CH",                              // NuGet package signature (DigiCert-chained release only)
            $"'{entryName}' should be signed with a SonarSource certificate.");

    private static byte[] ReadBytes(ZipArchiveEntry entry)
    {
        var buffer = new byte[entry.Length];
        using var entryStream = entry.Open();
        entryStream.ReadExactly(buffer);
        return buffer;
    }
}
