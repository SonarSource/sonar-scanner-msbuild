//-----------------------------------------------------------------------
// <copyright file="PropertiesWriterTest.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarRunner.Shim.Tests
{
    [TestClass]
    public class PropertiesWriterTest
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PropertiesWriterEscape()
        {
            Assert.AreEqual("foo", SonarRunner.Shim.PropertiesWriter.Escape("foo"));
            Assert.AreEqual(@"C:\\File.cs", SonarRunner.Shim.PropertiesWriter.Escape(@"C:\File.cs"));
            Assert.AreEqual(@"\u4F60\u597D", SonarRunner.Shim.PropertiesWriter.Escape("你好"));
            Assert.AreEqual(@"\u000A", SonarRunner.Shim.PropertiesWriter.Escape("\n"));
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
                SonarOutputDir = @"C:\my_folder"
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

sonar.projectKey=my_project_key
sonar.projectName=my_project_name
sonar.projectVersion=1.0
sonar.projectBaseDir={5}
sonar.working.directory=C:\\my_folder\\.sonar

# FIXME: Encoding is hardcoded
sonar.sourceEncoding=UTF-8

sonar.modules=DB2E5521-3172-47B9-BA50-864F12E6DFFF,B51622CF-82F4-48C9-9F38-FB981FAFAF3A,DA0FCD82-9C5C-4666-9370-C7388281D49B

",
 PropertiesWriter.Escape(productBaseDir),
 PropertiesWriter.Escape(productFxCopFilePath),
 PropertiesWriter.Escape(productCoverageFilePath),
 PropertiesWriter.Escape(testBaseDir),
 PropertiesWriter.Escape(missingFileOutsideProjectDir),
 PropertiesWriter.Escape(TestUtils.GetTestSpecificFolderName(TestContext)));

            SaveToResultFile(productBaseDir, "Expected.txt", expected.ToString());
            SaveToResultFile(productBaseDir, "Actual.txt", actual);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void PropertiesWriter_FxCopRerportForUnrecognisedLanguage()
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
                SonarOutputDir = @"C:\my_folder"
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

sonar.projectKey=my_project_key
sonar.projectName=my_project_name
sonar.projectVersion=1.0
sonar.projectBaseDir={1}
sonar.working.directory=C:\\my_folder\\.sonar

# FIXME: Encoding is hardcoded
sonar.sourceEncoding=UTF-8

sonar.modules=9507E2E6-7342-4A04-9CB9-B0C47C937019

",
 PropertiesWriter.Escape(productBaseDir),
 PropertiesWriter.Escape(TestUtils.GetTestSpecificFolderName(TestContext)));

            SaveToResultFile(productBaseDir, "Expected.txt", expected.ToString());
            SaveToResultFile(productBaseDir, "Actual.txt", actual);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void PropertiesWriter_ComputeProjectBaseDir()
        {
            AssertHasProjectBaseDir(@"c:\src\A", @"z:\", @"c:\src\A\1.proj");
            AssertHasProjectBaseDir(@"c:\src\A", @"z:\", @"c:\src\A\1.proj", @"c:\src\A\2.proj");
            AssertHasProjectBaseDir(@"c:\src", @"z:\", @"c:\src\A\1.proj", @"c:\src\B\2.proj");
            AssertHasProjectBaseDir(@"c:\common\src", @"z:\", @"c:\common\src\A\1.proj", @"c:\common\src\B\2.proj");
            AssertHasProjectBaseDir(@"c:", @"z:\", @"c:\common\src\A\1.proj", @"c:\common\src\B\2.proj", @"c:\outside\src\C\3.proj");
            AssertHasProjectBaseDir(@"z:", @"z:", @"c:\src\A\1.proj", @"d:\src\B\2.proj");
            AssertHasProjectBaseDir(@"c:\src", @"z:\", @"c:\src\..\src\1.proj", @"c:\src\2.proj");
            AssertHasProjectBaseDir(@"z:\", @"z:\");
        }

        private void AssertHasProjectBaseDir(string expectedProjectDir, string fallback, params string[] projectPaths)
        {
            var config = new AnalysisConfig();
            config.SonarOutputDir = fallback;
            var writer = new PropertiesWriter(config);

            using (new AssertIgnoreScope())
            {
                foreach (string projectPath in projectPaths)
                {
                    var projectInfo = new ProjectInfo { FullPath = projectPath, ProjectLanguage = ProjectLanguages.VisualBasic };
                    writer.WriteSettingsForProject(projectInfo, Enumerable.Empty<string>(), "", "");
                }
                var actual = writer.Flush();
                var expected = "\r\nsonar.projectBaseDir=" + PropertiesWriter.Escape(expectedProjectDir);

                Console.WriteLine(actual);

                Assert.IsTrue(actual.Contains(expected));
            }
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

            // These are the settings we are going to check. The other config values are not checked.
            product.AnalysisSettings = new List<AnalysisSetting>();
            product.AnalysisSettings.Add(new AnalysisSetting() { Id = "my.setting1", Value = "setting1" });
            product.AnalysisSettings.Add(new AnalysisSetting() { Id = "my.setting2", Value = "setting 2 with spaces" });
            product.AnalysisSettings.Add(new AnalysisSetting() { Id = "my.setting.3", Value = @"c:\dir1\dir2\foo.txt" }); // path that will be escaped
            
            // Act
            PropertiesWriter writer = new PropertiesWriter(config);
            writer.WriteSettingsForProject(product, new string[] { productFile }, null, null);          
            string fullActualPath = SaveToResultFile(projectBaseDir, "Actual.txt", writer.Flush());

            // Assert
            SQPropertiesFileReader propertyReader = new SQPropertiesFileReader(fullActualPath);

            AssertSettingExists(propertyReader, "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting1", "setting1");
            AssertSettingExists(propertyReader, "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting2", "setting 2 with spaces");
            AssertSettingExists(propertyReader, "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.my.setting.3", @"c:\\dir1\\dir2\\foo.txt");
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

            // Act
            PropertiesWriter writer = new PropertiesWriter(config);
            writer.WriteGlobalSettings(globalSettings);
            string fullActualPath = SaveToResultFile(projectBaseDir, "Actual.txt", writer.Flush());

            // Assert
            SQPropertiesFileReader propertyReader = new SQPropertiesFileReader(fullActualPath);

            AssertSettingExists(propertyReader, "my.setting1", "setting1");
            AssertSettingExists(propertyReader, "my.setting2", "setting 2 with spaces");
            AssertSettingExists(propertyReader, "my.setting.3", @"c:\\dir1\\dir2\\foo.txt");
        }

        #endregion

        #region Checks

        private static void AssertSettingExists(SQPropertiesFileReader propertyReader, string expectedId, string expectedValue)
        {
            string actualValue = propertyReader.GetProperty(expectedId); // will throw if the property is missing
            Assert.AreEqual(expectedValue, actualValue, "Property does not have the expected value. Property: {0}", expectedId);
        }

        private static void AssertSettingDoesNotExists(SQPropertiesFileReader propertyReader, string expectedId)
        {
            try
            {
                var actualValue = propertyReader.GetProperty(expectedId); // will throw if the property is missing
                Assert.Fail("The property {0} was expected not to be set, but actually has the value: {1}", expectedId, actualValue);
            }
            catch (ArgumentOutOfRangeException)
            {
                // ignore, expected
            }
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
