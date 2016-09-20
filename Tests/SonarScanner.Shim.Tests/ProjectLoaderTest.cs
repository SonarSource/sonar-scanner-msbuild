//-----------------------------------------------------------------------
// <copyright file="ProjectLoaderTest.cs" company="SonarSource SA and Microsoft Corporation">
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

namespace SonarScanner.Shim.Tests
{
    [TestClass]
    public class ProjectLoaderTest
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void ProjectLoader()
        {
            // Arrange
            string testSourcePath = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // Create sub-directories, some with project info XML files and some without
            TestUtils.EnsureTestSpecificFolder(this.TestContext, "EmptyDir1");

            ProjectDescriptor validTestProject = new ProjectDescriptor()
            {
                ParentDirectoryPath = testSourcePath,
                ProjectFolderName = "validTestProjectDir",
                ProjectFileName = "validTestProject.csproj",
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = true
            };
            validTestProject.AddCompileInputFile("TestFile1.cs", true);
            validTestProject.AddCompileInputFile("TestFile1.cs", true);
            validTestProject.AddContentFile("contentFile1.js", true);
            CreateFilesFromDescriptor(validTestProject, "testCompileListFile", "testFxCopReport", "testVisualStudioCodeCoverageReport");

            TestUtils.EnsureTestSpecificFolder(this.TestContext, "EmptyDir2");

            ProjectDescriptor validNonTestProject = new ProjectDescriptor()
            {
                ParentDirectoryPath = testSourcePath,
                ProjectFolderName = "validNonTestProjectDir",
                ProjectFileName = "validNonTestproject.proj",
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = false
            };
            validNonTestProject.AddContentFile("ASourceFile.vb", true);
            validNonTestProject.AddContentFile("AnotherSourceFile.vb", true);
            CreateFilesFromDescriptor(validNonTestProject, "list.txt", "fxcop.xml", "visualstudio-codecoverage.xml");

            ProjectDescriptor validNonTestNoReportsProject = new ProjectDescriptor()
            {
                ParentDirectoryPath = testSourcePath,
                ProjectFolderName = "validNonTestNoReportsProjectDir",
                ProjectFileName = "validNonTestNoReportsProject.proj",
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = false
            };
            validNonTestNoReportsProject.AddContentFile("SomeFile.cs", true);
            CreateFilesFromDescriptor(validNonTestNoReportsProject, "SomeList.txt", null, null);

            // Act
            IEnumerable<ProjectInfo> projects = SonarScanner.Shim.ProjectLoader.LoadFrom(testSourcePath);

            // Assert
            Assert.AreEqual(3, projects.Count());

            AssertProjectResultExists(validTestProject.ProjectName, projects);

            AssertProjectResultExists(validNonTestProject.ProjectName, projects);

            AssertProjectResultExists(validNonTestNoReportsProject.ProjectName, projects);
        }

        [TestMethod]
        [Description("Checks that the loader only looks in the top-level folder for project folders")]
        public void ProjectLoader_NonRecursive()
        {
            // 0. Setup
            string rootTestDir = Path.Combine(this.TestContext.DeploymentDirectory, "ProjectLoader_NonRecursive");
            string childDir = Path.Combine(rootTestDir, "Child1");

            // Create a valid project in the child directory
            ProjectDescriptor validNonTestProject = new ProjectDescriptor()
            {
                ParentDirectoryPath = childDir,
                ProjectFolderName = "validNonTestProjectDir",
                ProjectFileName = "validNonTestproject.proj",
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = false
            };
            validNonTestProject.AddCompileInputFile("ASourceFile.vb", true);
            validNonTestProject.AddCompileInputFile("AnotherSourceFile.vb", true);
            CreateFilesFromDescriptor(validNonTestProject, "CompileList.txt", null, null);

            // 1. Run against the root dir -> not expecting the project to be found
            IEnumerable<ProjectInfo> projects = SonarScanner.Shim.ProjectLoader.LoadFrom(rootTestDir);
            Assert.AreEqual(0, projects.Count());

            // 2. Run against the child dir -> project should be found
            projects = SonarScanner.Shim.ProjectLoader.LoadFrom(childDir);
            Assert.AreEqual(1, projects.Count());
        }

        #endregion Tests

        #region Helper methods

        /// <summary>
        /// Creates a folder containing a ProjectInfo.xml and compiled file list as
        /// specified in the supplied descriptor
        /// </summary>
        private static void CreateFilesFromDescriptor(ProjectDescriptor descriptor, string compileFiles, string fxcopReportFileName, string visualStudioCodeCoverageReportFileName)
        {
            if (!Directory.Exists(descriptor.FullDirectoryPath))
            {
                Directory.CreateDirectory(descriptor.FullDirectoryPath);
            }

            ProjectInfo projectInfo = descriptor.CreateProjectInfo();

            // Create the analysis file list if any input files have been specified
            if (descriptor.FilesToAnalyse.Any())
            {
                string fullAnalysisFileListPath = Path.Combine(descriptor.FullDirectoryPath, compileFiles);
                File.WriteAllLines(fullAnalysisFileListPath, descriptor.FilesToAnalyse);

                // Add the compile list as an analysis result
                projectInfo.AnalysisResults.Add(new AnalysisResult() { Id = AnalysisType.FilesToAnalyze.ToString(), Location = fullAnalysisFileListPath });
            }

            // Create the FxCop report file
            if (fxcopReportFileName != null)
            {
                string fullFxCopName = Path.Combine(descriptor.FullDirectoryPath, fxcopReportFileName);
                File.Create(fullFxCopName);

                // Add the FxCop report as an analysis result
                var analysisResult = new AnalysisResult() { Id = AnalysisType.FxCop.ToString(), Location = fullFxCopName };
                descriptor.AnalysisResults.Add(analysisResult);
                projectInfo.AnalysisResults.Add(analysisResult);
            }

            // Create the Visual Studio Code Coverage report file
            if (visualStudioCodeCoverageReportFileName != null)
            {
                string fullVisualStudioCodeCoverageName = Path.Combine(descriptor.FullDirectoryPath, visualStudioCodeCoverageReportFileName);
                File.Create(fullVisualStudioCodeCoverageName);

                // Add the Visual Studio Code Coverage report as an analysis result
                var analysisResult = new AnalysisResult() { Id = AnalysisType.VisualStudioCodeCoverage.ToString(), Location = fullVisualStudioCodeCoverageName };
                descriptor.AnalysisResults.Add(analysisResult);
                projectInfo.AnalysisResults.Add(analysisResult);
            }

            // Save a project info file in the target directory
            projectInfo.Save(Path.Combine(descriptor.FullDirectoryPath, FileConstants.ProjectInfoFileName));
        }

        #endregion Helper methods

        #region Assertions

        private static ProjectInfo AssertProjectResultExists(string expectedProjectName, IEnumerable<ProjectInfo> actualProjects)
        {
            ProjectInfo actual = actualProjects.FirstOrDefault(p => expectedProjectName.Equals(p.ProjectName));
            Assert.IsNotNull(actual, "Failed to find project with the expected name: {0}", expectedProjectName);
            return actual;
        }

        #endregion Assertions
    }
}