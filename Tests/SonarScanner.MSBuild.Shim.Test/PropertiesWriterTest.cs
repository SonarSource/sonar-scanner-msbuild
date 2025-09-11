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

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class PropertiesWriterTest
{
    public TestContext TestContext { get; set; }

    [DataRow("foo", "foo")]
    [DataRow(@"C:\File.cs", @"C:\\File.cs")]
    [DataRow("你好", @"\u4F60\u597D")]
    [DataRow("\n", @"\u000A")]
    [TestMethod]
    public void PropertiesWriterEscape(string escape, string expected) =>
        PropertiesWriter.Escape(escape).Should().Be(expected);

    [TestMethod]
    public void WriteSettingsForProject_ThrowsOnNullArgument()
    {
        var propertiesWriter = new PropertiesWriter(new AnalysisConfig());
        Action action = () => propertiesWriter.WriteSettingsForProject(null);

        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectData");
    }

    [TestMethod]
    public void WriteGlobalSettings_ThrowsOnNullArgument()
    {
        var propertiesWriter = new PropertiesWriter(new AnalysisConfig());
        Action action = () => propertiesWriter.WriteGlobalSettings(null);

        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("properties");
    }

    [TestMethod]
    public void WriteGlobalSettings_VerboseIsSkipped()
    {
        var propertiesWriter = new PropertiesWriter(new AnalysisConfig());
        propertiesWriter.WriteGlobalSettings([
            new(SonarProperties.Verbose, "true"),
            new(SonarProperties.HostUrl, "http://example.org"),
        ]);
        propertiesWriter.Flush().Should().BeIgnoringLineEndings("""
            sonar.host.url=http://example.org

            sonar.modules=


            """);
    }

    [TestMethod]
    public void WriteGlobalSettings_TrustStorePathIsSkipped()
    {
        var propertiesWriter = new PropertiesWriter(new AnalysisConfig());
        propertiesWriter.WriteGlobalSettings([
            new(SonarProperties.TruststorePath, "some/path"),
            new(SonarProperties.HostUrl, "http://example.org"),
        ]);
        propertiesWriter.Flush().Should().BeIgnoringLineEndings("""
            sonar.host.url=http://example.org

            sonar.modules=


            """);
    }

    [TestMethod]
    public void WriteGlobalSettings_HostUrlIsKeptIfHostUrlAndSonarcloudUrlAreSet()
    {
        var propertiesWriter = new PropertiesWriter(new AnalysisConfig());
        propertiesWriter.WriteGlobalSettings([
            new(SonarProperties.SonarcloudUrl, "http://SonarcloudUrl.org"),
            new(SonarProperties.HostUrl, "http://HostUrl.org"),
        ]);
        propertiesWriter.Flush().Should().BeIgnoringLineEndings("""
            sonar.scanner.sonarcloudUrl=http://SonarcloudUrl.org
            sonar.host.url=http://HostUrl.org

            sonar.modules=


            """);
    }

    [TestMethod]
    public void WriteSharedProperties_EmptySources_EmptyTests()
    {
        var propertiesWriter = new PropertiesWriter(new());
        propertiesWriter.WriteSharedFiles(new([], []));
        propertiesWriter.Flush().Should().BeIgnoringLineEndings("""

            sonar.modules=


            """);
    }

    [TestMethod]
    public void WriteSharedProperties_WithSources_EmptyTests()
    {
        var propertiesWriter = new PropertiesWriter(new());
        propertiesWriter.WriteSharedFiles(new([new(Path.Combine(TestUtils.DriveRoot(), "dev", "main.hs")), new(Path.Combine(TestUtils.DriveRoot(), "dev", "lambdas.hs"))], []));
        propertiesWriter.Flush().Should().BeIgnoringLineEndings($"""
            sonar.sources=\
            "{PropertiesPath(TestUtils.DriveRoot(), "dev", "main.hs")}",\
            "{PropertiesPath(TestUtils.DriveRoot(), "dev", "lambdas.hs")}"

            sonar.modules=


            """);
    }

    [TestMethod]
    public void WriteSharedProperties_EmptySources_WithTests()
    {
        var propertiesWriter = new PropertiesWriter(new());
        propertiesWriter.WriteSharedFiles(new([], [new(Path.Combine(TestUtils.DriveRoot(), "dev", "test.hs")), new(Path.Combine(TestUtils.DriveRoot(), "dev", "test2.hs"))]));
        propertiesWriter.Flush().Should().BeIgnoringLineEndings($"""
            sonar.tests=\
            "{PropertiesPath(TestUtils.DriveRoot(), "dev", "test.hs")}",\
            "{PropertiesPath(TestUtils.DriveRoot(), "dev", "test2.hs")}"

            sonar.modules=


            """);
    }

    [TestMethod]
    public void WriteSharedProperties_WithSources_WithTests()
    {
        var propertiesWriter = new PropertiesWriter(new());
        propertiesWriter.WriteSharedFiles(new([new(Path.Combine(TestUtils.DriveRoot(), "dev", "main.hs"))], [new(Path.Combine(TestUtils.DriveRoot(), "dev", "test.hs"))]));
        propertiesWriter.Flush().Should().BeIgnoringLineEndings($"""
            sonar.sources=\
            "{PropertiesPath(TestUtils.DriveRoot(), "dev", "main.hs")}"
            sonar.tests=\
            "{PropertiesPath(TestUtils.DriveRoot(), "dev", "test.hs")}"

            sonar.modules=


            """);
    }

    [TestMethod]
    public void WriteAnalyzerOutputPaths_ForUnexpectedLanguage_DoNotWritesOutPaths()
    {
        var propertiesWriter = new PropertiesWriter(new AnalysisConfig());
        propertiesWriter.WriteAnalyzerOutputPaths(CreateTestProjectDataWithPaths("unexpected", analyzerOutPaths: [@"c:\dir1\dir2"]));

        propertiesWriter.Flush().Should().BeIgnoringLineEndings(
            """
            sonar.modules=


            """);
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.analyzer.projectOutPaths")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.analyzer.projectOutPaths")]
    public void WriteAnalyzerOutputPaths_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var propertiesWriter = new PropertiesWriter(new AnalysisConfig());
        propertiesWriter.WriteAnalyzerOutputPaths(CreateTestProjectDataWithPaths(
            language,
            analyzerOutPaths: [Path.Combine(TestUtils.DriveRoot(), "dir1", "first"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second")]));
        propertiesWriter.Flush().Should().BeIgnoringLineEndings(
            $"""
            5762C17D-1DDF-4C77-86AC-E2B4940926A9.{expectedPropertyKey}=\
            "{PropertiesPath(TestUtils.DriveRoot(), "dir1", "first")}",\
            "{PropertiesPath(TestUtils.DriveRoot(), "dir1", "second")}"
            sonar.modules=


            """);
    }

    [TestMethod]
    public void WriteRoslynReportPaths_ForUnexpectedLanguage_DoNotWritesOutPaths()
    {
        var propertiesWriter = new PropertiesWriter(new AnalysisConfig());
        propertiesWriter.WriteRoslynReportPaths(CreateTestProjectDataWithPaths("unexpected"));

        propertiesWriter.Flush().Should().BeIgnoringLineEndings(
            """
            sonar.modules=


            """);
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.roslyn.reportFilePaths")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.roslyn.reportFilePaths")]
    public void WriteRoslynReportPaths_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var propertiesWriter = new PropertiesWriter(new AnalysisConfig());
        propertiesWriter.WriteRoslynReportPaths(CreateTestProjectDataWithPaths(
            language,
            roslynOutPaths: [Path.Combine(TestUtils.DriveRoot(), "dir1", "first"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second")]));

        propertiesWriter.Flush().Should().BeIgnoringLineEndings(
            $"""
            5762C17D-1DDF-4C77-86AC-E2B4940926A9.{expectedPropertyKey}=\
            "{PropertiesPath(TestUtils.DriveRoot(), "dir1", "first")}",\
            "{PropertiesPath(TestUtils.DriveRoot(), "dir1", "second")}"
            sonar.modules=


            """);
    }

    [TestMethod]
    public void Telemetry_ForUnexpectedLanguage_DoNotWritePaths()
    {
        var propertiesWriter = new PropertiesWriter(new AnalysisConfig());

        propertiesWriter.WriteTelemetryPaths(CreateTestProjectDataWithPaths("unexpected", telemetryPaths: [@"c:\dir1\dir2\Telemetry.json"]));

        propertiesWriter.Flush().Should().BeIgnoringLineEndings(
            """
            sonar.modules=


            """);
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp, "sonar.cs.scanner.telemetry")]
    [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.scanner.telemetry")]
    public void Telmetry_WritesEncodedPaths(string language, string expectedPropertyKey)
    {
        var propertiesWriter = new PropertiesWriter(new AnalysisConfig());
        propertiesWriter.WriteTelemetryPaths(CreateTestProjectDataWithPaths(
            language,
            telemetryPaths: [Path.Combine(TestUtils.DriveRoot(), "dir1", "first", "Telemetry.json"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second", "Telemetry.json")]));

        propertiesWriter.Flush().Should().BeIgnoringLineEndings(
            $"""
            5762C17D-1DDF-4C77-86AC-E2B4940926A9.{expectedPropertyKey}=\
            "{PropertiesPath(TestUtils.DriveRoot(), "dir1", "first", "Telemetry.json")}",\
            "{PropertiesPath(TestUtils.DriveRoot(), "dir1", "second", "Telemetry.json")}"
            sonar.modules=


            """);
    }

    [TestMethod]
    public void PropertiesWriterToString()
    {
        var productBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "PropertiesWriterTest_ProductBaseDir");
        var productProject = CreateEmptyFile(productBaseDir, "MyProduct.csproj");
        var productFile = CreateEmptyFile(productBaseDir, "File.cs");
        var productChineseFile = CreateEmptyFile(productBaseDir, "你好.cs");

        var productCoverageFilePath = CreateEmptyFile(productBaseDir, "productCoverageReport.txt").FullName;
        CreateEmptyFile(productBaseDir, "productTrx.trx");
        var productFileListFilePath = Path.Combine(productBaseDir, "productManagedFiles.txt");

        var otherDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "PropertiesWriterTest_OtherDir");
        var missingFileOutsideProjectDir = new FileInfo(Path.Combine(otherDir, "missing.cs"));

        var productFiles = new List<FileInfo>
        {
            productFile,
            productChineseFile,
            missingFileOutsideProjectDir
        };
        var productCS = CreateProjectData("你好", "DB2E5521-3172-47B9-BA50-864F12E6DFFF", productProject, false, productFiles, productFileListFilePath, productCoverageFilePath, ProjectLanguages.CSharp);
        productCS.SonarQubeModuleFiles.Add(productFile);
        productCS.SonarQubeModuleFiles.Add(productChineseFile);
        productCS.SonarQubeModuleFiles.Add(missingFileOutsideProjectDir);

        var productVB = CreateProjectData("vbProject", "B51622CF-82F4-48C9-9F38-FB981FAFAF3A", productProject, false, productFiles, productFileListFilePath, productCoverageFilePath, ProjectLanguages.VisualBasic);
        productVB.SonarQubeModuleFiles.Add(productFile);

        var testBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "PropertiesWriterTest_TestBaseDir");
        var testProject = CreateEmptyFile(testBaseDir, "MyTest.csproj");
        var testFile = CreateEmptyFile(testBaseDir, "File.cs");
        var testFileListFilePath = Path.Combine(testBaseDir, "testManagedFiles.txt");

        var testFiles = new List<FileInfo>
        {
            testFile
        };
        var test = CreateProjectData("my_test_project", "DA0FCD82-9C5C-4666-9370-C7388281D49B", testProject, true, testFiles, testFileListFilePath, null, ProjectLanguages.VisualBasic);
        test.SonarQubeModuleFiles.Add(testFile);

        var config = new AnalysisConfig
        {
            SonarProjectKey = "my_project_key",
            SonarProjectName = "my_project_name",
            SonarProjectVersion = "1.0",
            SonarOutputDir = @"C:\my_folder",
            SourcesDirectory = @"d:\source_files\"
        };

        string actual = null;
        using (new AssertIgnoreScope()) // expecting the property writer to complain about the missing file
        {
            var writer = new PropertiesWriter(config);
            writer.WriteSettingsForProject(productCS);
            writer.WriteSettingsForProject(productVB);
            writer.WriteSettingsForProject(test);

            actual = writer.Flush();
        }

        var expected = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            """
            DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectKey=my_project_key:DB2E5521-3172-47B9-BA50-864F12E6DFFF
            DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectName=\u4F60\u597D
            DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectBaseDir={0}
            DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.sourceEncoding=utf-8
            DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.tests=
            DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.sources=\
            "{0}{3}File.cs",\
            "{0}{3}\u4F60\u597D.cs",\
            "{2}"

            DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.working.directory=C:\\my_folder{3}.sonar{3}mod0
            B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectKey=my_project_key:B51622CF-82F4-48C9-9F38-FB981FAFAF3A
            B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectName=vbProject
            B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectBaseDir={0}
            B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.sourceEncoding=utf-8
            B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.tests=
            B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.sources=\
            "{0}{3}File.cs"

            B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.working.directory=C:\\my_folder{3}.sonar{3}mod1
            DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectKey=my_project_key:DA0FCD82-9C5C-4666-9370-C7388281D49B
            DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectName=my_test_project
            DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectBaseDir={1}
            DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.sourceEncoding=utf-8
            DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.sources=
            DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.tests=\
            "{1}{3}File.cs"

            DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.working.directory=C:\\my_folder{3}.sonar{3}mod2
            sonar.modules=DB2E5521-3172-47B9-BA50-864F12E6DFFF,B51622CF-82F4-48C9-9F38-FB981FAFAF3A,DA0FCD82-9C5C-4666-9370-C7388281D49B


            """,
            PropertiesWriter.Escape(productBaseDir),
            PropertiesWriter.Escape(testBaseDir),
            PropertiesWriter.Escape(missingFileOutsideProjectDir.FullName),
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"\\" : "/"); // On windows the path separator is an escaped '\'

        SaveToResultFile(productBaseDir, "Expected.txt", expected);
        SaveToResultFile(productBaseDir, "Actual.txt", actual);

        actual.Should().BeIgnoringLineEndings(expected);
    }

    [TestMethod]
    public void WriteSonarProjectInfo_WritesAllValues()
    {
        var config = new AnalysisConfig
        {
            SonarProjectKey = "my_project_key",
            SonarProjectName = "my_project_name",
            SonarProjectVersion = "4.2",
            SonarOutputDir = @"C:\OutpuDir",
        };
        config.SetConfigValue(SonarProperties.PullRequestCacheBasePath, @"C:\PullRequest\Cache\BasePath");
        var writer = new PropertiesWriter(config);
        writer.WriteSonarProjectInfo(new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "ProjectBaseDir")));

        writer.Flush().Should().BeIgnoringLineEndings(
            $"""
            sonar.projectKey=my_project_key
            sonar.projectName=my_project_name
            sonar.projectVersion=4.2
            sonar.working.directory=C:\\OutpuDir{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"\\" : "/")}.sonar
            sonar.projectBaseDir={PropertiesPath(TestUtils.DriveRoot(), "ProjectBaseDir")}
            sonar.pullrequest.cache.basepath=C:\\PullRequest\\Cache\\BasePath
            sonar.modules=


            """);
    }

    [TestMethod]
    public void WriteSonarProjectInfo_EmptyValues()
    {
        var config = new AnalysisConfig { SonarOutputDir = @"C:\OutputDir\CannotBeEmpty" };
        var writer = new PropertiesWriter(config);
        writer.WriteSonarProjectInfo(new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "ProjectBaseDir")));

        writer.Flush().Should().BeIgnoringLineEndings(
            $"""
            sonar.projectKey=
            sonar.working.directory=C:\\OutputDir\\CannotBeEmpty{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"\\" : "/")}.sonar
            sonar.projectBaseDir={PropertiesPath(TestUtils.DriveRoot(), "ProjectBaseDir")}
            sonar.pullrequest.cache.basepath=
            sonar.modules=


            """);
    }

    [TestMethod]
    public void Ctor_WhenAnalysisConfigIsNull_ThrowsArgumentNulLException()
    {
        Action act = () => new PropertiesWriter(null);

        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
    }

    [TestMethod]
    public void Flush_WhenCalledTwice_ThrowsInvalidOperationException()
    {
        var writer = new PropertiesWriter(
            new AnalysisConfig
            {
                SonarProjectKey = "key",
                SonarProjectName = "name",
                SonarProjectVersion = "1.0",
                SonarOutputDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext)
            });
        writer.Flush();

        Action act = () => writer.Flush();
        act.Should().ThrowExactly<InvalidOperationException>();
    }

    [TestMethod]
    public void WriteSettingsForProject_WhenFlushed_ThrowsInvalidOperationException()
    {
        var writer = new PropertiesWriter(
            new AnalysisConfig
            {
                SonarProjectKey = "key",
                SonarProjectName = "name",
                SonarProjectVersion = "1.0",
                SonarOutputDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext)
            });
        writer.Flush();

        using (new AssertIgnoreScope())
        {
            FluentActions.Invoking(() => writer.WriteSettingsForProject(new[] { new ProjectInfo() }.ToProjectData(new TestRuntime()).Single()))
                .Should().ThrowExactly<InvalidOperationException>();
        }
    }

    // Tests that analysis settings in the ProjectInfo are written to the file
    [TestMethod]
    public void PropertiesWriter_AnalysisSettingsWritten()
    {
        var projectBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "PropertiesWriterTest_AnalysisSettingsWritten");
        var productProject = CreateEmptyFile(projectBaseDir, "MyProduct.csproj");
        var productFile = CreateEmptyFile(projectBaseDir, "File.cs");
        var productFiles = new List<FileInfo>
        {
            productFile
        };
        var productFileListFilePath = Path.Combine(projectBaseDir, "productManagedFiles.txt");

        var product = CreateProjectData("AnalysisSettingsTest.proj", "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5", productProject, false, productFiles, productFileListFilePath, null, "language");

        // These are the settings we are going to check. The other analysis values are not checked.
        product.Project.AnalysisSettings =
        [
            new("my.setting1", "setting1"),
            new("my.setting2", "setting 2 with spaces"),
            new("my.setting.3", @"c:\dir1\dir2\foo.txt") // path that will be escaped
        ];
        product.ReferencedFiles.Add(productFile);

        var writer = new PropertiesWriter(new AnalysisConfig { SonarOutputDir = @"C:\my_folder" });
        writer.WriteSettingsForProject(product);

        var propertyReader = new SQPropertiesFileReader(SaveToResultFile(projectBaseDir, "Actual.txt", writer.Flush()));
        propertyReader.AssertSettingExists("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting1", "setting1");
        propertyReader.AssertSettingExists("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting2", "setting 2 with spaces");
        propertyReader.AssertSettingExists("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting.3", @"c:\dir1\dir2\foo.txt");
    }

    // Tests that .sonar.working.directory is explicitly set per module
    [TestMethod]
    public void PropertiesWriter_WorkdirPerModuleExplicitlySet()
    {
        var projectBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "PropertiesWriterTest_AnalysisSettingsWritten");
        var productProject = CreateEmptyFile(projectBaseDir, "MyProduct.csproj");

        var productFile = CreateEmptyFile(projectBaseDir, "File.cs");
        var productFiles = new List<FileInfo>
        {
            productFile
        };
        var productFileListFilePath = Path.Combine(projectBaseDir, "productManagedFiles.txt");

        var projectKey = "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5";
        var product = CreateProjectData("AnalysisSettingsTest.proj", projectKey, productProject, false, productFiles, productFileListFilePath, null, "language");
        product.ReferencedFiles.Add(productFile);

        var config = new AnalysisConfig
        {
            SonarOutputDir = @"C:\my_folder"
        };

        var writer = new PropertiesWriter(config);
        writer.WriteSettingsForProject(product);
        writer.WriteSonarProjectInfo(new DirectoryInfo("dummy basedir"));
        var s = writer.Flush();

        var props = new JavaProperties();
        props.Load(GenerateStreamFromString(s));
        var key = projectKey + "." + SonarProperties.WorkingDirectory;
        props.ContainsKey(key).Should().BeTrue();

        static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }

    // Tests that global settings in the ProjectInfo are written to the file
    [TestMethod]
    public void PropertiesWriter_GlobalSettingsWritten()
    {
        var globalSettings = new AnalysisProperties
        {
            new("my.setting1", "setting1"),
            new("my.setting2", "setting 2 with spaces"),
            new("my.setting.3", @"c:\dir1\dir2\foo.txt"), // path that will be escaped
            // Specific test for sonar.branch property
            new("sonar.branch", "aBranch") // path that will be escaped
        };

        var writer = new PropertiesWriter(new AnalysisConfig { SonarOutputDir = @"C:\my_folder" });
        writer.WriteGlobalSettings(globalSettings);

        var propertyReader = new SQPropertiesFileReader(
            SaveToResultFile(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "PropertiesWriterTest_GlobalSettingsWritten"), "Actual.txt", writer.Flush()));
        propertyReader.AssertSettingExists("my.setting1", "setting1");
        propertyReader.AssertSettingExists("my.setting2", "setting 2 with spaces");
        propertyReader.AssertSettingExists("my.setting.3", @"c:\dir1\dir2\foo.txt");
        propertyReader.AssertSettingExists("sonar.branch", "aBranch");
    }

    private static string PropertiesPath(params string[] paths) =>
        Path.Combine(paths).Replace(@"\", @"\\"); // We escape `\` when we write the properties file

    private static ProjectData CreateProjectData(string name,
                                                 string guid,
                                                 FileInfo fullFilePath,
                                                 bool isTest,
                                                 IEnumerable<FileInfo> files,
                                                 string fileListFilePath,
                                                 string coverageReportPath,
                                                 string language)
    {
        var projectInfo = new ProjectInfo
        {
            ProjectName = name,
            ProjectGuid = Guid.Parse(guid),
            FullPath = fullFilePath.FullName,
            ProjectType = isTest ? ProjectType.Test : ProjectType.Product,
            AnalysisResultFiles = [],
            AnalysisSettings = [],
            ProjectLanguage = language,
            Encoding = "UTF-8"
        };
        if (coverageReportPath is not null)
        {
            projectInfo.AddAnalyzerResult(AnalysisResultFileType.VisualStudioCodeCoverage, coverageReportPath);
        }
        if (files is not null && files.Any())
        {
            fileListFilePath.Should().NotBeNullOrWhiteSpace("Test setup error: must supply the managedFileListFilePath as a list of files has been supplied");
            File.WriteAllLines(fileListFilePath, files.Select(x => x.FullName));
            projectInfo.AddAnalyzerResult(AnalysisResultFileType.FilesToAnalyze, fileListFilePath);
        }
        return new[] { projectInfo }.ToProjectData(new TestRuntime()).Single();
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
        var projectData = CreateProjectData("Name", "5762C17D-1DDF-4C77-86AC-E2B4940926A9", new FileInfo("Name.proj"), false, [], null, null, language);
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
