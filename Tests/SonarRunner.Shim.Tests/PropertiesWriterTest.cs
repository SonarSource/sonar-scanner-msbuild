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
            ProjectInfo product = CreateProjectInfo("你好", "DB2E5521-3172-47B9-BA50-864F12E6DFFF", productProject, false, productFiles, productFileListFilePath, productFxCopFilePath, productCoverageFilePath);

            string testBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_TestBaseDir");
            string testProject = CreateEmptyFile(testBaseDir, "MyTest.csproj");
            string testFile = CreateEmptyFile(testBaseDir, "File.cs");
            string testFileListFilePath = Path.Combine(testBaseDir, "testManagedFiles.txt");

            List<string> testFiles = new List<string>();
            testFiles.Add(testFile);
            ProjectInfo test = CreateProjectInfo("my_test_project", "DA0FCD82-9C5C-4666-9370-C7388281D49B", testProject, true, testFiles, testFileListFilePath, null, null);

            List<ProjectInfo> projects = new List<ProjectInfo>();
            projects.Add(product);
            projects.Add(test);

            var logger = new TestLogger();
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
                writer.WriteSettingsForProject(product, new string[] { productFile, productChineseFile, missingFileOutsideProjectDir }, productFxCopFilePath, productCoverageFilePath);
                writer.WriteSettingsForProject(test, new string[] { testFile }, null, null);

                actual = writer.Flush();
            }

            string expected = string.Format(System.Globalization.CultureInfo.InvariantCulture,
@"sonar.projectKey=my_project_key
sonar.projectName=my_project_name
sonar.projectVersion=1.0
sonar.projectBaseDir=C:\\my_folder

# FIXME: Encoding is hardcoded
sonar.sourceEncoding=UTF-8

DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectKey=my_project_key:DB2E5521-3172-47B9-BA50-864F12E6DFFF
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectName=\u4F60\u597D
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectBaseDir={0}
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.cs.fxcop.reportPath={1}
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.cs.vscoveragexml.reportsPaths={2}
DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.sources=\
{0}\\File.cs,\
{0}\\\u4F60\u597D.cs,\
{4}

DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectKey=my_project_key:DA0FCD82-9C5C-4666-9370-C7388281D49B
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectName=my_test_project
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectBaseDir={3}
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.sources=
DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.tests=\
{3}\\File.cs

sonar.modules=DB2E5521-3172-47B9-BA50-864F12E6DFFF,DA0FCD82-9C5C-4666-9370-C7388281D49B

",
 PropertiesWriter.Escape(productBaseDir),
 PropertiesWriter.Escape(productFxCopFilePath),
 PropertiesWriter.Escape(productCoverageFilePath),
 PropertiesWriter.Escape(testBaseDir),
 PropertiesWriter.Escape(missingFileOutsideProjectDir));

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

        #endregion

        #region Private methods

        private static ProjectInfo CreateProjectInfo(string name, string projectId, string fullFilePath, bool isTest, IEnumerable<string> files, string fileListFilePath, string fxCopReportPath, string coverageReportPath)
        {
            ProjectInfo projectInfo = new ProjectInfo()
            {
                ProjectName = name,
                ProjectGuid = Guid.Parse(projectId),
                FullPath = fullFilePath,
                ProjectType = isTest ? ProjectType.Test : ProjectType.Product,
                AnalysisResults = new List<AnalysisResult>()
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

                projectInfo.AddAnalyzerResult(AnalysisType.ManagedCompilerInputs, fileListFilePath);
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

        private void SaveToResultFile(string testDir, string fileName, string content)
        {
            string fullPath = CreateFile(testDir, fileName, content);
            this.TestContext.AddResultFile(fullPath);
        }
        
        #endregion
    }
}
