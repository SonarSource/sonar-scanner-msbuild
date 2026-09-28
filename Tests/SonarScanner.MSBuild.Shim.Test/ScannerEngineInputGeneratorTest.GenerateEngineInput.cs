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
    public void GenerateEngineInput_WhenThereIsNoCommonPath_LogsError()
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory, ".sonarqube", "out");
        Directory.CreateDirectory(outPath);
        var fileToAnalyzePath = TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "file.cs");
        var filesToAnalyzePath = TestUtils.CreateFile(TestContext.TestRunDirectory, AnalysisResultFileType.FilesToAnalyze.ToString(), fileToAnalyzePath);
        var config = new AnalysisConfig { SonarOutputDir = outPath };
        var sut = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime);
        var firstProjectInfo = new ProjectInfo
        {
            ProjectGuid = Guid.NewGuid(),
            FullPath = Path.Combine(TestContext.TestRunDirectory, "First"),
            ProjectName = "First",
            AnalysisSettings = [],
            AnalysisResultFiles = [new(AnalysisResultFileType.FilesToAnalyze, filesToAnalyzePath)]
        };
        var secondProjectInfo = new ProjectInfo
        {
            ProjectGuid = Guid.NewGuid(),
            FullPath = Path.Combine(Path.GetTempPath(), "Second"),
            ProjectName = "Second",
            AnalysisSettings = [],
            AnalysisResultFiles = [new(AnalysisResultFileType.FilesToAnalyze, filesToAnalyzePath)]
        };
        TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "First");
        TestUtils.CreateEmptyFile(Path.GetTempPath(), "Second");

        // In order to force automatic root path detection to point to file system root,
        // create a project in the test run directory and a second one in the temp folder.
        sut.GenerateEngineInput(
            config,
            config.ToAnalysisProperties(runtime.Logger),
            new[] { firstProjectInfo, secondProjectInfo }.ToProjectData(runtime),
            runtime.DateTime.OffsetNow);

        runtime.Logger.Should().HaveErrors("""The project base directory cannot be automatically detected. Please specify the "/d:sonar.projectBaseDir" on the begin step.""");
    }

    [TestMethod]
    public void GenerateEngineInput_WhenProjectBaseDirDoesNotExist_LogsError()
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory!, ".sonarqube", "out");
        Directory.CreateDirectory(outPath);
        var project = new ProjectInfo
        {
            ProjectGuid = Guid.NewGuid(),
            FullPath = Path.Combine(TestContext.TestRunDirectory, "Project"),
            ProjectName = "Project",
            AnalysisSettings = [],
            AnalysisResultFiles = []
        };
        var config = new AnalysisConfig
        {
            SonarOutputDir = outPath,
            LocalSettings = [new Property(SonarProperties.ProjectBaseDir, "This path does not exist")]
        };
        var sut = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime);
        sut.GenerateEngineInput(
            config,
            config.ToAnalysisProperties(runtime.Logger),
            [new ProjectData(new[] { project }.GroupBy(x => x.ProjectGuid).Single(), runtime) { Status = ProjectInfoValidity.Valid }],
            runtime.DateTime.OffsetNow);

        runtime.Logger.Should().HaveErrors("The project base directory doesn't exist.");
    }

    [TestMethod]
    public void GenerateEngineInput_WhenThereAreNoValidProjects_LogsError()
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory!, ".sonarqube", "out");
        Directory.CreateDirectory(outPath);
        var config = new AnalysisConfig { SonarOutputDir = outPath };
        var sut = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime);
        var firstProjectInfo = new ProjectInfo
        {
            ProjectGuid = Guid.NewGuid(),
            FullPath = Path.Combine(TestContext.TestRunDirectory, "First"),
            ProjectName = "First",
            IsExcluded = true,
            AnalysisSettings = [],
            AnalysisResultFiles = []
        };
        var secondProjectInfo = new ProjectInfo
        {
            ProjectGuid = Guid.NewGuid(),
            FullPath = Path.Combine(TestContext.TestRunDirectory, "Second"),
            ProjectName = "Second",
            AnalysisSettings = [],
            AnalysisResultFiles = []
        };
        TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "First");
        TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "Second");
        sut.GenerateEngineInput(
            config,
            config.ToAnalysisProperties(runtime.Logger),
            new[] { firstProjectInfo, secondProjectInfo }.ToProjectData(runtime),
            runtime.DateTime.OffsetNow);

        runtime.Logger.Should().HaveInfos($"The exclude flag has been set so the project will not be analyzed. Project file: {firstProjectInfo.FullPath}")
            .And.HaveErrors("No analyzable projects were found. SonarQube analysis will not be performed.");
    }

    [TestMethod]
    [DataRow("https://sonarcloud.io")]
    [DataRow("https://sonarqube.us")]
    [DataRow("https://sonarqqqq.whale")]    // Any value, as long as it was auto-computed by the default URL mechanism and stored in SonarQubeAnalysisConfig.xml
    public void GenerateEngineInput_HostUrl_NotSet_UseSonarQubeHostUrl(string sonarQubeHost)
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory, ".sonarqube", "out");
        var config = new AnalysisConfig { SonarProjectKey = "key", SonarOutputDir = outPath, SonarQubeHostUrl = sonarQubeHost };
        var engineInput = GenerateEngineInput_HostUrl_Execute(config);

        new ScannerEngineInputReader(engineInput.ToString()).AssertProperty("sonar.host.url", sonarQubeHost);
        runtime.Logger.Should().HaveDebugs("Setting analysis property: sonar.host.url=" + sonarQubeHost);
    }

    [TestMethod]
    public void GenerateEngineInput_HostUrl_ExplicitValue_Propagated()
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory, ".sonarqube", "out");
        var config = new AnalysisConfig
        {
            SonarProjectKey = "key",
            SonarOutputDir = outPath,
            SonarQubeHostUrl = "Property should take precedence and this should not be used",
            LocalSettings = [new Property(SonarProperties.HostUrl, "http://localhost:9000")]
        };

        var engineInput = GenerateEngineInput_HostUrl_Execute(config);
        new ScannerEngineInputReader(engineInput.ToString()).AssertProperty("sonar.host.url", "http://localhost:9000");
    }

    [TestMethod]
    public void GenerateEngineInput_AnalyzerOutputPaths_ForUnexpectedLanguage_DoesNotWritePaths()
    {
        var context = new ScannerEngineInputContext(TestContext, "unexpected", runtime);
        context.AddAnalyzerOutPath("ProjectDir", ".sonarqube", "out", "0");

        context.GenerateEngineInput().Should().NotContain("ProjectDir");
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.analyzer.projectOutPaths")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.analyzer.projectOutPaths")]
    public void GenerateEngineInput_AnalyzerOutputPaths_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var context = new ScannerEngineInputContext(TestContext, language, runtime);
        var path1 = context.AddAnalyzerOutPath("ProjectDir", ".sonarqube", "out", "0");
        var path2 = context.AddAnalyzerOutPath("ProjectDir", ".sonarqube", "out", "1");

        context.CreateEngineInputReader().AssertProperty($"5762C17D-1DDF-4C77-86AC-E2B4940926A9.{expectedPropertyKey}", path1 + "," + path2);
    }

    [TestMethod]
    public void GenerateEngineInput_RoslynReportPaths_ForUnexpectedLanguage_DoesNotWritePaths()
    {
        var context = new ScannerEngineInputContext(TestContext, "unexpected", runtime);
        context.AddRoslynReportFilePath("ProjectDir", ".sonarqube", "out", "0", "Issues.json");

        context.GenerateEngineInput().Should().NotContain("ProjectDir");
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.roslyn.reportFilePaths")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.roslyn.reportFilePaths")]
    public void GenerateEngineInput_RoslynReportPaths_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var context = new ScannerEngineInputContext(TestContext, language, runtime);
        var path1 = context.AddRoslynReportFilePath("ProjectDir", ".sonarqube", "out", "0", "Issues.json");
        var path2 = context.AddRoslynReportFilePath("ProjectDir", ".sonarqube", "out", "1", "Issues.json");

        context.CreateEngineInputReader().AssertProperty($"5762C17D-1DDF-4C77-86AC-E2B4940926A9.{expectedPropertyKey}", path1 + "," + path2);
    }

    [TestMethod]
    public void GenerateEngineInput_Telemetry_ForUnexpectedLanguage_DoesNotWritePaths()
    {
        var context = new ScannerEngineInputContext(TestContext, "unexpected", runtime);
        context.AddTelemetryPath("ProjectDir", ".sonarqube", "out", "0", "Telemetry.json");

        context.GenerateEngineInput().Should().NotContain("ProjectDir");
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.scanner.telemetry")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.scanner.telemetry")]
    public void GenerateEngineInput_Telemetry_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var context = new ScannerEngineInputContext(TestContext, language, runtime);
        var path1 = context.AddTelemetryPath("ProjectDir", ".sonarqube", "out", "0", "Telemetry.json");
        var path2 = context.AddTelemetryPath("ProjectDir", ".sonarqube", "out", "1", "Telemetry.json");

        context.CreateEngineInputReader().AssertProperty($"5762C17D-1DDF-4C77-86AC-E2B4940926A9.{expectedPropertyKey}", path1 + "," + path2);
    }

    [TestMethod]
    public void GenerateEngineInput_ProjectAnalysisSettings_Propagated()
    {
        var context = new ScannerEngineInputContext(TestContext, ProjectLanguages.CSharp, runtime);
        context.Project.Project.AnalysisSettings =
        [
            new("my.setting1", "setting1"),
            new("my.setting2", "setting 2 with spaces"),
            new("my.setting.3", @"c:\dir1\dir2\foo.txt")
        ];

        var reader = context.CreateEngineInputReader();
        runtime.Logger.Should().HaveNoErrors();
        reader.AssertProperty("5762C17D-1DDF-4C77-86AC-E2B4940926A9.my.setting1", "setting1");
        reader.AssertProperty("5762C17D-1DDF-4C77-86AC-E2B4940926A9.my.setting2", "setting 2 with spaces");
        reader.AssertProperty("5762C17D-1DDF-4C77-86AC-E2B4940926A9.my.setting.3", @"c:\dir1\dir2\foo.txt");
    }

    private ScannerEngineInput GenerateEngineInput_HostUrl_Execute(AnalysisConfig config)
    {
        var sut = new ScannerEngineInputGenerator(config, cmdLineArgs, runtime);
        var projectPath = TestUtils.CreateEmptyFile(config.SonarOutputDir, "Project.csproj");
        var sourceFilePath = TestUtils.CreateEmptyFile(config.SonarOutputDir, "Program.cs");
        var filesToAnalyzePath = TestUtils.CreateFile(config.SonarOutputDir, "FilesToAnalyze.txt", sourceFilePath);
        var project = new ProjectInfo
        {
            ProjectGuid = new Guid("A85D6F60-4D86-401E-BE44-177F524BD4BB"),
            FullPath = projectPath,
            ProjectName = "Project",
            IsExcluded = false,
            AnalysisSettings = [],
            AnalysisResultFiles = [new(AnalysisResultFileType.FilesToAnalyze, filesToAnalyzePath)],
        };
        var engineInput = sut.GenerateEngineInput(
            config,
            config.ToAnalysisProperties(runtime.Logger),
            new[] { project }.ToProjectData(runtime),
            runtime.DateTime.OffsetNow);
        engineInput.Should().NotBeNull();
        return engineInput;
    }

    private class ScannerEngineInputContext
    {
        public readonly AnalysisConfig Config;
        public readonly ProjectData Project;
        private readonly TestRuntime runtime;

        public ScannerEngineInputContext(TestContext testContext, string language, TestRuntime runtime)
        {
            this.runtime = runtime;
            Config = new AnalysisConfig { SonarOutputDir = testContext.TestRunDirectory };
            var sourceFilePath = TestUtils.CreateEmptyFile(testContext.TestRunDirectory, "File.cs");
            var filesToAnalyzePath = TestUtils.CreateFile(testContext.TestRunDirectory, "FilesToAnalyze.txt", sourceFilePath);
            var info = new ProjectInfo
            {
                ProjectName = "Project",
                ProjectGuid = new("5762C17D-1DDF-4C77-86AC-E2B4940926A9"),
                ProjectLanguage = language,
                FullPath = TestUtils.CreateEmptyFile(testContext.TestRunDirectory, "Project.proj"),
                AnalysisResultFiles = [new(AnalysisResultFileType.FilesToAnalyze, filesToAnalyzePath)],
                AnalysisSettings = []
            };
            Project = new[] { info }.ToProjectData(runtime).Single();
            Project.Status.Should().Be(ProjectInfoValidity.Valid);
        }

        public string GenerateEngineInput()
        {
            var sut = new ScannerEngineInputGenerator(Config, new ListPropertiesProvider(), runtime);
            var engineInput = sut.GenerateEngineInput(Config, Config.ToAnalysisProperties(runtime.Logger), [Project], runtime.DateTime.OffsetNow);
            engineInput.Should().NotBeNull();
            return engineInput.ToString();
        }

        public ScannerEngineInputReader CreateEngineInputReader() =>
            new(GenerateEngineInput());

        public string AddAnalyzerOutPath(params string[] pathParts) =>
            AddPath(Project.AnalyzerOutPaths, pathParts);

        public string AddRoslynReportFilePath(params string[] pathParts) =>
            AddPath(Project.RoslynReportFilePaths, pathParts);

        public string AddTelemetryPath(params string[] pathParts) =>
            AddPath(Project.TelemetryPaths, pathParts);

        private static string AddPath(ICollection<FileInfo> paths, string[] pathParts)
        {
            var path = Path.Combine([TestUtils.DriveRoot(), .. pathParts]);
            paths.Add(new FileInfo(path));
            return path;
        }
    }
}
