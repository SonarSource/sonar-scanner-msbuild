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
using System.Text;
using FluentAssertions;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor.Unpacking;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test.JreCaching;

[TestClass]
public class TarGzUnpackTests
{
    private readonly TestLogger logger = new();
    private readonly IFileWrapper fileWrapper = Substitute.For<IFileWrapper>();
    private readonly IDirectoryWrapper directoryWrapper = Substitute.For<IDirectoryWrapper>();
    private readonly IFilePermissionsWrapper filePermissionsWrapper = Substitute.For<IFilePermissionsWrapper>();

    [TestMethod]
    public void TarGzUnpacking_Success_CopyFilePermissions_Fails()
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
        using var archive = new MemoryStream(Convert.FromBase64String(sampleTarGzFile));
        using var unzipped = new MemoryStream();
        fileWrapper.Create($"""{baseDirectory}\Main\Sub2\Sample.txt""").Returns(unzipped);
        filePermissionsWrapper.When(x => x.Copy(Arg.Any<TarEntry>(), Arg.Any<string>())).Throw(new Exception("Sample exception message"));

        CreateUnpacker().Unpack(archive, baseDirectory);

        directoryWrapper.Received(1).CreateDirectory($"""{baseDirectory}\Main\""");
        directoryWrapper.Received(1).CreateDirectory($"""{baseDirectory}\Main\Sub\""");
        directoryWrapper.Received(1).CreateDirectory($"""{baseDirectory}\Main\Sub2\""");
        Encoding.UTF8.GetString(unzipped.ToArray()).NormalizeLineEndings().Should().Be("hey beautiful");
        logger.AssertSingleDebugMessageExists($"""There was an error when trying to set permissions for '{baseDirectory}\Main\Sub2\Sample.txt'. Sample exception message""");
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
        using var unzipped = new MemoryStream();
        fileWrapper.Create($"""{baseDirectory}\ sample.txt""").Returns(unzipped);
        using var archive = new MemoryStream(Convert.FromBase64String(zipWithRootedPath));

        CreateUnpacker().Unpack(archive, baseDirectory);

        directoryWrapper.Received(1).CreateDirectory(baseDirectory);
        Encoding.UTF8.GetString(unzipped.ToArray()).NormalizeLineEndings().Should().Be("hello Costin");
    }

    [TestMethod]
    public void TarGzUnpacking_Fails_InvalidZipFile()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var archive = new MemoryStream([1, 2, 3]); // Invalid archive content
        var sut = CreateUnpacker();

        var action = () => sut.Unpack(archive, baseDirectory);

        action.Should().Throw<Exception>().WithMessage("Error GZIP header, first magic byte doesn't match");
        directoryWrapper.Received(0).CreateDirectory(Arg.Any<string>());
        fileWrapper.Received(0).Create(Arg.Any<string>());
    }

    [TestMethod]
    public void TarGzUnpacking_ZipSlip_IsDetected()
    {
        // slip.tar.gz from https://github.com/kevva/decompress/issues/71
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
        var sut = CreateUnpacker();

        var action = () => sut.Unpack(zipStream, baseDirectory);

        action.Should().Throw<InvalidNameException>().WithMessage("Parent traversal in paths is not allowed");
    }

    private TarGzUnpacker CreateUnpacker() =>
        new(logger, directoryWrapper, fileWrapper, filePermissionsWrapper);
}
