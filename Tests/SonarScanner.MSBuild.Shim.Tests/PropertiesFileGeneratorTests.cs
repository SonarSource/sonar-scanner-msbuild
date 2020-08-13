/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Shim.Tests
{
    [TestClass]
    public class PropertiesFileGeneratorTests
    {
        private const string TestSonarqubeOutputDir = @"e:\.sonarqube\out";
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void FileGen_NoProjectInfoFiles()
        {
            // Properties file should not be generated if there are no project info files.

            // Arrange - two sub-directories, neither containing a ProjectInfo.xml
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var subDir1 = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "dir1");
            var subDir2 = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "dir2");

            CreateEmptyFile(subDir1, "file1.txt");
            CreateEmptyFile(subDir2, "file2.txt");

            var logger = new TestLogger();
            var config = new AnalysisConfig() { SonarOutputDir = testDir };

            // Act
            var result = new PropertiesFileGenerator(config, logger).GenerateFile();

            // Assert
            AssertFailedToCreatePropertiesFiles(result, logger);
            AssertExpectedProjectCount(0, result);
        }

        [TestMethod]
        public void FileGen_ValidFiles()
        {
            // Only non-excluded projects with files to analyze should be marked as valid

            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            CreateProjectInfoInSubDir(testDir, "withoutFiles", Guid.NewGuid(), ProjectType.Product, false, "c:\\abc\\withoutfile.proj", "UTF-8"); // not excluded
            CreateProjectWithFiles("withFiles1", testDir);
            CreateProjectWithFiles("withFiles2", testDir);

            var logger = new TestLogger();
            var config = CreateValidConfig(testDir);

            // Act
            var result = new PropertiesFileGenerator(config, logger).GenerateFile();

            // Assert
            AssertExpectedStatus("withoutFiles", ProjectInfoValidity.NoFilesToAnalyze, result);
            AssertExpectedStatus("withFiles1", ProjectInfoValidity.Valid, result);
            AssertExpectedStatus("withFiles2", ProjectInfoValidity.Valid, result);
            AssertExpectedProjectCount(3, result);

            // One valid project info file -> file created
            AssertPropertiesFilesCreated(result, logger);
        }

        [TestMethod]
        public void FileGen_Duplicate_SameGuid_DifferentCase_ShouldNotIgnoreCase()
        {
            // Casing can be ignored on windows OS

            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var mockRuntimeInformation = new Mock<IRuntimeInformationWrapper>();
            mockRuntimeInformation.Setup(m => m.IsOS(System.Runtime.InteropServices.OSPlatform.Windows)).Returns(false);

            var guid = Guid.NewGuid();

            CreateProjectInfoInSubDir(testDir, "withoutFiles", guid, ProjectType.Product, false, "c:\\abc\\withoutfile.proj", "UTF-8");
            CreateProjectInfoInSubDir(testDir, "withoutFiles1", guid, ProjectType.Product, false, "C:\\abc\\withoutFile.proj", "UTF-8"); // not excluded

            var logger = new TestLogger();
            var config = CreateValidConfig(testDir);

            // Act
            var result = new PropertiesFileGenerator(config, logger, new RoslynV1SarifFixer(logger), mockRuntimeInformation.Object).GenerateFile();

            // Assert
            AssertExpectedStatus("withoutFiles", ProjectInfoValidity.DuplicateGuid, result);
            AssertExpectedProjectCount(1, result);

            logger.Warnings.Should().HaveCount(2);

            logger.Warnings.Should().BeEquivalentTo(
               new[]
               {
                    $"Duplicate ProjectGuid: \"{guid}\". The project will not be analyzed by SonarQube. Project file: \"c:\\abc\\withoutfile.proj\"",
                    $"Duplicate ProjectGuid: \"{guid}\". The project will not be analyzed by SonarQube. Project file: \"C:\\abc\\withoutFile.proj\"",
               });
        }

        [TestMethod]
        public void FileGen_Duplicate_SameGuid_DifferentCase_ShouldIgnoreCase()
        {
            // Casing can be ignored on windows OS

            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var mockRuntimeInformation = new Mock<IRuntimeInformationWrapper>();
            mockRuntimeInformation.Setup(m => m.IsOS(System.Runtime.InteropServices.OSPlatform.Windows)).Returns(true);

            var guid = Guid.NewGuid();

            CreateProjectInfoInSubDir(testDir, "withoutFiles", guid, ProjectType.Product, false, "c:\\abc\\withoutfile.proj", "UTF-8");
            CreateProjectInfoInSubDir(testDir, "withoutFiles1", guid, ProjectType.Product, false, "C:\\abc\\withoutFile.proj", "UTF-8"); // not excluded

            var logger = new TestLogger();
            var config = CreateValidConfig(testDir);

            // Act
            var result = new PropertiesFileGenerator(config, logger, new RoslynV1SarifFixer(logger), mockRuntimeInformation.Object).GenerateFile();

            // Assert
            AssertExpectedStatus("withoutFiles", ProjectInfoValidity.NoFilesToAnalyze, result);
            AssertExpectedProjectCount(1, result);

        }

        [TestMethod]
        public void FileGen_ValidFiles_SourceEncoding_Provided()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            CreateProjectWithFiles("withFiles1", testDir);

            var logger = new TestLogger();
            var config = CreateValidConfig(testDir);

            config.LocalSettings = new AnalysisProperties
            {
                new Property { Id = SonarProperties.SourceEncoding, Value = "test-encoding-here" }
            };

            // Act
            var result = new PropertiesFileGenerator(config, logger).GenerateFile();

            // Assert
            var settingsFileContent = File.ReadAllText(result.FullPropertiesFilePath);
            settingsFileContent.Should().Contain("sonar.sourceEncoding=test-encoding-here", "Command line parameter 'sonar.sourceEncoding' is ignored.");
            logger.DebugMessages.Should().Contain(string.Format(Resources.DEBUG_DumpSonarProjectProperties, settingsFileContent));
        }

        [TestMethod]
        public void FileGen_TFS_Coverage_Trx_Are_Written()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            CreateProjectWithFiles("withFiles1", testDir);

            var logger = new TestLogger();
            var config = CreateValidConfig(testDir);

            config.LocalSettings = new AnalysisProperties
            {
                new Property { Id = SonarProperties.VsCoverageXmlReportsPaths, Value = "coverage-path" },
                new Property { Id = SonarProperties.VsTestReportsPaths, Value = "trx-path" },
            };

            // Act
            var result = new PropertiesFileGenerator(config, logger, new RoslynV1SarifFixer(logger), new RuntimeInformationWrapper()).GenerateFile();

            // Assert
            var settingsFileContent = File.ReadAllText(result.FullPropertiesFilePath);
            settingsFileContent.Should().Contain("sonar.cs.vscoveragexml.reportsPaths=coverage-path");
            settingsFileContent.Should().Contain("sonar.cs.vstest.reportsPaths=trx-path");
            logger.DebugMessages.Should().Contain(string.Format(Resources.DEBUG_DumpSonarProjectProperties, settingsFileContent));
        }

        [TestMethod]
        public void FileGen_ValidFiles_WithAlreadyValidSarif()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            // SARIF file path
            var testSarifPath = Path.Combine(testDir, "testSarif.json");

            // Create SARIF report path property and add it to the project info
            var projectSettings = new AnalysisProperties
            {
                new Property() { Id = PropertiesFileGenerator.ReportFileCsharpPropertyKey, Value = testSarifPath }
            };
            var projectGuid = Guid.NewGuid();
            CreateProjectWithFiles("withFiles1", testDir, projectGuid, true, projectSettings);

            var logger = new TestLogger();
            var config = CreateValidConfig(testDir);

            // Mock SARIF fixer simulates already valid sarif
            var mockSarifFixer = new MockRoslynV1SarifFixer(testSarifPath);
            var mockReturnPath = mockSarifFixer.ReturnVal;

            // Act
            var result = new PropertiesFileGenerator(config, logger, mockSarifFixer, new RuntimeInformationWrapper()).GenerateFile();

            // Assert
            mockSarifFixer.CallCount.Should().Be(1);

            // Already valid SARIF -> no change in file -> unchanged property
            var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists(projectGuid.ToString().ToUpper() + "." + PropertiesFileGenerator.ReportFileCsharpPropertyKey, mockReturnPath);
        }

        [TestMethod]
        public void FileGen_ValidFiles_WithFixableSarif()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            // SARIF file path
            var testSarifPath = Path.Combine(testDir, "testSarif.json");

            // Create SARIF report path property and add it to the project info
            var projectSettings = new AnalysisProperties
            {
                new Property() { Id = PropertiesFileGenerator.ReportFileCsharpPropertyKey, Value = testSarifPath }
            };
            var projectGuid = Guid.NewGuid();
            CreateProjectWithFiles("withFiles1", testDir, projectGuid, true, projectSettings);

            var logger = new TestLogger();
            var config = CreateValidConfig(testDir);

            // Mock SARIF fixer simulates fixable SARIF with fixed name
            var returnPathFileName = Path.GetFileNameWithoutExtension(testSarifPath) +
                RoslynV1SarifFixer.FixedFileSuffix + Path.GetExtension(testSarifPath);

            var mockSarifFixer = new MockRoslynV1SarifFixer(returnPathFileName);
            var escapedMockReturnPath = mockSarifFixer.ReturnVal.Replace(@"\", @"\\");

            // Act
            var result = new PropertiesFileGenerator(config, logger, mockSarifFixer, new RuntimeInformationWrapper()).GenerateFile();

            // Assert
            mockSarifFixer.CallCount.Should().Be(1);
            mockSarifFixer.LastLanguage.Should().Be(RoslynV1SarifFixer.CSharpLanguage);

            // Fixable SARIF -> new file saved -> changed property
            var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists(projectGuid.ToString().ToUpper() + "." + PropertiesFileGenerator.ReportFileCsharpPropertyKey, escapedMockReturnPath);
        }

        [TestMethod]
        public void FileGen_ValidFiles_WithFixableSarif_VBNet()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            // SARIF file path
            var testSarifPath = Path.Combine(testDir, "testSarif.json");

            // Create SARIF report path property and add it to the project info
            var projectSettings = new AnalysisProperties
            {
                new Property() { Id = PropertiesFileGenerator.ReportFileVbnetPropertyKey, Value = testSarifPath }
            };
            var projectGuid = Guid.NewGuid();
            CreateProjectWithFiles("withFiles1", testDir, projectGuid, true, projectSettings);

            var logger = new TestLogger();
            var config = CreateValidConfig(testDir);

            // Mock SARIF fixer simulates fixable SARIF with fixed name
            var returnPathFileName = Path.GetFileNameWithoutExtension(testSarifPath) +
                RoslynV1SarifFixer.FixedFileSuffix + Path.GetExtension(testSarifPath);

            var mockSarifFixer = new MockRoslynV1SarifFixer(returnPathFileName);
            var escapedMockReturnPath = mockSarifFixer.ReturnVal.Replace(@"\", @"\\");

            // Act
            var result = new PropertiesFileGenerator(config, logger, mockSarifFixer, new RuntimeInformationWrapper()).GenerateFile();

            // Assert
            mockSarifFixer.CallCount.Should().Be(1);
            mockSarifFixer.LastLanguage.Should().Be(RoslynV1SarifFixer.VBNetLanguage);

            // Fixable SARIF -> new file saved -> changed property
            var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists(projectGuid.ToString().ToUpper() + "." + PropertiesFileGenerator.ReportFileVbnetPropertyKey, escapedMockReturnPath);
        }

        [TestMethod]
        public void FileGen_ValidFiles_WithUnfixableSarif()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            // SARIF file path
            var testSarifPath = Path.Combine(testDir, "testSarif.json");

            // Create SARIF report path property and add it to the project info
            var projectSettings = new AnalysisProperties
            {
                new Property() { Id = PropertiesFileGenerator.ReportFileCsharpPropertyKey, Value = testSarifPath }
            };
            var projectGuid = Guid.NewGuid();
            CreateProjectWithFiles("withFiles1", testDir, projectGuid, true, projectSettings);

            var logger = new TestLogger();
            var config = CreateValidConfig(testDir);

            // Mock SARIF fixer simulated unfixable/absent file
            var mockSarifFixer = new MockRoslynV1SarifFixer(null);

            // Act
            var result = new PropertiesFileGenerator(config, logger, mockSarifFixer, new RuntimeInformationWrapper()).GenerateFile();

            // Assert
            mockSarifFixer.CallCount.Should().Be(1);

            // One valid project info file -> file created
            AssertPropertiesFilesCreated(result, logger);

            // Unfixable SARIF -> cannot fix -> report file property removed
            var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingDoesNotExist(projectGuid.ToString().ToUpper() + "." + PropertiesFileGenerator.ReportFileCsharpPropertyKey);
        }

        [TestMethod]
        public void FileGen_FilesOutsideProjectPath()
        {
            // Files outside the project root should be ignored

            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var projectDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
            var projectPath = Path.Combine(projectDir, "project.proj");
            var projectInfo = CreateProjectInfoInSubDir(testDir, "projectName", Guid.NewGuid(), ProjectType.Product, false, "UTF-8", projectPath); // not excluded

            // Create a content file, but not under the project directory
            var contentFileList = CreateFile(projectDir, "contentList.txt", Path.Combine(testDir, "contentFile1.txt"));
            AddAnalysisResult(projectInfo, AnalysisType.FilesToAnalyze, contentFileList);

            var logger = new TestLogger();
            var config = CreateValidConfig(testDir);

            // Act
            var result = new PropertiesFileGenerator(config, logger).GenerateFile();

            // Assert
            AssertExpectedStatus("projectName", ProjectInfoValidity.NoFilesToAnalyze, result);
            AssertExpectedProjectCount(1, result);

            // No files -> project file not created
            AssertFailedToCreatePropertiesFiles(result, logger);
        }

        [TestMethod]
        public void FileGen_SharedFiles()
        {
            // Shared files should be attached to the root project

            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var project1Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project1");
            var project1Path = Path.Combine(project1Dir, "project1.proj");
            var project1Info = CreateProjectInfoInSubDir(testDir, "projectName1", Guid.NewGuid(), ProjectType.Product, false, project1Path, "UTF-8"); // not excluded
            var sharedFile = Path.Combine(testDir, "contentFile.txt");
            CreateEmptyFile(testDir, "contentFile.txt");

            // Reference shared file, but not under the project directory
            var contentFileList1 = CreateFile(project1Dir, "contentList.txt", sharedFile);
            AddAnalysisResult(project1Info, AnalysisType.FilesToAnalyze, contentFileList1);

            var project2Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project2");
            var project2Path = Path.Combine(project2Dir, "project2.proj");
            var project2Info = CreateProjectInfoInSubDir(testDir, "projectName2", Guid.NewGuid(), ProjectType.Product, false, project2Path, "UTF-8"); // not excluded

            // Reference shared file, but not under the project directory
            var contentFileList2 = CreateFile(project2Dir, "contentList.txt", sharedFile);
            AddAnalysisResult(project2Info, AnalysisType.FilesToAnalyze, contentFileList2);

            var logger = new TestLogger();
            var config = CreateValidConfig(testDir);

            // Act
            var result = new PropertiesFileGenerator(config, logger).GenerateFile();

            // Assert
            var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists("sonar.projectBaseDir", testDir);
            provider.AssertSettingExists("sonar.sources", sharedFile);
        }

        // SONARMSBRU-335
        [TestMethod]
        public void FileGen_SharedFiles_CaseInsensitive()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            // Create 2 uuids and order them so that test is reproducible
            var uuids = new Guid[] { Guid.NewGuid(), Guid.NewGuid() };
            Array.Sort(uuids);

            var project1Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project1");
            var project1Path = Path.Combine(project1Dir, "project1.proj");
            var project1Info = CreateProjectInfoInSubDir(testDir, "projectName1", uuids[0], ProjectType.Product, false, project1Path, "UTF-8"); // not excluded
            var sharedFile = Path.Combine(testDir, "contentFile.txt");
            var sharedFileDifferentCase = Path.Combine(testDir, "ContentFile.TXT");
            CreateEmptyFile(testDir, "contentFile.txt");

            // Reference shared file, but not under the project directory
            var contentFileList1 = CreateFile(project1Dir, "contentList.txt", sharedFile);
            AddAnalysisResult(project1Info, AnalysisType.FilesToAnalyze, contentFileList1);

            var project2Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project2");
            var project2Path = Path.Combine(project2Dir, "project2.proj");
            var project2Info = CreateProjectInfoInSubDir(testDir, "projectName2", uuids[1], ProjectType.Product, false, project2Path, "UTF-8"); // not excluded

            // Reference shared file, but not under the project directory
            var contentFileList2 = CreateFile(project2Dir, "contentList.txt", sharedFileDifferentCase);
            AddAnalysisResult(project2Info, AnalysisType.FilesToAnalyze, contentFileList2);

            var logger = new TestLogger();
            var config = CreateValidConfig(testDir);

            // Act
            var result = new PropertiesFileGenerator(config, logger).GenerateFile();

            // Assert
            var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists("sonar.projectBaseDir", testDir);
            // First one wins
            provider.AssertSettingExists("sonar.sources", sharedFile);
        }

        // SONARMSBRU-336
        [TestMethod]
        public void FileGen_SharedFiles_BelongToAnotherProject()
        {
            // Shared files that belong to another project should NOT be attached to the root project

            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var project1Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project1");
            var project1Path = Path.Combine(project1Dir, "project1.proj");
            var project1Guid = Guid.NewGuid();
            var project1Info = CreateProjectInfoInSubDir(testDir, "projectName1", project1Guid, ProjectType.Product, false, project1Path, "UTF-8"); // not excluded
            var fileInProject1 = Path.Combine(project1Dir, "contentFile.txt");
            CreateEmptyFile(project1Dir, "contentFile.txt");

            // Reference shared file, but not under the project directory
            var contentFileList1 = CreateFile(project1Dir, "contentList.txt", fileInProject1);
            AddAnalysisResult(project1Info, AnalysisType.FilesToAnalyze, contentFileList1);

            var project2Dir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project2");
            var project2Path = Path.Combine(project2Dir, "project2.proj");
            var project2Info = CreateProjectInfoInSubDir(testDir, "projectName2", Guid.NewGuid(), ProjectType.Product, false, project2Path, "UTF-8"); // not excluded

            // Reference shared file, but not under the project directory
            var contentFileList2 = CreateFile(project2Dir, "contentList.txt", fileInProject1);
            AddAnalysisResult(project2Info, AnalysisType.FilesToAnalyze, contentFileList2);

            var logger = new TestLogger();
            var config = CreateValidConfig(testDir);

            // Act
            var result = new PropertiesFileGenerator(config, logger).GenerateFile();

            // Assert
            var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists("sonar.projectBaseDir", testDir);
            provider.AssertSettingDoesNotExist("sonar.sources");
            provider.AssertSettingExists(project1Guid.ToString().ToUpper() + ".sonar.sources", fileInProject1);
        }

        [TestMethod] //https://jira.codehaus.org/browse/SONARMSBRU-13: Analysis fails if a content file referenced in the MSBuild project does not exist
        public void FileGen_MissingFilesAreSkipped()
        {
            // Create project info with a managed file list and a content file list.
            // Each list refers to a file that does not exist on disk.
            // The missing files should not appear in the generated properties file.

            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var projectBaseDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Project1");
            var projectFullPath = CreateEmptyFile(projectBaseDir, "project1.proj");

            var existingManagedFile = CreateEmptyFile(projectBaseDir, "File1.cs");
            var existingContentFile = CreateEmptyFile(projectBaseDir, "Content1.txt");

            var missingManagedFile = Path.Combine(projectBaseDir, "MissingFile1.cs");
            var missingContentFile = Path.Combine(projectBaseDir, "MissingContent1.txt");

            var projectInfo = new ProjectInfo()
            {
                FullPath = projectFullPath,
                AnalysisResults = new List<AnalysisResult>(),
                IsExcluded = false,
                ProjectGuid = Guid.NewGuid(),
                ProjectName = "project1.proj",
                ProjectType = ProjectType.Product,
                Encoding = "UTF-8"
            };

            var analysisFileList = CreateFileList(projectBaseDir, "filesToAnalyze.txt", existingManagedFile, missingManagedFile, existingContentFile, missingContentFile);
            projectInfo.AddAnalyzerResult(AnalysisType.FilesToAnalyze, analysisFileList);

            var projectInfoDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "ProjectInfo1Dir");
            var projectInfoFilePath = Path.Combine(projectInfoDir, FileConstants.ProjectInfoFileName);
            projectInfo.Save(projectInfoFilePath);

            var logger = new TestLogger();
            var config = new AnalysisConfig()
            {
                SonarProjectKey = "my_project_key",
                SonarProjectName = "my_project_name",
                SonarProjectVersion = "1.0",
                SonarOutputDir = testDir
            };

            // Act
            var result = new PropertiesFileGenerator(config, logger).GenerateFile();

            var actual = File.ReadAllText(result.FullPropertiesFilePath);

            // Assert
            AssertFileIsReferenced(existingContentFile, actual);
            AssertFileIsReferenced(existingManagedFile, actual);

            AssertFileIsNotReferenced(missingContentFile, actual);
            AssertFileIsNotReferenced(missingManagedFile, actual);

            logger.AssertSingleWarningExists(missingManagedFile);
            logger.AssertSingleWarningExists(missingContentFile);
        }

        [TestMethod]
        [Description("Checks that the generated properties file contains additional properties")]
        public void FileGen_AdditionalProperties()
        {
            // 0. Arrange
            var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var logger = new TestLogger();

            CreateProjectWithFiles("project1", analysisRootDir);
            var config = CreateValidConfig(analysisRootDir);

            // Add additional properties
            config.LocalSettings = new AnalysisProperties
            {
                new Property() { Id = "key1", Value = "value1" },
                new Property() { Id = "key.2", Value = "value two" },
                new Property() { Id = "key.3", Value = " " },

                // Sensitive data should not be written
                new Property() { Id = SonarProperties.DbPassword, Value = "secret db pwd" },
                new Property() { Id = SonarProperties.SonarPassword, Value = "secret pwd" }
            };

            // Server properties should not be added
            config.ServerSettings = new AnalysisProperties
            {
                new Property() { Id = "server.key", Value = "should not be added" }
            };

            // Act
            var result = new PropertiesFileGenerator(config, logger).GenerateFile();

            // Assert
            AssertExpectedProjectCount(1, result);

            // One valid project info file -> file created
            AssertPropertiesFilesCreated(result, logger);

            var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists("key1", "value1");
            provider.AssertSettingExists("key.2", "value two");
            provider.AssertSettingExists("key.3", "");

            provider.AssertSettingDoesNotExist("server.key");

            provider.AssertSettingDoesNotExist(SonarProperties.DbPassword);
            provider.AssertSettingDoesNotExist(SonarProperties.SonarPassword);
        }

        [TestMethod]
        public void FileGen_WhenNoGuid_LogWarnings()
        {
            // Arrange
            var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var logger = new TestLogger();

            CreateProjectWithFiles("project1", analysisRootDir, Guid.Empty);
            var config = CreateValidConfig(analysisRootDir);

            // Act
            var result = new PropertiesFileGenerator(config, logger).GenerateFile();

            // Assert
            result.RanToCompletion.Should().BeFalse();
            AssertExpectedProjectCount(1, result);
            AssertFailedToCreatePropertiesFiles(result, logger);

            logger.Warnings.Should().HaveCount(1);
            logger.Warnings[0].Should().StartWith("The following projects do not have a valid ProjectGuid and were not built using a valid solution (.sln) thus will be skipped from analysis...");
        }

        [TestMethod] // Old VS Bootstrapper should be forceably disabled: https://jira.sonarsource.com/browse/SONARMSBRU-122
        public void FileGen_VSBootstrapperIsDisabled()
        {
            // 0. Arrange
            var logger = new TestLogger();

            // Act
            var result = ExecuteAndCheckSucceeds("disableBootstrapper", logger);

            // Assert
            var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void FileGen_VSBootstrapperIsDisabled_OverrideUserSettings_DifferentValue()
        {
            // 0. Arrange
            var logger = new TestLogger();

            // Try to explicitly enable the setting
            var bootstrapperProperty = new Property() { Id = AnalysisConfigExtensions.VSBootstrapperPropertyKey, Value = "true" };

            // Act
            var result = ExecuteAndCheckSucceeds("disableBootstrapperDiff", logger, bootstrapperProperty);

            // Assert
            var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");
            logger.AssertSingleWarningExists(AnalysisConfigExtensions.VSBootstrapperPropertyKey);
        }

        [TestMethod]
        public void FileGen_VSBootstrapperIsDisabled_OverrideUserSettings_SameValue()
        {
            // Arrange
            var logger = new TestLogger();
            var bootstrapperProperty = new Property() { Id = AnalysisConfigExtensions.VSBootstrapperPropertyKey, Value = "false" };

            // Act
            var result = ExecuteAndCheckSucceeds("disableBootstrapperSame", logger, bootstrapperProperty);

            // Assert
            var provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists(AnalysisConfigExtensions.VSBootstrapperPropertyKey, "false");
            logger.AssertDebugMessageExists(AnalysisConfigExtensions.VSBootstrapperPropertyKey);
            logger.AssertWarningsLogged(0); // not expecting a warning if the user has supplied the value we want
        }

        [TestMethod]
        public void FileGen_ComputeProjectBaseDir()
        {
            VerifyProjectBaseDir(
                expectedValue: @"d:\work\mysources", // if there is a user value, use it
                teamBuildValue: @"d:\work",
                userValue: @"d:\work\mysources",
                projectPaths: new[] { @"d:\work\proj1.csproj" });

            VerifyProjectBaseDir(
              expectedValue: @"d:\work",  // if no user value, use the team build value
              teamBuildValue: @"d:\work",
              userValue: null,
              projectPaths: new[] { @"e:\work" });

            VerifyProjectBaseDir(
               expectedValue: @"e:\work",  // if no team build value, use the common project paths root
               teamBuildValue: null,
               userValue: "",
               projectPaths: new[] { @"e:\work" });

            VerifyProjectBaseDir(
              expectedValue: @"e:\work",  // if no team build value, use the common project paths root
              teamBuildValue: null,
              userValue: "",
              projectPaths: new[] { @"e:\work", @"e:\work" });

            VerifyProjectBaseDir(
              expectedValue: @"e:\work",  // if no team build value, use the common project paths root
              teamBuildValue: null,
              userValue: "",
              projectPaths: new[] { @"e:\work\A", @"e:\work\B\C" });

            VerifyProjectBaseDir(
              expectedValue: @"e:\work",  // if no team build value, use the common project paths root
              teamBuildValue: null,
              userValue: "",
              projectPaths: new[] { @"e:\work\A", @"e:\work\B", @"e:\work\C" });

            VerifyProjectBaseDir(
              expectedValue: @"e:\work\A",  // if no team build value, use the common project paths root
              teamBuildValue: null,
              userValue: "",
              projectPaths: new[] { @"e:\work\A\X", @"e:\work\A", @"e:\work\A" });

            VerifyProjectBaseDir(
              expectedValue: TestSonarqubeOutputDir,  // if no common root exists, use the .sonarqube/out dir
              teamBuildValue: null,
              userValue: "",
              projectPaths: new[] { @"f:\work\A", @"e:\work\B" });

            // Support relative paths
            VerifyProjectBaseDir(
                expectedValue: Path.Combine(Directory.GetCurrentDirectory(), "src"),
                teamBuildValue: null,
                userValue: @".\src",
                projectPaths: new[] { @"d:\work\proj1.csproj" });

            // Support short name paths
            var result = ComputeProjectBaseDir(
                teamBuildValue: null,
                userValue: @"C:\PROGRA~1",
                projectPaths: new[] { @"d:\work\proj1.csproj" });
            result.Should().BeOneOf(@"C:\Program Files", @"C:\Program Files (x86)");
        }

        [TestMethod]
        public void ProjectData_Orders_AnalyzerOutPaths()
        {
            var guid = Guid.NewGuid();

            var projectInfos = new[]
            {
                new ProjectInfo
                {
                    ProjectGuid = guid,
                    Configuration = "Release",
                    Platform = "anyCpu",
                    TargetFramework = "netstandard2.0",
                    AnalysisSettings = new AnalysisProperties
                    {
                        new Property { Id = ".analyzer.projectOutPath", Value = "1" }
                    },
                },
                new ProjectInfo
                {
                    ProjectGuid = guid,
                    Configuration = "Debug",
                    Platform = "anyCpu",
                    TargetFramework = "netstandard2.0",
                    AnalysisSettings = new AnalysisProperties
                    {
                        new Property { Id = ".analyzer.projectOutPath", Value = "2" }
                    },
                },
                new ProjectInfo
                {
                    ProjectGuid = guid,
                    Configuration = "Debug",
                    Platform = "x86",
                    TargetFramework = "net46",
                    AnalysisSettings = new AnalysisProperties
                    {
                        new Property { Id = ".analyzer.projectOutPath", Value = "3" }
                    },
                },
                new ProjectInfo
                {
                    ProjectGuid = guid,
                    Configuration = "Debug",
                    Platform = "x86",
                    TargetFramework = "netstandard2.0",
                    AnalysisSettings = new AnalysisProperties
                    {
                        new Property { Id = ".analyzer.projectOutPath", Value = "4" }
                    },
                },
            };

            var logger = new TestLogger();

            var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
            var propertiesFileGenerator = new PropertiesFileGenerator(CreateValidConfig(analysisRootDir), logger, new RoslynV1SarifFixer(logger), new RuntimeInformationWrapper());
            var results = propertiesFileGenerator.ToProjectData(projectInfos.GroupBy(p => p.ProjectGuid).First()).AnalyzerOutPaths.ToList();

            results.Should().HaveCount(4);
            results[0].FullName.Should().Be(new FileInfo("2").FullName);
            results[1].FullName.Should().Be(new FileInfo("3").FullName);
            results[2].FullName.Should().Be(new FileInfo("4").FullName);
            results[3].FullName.Should().Be(new FileInfo("1").FullName);
        }


        [TestMethod]
        public void ToProjectData_ProjectsWithDuplicateGuid()
        {
            var guid = Guid.NewGuid();

            var projectInfos = new[]
            {
                new ProjectInfo
                {
                    ProjectGuid = guid,
                    FullPath = "path1"
                },
                new ProjectInfo
                {
                    ProjectGuid = guid,
                    FullPath = "path2"
                },
                new ProjectInfo
                {
                    ProjectGuid = guid,
                    FullPath = "path2"
                },
            };

            var logger = new TestLogger();
            var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "project");
            var propertiesFileGenerator = new PropertiesFileGenerator(CreateValidConfig(analysisRootDir), logger);
            var result = propertiesFileGenerator.ToProjectData(projectInfos.GroupBy(p => p.ProjectGuid).First());

            result.Status.Should().Be(ProjectInfoValidity.DuplicateGuid);
            logger.Warnings.Should().BeEquivalentTo(
                new[]
                {
                    $"Duplicate ProjectGuid: \"{guid}\". The project will not be analyzed by SonarQube. Project file: \"path1\"",
                    $"Duplicate ProjectGuid: \"{guid}\". The project will not be analyzed by SonarQube. Project file: \"path2\"",
                });
        }

        [TestMethod]
        public void GetClosestProjectOrDefault_WhenNoProjects_ReturnsNull()
        {
            // Arrange & Act
            var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo("foo"), Enumerable.Empty<ProjectData>());

            // Assert
            actual.Should().BeNull();
        }

        [TestMethod]
        public void GetClosestProjectOrDefault_WhenNoMatch_ReturnsNull()
        {
            // Arrange
            var projects = new[]
            {
                new ProjectData(new ProjectInfo { FullPath = "D:\\foo.csproj" }),
                new ProjectData(new ProjectInfo { FullPath = "~foo\\bar.csproj" }),
                new ProjectData(new ProjectInfo { FullPath = "C:\\foobar.csproj" }),
            };

            // Act
            var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo("E:\\foo"), projects);

            // Assert
            actual.Should().BeNull();
        }

        [TestMethod]
        public void GetClosestProjectOrDefault_WhenOnlyOneProjectMatchingWithSameCase_ReturnsProject()
        {
            // Arrange
            var projects = new[]
            {
                new ProjectData(new ProjectInfo { FullPath = "foo.csproj" }),
                new ProjectData(new ProjectInfo { FullPath = "~foo\\bar.csproj" }),
                new ProjectData(new ProjectInfo { FullPath = "C:\\foo\\foo.csproj" }),
            };

            // Act
            var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo("C:\\foo\\foo.cs"), projects);

            // Assert
            actual.Should().Be(projects[2]);
        }

        [TestMethod]
        public void GetClosestProjectOrDefault_WhenOnlyOneProjectMatchingWithDifferentCase_ReturnsProject()
        {
            // Arrange
            var projects = new[]
            {
                new ProjectData(new ProjectInfo { FullPath = "foo.csproj" }),
                new ProjectData(new ProjectInfo { FullPath = "~foo\\bar.csproj" }),
                new ProjectData(new ProjectInfo { FullPath = "C:\\foo\\foo.csproj" }),
            };

            // Act
            var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo("C:\\FOO\\FOO.cs"), projects);

            // Assert
            actual.Should().Be(projects[2]);
        }

        [TestMethod]
        public void GetClosestProjectOrDefault_WhenOnlyOneProjectMatchingWithDifferentSeparators_ReturnsProject()
        {
            // Arrange
            var projects = new[]
            {
                new ProjectData(new ProjectInfo { FullPath = "foo.csproj" }),
                new ProjectData(new ProjectInfo { FullPath = "~foo\\bar.csproj" }),
                new ProjectData(new ProjectInfo { FullPath = "C:/foo/foo.csproj" }),
            };

            // Act
            var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo("C:\\foo\\foo.cs"), projects);

            // Assert
            actual.Should().Be(projects[2]);
        }

        [TestMethod]
        public void GetClosestProjectOrDefault_WhenMultipleProjectsMatch_ReturnsProjectWithLongestMatch()
        {
            // Arrange
            var projects = new[]
            {
                new ProjectData(new ProjectInfo { FullPath = "C:\\foo.csproj" }),
                new ProjectData(new ProjectInfo { FullPath = "C:\\foo\\bar.csproj" }),
                new ProjectData(new ProjectInfo { FullPath = "C:\\foo\\bar\\foo.csproj" }),
                 new ProjectData(new ProjectInfo { FullPath = "C:\\foo\\xxx.csproj" }),
                 new ProjectData(new ProjectInfo { FullPath = "C:\\foo\\bar\\foobar\\foo.csproj" }),
            };

            // Act
            var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo("C:\\foo\\bar\\foo.cs"), projects);

            // Assert
            actual.Should().Be(projects[2]);
        }

        [TestMethod]
        public void GetClosestProjectOrDefault_WhenMultipleProjectsMatchWithSameLength_ReturnsNull()
        {
            // Arrange
            var projects = new[]
            {
                new ProjectData(new ProjectInfo { FullPath = "C:\\fooNet46.csproj" }),
                new ProjectData(new ProjectInfo { FullPath = "C:\\fooXamarin.csproj" }),
                new ProjectData(new ProjectInfo { FullPath = "C:\\fooNetStd.csproj" }),
            };

            // Act
            var actual = PropertiesFileGenerator.GetSingleClosestProjectOrDefault(new FileInfo("C:\\foo\\foo.cs"), projects);

            // Assert
            actual.Should().BeNull();
        }
        #endregion Tests

        #region Assertions

        /// <summary>
        /// Creates a single new project valid project with dummy files and analysis config file with the specified local settings.
        /// Checks that a property file is created.
        /// </summary>
        private ProjectInfoAnalysisResult ExecuteAndCheckSucceeds(string projectName, TestLogger logger, params Property[] localSettings)
        {
            var analysisRootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, projectName);

            CreateProjectWithFiles(projectName, analysisRootDir);
            var config = CreateValidConfig(analysisRootDir);

            config.LocalSettings = new AnalysisProperties();
            foreach (var property in localSettings)
            {
                config.LocalSettings.Add(property);
            }

            // Act
            var result = new PropertiesFileGenerator(config, logger).GenerateFile();

            // Assert
            AssertExpectedProjectCount(1, result);
            AssertPropertiesFilesCreated(result, logger);

            return result;
        }

        private static void AssertFailedToCreatePropertiesFiles(ProjectInfoAnalysisResult result, TestLogger logger)
        {
            result.FullPropertiesFilePath.Should().BeNull("Not expecting the sonar-scanner properties file to have been set");
            result.RanToCompletion.Should().BeFalse("Expecting the property file generation to have failed");

            AssertNoValidProjects(result);

            logger.AssertErrorsLogged();
        }

        private void AssertPropertiesFilesCreated(ProjectInfoAnalysisResult result, TestLogger logger)
        {
            result.FullPropertiesFilePath.Should().NotBeNull("Expecting the sonar-scanner properties file to have been set");

            AssertValidProjectsExist(result);
            TestContext.AddResultFile(result.FullPropertiesFilePath);

            logger.AssertErrorsLogged(0);
        }

        private static void AssertExpectedStatus(string expectedProjectName, ProjectInfoValidity expectedStatus, ProjectInfoAnalysisResult actual)
        {
            var matches = actual.GetProjectsByStatus(expectedStatus).Where(p => p.ProjectName.Equals(expectedProjectName));
            matches.Should().ContainSingle("ProjectInfo was not classified as expected. Project name: {0}, expected status: {1}", expectedProjectName, expectedStatus);
        }

        private static void AssertNoValidProjects(ProjectInfoAnalysisResult actual)
        {
            IEnumerable<ProjectInfo> matches = actual.GetProjectsByStatus(ProjectInfoValidity.Valid);
            matches.Should().BeEmpty("Not expecting to find any valid ProjectInfo files");
        }

        private static void AssertValidProjectsExist(ProjectInfoAnalysisResult actual)
        {
            IEnumerable<ProjectInfo> matches = actual.GetProjectsByStatus(ProjectInfoValidity.Valid);
            matches.Should().NotBeEmpty("Expecting at least one valid ProjectInfo file to exist");
        }

        private static void AssertExpectedProjectCount(int expected, ProjectInfoAnalysisResult actual)
        {
            actual.Projects.Should().HaveCount(expected, "Unexpected number of projects in the result");
        }

        private static void AssertFileIsReferenced(string fullFilePath, string content)
        {
            var formattedPath = PropertiesWriter.Escape(fullFilePath);
            content.Should().Contain(formattedPath, "Files should be referenced: {0}", formattedPath);
        }

        private static void AssertFileIsNotReferenced(string fullFilePath, string content)
        {
            var formattedPath = PropertiesWriter.Escape(fullFilePath);
            content.Should().NotContain(formattedPath, "File should not be referenced: {0}", formattedPath);
        }

        #endregion Assertions

        #region Private methods

        private string ComputeProjectBaseDir(string teamBuildValue, string userValue, string[] projectPaths)
        {
            var config = new AnalysisConfig();
            var logger = new TestLogger();
            new PropertiesWriter(config, logger);
            config.SonarOutputDir = TestSonarqubeOutputDir;

            config.SourcesDirectory = teamBuildValue;

            if (config.LocalSettings == null)
            {
                config.LocalSettings = new AnalysisProperties();
            }
            config.LocalSettings.Add(new Property { Id = SonarProperties.ProjectBaseDir, Value = userValue });

            // Act
            return new PropertiesFileGenerator(config, logger)
                .ComputeRootProjectBaseDir(projectPaths.Select(p => new DirectoryInfo(p)))
                .FullName;
        }

        private void VerifyProjectBaseDir(string expectedValue, string teamBuildValue, string userValue, string[] projectPaths)
        {
            var result = ComputeProjectBaseDir(teamBuildValue, userValue, projectPaths);
            result.Should().Be(expectedValue);
        }

        /// <summary>
        /// Creates a project info under the specified analysis root directory
        /// together with the supporting project and content files, along with additional properties (if specified)
        /// </summary>
        private void CreateProjectWithFiles(string projectName, string analysisRootPath, bool createContentFiles = true, AnalysisProperties additionalProperties = null)
        {
            CreateProjectWithFiles(projectName, analysisRootPath, Guid.NewGuid(), createContentFiles, additionalProperties);
        }

        /// <summary>
        /// Creates a project info under the specified analysis root directory
        /// together with the supporting project and content files, along with GUID and additional properties (if specified)
        /// </summary>
        private void CreateProjectWithFiles(string projectName, string analysisRootPath, Guid projectGuid, bool createContentFiles = true, AnalysisProperties additionalProperties = null)
        {
            // Create a project with content files in a new subdirectory
            var projectDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, Path.Combine("projects", projectName));
            var projectFilePath = Path.Combine(projectDir, Path.ChangeExtension(projectName, "proj"));

            // Create a project info file in the correct location under the analysis root
            var contentProjectInfo = CreateProjectInfoInSubDir(analysisRootPath, projectName, projectGuid, ProjectType.Product, false, projectFilePath, "UTF-8", additionalProperties); // not excluded

            // Create content / managed files if required
            if (createContentFiles)
            {
                var contentFile = CreateEmptyFile(projectDir, "contentFile1.txt");
                var contentFileList = CreateFile(projectDir, "contentList.txt", contentFile);
                AddAnalysisResult(contentProjectInfo, AnalysisType.FilesToAnalyze, contentFileList);
            }
        }

        private static AnalysisConfig CreateValidConfig(string outputDir)
        {
            var dummyProjectKey = Guid.NewGuid().ToString();

            var config = new AnalysisConfig()
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
            var fullPath = Path.Combine(parentDir, fileName);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        /// <summary>
        /// Creates a new project info file in a new subdirectory with the given additional properties.
        /// </summary>
        private static string CreateProjectInfoInSubDir(string parentDir,
            string projectName, Guid projectGuid, ProjectType projectType, bool isExcluded, string fullProjectPath, string encoding,
            AnalysisProperties additionalProperties = null)
        {
            var newDir = Path.Combine(parentDir, projectName);
            Directory.CreateDirectory(newDir); // ensure the directory exists

            var project = new ProjectInfo()
            {
                FullPath = fullProjectPath,
                ProjectName = projectName,
                ProjectGuid = projectGuid,
                ProjectType = projectType,
                IsExcluded = isExcluded,
                Encoding = encoding
            };

            if (additionalProperties != null)
            {
                project.AnalysisSettings = additionalProperties;
            }

            var filePath = Path.Combine(newDir, FileConstants.ProjectInfoFileName);
            project.Save(filePath);
            return filePath;
        }

        private static void AddAnalysisResult(string projectInfoFile, AnalysisType resultType, string location)
        {
            var projectInfo = ProjectInfo.Load(projectInfoFile);
            projectInfo.AddAnalyzerResult(resultType, location);
            projectInfo.Save(projectInfoFile);
        }

        private static string CreateFileList(string parentDir, string fileName, params string[] files)
        {
            var fullPath = Path.Combine(parentDir, fileName);
            File.WriteAllLines(fullPath, files);
            return fullPath;
        }

        #endregion Private methods
    }
}
