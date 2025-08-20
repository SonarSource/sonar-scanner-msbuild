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

using System.Runtime.InteropServices;
using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public partial class PropertiesFileGeneratorTests
{
    private const string TestSonarqubeOutputDir = @"e:\.sonarqube\out";

    private const string ProjectBaseDirInfoMessage =
        "Starting with Scanner for .NET v8 the way the `sonar.projectBaseDir` property is automatically detected has changed "
        + "and this has an impact on the files that are analyzed and other properties that are resolved relative to it like `sonar.exclusions` and `sonar.test.exclusions`. "
        + "If you would like to customize the behavior, please set the `sonar.projectBaseDir` property to point to a directory that contains all the source code you want to analyze. "
        + "The path may be relative (to the directory from which the analysis was started) or absolute.";

    private readonly TestLogger logger = new();

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void PropertiesFileGenerator_WhenConfigIsNull_Throws() =>
        ((Func<PropertiesFileGenerator>)(() => new(null, new TestLogger()))).Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("analysisConfig");

    [TestMethod]
    public void PropertiesFileGenerator_FirstConstructor_WhenLoggerIsNull_Throws() =>
        ((Func<PropertiesFileGenerator>)(() => new(new AnalysisConfig(), null, new RoslynV1SarifFixer(new TestLogger()), new RuntimeInformationWrapper(), null)))
            .Should().ThrowExactly<ArgumentNullException>()
            .And.ParamName.Should().Be("logger");

    [TestMethod]
    public void PropertiesFileGenerator_SecondConstructor_WhenLoggerIsNull_Throws() =>
        // the RoslynV1SarifFixer will throw
        ((Func<PropertiesFileGenerator>)(() => new(new AnalysisConfig(), null))).Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");

    [TestMethod]
    public void PropertiesFileGenerator_WhenFixerIsNull_Throws() =>
        ((Func<PropertiesFileGenerator>)(() => new(new AnalysisConfig(), new TestLogger(), null, new RuntimeInformationWrapper(), null))).Should()
            .ThrowExactly<ArgumentNullException>()
            .And.ParamName.Should().Be("fixer");

    [TestMethod]
    public void PropertiesFileGenerator_WhenRuntimeInformationWrapperIsNull_Throws() =>
        ((Func<PropertiesFileGenerator>)(() => new(new AnalysisConfig(), logger, new RoslynV1SarifFixer(logger), null, null))).Should()
            .ThrowExactly<ArgumentNullException>()
            .And.ParamName.Should().Be("runtimeInformationWrapper");

    [TestMethod]
    public void PropertiesFileGenerator_WhenAdditionalFileServiceIsNull_Throws() =>
        ((Func<PropertiesFileGenerator>)(() => new(new AnalysisConfig(), logger, new RoslynV1SarifFixer(logger), new RuntimeInformationWrapper(), null))).Should()
            .ThrowExactly<ArgumentNullException>()
            .And.ParamName.Should().Be("additionalFilesService");

    [TestMethod]
    [DataRow("cs")]
    [DataRow("vbnet")]
    public void ToProjectData_Orders_AnalyzerOutPaths(string languageKey)
    {
        var guid = Guid.NewGuid();
        var propertyKey = $"sonar.{languageKey}.analyzer.projectOutPaths";
        var fullPath = TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "File.txt");
        var projectInfos = new[]
        {
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Release",
                Platform = "anyCpu",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = [new(propertyKey, "1")],
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "anyCpu",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = [new(propertyKey, "2")],
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "x86",
                TargetFramework = "net46",
                AnalysisSettings = [new(propertyKey, "3")],
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "x86",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = [new(propertyKey, "4")],
                FullPath = fullPath,
            },
        };

        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
        var propertiesFileGenerator = CreateSut(CreateValidConfig(analysisRootDir));
        var results = propertiesFileGenerator.ToProjectData(projectInfos.GroupBy(x => x.ProjectGuid).First()).AnalyzerOutPaths.ToList();

        results.Should().HaveCount(4);
        results[0].FullName.Should().Be(new FileInfo("2").FullName);
        results[1].FullName.Should().Be(new FileInfo("3").FullName);
        results[2].FullName.Should().Be(new FileInfo("4").FullName);
        results[3].FullName.Should().Be(new FileInfo("1").FullName);
    }

    [TestMethod]
    public void ToProjectData_ProjectsWithDuplicateGuid()
    {
        var guid = Guid.NewGuid();
        var projectInfos = new[]
        {
            new ProjectInfo { ProjectGuid = guid, FullPath = "path1" },
            new ProjectInfo { ProjectGuid = guid, FullPath = "path2" },
            new ProjectInfo { ProjectGuid = guid, FullPath = "path2" }
        };
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
        var propertiesFileGenerator = new PropertiesFileGenerator(CreateValidConfig(analysisRootDir), logger);
        var result = propertiesFileGenerator.ToProjectData(projectInfos.GroupBy(x => x.ProjectGuid).First());

        result.Status.Should().Be(ProjectInfoValidity.DuplicateGuid);
        logger.Warnings.Should().BeEquivalentTo(
            $@"Duplicate ProjectGuid: ""{guid}"". The project will not be analyzed. Project file: ""path1""",
            $@"Duplicate ProjectGuid: ""{guid}"". The project will not be analyzed. Project file: ""path2""");
    }

    // Repro for https://sonarsource.atlassian.net/browse/SCAN4NET-431
    [TestMethod]
    public void ToProjectData_DoesNotChooseValidProject()
    {
        var guid = Guid.NewGuid();
        var fullPath = TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "File.txt");
        var contentFile1 = TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "contentFile1.txt");
        var contentFileList1 = TestUtils.CreateFile(TestContext.TestRunDirectory, "contentList.txt", contentFile1);
        var projectInfos = new[]
        {
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "x86",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = [],
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                Platform = "x86",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = [
                    new(PropertiesFileGenerator.ReportFilePathsCSharpPropertyKey, "validRoslyn"),
                    new(PropertiesFileGenerator.ProjectOutPathsCsharpPropertyKey, "validOutPath")
                ],
                FullPath = fullPath,
            }
        };
        projectInfos[0].AddAnalyzerResult(AnalysisType.FilesToAnalyze, contentFileList1);
        projectInfos[1].AddAnalyzerResult(AnalysisType.FilesToAnalyze, contentFileList1);
        var config = CreateValidConfig("outputDir");
        var propertiesFileGenerator = new PropertiesFileGenerator(config, logger);
        var sut = propertiesFileGenerator.ToProjectData(projectInfos.GroupBy(x => x.ProjectGuid).First());

        sut.Status.Should().Be(ProjectInfoValidity.Valid);
        sut.Project.AnalysisSettings.Should().BeNullOrEmpty(); // Expected to change when fixed
        var writer = new PropertiesWriter(config);
        writer.WriteSettingsForProject(sut);
        var resultString = writer.Flush();
        resultString.Should().NotContain("validRoslyn"); // Expected to change when fixed
        resultString.Should().NotContain("validOutPath"); // Expected to change when fixed
    }

    [TestMethod]
    [DataRow("cs")]
    [DataRow("vbnet")]
    public void Telemetry_Multitargeting(string languageKey)
    {
        var guid = Guid.NewGuid();
        var propertyKey = $"sonar.{languageKey}.scanner.telemetry";
        var fullPath = TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "File.txt");
        var projectInfos = new[]
        {
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                TargetFramework = "netstandard2.0",
                AnalysisSettings = [new(propertyKey, "1.json")],
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Debug",
                TargetFramework = "net46",
                AnalysisSettings = [new(propertyKey, "2.json")],
                FullPath = fullPath,
            },
            new ProjectInfo
            {
                ProjectGuid = guid,
                Configuration = "Release",
                TargetFramework = "netstandard2.0",
                AnalysisSettings =  [
                    new(propertyKey, "3.json"),
                    new(propertyKey, "4.json"),
                ],
                FullPath = fullPath,
            },
        };

        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
        var propertiesFileGenerator = CreateSut(CreateValidConfig(analysisRootDir));
        var results = propertiesFileGenerator.ToProjectData(projectInfos.GroupBy(x => x.ProjectGuid).Single()).TelemetryPaths.ToList();

        results.Should().BeEquivalentTo([new FileInfo("2.json"), new("1.json"), new("3.json"), new("4.json")], x => x.Excluding(x => x.Length).Excluding(x => x.Directory));
    }

    [TestMethod]
    public void GetClosestProjectOrDefault_WhenNoProjects_ReturnsNull() =>
        PropertiesFileGenerator.SingleClosestProjectOrDefault(new FileInfo("File.cs"), []).Should().BeNull();

    [TestMethod]
    public void GetClosestProjectOrDefault_WhenNoMatch_ReturnsNull()
    {
        var projects = new[]
        {
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot("D"), "WrongDrive.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine("~ProjectDir", "Incorrect.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot("C"), "WrongDrive.csproj") }),
        };

        PropertiesFileGenerator.SingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot("E"), "File.cs")), projects).Should().BeNull();
    }

    [TestMethod]
    public void GetClosestProjectOrDefault_WhenOnlyOneProjectMatchingWithSameCase_ReturnsProject()
    {
        var projects = new[]
        {
            new ProjectData(new ProjectInfo { FullPath = "InRoot.csproj" }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine("~ProjectDir", "Incorrect.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "Winner.csproj") }),
        };

        PropertiesFileGenerator.SingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "File.cs")), projects).Should().Be(projects[2]);
    }

    [TestMethod]
    public void GetClosestProjectOrDefault_WhenOnlyOneProjectMatchingWithDifferentCase_ReturnsProject()
    {
        var projects = new[]
        {
            new ProjectData(new ProjectInfo { FullPath = "InRoot.csproj" }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine("~PROJECTDIR", "Incorrect.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "Winner.csproj") }),
        };

        PropertiesFileGenerator.SingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot("C"), "PROJECTDIR", "FILE.cs")), projects).Should().Be(projects[2]);
    }

    [TestMethod]
    public void GetClosestProjectOrDefault_WhenOnlyOneProjectMatchingWithDifferentSeparators_ReturnsProject()
    {
        var projects = new[]
        {
            new ProjectData(new ProjectInfo { FullPath = "InRoot.csproj" }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine("~ProjectDir", "Incorrect.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = $"{TestUtils.DriveRoot()}{Path.AltDirectorySeparatorChar}ProjectDir{Path.AltDirectorySeparatorChar}Winner.csproj" }),
        };

        PropertiesFileGenerator.SingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "File.cs")), projects).Should().Be(projects[2]);
    }

    [TestMethod]
    public void GetClosestProjectOrDefault_WhenMultipleProjectsMatch_ReturnsProjectWithLongestMatch()
    {
        var projects = new[]
        {
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "InRoot.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "InProjectDir.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "SubDir", "Winner.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "AnotherInProjectDir.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "SubDir", "Deeper", "TooDeep.csproj") }),
        };

        PropertiesFileGenerator.SingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "SubDir", "File.cs")), projects).Should().Be(projects[2]);
    }

    [TestMethod]
    public void GetClosestProjectOrDefault_WhenMultipleProjectsMatchWithSameLength_ReturnsClosestProject()
    {
        var projects = new[]
        {
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "Net46.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "Xamarin.csproj") }),
            new ProjectData(new ProjectInfo { FullPath = Path.Combine(TestUtils.DriveRoot(), "NetStd.csproj") }),
        };

        PropertiesFileGenerator.SingleClosestProjectOrDefault(new FileInfo(Path.Combine(TestUtils.DriveRoot(), "ProjectDir", "SubDir", "foo.cs")), projects).Should().Be(projects[0]);
    }

    [TestMethod]
    public void ComputeProjectBaseDir_BestCommonRoot_AllInRoot_NoWarning()
    {
        var sut = new PropertiesFileGenerator(new(), logger);
        var projectPaths = new[]
        {
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Lib")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Src")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Test")),
        };

        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name"));
        logger.AssertNoWarningsLogged();
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    // On Unix, there always is a best common root "/" and there are never projects outside of the root.
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_BestCommonRoot_ProjectOutsideRoot_LogsWarning()
    {
        var sut = new PropertiesFileGenerator(new(), logger);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\Projects\Name\Src"),
            new DirectoryInfo(@"C:\Projects\Name\Test"),
            new DirectoryInfo(@"D:\OutsideRoot"),
            new DirectoryInfo(@"E:\AlsoOutside"),
        };

        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(@"C:\Projects\Name");
        logger.AssertWarningLogged(@"Directory 'D:\OutsideRoot' is not located under the base directory 'C:\Projects\Name' and will not be analyzed.");
        logger.AssertWarningLogged(@"Directory 'E:\AlsoOutside' is not located under the base directory 'C:\Projects\Name' and will not be analyzed.");
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    // On Linux there always is a best common root "/".
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_NoBestCommonRoot_ReturnsNull()
    {
        var sut = new PropertiesFileGenerator(new AnalysisConfig(), logger);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\RootOnce"),
            new DirectoryInfo(@"D:\AlsoOnce"),
            new DirectoryInfo(@"E:\NotHelping"),
        };
        sut.ComputeProjectBaseDir(projectPaths).Should().BeNull();

        logger.AssertNoErrorsLogged();
        logger.AssertNoWarningsLogged();
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    [TestMethod]
    public void ComputeProjectBaseDir_WorkingDirectory_AllFilesInWorkingDirectory()
    {
        var sut = new PropertiesFileGenerator(new AnalysisConfig { SonarScannerWorkingDirectory = Path.Combine(TestUtils.DriveRoot(), "Projects") }, logger);
        var projectPaths = new[]
        {
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Lib")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Src")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Projects", "Name", "Test")),
        };
        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(Path.Combine(TestUtils.DriveRoot(), "Projects"));

        logger.AssertNoWarningsLogged();
        logger.DebugMessages.Should().BeEquivalentTo($"Using working directory as project base directory: '{Path.Combine(TestUtils.DriveRoot(), "Projects")}'.");
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    [TestMethod]
    public void ComputeProjectBaseDir_WorkingDirectory_FilesOutsideWorkingDirectory_FallsBackToCommonPath()
    {
        var sut = new PropertiesFileGenerator(new AnalysisConfig { SonarScannerWorkingDirectory = Path.Combine(TestUtils.DriveRoot(), "Solution", "Net") }, logger);
        var projectPaths = new[]
        {
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Solution", "Net", "Name", "Lib")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Solution", "Net", "Name", "Src")),
            new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "Solution", "JS")), // At least one directory is not below SonarScannerWorkingDirectory. We fall back to the common root logic.
        };
        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(Path.Combine(TestUtils.DriveRoot(), "Solution"));

        logger.AssertNoWarningsLogged();
        logger.DebugMessages.Should().ContainSingle().Which.Should().BeIgnoringLineEndings(
            $"""
            Using longest common projects path as a base directory: '{Path.Combine(TestUtils.DriveRoot(), "Solution")}'. Identified project paths:
            {Path.Combine(TestUtils.DriveRoot(), "Solution", "Net", "Name", "Lib")}
            {Path.Combine(TestUtils.DriveRoot(), "Solution", "Net", "Name", "Src")}
            {Path.Combine(TestUtils.DriveRoot(), "Solution", "JS")}
            """);
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    // On Unix, there always is a best common root "/".
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_BestCommonRoot_CaseSensitive_NoRoot_ReturnsNull()
    {
        var runtimeInformationWrapper = Substitute.For<IRuntimeInformationWrapper>();
        runtimeInformationWrapper.IsOS(OSPlatform.Windows).Returns(false);
        var additionalFileService = Substitute.For<IAdditionalFilesService>();
        var sut = new PropertiesFileGenerator(new() { SonarOutputDir = @"C:\fallback" }, logger, new RoslynV1SarifFixer(logger), runtimeInformationWrapper, additionalFileService);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\Projects\Name\Lib"),
            new DirectoryInfo(@"c:\projects\name\Test"),
        };
        sut.ComputeProjectBaseDir(projectPaths).Should().BeNull();

        logger.AssertNoWarningsLogged();
        logger.AssertNoErrorsLogged();
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    // Case sensitive tests don't apply to Unix.
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_BestCommonRoot_CaseInsensitive()
    {
        var runtimeInformationWrapper = Substitute.For<IRuntimeInformationWrapper>();
        runtimeInformationWrapper.IsOS(OSPlatform.Windows).Returns(true);
        var additionalFileService = Substitute.For<IAdditionalFilesService>();
        var sut = new PropertiesFileGenerator(new(), logger, new RoslynV1SarifFixer(logger), runtimeInformationWrapper, additionalFileService);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\Projects\Name\Lib"),
            new DirectoryInfo(@"c:\projects\name\Test"),
        };
        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(@"C:\Projects\Name");

        logger.AssertNoWarningsLogged();
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
    }

    // On Unix, there always is a best common root "/".
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void ComputeProjectBaseDir_WorkingDirectory_FilesOutsideWorkingDirectory_NoCommonRoot()
    {
        var sut = new PropertiesFileGenerator(new AnalysisConfig { SonarScannerWorkingDirectory = @"C:\Solution" }, logger);
        var projectPaths = new[]
        {
            new DirectoryInfo(@"C:\Solution\Net\Name\Lib"),
            new DirectoryInfo(@"C:\Solution\Net\Name\Src"),
            new DirectoryInfo(@"D:\SomewhereElse"), // At least one directory is not below SonarScannerWorkingDirectory. We fall back to the common root logic.
        };
        sut.ComputeProjectBaseDir(projectPaths).FullName.Should().Be(@"C:\Solution\Net\Name");

        logger.Warnings.Should().BeEquivalentTo(@"Directory 'D:\SomewhereElse' is not located under the base directory 'C:\Solution\Net\Name' and will not be analyzed.");
        logger.DebugMessages.Should().BeEquivalentTo(
            """
            Using longest common projects path as a base directory: 'C:\Solution\Net\Name'. Identified project paths:
            C:\Solution\Net\Name\Lib
            C:\Solution\Net\Name\Src
            D:\SomewhereElse
            """);
        logger.AssertSingleInfoMessageExists(ProjectBaseDirInfoMessage);
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

        new PropertiesFileGenerator(config, logger).ComputeProjectBaseDir([]).Name.Should().Be(expected);
        logger.DebugMessages.Should().ContainSingle(x => x.StartsWith("Using user supplied project base directory:"));
    }

    /// <summary>
    /// Creates a single new project valid project with dummy files and analysis config file with the specified local settings.
    /// Checks that a property file is created.
    /// </summary>
    private ProjectInfoAnalysisResult ExecuteAndCheckSucceeds(string projectName, TestLogger logger, params Property[] localSettings)
    {
        var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, projectName);
        TestUtils.CreateProjectWithFiles(TestContext, projectName, analysisRootDir);
        var config = CreateValidConfig(analysisRootDir);
        config.LocalSettings = [.. localSettings];
        var result = new PropertiesFileGenerator(config, logger).GenerateFile();

        AssertExpectedProjectCount(1, result);
        AssertPropertiesFilesCreated(result, logger);
        return result;
    }

    private static void AssertFailedToCreatePropertiesFiles(ProjectInfoAnalysisResult result, TestLogger logger)
    {
        result.FullPropertiesFilePath.Should().BeNull("Not expecting the sonar-scanner properties file to have been set");
        result.RanToCompletion.Should().BeFalse("Expecting the property file generation to have failed");
        AssertNoValidProjects(result);
        logger.AssertErrorsLogged();
    }

    private void AssertPropertiesFilesCreated(ProjectInfoAnalysisResult result, TestLogger logger)
    {
        result.FullPropertiesFilePath.Should().NotBeNull("Expecting the sonar-scanner properties file to have been set");
        AssertValidProjectsExist(result);
        TestContext.AddResultFile(result.FullPropertiesFilePath);
        logger.AssertErrorsLogged(0);
    }

    private static void AssertExpectedStatus(string expectedProjectName, ProjectInfoValidity expectedStatus, ProjectInfoAnalysisResult actual) =>
        actual.ProjectsByStatus(expectedStatus).Where(x => x.ProjectName.Equals(expectedProjectName))
            .Should().ContainSingle("ProjectInfo was not classified as expected. Project name: {0}, expected status: {1}", expectedProjectName, expectedStatus);

    private static void AssertNoValidProjects(ProjectInfoAnalysisResult actual) =>
        actual.ProjectsByStatus(ProjectInfoValidity.Valid).Should().BeEmpty("Not expecting to find any valid ProjectInfo files");

    private static void AssertValidProjectsExist(ProjectInfoAnalysisResult actual) =>
        actual.ProjectsByStatus(ProjectInfoValidity.Valid).Should().NotBeEmpty("Expecting at least one valid ProjectInfo file to exist");

    private static void AssertExpectedProjectCount(int expected, ProjectInfoAnalysisResult actual) =>
        actual.Projects.Should().HaveCount(expected, "Unexpected number of projects in the result");

    private static void AssertFileIsReferenced(string fullFilePath, string content) =>
        content.Should().Contain(PropertiesWriter.Escape(fullFilePath), "Files should be referenced");

    private static void AssertFileIsNotReferenced(string fullFilePath, string content) =>
        content.Should().NotContain(PropertiesWriter.Escape(fullFilePath), "File should not be referenced");

    private static string ComputeProjectBaseDir(string teamBuildValue, string userValue, string[] projectPaths)
    {
        var config = new AnalysisConfig();
        var logger = new TestLogger();
        config.SonarOutputDir = TestSonarqubeOutputDir;
        config.SourcesDirectory = teamBuildValue;
        config.LocalSettings ??= new();
        config.LocalSettings.Add(new(SonarProperties.ProjectBaseDir, userValue));
        return new PropertiesFileGenerator(config, logger).ComputeProjectBaseDir(projectPaths.Select(x => new DirectoryInfo(x)).ToList())?.FullName;
    }

    private static void VerifyProjectBaseDir(string expectedValue, string teamBuildValue, string userValue, string[] projectPaths) =>
        ComputeProjectBaseDir(teamBuildValue, userValue, projectPaths).Should().Be(expectedValue);

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

    private PropertiesFileGenerator CreateSut(
        AnalysisConfig analysisConfig,
        IRoslynV1SarifFixer sarifFixer = null,
        IRuntimeInformationWrapper runtimeInformationWrapper = null,
        IAdditionalFilesService additionalFileService = null)
    {
        sarifFixer ??= new RoslynV1SarifFixer(logger);
        runtimeInformationWrapper ??= new RuntimeInformationWrapper();
        additionalFileService ??= new AdditionalFilesService(DirectoryWrapper.Instance, logger);
        return new(analysisConfig, logger, sarifFixer, runtimeInformationWrapper, additionalFileService);
    }
}
