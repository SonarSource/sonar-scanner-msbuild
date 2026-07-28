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

namespace SonarScanner.MSBuild.PackagingTest;

[TestClass]
public class NuGetTest
{
    [TestInitialize]
    public void Initialize() =>
        TestOrchestration.InitializeTestClass();

    [TestMethod]
    public void ValidateFileList() =>
        Verifier.UnzippedFileList("NuGet", "dotnet-sonarscanner.*.nupkg").Where(x => !x.StartsWith("package/services/metadata/core-properties/")).Should().BeEquivalentTo(
            NuGetSignatureFiles().Concat([
                "dotnet-sonarscanner.nuspec",
                "[Content_Types].xml",
                "_rels/.rels",
                "docs/README.md",
                "licenses/LICENSE.txt",
                "licenses/THIRD_PARTY_LICENSES/Google.Protobuf-LICENSE.txt",
                "licenses/THIRD_PARTY_LICENSES/Microsoft.CodeCoverage.IO-LICENSE.txt",
                "licenses/THIRD_PARTY_LICENSES/Newtonsoft.Json-LICENSE.txt",
                "licenses/THIRD_PARTY_LICENSES/SharpZipLib-LICENSE.txt",
                "tools/netcoreapp3.1/any/DotnetToolSettings.xml",
                "tools/netcoreapp3.1/any/Google.Protobuf.dll",
                "tools/netcoreapp3.1/any/ICSharpCode.SharpZipLib.dll",
                "tools/netcoreapp3.1/any/Microsoft.CodeCoverage.IO.dll",
                "tools/netcoreapp3.1/any/Newtonsoft.Json.dll",
                "tools/netcoreapp3.1/any/SonarQube.Analysis.xml",
                "tools/netcoreapp3.1/any/SonarScanner.MSBuild.Common.dll",
                "tools/netcoreapp3.1/any/SonarScanner.MSBuild.dll",
                "tools/netcoreapp3.1/any/SonarScanner.MSBuild.PostProcessor.dll",
                "tools/netcoreapp3.1/any/SonarScanner.MSBuild.PreProcessor.dll",
                "tools/netcoreapp3.1/any/SonarScanner.MSBuild.Shim.dll",
                "tools/netcoreapp3.1/any/SonarScanner.MSBuild.TFS.dll",
                "tools/netcoreapp3.1/any/SonarScanner.MSBuild.Tasks.dll",
                "tools/netcoreapp3.1/any/SonarScanner.MSBuild.runtimeconfig.json",
                "tools/netcoreapp3.1/any/Targets/SonarQube.Integration.ImportBefore.targets",
                "tools/netcoreapp3.1/any/Targets/SonarQube.Integration.targets",
            ]));

    [TestMethod]
    public void ValidateSignatures()
    {
        TestOrchestration.RunOnlyOnReleaseBranch();
        using var archive = Verifier.UnzipFile("NuGet", "dotnet-sonarscanner.*.nupkg");
        var dlls = archive.Entries.Where(Verifier.IsSonarBinary).ToArray();
        dlls.Should().HaveCount(7);
        foreach (var dll in dlls)
        {
            Verifier.ValidateSignature(dll);
        }
    }

    private static string[] NuGetSignatureFiles() =>
        TestOrchestration.IsReleaseBranch ? [".signature.p7s"] : [];
}
