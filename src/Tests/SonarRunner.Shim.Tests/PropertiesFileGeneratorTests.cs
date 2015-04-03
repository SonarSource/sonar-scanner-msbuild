//-----------------------------------------------------------------------
// <copyright file="PropertiesFileGeneratorTests.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
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
    public class PropertiesFileGeneratorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void FileGen_NoProjectInfoFiles()
        {
            // Properties file should not be generated if there are no project info files.

            // Arrange - two sub-directories, neither containing a ProjectInfo.xml
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string subDir1 = TestUtils.EnsureTestSpecificFolder(this.TestContext, "dir1");
            string subDir2 = TestUtils.EnsureTestSpecificFolder(this.TestContext, "dir2");

            string file1 = CreateEmptyFile(subDir1, "file1.txt");
            string file2 = CreateEmptyFile(subDir2, "file2.txt");

            TestLogger logger = new TestLogger();
            AnalysisConfig config = new AnalysisConfig() { SonarOutputDir = testDir };

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);
        
            // Assert
            AssertFailedToCreatePropertiesFiles(result, logger);
            AssertExpectedProjectCount(0, result);
        }

        [TestMethod]
        public void FileGen_DuplicateProjectIds()
        {
            // ProjectInfo files with duplicate ids should be ignored

            // Arrange - three files, all with the same Guid, one of which is excluded
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid duplicateGuid = Guid.NewGuid();
            CreateProjectInfoInSubDir(testDir, "duplicate1", duplicateGuid, ProjectType.Product, false, null); // not excluded
            CreateProjectInfoInSubDir(testDir, "duplicate2", duplicateGuid, ProjectType.Test, false, null); // not excluded
            CreateProjectInfoInSubDir(testDir, "excluded", duplicateGuid, ProjectType.Product, true, null); // excluded

            TestLogger logger = new TestLogger();
            AnalysisConfig config = new AnalysisConfig() { SonarOutputDir = testDir };

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            AssertExpectedStatus("duplicate1", ProjectInfoValidity.DuplicateGuid, result);
            AssertExpectedStatus("duplicate2", ProjectInfoValidity.DuplicateGuid, result);
            AssertExpectedStatus("excluded", ProjectInfoValidity.ExcludeFlagSet, result); // Expecting excluded rather than duplicate
            AssertExpectedProjectCount(3, result);

            // No valid project info files -> file not generated
            AssertFailedToCreatePropertiesFiles(result, logger);
        }

        [TestMethod]
        public void FileGen_ExcludedProjectsAreNotDuplicates()
        {
            // Excluded ProjectInfo files should be ignored when calculating duplicates

            // Arrange - two sub-directories, neither containing a ProjectInfo.xml
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid duplicateGuid = Guid.NewGuid();
            CreateProjectInfoInSubDir(testDir, "excl1", duplicateGuid, ProjectType.Product, true, null); // excluded
            CreateProjectInfoInSubDir(testDir, "excl2", duplicateGuid, ProjectType.Test, true, null); // excluded
            CreateProjectInfoInSubDir(testDir, "notExcl", duplicateGuid, ProjectType.Product, false, null); // not excluded

            TestLogger logger = new TestLogger();
            AnalysisConfig config = new AnalysisConfig() { SonarOutputDir = testDir };

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            AssertExpectedStatus("excl1", ProjectInfoValidity.ExcludeFlagSet, result);
            AssertExpectedStatus("excl2", ProjectInfoValidity.ExcludeFlagSet, result);
            AssertExpectedStatus("notExcl", ProjectInfoValidity.NoFilesToAnalyze, result); // not "duplicate" since the duplicate guids are excluded
            AssertExpectedProjectCount(3, result);

            // One valid project info file -> file
            AssertFailedToCreatePropertiesFiles(result, logger);
        }

        [TestMethod]
        public void FileGen_ValidFiles()
        {
            // Only non-excluded projects with files to analyse should be marked as valid

            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
         
            string projectWithoutFiles = CreateProjectInfoInSubDir(testDir, "withoutFiles", Guid.NewGuid(), ProjectType.Product, false, null); // not excluded

            string projectWithContentDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "projectWithContent");
            string contentProjectPath = Path.Combine(projectWithContentDir, "contentProject.proj");
            string contentProjectInfo = CreateProjectInfoInSubDir(testDir, "withContentFiles", Guid.NewGuid(), ProjectType.Product, false, contentProjectPath); // not excluded

            string managedProjectDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "managedProject");
            string managedProjectPath = Path.Combine(managedProjectDir, "managedProject.proj");
            string managedProjectInfo = CreateProjectInfoInSubDir(testDir, "withManagedFiles", Guid.NewGuid(), ProjectType.Product, false, managedProjectPath); // not excluded

            // Create the content files under the relevant project directories
            string contentFileList = CreateFile(projectWithContentDir, "contentList.txt", Path.Combine(projectWithContentDir, "contentFile1.txt"));
            AddAnalysisResult(contentProjectInfo, AnalysisType.ContentFiles, contentFileList);

            string managedFileList = CreateFile(managedProjectDir, "managedList.txt", Path.Combine(managedProjectDir, "managedFile1.cs"));
            AddAnalysisResult(managedProjectInfo, AnalysisType.ManagedCompilerInputs, managedFileList);

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            AssertExpectedStatus("withoutFiles", ProjectInfoValidity.NoFilesToAnalyze, result);
            AssertExpectedStatus("withContentFiles", ProjectInfoValidity.Valid, result);
            AssertExpectedStatus("withManagedFiles", ProjectInfoValidity.Valid, result);
            AssertExpectedProjectCount(3, result);

            // One valid project info file -> file created
            AssertPropertiesFilesCreated(result, logger);
        }

        [TestMethod]
        public void FileGen_FilesOutsideProjectPath()
        {
            // Files outside the project root should be ignored

            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string projectDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "project");
            string projectPath = Path.Combine(projectDir, "project.proj");
            string projectInfo = CreateProjectInfoInSubDir(testDir, "projectName", Guid.NewGuid(), ProjectType.Product, false, projectPath); // not excluded

            // Create a content file, but not under the project directory
            string contentFileList = CreateFile(projectDir, "contentList.txt", Path.Combine(testDir, "contentFile1.txt"));
            AddAnalysisResult(projectInfo, AnalysisType.ContentFiles, contentFileList);

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            AssertExpectedStatus("projectName", ProjectInfoValidity.NoFilesToAnalyze, result);
            AssertExpectedProjectCount(1, result);

            // No files -> project file not created
            AssertFailedToCreatePropertiesFiles(result, logger);
        }

        #endregion

        #region Assertions

        private static void AssertFailedToCreatePropertiesFiles(ProjectInfoAnalysisResult result, TestLogger logger)
        {
            Assert.IsNull(result.FullPropertiesFilePath, "Not expecting the sonar-runner properties file to have been set");
            Assert.AreEqual(false, result.RanToCompletion, "Expecting the property file generation to have failed");

            AssertNoValidProjects(result);

            logger.AssertErrorsLogged();
        }

        private static void AssertPropertiesFilesCreated(ProjectInfoAnalysisResult result, TestLogger logger)
        {
            Assert.IsNotNull(result.FullPropertiesFilePath, "Not expecting the sonar-runner properties file to have been set");

            AssertValidProjectsExist(result);

            logger.AssertErrorsLogged(0);
        }

        private static void AssertExpectedStatus(string expectedProjectName, ProjectInfoValidity expectedStatus, ProjectInfoAnalysisResult actual)
        {
            IEnumerable<ProjectInfo> matches = actual.GetProjectsByStatus(expectedStatus).Where(p => p.ProjectName.Equals(expectedProjectName));
            Assert.IsFalse(matches.Count() > 2, "ProjectName is reported more than once: {0}", expectedProjectName);
            Assert.AreEqual(1, matches.Count(), "ProjectInfo was not classified as expected. Project name: {0}, expected status: {1}", expectedProjectName, expectedStatus);
        }

        private static void AssertNoValidProjects(ProjectInfoAnalysisResult actual)
        {
            IEnumerable<ProjectInfo> matches = actual.GetProjectsByStatus(ProjectInfoValidity.Valid);
            Assert.AreEqual(0, matches.Count(), "Not expecting to find any valid ProjectInfo files");
        }

        private static void AssertValidProjectsExist(ProjectInfoAnalysisResult actual)
        {
            IEnumerable<ProjectInfo> matches = actual.GetProjectsByStatus(ProjectInfoValidity.Valid);
            Assert.AreNotEqual(0, matches.Count(), "Expecting at least one valid ProjectInfo file to exist");
        }

        private static void AssertExpectedProjectCount(int expected, ProjectInfoAnalysisResult actual)
        {
            Assert.AreEqual(expected, actual.Projects.Count, "Unexpected number of projects in the result");
        }
        #endregion

        #region Private methods

        private static AnalysisConfig CreateValidConfig(string outputDir)
        {
            string dummyProjectKey = Guid.NewGuid().ToString();

            AnalysisConfig config = new AnalysisConfig()
            {
                SonarOutputDir = outputDir,

                SonarProjectKey = dummyProjectKey,
                SonarProjectName = dummyProjectKey,
                SonarConfigDir = Path.Combine(outputDir, "config"),
                SonarProjectVersion = "1.0"
            };

            return config;
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

        /// <summary>
        /// Creates a new project info file in a new subdirectory.
        /// </summary>
        private static string CreateProjectInfoInSubDir(string parentDir,
            string projectName, Guid projectGuid, ProjectType projectType, bool isExcluded, string fullProjectPath)
        {
            string newDir = Path.Combine(parentDir, Guid.NewGuid().ToString());
            Directory.CreateDirectory(newDir); // ensure the directory exists

            ProjectInfo project = new ProjectInfo()
            {
                FullPath = fullProjectPath,
                ProjectName = projectName,
                ProjectGuid = projectGuid,
                ProjectType = projectType,
                IsExcluded = isExcluded,
            };

            string filePath = Path.Combine(newDir, FileConstants.ProjectInfoFileName);
            project.Save(filePath);
            return filePath;
        }

        private static void AddAnalysisResult(string projectInfoFile, AnalysisType resultType, string location)
        {
            ProjectInfo projectInfo = ProjectInfo.Load(projectInfoFile);
            projectInfo.AddAnalyzerResult(resultType, location);
            projectInfo.Save(projectInfoFile);
        }

        #endregion
    }
}