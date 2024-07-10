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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.PreProcessor.JreCaching;
using TestUtilities;

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
        var baseDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var main = Path.Combine(baseDirectory, "Main");
        var sub1 = Path.Combine(baseDirectory, "Main", "Sub1");
        var sub2 = Path.Combine(baseDirectory, "Main", "Sub2");
        var sampleTxt = Path.Combine(baseDirectory, "Main", "Sub2", "Sample.txt");
        try
        {
            using var zipStream = new MemoryStream(Convert.FromBase64String(sampleZipFile));
            var sut = new ZipUnpacker();
            sut.Unpack(zipStream, baseDirectory);
            Directory.Exists(main).Should().BeTrue();
            Directory.Exists(sub1).Should().BeTrue();
            Directory.Exists(sub2).Should().BeTrue();
            File.Exists(sampleTxt).Should().BeTrue();
            var content = File.ReadAllText(sampleTxt).NormalizeLineEndings();
            content.Should().Be("""
            The SonarScanner for .NET is the recommended way to launch a SonarQube or 
            SonarCloud analysis for Clean Code projects/solutions using MSBuild or 
            dotnet command as a build tool.
            """.NormalizeLineEndings());
        }
        finally
        {
            Directory.Delete(baseDirectory, true);
        }
    }

    [TestMethod]
    public void ZipFileUnpacking_Fails_InvalidZipFile()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var zipStream = new MemoryStream(); // Invalid zip file content
        var sut = new ZipUnpacker();
        var action = () => sut.Unpack(zipStream, baseDirectory);
        action.Should().Throw<InvalidDataException>().WithMessage("Central Directory corrupt.")
            .WithInnerException<IOException>().WithMessage("An attempt was made to move the position before the beginning of the stream.");
        Directory.Exists(baseDirectory).Should().BeFalse();
    }

    [TestMethod]
    public void ZipFileUnpacking_ZipSlip_IsDetected()
    {
        // zip-slip.zip from https://github.com/mssalvatore/CVE-2019-14751_PoC/tree/master
        // google "Zip Slip Vulnerability" for details
        const string zipSlip = """
            UEsDBAoAAAAAAAd/Ak8AAAAAAAAAAAAAAAAGABwAZmlsZXMvVVQJAANdlURdZZVEXXV4CwABBOgDAAAE6AMAAFBLAwQKAAAAAAAHfwJPfS/nlRUAAAAVAAAA
            LQAcAGZpbGVzLy4uLy4uLy4uLy4uLy4uLy4uLy4uLy4uLy4uL3RtcC9ldmlsLnR4dFVUCQADXZVEXV2VRF11eAsAAQToAwAABOgDAABUaGlzIGlzIGFuIGV2
            aWwgZmlsZQpQSwMECgAAAAAA934CTxMK+swVAAAAFQAAAA4AHABmaWxlcy9nb29kLnR4dFVUCQADQZVEXUGVRF11eAsAAQToAwAABOgDAABUaGlzIGlzIGEg
            Z29vZCBmaWxlLgpQSwECHgMKAAAAAAAHfwJPAAAAAAAAAAAAAAAABgAYAAAAAAAAABAA7UEAAAAAZmlsZXMvVVQFAANdlURddXgLAAEE6AMAAAToAwAAUEsB
            Ah4DCgAAAAAAB38CT30v55UVAAAAFQAAAC0AGAAAAAAAAQAAAKSBQAAAAGZpbGVzLy4uLy4uLy4uLy4uLy4uLy4uLy4uLy4uLy4uL3RtcC9ldmlsLnR4dFVU
            BQADXZVEXXV4CwABBOgDAAAE6AMAAFBLAQIeAwoAAAAAAPd+Ak8TCvrMFQAAABUAAAAOABgAAAAAAAEAAACkgbwAAABmaWxlcy9nb29kLnR4dFVUBQADQZVE
            XXV4CwABBOgDAAAE6AMAAFBLBQYAAAAAAwADABMBAAAZAQAAAAA=
            """;
        var baseDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var zipStream = new MemoryStream(Convert.FromBase64String(zipSlip));
        var sut = new ZipUnpacker();
        try
        {
            var action = () => sut.Unpack(zipStream, baseDirectory);
            action.Should().Throw<IOException>().WithMessage("Extracting Zip entry would have resulted in a file outside the specified destination directory.");
        }
        finally
        {
            Directory.Delete(baseDirectory, true);
        }
    }
}
