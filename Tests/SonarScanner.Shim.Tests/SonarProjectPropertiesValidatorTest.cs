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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.IO;
using System.Collections.Generic;
using TestUtilities;

namespace SonarScanner.Shim.Tests
{
    #region Tests

    [TestClass]
    public class SonarProjectPropertiesValidatorTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void SonarProjectPropertiesValidatorTest_FailCurrentDirectory()
        {
            var folder = TestUtils.CreateTestSpecificFolder(TestContext);
            File.Create(Path.Combine(folder, "sonar-project.properties"));

            bool called = false;
            SonarProjectPropertiesValidator.Validate(
                folder, new Dictionary<ProjectInfo, ProjectInfoValidity>(),
                onValid: () => Assert.Fail("expected validation to fail"),
                onInvalid: (paths) =>
                {
                    called = true;
                    Assert.AreEqual(1, paths.Count);
                    Assert.AreEqual(folder, paths[0]);
                });
            Assert.IsTrue(called, "Callback not called");
        }

        [TestMethod]
        public void SonarProjectPropertiesValidatorTest_FailProjectDirectory()
        {
            var folder = TestUtils.CreateTestSpecificFolder(TestContext);

            var p1 = MockProject(folder, "Project1");
            var p2 = MockProject(folder, "Project2");
            var p3 = MockProject(folder, "Project3");

            File.Create(Path.Combine(Path.GetDirectoryName(p1.FullPath), "sonar-project.properties"));
            File.Create(Path.Combine(Path.GetDirectoryName(p3.FullPath), "sonar-project.properties"));

            var projects = new Dictionary<ProjectInfo, ProjectInfoValidity>();
            projects[p1] = ProjectInfoValidity.Valid;
            projects[p2] = ProjectInfoValidity.Valid;
            projects[p3] = ProjectInfoValidity.Valid;

            bool called = false;
            SonarProjectPropertiesValidator.Validate(
                folder, projects,
                onValid: () => Assert.Fail("expected validation to fail"),
                onInvalid: (paths) =>
                {
                    called = true;
                    Assert.AreEqual(2, paths.Count);
                    Assert.AreEqual(Path.GetDirectoryName(p1.FullPath), paths[0]);
                    Assert.AreEqual(Path.GetDirectoryName(p3.FullPath), paths[1]);
                });
            Assert.IsTrue(called, "Callback not called");
        }

        [TestMethod]
        public void SonarProjectPropertiesValidatorTest_SucceedAndSkipInvalidProjects()
        {
            var folder = TestUtils.CreateTestSpecificFolder(TestContext);

            var p1 = MockProject(folder, "Project1");
            var p2 = MockProject(folder, "Project2");
            var p3 = MockProject(folder, "Project3");
            var p4 = MockProject(folder, "Project4");
            var p5 = MockProject(folder, "Project5");

            File.Create(Path.Combine(Path.GetDirectoryName(p2.FullPath), "sonar-project.properties"));
            File.Create(Path.Combine(Path.GetDirectoryName(p3.FullPath), "sonar-project.properties"));
            File.Create(Path.Combine(Path.GetDirectoryName(p4.FullPath), "sonar-project.properties"));
            File.Create(Path.Combine(Path.GetDirectoryName(p5.FullPath), "sonar-project.properties"));

            var projects = new Dictionary<ProjectInfo, ProjectInfoValidity>();
            projects[p1] = ProjectInfoValidity.Valid;
            projects[p2] = ProjectInfoValidity.DuplicateGuid;
            projects[p3] = ProjectInfoValidity.ExcludeFlagSet;
            projects[p4] = ProjectInfoValidity.InvalidGuid;
            projects[p5] = ProjectInfoValidity.NoFilesToAnalyze;

            bool called = false;
            SonarProjectPropertiesValidator.Validate(
                folder, projects,
                onValid: () => called = true,
                onInvalid: (paths) =>
                {
                    Assert.Fail("Expected to succeed");
                });
            Assert.IsTrue(called, "Callback not called");
        }

        #endregion

        #region Private methods

        private ProjectInfo MockProject(string folder, string projectName)
        {
            var projectFolder = Path.Combine(folder, projectName);
            Directory.CreateDirectory(projectFolder);
            ProjectInfo project = new ProjectInfo();
            project.FullPath = Path.Combine(projectFolder, projectName + ".csproj");
            return project;
        }

        #endregion
    }
}
