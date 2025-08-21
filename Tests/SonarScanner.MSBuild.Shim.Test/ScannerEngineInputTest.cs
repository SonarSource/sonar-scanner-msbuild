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

[TestClass]
public class ScannerEngineInputTest
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_ConfigIsNull_ThrowsOnNullArgument() =>
        ((Func<ScannerEngineInput>)(() => new ScannerEngineInput(null))).Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");

    [TestMethod]
    public void WriteSettingsForProject_ThrowsOnNullArgument()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig());
        Action action = () => sut.WriteSettingsForProject(null);

        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectData");
    }

    [TestMethod]
    public void WriteGlobalSettings_ThrowsOnNullArgument()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig());
        Action action = () => sut.WriteGlobalSettings(null);

        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("properties");
    }

    [TestMethod]
    public void WriteGlobalSettings_VerboseIsSkipped()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig());
        sut.WriteGlobalSettings([
            new(SonarProperties.Verbose, "true"),
            new(SonarProperties.HostUrl, "http://example.org"),
        ]);
        sut.ToString().Should().BeIgnoringLineEndings("""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "sonar.host.url",
                  "value": "http://example.org"
                }
              ]
            }
            """);
    }

    [TestMethod]
    public void WriteGlobalSettings_HostUrlIsKeptIfHostUrlAndSonarcloudUrlAreSet()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig());
        sut.WriteGlobalSettings([
            new(SonarProperties.SonarcloudUrl, "http://SonarcloudUrl.org"),
            new(SonarProperties.HostUrl, "http://HostUrl.org"),
        ]);
        sut.ToString().Should().BeIgnoringLineEndings("""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "sonar.scanner.sonarcloudUrl",
                  "value": "http://SonarcloudUrl.org"
                },
                {
                  "key": "sonar.host.url",
                  "value": "http://HostUrl.org"
                }
              ]
            }
            """);
    }

    [TestMethod]
    public void WriteSharedProperties_EmptySources_EmptyTests()
    {
        var sut = new ScannerEngineInput(new());
        sut.WriteSharedFiles(new([], []));
        sut.ToString().Should().BeIgnoringLineEndings("""
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
    public void WriteSharedProperties_WithSources_EmptyTests()
    {
        var sut = new ScannerEngineInput(new());
        sut.WriteSharedFiles(new([new(Path.Combine(TestUtils.DriveRoot(), "dev", "main.hs")), new(Path.Combine(TestUtils.DriveRoot(), "dev", "lambdas.hs"))], []));
        sut.ToString().Should().BeIgnoringLineEndings($$"""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "sonar.sources",
                  "value": {{JsonConvert.ToString(Path.Combine(TestUtils.DriveRoot(), "dev", "main.hs") + "," + Path.Combine(TestUtils.DriveRoot(), "dev", "lambdas.hs"))}}
                }
              ]
            }
            """);
    }

    [TestMethod]
    public void WriteSharedProperties_EmptySources_WithTests()
    {
        var sut = new ScannerEngineInput(new());
        sut.WriteSharedFiles(new([], [new(Path.Combine(TestUtils.DriveRoot(), "dev", "test.hs")), new(Path.Combine(TestUtils.DriveRoot(), "dev", "test2.hs"))]));
        sut.ToString().Should().BeIgnoringLineEndings($$"""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "sonar.tests",
                  "value": {{JsonConvert.ToString(Path.Combine(TestUtils.DriveRoot(), "dev", "test.hs") + "," + Path.Combine(TestUtils.DriveRoot(), "dev", "test2.hs"))}}
                }
              ]
            }
            """);
    }

    [TestMethod]
    public void WriteSharedProperties_WithSources_WithTests()
    {
        var sut = new ScannerEngineInput(new());
        sut.WriteSharedFiles(new([new(Path.Combine(TestUtils.DriveRoot(), "dev", "main.hs"))], [new(Path.Combine(TestUtils.DriveRoot(), "dev", "test.hs"))]));
        sut.ToString().Should().BeIgnoringLineEndings($$"""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "sonar.sources",
                  "value": {{JsonConvert.ToString(Path.Combine(TestUtils.DriveRoot(), "dev", "main.hs"))}}
                },
                {
                  "key": "sonar.tests",
                  "value": {{JsonConvert.ToString(Path.Combine(TestUtils.DriveRoot(), "dev", "test.hs"))}}
                }
              ]
            }
            """);
    }

    [TestMethod]
    public void WriteAnalyzerOutputPaths_ForUnexpectedLanguage_DoNotWritesOutPaths()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig());
        sut.WriteAnalyzerOutputPaths(CreateTestProjectDataWithPaths("unexpected", analyzerOutPaths: [@"c:\dir1\dir2"]));

        sut.ToString().Should().BeIgnoringLineEndings("""
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
        var sut = new ScannerEngineInput(new AnalysisConfig());
        sut.WriteAnalyzerOutputPaths(CreateTestProjectDataWithPaths(
            language,
            analyzerOutPaths: [Path.Combine(TestUtils.DriveRoot(), "dir1", "first"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second")]));
        sut.ToString().Should().BeIgnoringLineEndings($$"""
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
        var sut = new ScannerEngineInput(new AnalysisConfig());
        sut.WriteRoslynReportPaths(CreateTestProjectDataWithPaths("unexpected"));
        sut.ToString().Should().BeIgnoringLineEndings("""
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
        var sut = new ScannerEngineInput(new AnalysisConfig());
        sut.WriteRoslynReportPaths(CreateTestProjectDataWithPaths(
            language,
            roslynOutPaths: [Path.Combine(TestUtils.DriveRoot(), "dir1", "first"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second")]));
        sut.ToString().Should().BeIgnoringLineEndings($$"""
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
    public void WriteVsTestReportPaths_WritesEncodedPaths()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig());
        sut.WriteVsTestReportPaths([Path.Combine(TestUtils.DriveRoot(), "dir1", "first"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second")]);
        sut.ToString().Should().BeIgnoringLineEndings($$"""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "sonar.cs.vstest.reportsPaths",
                  "value": {{JsonConvert.ToString(Path.Combine(TestUtils.DriveRoot(), "dir1", "first") + "," + Path.Combine(TestUtils.DriveRoot(), "dir1", "second"))}}
                }
              ]
            }
            """);
    }

    [TestMethod]
    public void WriteVsXmlCoverageReportPaths_WritesEncodedPaths()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig());
        sut.WriteVsXmlCoverageReportPaths([Path.Combine(TestUtils.DriveRoot(), "dir1", "first"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second")]);
        sut.ToString().Should().BeIgnoringLineEndings($$"""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "sonar.cs.vscoveragexml.reportsPaths",
                  "value": {{JsonConvert.ToString(Path.Combine(TestUtils.DriveRoot(), "dir1", "first") + "," + Path.Combine(TestUtils.DriveRoot(), "dir1", "second"))}}
                }
              ]
            }
            """);
    }

    [TestMethod]
    public void Telemetry_ForUnexpectedLanguage_DoNotWritePaths()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig());

        sut.WriteTelemetryPaths(CreateTestProjectDataWithPaths("unexpected", telemetryPaths: [@"c:\dir1\dir2\Telemetry.json"]));

        sut.ToString().Should().BeIgnoringLineEndings("""
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
        var sut = new ScannerEngineInput(new AnalysisConfig());
        sut.WriteTelemetryPaths(CreateTestProjectDataWithPaths(
            language,
            telemetryPaths: [Path.Combine(TestUtils.DriveRoot(), "dir1", "first", "Telemetry.json"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second", "Telemetry.json")]));
        sut.ToString().Should().BeIgnoringLineEndings($$"""
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
    public void JsonBuilderToString()
    {
        var productBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "JsonbuilderTest_ProductBaseDir");
        var productProject = CreateEmptyFile(productBaseDir, "MyProduct.csproj");
        var productFile = CreateEmptyFile(productBaseDir, "File.cs");
        var productChineseFile = CreateEmptyFile(productBaseDir, "你好.cs");

        var productCoverageFilePath = CreateEmptyFile(productBaseDir, "productCoverageReport.txt").FullName;
        CreateEmptyFile(productBaseDir, "productTrx.trx");
        var productFileListFilePath = Path.Combine(productBaseDir, "productManagedFiles.txt");

        var otherDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "JsonbuilderTest_OtherDir");
        var missingFileOutsideProjectDir = new FileInfo(Path.Combine(otherDir, "missing.cs"));

        var productFiles = new List<FileInfo>
        {
            productFile,
            productChineseFile,
            missingFileOutsideProjectDir
        };
        var productCS = new ProjectData(CreateProjectInfo("你好", "DB2E5521-3172-47B9-BA50-864F12E6DFFF", productProject, false, productFiles, productFileListFilePath, productCoverageFilePath, ProjectLanguages.CSharp, "UTF-8"));
        productCS.SonarQubeModuleFiles.Add(productFile);
        productCS.SonarQubeModuleFiles.Add(productChineseFile);
        productCS.SonarQubeModuleFiles.Add(missingFileOutsideProjectDir);

        var productVB = new ProjectData(CreateProjectInfo("vbProject", "B51622CF-82F4-48C9-9F38-FB981FAFAF3A", productProject, false, productFiles, productFileListFilePath, productCoverageFilePath, ProjectLanguages.VisualBasic, "UTF-8"));
        productVB.SonarQubeModuleFiles.Add(productFile);

        var testBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "JsonbuilderTest_TestBaseDir");
        var testProject = CreateEmptyFile(testBaseDir, "MyTest.csproj");
        var testFile = CreateEmptyFile(testBaseDir, "File.cs");
        var testFileListFilePath = Path.Combine(testBaseDir, "testManagedFiles.txt");

        var testFiles = new List<FileInfo>
        {
            testFile
        };
        var test = new ProjectData(CreateProjectInfo("my_test_project", "DA0FCD82-9C5C-4666-9370-C7388281D49B", testProject, true, testFiles, testFileListFilePath, null, ProjectLanguages.VisualBasic, "UTF-8"));
        test.SonarQubeModuleFiles.Add(testFile);

        var sonarOutputDir = @"C:\my_folder";

        var config = new AnalysisConfig
        {
            SonarProjectKey = "my_project_key",
            SonarProjectName = "my_project_name",
            SonarProjectVersion = "1.0",
            SonarOutputDir = sonarOutputDir,
            SourcesDirectory = @"d:\source_files\"
        };

        string actual = null;
        using (new AssertIgnoreScope()) // expecting the property writer to complain about the missing file
        {
            var sut = new ScannerEngineInput(config);
            sut.WriteSettingsForProject(productCS);
            sut.WriteSettingsForProject(productVB);
            sut.WriteSettingsForProject(test);

            actual = sut.ToString();
        }

        var expected = $$"""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": "DB2E5521-3172-47B9-BA50-864F12E6DFFF,B51622CF-82F4-48C9-9F38-FB981FAFAF3A,DA0FCD82-9C5C-4666-9370-C7388281D49B"
                },
                {
                  "key": "DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectKey",
                  "value": "my_project_key:DB2E5521-3172-47B9-BA50-864F12E6DFFF"
                },
                {
                  "key": "DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectName",
                  "value": "你好"
                },
                {
                  "key": "DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectBaseDir",
                  "value": {{JsonConvert.ToString(productBaseDir)}}
                },
                {
                  "key": "DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.sourceEncoding",
                  "value": "utf-8"
                },
                {
                  "key": "DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.tests",
                  "value": ""
                },
                {
                  "key": "DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.sources",
                  "value": {{JsonConvert.ToString(Path.Combine(productBaseDir, "File.cs") + "," + Path.Combine(productBaseDir, "你好.cs") + "," + missingFileOutsideProjectDir.FullName)}}
                },
                {
                  "key": "DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.working.directory",
                  "value": {{JsonConvert.ToString(Path.Combine(sonarOutputDir, ".sonar", "mod0"))}}
                },
                {
                  "key": "B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectKey",
                  "value": "my_project_key:B51622CF-82F4-48C9-9F38-FB981FAFAF3A"
                },
                {
                  "key": "B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectName",
                  "value": "vbProject"
                },
                {
                  "key": "B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectBaseDir",
                  "value": {{JsonConvert.ToString(productBaseDir)}}
                },
                {
                  "key": "B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.sourceEncoding",
                  "value": "utf-8"
                },
                {
                  "key": "B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.tests",
                  "value": ""
                },
                {
                  "key": "B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.sources",
                  "value": {{JsonConvert.ToString(Path.Combine(productBaseDir, "File.cs"))}}
                },
                {
                  "key": "B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.working.directory",
                  "value": {{JsonConvert.ToString(Path.Combine(sonarOutputDir, ".sonar", "mod1"))}}
                },
                {
                  "key": "DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectKey",
                  "value": "my_project_key:DA0FCD82-9C5C-4666-9370-C7388281D49B"
                },
                {
                  "key": "DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectName",
                  "value": "my_test_project"
                },
                {
                  "key": "DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectBaseDir",
                  "value": {{JsonConvert.ToString(testBaseDir)}}
                },
                {
                  "key": "DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.sourceEncoding",
                  "value": "utf-8"
                },
                {
                  "key": "DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.sources",
                  "value": ""
                },
                {
                  "key": "DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.tests",
                  "value": {{JsonConvert.ToString(Path.Combine(testBaseDir, "File.cs"))}}
                },
                {
                  "key": "DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.working.directory",
                  "value": {{JsonConvert.ToString(Path.Combine(sonarOutputDir, ".sonar", "mod2"))}}
                }
              ]
            }
            """;
        actual.Should().BeIgnoringLineEndings(expected);
    }

    [TestMethod]
    public void WriteSonarProjectInfo_WritesAllValues()
    {
        var projectBaseDir = Path.Combine(TestUtils.DriveRoot(), "ProjectBaseDir");
        var sonarOutputDir = @"C:\OutpuDir";
        var sonarDir = Path.Combine(sonarOutputDir, ".sonar");
        var config = new AnalysisConfig
        {
            SonarProjectKey = "my_project_key",
            SonarProjectName = "my_project_name",
            SonarProjectVersion = "4.2",
            SonarOutputDir = sonarOutputDir,
        };
        config.SetConfigValue(SonarProperties.PullRequestCacheBasePath, @"C:\PullRequest\Cache\BasePath");
        var sut = new ScannerEngineInput(config);
        sut.WriteSonarProjectInfo(new DirectoryInfo(projectBaseDir));

        sut.ToString().Should().BeIgnoringLineEndings(
            $$"""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "sonar.projectKey",
                  "value": "my_project_key"
                },
                {
                  "key": "sonar.projectName",
                  "value": "my_project_name"
                },
                {
                  "key": "sonar.projectVersion",
                  "value": "4.2"
                },
                {
                  "key": "sonar.working.directory",
                  "value": {{JsonConvert.ToString(Path.Combine(sonarOutputDir, ".sonar"))}}
                },
                {
                  "key": "sonar.projectBaseDir",
                  "value": {{JsonConvert.ToString(projectBaseDir)}}
                },
                {
                  "key": "sonar.pullrequest.cache.basepath",
                  "value": "C:\\PullRequest\\Cache\\BasePath"
                }
              ]
            }
            """);
    }

    [TestMethod]
    public void WriteSonarProjectInfo_EmptyValues()
    {
        var config = new AnalysisConfig { SonarOutputDir = @"C:\OutputDir\CannotBeEmpty" };
        var sut = new ScannerEngineInput(config);
        sut.WriteSonarProjectInfo(new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "ProjectBaseDir")));
        sut.ToString().Should().BeIgnoringLineEndings(
            $$"""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "sonar.projectKey",
                  "value": null
                },
                {
                  "key": "sonar.working.directory",
                  "value": {{JsonConvert.ToString(Path.Combine(@"C:\OutputDir\CannotBeEmpty", ".sonar"))}}
                },
                {
                  "key": "sonar.projectBaseDir",
                  "value": {{JsonConvert.ToString(Path.Combine(TestUtils.DriveRoot(), "ProjectBaseDir"))}}
                },
                {
                  "key": "sonar.pullrequest.cache.basepath",
                  "value": null
                }
              ]
            }
            """);
    }

    // Tests that analysis settings in the ProjectInfo are written to the file
    [TestMethod]
    public void AnalysisSettingsWritten()
    {
        var projectBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "ScannerEngineInputBuilderTest_AnalysisSettingsWritten");
        var productProject = CreateEmptyFile(projectBaseDir, "MyProduct.csproj");
        var productFile = CreateEmptyFile(projectBaseDir, "File.cs");
        var productFiles = new List<FileInfo>
        {
            productFile
        };
        var productFileListFilePath = Path.Combine(projectBaseDir, "productManagedFiles.txt");

        var product = new ProjectData(CreateProjectInfo("AnalysisSettingsTest.proj", "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5", productProject, false, productFiles, productFileListFilePath, null, "language", "UTF-8"));

        // These are the settings we are going to check. The other analysis values are not checked.
        product.Project.AnalysisSettings =
        [
            new("my.setting1", "setting1"),
            new("my.setting2", "setting 2 with spaces"),
            new("my.setting.3", @"c:\dir1\dir2\foo.txt")
        ];
        product.ReferencedFiles.Add(productFile);

        var sut = new ScannerEngineInput(new AnalysisConfig { SonarOutputDir = @"C:\my_folder" });
        sut.WriteSettingsForProject(product);

        var reader = new ScannerEngineInputReader(sut.ToString());
        reader.AssertProperty("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting1", "setting1");
        reader.AssertProperty("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting2", "setting 2 with spaces");
        reader.AssertProperty("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting.3", @"c:\dir1\dir2\foo.txt");
    }

    // Tests that .sonar.working.directory is explicitly set per module
    [TestMethod]
    public void WorkdirPerModuleExplicitlySet()
    {
        var projectBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "JsonbuilderTest_AnalysisSettingsWritten");
        var productProject = CreateEmptyFile(projectBaseDir, "MyProduct.csproj");

        var productFile = CreateEmptyFile(projectBaseDir, "File.cs");
        var productFiles = new List<FileInfo>
        {
            productFile
        };
        var productFileListFilePath = Path.Combine(projectBaseDir, "productManagedFiles.txt");

        var projectKey = "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5";
        var product = new ProjectData(CreateProjectInfo("AnalysisSettingsTest.proj", projectKey, productProject, false, productFiles, productFileListFilePath, null, "language", "UTF-8"));
        product.ReferencedFiles.Add(productFile);

        var config = new AnalysisConfig
        {
            SonarOutputDir = Path.Combine(TestUtils.DriveRoot(), "my_folder")
        };

        var sut = new ScannerEngineInput(config);
        sut.WriteSettingsForProject(product);
        sut.WriteSonarProjectInfo(new DirectoryInfo("dummy basedir"));

        var workDirKey = projectKey + "." + SonarProperties.WorkingDirectory;
        new ScannerEngineInputReader(sut.ToString()).AssertProperty(workDirKey, Path.Combine(TestUtils.DriveRoot(), "my_folder", ".sonar", "mod0"));
    }

    // Tests that global settings in the ProjectInfo are written to the file
    [TestMethod]
    public void GlobalSettingsWritten()
    {
        var globalSettings = new AnalysisProperties
        {
            new("my.setting1", "setting1"),
            new("my.setting2", "setting 2 with spaces"),
            new("my.setting.3", @"c:\dir1\dir2\foo.txt"),
            // Specific test for sonar.branch property
            new("sonar.branch", "aBranch")
        };

        var sut = new ScannerEngineInput(new AnalysisConfig { SonarOutputDir = @"C:\my_folder" });
        sut.WriteGlobalSettings(globalSettings);

        var reader = new ScannerEngineInputReader(sut.ToString());
        reader.AssertProperty("my.setting1", "setting1");
        reader.AssertProperty("my.setting2", "setting 2 with spaces");
        reader.AssertProperty("my.setting.3", @"c:\dir1\dir2\foo.txt");
        reader.AssertProperty("sonar.branch", "aBranch");
    }

    [TestMethod]
    public void AsMultiValueProperty_EncodeValues()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig());
        sut.AppendKeyValue(
            "sonar",
            "multivalueproperty",
            [
                "Normal value without double quotes @#$%^&*()",
                "With \r Carriage Return",
                "With \n Line Feed",
                "Normal value",
                "With , Comma",
                "With \" Double Quote",
                "Normal value"
            ]);
        sut.ToString().Should().BeIgnoringLineEndings("""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "sonar.multivalueproperty",
                  "value": "Normal value without double quotes @#$%^&*(),\"With \r Carriage Return\",\"With \n Line Feed\",Normal value,\"With , Comma\",\"With \"\" Double Quote\",Normal value"
                }
              ]
            }
            """);
    }

    private static ProjectInfo CreateProjectInfo(
        string name,
        string projectId,
        FileInfo fullFilePath,
        bool isTest,
        IEnumerable<FileInfo> files,
        string fileListFilePath,
        string coverageReportPath,
        string language,
        string encoding)
    {
        var projectInfo = new ProjectInfo
        {
            ProjectName = name,
            ProjectGuid = Guid.Parse(projectId),
            FullPath = fullFilePath.FullName,
            ProjectType = isTest ? ProjectType.Test : ProjectType.Product,
            AnalysisResults = [],
            ProjectLanguage = language,
            Encoding = encoding
        };
        if (coverageReportPath is not null)
        {
            projectInfo.AddAnalyzerResult(AnalysisType.VisualStudioCodeCoverage, coverageReportPath);
        }
        if (files is not null && files.Any())
        {
            fileListFilePath.Should().NotBeNullOrWhiteSpace("Test setup error: must supply the managedFileListFilePath as a list of files has been supplied");
            File.WriteAllLines(fileListFilePath, files.Select(x => x.FullName));
            projectInfo.AddAnalyzerResult(AnalysisType.FilesToAnalyze, fileListFilePath);
        }
        return projectInfo;
    }

    private static FileInfo CreateEmptyFile(string parentDir, string fileName) =>
        new(CreateFile(parentDir, fileName, string.Empty));

    private static string CreateFile(string parentDir, string fileName, string content)
    {
        var fullPath = Path.Combine(parentDir, fileName);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    private static ProjectData CreateTestProjectDataWithPaths(string language, string[] analyzerOutPaths = null, string[] roslynOutPaths = null, string[] telemetryPaths = null)
    {
        analyzerOutPaths ??= [];
        roslynOutPaths ??= [];
        telemetryPaths ??= [];
        var projectData = new ProjectData(new ProjectInfo
        {
            ProjectGuid = new Guid("5762C17D-1DDF-4C77-86AC-E2B4940926A9"),
            ProjectLanguage = language
        });
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
