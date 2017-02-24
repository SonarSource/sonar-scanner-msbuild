/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NFluent;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            Assert.AreEqual("foo", SonarScanner.Shim.PropertiesWriter.Escape("foo"));
            Assert.AreEqual(@"C:\\File.cs", SonarScanner.Shim.PropertiesWriter.Escape(@"C:\File.cs"));
            Assert.AreEqual(@"\u4F60\u597D", SonarScanner.Shim.PropertiesWriter.Escape("你好"));
            Assert.AreEqual(@"\u000A", SonarScanner.Shim.PropertiesWriter.Escape("\n"));
        }

        [TestMethod]
        public void PropertiesWriterToString()
        {
            var productBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_ProductBaseDir");
            string productProject = CreateEmptyFile(productBaseDir, "MyProduct.csproj");
            string productFile = CreateEmptyFile(productBaseDir, "File.cs");
            string productChineseFile = CreateEmptyFile(productBaseDir, "你好.cs");

            string productFxCopFilePath = CreateEmptyFile(productBaseDir, "productFxCopReport.txt");
            string productCoverageFilePath = CreateEmptyFile(productBaseDir, "productCoverageReport.txt");
            string productFileListFilePath = Path.Combine(productBaseDir, "productManagedFiles.txt");

            string otherDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_OtherDir");
            string missingFileOutsideProjectDir = Path.Combine(otherDir, "missing.cs");

            List<string> productFiles = new List<string>();
            productFiles.Add(productFile);
            productFiles.Add(productChineseFile);
            productFiles.Add(missingFileOutsideProjectDir);
            ProjectInfo productCS = CreateProjectInfo("你好", "DB2E5521-3172-47B9-BA50-864F12E6DFFF", productProject, false, productFiles, productFileListFilePath, productFxCopFilePath, productCoverageFilePath, ProjectLanguages.CSharp);
            ProjectInfo productVB = CreateProjectInfo("vbProject", "B51622CF-82F4-48C9-9F38-FB981FAFAF3A", productProject, false, productFiles, productFileListFilePath, productFxCopFilePath, productCoverageFilePath, ProjectLanguages.VisualBasic);

            string testBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_TestBaseDir");
            string testProject = CreateEmptyFile(testBaseDir, "MyTest.csproj");
            string testFile = CreateEmptyFile(testBaseDir, "File.cs");
            string testFileListFilePath = Path.Combine(testBaseDir, "testManagedFiles.txt");

            List<string> testFiles = new List<string>();
            testFiles.Add(testFile);
            ProjectInfo test = CreateProjectInfo("my_test_project", "DA0FCD82-9C5C-4666-9370-C7388281D49B", testProject, true, testFiles, testFileListFilePath, null, null, ProjectLanguages.VisualBasic);

            AnalysisConfig config = new AnalysisConfig()
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
                PropertiesWriter writer = new PropertiesWriter(config);
                writer.WriteSettingsForProject(productCS, new string[] { productFile, productChineseFile, missingFileOutsideProjectDir }, productFxCopFilePath, productCoverageFilePath);
                writer.WriteSettingsForProject(productVB, new string[] { productFile }, productFxCopFilePath, null);
                writer.WriteSettingsForProject(test, new string[] { testFile }, null, null);

                actual = writer.Flush();
            }

            string expected = string.Format(System.Globalization.CultureInfo.InvariantCulture,
@"DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectKey=my_project_key:DB2E5521-3172-47B9-BA50-864F12E6DFFF
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectName=\u4F60\u597D
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectBaseDir={0}
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.cs.fxcop.reportPath={1}
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.cs.vscoveragexml.reportsPaths={2}
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.sources=\
{0}\\File.cs,\
{0}\\\u4F60\u597D.cs,\
{4}

B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectKey=my_project_key:B51622CF-82F4-48C9-9F38-FB981FAFAF3A
B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectName=vbProject
B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.projectBaseDir={0}
B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.vbnet.fxcop.reportPath={1}
B51622CF-82F4-48C9-9F38-FB981FAFAF3A.sonar.sources=\
{0}\\File.cs

DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectKey=my_project_key:DA0FCD82-9C5C-4666-9370-C7388281D49B
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectName=my_test_project
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectBaseDir={3}
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.sources=
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.tests=\
{3}\\File.cs

sonar.modules=DB2E5521-3172-47B9-BA50-864F12E6DFFF,B51622CF-82F4-48C9-9F38-FB981FAFAF3A,DA0FCD82-9C5C-4666-9370-C7388281D49B

",
 PropertiesWriter.Escape(productBaseDir),
 PropertiesWriter.Escape(productFxCopFilePath),
 PropertiesWriter.Escape(productCoverageFilePath),
 PropertiesWriter.Escape(testBaseDir),
 PropertiesWriter.Escape(missingFileOutsideProjectDir),
 PropertiesWriter.Escape(config.SourcesDirectory));

            SaveToResultFile(productBaseDir, "Expected.txt", expected.ToString());
            SaveToResultFile(productBaseDir, "Actual.txt", actual);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void PropertiesWriter_FxCopReportForUnrecognisedLanguage()
        {
            var productBaseDir = TestUtils.CreateTestSpecificFolder(TestContext);
            string productProject = CreateEmptyFile(productBaseDir, "MyProduct.proj");
            string productFile = CreateEmptyFile(productBaseDir, "File.txt");
            string productFxCopFilePath = CreateEmptyFile(productBaseDir, "productFxCopReport.txt");
            string productFileListFilePath = Path.Combine(productBaseDir, "productFileList.txt");

            List<string> productFiles = new List<string>();
            productFiles.Add(productFile);
            ProjectInfo product = CreateProjectInfo("myproduct", "9507E2E6-7342-4A04-9CB9-B0C47C937019", productProject, false, productFiles, productFileListFilePath, productFxCopFilePath, null, "my.language");

            AnalysisConfig config = new AnalysisConfig()
            {
                SonarProjectKey = "my_project_key",
                SonarProjectName = "my_project_name",
                SonarProjectVersion = "1.0",
                SonarOutputDir = @"C:\my_folder",
                SourcesDirectory = @"D:\sources"
            };

            string actual = null;
            using (new AssertIgnoreScope()) // expecting the property writer to complain about having an FxCop report for an unknown language
            {
                PropertiesWriter writer = new PropertiesWriter(config);
                writer.WriteSettingsForProject(product, new string[] { productFile, }, productFxCopFilePath, null);
                actual = writer.Flush();
            }

            string expected = string.Format(System.Globalization.CultureInfo.InvariantCulture,
@"9507E2E6-7342-4A04-9CB9-B0C47C937019.sonar.projectKey=my_project_key:9507E2E6-7342-4A04-9CB9-B0C47C937019
9507E2E6-7342-4A04-9CB9-B0C47C937019.sonar.projectName=myproduct
9507E2E6-7342-4A04-9CB9-B0C47C937019.sonar.projectBaseDir={0}
9507E2E6-7342-4A04-9CB9-B0C47C937019.sonar.sources=\
{0}\\File.txt

sonar.modules=9507E2E6-7342-4A04-9CB9-B0C47C937019

",
 PropertiesWriter.Escape(productBaseDir),
 PropertiesWriter.Escape(config.SourcesDirectory));

            SaveToResultFile(productBaseDir, "Expected.txt", expected.ToString());
            SaveToResultFile(productBaseDir, "Actual.txt", actual);

            Assert.AreEqual(expected, actual);
        }

       
        [TestMethod]
        public void PropertiesWriter_InvalidOperations()
        {
            AnalysisConfig validConfig = new AnalysisConfig()
            {
                SonarProjectKey = "key",
                SonarProjectName = "name",
                SonarProjectVersion = "1.0",
                SonarOutputDir = this.TestContext.DeploymentDirectory
            };

            // 1. Must supply an analysis config on construction
            AssertException.Expects<ArgumentNullException>(() => new PropertiesWriter(null));


            // 2. Can't call WriteSettingsForProject after Flush
            PropertiesWriter writer = new PropertiesWriter(validConfig);
            writer.Flush();
            AssertException.Expects<InvalidOperationException>(() => writer.Flush());

            // 3. Can't call Flush twice
            writer = new PropertiesWriter(validConfig);
            writer.Flush();
            using (new AssertIgnoreScope())
            {
                AssertException.Expects<InvalidOperationException>(() => writer.WriteSettingsForProject(new ProjectInfo(), new string[] { "file" }, "fxCopReport", "code coverage report"));
            }
        }

        [TestMethod]
        public void PropertiesWriter_AnalysisSettingsWritten()
        {
            // Tests that analysis settings in the ProjectInfo are written to the file
            // Arrange
            string projectBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_AnalysisSettingsWritten");
            string productProject = CreateEmptyFile(projectBaseDir, "MyProduct.csproj");

            string productFile = CreateEmptyFile(projectBaseDir, "File.cs");
            List<string> productFiles = new List<string>();
            productFiles.Add(productFile);
            string productFileListFilePath = Path.Combine(projectBaseDir, "productManagedFiles.txt");

            ProjectInfo product = CreateProjectInfo("AnalysisSettingsTest.proj", "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5", productProject, false, productFiles, productFileListFilePath, null, null, "language");

            List<ProjectInfo> projects = new List<ProjectInfo>();
            projects.Add(product);

            AnalysisConfig config = new AnalysisConfig()
            {
                SonarOutputDir = @"C:\my_folder"
            };

            // These are the settings we are going to check. The other analysis values are not checked.
            product.AnalysisSettings = new AnalysisProperties();
            product.AnalysisSettings.Add(new Property() { Id = "my.setting1", Value = "setting1" });
            product.AnalysisSettings.Add(new Property() { Id = "my.setting2", Value = "setting 2 with spaces" });
            product.AnalysisSettings.Add(new Property() { Id = "my.setting.3", Value = @"c:\dir1\dir2\foo.txt" }); // path that will be escaped

            // Act
            PropertiesWriter writer = new PropertiesWriter(config);
            writer.WriteSettingsForProject(product, new string[] { productFile }, null, null);
            string fullActualPath = SaveToResultFile(projectBaseDir, "Actual.txt", writer.Flush());

            // Assert
            SQPropertiesFileReader propertyReader = new SQPropertiesFileReader(fullActualPath);

            propertyReader.AssertSettingExists("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting1", "setting1");
            propertyReader.AssertSettingExists("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting2", "setting 2 with spaces");
            propertyReader.AssertSettingExists("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting.3", @"c:\\dir1\\dir2\\foo.txt");
        }

        [TestMethod]
        public void PropertiesWriter_GlobalSettingsWritten()
        {
            // Tests that global settings in the ProjectInfo are written to the file

            // Arrange
            string projectBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_GlobalSettingsWritten");

            AnalysisConfig config = new AnalysisConfig()
            {
                SonarOutputDir = @"C:\my_folder"
            };

            AnalysisProperties globalSettings = new AnalysisProperties();
            globalSettings.Add(new Property() { Id = "my.setting1", Value = "setting1" });
            globalSettings.Add(new Property() { Id = "my.setting2", Value = "setting 2 with spaces" });
            globalSettings.Add(new Property() { Id = "my.setting.3", Value = @"c:\dir1\dir2\foo.txt" }); // path that will be escaped

            // Specific test for sonar.branch property
            globalSettings.Add(new Property() { Id = "sonar.branch", Value = "aBranch" }); // path that will be escaped

            // Act
            PropertiesWriter writer = new PropertiesWriter(config);
            writer.WriteGlobalSettings(globalSettings);
            string fullActualPath = SaveToResultFile(projectBaseDir, "Actual.txt", writer.Flush());

            // Assert
            SQPropertiesFileReader propertyReader = new SQPropertiesFileReader(fullActualPath);

            propertyReader.AssertSettingExists("my.setting1", "setting1");
            propertyReader.AssertSettingExists("my.setting2", "setting 2 with spaces");
            propertyReader.AssertSettingExists("my.setting.3", @"c:\\dir1\\dir2\\foo.txt");

            propertyReader.AssertSettingExists("sonar.branch", "aBranch");
        }

        #endregion

        #region Private methods

       

        private static ProjectInfo CreateProjectInfo(string name, string projectId, string fullFilePath, bool isTest, IEnumerable<string> files, string fileListFilePath, string fxCopReportPath, string coverageReportPath, string language)
        {
            ProjectInfo projectInfo = new ProjectInfo()
            {
                ProjectName = name,
                ProjectGuid = Guid.Parse(projectId),
                FullPath = fullFilePath,
                ProjectType = isTest ? ProjectType.Test : ProjectType.Product,
                AnalysisResults = new List<AnalysisResult>(),
                ProjectLanguage = language
            };

            if (fxCopReportPath != null)
            {
                projectInfo.AddAnalyzerResult(AnalysisType.FxCop, fxCopReportPath);
            }
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
            string fullPath = Path.Combine(parentDir, fileName);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        private string SaveToResultFile(string testDir, string fileName, string content)
        {
            string fullPath = CreateFile(testDir, fileName, content);
            this.TestContext.AddResultFile(fullPath);
            return fullPath;
        }

        #endregion
    }
}
