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
using System.Security.Cryptography.Pkcs;

namespace SonarScanner.MSBuild.PackagingTest.Utilities;

public static class Verifier
{
    private const int HeaderSize = 8; // WIN_CERTIFICATE header: dwLength(4) + wRevision(2) + wCertificateType(2) = 8 bytes, followed by the PKCS#7 blob

    public static ZipArchive UnzipFile(string directoryName, string pattern)
    {
        var path = directoryName is null ? Paths.BinariesRoot : Path.Combine(Paths.BinariesRoot, directoryName);
        var file = Directory.GetFiles(path, pattern).Should().ContainSingle().Subject;
        return new(File.OpenRead(file), ZipArchiveMode.Read);
    }

    public static string[] UnzippedFileList(string directoryName, string pattern)
    {
        using var archive = Verifier.UnzipFile(directoryName, pattern);
        return archive.Entries.Select(x => x.FullName).ToArray();
    }

    public static bool IsSonarBinary(ZipArchiveEntry entry) =>
        entry.Name.StartsWith("Sonar", StringComparison.OrdinalIgnoreCase)
        && (entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

    public static void ValidateSignature(ZipArchiveEntry entry)
    {
        using var stream = ReadStream(entry);
        using var peReader = new PEReader(stream);
        var certificates = peReader.PEHeaders.PEHeader.CertificateTableDirectory;
        certificates.Size.Should().NotBe(0, $"file {entry.FullName} should contain signature");
        var cms = new SignedCms();
        cms.Decode(peReader.GetEntireImage().GetContent().AsSpan(certificates.RelativeVirtualAddress + HeaderSize, certificates.Size - HeaderSize));
        // Release branches sign with the real SonarSource SA certificate; other branches get Azure Trusted Signing's test certificate instead.
        var expectedSubject = TestOrchestration.IsReleaseBranch
            ? "CN=SonarSource SA, O=SonarSource SA, L=Vernier, S=Genève, C=CH"
            : "CN=\"SonarSource US, Inc.(TEST ONLY)\", O=\"SonarSource US, Inc.\", L=Austin, S=Texas, C=US";
        cms.Certificates.Should().ContainSingle(x => x.Subject == expectedSubject);    // There's also a Microsoft/DigiCert CA certificate present
    }

    private static MemoryStream ReadStream(ZipArchiveEntry entry)
    {
        var contentStream = new MemoryStream();
        using var entryStream = entry.Open();
        entryStream.CopyTo(contentStream);
        contentStream.Position = 0;
        return contentStream;
    }
}
