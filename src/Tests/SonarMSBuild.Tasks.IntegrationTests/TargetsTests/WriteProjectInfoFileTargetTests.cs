//-----------------------------------------------------------------------
// <copyright file="WriteProjectInfoFileTargetTests.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sonar.Common;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace Sonar.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    [DeploymentItem("LinkedFiles\\Sonar.Integration.v0.1.targets")]
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
            preImportProperties.SonarTestProjectNameRegex = "pattern that won't match anything";

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
            preImportProperties.SonarTestProjectNameRegex = "*.*";
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
        public void WriteProjectInfo_TestProject_WildcardMatch_UserSpecified_Match()
        {
            // Check user-specified wildcard matching

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarTestProjectNameRegex = ".*foo.*";

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
            preImportProperties.SonarTestProjectNameRegex = @".*foo.*";

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
            preImportProperties.SonarTestProjectNameRegex = @"pattern that won't match anything";
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
            preImportProperties.SonarTestProjectNameRegex = @"pattern that won't match anything";
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
            // The managed file list should not be created if there are no files to compil

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

        #endregion


        #region Miscellaneous tests

        [TestMethod]
        [TestCategory("ProjectInfo")]
        public void WriteProjectInfo_ErrorIfProjectExcluded()
        {
            // The target should error if $(SonarExclude) is true

            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            ProjectDescriptor descriptor = BuildUtilities.CreateValidNamedProjectDescriptor(rootInputFolder, "excludedProj.txt");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarExclude = "true";
            ProjectRootElement projectRoot = CreateInitializedProject(descriptor, preImportProperties, rootOutputFolder);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.WriteSonarProjectDataTarget);

            // Assert
            BuildAssertions.AssertTargetFailed(result, TargetConstants.WriteSonarProjectDataTarget);

            logger.AssertExpectedErrorCount(1);
            logger.AssertTargetExecuted(TargetConstants.WriteSonarProjectDataTarget);
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
            preImportProperties.RunSonarAnalysis = "true";
            preImportProperties.SonarOutputPath = rootOutputFolder;
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
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.WriteSonarProjectDataTarget);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.WriteSonarProjectDataTarget);

            logger.AssertNoWarningsOrErrors();
            logger.AssertTargetExecuted(TargetConstants.WriteSonarProjectDataTarget);

            // Check expected project outputs
            Assert.AreEqual(1, Directory.EnumerateDirectories(rootOutputFolder).Count(), "Only expecting one child directory to exist under the root analysis output folder");
            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);

            return projectInfo;
        }

        /// <summary>
        /// Creates an empty file on disc and adds it to the project as an
        /// item with the specified ItemGroup include name.
        /// The SonarExclude metadata item is set to the specified value.
        /// </summary>
        /// <param name="projectRoot"></param>
        /// <param name="includeName">The name of the item type e.g. Compile, Content</param>
        /// <param name="sonarExclude">The value to assign to the SonarExclude metadata item</param>
        /// <returns></returns>
        private string AddFileToProject(ProjectRootElement projectRoot, string includeName, string sonarExclude)
        {
            string projectPath = Path.GetDirectoryName(projectRoot.DirectoryPath); // project needs to have been saved for this to work
            string fileName = System.Guid.NewGuid().ToString();
            string fullPath = Path.Combine(projectPath, fileName);
            File.WriteAllText(fullPath, "");

            IList<KeyValuePair<string, string>> metadata = new List<KeyValuePair<string, string>>();

            if (sonarExclude != null)
            {
                metadata.Add(new KeyValuePair<string, string>(TargetProperties.SonarExclude, sonarExclude));
            }

            projectRoot.AddItem(includeName, fullPath, metadata);
            return fullPath;
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
