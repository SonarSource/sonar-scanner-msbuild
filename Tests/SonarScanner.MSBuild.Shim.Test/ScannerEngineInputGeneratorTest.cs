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

[TestClass]
public partial class ScannerEngineInputGeneratorTest
{
    private const string TestSonarqubeOutputDir = @"e:\.sonarqube\out";

    private const string ProjectBaseDirInfoMessage =
        "Starting with SonarScanner for .NET v8 the way the `sonar.projectBaseDir` property is automatically detected has changed "
        + "and this has an impact on the files that are analyzed and other properties that are resolved relative to it like `sonar.exclusions` and `sonar.test.exclusions`. "
        + "If you would like to customize the behavior, please set the `sonar.projectBaseDir` property to point to a directory that contains all the source code you want to analyze. "
        + "The path may be relative (to the directory from which the analysis was started) or absolute.";

    private readonly TestRuntime runtime = new() { Directory = DirectoryWrapper.Instance };
    private readonly ListPropertiesProvider cmdLineArgs = [];

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_Null_Throws()
    {
        var cnfg = new AnalysisConfig();
        var rntm = runtime;
        var cmds = new ListPropertiesProvider();
        FluentActions.Invoking(() => new ScannerEngineInputGenerator(null, cmds, rntm)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("analysisConfig");
        FluentActions.Invoking(() => new ScannerEngineInputGenerator(cnfg, null, rntm)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("cmdLineArgs");
        FluentActions.Invoking(() => new ScannerEngineInputGenerator(cnfg, cmds, null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("runtime");
        FluentActions.Invoking(() => new ScannerEngineInputGenerator(cnfg, null, null, null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("runtime");
        FluentActions.Invoking(() => new ScannerEngineInputGenerator(cnfg, rntm, null, null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("cmdLineArgs");
        FluentActions.Invoking(() => new ScannerEngineInputGenerator(cnfg, rntm, cmds, null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("additionalFilesService");
    }

    [TestMethod]
    public void SingleClosestProjectOrDefault_WhenNoProjects_ReturnsNull() =>
        ScannerEngineInputGenerator.SingleClosestProjectOrDefault(new FileInfo("File.cs"), []).Should().BeNull();

    [TestMethod]
    public void SingleClosestProjectOrDefault_WhenNoMatch_ReturnsNull()
    {
        var projects = new[]
        {
            CreateProjectData(Path.Combine(TestUtils.DriveRoot("D"), "WrongDrive.csproj")),
            CreateProjectData(Path.Combine("~ProjectDir", "Incorrect.csproj")),
            CreateProjectData(Path.Combine(TestUtils.DriveRoot("C"), "WrongDrive.csproj"))
        };

        ScannerEngineInputGenerator.SingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot("E"), "File.cs")), projects).Should().BeNull();
    }

    [TestMethod]
    public void SingleClosestProjectOrDefault_WhenOnlyOneProjectMatchingWithSameCase_ReturnsProject()
    {
        var projects = new[]
        {
            CreateProjectData("InRoot.csproj"),
            CreateProjectData(Path.Combine("~ProjectDir", "Incorrect.csproj")),
            CreateProjectData(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "Winner.csproj"))
        };

        ScannerEngineInputGenerator.SingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "File.cs")), projects).Should().Be(projects[2]);
    }

    [TestMethod]
    public void SingleClosestProjectOrDefault_WhenOnlyOneProjectMatchingWithDifferentCase_ReturnsProject()
    {
        var projects = new[]
        {
            CreateProjectData("InRoot.csproj"),
            CreateProjectData(Path.Combine("~PROJECTDIR", "Incorrect.csproj")),
            CreateProjectData(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "Winner.csproj"))
        };

        ScannerEngineInputGenerator.SingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot("C"), "PROJECTDIR", "FILE.cs")), projects).Should().Be(projects[2]);
    }

    [TestMethod]
    public void SingleClosestProjectOrDefault_WhenOnlyOneProjectMatchingWithDifferentSeparators_ReturnsProject()
    {
        var projects = new[]
        {
            CreateProjectData("InRoot.csproj"),
            CreateProjectData(Path.Combine("~ProjectDir", "Incorrect.csproj")),
            CreateProjectData($"{TestUtils.DriveRoot()}{Path.AltDirectorySeparatorChar}ProjectDir{Path.AltDirectorySeparatorChar}Winner.csproj")
        };

        ScannerEngineInputGenerator.SingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "File.cs")), projects).Should().Be(projects[2]);
    }

    [TestMethod]
    public void SingleClosestProjectOrDefault_WhenMultipleProjectsMatch_ReturnsProjectWithLongestMatch()
    {
        var projects = new[]
        {
            CreateProjectData(Path.Combine(TestUtils.DriveRoot(), "InRoot.csproj")),
            CreateProjectData(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "InProjectDir.csproj")),
            CreateProjectData(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "SubDir", "Winner.csproj")),
            CreateProjectData(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "AnotherInProjectDir.csproj")),
            CreateProjectData(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "SubDir", "Deeper", "TooDeep.csproj"))
        };

        ScannerEngineInputGenerator.SingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "SubDir", "File.cs")), projects).Should().Be(projects[2]);
    }

    [TestMethod]
    public void SingleClosestProjectOrDefault_WhenMultipleProjectsMatchWithSameLength_ReturnsClosestProject()
    {
        var projects = new[]
        {
            CreateProjectData(Path.Combine(TestUtils.DriveRoot(), "Net46.csproj")),
            CreateProjectData(Path.Combine(TestUtils.DriveRoot(), "Xamarin.csproj")),
            CreateProjectData(Path.Combine(TestUtils.DriveRoot(), "NetStd.csproj"))
        };

        ScannerEngineInputGenerator.SingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "SubDir", "foo.cs")), projects).Should().Be(projects[0]);
    }

    private void AssertFailedToCreateScannerInput(ScannerEngineInput scannerInput)
    {
        scannerInput.Should().BeNull();
        runtime.Logger.Should().HaveErrors();   // FIXME needs to be more specific
    }

    private void AssertScannerInputCreated(ScannerEngineInput scannerInput)
    {
        scannerInput.Should().NotBeNull();
        AssertValidProjectsExist(scannerInput);
        Console.WriteLine(scannerInput.ToString());
        runtime.Logger.Should().HaveNoErrors();
    }

    private static void AssertExpectedStatus(ScannerEngineInput scannerInput, params Guid[] validProjectGuids) => // FIXME rename
        new ScannerEngineInputReader(scannerInput.ToString())["sonar.modules"].Should().NotBeNull() // FIXME the null check might go away depending on how we restructure methods
            .And.Subject.Split(',').Should().BeEquivalentTo(validProjectGuids.Select(x => x.ToString().ToUpper()));

    private static void AssertValidProjectsExist(ScannerEngineInput scannerInput) =>    // FIXME inline
        new ScannerEngineInputReader(scannerInput.ToString())["sonar.modules"].Should().NotBeNull();

    private static void AssertExpectedProjectCount(int expected, ScannerEngineInput scannerInput)
    {
        scannerInput.Should().NotBeNull();
        new ScannerEngineInputReader(scannerInput.ToString())["sonar.modules"].Should().NotBeNull().And.Subject.Split(',').Should().HaveCount(expected);
    }

    private AnalysisConfig CreateValidConfig()
    {
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        TestUtils.CreateProjectWithFiles(TestContext, "project1", analysisRootDir);
        return CreateValidConfig(analysisRootDir);
    }

    private static AnalysisConfig CreateValidConfig(string outputDir, AnalysisProperties serverProperties = null, string workingDir = null)
    {
        var dummyProjectKey = Guid.NewGuid().ToString();
        return new()
        {
            SonarOutputDir = outputDir,
            SonarQubeHostUrl = "http://sonarqube.com",
            SonarProjectKey = dummyProjectKey,
            SonarProjectName = dummyProjectKey,
            SonarConfigDir = Path.Combine(outputDir, "config"),
            SonarProjectVersion = "1.0",
            SonarScannerWorkingDirectory = workingDir,
            ServerSettings = serverProperties ?? [],
            LocalSettings = [],
            ScanAllAnalysis = true,
        };
    }

    private static string CreateFileList(string parentDir, string fileName, params string[] files)
    {
        var fullPath = Path.Combine(parentDir, fileName);
        File.WriteAllLines(fullPath, files);
        return fullPath;
    }

    private ScannerEngineInputGenerator CreateSut(AnalysisConfig analysisConfig, PlatformOS os = PlatformOS.Unknown)
    {
        if (os != PlatformOS.Unknown)
        {
            runtime.ConfigureOS(os);
        }
        return new(analysisConfig, runtime, cmdLineArgs, new(runtime));
    }

    private static ProjectInfo[] LoadProjects(AnalysisConfig config) =>
        ProjectLoader.LoadFrom(config.SonarOutputDir);

    private ProjectData CreateProjectData(string fullPath) =>
        new[] { new ProjectInfo { FullPath = fullPath } }.ToProjectData(runtime).Single();
}
