//-----------------------------------------------------------------------
// <copyright file="BuildWrapperTargetsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class BuildWrapperTargetsTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void SQBeforeClCompile_TempPathNotSet_NotExecuted()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            BuildLogger logger = new BuildLogger();

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = "";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            // Act
            BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.BuildWrapperBeforeClCompileTarget);

            // Assert
            logger.AssertTargetNotExecuted(TargetConstants.BuildWrapperBeforeClCompileTarget);
        }

        [TestMethod]
        public void SQBeforeClCompile_SkipFlagIsSet_NotExecuted()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            BuildLogger logger = new BuildLogger();

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.BuildWrapperSkipFlag = "true";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            // Act
            BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.BuildWrapperBeforeClCompileTarget);

            // Assert
            logger.AssertTargetNotExecuted(TargetConstants.BuildWrapperBeforeClCompileTarget);
        }


        [TestMethod]
        public void SQBeforeClCompile_DefaultBinPath_BinariesNotFound_TaskNotExecuted()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");
            BuildLogger logger = new BuildLogger();

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.BuildWrapperSkipFlag = "false";
            properties.SonarQubeOutputPath = rootOutputFolder;

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            // Act
            BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.BuildWrapperBeforeClCompileTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.BuildWrapperBeforeClCompileTarget);
            logger.AssertTargetNotExecuted(TargetConstants.BuildWrapperAttachTarget);
        }

        [TestMethod]
        public void SQBeforeClCompile_DefaultBinPath_BinariesFound_TaskExecuted()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");
            BuildLogger logger = new BuildLogger();

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.BuildWrapperSkipFlag = "false";
            properties.SonarQubeOutputPath = rootOutputFolder;

            // By default, the targets try to find the build wrapper binaries in a specific folder
            // below the folder containing the integration tasks assembly

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            // Check we can find the integration targets file in the expected location
            string targetsDir = TestUtils.GetTestSpecificFolderName(this.TestContext);
            string integrationTargetsPath = Path.Combine(targetsDir, TestUtils.AnalysisTargetFile);
            Assert.IsTrue(File.Exists(integrationTargetsPath), "Test setup error: did not find the integration targets in the expected location: {0}", integrationTargetsPath);

            // The targets expect to find the build wrapper binaries in a folder under
            // the folder containing the tasks assembly.
            // Create a dummy tasks assembly
            string dummyAsmFilePath = Path.Combine(targetsDir, "SonarQube.Integration.Tasks.dll");
            Assert.IsFalse(File.Exists(dummyAsmFilePath), "Test setup error: not expecting the integration tasks assembly to exist at {0}", dummyAsmFilePath);
            File.WriteAllText(dummyAsmFilePath, "dummy content");

            // Create the build wrapper sub directory
            string buildWrapperBinDir = Path.Combine(targetsDir, "build-wrapper-win-x86");
            Directory.CreateDirectory(buildWrapperBinDir);

            // Create the required build wrapper files relative to this location
            CreateBuildWrapperMarkerFile(buildWrapperBinDir);

            // Act
            BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.BuildWrapperBeforeClCompileTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.BuildWrapperBeforeClCompileTarget);
            logger.AssertTargetExecuted(TargetConstants.BuildWrapperAttachTarget);
            // Note: task should fail because not all required files are present. However,
            // the important thing for this test is that is executed.
            logger.AssertTaskExecuted(TargetConstants.BuildWrapperAttachTask);
        }

        [TestMethod]
        public void SQBeforeClCompile_SpecifiedWrapperLocation_BinariesFound_TaskExecuted()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");
            BuildLogger logger = new BuildLogger();

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.BuildWrapperSkipFlag = "false";
            properties.SonarQubeOutputPath = rootOutputFolder;

            string buildWrapperDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "dummyBW");

            properties.BuildWrapperBinPath = buildWrapperDir;
            // Create the required build wrapper files relative to this location
            CreateBuildWrapperMarkerFile(buildWrapperDir);

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            // Act
            BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.BuildWrapperBeforeClCompileTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.BuildWrapperBeforeClCompileTarget);
            logger.AssertTargetExecuted(TargetConstants.BuildWrapperAttachTarget);
            // Note: task should fail because not all required files are present. However,
            // the important thing for this test is that is executed.
            logger.AssertTaskExecuted(TargetConstants.BuildWrapperAttachTask);
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Creates dummy files and directories used by the targets to
        /// decide that the build wrapper is available.
        /// </summary>
        private static void CreateBuildWrapperMarkerFile(string rootDirectory)
        {
            // Create the binary file the targets use to indicate the existence of the build wrapper
            string markerFilePath = Path.Combine(rootDirectory, "build-wrapper-win-x86-32.exe");
            File.WriteAllText(markerFilePath, "dummy content");
        }

        #endregion
    }
}
