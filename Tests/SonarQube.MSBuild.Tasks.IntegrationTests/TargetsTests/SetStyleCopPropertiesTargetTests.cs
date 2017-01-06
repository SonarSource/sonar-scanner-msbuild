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
using System.Collections.Generic;
using System.Linq;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.IntegrationTests.TargetsTests
{
    [TestClass]
    public class SetStyleCopPropertiesTargetTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [Description("Checks the target is not executed if the temp folder is not set")]
        public void StyleCop_TempFolderIsNotSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = "";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.SetStyleCopSettingsTarget);

            // Assert
            logger.AssertTargetNotExecuted(TargetConstants.SetStyleCopSettingsTarget);
        }

        [TestMethod]
        [Description("Checks the target is executed if the temp folder has been provided")]
        public void StyleCop_TempFolderIsSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.SonarQubeOutputPath = rootInputFolder;

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.SetStyleCopSettingsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.SetStyleCopSettingsTarget);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.SetStyleCopSettingsTarget);
        }

        [TestMethod]
        [Description("Checks the targets are executed in the expected order")]
        public void StyleCop_TargetExecutionOrder()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.SonarQubeOutputPath = rootInputFolder;
            properties.SonarQubeConfigPath = rootOutputFolder;

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            // Add some settings we expect to be ignored
            AddAnalysisSetting("sonar.other.setting", "other value", projectRoot);
            AddAnalysisSetting("sonar.other.setting.2", "other value 2", projectRoot);
            projectRoot.Save();

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.DefaultBuildTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.SetStyleCopSettingsTarget);
            logger.AssertExpectedTargetOrdering(TargetConstants.SetStyleCopSettingsTarget, TargetConstants.WriteProjectDataTarget);

            AssertExpectedStyleCopSetting(projectRoot.ProjectFileLocation.File, result);
        }

        [TestMethod]
        [Description("Checks the item value will not be overridden if it is already set")]
        public void StyleCop_ValueAlreadySet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties properties = new WellKnownProjectProperties();
            properties.SonarQubeTempPath = rootInputFolder;
            properties.SonarQubeOutputPath = rootInputFolder;
            properties.SonarQubeConfigPath = rootOutputFolder;

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, properties);

            // Apply some SonarQubeSettings, one of which specifies the StyleCop setting
            AddAnalysisSetting("sonar.other.setting", "other value", projectRoot);
            AddAnalysisSetting(TargetConstants.StyleCopProjectPathItemName, "xxx.yyy", projectRoot);
            AddAnalysisSetting("sonar.other.setting.2", "other value 2", projectRoot);
            projectRoot.Save();

            BuildLogger logger = new BuildLogger();
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger, TargetConstants.SetStyleCopSettingsTarget);

            // Assert
            logger.AssertTargetExecuted(TargetConstants.SetStyleCopSettingsTarget);
            AssertExpectedStyleCopSetting("xxx.yyy", result);
        }

        #endregion

        #region Private methods

        private static void AddAnalysisSetting(string name, string value, ProjectRootElement project)
        {
            ProjectItemElement element = project.AddItem(BuildTaskConstants.SettingItemName, name);
            element.AddMetadata(BuildTaskConstants.SettingValueMetadataName, value);
        }

        #endregion

        #region Checks

        private static void AssertExpectedStyleCopSetting(string expectedValue, BuildResult actualResult)
        {
            BuildAssertions.AssertExpectedAnalysisSetting(actualResult, TargetConstants.StyleCopProjectPathItemName, expectedValue);
        }

        #endregion
    }
}
