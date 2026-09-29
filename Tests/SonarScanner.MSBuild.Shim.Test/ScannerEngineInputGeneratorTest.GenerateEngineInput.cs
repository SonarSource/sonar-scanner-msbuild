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
    public void Generate_WhenThereIsNoCommonPath_LogsError()
    {
        var fileToAnalyzePath = TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "file.cs");
        var filesToAnalyzePath = TestUtils.CreateFile(TestContext.TestRunDirectory, AnalysisResultFileType.FilesToAnalyze.ToString(), fileToAnalyzePath);
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
        new ScannerEngineInputGenerator(new AnalysisConfig(), cmdLineArgs, runtime).Generate([firstProjectInfo, secondProjectInfo], runtime.DateTime.OffsetNow);
        runtime.Logger.Should().HaveErrors("""The project base directory cannot be automatically detected. Please specify the "/d:sonar.projectBaseDir" on the begin step.""");
    }

    [TestMethod]
    public void Generate_WhenProjectBaseDirDoesNotExist_LogsError()
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory, ".sonarqube", "out");
        TestUtils.CreateProjectWithFiles(TestContext, "Project", outPath);
        var config = new AnalysisConfig
        {
            SonarOutputDir = outPath,
            LocalSettings = [new Property(SonarProperties.ProjectBaseDir, "This path does not exist")]
        };

        new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate(LoadProjects(config), runtime.DateTime.OffsetNow);
        runtime.Logger.Should().HaveErrors("The project base directory doesn't exist.");
    }

    [TestMethod]
    public void Generate_WhenThereAreNoValidProjects_LogsError()
    {
        var config = new AnalysisConfig { SonarOutputDir = Path.Combine(TestContext.TestRunDirectory, ".sonarqube", "out") };
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

        new ScannerEngineInputGenerator(config, cmdLineArgs, runtime).Generate([firstProjectInfo, secondProjectInfo], runtime.DateTime.OffsetNow);
        runtime.Logger.Should().HaveInfos($"The exclude flag has been set so the project will not be analyzed. Project file: {firstProjectInfo.FullPath}")
            .And.HaveErrors("No analyzable projects were found. SonarQube analysis will not be performed.");
    }

    [TestMethod]
    [DataRow("https://sonarcloud.io")]
    [DataRow("https://sonarqube.us")]
    [DataRow("https://sonarqqqq.whale")]    // Any value, as long as it was auto-computed by the default URL mechanism and stored in SonarQubeAnalysisConfig.xml
    public void Generate_HostUrl_NotSet_UseSonarQubeHostUrl(string sonarQubeHost)
    {
        var config = new AnalysisConfig { SonarProjectKey = "key", SonarOutputDir = Path.Combine(TestContext.TestRunDirectory, ".sonarqube", "out"), SonarQubeHostUrl = sonarQubeHost };
        var engineInput = Generate_HostUrl_Execute(config);

        new ScannerEngineInputReader(engineInput.ToString()).AssertProperty("sonar.host.url", sonarQubeHost);
        runtime.Logger.Should().HaveDebugs("Setting analysis property: sonar.host.url=" + sonarQubeHost);
    }

    [TestMethod]
    public void Generate_HostUrl_ExplicitValue_Propagated()
    {
        var config = new AnalysisConfig
        {
            SonarProjectKey = "key",
            SonarOutputDir = Path.Combine(TestContext.TestRunDirectory, ".sonarqube", "out"),
            SonarQubeHostUrl = "Property should take precedence and this should not be used",
            LocalSettings = [new Property(SonarProperties.HostUrl, "http://localhost:9000")]
        };

        var engineInput = Generate_HostUrl_Execute(config);
        new ScannerEngineInputReader(engineInput.ToString()).AssertProperty("sonar.host.url", "http://localhost:9000");
    }

    [TestMethod]
    public void Generate_AnalyzerOutputPaths_ForUnexpectedLanguage_DoesNotWritePaths()
    {
        var context = new ScannerEngineInputContext(TestContext, "unexpected", runtime);
        context.AddAnalyzerOutPath("ProjectDir", ".sonarqube", "out", "0");

        context.Generate().Should().NotContain("ProjectDir");
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.analyzer.projectOutPaths")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.analyzer.projectOutPaths")]
    public void Generate_AnalyzerOutputPaths_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var context = new ScannerEngineInputContext(TestContext, language, runtime);
        var path1 = context.AddAnalyzerOutPath("ProjectDir", ".sonarqube", "out", "0");
        var path2 = context.AddAnalyzerOutPath("ProjectDir", ".sonarqube", "out", "1");

        context.CreateEngineInputReader().AssertProperty($"5762C17D-1DDF-4C77-86AC-E2B4940926A9.{expectedPropertyKey}", path1 + "," + path2);
    }

    [TestMethod]
    public void Generate_RoslynReportPaths_ForUnexpectedLanguage_DoesNotWritePaths()
    {
        var context = new ScannerEngineInputContext(TestContext, "unexpected", runtime);
        context.AddRoslynReportFilePath("ProjectDir", ".sonarqube", "out", "0", "Issues.json");

        context.Generate().Should().NotContain("ProjectDir");
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.roslyn.reportFilePaths")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.roslyn.reportFilePaths")]
    public void Generate_RoslynReportPaths_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var context = new ScannerEngineInputContext(TestContext, language, runtime);
        var path1 = context.AddRoslynReportFilePath("ProjectDir", ".sonarqube", "out", "0", "Issues.json");
        var path2 = context.AddRoslynReportFilePath("ProjectDir", ".sonarqube", "out", "1", "Issues.json");

        context.CreateEngineInputReader().AssertProperty($"5762C17D-1DDF-4C77-86AC-E2B4940926A9.{expectedPropertyKey}", path1 + "," + path2);
    }

    [TestMethod]
    public void Generate_Telemetry_ForUnexpectedLanguage_DoesNotWritePaths()
    {
        var context = new ScannerEngineInputContext(TestContext, "unexpected", runtime);
        context.AddTelemetryPath("ProjectDir", ".sonarqube", "out", "0", "Telemetry.json");

        context.Generate().Should().NotContain("ProjectDir");
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.scanner.telemetry")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.scanner.telemetry")]
    public void Generate_Telemetry_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var context = new ScannerEngineInputContext(TestContext, language, runtime);
        var path1 = context.AddTelemetryPath("ProjectDir", ".sonarqube", "out", "0", "Telemetry.json");
        var path2 = context.AddTelemetryPath("ProjectDir", ".sonarqube", "out", "1", "Telemetry.json");

        context.CreateEngineInputReader().AssertProperty($"5762C17D-1DDF-4C77-86AC-E2B4940926A9.{expectedPropertyKey}", path1 + "," + path2);
    }

    [TestMethod]
    public void Generate_ProjectAnalysisSettings_Propagated()
    {
        var context = new ScannerEngineInputContext(TestContext, ProjectLanguages.CSharp, runtime);
        context.AddSetting("my.setting1", "setting1");
        context.AddSetting("my.setting2", "setting 2 with spaces");
        context.AddSetting("my.setting.3", @"c:\dir1\dir2\foo.txt");

        var reader = context.CreateEngineInputReader();
        runtime.Logger.Should().HaveNoErrors();
        reader.AssertProperty("5762C17D-1DDF-4C77-86AC-E2B4940926A9.my.setting1", "setting1");
        reader.AssertProperty("5762C17D-1DDF-4C77-86AC-E2B4940926A9.my.setting2", "setting 2 with spaces");
        reader.AssertProperty("5762C17D-1DDF-4C77-86AC-E2B4940926A9.my.setting.3", @"c:\dir1\dir2\foo.txt");
    }

    private ScannerEngineInput Generate_HostUrl_Execute(AnalysisConfig config)
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
        var engineInput = sut.Generate([project], runtime.DateTime.OffsetNow);
        engineInput.Should().NotBeNull();
        return engineInput;
    }

    private class ScannerEngineInputContext
    {
        public readonly AnalysisConfig Config;
        private readonly AnalysisProperties settings = [];
        private readonly TestContext testContext;
        private readonly string language;
        private readonly TestRuntime runtime;

        public ScannerEngineInputContext(TestContext testContext, string language, TestRuntime runtime)
        {
            this.testContext = testContext;
            this.language = language;
            this.runtime = runtime;
            Config = new AnalysisConfig { SonarOutputDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext, ".sonarqube", "out") };
        }

        public void AddSetting(string key, string value) =>
            settings.Add(new Property(key, value));

        public string Generate()
        {
            TestUtils.CreateProjectWithFiles(testContext, "Project", language, Config.SonarOutputDir, new("5762C17D-1DDF-4C77-86AC-E2B4940926A9"), additionalProperties: settings);
            var sut = new ScannerEngineInputGenerator(Config, new ListPropertiesProvider(), runtime);
            var engineInput = sut.Generate(LoadProjects(Config), runtime.DateTime.OffsetNow);
            engineInput.Should().NotBeNull();
            return engineInput.ToString();
        }

        public ScannerEngineInputReader CreateEngineInputReader() =>
            new(Generate());

        public string AddAnalyzerOutPath(params string[] pathParts) =>
            AppendPath(
                language == ProjectLanguages.CSharp ? ScannerEngineInputGenerator.ProjectOutPathsKeyCS : ScannerEngineInputGenerator.ProjectOutPathsKeyVB,
                ScannerEngineInputGenerator.AnalyzerOutputPathsDelimiter,
                pathParts);

        public string AddRoslynReportFilePath(params string[] pathParts) =>
            AppendPath(
                language == ProjectLanguages.CSharp ? ScannerEngineInputGenerator.ReportFilePathsKeyCS : ScannerEngineInputGenerator.ReportFilePathsKeyVB,
                ScannerEngineInputGenerator.RoslynReportPathsDelimiter,
                pathParts);

        public string AddTelemetryPath(params string[] pathParts)
        {
            var path = CreatePath(pathParts);
            AddSetting(language == ProjectLanguages.CSharp ? ScannerEngineInputGenerator.TelemetryPathsKeyCS : ScannerEngineInputGenerator.TelemetryPathsKeyVB, path);
            return path;
        }

        private string AppendPath(string key, char delimiter, string[] pathParts)
        {
            var path = CreatePath(pathParts);
            if (settings.Find(x => x.Id == key) is { } existing)
            {
                existing.Value += delimiter + path;
            }
            else
            {
                settings.Add(new Property(key, path));
            }
            return path;
        }

        private static string CreatePath(string[] pathParts) =>
            Path.Combine([TestUtils.DriveRoot(), .. pathParts]);
    }
}
