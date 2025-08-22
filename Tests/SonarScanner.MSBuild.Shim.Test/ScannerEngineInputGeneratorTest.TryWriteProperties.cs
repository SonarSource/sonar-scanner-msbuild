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

public partial class ScannerEngineInputGeneratorTest
{
    [TestMethod]
    public void TryWriteProperties_WhenThereIsNoCommonPath_LogsError()
    {
        var outPath = Path.Combine(TestContext.TestRunDirectory!, ".sonarqube", "out");
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
        sut.TryWriteProperties(new PropertiesWriter(config), new ScannerEngineInput(config), [firstProjectInfo, secondProjectInfo], out _);

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
        sut.TryWriteProperties(new PropertiesWriter(config), new ScannerEngineInput(config), [firstProjectInfo, secondProjectInfo], out _);

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
        sut.TryWriteProperties(legacyWriter, engineInput, [project], out _).Should().BeTrue();
    }
}
