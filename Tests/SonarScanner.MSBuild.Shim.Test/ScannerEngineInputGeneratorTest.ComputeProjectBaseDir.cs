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
        var sut = new ScannerEngineInputGenerator(new() { SonarOutputDir = @"C:\fallback" }, runtime, cmdLineArgs, additionalFileService);
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
        var sut = new ScannerEngineInputGenerator(new(), runtime, cmdLineArgs, additionalFileService);
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

    [TestMethod]
    public void ComputeProjectBaseDir_Precedence()
    {
        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("d"), "work", "mysources"), // if there is a user value, use it
            teamBuildValue: Path.Combine(TestUtils.DriveRoot("d"), "work"),
            userValue: Path.Combine(TestUtils.DriveRoot("d"), "work", "mysources"),
            projectPaths: [Path.Combine(TestUtils.DriveRoot("d"), "work", "proj1.csproj")]);

        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("d"), "work"),  // if no user value, use the team build value
            teamBuildValue: Path.Combine(TestUtils.DriveRoot("d"), "work"),
            userValue: null,
            projectPaths: [Path.Combine(TestUtils.DriveRoot("e"), "work")]);

        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("e"), "work"),  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [Path.Combine(TestUtils.DriveRoot("e"), "work")]);

        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("e"), "work"),  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [Path.Combine(TestUtils.DriveRoot("e"), "work"), Path.Combine(TestUtils.DriveRoot("e"), "work")]);

        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("e"), "work"),  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [Path.Combine(TestUtils.DriveRoot("e"), "work", "A"), Path.Combine(TestUtils.DriveRoot("e"), "work", "B", "C")]);

        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("e"), "work"),  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [Path.Combine(TestUtils.DriveRoot("e"), "work", "A"), Path.Combine(TestUtils.DriveRoot("e"), "work", "B"), Path.Combine(TestUtils.DriveRoot("e"), "work", "C")]);

        VerifyProjectBaseDir(
            expectedValue: Path.Combine(TestUtils.DriveRoot("e"), "work", "A"),  // if no team build value, use the common project paths root
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [Path.Combine(TestUtils.DriveRoot("e"), "work", "A", "X"), Path.Combine(TestUtils.DriveRoot("e"), "work", "A"), Path.Combine(TestUtils.DriveRoot("e"), "work", "A")]);

        // Support relative paths
        VerifyProjectBaseDir(
            expectedValue: Path.Combine(Directory.GetCurrentDirectory(), "src"),
            teamBuildValue: null,
            userValue: Path.Combine(".", "src"),
            projectPaths: [@"d:\work\proj1.csproj"]);
    }

    [TestMethod]
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    public void ComputeProjectBaseDir_Windows()
    {
        VerifyProjectBaseDir(
            expectedValue: null,  // if no common root exists, return null
            teamBuildValue: null,
            userValue: string.Empty,
            projectPaths: [@"f:\work\A", @"e:\work\B"]);

        // Support short name paths
        var baseDir = ComputeProjectBaseDir(
            teamBuildValue: null,
            userValue: @"C:\PROGRA~1",
            projectPaths: [@"d:\work\proj1.csproj"]);
        baseDir.Should().BeOneOf(@"C:\Program Files", @"C:\Program Files (x86)");
    }

    [TestMethod]
    [DataRow(@"d:\work", @"d:\work\mysources", new[] { @"d:\work\proj1.csproj" }, false)]
    [DataRow(@"d:\work", null, new[] { @"e:\work" }, false)]
    [DataRow(null, "", new[] { @"e:\work" }, true)]
    [DataRow(null, "", new[] { @"e:\work", @"e:\work" }, true)]
    public void ComputeProjectBaseDir_LogsProjectBaseDirInfo(string teamBuildValue, string userValue, string[] projectPaths, bool shouldLog)
    {
        var config = new AnalysisConfig
        {
            SonarOutputDir = TestSonarqubeOutputDir,
            SourcesDirectory = teamBuildValue,
            LocalSettings = [new(SonarProperties.ProjectBaseDir, userValue)]
        };
        new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).ComputeProjectBaseDir(projectPaths.Select(x => new DirectoryInfo(x)).ToList());

        if (shouldLog)
        {
            runtime.Logger.Should().HaveInfos(ProjectBaseDirInfoMessage);
        }
        else
        {
            runtime.Logger.Should().NotHaveInfo(ProjectBaseDirInfoMessage);
        }
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
