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

using System.Drawing.Drawing2D;
using System.Security.Cryptography;

namespace SonarScanner.MSBuild.PackagingTest;

[TestClass]
public class ChocolateyTest
{
    [TestInitialize]
    public void Initialize() =>
        TestOrchestration.InitializeTestClass();

    [TestMethod]
    public void ValidateFileList_Net() =>
        Verifier.UnzippedFileList("Chocolatey", "sonarscanner-net.*.nupkg").Where(x => !x.StartsWith("package/services/metadata/core-properties/")).Should().BeEquivalentTo([
            "[Content_Types].xml",
            "sonarscanner-net.nuspec",
            "_rels/.rels",
            "tools/chocolateyInstall.ps1",
        ]);

    [TestMethod]
    public void ValidateFileList_NetFramework() =>
        Verifier.UnzippedFileList("Chocolatey", "sonarscanner-net-framework.*.nupkg").Where(x => !x.StartsWith("package/services/metadata/core-properties/")).Should().BeEquivalentTo([
            "[Content_Types].xml",
            "sonarscanner-net-framework.nuspec",
            "_rels/.rels",
            "tools/chocolateyInstall.ps1",
        ]);

    [TestMethod]
    [DataRow("net")]
    [DataRow("net-framework")]
    public void ValidateScriptContent(string framework)
    {
        const string formattingWhitespace = "    "; // This piece of whitespace makes the powershell script nicely formatted, and is hard to assert without const
        using var archive = Verifier.UnzipFile("Chocolatey", $"sonarscanner-{framework}.*.nupkg");
        using var stream = archive.Entries.Should().ContainSingle(x => x.Name == "chocolateyInstall.ps1").Subject.Open();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        var zip = Path.Combine(Paths.BinariesRoot, $"sonar-scanner-{TestOrchestration.FullVersion}-{framework}.zip");
        File.Exists(zip).Should().BeTrue($"source ZIP {zip} should exist to compute SHA");
        var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip)));

        content.Replace("\r\n", "\n").Should().Be($"""
            Install-ChocolateyZipPackage "sonarscanner-{framework}" `
                    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/{TestOrchestration.FullVersion}/sonar-scanner-{TestOrchestration.FullVersion}-{framework}.zip" `
                    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
                    -ChecksumType "sha256" `
                    -Checksum "{sha}"
            {formattingWhitespace}

            """);
    }
}
