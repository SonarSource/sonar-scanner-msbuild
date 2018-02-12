/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using TestUtilities;

namespace SonarScanner.Shim.Tests
{
    [TestClass]
    public class PropertiesWriterTest
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PropertiesWriterEscape()
        {
            Assert.AreEqual("foo", PropertiesWriter.Escape("foo"));
            Assert.AreEqual(@"C:\\File.cs", PropertiesWriter.Escape(@"C:\File.cs"));
            Assert.AreEqual(@"\u4F60\u597D", PropertiesWriter.Escape("你好"));
            Assert.AreEqual(@"\u000A", PropertiesWriter.Escape("\n"));
        }

        [TestMethod]
        public void PropertiesWriterToString()
        {
            var productBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_ProductBaseDir");
            var productProject = CreateEmptyFile(productBaseDir, "MyProduct.csproj");
            var productFile = CreateEmptyFile(productBaseDir, "File.cs");
            var productChineseFile = CreateEmptyFile(productBaseDir, "你好.cs");

            var productCoverageFilePath = CreateEmptyFile(productBaseDir, "productCoverageReport.txt");
            var productTrxPath = CreateEmptyFile(productBaseDir, "productTrx.trx");
            var productFileListFilePath = Path.Combine(productBaseDir, "productManagedFiles.txt");

            var otherDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_OtherDir");
            var missingFileOutsideProjectDir = Path.Combine(otherDir, "missing.cs");

            var productFiles = new List<string>
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

            var testBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_TestBaseDir");
            var testProject = CreateEmptyFile(testBaseDir, "MyTest.csproj");
            var testFile = CreateEmptyFile(testBaseDir, "File.cs");
            var testFileListFilePath = Path.Combine(testBaseDir, "testManagedFiles.txt");

            var testFiles = new List<string>
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
                var writer = new PropertiesWriter(config);
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

B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectKey=my_project_key:B51622CF-82F4-48C9-9F38-FB981FAFAF3A
B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectName=vbProject
B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectBaseDir={0}
B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.sourceEncoding=utf-8
B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.sources=\
{0}\\File.cs

DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectKey=my_project_key:DA0FCD82-9C5C-4666-9370-C7388281D49B
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectName=my_test_project
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectBaseDir={1}
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.sourceEncoding=utf-8
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.sources=
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.tests=\
{1}\\File.cs

sonar.modules=DB2E5521-3172-47B9-BA50-864F12E6DFFF,B51622CF-82F4-48C9-9F38-FB981FAFAF3A,DA0FCD82-9C5C-4666-9370-C7388281D49B

",
 PropertiesWriter.Escape(productBaseDir),
 PropertiesWriter.Escape(testBaseDir),
 PropertiesWriter.Escape(missingFileOutsideProjectDir));

            SaveToResultFile(productBaseDir, "Expected.txt", expected.ToString());
            SaveToResultFile(productBaseDir, "Actual.txt", actual);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void PropertiesWriter_InvalidOperations()
        {
            var validConfig = new AnalysisConfig()
            {
                SonarProjectKey = "key",
                SonarProjectName = "name",
                SonarProjectVersion = "1.0",
                SonarOutputDir = TestContext.DeploymentDirectory
            };

            // 1. Must supply an analysis config on construction
            AssertException.Expects<ArgumentNullException>(() => new PropertiesWriter(null));

            // 2. Can't call WriteSettingsForProject after Flush
            var writer = new PropertiesWriter(validConfig);
            writer.Flush();
            AssertException.Expects<InvalidOperationException>(() => writer.Flush());

            // 3. Can't call Flush twice
            writer = new PropertiesWriter(validConfig);
            writer.Flush();
            using (new AssertIgnoreScope())
            {
                AssertException.Expects<InvalidOperationException>(
                    () => writer.WriteSettingsForProject(new ProjectData(new ProjectInfo())));
            }
        }

        [TestMethod]
        public void PropertiesWriter_AnalysisSettingsWritten()
        {
            // Tests that analysis settings in the ProjectInfo are written to the file
            // Arrange
            var projectBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_AnalysisSettingsWritten");
            var productProject = CreateEmptyFile(projectBaseDir, "MyProduct.csproj");

            var productFile = CreateEmptyFile(projectBaseDir, "File.cs");
            var productFiles = new List<string>
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
                new Property() { Id = "my.setting1", Value = "setting1" },
                new Property() { Id = "my.setting2", Value = "setting 2 with spaces" },
                new Property() { Id = "my.setting.3", Value = @"c:\dir1\dir2\foo.txt" } // path that will be escaped
            };
            product.ReferencedFiles.Add(productFile);
            // Act
            var writer = new PropertiesWriter(config);
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
            // Tests that .sonar.working.directory is explicityl set per module

