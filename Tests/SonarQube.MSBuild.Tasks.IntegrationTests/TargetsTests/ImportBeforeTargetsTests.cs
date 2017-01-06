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

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class ImportBeforeTargetsTests
    {
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Name of the property to check for to determine whether or not
        /// the targets have been imported or not
        /// </summary>
        private const string DummyAnalysisTargetsMarkerProperty = "DummyProperty";

        [TestInitialize]
        public void TestInitialize()
        {
            TestUtils.EnsureImportBeforeTargetsExists(this.TestContext);
        }

        #region Tests

        [TestMethod]
        [Description("Checks the properties are not set if SonarQubeTargetsPath is missing")]
        public void ImportsBefore_SonarQubeTargetsPathNotSet()
        {
            // 1. Prebuild
            // Arrange
            this.EnsureDummyIntegrationTargetsFileExists();

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTargetsPath = "";
            preImportProperties.TeamBuild2105BuildDirectory = "";
            preImportProperties.TeamBuildLegacyBuildDirectory = "";

            // Act
            ProjectInstance projectInstance = this.CreateAndEvaluateProject(preImportProperties);

            // Assert
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarQubeTargetFilePath);
            AssertAnalysisTargetsAreNotImported(projectInstance);


            // 2. Now build -> succeeds. Info target not executed
            BuildLogger logger = new BuildLogger();

            BuildResult result = BuildUtilities.BuildTargets(projectInstance, logger);

            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);
            logger.AssertTargetNotExecuted(TargetConstants.ImportBeforeInfoTarget);
            logger.AssertExpectedErrorCount(0);
        }

        [TestMethod]
        [Description("Checks the properties are not set if the project is building inside Visual Studio")]
        public void ImportsBefore_BuildingInsideVS_NotImported()
        {
            // 1. Pre-build
            // Arrange
            string dummySonarTargetsDir = this.EnsureDummyIntegrationTargetsFileExists();

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = Path.GetTempPath();
            preImportProperties.SonarQubeTargetsPath = Path.GetDirectoryName(dummySonarTargetsDir);
            preImportProperties.BuildingInsideVS = "tRuE"; // should not be case-sensitive

            // Act
            ProjectInstance projectInstance = this.CreateAndEvaluateProject(preImportProperties);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeTargetFilePath, dummySonarTargetsDir);
            AssertAnalysisTargetsAreNotImported(projectInstance);


            // 2. Now build -> succeeds
            BuildLogger logger = new BuildLogger();

            BuildResult result = BuildUtilities.BuildTargets(projectInstance, logger);

            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);
            logger.AssertTargetNotExecuted(TargetConstants.ImportBeforeInfoTarget);
            logger.AssertExpectedErrorCount(0);
        }

        [TestMethod]
        [Description("Checks what happens if the analysis targets cannot be located")]
        public void ImportsBefore_MissingAnalysisTargets()
        {
            // 1. Prebuild
            // Arrange
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = "nonExistentPath";
            preImportProperties.MSBuildExtensionsPath = "nonExistentPath";
            preImportProperties.TeamBuild2105BuildDirectory = "";
            preImportProperties.TeamBuildLegacyBuildDirectory = "";

            // Act
            ProjectInstance projectInstance = this.CreateAndEvaluateProject(preImportProperties);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeTargetsPath, @"nonExistentPath\bin\targets");
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeTargetFilePath, @"nonExistentPath\bin\targets\SonarQube.Integration.targets");

            AssertAnalysisTargetsAreNotImported(projectInstance); // Targets should not be imported


            // 2. Now build -> fails with an error message
            BuildLogger logger = new BuildLogger();

            BuildResult result = BuildUtilities.BuildTargets(projectInstance, logger);

            BuildAssertions.AssertTargetFailed(result, TargetConstants.DefaultBuildTarget);
            logger.AssertTargetExecuted(TargetConstants.ImportBeforeInfoTarget);
            logger.AssertExpectedErrorCount(1);

            string projectName = Path.GetFileName(projectInstance.FullPath);
            Assert.IsTrue(logger.Errors[0].Message.Contains(projectName), "Expecting the error message to contain the project file name");
        }

        [TestMethod]
        [Description("Checks that the targets are imported if the properties are set correctly and the targets can be found")]
        public void ImportsBefore_TargetsExist()
        {
            // 1. Pre-build
            // Arrange
            string dummySonarTargetsDir = this.EnsureDummyIntegrationTargetsFileExists();

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = Path.GetTempPath();
            preImportProperties.SonarQubeTargetsPath = Path.GetDirectoryName(dummySonarTargetsDir);
            preImportProperties.TeamBuild2105BuildDirectory = "";
            preImportProperties.TeamBuildLegacyBuildDirectory = "";

            // Act
            ProjectInstance projectInstance = this.CreateAndEvaluateProject(preImportProperties);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarQubeTargetFilePath, dummySonarTargetsDir);
            AssertAnalysisTargetsAreImported(projectInstance);


            // 2. Now build -> succeeds
            BuildLogger logger = new BuildLogger();

            BuildResult result = BuildUtilities.BuildTargets(projectInstance, logger);

            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);
            logger.AssertTargetExecuted(TargetConstants.ImportBeforeInfoTarget);
            logger.AssertExpectedErrorCount(0);
        }
        
        #endregion

        #region Private methods

        private ProjectInstance CreateAndEvaluateProject(Dictionary<string, string> preImportProperties)
        {
            // TODO: consider changing these tests to redirect where the common targets look for ImportBefore assemblies.
            // That would allow us to test the actual ImportBefore behaviour (we're currently creating a project that
            // explicitly imports our SonarQube "ImportBefore" project).
            BuildUtilities.DisableStandardTargetsWildcardImporting(preImportProperties);

            ProjectRootElement projectRoot = CreateImportsBeforeTestProject(preImportProperties);

            // Evaluate the imports
            ProjectInstance projectInstance = new ProjectInstance(projectRoot);

            SavePostEvaluationProject(projectInstance);
            return projectInstance;
        }

        /// <summary>
        /// Creates and returns a minimal project file that has imported the ImportsBefore targets file
        /// </summary>
        /// <param name="preImportProperties">Any properties that need to be set before the C# targets are imported. Can be null.</param>
        private ProjectRootElement CreateImportsBeforeTestProject(IDictionary<string, string> preImportProperties)
        {
            // Create a dummy SonarQube analysis targets file
            EnsureDummyIntegrationTargetsFileExists();

            // Locate the real "ImportsBefore" target file
            string importsBeforeTargets = Path.Combine(TestUtils.GetTestSpecificFolderName(this.TestContext), TargetConstants.ImportsBeforeFile);
            Assert.IsTrue(File.Exists(importsBeforeTargets), "Test error: the SonarQube imports before target file does not exist. Path: {0}", importsBeforeTargets);

            string projectName = this.TestContext.TestName + ".proj";
            string testSpecificFolder = TestUtils.EnsureTestSpecificFolder(this.TestContext);
            string fullProjectPath = Path.Combine(testSpecificFolder, projectName);

            ProjectRootElement root = BuildUtilities.CreateMinimalBuildableProject(preImportProperties, false /* not a VB project */, importsBeforeTargets);
            root.AddProperty(TargetProperties.ProjectGuid, Guid.NewGuid().ToString("D"));

            root.Save(fullProjectPath);
            this.TestContext.AddResultFile(fullProjectPath);

            return root;
        }

        /// <summary>
        /// Saves the project once the imports have been evaluated
        /// </summary>
        private void SavePostEvaluationProject(ProjectInstance projectInstance)
        {
            string postBuildProject = projectInstance.FullPath + ".postbuild.proj";
            projectInstance.ToProjectRootElement().Save(postBuildProject);
            this.TestContext.AddResultFile(postBuildProject);
        }

        /// <summary>
        /// Ensures that a dummy targets file with the name of the SonarQube analysis targets file exists.
        /// Return the full path to the targets file.
        /// </summary>
        private string EnsureDummyIntegrationTargetsFileExists()
        {
            // This can't just be in the TestContext.DeploymentDirectory as this will
            // be shared with other tests, and some of those tests might be deploying
            // the real analysis targets to that location.
            string testSpecificDir = TestUtils.EnsureTestSpecificFolder(this.TestContext);

            string fullPath = Path.Combine(testSpecificDir, TargetConstants.AnalysisTargetFile);
            if (!File.Exists(fullPath))
            {

// To check whether the targets are imported or not we check for
// the existence of the DummyProperty, below.
                string contents = @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <DummyProperty>123</DummyProperty>
  </PropertyGroup>
  <Target Name='DummyTarget' />
</Project>
";
                File.WriteAllText(fullPath, contents);
            }
            return fullPath;
        }

        /// <summary>
        /// Ensures a file with the correct file name exists in the correct sub-directory
        /// under the specified root
        /// </summary>
        private static string EnsureDummyAnalysisConfigFileExists(string rootBuildDir)
        {
            string subDir = Path.Combine(rootBuildDir, ".sonarqube", "conf");
            Directory.CreateDirectory(subDir);

            string fullPath = Path.Combine(subDir, FileConstants.ConfigFileName);
            File.WriteAllText(fullPath, "Dummy config file");
            return fullPath;
        }

        #endregion

        #region Assertions

        private static void AssertAnalysisTargetsAreNotImported(ProjectInstance projectInstance)
        {
            ProjectPropertyInstance propertyInstance = projectInstance.GetProperty(DummyAnalysisTargetsMarkerProperty);
            Assert.IsNull(propertyInstance, "SonarQube Analysis targets should not have been imported");
        }

        private static void AssertAnalysisTargetsAreImported(ProjectInstance projectInstance)
        {
            ProjectPropertyInstance propertyInstance = projectInstance.GetProperty(DummyAnalysisTargetsMarkerProperty);
            Assert.IsNotNull(propertyInstance, "Failed to import the SonarQube Analysis targets");
        }

        #endregion
    }
}
