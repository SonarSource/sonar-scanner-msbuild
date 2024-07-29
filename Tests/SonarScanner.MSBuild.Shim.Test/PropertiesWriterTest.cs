/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Shim.Test
{
    [TestClass]
    public class PropertiesWriterTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void PropertiesWriterEscape()
        {
            PropertiesWriter.Escape("foo").Should().Be("foo");
            PropertiesWriter.Escape(@"C:\File.cs").Should().Be(@"C:\\File.cs");
            PropertiesWriter.Escape("你好").Should().Be(@"\u4F60\u597D");
            PropertiesWriter.Escape("\n").Should().Be(@"\u000A");
        }

        [TestMethod]
        public void WriteSettingsForProject_ThrowsOnNullArgument()
        {
            var propertiesWriter = new PropertiesWriter(new AnalysisConfig(), new TestLogger());
            Action action = () => propertiesWriter.WriteSettingsForProject(null);

            action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectData");
        }

        [TestMethod]
        public void WriteGlobalSettings_ThrowsOnNullArgument()
        {
            var propertiesWriter = new PropertiesWriter(new AnalysisConfig(), new TestLogger());
            Action action = () => propertiesWriter.WriteGlobalSettings(null);

            action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("properties");
        }

        [TestMethod]
        public void WriteGlobalSettings_VerboseIsSkipped()
        {
            var propertiesWriter = new PropertiesWriter(new AnalysisConfig(), new TestLogger());
            propertiesWriter.WriteGlobalSettings([
                new(SonarProperties.Verbose, "true"),
                new(SonarProperties.HostUrl, "http://example.org"),
            ]);
            propertiesWriter.Flush().Should().Be("""
                sonar.host.url=http://example.org

                sonar.modules=


                """);
        }

        [TestMethod]
        public void WriteGlobalSettings_HostUrlIsSetToSonarCloudIo_IfNotSet()
        {
            var propertiesWriter = new PropertiesWriter(new AnalysisConfig(), new TestLogger());
            propertiesWriter.WriteGlobalSettings([]);
            propertiesWriter.Flush().Should().Be("""
                sonar.host.url=https://sonarcloud.io

                sonar.modules=


                """);
        }

        [TestMethod]
        public void WriteGlobalSettings_HostUrlIsPassedThroughIfSet()
        {
            var propertiesWriter = new PropertiesWriter(new AnalysisConfig(), new TestLogger());
            propertiesWriter.WriteGlobalSettings([new(SonarProperties.HostUrl, "http://example.org")]);
            propertiesWriter.Flush().Should().Be("""
                sonar.host.url=http://example.org

                sonar.modules=


                """);
        }

        [TestMethod]
        public void WriteGlobalSettings_HostUrlIsSetToSonarCloudUrlIfSet()
        {
            var propertiesWriter = new PropertiesWriter(new AnalysisConfig(), new TestLogger());
            propertiesWriter.WriteGlobalSettings([new(SonarProperties.SonarcloudUrl, "http://example.org")]);
            propertiesWriter.Flush().NormalizeLineEndings().Should().Be("""
                sonar.scanner.sonarcloudUrl=http://example.org
                sonar.host.url=http://example.org

                sonar.modules=


                """.NormalizeLineEndings());
        }

        [TestMethod]
        public void WriteGlobalSettings_HostUrlIsKeptIfHostUrlAndSonarcloudUrlAreSet()
        {
            var propertiesWriter = new PropertiesWriter(new AnalysisConfig(), new TestLogger());
            propertiesWriter.WriteGlobalSettings([
                new(SonarProperties.SonarcloudUrl, "http://SonarcloudUrl.org"),
                new(SonarProperties.HostUrl, "http://HostUrl.org"),
            ]);
            propertiesWriter.Flush().NormalizeLineEndings().Should().Be("""
                sonar.scanner.sonarcloudUrl=http://SonarcloudUrl.org
                sonar.host.url=http://HostUrl.org

                sonar.modules=


                """.NormalizeLineEndings());
        }

        [TestMethod]
        public void WriteSharedProperties_EmptySources_EmptyTests()
        {
            var propertiesWriter = new PropertiesWriter(new(), new TestLogger());
            propertiesWriter.WriteSharedFiles(new([], []));
            propertiesWriter.Flush().Should().Be("""

                sonar.modules=


                """);
        }

        [TestMethod]
        public void WriteSharedProperties_WithSources_EmptyTests()
        {
            var propertiesWriter = new PropertiesWriter(new(), new TestLogger());
            propertiesWriter.WriteSharedFiles(new([new("C:/dev/main.hs"), new("C:/dev/lambdas.hs")], []));
            propertiesWriter.Flush().Should().Be("""
                sonar.sources=\
                C:\\dev\\main.hs,\
                C:\\dev\\lambdas.hs

                sonar.modules=


                """);
        }

        [TestMethod]
        public void WriteSharedProperties_EmptySources_WithTests()
        {
            var propertiesWriter = new PropertiesWriter(new(), new TestLogger());
            propertiesWriter.WriteSharedFiles(new([], [new("C:/dev/test.hs"), new("C:/dev/test2.hs")]));
            propertiesWriter.Flush().Should().Be("""
                sonar.tests=\
                C:\\dev\\test.hs,\
                C:\\dev\\test2.hs

                sonar.modules=


                """);
        }

        [TestMethod]
        public void WriteSharedProperties_WithSources_WithTests()
        {
            var propertiesWriter = new PropertiesWriter(new(), new TestLogger());
            propertiesWriter.WriteSharedFiles(new([new("C:/dev/main.hs")], [new("C:/dev/test.hs")]));
            propertiesWriter.Flush().Should().Be("""
                sonar.sources=\
                C:\\dev\\main.hs
                sonar.tests=\
                C:\\dev\\test.hs

                sonar.modules=


                """);
        }

        [TestMethod]
        public void WriteAnalyzerOutputPaths_ForUnexpectedLanguage_DoNotWritesOutPaths()
        {
            var config = new AnalysisConfig();
            var propertiesWriter = new PropertiesWriter(config, new TestLogger());
            var someGuid = new Guid("5762C17D-1DDF-4C77-86AC-E2B4940926A9");

            var projectInfo = new ProjectInfo() { ProjectGuid = someGuid, ProjectLanguage = "unexpected" };
            var projectData = new ProjectData(projectInfo);
            projectData.AnalyzerOutPaths.Add(new FileInfo(@"c:\dir1\dir2"));

            propertiesWriter.WriteAnalyzerOutputPaths(projectData);

            propertiesWriter.Flush().Should().Be(
@"sonar.modules=

");
        }

        [DataTestMethod]
        [DataRow(ProjectLanguages.CSharp, "sonar.cs.analyzer.projectOutPaths")]
        [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.analyzer.projectOutPaths")]
        public void WriteAnalyzerOutputPaths_WritesEncodedPaths(string language, string expectedPropertyKey)
        {
            var config = new AnalysisConfig();
            var propertiesWriter = new PropertiesWriter(config, new TestLogger());
            var someGuid = new Guid("5762C17D-1DDF-4C77-86AC-E2B4940926A9");

            var projectInfo = new ProjectInfo() { ProjectGuid = someGuid, ProjectLanguage = language };
            var projectData = new ProjectData(projectInfo);
            projectData.AnalyzerOutPaths.Add(new FileInfo(@"c:\dir1\first"));
            projectData.AnalyzerOutPaths.Add(new FileInfo(@"c:\dir1\second"));

            propertiesWriter.WriteAnalyzerOutputPaths(projectData);

            propertiesWriter.Flush().Should().Be(
$@"5762C17D-1DDF-4C77-86AC-E2B4940926A9.{expectedPropertyKey}=\
c:\\dir1\\first,\
c:\\dir1\\second
sonar.modules=

");
        }

        [TestMethod]
        public void WriteRoslynReportPaths_ForUnexpectedLanguage_DoNotWritesOutPaths()
        {
            var config = new AnalysisConfig();
            var propertiesWriter = new PropertiesWriter(config, new TestLogger());
            var someGuid = new Guid("5762C17D-1DDF-4C77-86AC-E2B4940926A9");

            var projectInfo = new ProjectInfo() { ProjectGuid = someGuid, ProjectLanguage = "unexpected" };
            var projectData = new ProjectData(projectInfo);
            projectData.RoslynReportFilePaths.Add(new FileInfo(@"c:\dir1\dir2"));

            propertiesWriter.WriteRoslynReportPaths(projectData);

            propertiesWriter.Flush().Should().Be(
@"sonar.modules=

");
        }

        [DataTestMethod]
        [DataRow(ProjectLanguages.CSharp, "sonar.cs.roslyn.reportFilePaths")]
        [DataRow(ProjectLanguages.VisualBasic, "sonar.vbnet.roslyn.reportFilePaths")]
        public void WriteRoslynReportPaths_WritesEncodedPaths(string language, string expectedPropertyKey)
        {
            var config = new AnalysisConfig();
            var propertiesWriter = new PropertiesWriter(config, new TestLogger());
            var someGuid = new Guid("5762C17D-1DDF-4C77-86AC-E2B4940926A9");

            var projectInfo = new ProjectInfo() { ProjectGuid = someGuid, ProjectLanguage = language };
            var projectData = new ProjectData(projectInfo);
            projectData.RoslynReportFilePaths.Add(new FileInfo(@"c:\dir1\first"));
            projectData.RoslynReportFilePaths.Add(new FileInfo(@"c:\dir1\second"));

            propertiesWriter.WriteRoslynReportPaths(projectData);

            propertiesWriter.Flush().Should().Be(
$@"5762C17D-1DDF-4C77-86AC-E2B4940926A9.{expectedPropertyKey}=\
c:\\dir1\\first,\
c:\\dir1\\second
sonar.modules=

");
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
            var productCS = new ProjectData(CreateProjectInfo("你好", "DB2E5521-3172-47B9-BA50-864F12E6DFFF", productProject, false, productFiles, productFileListFilePath, productCoverageFilePath, ProjectLanguages.CSharp, "UTF-8"));
            productCS.SonarQubeModuleFiles.Add(productFile);
            productCS.SonarQubeModuleFiles.Add(productChineseFile);
            productCS.SonarQubeModuleFiles.Add(missingFileOutsideProjectDir);

            var productVB = new ProjectData(CreateProjectInfo("vbProject", "B51622CF-82F4-48C9-9F38-FB981FAFAF3A", productProject, false, productFiles, productFileListFilePath, productCoverageFilePath, ProjectLanguages.VisualBasic, "UTF-8"));
            productVB.SonarQubeModuleFiles.Add(productFile);

            var testBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "PropertiesWriterTest_TestBaseDir");
            var testProject = CreateEmptyFile(testBaseDir, "MyTest.csproj");
            var testFile = CreateEmptyFile(testBaseDir, "File.cs");
            var testFileListFilePath = Path.Combine(testBaseDir, "testManagedFiles.txt");

            var testFiles = new List<FileInfo>
            {
                testFile
            };
            var test = new ProjectData(CreateProjectInfo("my_test_project", "DA0FCD82-9C5C-4666-9370-C7388281D49B", testProject, true, testFiles, testFileListFilePath, null, ProjectLanguages.VisualBasic, "UTF-8"));
            test.SonarQubeModuleFiles.Add(testFile);

            var config = new AnalysisConfig()
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
                var writer = new PropertiesWriter(config, new TestLogger());
                writer.WriteSettingsForProject(productCS);
                writer.WriteSettingsForProject(productVB);
                writer.WriteSettingsForProject(test);

                actual = writer.Flush();
            }

            var expected = string.Format(System.Globalization.CultureInfo.InvariantCulture,
@"DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectKey=my_project_key:DB2E5521-3172-47B9-BA50-864F12E6DFFF
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectName=\u4F60\u597D
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectBaseDir={0}
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.sourceEncoding=utf-8
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.sources=\
{0}\\File.cs,\
{0}\\\u4F60\u597D.cs,\
{2}

DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.working.directory=C:\\my_folder\\.sonar\\mod0
B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectKey=my_project_key:B51622CF-82F4-48C9-9F38-FB981FAFAF3A
B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectName=vbProject
B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectBaseDir={0}
B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.sourceEncoding=utf-8
B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.sources=\
{0}\\File.cs

B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.working.directory=C:\\my_folder\\.sonar\\mod1
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectKey=my_project_key:DA0FCD82-9C5C-4666-9370-C7388281D49B
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectName=my_test_project
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectBaseDir={1}
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.sourceEncoding=utf-8
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.sources=
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.tests=\
{1}\\File.cs

DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.working.directory=C:\\my_folder\\.sonar\\mod2
sonar.modules=DB2E5521-3172-47B9-BA50-864F12E6DFFF,B51622CF-82F4-48C9-9F38-FB981FAFAF3A,DA0FCD82-9C5C-4666-9370-C7388281D49B

",
 PropertiesWriter.Escape(productBaseDir),
 PropertiesWriter.Escape(testBaseDir),
 PropertiesWriter.Escape(missingFileOutsideProjectDir.FullName));

            SaveToResultFile(productBaseDir, "Expected.txt", expected.ToString());
            SaveToResultFile(productBaseDir, "Actual.txt", actual);

            actual.Should().Be(expected);
        }

        [TestMethod]
        public void WriteSonarProjectInfo_WritesAllValues()
        {
            var config = new AnalysisConfig()
            {
                SonarProjectKey = "my_project_key",
                SonarProjectName = "my_project_name",
                SonarProjectVersion = "4.2",
                SonarOutputDir = @"C:\OutpuDir",
            };
            config.SetConfigValue(SonarProperties.PullRequestCacheBasePath, @"C:\PullRequest\Cache\BasePath");
            var writer = new PropertiesWriter(config, Substitute.For<ILogger>());
            writer.WriteSonarProjectInfo(new DirectoryInfo(@"C:\ProjectBaseDir"));

            writer.Flush().Should().Be(
@"sonar.projectKey=my_project_key
sonar.projectName=my_project_name
sonar.projectVersion=4.2
sonar.working.directory=C:\\OutpuDir\\.sonar
sonar.projectBaseDir=C:\\ProjectBaseDir
sonar.pullrequest.cache.basepath=C:\\PullRequest\\Cache\\BasePath
sonar.modules=

");
        }

        [TestMethod]
        public void WriteSonarProjectInfo_EmptyValues()
        {
            var config = new AnalysisConfig() { SonarOutputDir = @"C:\OutputDir\CannotBeEmpty" };
            var writer = new PropertiesWriter(config, Substitute.For<ILogger>());
            writer.WriteSonarProjectInfo(new DirectoryInfo(@"C:\ProjectBaseDir"));

            writer.Flush().Should().Be(
@"sonar.projectKey=
sonar.working.directory=C:\\OutputDir\\CannotBeEmpty\\.sonar
sonar.projectBaseDir=C:\\ProjectBaseDir
sonar.pullrequest.cache.basepath=
sonar.modules=

");
        }

        [TestMethod]
        public void Ctor_WhenAnalysisConfigIsNull_ThrowsArgumentNulLException()
        {
            // Arrange
            Action act = () => new PropertiesWriter(null, new TestLogger());

            // Act & Assert
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
        }

        [TestMethod]
        public void Ctor_WhenLoggerIsNull_ThrowsArgumentNulLException()
        {
            // Arrange
            Action act = () => new PropertiesWriter(new AnalysisConfig(), null);

            // Act & Assert
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void Flush_WhenCalledTwice_ThrowsInvalidOperationException()
        {
            // Arrange
            var validConfig = new AnalysisConfig()
            {
                SonarProjectKey = "key",
                SonarProjectName = "name",
                SonarProjectVersion = "1.0",
                SonarOutputDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext)
            };

            var writer = new PropertiesWriter(validConfig, new TestLogger());
            writer.Flush();

            // Act & Assert
            Action act = () => writer.Flush();
            act.Should().ThrowExactly<InvalidOperationException>();
        }

        [TestMethod]
        public void WriteSettingsForProject_WhenFlushed_ThrowsInvalidOperationException()
        {
            // Arrange
            var validConfig = new AnalysisConfig()
            {
                SonarProjectKey = "key",
                SonarProjectName = "name",
                SonarProjectVersion = "1.0",
                SonarOutputDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext)
            };

            var writer = new PropertiesWriter(validConfig, new TestLogger());
            writer.Flush();

            // Act & Assert
            using (new AssertIgnoreScope())
            {
                Action act = () => writer.WriteSettingsForProject(new ProjectData(new ProjectInfo()));
                act.Should().ThrowExactly<InvalidOperationException>();
            }
        }

        [TestMethod]
        public void PropertiesWriter_AnalysisSettingsWritten()
        {
            // Tests that analysis settings in the ProjectInfo are written to the file
            // Arrange
            var projectBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "PropertiesWriterTest_AnalysisSettingsWritten");
            var productProject = CreateEmptyFile(projectBaseDir, "MyProduct.csproj");

            var productFile = CreateEmptyFile(projectBaseDir, "File.cs");
            var productFiles = new List<FileInfo>
            {
                productFile
            };
            var productFileListFilePath = Path.Combine(projectBaseDir, "productManagedFiles.txt");

            var product = new ProjectData(CreateProjectInfo("AnalysisSettingsTest.proj", "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5", productProject, false, productFiles, productFileListFilePath, null, "language", "UTF-8"));

            var config = new AnalysisConfig()
            {
                SonarOutputDir = @"C:\my_folder"
            };

            // These are the settings we are going to check. The other analysis values are not checked.
            product.Project.AnalysisSettings = new AnalysisProperties
            {
                new("my.setting1", "setting1"),
                new("my.setting2", "setting 2 with spaces"),
                new("my.setting.3", @"c:\dir1\dir2\foo.txt") // path that will be escaped
            };
            product.ReferencedFiles.Add(productFile);
            // Act
            var writer = new PropertiesWriter(config, new TestLogger());
            writer.WriteSettingsForProject(product);
            var fullActualPath = SaveToResultFile(projectBaseDir, "Actual.txt", writer.Flush());

            // Assert
            var propertyReader = new SQPropertiesFileReader(fullActualPath);

            propertyReader.AssertSettingExists("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting1", "setting1");
            propertyReader.AssertSettingExists("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting2", "setting 2 with spaces");
            propertyReader.AssertSettingExists("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting.3", @"c:\dir1\dir2\foo.txt");
        }

        [TestMethod]
        public void PropertiesWriter_WorkdirPerModuleExplicitlySet()
        {
            // Tests that .sonar.working.directory is explicitly set per module

            // Arrange
            var projectBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "PropertiesWriterTest_AnalysisSettingsWritten");
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

            var config = new AnalysisConfig()
            {
                SonarOutputDir = @"C:\my_folder"
            };

            // Act
            var writer = new PropertiesWriter(config, new TestLogger());
            writer.WriteSettingsForProject(product);
            writer.WriteSonarProjectInfo(new DirectoryInfo("dummy basedir"));
            var s = writer.Flush();

            var props = new JavaProperties();
            props.Load(GenerateStreamFromString(s));
            var key = projectKey + "." + SonarProperties.WorkingDirectory;
#pragma warning disable DictionaryShouldContainKey // Simplify Assertion
            props.ContainsKey(key).Should().BeTrue();
#pragma warning restore DictionaryShouldContainKey // Simplify Assertion
        }

        public static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        [TestMethod]
        public void PropertiesWriter_GlobalSettingsWritten()
        {
            // Tests that global settings in the ProjectInfo are written to the file

            // Arrange
            var projectBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "PropertiesWriterTest_GlobalSettingsWritten");

            var config = new AnalysisConfig()
            {
                SonarOutputDir = @"C:\my_folder"
            };

            var globalSettings = new AnalysisProperties
            {
                new("my.setting1", "setting1"),
                new("my.setting2", "setting 2 with spaces"),
                new("my.setting.3", @"c:\dir1\dir2\foo.txt"), // path that will be escaped
                // Specific test for sonar.branch property
                new("sonar.branch", "aBranch") // path that will be escaped
            };

            // Act
            var writer = new PropertiesWriter(config, new TestLogger());
            writer.WriteGlobalSettings(globalSettings);
            var fullActualPath = SaveToResultFile(projectBaseDir, "Actual.txt", writer.Flush());

            // Assert
            var propertyReader = new SQPropertiesFileReader(fullActualPath);

            propertyReader.AssertSettingExists("my.setting1", "setting1");
            propertyReader.AssertSettingExists("my.setting2", "setting 2 with spaces");
            propertyReader.AssertSettingExists("my.setting.3", @"c:\dir1\dir2\foo.txt");

            propertyReader.AssertSettingExists("sonar.branch", "aBranch");
        }

        [TestMethod]
        public void EncodeAsMultiValueProperty_WhenSQGreaterThanOrEqualTo65_EscapeAndJoinPaths()
        {
            // Arrange
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

            var testSubject65 = new PropertiesWriter(config65, new TestLogger());
            var testSubject66 = new PropertiesWriter(config66, new TestLogger());

            var paths = new[] { "C:\\foo.cs", "C:\\foo,bar.cs", "C:\\foo\"bar.cs" };

            // Act
            var actual65 = testSubject65.EncodeAsMultiValueProperty(paths);
            var actual66 = testSubject66.EncodeAsMultiValueProperty(paths);

            // Assert
            actual65.Should().Be(@"""C:\foo.cs"",\
""C:\foo,bar.cs"",\
""C:\foo""""bar.cs""");
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

        private void EncodeAsMultiValueProperty_WhenGivenSQVersionAndNoInvalidPath_JoinPaths(string sonarqubeVersion)
        {
            // Arrange
            var config = new AnalysisConfig
            {
                SonarOutputDir = @"C:\my_folder",
                SonarQubeVersion = sonarqubeVersion
            };

            var testSubject = new PropertiesWriter(config, new TestLogger());
            var paths = new[] { "C:\\foo.cs", "C:\\foobar.cs" };

            // Act
            var actual = testSubject.EncodeAsMultiValueProperty(paths);

            // Assert
            actual.Should().Be(@"C:\foo.cs,\
C:\foobar.cs");
        }

        [TestMethod]
        public void EncodeAsMultiValueProperty_WhenSQLessThan65AndInvalidPath_ExcludeInvalidPathAndJoinOthers() =>
            EncodeAsMultiValueProperty_WhenGivenSQVersionAndInvalidPath_ExcludeInvalidPathAndJoinOthers("6.0");

        [TestMethod]
        public void EncodeAsMultiValueProperty_WhenSQVersionIsNullAndInvalidPath_ExcludeInvalidPathAndJoinOthers() =>
            EncodeAsMultiValueProperty_WhenGivenSQVersionAndInvalidPath_ExcludeInvalidPathAndJoinOthers(null);

        [TestMethod]
        public void EncodeAsMultiValueProperty_WhenSQVersionNotAVersionAndInvalidPath_ExcludeInvalidPathAndJoinOthers() =>
            EncodeAsMultiValueProperty_WhenGivenSQVersionAndInvalidPath_ExcludeInvalidPathAndJoinOthers("foo");

        private void EncodeAsMultiValueProperty_WhenGivenSQVersionAndInvalidPath_ExcludeInvalidPathAndJoinOthers(string sonarqubeVersion)
        {
            // Arrange
            var config = new AnalysisConfig
            {
                SonarOutputDir = @"C:\my_folder",
                SonarQubeVersion = sonarqubeVersion
            };

            var logger = new TestLogger();
            var testSubject = new PropertiesWriter(config, logger);
            var paths = new[] { "C:\\foo.cs", "C:\\foo,bar.cs" };

            // Act
            var actual = testSubject.EncodeAsMultiValueProperty(paths);

            // Assert
            actual.Should().Be(@"C:\foo.cs");
            logger.Warnings.Should().HaveCount(1);
            logger.Warnings[0].Should().Be("The following paths contain invalid characters and will be excluded from this analysis: C:\\foo,bar.cs");
        }

        private static ProjectInfo CreateProjectInfo(string name, string projectId, FileInfo fullFilePath, bool isTest, IEnumerable<FileInfo> files,
            string fileListFilePath, string coverageReportPath, string language, string encoding)
        {
            var projectInfo = new ProjectInfo()
            {
                ProjectName = name,
                ProjectGuid = Guid.Parse(projectId),
                FullPath = fullFilePath.FullName,
                ProjectType = isTest ? ProjectType.Test : ProjectType.Product,
                AnalysisResults = new List<AnalysisResult>(),
                ProjectLanguage = language,
                Encoding = encoding
            };

            if (coverageReportPath != null)
            {
                projectInfo.AddAnalyzerResult(AnalysisType.VisualStudioCodeCoverage, coverageReportPath);
            }

            if (files != null && files.Any())
            {
                string.IsNullOrWhiteSpace(fileListFilePath).Should().BeFalse("Test setup error: must supply the managedFileListFilePath as a list of files has been supplied");
                File.WriteAllLines(fileListFilePath, files.Select(x => x.FullName));

                projectInfo.AddAnalyzerResult(AnalysisType.FilesToAnalyze, fileListFilePath);
            }

            return projectInfo;
        }

        private static FileInfo CreateEmptyFile(string parentDir, string fileName)
        {
            return new FileInfo(CreateFile(parentDir, fileName, string.Empty));
        }

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
    }
}
