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

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public partial class ScannerEngineInputGeneratorTest
{
    private const string TestSonarqubeOutputDir = @"e:\.sonarqube\out";

    private const string ProjectBaseDirInfoMessage =
        "Starting with Scanner for .NET v8 the way the `sonar.projectBaseDir` property is automatically detected has changed "
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
        var rvsf = new RoslynV1SarifFixer(runtime.Logger);
        var cmds = new ListPropertiesProvider();
        FluentActions.Invoking(() => new ScannerEngineInputGenerator(null, cmds, rntm)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("analysisConfig");
        FluentActions.Invoking(() => new ScannerEngineInputGenerator(cnfg, null, rntm)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("cmdLineArgs");
        FluentActions.Invoking(() => new ScannerEngineInputGenerator(cnfg, cmds, null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("runtime");
        FluentActions.Invoking(() => new ScannerEngineInputGenerator(cnfg, null, null, null, null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("runtime");
        FluentActions.Invoking(() => new ScannerEngineInputGenerator(cnfg, rntm, null, null, null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("fixer");
        FluentActions.Invoking(() => new ScannerEngineInputGenerator(cnfg, rntm, rvsf, null, null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("cmdLineArgs");
        FluentActions.Invoking(() => new ScannerEngineInputGenerator(cnfg, rntm, rvsf, cmds, null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("additionalFilesService");
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

    private void AssertFailedToCreateScannerInput(AnalysisResult result)
    {
        result.FullPropertiesFilePath.Should().BeNull();
        result.ScannerEngineInput.Should().BeNull();
        result.RanToCompletion.Should().BeFalse();
        AssertNoValidProjects(result);
        runtime.Should().HaveErrorsLogged();
    }

    private void AssertScannerInputCreated(AnalysisResult result)
    {
        result.FullPropertiesFilePath.Should().NotBeNull();
        result.ScannerEngineInput.Should().NotBeNull();
        AssertValidProjectsExist(result);
        TestContext.AddResultFile(result.FullPropertiesFilePath);
        Console.WriteLine(result.ScannerEngineInput.ToString());
        runtime.Should().HaveNoErrorsLogged();
    }

    private static void AssertExpectedStatus(string expectedProjectName, ProjectInfoValidity expectedStatus, AnalysisResult actual) =>
        actual.ProjectsByStatus(expectedStatus).Where(x => x.ProjectName.Equals(expectedProjectName))
            .Should().ContainSingle("ProjectInfo was not classified as expected. Project name: {0}, expected status: {1}", expectedProjectName, expectedStatus);

    private static void AssertNoValidProjects(AnalysisResult actual) =>
        actual.ProjectsByStatus(ProjectInfoValidity.Valid).Should().BeEmpty();

    private static void AssertValidProjectsExist(AnalysisResult actual) =>
        actual.ProjectsByStatus(ProjectInfoValidity.Valid).Should().NotBeEmpty();

    private static void AssertExpectedProjectCount(int expected, AnalysisResult actual) =>
        actual.Projects.Should().HaveCount(expected);

    private static void AssertFileIsReferenced(string fullFilePath, string content) =>
        content.Should().Contain(PropertiesWriter.Escape(fullFilePath), "files should be referenced");

    private static void AssertFileIsNotReferenced(string fullFilePath, string content) =>
        content.Should().NotContain(PropertiesWriter.Escape(fullFilePath), "file should not be referenced");

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

    private static string AddQuotes(string input) =>
        $"""
        "{input}"
        """;

    private ScannerEngineInputGenerator CreateSut(AnalysisConfig analysisConfig,
                                                  RoslynV1SarifFixer sarifFixer = null,
                                                  PlatformOS os = PlatformOS.Unknown)
    {
        sarifFixer ??= new RoslynV1SarifFixer(runtime.Logger);
        if (os != PlatformOS.Unknown)
        {
            runtime.ConfigureOS(os);
        }
        return new(analysisConfig, runtime, sarifFixer, cmdLineArgs, new(runtime));
    }

    private ProjectData CreateProjectData(string fullPath) =>
        new[] { new ProjectInfo { FullPath = fullPath } }.ToProjectData(runtime).Single();
}
