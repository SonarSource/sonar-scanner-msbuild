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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.JreCaching;

namespace SonarScanner.MSBuild.PreProcessor.Test.JreCaching;

[TestClass]
public class ZipUnpackTests
{
    [TestMethod]
    public void ZipFileUnpacking_Success()
    {
        // A sample zip file with the following content:
        // Main
        //  ├── Sub1
        //  └── Sub2
        //      └── Sample.txt
        const string sampleZipFile = """
            UEsDBBQAAAAAAPGQ41gAAAAAAAAAAAAAAAAFAAAATWFpbi9QSwMEFAAAAAAA7JDjWAAAAAAAAAAAAAAAAAoAAABNYWluL1N1YjEvUEsD
            BBQAAAAAAPiQ41gAAAAAAAAAAAAAAAAKAAAATWFpbi9TdWIyL1BLAwQUAAAACAASkeNY1NV/TYcAAAC0AAAAFAAAAE1haW4vU3ViMi9T
            YW1wbGUudHh0JY1BDsIwDATvSPxhX1D+QMURJNR+wE0MDXJtFCdC/T1pe1ytZmacGYMp5SGQKme8LKN73EYkR2ln5mDLwho54kcrikGo
            aphBB/isE6NB59M+e7EaQUqyelNsul6YFL1Fxjfbh0Pxi5vUkkwd1ZO+cR+uNUk8RNGKcsEWJm0yb61pu1vdpPsDUEsBAj8AFAAAAAAA
            8ZDjWAAAAAAAAAAAAAAAAAUAJAAAAAAAAAAQAAAAAAAAAE1haW4vCgAgAAAAAAABABgAzBR7HGPN2gHMFHscY83aAdlY2BJjzdoBUEsB
            Aj8AFAAAAAAA7JDjWAAAAAAAAAAAAAAAAAoAJAAAAAAAAAAQAAAAIwAAAE1haW4vU3ViMS8KACAAAAAAAAEAGAAdYWYXY83aAR1hZhdj
            zdoBHWFmF2PN2gFQSwECPwAUAAAAAAD4kONYAAAAAAAAAAAAAAAACgAkAAAAAAAAABAAAABLAAAATWFpbi9TdWIyLwoAIAAAAAAAAQAY
            AOchsyVjzdoB5yGzJWPN2gEN//UaY83aAVBLAQI/ABQAAAAIABKR41jU1X9NhwAAALQAAAAUACQAAAAAAAAAIAAAAHMAAABNYWluL1N1
            YjIvU2FtcGxlLnR4dAoAIAAAAAAAAQAYAM5kOEJjzdoB98Y4QmPN2gHOCFYiY83aAVBLBQYAAAAABAAEAHUBAAAsAQAAAAA=
            """;
        const string baseDirectory = @"C:\User\user\.sonar\cache\sha265\JRE_extracted";
        using var zipStream = new MemoryStream(Convert.FromBase64String(sampleZipFile));
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        using var unzipped = new MemoryStream();
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Create($@"{baseDirectory}\Main/Sub2/Sample.txt").Returns(unzipped);
        var sut = new ZipUnpack(directoryWrapper, fileWrapper);
        sut.Unpack(zipStream, baseDirectory);
        var content = Encoding.UTF8.GetString(unzipped.ToArray()).NormalizeLineEndings();
        content.Should().Be("""
            The SonarScanner for .NET is the recommended way to launch a SonarQube or 
            SonarCloud analysis for Clean Code projects/solutions using MSBuild or 
            dotnet command as a build tool.
            """.NormalizeLineEndings());
        directoryWrapper.Received(1).CreateDirectory($@"{baseDirectory}");
        directoryWrapper.Received(1).CreateDirectory($@"{baseDirectory}\Main/");
        directoryWrapper.Received(1).CreateDirectory($@"{baseDirectory}\Main/Sub1/");
        directoryWrapper.Received(1).CreateDirectory($@"{baseDirectory}\Main/Sub2/");
        fileWrapper.Received(1).Create($@"{baseDirectory}\Main/Sub2/Sample.txt");
    }