            // Arrange
            var projectBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_AnalysisSettingsWritten");
            var productProject = CreateEmptyFile(projectBaseDir, "MyProduct.csproj");

            var productFile = CreateEmptyFile(projectBaseDir, "File.cs");
            var productFiles = new List<string>
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
            var writer = new PropertiesWriter(config);
            writer.WriteSettingsForProject(product);
            writer.WriteSonarProjectInfo("dummy basedir");
            var s = writer.Flush();

            var props = new JavaProperties();
            props.Load(GenerateStreamFromString(s));
            var key = projectKey + "." + SonarProperties.WorkingDirectory;
            Assert.IsTrue(props.ContainsKey(key));
        }

        [TestMethod]
        public void PropertiesWriter_GlobalSettingsWritten()
        {
            // Tests that global settings in the ProjectInfo are written to the file

            // Arrange
            var projectBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_GlobalSettingsWritten");

            var config = new AnalysisConfig()
            {
                SonarOutputDir = @"C:\my_folder"
            };

            var globalSettings = new AnalysisProperties
            {
                new Property() { Id = "my.setting1", Value = "setting1" },
                new Property() { Id = "my.setting2", Value = "setting 2 with spaces" },
                new Property() { Id = "my.setting.3", Value = @"c:\dir1\dir2\foo.txt" }, // path that will be escaped

                // Specific test for sonar.branch property
                new Property() { Id = "sonar.branch", Value = "aBranch" } // path that will be escaped
            };

            // Act
            var writer = new PropertiesWriter(config);
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
        public void PropertiesWriter_WriteVisualStudioCoveragePaths()
        {
            // Arrange
            var projectBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_GlobalSettingsWritten");

            var config = new AnalysisConfig()
            {
                SonarOutputDir = @"C:\my_folder"
            };

            var writer = new PropertiesWriter(config);

            // Act
            writer.WriteVisualStudioCoveragePaths(new[] { "path1", "path2" });

            // Assert
            var actual = writer.Flush(); // Flush writes "sonar.properties" in addition to other written properties
            Assert.AreEqual("sonar.cs.vscoveragexml.reportsPaths=path1,path2\r\nsonar.modules=\r\n\r\n", actual);
        }

        [TestMethod]
        public void PropertiesWriter_WriteVisualStudioTestResultsPaths()
        {
            // Arrange
            var projectBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_GlobalSettingsWritten");

            var config = new AnalysisConfig()
            {
                SonarOutputDir = @"C:\my_folder"
            };

            var writer = new PropertiesWriter(config);

            // Act
            writer.WriteVisualStudioTestResultsPaths(new[] { "path1", "path2" });

            // Assert
            var actual = writer.Flush(); // Flush writes "sonar.properties" in addition to other written properties
            Assert.AreEqual("sonar.cs.vstest.reportsPaths=path1,path2\r\nsonar.modules=\r\n\r\n", actual);
        }

        #endregion Tests

        #region Private methods

        private static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        private static ProjectInfo CreateProjectInfo(string name, string projectId, string fullFilePath, bool isTest, IEnumerable<string> files,
            string fileListFilePath, string coverageReportPath, string language, string encoding)
        {
            var projectInfo = new ProjectInfo()
            {
                ProjectName = name,
                ProjectGuid = Guid.Parse(projectId),
                FullPath = fullFilePath,
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
                Assert.IsTrue(!string.IsNullOrWhiteSpace(fileListFilePath), "Test setup error: must supply the managedFileListFilePath as a list of files has been supplied");
                File.WriteAllLines(fileListFilePath, files);

                projectInfo.AddAnalyzerResult(AnalysisType.FilesToAnalyze, fileListFilePath);
            }

            return projectInfo;
        }

        private static string CreateEmptyFile(string parentDir, string fileName)
        {
            return CreateFile(parentDir, fileName, string.Empty);
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

        #endregion Private methods
    }
}
