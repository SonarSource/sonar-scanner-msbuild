/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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
using System.Text;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class ChecksumSha256Tests
{
    [TestMethod]
    // Source https://www.dlitz.net/crypto/shad256-test-vectors/
    [DataRow("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [DataRow("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public void Sha256TestVectors(string ascii, string hash)
    {
        var sut = new ChecksumSha256();
        var asciiBytes = Encoding.ASCII.GetBytes(ascii);
        sut.ComputeHash(new MemoryStream(asciiBytes)).Should().Be(hash);
    }

    [TestMethod]
    [Ignore("For local testing. This test can be used to check a file downloaded from the server with the sha256 value specified by the server.")]
    public void Sha256Download()
    {
        // At the time of writing, this was the first file returned by the https://api.sonarcloud.io/analysis/jres endpoint:
        //    "filename": "OpenJDK17U-jre_x64_alpine-linux_hotspot_17.0.11_9.tar.gz",
        //    "sha256": "b5dffd0be08c464d9c3903e2947508c1a5c21804ea1cff5556991a2a47d617d8",
        //    "javaPath": "jdk-17.0.11+9-jre/bin/java",
        //    "os": "alpine",
        //    "arch": "x64",
        //    "downloadUrl": "https://scanner.sonarcloud.io/jres/OpenJDK17U-jre_x64_alpine-linux_hotspot_17.0.11_9.tar.gz"
        var sut = new ChecksumSha256();
        using var stream = new FileStream(@"C:\Users\martin.strecker\Downloads\OpenJDK17U-jre_x64_alpine-linux_hotspot_17.0.11_9.tar.gz", FileMode.Open);
        sut.ComputeHash(stream).Should().Be("b5dffd0be08c464d9c3903e2947508c1a5c21804ea1cff5556991a2a47d617d8");
    }
}
