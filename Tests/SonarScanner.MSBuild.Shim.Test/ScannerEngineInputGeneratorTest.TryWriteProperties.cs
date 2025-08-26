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

using Newtonsoft.Json;

namespace SonarScanner.MSBuild.Shim.Test;

public partial class ScannerEngineInputGeneratorTest
{
    [TestMethod]
    public void TryWriteProperties_WhenThereIsNoCommonPath_LogsError()
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory, ".sonarqube", "out");
        Directory.CreateDirectory(outPath);
        var fileToAnalyzePath = TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "file.cs");
        var filesToAnalyzePath = TestUtils.CreateFile(TestContext.TestRunDirectory, TestUtils.FilesToAnalyze, fileToAnalyzePath);
        var config = new AnalysisConfig { SonarOutputDir = outPath };
        var sut = new ScannerEngineInputGenerator(config, logger);
        var firstProjectInfo = new ProjectInfo
        {
            ProjectGuid = Guid.NewGuid(),
            FullPath = Path.Combine(TestContext.TestRunDirectory, "First"),
            ProjectName = "First",
            AnalysisSettings = [],
            AnalysisResults = [new AnalysisResult { Id = TestUtils.FilesToAnalyze, Location = filesToAnalyzePath }]
        };
        var secondProjectInfo = new ProjectInfo
        {
            ProjectGuid = Guid.NewGuid(),
            FullPath = Path.Combine(Path.GetTempPath(), "Second"),
            ProjectName = "Second",
            AnalysisSettings = [],
            AnalysisResults = [new AnalysisResult { Id = TestUtils.FilesToAnalyze, Location = filesToAnalyzePath }]
        };
        TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "First");
        TestUtils.CreateEmptyFile(Path.GetTempPath(), "Second");

        // In order to force automatic root path detection to point to file system root,
        // create a project in the test run directory and a second one in the temp folder.
        sut.TryWriteProperties(
            config.ToAnalysisProperties(logger),
            new[] { firstProjectInfo, secondProjectInfo }.ToProjectData(true, logger),
            new PropertiesWriter(config),
            new ScannerEngineInput(config));

        logger.AssertErrorLogged("""The project base directory cannot be automatically detected. Please specify the "/d:sonar.projectBaseDir" on the begin step.""");
    }

    [TestMethod]
    public void TryWriteProperties_WhenThereAreNoValidProjects_LogsError()
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory!, ".sonarqube", "out");
        Directory.CreateDirectory(outPath);
        var config = new AnalysisConfig { SonarOutputDir = outPath };
        var sut = new ScannerEngineInputGenerator(config, logger);
        var firstProjectInfo = new ProjectInfo
        {
            ProjectGuid = Guid.NewGuid(),
            FullPath = Path.Combine(TestContext.TestRunDirectory, "First"),
            ProjectName = "First",
            IsExcluded = true,
            AnalysisSettings = [],
            AnalysisResults = []
        };
        var secondProjectInfo = new ProjectInfo
        {
            ProjectGuid = Guid.NewGuid(),
            FullPath = Path.Combine(TestContext.TestRunDirectory, "Second"),
            ProjectName = "Second",
            AnalysisSettings = [],
            AnalysisResults = []
        };
        TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "First");
        TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "Second");
        sut.TryWriteProperties(
            config.ToAnalysisProperties(logger),
            new[] { firstProjectInfo, secondProjectInfo }.ToProjectData(true, logger),
            new PropertiesWriter(config),
            new ScannerEngineInput(config));

        logger.AssertInfoLogged($"The exclude flag has been set so the project will not be analyzed. Project file: {firstProjectInfo.FullPath}");
        logger.AssertErrorLogged("No analyzable projects were found. SonarQube analysis will not be performed. Check the build summary report for details.");
    }

    [TestMethod]
    [DataRow("https://sonarcloud.io")]
    [DataRow("https://sonarqube.us")]
    [DataRow("https://sonarqqqq.whale")]    // Any value, as long as it was auto-computed by the default URL mechanism and stored in SonarQubeAnalysisConfig.xml
    public void TryWriteProperties_HostUrl_NotSet_UseSonarQubeHostUrl(string sonarQubeHost)
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory, ".sonarqube", "out");
        var config = new AnalysisConfig { SonarProjectKey = "key", SonarOutputDir = outPath, SonarQubeHostUrl = sonarQubeHost };
        var legacyWriter = new PropertiesWriter(config);
        var engineInput = new ScannerEngineInput(config);
        TryWriteProperties_HostUrl_Execute(config, legacyWriter, engineInput);

        legacyWriter.Flush().Should().Contain($"sonar.host.url={sonarQubeHost}");
        new ScannerEngineInputReader(engineInput.ToString()).AssertProperty("sonar.host.url", sonarQubeHost);
        logger.AssertDebugLogged("Setting analysis property: sonar.host.url=" + sonarQubeHost);
    }

    [TestMethod]
    public void TryWriteProperties_HostUrl_ExplicitValue_Propagated()
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory, ".sonarqube", "out");
        var config = new AnalysisConfig
        {
            SonarProjectKey = "key",
            SonarOutputDir = outPath,
            SonarQubeHostUrl = "Property should take precedence and this should not be used",
            LocalSettings = [new Property(SonarProperties.HostUrl, "http://localhost:9000")]
        };
        var legacyWriter = new PropertiesWriter(config);
        var engineInput = new ScannerEngineInput(config);
        TryWriteProperties_HostUrl_Execute(config, legacyWriter, engineInput);

        legacyWriter.Flush().Should().Contain("sonar.host.url=http://localhost:9000");
        new ScannerEngineInputReader(engineInput.ToString()).AssertProperty("sonar.host.url", "http://localhost:9000");
    }

    [TestMethod]
    public void WriteAnalyzerOutputPaths_ForUnexpectedLanguage_DoNotWritesOutPaths()
    {
        var input = new ScannerEngineInput(new());
        ScannerEngineInputGenerator.WriteAnalyzerOutputPaths(input, CreateProjectDataWithPaths("unexpected", analyzerOutPaths: [@"c:\dir1\dir2"]));

        input.ToString().Should().BeIgnoringLineEndings("""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                }
              ]
            }
            """);
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.analyzer.projectOutPaths")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.analyzer.projectOutPaths")]
    public void WriteAnalyzerOutputPaths_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var input = new ScannerEngineInput(new());
        ScannerEngineInputGenerator.WriteAnalyzerOutputPaths(input, CreateProjectDataWithPaths(
            language,
            analyzerOutPaths: [Path.Combine(TestUtils.DriveRoot(), "dir1", "first"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second")]));

        input.ToString().Should().BeIgnoringLineEndings($$"""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "5762C17D-1DDF-4C77-86AC-E2B4940926A9.{{expectedPropertyKey}}",
                  "value": {{JsonConvert.ToString(Path.Combine(TestUtils.DriveRoot(), "dir1", "first") + "," + Path.Combine(TestUtils.DriveRoot(), "dir1", "second"))}}
                }
              ]
            }
            """);
    }

    [TestMethod]
    public void WriteRoslynReportPaths_ForUnexpectedLanguage_DoNotWritesOutPaths()
    {
        var input = new ScannerEngineInput(new());
        ScannerEngineInputGenerator.WriteRoslynReportPaths(input, CreateProjectDataWithPaths("unexpected"));

        input.ToString().Should().BeIgnoringLineEndings("""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                }
              ]
            }
            """);
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.roslyn.reportFilePaths")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.roslyn.reportFilePaths")]
    public void WriteRoslynReportPaths_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var input = new ScannerEngineInput(new());
        ScannerEngineInputGenerator.WriteRoslynReportPaths(input, CreateProjectDataWithPaths(language, roslynOutPaths: [
            Path.Combine(TestUtils.DriveRoot(), "dir1", "first"),
            Path.Combine(TestUtils.DriveRoot(), "dir1", "second")]));

        input.ToString().Should().BeIgnoringLineEndings($$"""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "5762C17D-1DDF-4C77-86AC-E2B4940926A9.{{expectedPropertyKey}}",
                  "value": {{JsonConvert.ToString(Path.Combine(TestUtils.DriveRoot(), "dir1", "first") + "," + Path.Combine(TestUtils.DriveRoot(), "dir1", "second"))}}
                }
              ]
            }
            """);
    }

    [TestMethod]
    public void Telemetry_ForUnexpectedLanguage_DoNotWritePaths()
    {
        var input = new ScannerEngineInput(new());
        ScannerEngineInputGenerator.WriteTelemetryPaths(input, CreateProjectDataWithPaths("unexpected", telemetryPaths: [@"c:\dir1\dir2\Telemetry.json"]));

        input.ToString().Should().BeIgnoringLineEndings("""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                }
              ]
            }
            """);
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.scanner.telemetry")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.scanner.telemetry")]
    public void Telemetry_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var input = new ScannerEngineInput(new());
        ScannerEngineInputGenerator.WriteTelemetryPaths(input, CreateProjectDataWithPaths(language, telemetryPaths: [
            Path.Combine(TestUtils.DriveRoot(), "dir1", "first", "Telemetry.json"),
            Path.Combine(TestUtils.DriveRoot(), "dir1", "second", "Telemetry.json")]));

        input.ToString().Should().BeIgnoringLineEndings($$"""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "5762C17D-1DDF-4C77-86AC-E2B4940926A9.{{expectedPropertyKey}}",
                  "value": {{JsonConvert.ToString(Path.Combine(TestUtils.DriveRoot(), "dir1", "first", "Telemetry.json") + "," + Path.Combine(TestUtils.DriveRoot(), "dir1", "second", "Telemetry.json"))}}
                }
              ]
            }
            """);
    }

    [TestMethod]
    public void TryWriteProperties_ProjectAnalysisSettings_Propagated()
    {
        var config = new AnalysisConfig { SonarOutputDir = TestContext.TestRunDirectory };
        var input = new ScannerEngineInput(config);
        var project = CreateProjectDataWithPaths(ProjectLanguages.CSharp);
        project.Status = ProjectInfoValidity.Valid;
        project.Project.FullPath = TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "Project.proj");
        project.Project.AnalysisSettings =
        [
            new("my.setting1", "setting1"),
            new("my.setting2", "setting 2 with spaces"),
            new("my.setting.3", @"c:\dir1\dir2\foo.txt")
        ];
        project.ReferencedFiles.Add(new(TestUtils.CreateEmptyFile(TestContext.TestRunDirectory, "File.cs")));
        var sut = new ScannerEngineInputGenerator(config, logger);
        sut.TryWriteProperties(config.ToAnalysisProperties(logger), [project], new PropertiesWriter(config), input);

        logger.AssertNoErrorsLogged();
        var reader = new ScannerEngineInputReader(input.ToString());
        reader.AssertProperty("5762C17D-1DDF-4C77-86AC-E2B4940926A9.my.setting1", "setting1");
        reader.AssertProperty("5762C17D-1DDF-4C77-86AC-E2B4940926A9.my.setting2", "setting 2 with spaces");
        reader.AssertProperty("5762C17D-1DDF-4C77-86AC-E2B4940926A9.my.setting.3", @"c:\dir1\dir2\foo.txt");
    }

    private void TryWriteProperties_HostUrl_Execute(AnalysisConfig config, PropertiesWriter legacyWriter, ScannerEngineInput engineInput)
    {
        Directory.CreateDirectory(config.SonarOutputDir);
        var sut = new ScannerEngineInputGenerator(config, logger);
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
            AnalysisResults = [new AnalysisResult { Id = AnalysisType.FilesToAnalyze.ToString(), Location = filesToAnalyzePath }],
        };
        sut.TryWriteProperties(
            config.ToAnalysisProperties(logger),
            new[] { project }.ToProjectData(true, logger),
            legacyWriter,
            engineInput)
            .Should().BeTrue();
    }

    private static ProjectData CreateProjectDataWithPaths(string language, string[] analyzerOutPaths = null, string[] roslynOutPaths = null, string[] telemetryPaths = null)
    {
        analyzerOutPaths ??= [];
        roslynOutPaths ??= [];
        telemetryPaths ??= [];
        var projectData = new[] { new ProjectInfo { ProjectGuid = new("5762C17D-1DDF-4C77-86AC-E2B4940926A9"), ProjectLanguage = language } }.ToProjectData(true, Substitute.For<ILogger>()).Single();
        foreach (var path in analyzerOutPaths)
        {
            projectData.AnalyzerOutPaths.Add(new FileInfo(path));
        }
        foreach (var path in roslynOutPaths)
        {
            projectData.RoslynReportFilePaths.Add(new FileInfo(path));
        }
        foreach (var path in telemetryPaths)
        {
            projectData.TelemetryPaths.Add(new FileInfo(path));
        }
        return projectData;
    }
}
