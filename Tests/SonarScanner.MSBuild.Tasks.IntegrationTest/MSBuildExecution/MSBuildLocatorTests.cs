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

namespace SonarScanner.MSBuild.Tasks.IntegrationTest;

[TestClass]
public class MSBuildLocatorTests
{
    private const string MsBuildPathEnvVar = "MSBUILD_PATH";

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void GetMSBuildPath_MsBuildPathEnvVarSet_ReturnsItDirectly()
    {
        // The CI image sets MSBUILD_PATH to the newest Visual Studio, which lets us bypass the unspecified order of
        // ISetupConfiguration.EnumInstances() (that order changes between image builds and can select an old MSBuild).
        var original = Environment.GetEnvironmentVariable(MsBuildPathEnvVar);
        try
        {
            var expected = @"C:\any\path\to\msbuild.exe";
            Environment.SetEnvironmentVariable(MsBuildPathEnvVar, expected);
            MSBuildLocator.GetMSBuildPath(TestContext).Should().Be(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable(MsBuildPathEnvVar, original);
        }
    }

    [TestMethod]
    public void SelectNewest_NoCandidates_ReturnsNull() =>
        MSBuildLocator.SelectNewest([]).Should().BeNull();

    [TestMethod]
    public void SelectNewest_SingleCandidate_ReturnsIt() =>
        MSBuildLocator.SelectNewest([(new Version("16.11.35026.282"), @"C:\VS2019\msbuild.exe")])
            .Should().Be(@"C:\VS2019\msbuild.exe");

    [TestMethod]
    public void SelectNewest_PicksHighestVersion_RegardlessOfOrder()
    {
        (Version version, string exePath)[] candidates =
        [
            (new Version("16.11.35026.282"), @"C:\VS2019\msbuild.exe"),
            (new Version("18.5.11723.231"), @"C:\VS2026\msbuild.exe"),
            (new Version("17.14.36121.58"), @"C:\VS2022\msbuild.exe"),
        ];

        MSBuildLocator.SelectNewest(candidates).Should().Be(@"C:\VS2026\msbuild.exe");
    }
}
