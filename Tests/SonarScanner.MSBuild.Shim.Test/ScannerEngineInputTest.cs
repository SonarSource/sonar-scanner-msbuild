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
    private readonly IAnalysisPropertyProvider provider = new ListPropertiesProvider();

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_ConfigIsNull_ThrowsOnNullArgument() =>
        FluentActions.Invoking(() => new ScannerEngineInput(null, new ListPropertiesProvider())).Should().ThrowExactly<ArgumentNullException>().WithParameterName("config");

    [TestMethod]
    public void AddProject_ThrowsOnNullArgument() =>
        new ScannerEngineInput(new AnalysisConfig(), new ListPropertiesProvider()).Invoking(x => x.AddProject(null)).Should().Throw<ArgumentNullException>().WithParameterName("project");

    [TestMethod]
    public void AddGlobalSettings_ThrowsOnNullArgument() =>
        new ScannerEngineInput(new AnalysisConfig(), new ListPropertiesProvider()).Invoking(x => x.AddGlobalSettings(null)).Should().Throw<ArgumentNullException>().WithParameterName("properties");

    [TestMethod]
    public void AddGlobalSettings_VerboseIsSkipped()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig(), new ListPropertiesProvider());
        sut.AddGlobalSettings([
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
    public void AddGlobalSettings_HostUrlIsKeptIfHostUrlAndSonarcloudUrlAreSet()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig(), new ListPropertiesProvider());
        sut.AddGlobalSettings([
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
    public void AddSharedFiles_EmptySources_EmptyTests()
    {
        var sut = new ScannerEngineInput(new(), new ListPropertiesProvider());
        sut.AddSharedFiles(new([], []));
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
    public void AddSharedFiles_WithSources_EmptyTests()
    {
        var sut = new ScannerEngineInput(new(), new ListPropertiesProvider());
        sut.AddSharedFiles(new([new(Path.Combine(TestUtils.DriveRoot(), "dev", "main.hs")), new(Path.Combine(TestUtils.DriveRoot(), "dev", "lambdas.hs"))], []));
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
    public void AddSharedFiles_EmptySources_WithTests()
    {
        var sut = new ScannerEngineInput(new(), new ListPropertiesProvider());
        sut.AddSharedFiles(new([], [new(Path.Combine(TestUtils.DriveRoot(), "dev", "test.hs")), new(Path.Combine(TestUtils.DriveRoot(), "dev", "test2.hs"))]));
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
    public void AddSharedFiles_WithSources_WithTests()
    {
        var sut = new ScannerEngineInput(new(), new ListPropertiesProvider());
        sut.AddSharedFiles(new([new(Path.Combine(TestUtils.DriveRoot(), "dev", "main.hs"))], [new(Path.Combine(TestUtils.DriveRoot(), "dev", "test.hs"))]));
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
    public void AddVsTestReportPaths_WritesEncodedPaths()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig(), new ListPropertiesProvider());
        sut.AddVsTestReportPaths([Path.Combine(TestUtils.DriveRoot(), "dir1", "first"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second")]);
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
    public void AddVsXmlCoverageReportPaths_WritesEncodedPaths()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig(), new ListPropertiesProvider());
        sut.AddVsXmlCoverageReportPaths([Path.Combine(TestUtils.DriveRoot(), "dir1", "first"), Path.Combine(TestUtils.DriveRoot(), "dir1", "second")]);
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
    public void AddProject()
    {
        var sonarOutputDir = @"C:\my_folder";
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

        var productCS = CreateProjectData("你好", "DB2E5521-3172-47B9-BA50-864F12E6DFFF", productProject, false, productFiles, productFileListFilePath, productCoverageFilePath, ProjectLanguages.CSharp);
        productCS.SonarQubeModuleFiles.Add(productFile);
        productCS.SonarQubeModuleFiles.Add(productChineseFile);
        productCS.SonarQubeModuleFiles.Add(missingFileOutsideProjectDir);

        var productVB = CreateProjectData("vbProject", "B51622CF-82F4-48C9-9F38-FB981FAFAF3A", productProject, false, productFiles, productFileListFilePath, productCoverageFilePath, ProjectLanguages.VisualBasic);
        productVB.SonarQubeModuleFiles.Add(productFile);

        var testBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "JsonbuilderTest_TestBaseDir");
        var testProject = CreateEmptyFile(testBaseDir, "MyTest.csproj");
        var testFile = CreateEmptyFile(testBaseDir, "File.cs");
        var testFileListFilePath = Path.Combine(testBaseDir, "testManagedFiles.txt");
        var testFiles = new List<FileInfo> { testFile };
        var test = CreateProjectData("my_test_project", "DA0FCD82-9C5C-4666-9370-C7388281D49B", testProject, true, testFiles, testFileListFilePath, null, ProjectLanguages.VisualBasic);
        test.SonarQubeModuleFiles.Add(testFile);

        var config = new AnalysisConfig
        {
            SonarProjectKey = "my_project_key",
            SonarProjectName = "my_project_name",
            SonarProjectVersion = "1.0",
            SonarOutputDir = sonarOutputDir,
            SourcesDirectory = @"d:\source_files\"
        };
        var sut = new ScannerEngineInput(config, provider);
        sut.AddProject(productCS);
        sut.AddProject(productVB);
        sut.AddProject(test);
        var actual = sut.ToString();

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
                  "key": "DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.working.directory",
                  "value": {{JsonConvert.ToString(Path.Combine(sonarOutputDir, ".sonar", "mod0"))}}
                },
                {
                  "key": "DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.sourceEncoding",
                  "value": "utf-8"
                },
                {
                  "key": "DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.sources",
                  "value": {{JsonConvert.ToString(Path.Combine(productBaseDir, "File.cs") + "," + Path.Combine(productBaseDir, "你好.cs") + "," + missingFileOutsideProjectDir.FullName)}}
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
                  "key": "B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.working.directory",
                  "value": {{JsonConvert.ToString(Path.Combine(sonarOutputDir, ".sonar", "mod1"))}}
                },
                {
                  "key": "B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.sourceEncoding",
                  "value": "utf-8"
                },
                {
                  "key": "B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.sources",
                  "value": {{JsonConvert.ToString(Path.Combine(productBaseDir, "File.cs"))}}
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
                  "key": "DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.working.directory",
                  "value": {{JsonConvert.ToString(Path.Combine(sonarOutputDir, ".sonar", "mod2"))}}
                },
                {
                  "key": "DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.sourceEncoding",
                  "value": "utf-8"
                },
                {
                  "key": "DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.tests",
                  "value": {{JsonConvert.ToString(Path.Combine(testBaseDir, "File.cs"))}}
                }
              ]
            }
            """;
        actual.Should().BeIgnoringLineEndings(expected);
    }

    [TestMethod]
    public void AddConfig_WritesAllValues()
    {
        var projectBaseDir = Path.Combine(TestUtils.DriveRoot(), "ProjectBaseDir");
        var sonarOutputDir = @"C:\OutpuDir";
        var config = new AnalysisConfig
        {
            SonarProjectKey = "my_project_key",
            SonarProjectName = "my_project_name",
            SonarProjectVersion = "4.2",
            SonarOutputDir = sonarOutputDir,
        };
        config.SetConfigValue(SonarProperties.PullRequestCacheBasePath, @"C:\PullRequest\Cache\BasePath");
        var sut = new ScannerEngineInput(config, provider);
        sut.AddConfig(new DirectoryInfo(projectBaseDir));

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
    public void AddConfig_EmptyValues()
    {
        var config = new AnalysisConfig { SonarOutputDir = @"C:\OutputDir\CannotBeEmpty" };
        var sut = new ScannerEngineInput(config, provider);
        sut.AddConfig(new DirectoryInfo(Path.Combine(TestUtils.DriveRoot(), "ProjectBaseDir")));
        sut.ToString().Should().BeIgnoringLineEndings(
            $$"""
            {
              "scannerProperties": [
                {
                  "key": "sonar.modules",
                  "value": ""
                },
                {
                  "key": "sonar.working.directory",
                  "value": {{JsonConvert.ToString(Path.Combine(@"C:\OutputDir\CannotBeEmpty", ".sonar"))}}
                },
                {
                  "key": "sonar.projectBaseDir",
                  "value": {{JsonConvert.ToString(Path.Combine(TestUtils.DriveRoot(), "ProjectBaseDir"))}}
                }
              ]
            }
            """);
    }

    // Tests that .sonar.working.directory is explicitly set per module
    [TestMethod]
    public void AddProject_WorkdirPerModuleExplicitlySet()
    {
        var projectBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "JsonbuilderTest_AnalysisSettingsWritten");
        var productProject = CreateEmptyFile(projectBaseDir, "MyProduct.csproj");
        var productFile = CreateEmptyFile(projectBaseDir, "File.cs");
        var productFiles = new List<FileInfo> { productFile };
        var productFileListFilePath = Path.Combine(projectBaseDir, "productManagedFiles.txt");
        var projectKey = "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5";
        var product = CreateProjectData("AnalysisSettingsTest.proj", projectKey, productProject, false, productFiles, productFileListFilePath, null, "language");
        product.ReferencedFiles.Add(productFile);
        var config = new AnalysisConfig
        {
            SonarOutputDir = Path.Combine(TestUtils.DriveRoot(), "my_folder")
        };
        var sut = new ScannerEngineInput(config, provider);
        sut.AddProject(product);
        sut.AddConfig(new DirectoryInfo("dummy basedir"));

        var workDirKey = projectKey + "." + SonarProperties.WorkingDirectory;
        new ScannerEngineInputReader(sut.ToString()).AssertProperty(workDirKey, Path.Combine(TestUtils.DriveRoot(), "my_folder", ".sonar", "mod0"));
    }

    // Tests that global settings in the ProjectInfo are written to the file
    [TestMethod]
    public void AddGlobalSettings()
    {
        var globalSettings = new AnalysisProperties
        {
            new("my.setting1", "setting1"),
            new("my.setting2", "setting 2 with spaces"),
            new("my.setting.3", @"c:\dir1\dir2\foo.txt"),
            // Specific test for sonar.branch property
            new("sonar.branch", "aBranch")
        };
        var sut = new ScannerEngineInput(new AnalysisConfig { SonarOutputDir = @"C:\my_folder" }, new ListPropertiesProvider());
        sut.AddGlobalSettings(globalSettings);

        var reader = new ScannerEngineInputReader(sut.ToString());
        reader.AssertProperty("my.setting1", "setting1");
        reader.AssertProperty("my.setting2", "setting 2 with spaces");
        reader.AssertProperty("my.setting.3", @"c:\dir1\dir2\foo.txt");
        reader.AssertProperty("sonar.branch", "aBranch");
    }

    [TestMethod]
    public void AddGlobalSettings_DoesNotAddProjectBaseDir()
    {
        var globalSettings = new AnalysisProperties
        {
            new(SonarProperties.ProjectBaseDir, "somePath"),
        };
        var sut = new ScannerEngineInput(new AnalysisConfig { SonarOutputDir = @"C:\my_folder" }, new ListPropertiesProvider());
        sut.AddGlobalSettings(globalSettings);

        var reader = new ScannerEngineInputReader(sut.ToString());
        reader.AssertPropertyDoesNotExist(SonarProperties.ProjectBaseDir);
    }

    [TestMethod]
    public void ProjectBaseDir_FromConfigAndGlobalSettings_IsNotDuplicated()
    {
        var basedir = new DirectoryInfo("somePath");
        var globalSettings = new AnalysisProperties
        {
            new(SonarProperties.ProjectBaseDir, basedir.FullName),
        };
        var sut = new ScannerEngineInput(new AnalysisConfig { SonarOutputDir = @"C:\my_folder" }, new ListPropertiesProvider());

        sut.AddConfig(basedir);
        sut.AddGlobalSettings(globalSettings);

        var reader = new ScannerEngineInputReader(sut.ToString());
        reader.AssertProperty(SonarProperties.ProjectBaseDir, basedir.FullName);
    }

    [TestMethod]
    public void Add_AsMultiValueProperty_EncodeValues()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig(), new ListPropertiesProvider());
        sut.Add(
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

    [TestMethod]
    public void CloneWithoutSensitiveData_PreservesDataAndSonarModules()
    {
        var root = Path.Combine(TestUtils.DriveRoot(), "Project");
        var config = new AnalysisConfig { SonarOutputDir = Path.Combine(root, ".sonarqube", "out"), SonarProjectKey = "ProjectKey" };
        var sut = new ScannerEngineInput(config, provider);
        sut.Add("sonar", "safe.key", "Safe Value");
        sut.AddProject(CreateProjectData("Name", "DB2E5521-3172-47B9-BA50-864F12E6DFFF", new FileInfo(Path.Combine(root, "Project.csproj")), false, [], null, null, ProjectLanguages.CSharp));
        var expected = sut.ToString();

        sut.CloneWithoutSensitiveData().ToString().Should().Be(expected);
    }

    [TestMethod]
    public void CloneWithoutSensitiveData_SensitiveKey()
    {
        var root = Path.Combine(TestUtils.DriveRoot(), "Project");
        var config = new AnalysisConfig { SonarOutputDir = Path.Combine(root, ".sonarqube", "out"), SonarProjectKey = "ProjectKey" };
        var secretsProvider = new ListPropertiesProvider
        {
            { "sonar.token", "!Sacred!Secret!" }
        };
        var sut = new ScannerEngineInput(config, secretsProvider);
        sut.Add("sonar", "safe.key", "Safe Value");
        var reader = new ScannerEngineInputReader(sut.CloneWithoutSensitiveData().ToString());

        reader.AssertProperty("sonar.safe.key", "Safe Value");
        reader.AssertProperty("sonar.token", "***");
    }

    [TestMethod]
    public void CloneWithoutSensitiveData_SensitiveValue()
    {
        var root = Path.Combine(TestUtils.DriveRoot(), "Project");
        var config = new AnalysisConfig { SonarOutputDir = Path.Combine(root, ".sonarqube", "out"), SonarProjectKey = "ProjectKey" };
        var sut = new ScannerEngineInput(config, provider);
        sut.Add("sonar", "safe.key", "Safe Value");
        sut.Add("sonar", "unsafe.value", "<Fragment><Key>sonar.token</Key><Value>!Sacred!Secret! nested in value of a safe key</Value></Fragment>");
        var reader = new ScannerEngineInputReader(sut.CloneWithoutSensitiveData().ToString());

        reader.AssertProperty("sonar.safe.key", "Safe Value");
        reader.AssertProperty("sonar.unsafe.value", "***");
    }

    [TestMethod]
    public void SonarToken_PopulatesInput()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig(), new ListPropertiesProvider
        {
            { SonarProperties.SonarToken, "TokenValue" }
        });
        var reader = new ScannerEngineInputReader(sut.ToString());
        reader.AssertProperty(SonarProperties.SonarToken, "TokenValue");
    }

    [TestMethod]
    public void SonarLogin_PopulatesInput()
    {
        var sut = new ScannerEngineInput(new AnalysisConfig(), new ListPropertiesProvider
        {
            { SonarProperties.SonarUserName, "UserName" },
            { SonarProperties.SonarPassword, "Password" }
        });
        var reader = new ScannerEngineInputReader(sut.ToString());
        reader.AssertProperty(SonarProperties.SonarUserName, "UserName");
        reader.AssertProperty(SonarProperties.SonarPassword, "Password");
    }

    private static ProjectData CreateProjectData(string name,
                                                 string projectId,
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
            ProjectGuid = Guid.Parse(projectId),
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
        return new[] { projectInfo }.ToProjectData(true, Substitute.For<ILogger>()).Single();
    }

    private static FileInfo CreateEmptyFile(string parentDir, string fileName) =>
        new(CreateFile(parentDir, fileName, string.Empty));

    private static string CreateFile(string parentDir, string fileName, string content)
    {
        var fullPath = Path.Combine(parentDir, fileName);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }
}
