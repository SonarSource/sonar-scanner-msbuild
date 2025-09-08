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

using ICSharpCode.SharpZipLib.Core;

namespace SonarScanner.MSBuild.PreProcessor.Unpacking.Test;

[TestClass]
public class TarGzUnpackTests
{
    private readonly TestRuntime runtime = new();

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
        var filePath = Path.Combine(baseDirectory, "Main", "Sub2", "Sample.txt");
        using var archive = new MemoryStream(Convert.FromBase64String(sampleTarGzFile));
        using var unzipped = new MemoryStream();
        runtime.File.Create(filePath).Returns(unzipped);
        runtime.OperatingSystem.When(x => x.SetPermission(Arg.Any<string>(), Arg.Any<int>())).Throw(new Exception("Sample exception message"));

        new TarGzUnpacker(runtime).Unpack(archive, baseDirectory);

        runtime.Directory.Received(1).CreateDirectory(Path.Combine(baseDirectory, "Main") + Path.DirectorySeparatorChar);
        runtime.Directory.Received(1).CreateDirectory(Path.Combine(baseDirectory, "Main", "Sub") + Path.DirectorySeparatorChar);
        runtime.Directory.Received(1).CreateDirectory(Path.Combine(baseDirectory, "Main", "Sub2") + Path.DirectorySeparatorChar);
        Encoding.UTF8.GetString(unzipped.ToArray()).ToUnixLineEndings().Should().Be("hey beautiful");
        runtime.Should().HaveSingleDebugLogged($"There was an error when trying to set permissions for '{filePath}'. Sample exception message");
    }

    [TestCategory(TestCategories.NoMacOS)]
    [TestCategory(TestCategories.NoLinux)]
    [TestMethod]
    public void TarGzUnpacking_BackslashRootedPath_Success()
    {
        // A tarball with a single file with a rooted path: "\ sample.txt"
        var zipWithRootedPath = """
            H4sIAAAAAAAAA+3OMQ7CMBBE0T3KngCtsY0PwDVoUlghkiEoNhLHB
            5QmFdBEEdJ/zRQzxZy0dpdbybv2aLISe0kpvdOlaMucuSAuHIKPto
            /eizmXfBS1tQ4t3WvrJlXpp9x/2n3r/9Q5lzLqcaxtuG79BQAAAAA
            AAAAAAAAAAADwuyfh1ptHACgAAA==
            """;

        RootedPath_Success(zipWithRootedPath);
    }

    [TestMethod]
    public void TarGzUnpacking_ForwardSlashRootedPath_Success()
    {
        // A tarball with a single file with a rooted path: "/ sample.txt"
        const string zipWithRootedPath = """
            H4sIAAAAAAAAA+3QQQrCMBBG4ax7ijlBO0lTcwBP0kVBIRppInh8g
            6vioroppfC+zVvM5mc6yePtEae2vIrZiFYn7z+tvqtqB2N9CP3gfO
            +cUWtDjehWg5aeuYyziJlTWn3Ar/tBXaYYk5xTLtd7s/cYAAAAAAA
            AAAAAAAAAAMDf3hbawR8AKAAA
            """;

        RootedPath_Success(zipWithRootedPath);
    }

    [TestMethod]
    public void TarGzUnpacking_Fails_InvalidZipFile()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var archive = new MemoryStream([1, 2, 3]); // Invalid archive content
        var sut = new TarGzUnpacker(runtime);

        var action = () => sut.Unpack(archive, baseDirectory);

        action.Should().Throw<Exception>().WithMessage("Error GZIP header, first magic byte doesn't match");
        runtime.Directory.Received(0).CreateDirectory(Arg.Any<string>());
        runtime.File.Received(0).Create(Arg.Any<string>());
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
        var sut = new TarGzUnpacker(runtime);

        var action = () => sut.Unpack(zipStream, baseDirectory);

        action.Should().Throw<InvalidNameException>().WithMessage("Parent traversal in paths is not allowed");
    }

    private void RootedPath_Success(string base64Archive)
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var unzipped = new MemoryStream();
        runtime.File.Create(Path.Combine(baseDirectory, " sample.txt")).Returns(unzipped);
        using var archive = new MemoryStream(Convert.FromBase64String(base64Archive));

        new TarGzUnpacker(runtime).Unpack(archive, baseDirectory);

        runtime.Directory.Received(1).CreateDirectory(baseDirectory);
        Encoding.UTF8.GetString(unzipped.ToArray()).ToUnixLineEndings().TrimEnd().Should().Be("hello Costin");
    }
}