    [TestMethod]
    public void ZipFileUnpacking_Fails_InvalidZipFile()
    {
        const string baseDirectory = @"C:\User\user\.sonar\cache\sha265\JRE_extracted";
        using var zipStream = new MemoryStream(); // Invalid zip file content
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        var fileWrapper = Substitute.For<IFileWrapper>();
        var sut = new ZipUnpack(directoryWrapper, fileWrapper);
        var action = () => sut.Unpack(zipStream, baseDirectory);
        action.Should().Throw<InvalidDataException>().WithMessage("Central Directory corrupt.")
            .WithInnerException<IOException>().WithMessage("An attempt was made to move the position before the beginning of the stream.");
        directoryWrapper.Received(1).CreateDirectory($@"{baseDirectory}");
    }

    [TestMethod]
    public void ZipSlip()
    {
        // zip-slip.zip from https://github.com/mssalvatore/CVE-2019-14751_PoC/tree/master
        const string zipSlip = """
            UEsDBAoAAAAAAAd/Ak8AAAAAAAAAAAAAAAAGABwAZmlsZXMvVVQJAANdlURdZZVEXXV4CwABBOgDAAAE6AMAAFBLAwQKAAAAAAAHfwJPfS/nlRUAAAAVAAAA
            LQAcAGZpbGVzLy4uLy4uLy4uLy4uLy4uLy4uLy4uLy4uLy4uL3RtcC9ldmlsLnR4dFVUCQADXZVEXV2VRF11eAsAAQToAwAABOgDAABUaGlzIGlzIGFuIGV2
            aWwgZmlsZQpQSwMECgAAAAAA934CTxMK+swVAAAAFQAAAA4AHABmaWxlcy9nb29kLnR4dFVUCQADQZVEXUGVRF11eAsAAQToAwAABOgDAABUaGlzIGlzIGEg
            Z29vZCBmaWxlLgpQSwECHgMKAAAAAAAHfwJPAAAAAAAAAAAAAAAABgAYAAAAAAAAABAA7UEAAAAAZmlsZXMvVVQFAANdlURddXgLAAEE6AMAAAToAwAAUEsB
            Ah4DCgAAAAAAB38CT30v55UVAAAAFQAAAC0AGAAAAAAAAQAAAKSBQAAAAGZpbGVzLy4uLy4uLy4uLy4uLy4uLy4uLy4uLy4uLy4uL3RtcC9ldmlsLnR4dFVU
            BQADXZVEXXV4CwABBOgDAAAE6AMAAFBLAQIeAwoAAAAAAPd+Ak8TCvrMFQAAABUAAAAOABgAAAAAAAEAAACkgbwAAABmaWxlcy9nb29kLnR4dFVUBQADQZVE
            XXV4CwABBOgDAAAE6AMAAFBLBQYAAAAAAwADABMBAAAZAQAAAAA=
            """;
        const string baseDirectory = @"C:\User\user\.sonar\cache\sha265\JRE_extracted";
        using var zipStream = new MemoryStream(Convert.FromBase64String(zipSlip));
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        using var evil = new MemoryStream();
        using var good = new MemoryStream();
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Create($@"{baseDirectory}\files/../../../../../../../../../tmp/evil.txt").Returns(evil);
        fileWrapper.Create($@"{baseDirectory}\files/good.txt").Returns(good);
        var sut = new ZipUnpack(directoryWrapper, fileWrapper);
        sut.Unpack(zipStream, baseDirectory);
        Encoding.UTF8.GetString(evil.ToArray()).NormalizeLineEndings().Should().Be("""
            This is an evil file

            """.NormalizeLineEndings());
        Encoding.UTF8.GetString(good.ToArray()).NormalizeLineEndings().Should().Be("""
            This is a good file.

            """.NormalizeLineEndings());
        directoryWrapper.Received(1).CreateDirectory($@"{baseDirectory}");
        directoryWrapper.Received(1).CreateDirectory($@"{baseDirectory}\files/");
        directoryWrapper.Received(1).CreateDirectory($@"{baseDirectory}\files\..\..\..\..\..\..\..\..\..\tmp");
        directoryWrapper.Received(1).CreateDirectory($@"{baseDirectory}\files");
        fileWrapper.Received(1).Create($@"{baseDirectory}\files/../../../../../../../../../tmp/evil.txt");
        fileWrapper.Received(1).Create($@"{baseDirectory}\files/good.txt");
    }
}
