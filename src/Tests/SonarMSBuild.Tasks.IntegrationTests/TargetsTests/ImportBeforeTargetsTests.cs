//-----------------------------------------------------------------------
// <copyright file="ImportBeforeTargetsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using TestUtilities;

namespace Sonar.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    [DeploymentItem("LinkedFiles\\Sonar.Integration.ImportBefore.targets")]
    public class ImportBeforeTargetsTests
    {
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Name of the property to check for to determine whether or not
        /// the targets have been imported or not
        /// </summary>
        private const string DummyAnalysisTargetsMarkerProperty = "DummyProperty";

        #region Tests

        [TestMethod]
        [Description("Checks the properties are not set if RunSonarAnalysis is missing")]
        public void ImportsBefore_RunSonarAnalysisNotSet()
        {
            // Arrange
            string dummySonarTargetsDir = this.EnsureDummySonarIntegrationTargetsFileExists();

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarBinPath = Path.GetDirectoryName(dummySonarTargetsDir);

            // Act
            ProjectInstance projectInstance = this.CreateAndEvaluateProject(preImportProperties);

            // Assert
            BuildAssertions.AssertPropertyDoesNotExist(projectInstance, TargetProperties.SonarTargetFilePath);
            AssertAnalysisTargetsAreNotImported(projectInstance);
        }

        [TestMethod]
        [Description("Checks what happens if the targets cannot be located")]
        public void ImportsBefore_MissingTargets()
        {
            // Arrange
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarAnalysis = "true";
            preImportProperties.MSBuildExtensionsPath = "nonExistentPath";

            // Act
            ProjectInstance projectInstance = this.CreateAndEvaluateProject(preImportProperties);

            // Assert
            BuildAssertions.AssertPropertyExists(projectInstance, TargetProperties.SonarTargetsPath);
            BuildAssertions.AssertPropertyExists(projectInstance, TargetProperties.SonarTargetFilePath);

            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarTargetsPath, @"nonExistentPath\SonarQube");
            AssertAnalysisTargetsAreNotImported(projectInstance); // Targets do not exist at that location so they should not be imported
        }

        [TestMethod]
        [DeploymentItem("LinkedFiles\\Sonar.Integration.ImportBefore.targets")]
        [Description("Checks that the targets are imported if the properties are set correctly and the targets can be found")]
        public void ImportsBefore_TargetsExist()
        {
            // Arrange
            string dummySonarTargetsDir = this.EnsureDummySonarIntegrationTargetsFileExists();

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.RunSonarAnalysis = "true";
            preImportProperties.SonarBinPath = Path.GetDirectoryName(dummySonarTargetsDir);

            // Act
            ProjectInstance projectInstance = this.CreateAndEvaluateProject(preImportProperties);

            // Assert
            BuildAssertions.AssertExpectedPropertyValue(projectInstance, TargetProperties.SonarTargetFilePath, dummySonarTargetsDir);
            AssertAnalysisTargetsAreImported(projectInstance);
        }


        #endregion

        #region Private methods

        private ProjectInstance CreateAndEvaluateProject(Dictionary<string, string> preImportProperties)
        {
            // Disable the normal ImportAfter/Import before behaviour to prevent any additional
            // targets from being picked up.
            // See the Microsoft Common targets for more info e.g. C:\Program Files (x86)\MSBuild\12.0\Bin\Microsoft.Common.CurrentVersion.targets
            // TODO: consider changing these tests to redirect where the common targets look for ImportBefore assemblies.
            // That would allow us to test the actual ImportBefore behaviour (we're currently creating a project that
            // explicitly imports our SonarQube "ImportBefore" project).
            preImportProperties.Add("ImportByWildcardBeforeMicrosoftCommonTargets", "false");
            preImportProperties.Add("ImportByWildcardAfterMicrosoftCommonTargets", "false");

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
            string dummyAnalysisTargets = EnsureDummySonarIntegrationTargetsFileExists();

            // Locate the real "ImportsBefore" target file
            string importsBeforeTargets = Path.Combine(this.TestContext.TestDeploymentDir, TargetConstants.SonarImportsBeforeFile);
            Assert.IsTrue(File.Exists(importsBeforeTargets), "Test error: the SonarQube imports before target file does not exist. Path: {0}", importsBeforeTargets);

            string projectName = this.TestContext.TestName + ".proj";
            string testSpecificFolder = TestUtils.EnsureTestSpecificFolder(this.TestContext);
            string fullProjectPath = Path.Combine(testSpecificFolder, projectName);

            ProjectRootElement root = BuildUtilities.CreateMinimalBuildableProject(preImportProperties, importsBeforeTargets);
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
        private string EnsureDummySonarIntegrationTargetsFileExists()
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
