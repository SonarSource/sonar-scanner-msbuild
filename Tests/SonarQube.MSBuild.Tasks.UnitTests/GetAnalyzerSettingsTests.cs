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

using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.UnitTests
{
    [TestClass]
    public class GetAnalyzerSettingsTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void GetAnalyzerSettings_MissingConfigDir_NoError()
        {
            // Arrange
            GetAnalyzerSettings testSubject = new GetAnalyzerSettings();
            testSubject.AnalysisConfigDir = "c:\\missing";

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            CheckNoAnalyzerSettings(testSubject);
        }

        [TestMethod]
        public void GetAnalyzerSettings_MissingConfigFile_NoError()
        {
            // Arrange
            GetAnalyzerSettings testSubject = new GetAnalyzerSettings();
            testSubject.AnalysisConfigDir = this.TestContext.DeploymentDirectory;

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            CheckNoAnalyzerSettings(testSubject);
        }

        [TestMethod]
        public void GetAnalyzerSettings_ConfigExistsButNoAnalyzerSettings_NoError()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            GetAnalyzerSettings testSubject = new GetAnalyzerSettings();

            AnalysisConfig config = new AnalysisConfig();
            string fullPath = Path.Combine(testDir, FileConstants.ConfigFileName);
            config.Save(fullPath);

            testSubject.AnalysisConfigDir = testDir;

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            CheckNoAnalyzerSettings(testSubject);
        }

        [TestMethod]
        public void GetAnalyzerSettings_ConfigExists_DataReturned()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            GetAnalyzerSettings testSubject = new GetAnalyzerSettings();

            string[] expectedAnalyzers = new string[] { "c:\\analyzer1.DLL", "c:\\analyzer2.dll" };
            string[] expectedAdditionalFiles = new string[] { "c:\\add1.txt", "d:\\add2.txt" };

            // SONARMSBRU-216: non-assembly files should be filtered out
            List<string> filesInConfig = new List<string>(expectedAnalyzers);
            filesInConfig.Add("c:\\not_an_assembly.exe");
            filesInConfig.Add("c:\\not_an_assembly.zip");
            filesInConfig.Add("c:\\not_an_assembly.txt");
            filesInConfig.Add("c:\\not_an_assembly.dll.foo");
            filesInConfig.Add("c:\\not_an_assembly.winmd");

            AnalysisConfig config = new AnalysisConfig();
            config.AnalyzersSettings = new List<AnalyzerSettings>();
            
            AnalyzerSettings settings = new AnalyzerSettings();
            settings.Language = "my lang";
            settings.RuleSetFilePath = "f:\\yyy.ruleset";
            settings.AnalyzerAssemblyPaths = filesInConfig;
            settings.AdditionalFilePaths = expectedAdditionalFiles.ToList();
            config.AnalyzersSettings.Add(settings);

            AnalyzerSettings anotherSettings = new AnalyzerSettings();
            anotherSettings.Language = "cobol";
            anotherSettings.RuleSetFilePath = "f:\\xxx.ruleset";
            anotherSettings.AnalyzerAssemblyPaths = filesInConfig;
            anotherSettings.AdditionalFilePaths = expectedAdditionalFiles.ToList();
            config.AnalyzersSettings.Add(anotherSettings);

            string fullPath = Path.Combine(testDir, FileConstants.ConfigFileName);
            config.Save(fullPath);

            testSubject.AnalysisConfigDir = testDir;
            testSubject.Language = "my lang";

            // Act
            ExecuteAndCheckSuccess(testSubject);

            // Assert
            Assert.AreEqual("f:\\yyy.ruleset", testSubject.RuleSetFilePath);
            CollectionAssert.AreEquivalent(expectedAnalyzers, testSubject.AnalyzerFilePaths);
            CollectionAssert.AreEquivalent(expectedAdditionalFiles, testSubject.AdditionalFiles);
        }

        #endregion

        #region Checks methods

        private static void ExecuteAndCheckSuccess(Task task)
        {
            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            task.BuildEngine = dummyEngine;

            bool taskSucess = task.Execute();
            Assert.IsTrue(taskSucess, "Expecting the task to succeed");
            dummyEngine.AssertNoErrors();
            dummyEngine.AssertNoWarnings();
        }

        private static void CheckNoAnalyzerSettings(GetAnalyzerSettings executedTask)
        {
            Assert.IsNull(executedTask.RuleSetFilePath);
            Assert.IsNull(executedTask.AdditionalFiles);
            Assert.IsNull(executedTask.AnalyzerFilePaths);
        }

        #endregion
    }
}
