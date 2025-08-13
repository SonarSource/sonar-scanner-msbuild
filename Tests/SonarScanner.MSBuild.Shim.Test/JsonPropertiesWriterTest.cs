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
using System.Text.Json.Nodes;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class JsonPropertiesWriterTest
{
    public TestContext TestContext { get; set; }
    private static string LineSeparator => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? """\r\n""" : """\n""";

    [TestMethod]
    public void Constructor_ConfigIsNull_ThrowsOnNullArgument()
    {
        Action action = () => new JsonPropertiesWriter(null, new TestLogger());

        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
    }

    [TestMethod]
    public void Constructor_LoggerIsNull_ThrowsOnNullArgument()
    {
        Action action = () => new JsonPropertiesWriter(new AnalysisConfig(), null);

        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void WriteSettingsForProject_ThrowsOnNullArgument()
    {
        var propertiesWriter = new JsonPropertiesWriter(new AnalysisConfig(), new TestLogger());
        Action action = () => propertiesWriter.WriteSettingsForProject(null);

        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectData");
    }

    [TestMethod]
    public void WriteGlobalSettings_ThrowsOnNullArgument()
    {
        var propertiesWriter = new JsonPropertiesWriter(new AnalysisConfig(), new TestLogger());
        Action action = () => propertiesWriter.WriteGlobalSettings(null);

        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("properties");
    }

    [TestMethod]
    public void WriteGlobalSettings_VerboseIsSkipped()
    {
        var propertiesWriter = new JsonPropertiesWriter(new AnalysisConfig(), new TestLogger());
        propertiesWriter.WriteGlobalSettings([
            new(SonarProperties.Verbose, "true"),
            new(SonarProperties.HostUrl, "http://example.org"),
        ]);
        propertiesWriter.Flush().Should().BeIgnoringLineEndings("""
            [
              {
                "key": "sonar.host.url",
                "value": "http://example.org"
              },
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    public void WriteGlobalSettings_HostUrlIsKeptIfHostUrlAndSonarcloudUrlAreSet()
    {
        var propertiesWriter = new JsonPropertiesWriter(new AnalysisConfig(), new TestLogger());
        propertiesWriter.WriteGlobalSettings([
            new(SonarProperties.SonarcloudUrl, "http://SonarcloudUrl.org"),
            new(SonarProperties.HostUrl, "http://HostUrl.org"),
        ]);
        propertiesWriter.Flush().Should().BeIgnoringLineEndings("""
            [
              {
                "key": "sonar.scanner.sonarcloudUrl",
                "value": "http://SonarcloudUrl.org"
              },
              {
                "key": "sonar.host.url",
                "value": "http://HostUrl.org"
              },
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    public void WriteSharedProperties_EmptySources_EmptyTests()
    {
        var propertiesWriter = new JsonPropertiesWriter(new(), new TestLogger());
        propertiesWriter.WriteSharedFiles(new([], []));
        propertiesWriter.Flush().Should().BeIgnoringLineEndings("""
            [
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    public void WriteSharedProperties_WithSources_EmptyTests()
    {
        var propertiesWriter = new JsonPropertiesWriter(new(), new TestLogger());
        propertiesWriter.WriteSharedFiles(new([new(Path.Combine(TestUtils.DriveRoot(), "dev", "main.hs")), new(Path.Combine(TestUtils.DriveRoot(), "dev", "lambdas.hs"))], []));
        propertiesWriter.Flush().Should().BeIgnoringLineEndings($$"""
            [
              {
                "key": "sonar.sources",
                "value": "{{PropertiesPath(TestUtils.DriveRoot(), "dev", "main.hs")}},\\{{LineSeparator}}{{PropertiesPath(TestUtils.DriveRoot(), "dev", "lambdas.hs")}}"
              },
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    public void WriteSharedProperties_EmptySources_WithTests()
    {
        var propertiesWriter = new JsonPropertiesWriter(new(), new TestLogger());
        propertiesWriter.WriteSharedFiles(new([], [new(Path.Combine(TestUtils.DriveRoot(), "dev", "test.hs")), new(Path.Combine(TestUtils.DriveRoot(), "dev", "test2.hs"))]));
        propertiesWriter.Flush().Should().BeIgnoringLineEndings($$"""
            [
              {
                "key": "sonar.tests",
                "value": "{{PropertiesPath(TestUtils.DriveRoot(), "dev", "test.hs")}},\\{{LineSeparator}}{{PropertiesPath(TestUtils.DriveRoot(), "dev", "test2.hs")}}"
              },
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    public void WriteSharedProperties_WithSources_WithTests()
    {
        var propertiesWriter = new JsonPropertiesWriter(new(), new TestLogger());
        propertiesWriter.WriteSharedFiles(new([new(Path.Combine(TestUtils.DriveRoot(), "dev", "main.hs"))], [new(Path.Combine(TestUtils.DriveRoot(), "dev", "test.hs"))]));
        propertiesWriter.Flush().Should().BeIgnoringLineEndings($$"""
            [
              {
                "key": "sonar.sources",
                "value": "{{PropertiesPath(TestUtils.DriveRoot(), "dev", "main.hs")}}"
              },
              {
                "key": "sonar.tests",
                "value": "{{PropertiesPath(TestUtils.DriveRoot(), "dev", "test.hs")}}"
              },
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    public void WriteAnalyzerOutputPaths_ForUnexpectedLanguage_DoNotWritesOutPaths()
    {
        var propertiesWriter = new JsonPropertiesWriter(new AnalysisConfig(), new TestLogger());
        propertiesWriter.WriteAnalyzerOutputPaths(CreateTestProjectDataWithPaths("unexpected", analyzerOutPaths: [@"c:\dir1\dir2"]));

        propertiesWriter.Flush().Should().BeIgnoringLineEndings("""
            [
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.analyzer.projectOutPaths")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.analyzer.projectOutPaths")]
    public void WriteAnalyzerOutputPaths_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var propertiesWriter = new JsonPropertiesWriter(new AnalysisConfig(), new TestLogger());
        propertiesWriter.WriteAnalyzerOutputPaths(CreateTestProjectDataWithPaths(
            language,
            analyzerOutPaths: [Path.Combine(TestUtils.DriveRoot(), "dir1", "first"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second")]));
        propertiesWriter.Flush().Should().BeIgnoringLineEndings($$"""
            [
              {
                "key": "5762C17D-1DDF-4C77-86AC-E2B4940926A9.{{expectedPropertyKey}}",
                "value": "{{PropertiesPath(TestUtils.DriveRoot(), "dir1", "first")}},\\{{LineSeparator}}{{PropertiesPath(TestUtils.DriveRoot(), "dir1", "second")}}"
              },
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    public void WriteRoslynReportPaths_ForUnexpectedLanguage_DoNotWritesOutPaths()
    {
        var propertiesWriter = new JsonPropertiesWriter(new AnalysisConfig(), new TestLogger());
        propertiesWriter.WriteRoslynReportPaths(CreateTestProjectDataWithPaths("unexpected"));

        propertiesWriter.Flush().Should().BeIgnoringLineEndings(
            """
            [
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.roslyn.reportFilePaths")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.roslyn.reportFilePaths")]
    public void WriteRoslynReportPaths_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var propertiesWriter = new JsonPropertiesWriter(new AnalysisConfig(), new TestLogger());
        propertiesWriter.WriteRoslynReportPaths(CreateTestProjectDataWithPaths(
            language,
            roslynOutPaths: [Path.Combine(TestUtils.DriveRoot(), "dir1", "first"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second")]));
        propertiesWriter.Flush().Should().BeIgnoringLineEndings($$"""
            [
              {
                "key": "5762C17D-1DDF-4C77-86AC-E2B4940926A9.{{expectedPropertyKey}}",
                "value": "{{PropertiesPath(TestUtils.DriveRoot(), "dir1", "first")}},\\{{LineSeparator}}{{PropertiesPath(TestUtils.DriveRoot(), "dir1", "second")}}"
              },
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    public void Telemetry_ForUnexpectedLanguage_DoNotWritePaths()
    {
        var propertiesWriter = new JsonPropertiesWriter(new AnalysisConfig(), new TestLogger());

        propertiesWriter.WriteTelemetryPaths(CreateTestProjectDataWithPaths("unexpected", telemetryPaths: [@"c:\dir1\dir2\Telemetry.json"]));

        propertiesWriter.Flush().Should().BeIgnoringLineEndings("""
            [
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.scanner.telemetry")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.scanner.telemetry")]
    public void Telemetry_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var propertiesWriter = new JsonPropertiesWriter(new AnalysisConfig(), new TestLogger());
        propertiesWriter.WriteTelemetryPaths(CreateTestProjectDataWithPaths(
            language,
            telemetryPaths: [Path.Combine(TestUtils.DriveRoot(), "dir1", "first", "Telemetry.json"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second", "Telemetry.json")]));
        propertiesWriter.Flush().Should().BeIgnoringLineEndings($$"""
            [
              {
                "key": "5762C17D-1DDF-4C77-86AC-E2B4940926A9.{{expectedPropertyKey}}",
                "value": "{{PropertiesPath(TestUtils.DriveRoot(), "dir1", "first", "Telemetry.json")}},\\{{LineSeparator}}{{PropertiesPath(TestUtils.DriveRoot(), "dir1", "second", "Telemetry.json")}}"
              },
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    public void JsonPropertiesWriterToString()
    {
        var productBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "JsonPropertiesWriterTest_ProductBaseDir").Replace("""\""", """\\""");
        var productProject = CreateEmptyFile(productBaseDir, "MyProduct.csproj");
        var productFile = CreateEmptyFile(productBaseDir, "File.cs");
        var productChineseFile = CreateEmptyFile(productBaseDir, "你好.cs");

        var productCoverageFilePath = CreateEmptyFile(productBaseDir, "productCoverageReport.txt").FullName;
        CreateEmptyFile(productBaseDir, "productTrx.trx");
        var productFileListFilePath = Path.Combine(productBaseDir, "productManagedFiles.txt");

        var otherDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "JsonPropertiesWriterTest_OtherDir");
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

        var testBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "JsonPropertiesWriterTest_TestBaseDir").Replace("""\""", """\\""");
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
            var writer = new JsonPropertiesWriter(config, new TestLogger());
            writer.WriteSettingsForProject(productCS);
            writer.WriteSettingsForProject(productVB);
            writer.WriteSettingsForProject(test);

            actual = writer.Flush();
        }

        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"\\" : "/";
        var expected = $$"""
            [
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
                "value": "{{productBaseDir}}"
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
                "value": "{{productBaseDir}}{{separator}}File.cs,\\{{LineSeparator}}{{productBaseDir}}{{separator}}你好.cs,\\{{LineSeparator}}{{missingFileOutsideProjectDir.FullName.Replace("""\""", """\\""")}}"
              },
              {
                "key": "DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.working.directory",
                "value": "{{Path.Combine(sonarOutputDir, ".sonar", "mod0").Replace("""\""", """\\""")}}"
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
                "value": "{{productBaseDir}}"
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
                "value": "{{productBaseDir}}{{separator}}File.cs"
              },
              {
                "key": "B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.working.directory",
                "value": "{{Path.Combine(sonarOutputDir, ".sonar", "mod1").Replace("""\""", """\\""")}}"
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
                "value": "{{testBaseDir}}"
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
                "value": "{{testBaseDir}}{{separator}}File.cs"
              },
              {
                "key": "DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.working.directory",
                "value": "{{Path.Combine(sonarOutputDir, ".sonar", "mod2").Replace("""\""", """\\""")}}"
              },
              {
                "key": "sonar.modules",
                "value": "DB2E5521-3172-47B9-BA50-864F12E6DFFF,B51622CF-82F4-48C9-9F38-FB981FAFAF3A,DA0FCD82-9C5C-4666-9370-C7388281D49B"
              }
            ]
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
        var writer = new JsonPropertiesWriter(config, Substitute.For<ILogger>());
        writer.WriteSonarProjectInfo(new DirectoryInfo(projectBaseDir));

        writer.Flush().Should().BeIgnoringLineEndings(
            $$"""
            [
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
                "value": "{{Path.Combine(sonarOutputDir, ".sonar").Replace("""\""", """\\""")}}"
              },
              {
                "key": "sonar.projectBaseDir",
                "value": "{{projectBaseDir.Replace("""\""", """\\""")}}"
              },
              {
                "key": "sonar.pullrequest.cache.basepath",
                "value": "C:\\PullRequest\\Cache\\BasePath"
              },
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    public void WriteSonarProjectInfo_EmptyValues()
    {
        var config = new AnalysisConfig { SonarOutputDir = @"C:\OutputDir\CannotBeEmpty" };
        var writer = new JsonPropertiesWriter(config, Substitute.For<ILogger>());
        writer.WriteSonarProjectInfo(new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "ProjectBaseDir")));
        writer.Flush().Should().BeIgnoringLineEndings(
            $$"""
            [
              {
                "key": "sonar.projectKey",
                "value": ""
              },
              {
                "key": "sonar.working.directory",
                "value": "C:\\OutputDir\\CannotBeEmpty{{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"\\" : "/")}}.sonar"
              },
              {
                "key": "sonar.projectBaseDir",
                "value": "{{PropertiesPath(TestUtils.DriveRoot(), "ProjectBaseDir")}}"
              },
              {
                "key": "sonar.pullrequest.cache.basepath",
                "value": ""
              },
              {
                "key": "sonar.modules",
                "value": ""
              }
            ]
            """);
    }

    [TestMethod]
    public void Flush_WhenCalledTwice_ThrowsInvalidOperationException()
    {
        var writer = new JsonPropertiesWriter(
            new AnalysisConfig
            {
                SonarProjectKey = "key",
                SonarProjectName = "name",
                SonarProjectVersion = "1.0",
                SonarOutputDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext)
            },
            new TestLogger());
        writer.Flush();

        Action act = () => writer.Flush();
        act.Should().ThrowExactly<InvalidOperationException>();
    }

    [TestMethod]
    public void WriteSettingsForProject_WhenFlushed_ThrowsInvalidOperationException()
    {
        var writer = new JsonPropertiesWriter(
            new AnalysisConfig
            {
                SonarProjectKey = "key",
                SonarProjectName = "name",
                SonarProjectVersion = "1.0",
                SonarOutputDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext)
            },
            new TestLogger());
        writer.Flush();

        using (new AssertIgnoreScope())
        {
            Action act = () => writer.WriteSettingsForProject(new ProjectData(new ProjectInfo()));
            act.Should().ThrowExactly<InvalidOperationException>();
        }
    }

    // Tests that analysis settings in the ProjectInfo are written to the file
    [TestMethod]
    public void JsonPropertiesWriter_AnalysisSettingsWritten()
    {
        var projectBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "JsonPropertiesWriterTest_AnalysisSettingsWritten");
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

        var writer = new JsonPropertiesWriter(new AnalysisConfig { SonarOutputDir = @"C:\my_folder" }, new TestLogger());
        writer.WriteSettingsForProject(product);

        var jsonProperties = JsonPropertiesReader(SaveToResultFile(projectBaseDir, "Actual.json", writer.Flush()));
        PropertyWithValueExists(jsonProperties, "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting1", "setting1").Should().BeTrue();
        PropertyWithValueExists(jsonProperties, "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting2", "setting 2 with spaces").Should().BeTrue();
        PropertyWithValueExists(jsonProperties, "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting.3", @"c:\dir1\dir2\foo.txt").Should().BeTrue();
    }

    // Tests that .sonar.working.directory is explicitly set per module
    [TestMethod]
    public void JsonPropertiesWriter_WorkdirPerModuleExplicitlySet()
    {
        var projectBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "JsonPropertiesWriterTest_AnalysisSettingsWritten");
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
            SonarOutputDir = @"C:\my_folder"
        };

        var writer = new JsonPropertiesWriter(config, new TestLogger());
        writer.WriteSettingsForProject(product);
        writer.WriteSonarProjectInfo(new DirectoryInfo("dummy basedir"));
        var jsonString = writer.Flush();

        // ToDo: https://sonarsource.atlassian.net/browse/SCAN4NET-747
        // The following code, checks if a key exists in the properties.
        // It should be re-written and extracted in the "JsonPropertiesReader" class. It's going to be the equivalent of the "SQPropertiesFileReader" but for JSON property files.
        var jsonArray = (JsonArray)JsonNode.Parse(jsonString);
        var workDirKey = projectKey + "." + SonarProperties.WorkingDirectory;
        var found = false;
        foreach (var item in jsonArray)
        {
            if (item is JsonObject jsonObject)
            {
                if (jsonObject.First().Value?.ToString() == workDirKey)
                {
                    found = true;
                    break;
                }
            }
        }
        found.Should().BeTrue();
    }

    // Tests that global settings in the ProjectInfo are written to the file
    [TestMethod]
    public void JsonPropertiesWriter_GlobalSettingsWritten()
    {
        var globalSettings = new AnalysisProperties
        {
            new("my.setting1", "setting1"),
            new("my.setting2", "setting 2 with spaces"),
            new("my.setting.3", @"c:\dir1\dir2\foo.txt"),
            // Specific test for sonar.branch property
            new("sonar.branch", "aBranch")
        };

        var writer = new JsonPropertiesWriter(new AnalysisConfig { SonarOutputDir = @"C:\my_folder" }, new TestLogger());
        writer.WriteGlobalSettings(globalSettings);

        var jsonProperties = JsonPropertiesReader(SaveToResultFile(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "JsonPropertiesWriterTest_GlobalSettingsWritten"), "Actual.txt", writer.Flush()));
        PropertyWithValueExists(jsonProperties, "my.setting1", "setting1").Should().BeTrue();
        PropertyWithValueExists(jsonProperties, "my.setting2", "setting 2 with spaces").Should().BeTrue();
        PropertyWithValueExists(jsonProperties, "my.setting.3", @"c:\dir1\dir2\foo.txt").Should().BeTrue();
        PropertyWithValueExists(jsonProperties, "sonar.branch", "aBranch").Should().BeTrue();
    }

    [TestMethod]
    public void EncodeAsMultiValueProperty_WhenSQGreaterThanOrEqualTo65_EscapeAndJoinPaths()
    {
        var config65 = new AnalysisConfig
        {
            SonarOutputDir = @"C:\my_folder",
            SonarQubeVersion = "6.5"
        };
        var config66 = new AnalysisConfig
        {
            SonarOutputDir = @"C:\my_folder",
            SonarQubeVersion = "6.6"
        };
        var testSubject65 = new JsonPropertiesWriter(config65, new TestLogger());
        var testSubject66 = new JsonPropertiesWriter(config66, new TestLogger());

        var paths = new[] { "C:\\foo.cs", "C:\\foo,bar.cs", "C:\\foo\"bar.cs" };

        var actual65 = testSubject65.EncodeAsMultiValueProperty(paths);
        var actual66 = testSubject66.EncodeAsMultiValueProperty(paths);

        actual65.Should().BeIgnoringLineEndings("""
            "C:\foo.cs",\
            "C:\foo,bar.cs",\
            "C:\foo""bar.cs"
            """);
        actual66.Should().Be(actual65);
    }

    [TestMethod]
    public void EncodeAsMultiValueProperty_WhenSQLessThan65AndNoInvalidPath_JoinPaths() =>
        EncodeAsMultiValueProperty_WhenGivenSQVersionAndNoInvalidPath_JoinPaths("6.0");

    [TestMethod]
    public void EncodeAsMultiValueProperty_WhenSQVersionNullAndNoInvalidPath_JoinPaths() =>
        EncodeAsMultiValueProperty_WhenGivenSQVersionAndNoInvalidPath_JoinPaths(null);

    [TestMethod]
    public void EncodeAsMultiValueProperty_WhenSQVersionNotAVersionAndNoInvalidPath_JoinPaths() =>
        EncodeAsMultiValueProperty_WhenGivenSQVersionAndNoInvalidPath_JoinPaths("foo");

    [TestMethod]
    public void EncodeAsMultiValueProperty_WhenSQLessThan65AndInvalidPath_ExcludeInvalidPathAndJoinOthers() =>
        EncodeAsMultiValueProperty_WhenGivenSQVersionAndInvalidPath_ExcludeInvalidPathAndJoinOthers("6.0");

    [TestMethod]
    public void EncodeAsMultiValueProperty_WhenSQVersionIsNullAndInvalidPath_ExcludeInvalidPathAndJoinOthers() =>
        EncodeAsMultiValueProperty_WhenGivenSQVersionAndInvalidPath_ExcludeInvalidPathAndJoinOthers(null);

    [TestMethod]
    public void EncodeAsMultiValueProperty_WhenSQVersionNotAVersionAndInvalidPath_ExcludeInvalidPathAndJoinOthers() =>
        EncodeAsMultiValueProperty_WhenGivenSQVersionAndInvalidPath_ExcludeInvalidPathAndJoinOthers("foo");

    private static void EncodeAsMultiValueProperty_WhenGivenSQVersionAndNoInvalidPath_JoinPaths(string sonarqubeVersion) =>
        new JsonPropertiesWriter(
                new AnalysisConfig
                {
                    SonarOutputDir = @"C:\my_folder",
                    SonarQubeVersion = sonarqubeVersion
                },
                new TestLogger())
            .EncodeAsMultiValueProperty([@"C:\foo.cs", @"C:\foobar.cs"])
            .Should().BeIgnoringLineEndings("""
            C:\foo.cs,\
            C:\foobar.cs
            """);

    private static void EncodeAsMultiValueProperty_WhenGivenSQVersionAndInvalidPath_ExcludeInvalidPathAndJoinOthers(string sonarqubeVersion)
    {
        var logger = new TestLogger();
        var testSubject = new JsonPropertiesWriter(
            new AnalysisConfig
            {
                SonarOutputDir = @"C:\my_folder",
                SonarQubeVersion = sonarqubeVersion
            },
            logger);
        var paths = new[] { "C:\\foo.cs", "C:\\foo,bar.cs" };

        testSubject.EncodeAsMultiValueProperty(paths).Should().Be(@"C:\foo.cs");
        logger.Warnings.Should().ContainSingle();
        logger.Warnings[0].Should().Be("The following paths contain invalid characters and will be excluded from this analysis: C:\\foo,bar.cs");
    }

    private static string PropertiesPath(params string[] paths) =>
        Path.Combine(paths).Replace(@"\", @"\\"); // We escape `\` when we write the properties file

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

    private string SaveToResultFile(string testDir, string fileName, string content)
    {
        var fullPath = CreateFile(testDir, fileName, content);
        TestContext.AddResultFile(fullPath);
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

    // ToDo: https://sonarsource.atlassian.net/browse/SCAN4NET-747
    // this method should be moved to its own class "JsonPropertiesReader". It's going to be the equivalent of the SQPropertiesFileReader but for JSON property files.
    private static List<KeyValuePair<string, string>> JsonPropertiesReader(string filePath)
    {
        var result = new List<KeyValuePair<string, string>>();
        var jsonString = File.ReadAllText(filePath);

        var jsonArray = (JsonArray)JsonNode.Parse(jsonString);
        foreach (var item in jsonArray)
        {
            if (item is JsonObject jsonObject)
            {
                result.Add(new KeyValuePair<string, string>(jsonObject.First().Value?.ToString(), jsonObject.Last().Value?.ToString() ?? string.Empty));
            }
            else
            {
                Console.WriteLine("Warning: An item in the JSON array is not an object and will be skipped.");
            }
        }
        return result;
    }

    // ToDo: https://sonarsource.atlassian.net/browse/SCAN4NET-747
    // this method should be moved to its own class "JsonPropertiesReader". It's going to be the equivalent of the SQPropertiesFileReader but for JSON property files
    private static bool PropertyWithValueExists(List<KeyValuePair<string, string>> properties, string key, string value)
    {
        foreach (var entry in properties)
        {
            if (entry.Key.Equals(key, StringComparison.Ordinal))
            {
                if (entry.Value.Equals(value, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
