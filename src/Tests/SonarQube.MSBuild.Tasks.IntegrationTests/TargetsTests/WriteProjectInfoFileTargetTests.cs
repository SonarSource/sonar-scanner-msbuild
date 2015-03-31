//-----------------------------------------------------------------------
// <copyright file="WriteProjectInfoFileTargetTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    [DeploymentItem("LinkedFiles\\SonarQube.Integration.v0.1.targets")]
    public class WriteProjectInfoFileTargetTests
    {
        public TestContext TestContext { get; set; }

        #region Test project recognition tests

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_ExplicitMarking_True()
        {
            // If the project is explicitly marked as a test then the other condition should be ignored

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarTestProject = "true";
            preImportProperties.TestProjectNameRegex = "pattern that won't match anything";

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "test.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_ExplicitMarking_False()
        {
            // If the project is explicitly marked as not a test then the other condition should be ignored

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarTestProject = "false";
            preImportProperties.TestProjectNameRegex = "*.*";
            preImportProperties.ProjectTypeGuids = "X" + TargetConstants.MsTestProjectTypeGuid.ToUpperInvariant() + "Y";

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "foo.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsProductProject(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_WildcardMatch_Default_NoMatch()
        {
            // Check the default wildcard matching

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "foo.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsProductProject(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_WildcardMatch_Default_Match()
        {
            // Check the default wildcard matching

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "foo.tests.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_WildcardMatchDirName1_Default()
        {
            // Check the default wildcard matching includes projects under a directory called "tests"

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs\\tests\\foo\\");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "foo.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_WildcardMatchDirName2_Default()
        {
            // Check the default wildcard matching includes projects under a directory called "test"

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs\\TEst\\bar\\");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "bar.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
        }
        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_WildcardNoMatchDirName_Default()
        {
            // Check the default wildcard matching does not include projects where "tests"
            // is part of the directory name, but not the whole name

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs\\XXtests\\foo\\");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "foo.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsProductProject(projectInfo);
        }


        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_WildcardMatch_UserSpecified_Match()
        {
            // Check user-specified wildcard matching

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.TestProjectNameRegex = ".*foo.*";

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "fOO.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_WildcardMatch_UserSpecified_NoMatch()
        {
            // Check user-specified wildcard matching

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.TestProjectNameRegex = @".*foo.*";

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "afoXB.proj");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsProductProject(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_HasTestGuid()
        {
            // Checks the MSTest project type guid is recognised

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.TestProjectNameRegex = @"pattern that won't match anything";
            preImportProperties.ProjectTypeGuids = "X" + TargetConstants.MsTestProjectTypeGuid.ToUpperInvariant() + "Y";

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "a.b");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("IsTest")]
        public void WriteProjectInfo_TestProject_HasTestGuid_LowerCase()
        {
            // Checks the MSTest project type guid is recognised

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.TestProjectNameRegex = @"pattern that won't match anything";
            preImportProperties.ProjectTypeGuids = "X" + TargetConstants.MsTestProjectTypeGuid.ToLowerInvariant() + "Y";

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "a.b");

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(descriptor, preImportProperties, rootOutputFolder);

            // Assert
            AssertIsTestProject(projectInfo);
        }

        #endregion

        #region File lists

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_ManagedCompileList_NoFiles()
        {
            // The managed file list should not be created if there are no files to compile

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "a.b.proj.txt");
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileDoesNotExist(projectInfo, AnalysisType.ManagedCompilerInputs);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_ManagedCompileList_HasFiles()
        {
            // The managed file list should be created with the expected files

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "a.proj.txt");
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            string file1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "false");
            string excludedFile1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "true");
            string file2 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "FALSE");
            string excludedFile2 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "TRUE");
            string file3 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, null); // no metadata

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.ManagedCompilerInputs, file1, file2, file3);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_ManagedCompileList_AutoGenFilesIgnored()
        {
            // The managed file list should not include items with <AutoGen>true</AutoGen> metadata

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "agM.proj.txt");
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            string compile1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, null, null); // no metadata
            string autogenTrue1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, null, "TRUE"); // only AutoGen, set to true
            string autogenTrue2 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "false", "truE"); // exclude=false, autogen=true
            string autogenFalseAndIncluded = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "false", "FALSe"); // exclude=false, autogen=false
            string autogenFalseButExcluded = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, "true", "false"); // exclude=true, autogen=false
            string nonCompileItem1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, null, "false"); // autogen=false, but wrong item type
            string nonCompileItem2 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, null, null); // wrong item type

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.ManagedCompilerInputs, compile1, autogenFalseAndIncluded);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_ContentFileList_NoFiles()
        {
            // The content file list should not be created if there are no files

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "content.proj.txt");
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileDoesNotExist(projectInfo, AnalysisType.ContentFiles);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_ContentFileList_HasFiles()
        {
            // The content file list should be created with the expected files

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "content.X.proj.txt");
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            string file1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "false");
            string excludedFile1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "true");
            string file2 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "FALSE");
            string excludedFile2 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "TRUE");
            string file3 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, null); // no metadata

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.ContentFiles, file1, file2, file3);
        }

        [TestMethod]
        [TestCategory("ProjectInfo")]
        [TestCategory("Lists")]
        public void WriteProjectInfo_ContentFileList_AutoGenFilesIgnored()
        {
            // The content file list should not include items with <AutoGen>true</AutoGen> metadata

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "agC.proj.txt");
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, new WellKnownProjectProperties(), rootOutputFolder);

            string content1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, null, null); // no metadata
            string autogenTrue1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, null, "TRUE"); // only AutoGen, set to true
            string autogenTrue2 = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "false", "truE"); // exclude=false, autogen=true
            string autogenFalseAndIncluded = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "false", "FALSe"); // exclude=false, autogen=false
            string autogenFalseButExcluded = AddFileToProject(projectRoot, TargetProperties.ItemType_Content, "true", "false"); // exclude=true, autogen=false
            string nonContentItem1 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, null, "false"); // autogen=false, but wrong item type
            string nonContentItem2 = AddFileToProject(projectRoot, TargetProperties.ItemType_Compile, null, null); // wrong item type

            // Act
            ProjectInfo projectInfo = ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);

            // Assert
            AssertResultFileExists(projectInfo, AnalysisType.ContentFiles, content1, autogenFalseAndIncluded);
        }

        #endregion


        #region Miscellaneous tests

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_ErrorIfProjectExcluded()
        {
            // The target should error if $(SonarQubeExclude) is true

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "excludedProj.txt");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeExclude = "true";
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, preImportProperties, rootOutputFolder);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.WriteProjectDataTarget);

            // Assert
            BuildAssertions.AssertTargetFailed(result, TargetConstants.WriteProjectDataTarget);

            logger.AssertExpectedErrorCount(1);
            logger.AssertTargetExecuted(TargetConstants.WriteProjectDataTarget);
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Creates a new project using the supplied descriptor and properties, then
        /// execute the WriteProjectInfoFile task. The method will check the build succeeded
        /// and that a single project output file was created.
        /// </summary>
        /// <returns>The project info file that was created during the build</returns>
        private ProjectInfo ExecuteWriteProjectInfo(ProjectDescriptor descriptor, WellKnownProjectProperties preImportProperties, string rootOutputFolder)
        {
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, preImportProperties, rootOutputFolder);

            return this.ExecuteWriteProjectInfo(projectRoot, rootOutputFolder);
        }

        private ProjectRootElement CreateInitializedProject(ProjectDescriptor descriptor, WellKnownProjectProperties preImportProperties, string rootOutputFolder)
        {
            // Still need to set the conditions so the target is invoked
            preImportProperties.RunSonarQubeAnalysis = "true";
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;
            ProjectRootElement projectRoot = BuildUtilities.CreateInitializedProjectRoot(this.TestContext, descriptor, preImportProperties);

            return projectRoot;
        }

        /// <summary>
        /// Executes the WriteProjectInfoFile target in the the supplied project.
        /// The method will check the build succeeded and that a single project
        /// output file was created.
        /// </summary>
        /// <returns>The project info file that was created during the build</returns>
        private ProjectInfo ExecuteWriteProjectInfo(ProjectRootElement projectRoot, string rootOutputFolder)
        {
            projectRoot.Save();
            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.WriteProjectDataTarget);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.WriteProjectDataTarget);

            logger.AssertNoWarningsOrErrors();
            logger.AssertTargetExecuted(TargetConstants.WriteProjectDataTarget);

            // Check expected project outputs
            Assert.AreEqual(1, Directory.EnumerateDirectories(rootOutputFolder).Count(), "Only expecting one child directory to exist under the root analysis output folder");
            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);

            return projectInfo;
        }

        /// <summary>
        /// Creates an empty file on disc and adds it to the project as an
        /// item with the specified ItemGroup include name.
        /// The SonarQubeExclude metadata item is set to the specified value.
        /// </summary>
        /// <param name="projectRoot"></param>
        /// <param name="includeName">The name of the item type e.g. Compile, Content</param>
        /// <param name="sonarQubeExclude">The value to assign to the SonarExclude metadata item</param>
        /// <returns>The full path to the new file</returns>
        private string AddFileToProject(ProjectRootElement projectRoot, string includeName, string sonarQubeExclude)
        {
            ProjectItemElement newItem = AddFileToProject(projectRoot, includeName);
            if (sonarQubeExclude != null)
            {
                newItem.AddMetadata(TargetProperties.SonarQubeExcludeMetadata, sonarQubeExclude);
            }          
            
            return newItem.Include;
        }

        /// Creates an empty file on disc and adds it to the project as an
        /// item with the specified ItemGroup include name.
        /// The SonarQubeExclude metadata item is set to the specified value.
        /// </summary>
        /// <param name="projectRoot"></param>
        /// <param name="includeName">The name of the item type e.g. Compile, Content</param>
        /// <param name="sonarQubeExclude">The value to assign to the SonarExclude metadata item</param>
        /// <returns>The full path to the new file</returns>
        private string AddFileToProject(ProjectRootElement projectRoot, string includeName, string sonarQubeExclude, string autoGen)
        {
            ProjectItemElement newItem = AddFileToProject(projectRoot, includeName);
            if (sonarQubeExclude != null)
            {
                newItem.AddMetadata(TargetProperties.SonarQubeExcludeMetadata, sonarQubeExclude);
            }
            if (autoGen != null)
            {
                newItem.AddMetadata(TargetProperties.AutoGenMetadata, autoGen);
            }

            return newItem.Include;
        }


        /// <summary>
        /// Creates an empty file on disc and adds it to the project as an
        /// item with the specified ItemGroup include name.
        /// The SonarQubeExclude metadata item is set to the specified value.
        /// </summary>
        /// <param name="projectRoot"></param>
        /// <param name="includeName">The name of the item type e.g. Compile, Content</param>
        /// <returns>The new project item</returns>
        private ProjectItemElement AddFileToProject(ProjectRootElement projectRoot, string includeName)
        {
            string projectPath = Path.GetDirectoryName(projectRoot.DirectoryPath); // project needs to have been saved for this to work
            string fileName = System.Guid.NewGuid().ToString();
            string fullPath = Path.Combine(projectPath, fileName);
            File.WriteAllText(fullPath, "");

            ProjectItemElement element = projectRoot.AddItem(includeName, fullPath);
            return element;
        }

        #endregion

        #region Assertions

        private static void AssertIsProductProject(ProjectInfo projectInfo)
        {
            Assert.AreEqual(ProjectType.Product, projectInfo.ProjectType, "Should be a product (i.e. non-test) project");
        }

        private static void AssertIsTestProject(ProjectInfo projectInfo)
        {
            Assert.AreEqual(ProjectType.Test, projectInfo.ProjectType, "Should be a test project");
        }

        private void AssertResultFileDoesNotExist(ProjectInfo projectInfo, AnalysisType resultType)
        {
            AnalysisResult result;
            bool found = projectInfo.TryGetAnalyzerResult(resultType, out result);

            if (found)
            {
                this.TestContext.AddResultFile(result.Location);
            }

            Assert.IsFalse(found, "Analysis result found unexpectedly. Result type: {0}", resultType);
        }

        private void AssertResultFileExists(ProjectInfo projectInfo, AnalysisType resultType, params string[] expected)
        {
            AnalysisResult result;
            bool found = projectInfo.TryGetAnalyzerResult(resultType, out result);

            Assert.IsTrue(found, "Analysis result not found: {0}", resultType);
            Assert.IsTrue(File.Exists(result.Location), "Analysis result file not found");

            this.TestContext.AddResultFile(result.Location);

            string[] actualFiles = File.ReadAllLines(result.Location);
            CollectionAssert.AreEquivalent(expected, actualFiles, "The analysis result file does not contain the expected entries");
        }

        #endregion
    }
}
