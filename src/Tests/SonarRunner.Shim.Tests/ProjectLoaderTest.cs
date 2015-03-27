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

namespace SonarRunner.Shim.Tests
{
    [TestClass]
    public class ProjectLoaderTest
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [DeploymentItem("ProjectLoaderTest", "ProjectLoaderTest")]
        public void ProjectLoader()
        {
            // Arrange
            string testSourcePath = Path.Combine(this.TestContext.DeploymentDirectory, "ProjectLoaderTest");
            Assert.IsTrue(Directory.Exists(testSourcePath), "Test error: failed to locate the ProjectLoaderTest folder: {0}", testSourcePath);

            // The test folders that can be set up statically will have been copied to testSourcePath.
            // Now add the test folders that need to be set up dynamically (i.e. those that need a valid full path
            // embedded in the ProjectInfo.xml).
            ProjectDescriptor validTestProject = new ProjectDescriptor()
            {
                ParentDirectoryPath = testSourcePath,
                ProjectFolderName = "validTestProjectDir",
                ProjectFileName = "validTestProject.csproj",
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = true,
                ManagedSourceFiles = new string[] { "TestFile1.cs", "TestFile2.cs" },
                ContentFiles = new string[] { "contentFile1.js" },
            };
            CreateFilesFromDescriptor(validTestProject, "testCompileListFile", "testContentList", "testFxCopReport", "testVisualStudioCodeCoverageReport");

            ProjectDescriptor validNonTestProject = new ProjectDescriptor()
            {
                ParentDirectoryPath = testSourcePath,
                ProjectFolderName = "validNonTestProjectDir",
                ProjectFileName = "validNonTestproject.proj",
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = false,
                ManagedSourceFiles = new string[] { "ASourceFile.vb", "AnotherSourceFile.vb" },
            };
            CreateFilesFromDescriptor(validNonTestProject, "list.txt", null, "fxcop.xml", "visualstudio-codecoverage.xml");

            ProjectDescriptor validNonTestNoReportsProject = new ProjectDescriptor()
            {
                ParentDirectoryPath = testSourcePath,
                ProjectFolderName = "validNonTestNoReportsProjectDir",
                ProjectFileName = "validNonTestNoReportsProject.proj",
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = false,
                ManagedSourceFiles = new string[] { "SomeFile.cs" }
            };
            CreateFilesFromDescriptor(validNonTestNoReportsProject, "SomeList.txt", null, null, null);

            // Act
            List<Project> projects = SonarRunner.Shim.ProjectLoader.LoadFrom(testSourcePath);

            // Assert
            Assert.AreEqual(3, projects.Count);

            Project actualTestProject = AssertProjectResultExists(validTestProject.ProjectName, projects);
            AssertProjectMatchesDescriptor(validTestProject, actualTestProject);

            Project actualNonTestProject = AssertProjectResultExists(validNonTestProject.ProjectName, projects);
            AssertProjectMatchesDescriptor(validNonTestProject, actualNonTestProject);

            Project actualNonTestNoReportsProject = AssertProjectResultExists(validNonTestNoReportsProject.ProjectName, projects);
            AssertProjectMatchesDescriptor(validNonTestNoReportsProject, actualNonTestNoReportsProject);
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
                IsTestProject = false,
                ManagedSourceFiles = new string[] { "ASourceFile.vb", "AnotherSourceFile.vb" }
            };
            CreateFilesFromDescriptor(validNonTestProject, "CompileList.txt", null, null, null);

            // 1. Run against the root dir -> not expecting the project to be found
            List<Project> projects = SonarRunner.Shim.ProjectLoader.LoadFrom(rootTestDir);
            Assert.AreEqual(0, projects.Count);

            // 2. Run against the child dir -> project should be found
            projects = SonarRunner.Shim.ProjectLoader.LoadFrom(childDir);
            Assert.AreEqual(1, projects.Count);
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Creates a folder containing a ProjectInfo.xml and compiled file list as
        /// specified in the supplied descriptor
        /// </summary>
        private static void CreateFilesFromDescriptor(ProjectDescriptor descriptor, string compileFiles, string contentFiles, string fxcopReportFileName, string visualStudioCodeCoverageReportFileName)
        {
            if (!Directory.Exists(descriptor.FullDirectoryPath))
            {
                Directory.CreateDirectory(descriptor.FullDirectoryPath);
            }

            ProjectInfo projectInfo = descriptor.CreateProjectInfo();

            // Create the compile list if any input files have been specified
            if (descriptor.ManagedSourceFiles != null)
            {
                string fullCompileListName = Path.Combine(descriptor.FullDirectoryPath, compileFiles);
                File.WriteAllLines(fullCompileListName, descriptor.ManagedSourceFiles);

                // Add the compile list as an analysis result
                projectInfo.AnalysisResults.Add(new AnalysisResult() { Id = AnalysisType.ManagedCompilerInputs.ToString(), Location = fullCompileListName });
            }

            // Create the content list if any content files have been specified
            if (descriptor.ContentFiles != null)
            {
                string contentFilesName = Path.Combine(descriptor.FullDirectoryPath, contentFiles);
                File.WriteAllLines(contentFilesName, descriptor.ContentFiles);

                // Add the FxCop report as an analysis result
                var analysisResult = new AnalysisResult() { Id = AnalysisType.ContentFiles.ToString(), Location = contentFilesName };
                descriptor.AnalysisResults.Add(analysisResult);
                projectInfo.AnalysisResults.Add(analysisResult);
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

        #endregion

        #region Assertions

        private static Project AssertProjectResultExists(string expectedProjectName, List<Project> actualProjects)
        {
            Project actual = actualProjects.FirstOrDefault(p => expectedProjectName.Equals(p.Name));
            Assert.IsNotNull(actual, "Failed to find project with the expected name: {0}", expectedProjectName);
            return actual;
        }

        private static void AssertProjectMatchesDescriptor(ProjectDescriptor expected, Project actual)
        {
            Assert.AreEqual(expected.ProjectName, actual.Name);
            Assert.AreEqual(expected.ProjectGuid, actual.Guid);
            Assert.AreEqual(expected.FullFilePath, actual.MsBuildProject);
            Assert.AreEqual(expected.IsTestProject, actual.IsTest);

            List<string> expectedFiles = new List<string>();
            if (expected.ManagedSourceFiles != null)
            {
                expectedFiles.AddRange(expected.ManagedSourceFiles);
            }
            if (expected.ContentFiles != null)
            {
                expectedFiles.AddRange(expected.ContentFiles);
            }
            Assert.AreEqual(expectedFiles.Count, actual.Files.Count);
            CollectionAssert.AreEqual(expectedFiles, actual.Files);

            AnalysisResult fxCopResult = expected.AnalysisResults.FirstOrDefault(e => AnalysisType.FxCop.ToString().Equals(e.Id));
            string fxCopReport = null;
            if (fxCopResult != null)
            {
                fxCopReport = fxCopResult.Location;
            }
            Assert.AreEqual(fxCopReport, actual.FxCopReport);
        }

        #endregion
    }
}
