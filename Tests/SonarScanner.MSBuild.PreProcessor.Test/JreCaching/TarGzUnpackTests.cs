/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using FluentAssertions;
using ICSharpCode.SharpZipLib.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.JreCaching;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test.JreCaching;

[TestClass]
public class TarGzUnpackTests
{
    [TestMethod]
    public void TarGzUnpacking_Success()
    {
        // A tarball with the following content:
        // Main
        //  ├── Sub
        //  └── Sub2
        //      └── Sample.txt
        const string sampleTarGzFile = """
            H4sICL04jWYEAE1haW4udGFyAO3SUQrDIAyA4RzFE2wao55iTz2BBccK3Ribw
            nb7iVDKnkqh+mK+l4S8/rn46XGGumTmnMuz+JvLrsiSRk1ImO/WkgRhoIH0jv
            4lBHSq9B/SWPMHdvVHk+9OO+T+LSz9seID7OpPpT8Zy/1bWPsP/v6cwyl+Ihx
            ss78ya3+T70qR1iAkNNB5/1v4ijH4FKdrmoExxlgvfmqGu7oADgAA
            """;
        var baseDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var main = Path.Combine(baseDirectory, "Main");
        var sub1 = Path.Combine(baseDirectory, "Main", "Sub");
        var sub2 = Path.Combine(baseDirectory, "Main", "Sub2");
        var sampleTxt = Path.Combine(baseDirectory, "Main", "Sub2", "Sample.txt");
        var osProvider = Substitute.For<IOperatingSystemProvider>();
        osProvider.OperatingSystem().Returns(PlatformOS.MacOSX);
        using var archive = new MemoryStream(Convert.FromBase64String(sampleTarGzFile));
        var sut = new TarGzUnpacker(DirectoryWrapper.Instance, FileWrapper.Instance, osProvider);
        try
        {
            sut.Unpack(archive, baseDirectory);

            Directory.Exists(main).Should().BeTrue();
            Directory.Exists(sub1).Should().BeTrue();
            Directory.Exists(sub2).Should().BeTrue();
            File.Exists(sampleTxt).Should().BeTrue();
            var content = File.ReadAllText(sampleTxt).NormalizeLineEndings();
            content.Should().Be("hey beautiful");
        }
        finally
        {
            Directory.Delete(baseDirectory, true);
        }
    }

    [TestMethod]
    public void TarGzUnpacking_RootedPath_Success()
    {
        // A tarball with a single file with a rooted path: "\ sample.txt"
        const string zipWithRootedPath = """
            H4sIAAAAAAAAA+3OMQ7CMBBE0T3KngCtsY0PwDVoUlghkiEoNhLHB
            5QmFdBEEdJ/zRQzxZy0dpdbybv2aLISe0kpvdOlaMucuSAuHIKPto
            /eizmXfBS1tQ4t3WvrJlXpp9x/2n3r/9Q5lzLqcaxtuG79BQAAAAA
            AAAAAAAAAAADwuyfh1ptHACgAAA==
            """;
        var baseDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var sampleTxt = Path.Combine(baseDirectory, " sample.txt");
        var osProvider = Substitute.For<IOperatingSystemProvider>();
        osProvider.OperatingSystem().Returns(PlatformOS.MacOSX);
        using var archive = new MemoryStream(Convert.FromBase64String(zipWithRootedPath));
        var sut = new TarGzUnpacker(DirectoryWrapper.Instance, FileWrapper.Instance, osProvider);
        try
        {
            sut.Unpack(archive, baseDirectory);

            File.Exists(sampleTxt).Should().BeTrue();
            var content = File.ReadAllText(sampleTxt).NormalizeLineEndings();
            content.Should().Be("hello Costin");
        }
        finally
        {
            Directory.Delete(baseDirectory, true);
        }

    }

    [TestMethod]
    public void TarGzUnpacking_Fails_InvalidZipFile()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var archive = new MemoryStream([1, 2, 3]); // Invalid archive content
        var sut = new TarGzUnpacker(DirectoryWrapper.Instance, FileWrapper.Instance, Substitute.For<IOperatingSystemProvider>());

        var action = () => sut.Unpack(archive, baseDirectory);

        action.Should().Throw<Exception>().WithMessage("Error GZIP header, first magic byte doesn't match");
        Directory.Exists(baseDirectory).Should().BeFalse();
    }

    [TestMethod]
    public void TarGzUnpacking_ZipSlip_IsDetected()
    {
        // zip-slip.zip from https://github.com/kevva/decompress/issues/71
        // google "Zip Slip Vulnerability" for details
        const string zipSlip = """
            H4sICJDill0C/215LXNsaXAudGFyAO3TvQrCMBSG4cxeRa4gTdKk
            XRUULHQo2MlNUET8K7aC9OrFFsTFn0ELlffhwDmcZEngU4EKhunx
            sE43h634Dd161rWL3X1u9sZYa4VMRQfOZbU4Sfn1R/aEUgH1YVX7
            Iih3m6JYLVV1qcQ/6OLnbnmIoibjJvb6sbesESb0znsfGh8Kba1z
            XkjdZf6Pdb1bvbj37ryn+Z8nmcyno1zO0iTLJuOBAAAAAAAAAAAA
            AAAAQJ9cAZCup/MAKAAA
            """;
        var baseDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var zipStream = new MemoryStream(Convert.FromBase64String(zipSlip));
        var sut = new TarGzUnpacker(DirectoryWrapper.Instance, FileWrapper.Instance, Substitute.For<IOperatingSystemProvider>());

        var action = () => sut.Unpack(zipStream, baseDirectory);

        action.Should().Throw<InvalidNameException>().WithMessage("Parent traversal in paths is not allowed");
    }
}
