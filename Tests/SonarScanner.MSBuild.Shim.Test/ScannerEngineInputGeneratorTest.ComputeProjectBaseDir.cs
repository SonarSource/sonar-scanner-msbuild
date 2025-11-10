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

namespace SonarScanner.MSBuild.Shim.Test;

public partial class ScannerEngineInputGeneratorTest
{
    [TestMethod]
    public void ComputeProjectBaseDir_BestCommonRoot_AllInRoot_NoWarning()
    {
        var sut = new ScannerEngineInputGenerator(new(), cmdLineArgs, runtime);
        var projectPaths = new[]
        {
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Lib")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Src")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Test")),
        };

        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name"));
        runtime.Logger.Should().HaveNoWarnings()
            .And.HaveInfoOnce(ProjectBaseDirInfoMessage);
    }

    // On Unix, there always is a best common root "/" and there are never projects outside of the root.
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_BestCommonRoot_ProjectOutsideRoot_LogsWarning()
    {
        var sut = new ScannerEngineInputGenerator(new(), cmdLineArgs, runtime);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\Projects\Name\Src"),
            new DirectoryInfo(@"C:\Projects\Name\Test"),
            new DirectoryInfo(@"D:\OutsideRoot"),
            new DirectoryInfo(@"E:\AlsoOutside"),
        };

        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(@"C:\Projects\Name");
        runtime.Logger.Should()
            .HaveWarnings(
                @"Directory 'D:\OutsideRoot' is not located under the base directory 'C:\Projects\Name' and will not be analyzed.",
                @"Directory 'E:\AlsoOutside' is not located under the base directory 'C:\Projects\Name' and will not be analyzed.")
            .And.HaveInfoOnce(ProjectBaseDirInfoMessage);
    }

    // On Linux there always is a best common root "/".
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_NoBestCommonRoot_ReturnsNull()
    {
        var sut = new ScannerEngineInputGenerator(new AnalysisConfig(), cmdLineArgs, runtime);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\RootOnce"),
            new DirectoryInfo(@"D:\AlsoOnce"),
            new DirectoryInfo(@"E:\NotHelping"),
        };
        sut.ComputeProjectBaseDir(projectPaths).Should().BeNull();

        runtime.Logger.Should().HaveNoErrors()
            .And.HaveNoWarnings()
            .And.HaveInfoOnce(ProjectBaseDirInfoMessage);
    }

    [TestMethod]
    public void ComputeProjectBaseDir_WorkingDirectory_AllFilesInWorkingDirectory()
    {
        var sut = new ScannerEngineInputGenerator(new AnalysisConfig { SonarScannerWorkingDirectory = Path.Combine(TestUtils.DriveRoot(), "Projects") }, cmdLineArgs, runtime);
        var projectPaths = new[]
        {
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Lib")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Src")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Test")),
        };
        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(Path.Combine(TestUtils.DriveRoot(), "Projects"));

        runtime.Logger.Should().HaveNoWarnings()
            .And.HaveDebugs($"Using working directory as project base directory: '{Path.Combine(TestUtils.DriveRoot(), "Projects")}'.")
            .And.HaveDebugs(1)
            .And.HaveInfoOnce(ProjectBaseDirInfoMessage);
    }

    [TestMethod]
    public void ComputeProjectBaseDir_WorkingDirectory_FilesOutsideWorkingDirectory_FallsBackToCommonPath()
    {
        var sut = new ScannerEngineInputGenerator(new AnalysisConfig { SonarScannerWorkingDirectory = Path.Combine(TestUtils.DriveRoot(), "Solution", "Net") }, cmdLineArgs, runtime);
        var projectPaths = new[]
        {
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Solution", "Net", "Name", "Lib")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Solution", "Net", "Name", "Src")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Solution", "JS")), // At least one directory is not below SonarScannerWorkingDirectory. We fall back to the common root logic.
        };
        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(Path.Combine(TestUtils.DriveRoot(), "Solution"));

        runtime.Logger.Should().HaveNoWarnings()
            .And.HaveInfoOnce(ProjectBaseDirInfoMessage)
            .And.HaveDebugs(1).Which.Single().Should()
            .BeIgnoringLineEndings(
                $"""
                Using longest common projects path as a base directory: '{Path.Combine(TestUtils.DriveRoot(), "Solution")}'. Identified project paths:
                {Path.Combine(TestUtils.DriveRoot(), "Solution", "Net", "Name", "Lib")}
                {Path.Combine(TestUtils.DriveRoot(), "Solution", "Net", "Name", "Src")}
                {Path.Combine(TestUtils.DriveRoot(), "Solution", "JS")}
                """);
    }

    // On Unix, there always is a best common root "/".
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_BestCommonRoot_CaseSensitive_NoRoot_ReturnsNull()
    {
        var additionalFileService = Substitute.For<AdditionalFilesService>(runtime);
        runtime.ConfigureOS(PlatformOS.Linux);
        var sut = new ScannerEngineInputGenerator(new() { SonarOutputDir = @"C:\fallback" }, runtime, new RoslynV1SarifFixer(runtime.Logger), cmdLineArgs, additionalFileService);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\Projects\Name\Lib"),
            new DirectoryInfo(@"c:\projects\name\Test"),
        };
        sut.ComputeProjectBaseDir(projectPaths).Should().BeNull();

        runtime.Logger.Should().HaveNoWarnings()
            .And.HaveNoErrors()
            .And.HaveInfoOnce(ProjectBaseDirInfoMessage);
    }

    // Case sensitive tests don't apply to Unix.
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_BestCommonRoot_CaseInsensitive()
    {
        var additionalFileService = Substitute.For<AdditionalFilesService>(runtime);
        runtime.ConfigureOS(PlatformOS.Windows);
        var sut = new ScannerEngineInputGenerator(new(), runtime, new RoslynV1SarifFixer(runtime.Logger), cmdLineArgs, additionalFileService);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\Projects\Name\Lib"),
            new DirectoryInfo(@"c:\projects\name\Test"),
        };
        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(@"C:\Projects\Name");

        runtime.Logger.Should().HaveNoWarnings()
            .And.HaveInfoOnce(ProjectBaseDirInfoMessage);
    }

    // On Unix, there always is a best common root "/".
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_WorkingDirectory_FilesOutsideWorkingDirectory_NoCommonRoot()
    {
        var sut = new ScannerEngineInputGenerator(new AnalysisConfig { SonarScannerWorkingDirectory = @"C:\Solution" }, cmdLineArgs, runtime);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\Solution\Net\Name\Lib"),
            new DirectoryInfo(@"C:\Solution\Net\Name\Src"),
            new DirectoryInfo(@"D:\SomewhereElse"), // At least one directory is not below SonarScannerWorkingDirectory. We fall back to the common root logic.
        };
        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(@"C:\Solution\Net\Name");

        runtime.Logger.Should().HaveWarnings(@"Directory 'D:\SomewhereElse' is not located under the base directory 'C:\Solution\Net\Name' and will not be analyzed.")
            .And.HaveWarnings(1)
            .And.HaveDebugs("""
                Using longest common projects path as a base directory: 'C:\Solution\Net\Name'. Identified project paths:
                C:\Solution\Net\Name\Lib
                C:\Solution\Net\Name\Src
                D:\SomewhereElse
                """
                    .ToUnixLineEndings())
            .And.HaveDebugs(1)
            .And.HaveInfoOnce(ProjectBaseDirInfoMessage);
    }

    [TestMethod] // the priority is local > scannerEnv > server.
    [DataRow("local", null, null, "local")]
    [DataRow("local", "scannerEnv", null, "local")]
    [DataRow("local", null, "server", "local")]
    [DataRow("local", "scannerEnv", "server", "local")]
    [DataRow(null, "scannerEnv", null, "scannerEnv")]
    [DataRow(null, "scannerEnv", "server", "scannerEnv")]
    [DataRow(null, null, "server", "server")]
    public void ComputeProjectBaseDir_SetFromMultipleSources(string local, string scannerEnv, string server, string expected)
    {
        var projectBaseDirKey = "sonar.projectBaseDir";
        using var scope = new EnvironmentVariableScope();
        var config = new AnalysisConfig { LocalSettings = [], ServerSettings = [] };
        if (local is not null)
        {
            config.LocalSettings.Add(new(projectBaseDirKey, local));
        }
        if (server is not null)
        {
            config.ServerSettings.Add(new(projectBaseDirKey, server));
        }
        if (scannerEnv is not null)
        {
            scope.SetVariable("SONARQUBE_SCANNER_PARAMS", $$"""{"{{projectBaseDirKey}}": "{{scannerEnv}}"}""");
        }

        new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).ComputeProjectBaseDir([]).Name.Should().Be(expected);
        runtime.Logger.DebugMessages.Should().ContainSingle(x => x.StartsWith("Using user supplied project base directory:"));
    }

    private string ComputeProjectBaseDir(string teamBuildValue, string userValue, string[] projectPaths)
    {
        var config = new AnalysisConfig
        {
            SonarOutputDir = TestSonarqubeOutputDir,
            SourcesDirectory = teamBuildValue,
            LocalSettings = [new(SonarProperties.ProjectBaseDir, userValue)],
        };
        return new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).ComputeProjectBaseDir(projectPaths.Select(x => new DirectoryInfo(x)).ToList())?.FullName;
    }

    private void VerifyProjectBaseDir(string expectedValue, string teamBuildValue, string userValue, string[] projectPaths) =>
        ComputeProjectBaseDir(teamBuildValue, userValue, projectPaths).Should().Be(expectedValue);
}
