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

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.UnitTests
{
    [TestClass]
    public class WriteProjectInfoFileTests
    {
        private const string ExpectedProjectInfoFileName = "ProjectInfo.xml";

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [Description("Tests that the project info file is created when the task is executed")]
        public void WriteProjectInfoFile_FileCreated()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid projectGuid = Guid.NewGuid();

            WriteProjectInfoFile task = new WriteProjectInfoFile();
            task.FullProjectPath = "c:\\fullPath\\project.proj";
            task.ProjectLanguage = "cs";
            task.IsTest = true;
            task.IsExcluded = false;
            task.OutputFolder = testFolder;
            task.ProjectGuid = projectGuid.ToString("B");
            task.ProjectName = "MyProject";
            task.ProjectLanguage = ProjectLanguages.CSharp;
            // No analysis results are supplied

            // Act
            ProjectInfo reloadedProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);

            // Addition assertions
            ProjectInfoAssertions.AssertExpectedValues(
                "c:\\fullPath\\project.proj",
                ProjectLanguages.CSharp,
                ProjectType.Test,
                projectGuid,
                "MyProject",
                false, // IsExcluded
                reloadedProjectInfo);
        }

        [TestMethod]
        [Description("Tests that analysis results are correctly handled")]
        public void WriteProjectInfoFile_AnalysisResults()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid projectGuid = Guid.NewGuid();

            WriteProjectInfoFile task = new WriteProjectInfoFile();
            task.BuildEngine = new DummyBuildEngine();
            task.FullProjectPath = "x:\\a.csproj";
            task.IsTest = false;
            task.OutputFolder = testFolder;
            task.ProjectGuid = projectGuid.ToString("B");
            task.ProjectName = "MyProject";
            task.ProjectLanguage = "C#";

            List<ITaskItem> resultInputs = new List<ITaskItem>();

            // Add invalid task items
            // Note: the TaskItem class won't allow the item spec or metadata values to be null,
            // so we aren't testing those
            resultInputs.Add(CreateMetadataItem("itemSpec1", "abc", "def")); // Id field is missing
            resultInputs.Add(CreateAnalysisResultTaskItem("\r", "should be ignored - whitespace")); // whitespace id
            resultInputs.Add(CreateAnalysisResultTaskItem("should be ignored - whitespace", " ")); // whitespace location

            // Add valid task items
            resultInputs.Add(CreateAnalysisResultTaskItem("id1", "location1"));
            resultInputs.Add(CreateAnalysisResultTaskItem("id2", "location2", "md1", "md1 value", "md2", "md2 value")); // valid but with extra metadata

            task.AnalysisResults = resultInputs.ToArray();

            // Act
            ProjectInfo createdProjectInfo;
            using (new AssertIgnoreScope()) // We've deliberately created task items with unexpected item names that will cause assertions
            {
                createdProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);
            }

            // Assert
            AssertAnalysisResultExists(createdProjectInfo, "id1", "location1");
            AssertAnalysisResultExists(createdProjectInfo, "id2", "location2");
            AssertExpectedAnalysisResultCount(2, createdProjectInfo);
        }

        [TestMethod]
        [Description("Tests that analysis settings are correctly handled")]
        public void WriteProjectInfoFile_AnalysisSettings()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid projectGuid = Guid.NewGuid();

            WriteProjectInfoFile task = new WriteProjectInfoFile();

            DummyBuildEngine buildEngine = new DummyBuildEngine();
            task.BuildEngine = buildEngine;
            task.FullProjectPath = "x:\\analysisSettings.csproj";
            task.IsTest = false;
            task.OutputFolder = testFolder;
            task.ProjectGuid = projectGuid.ToString("B");
            task.ProjectName = "MyProject";
            task.ProjectLanguage = "C#";

            // Example of a valid setting:
            // <SonarQubeSetting Include="sonar.resharper.projectname">
            //    <Value>C:\zzz\reportlocation.xxx</Value>
            // </SonarQubeSetting>

            List<ITaskItem> settingsInputs = new List<ITaskItem>();

            // Add invalid task items
            // Note: the TaskItem class won't allow the item spec or metadata values to be null,
            // so we aren't testing those
            settingsInputs.Add(CreateMetadataItem("invalid.missing.value.metadata", "NotValueMetadata", "missing value 1")); // value field is missing
            settingsInputs.Add(CreateAnalysisSettingTaskItem(" ", "should be ignored - key is whitespace only")); // whitespace key
            settingsInputs.Add(CreateAnalysisSettingTaskItem("invalid spaces in key", "spaces.in.key")); // spaces in key
            settingsInputs.Add(CreateAnalysisSettingTaskItem(" invalid.key.has.leading.whitespace", "leading whitespace in key"));
            settingsInputs.Add(CreateAnalysisSettingTaskItem("invalid.key.has.trailing.whitespace ", "trailing whitespace in key"));
            settingsInputs.Add(CreateAnalysisSettingTaskItem(".invalid.non.alpha.first.character", "non alpha first character"));

            // Add valid task items
            settingsInputs.Add(CreateAnalysisSettingTaskItem("valid.setting.1", @"c:\dir1\dir2\file.txt"));
            settingsInputs.Add(CreateAnalysisSettingTaskItem("valid.value.is.whitespace.only", " "));
            settingsInputs.Add(CreateMetadataItem("valid.metadata.name.is.case.insensitive", BuildTaskConstants.SettingValueMetadataName.ToUpperInvariant(), "uppercase metadata name")); // metadata name is in the wrong case
            settingsInputs.Add(CreateAnalysisSettingTaskItem("valid.value.has.whitespace", "valid setting with whitespace"));
            settingsInputs.Add(CreateAnalysisSettingTaskItem("X", "single character key"));
            settingsInputs.Add(CreateAnalysisSettingTaskItem("Y...", "single character followed by periods"));
            settingsInputs.Add(CreateAnalysisSettingTaskItem("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.sonar.projectName", "guid followed by key"));

            task.AnalysisSettings = settingsInputs.ToArray();

            // Act
            ProjectInfo createdProjectInfo;
            createdProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);

            // Assert
            buildEngine.AssertSingleWarningExists("invalid.missing.value.metadata");
            // Can't easily check for the message complaining against the empty key
            buildEngine.AssertSingleWarningExists("invalid spaces in key");
            buildEngine.AssertSingleWarningExists(" invalid.key.has.leading.whitespace");
            buildEngine.AssertSingleWarningExists("invalid.key.has.trailing.whitespace ");
            buildEngine.AssertSingleWarningExists(".invalid.non.alpha.first.character");

            AssertAnalysisSettingExists(createdProjectInfo, "valid.setting.1", @"c:\dir1\dir2\file.txt");
            AssertAnalysisSettingExists(createdProjectInfo, "valid.value.is.whitespace.only", null);
            AssertAnalysisSettingExists(createdProjectInfo, "valid.value.has.whitespace", "valid setting with whitespace");
            AssertAnalysisSettingExists(createdProjectInfo, "valid.metadata.name.is.case.insensitive", "uppercase metadata name");
            AssertAnalysisSettingExists(createdProjectInfo, "X", "single character key");
            AssertAnalysisSettingExists(createdProjectInfo, "Y...", "single character followed by periods");
            AssertAnalysisSettingExists(createdProjectInfo, "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.sonar.projectName", "guid followed by key");

            AssertExpectedAnalysisSettingsCount(7, createdProjectInfo);
        }

        [TestMethod]
        [Description("Tests that the project info file is not created if a project guid is not supplied")]
        [WorkItem(50)] // Regression test for Bug 50:MSBuild projects with missing ProjectGuids cause the build to fail
        public void WriteProjectInfoFile_MissingProjectGuid()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            WriteProjectInfoFile task = new WriteProjectInfoFile();
            task.FullProjectPath = "c:\\fullPath\\project.proj";
            task.IsTest = true;
            task.OutputFolder = testFolder;
            task.ProjectName = "ProjectWithoutProjectGuid";
            task.ProjectLanguage = "C#";
            // No analysis results are supplied

            // Act
            DummyBuildEngine engine = new DummyBuildEngine();
            task.BuildEngine = engine;
            bool success = task.Execute();

            // Assert
            Assert.IsTrue(success, "Not expecting the task to fail as this would fail the build");
            engine.AssertNoErrors();
            Assert.AreEqual(1, engine.Warnings.Count, "Expecting a build warning as the ProjectGuid is missing");

            BuildWarningEventArgs firstWarning = engine.Warnings[0];
            Assert.IsNotNull(firstWarning.Message, "Warning message should not be null");

            string projectInfoFilePath = Path.Combine(testFolder, ExpectedProjectInfoFileName);
            Assert.IsFalse(File.Exists(projectInfoFilePath), "Not expecting the project info file to have been created");
        }

        [TestMethod]
        [Description("Tests that the project info file is created using solution Guid if a project guid is not supplied")]
        public void WriteProjectInfoFile_UseSolutionProjectGuid()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid projectGuid = Guid.NewGuid();

            WriteProjectInfoFile task = new WriteProjectInfoFile();
            task.FullProjectPath = "c:\\fullPath\\project.proj";
            task.SolutionConfigurationContents = @"<SolutionConfiguration>
                <ProjectConfiguration Project=""{FOO}"" AbsolutePath=""c:\fullPath\foo.proj"" BuildProjectInSolution=""True""> Debug | AnyCPU </ProjectConfiguration>
                <ProjectConfiguration Project=""{"+ projectGuid + @"}"" AbsolutePath=""c:\fullPath\project.proj"" BuildProjectInSolution=""True""> Debug | AnyCPU </ProjectConfiguration>
               </SolutionConfiguration >";
            task.IsTest = true;
            task.OutputFolder = testFolder;
            task.ProjectName = "ProjectWithoutProjectGuid";
            task.ProjectLanguage = "C#";

            // Act
            ProjectInfo reloadedProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);

            // Addition assertions
            ProjectInfoAssertions.AssertExpectedValues(
                "c:\\fullPath\\project.proj",
                ProjectLanguages.CSharp,
                ProjectType.Test,
                projectGuid,
                "ProjectWithoutProjectGuid",
                false, // IsExcluded
                reloadedProjectInfo);
        }

        [TestMethod]
        [Description("Tests that project info files are created for unrecognised languages")]
        public void WriteProjectInfoFile_UnrecognisedLanguages()
        {
            // Arrange
            WriteProjectInfoFile task = new WriteProjectInfoFile();
            task.FullProjectPath = "c:\\fullPath\\project.proj";
            task.IsTest = true;
            task.ProjectName = "UnrecognisedLanguageProject";
            task.ProjectGuid = Guid.NewGuid().ToString("B");

            // 1. Null language
            task.ProjectLanguage = null;
            task.OutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "null.language");

            ProjectInfo actual = ExecuteAndCheckSucceeds(task, task.OutputFolder);
            Assert.IsNull(actual.ProjectLanguage, "Expecting the language to be null");

            // 2. Unrecognized language
            task.ProjectLanguage = "unrecognized language";
            task.OutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "unrecog.language");

            actual = ExecuteAndCheckSucceeds(task, task.OutputFolder);
            Assert.AreEqual("unrecognized language", actual.ProjectLanguage, "Unexpected value for project language");
        }

        [TestMethod]
        public void WriteProjectInfoFile_Execute_WhenCodePageAndWhateverProjectType_ExpectsGivenEncoding()
        {
            // Arrange
            var expectedEncoding = Encoding.GetEncoding(1141);
            var encodingProvider = new TestEncodingProvider(
                encoding =>
                {
                    if (encoding == expectedEncoding.CodePage)
                    {
                        return expectedEncoding;
                    }
                    else
                    {
                        Assert.Fail("should not have been called");
                        throw new InvalidOperationException();
                    }
                },
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                });

            // Act
            var actual = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, expectedEncoding.CodePage, "whatever", "foo");

            // Assert
            Assert.AreEqual(actual.Encoding, expectedEncoding.WebName, "unexpected encoding");
        }

        [TestMethod]
        public void WriteProjectInfoFile_Execute_WhenCSharpWithNullCodePageAndDefaultSystemEncodingExists_ExpectsDefaultSystemEncoding()
        {
            // Arrange
            var expectedEncoding = Encoding.GetEncoding(1141);
            var encodingProvider = new TestEncodingProvider(
                encoding =>
                {
                    if (encoding == 0) // default system encoding
                    {
                        return expectedEncoding;
                    }
                    else
                    {
                        Assert.Fail("should not have been called");
                        throw new InvalidOperationException();
                    }
                },
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                });

            // Act
            var actual = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, null, "C#", "foo2");

            // Assert
            Assert.AreEqual(actual.Encoding, expectedEncoding.WebName, "unexpected encoding");
        }

        [TestMethod]
        public void WriteProjectInfoFile_Execute_WhenCSharpVBNetWithInferiorToZeroCodePageAndDefaultSystemEncodingExists_ExpectsDefaultSystemEncoding()
        {
            // Arrange
            var expectedEncoding = Encoding.GetEncoding(1141);
            const decimal codePage = -1;
            var encodingProvider = new TestEncodingProvider(
                encoding =>
                {
                    if (encoding == 0) // default system encoding
                    {
                        return expectedEncoding;
                    }
                    else
                    {
                        Assert.Fail("should not have been called");
                        throw new InvalidOperationException();
                    }
                },
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                });

            // Act
            var actualCSharp = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "C#", "foo31");
            var actualVBNet = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "VB", "foo32");

            // Assert
            Assert.AreEqual(actualCSharp.Encoding, expectedEncoding.WebName, "unexpected encoding");
            Assert.AreEqual(actualVBNet.Encoding, expectedEncoding.WebName, "unexpected encoding");
        }

        [TestMethod]
        public void WriteProjectInfoFile_Execute_WhenCSharpVBNetWithBiggerThanLongCodePageAndDefaultSystemEncodingExists_ExpectsDefaultSystemEncoding()
        {
            // Arrange
            var expectedEncoding = Encoding.GetEncoding(1141);
            var encodingProvider = new TestEncodingProvider(
                encoding =>
                {
                    if (encoding == 0) // default system encoding
                    {
                        return expectedEncoding;
                    }
                    else
                    {
                        Assert.Fail("should not have been called");
                        throw new InvalidOperationException();
                    }
                },
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                });

            var codePage = (decimal)long.MaxValue + 1;

            // Act
            var actualCSharp = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "C#", "foo41");
            var actualVBNet = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "VB", "foo42");

            // Assert
            Assert.AreEqual(actualCSharp.Encoding, expectedEncoding.WebName, "unexpected encoding");
            Assert.AreEqual(actualVBNet.Encoding, expectedEncoding.WebName, "unexpected encoding");
        }

        [TestMethod]
        public void WriteProjectInfoFile_Execute_WhenCSharpVBNetWithNonExistingCodePageAndDefaultSystemEncodingExists_ExpectsDefaultSystemEncoding()
        {
            // Arrange
            var expectedEncoding = Encoding.GetEncoding(1141);
            const decimal codePage = 42;
            var encodingProvider = new TestEncodingProvider(
                encoding =>
                {
                    if (encoding == codePage)
                    {
                        throw new Exception("expected exception");
                    }
                    else if (encoding == 0) // default system encoding
                    {
                        return expectedEncoding;
                    }
                    else
                    {
                        Assert.Fail("should not have been called");
                        throw new InvalidOperationException();
                    }
                },
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                });

            // Act
            var actualCSharp = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "C#", "foo51");
            var actualVBNet = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "VB", "foo52");

            // Assert
            Assert.AreEqual(actualCSharp.Encoding, expectedEncoding.WebName, "unexpected encoding");
            Assert.AreEqual(actualVBNet.Encoding, expectedEncoding.WebName, "unexpected encoding");
        }

        [TestMethod]
        public void WriteProjectInfoFile_Execute_WhenCSharpVBNetWithNullCodePageAndDefaultSystemEncodingReturnsNull_ExpectsEncoding1252()
        {
            // Arrange
            var expectedEncoding = Encoding.GetEncoding(1252);
            var encodingProvider = new TestEncodingProvider(
                encoding =>
                {
                    if (encoding == 0) // default system encoding
                    {
                        return null;
                    }
                    else if (encoding == expectedEncoding.CodePage)
                    {
                        return expectedEncoding;
                    }
                    else
                    {
                        Assert.Fail("should not have been called");
                        throw new InvalidOperationException();
                    }
                },
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                });

            // Act
            var actualCSharp = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, null, "C#", "foo61");
            var actualVBNet = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, null, "VB", "foo62");

            // Assert
            Assert.AreEqual(actualCSharp.Encoding, expectedEncoding.WebName, "unexpected encoding");
            Assert.AreEqual(actualVBNet.Encoding, expectedEncoding.WebName, "unexpected encoding");
        }

        [TestMethod]
        public void WriteProjectInfoFile_Execute_WhenCSharpVBNetWithNullCodePageDefaultSEReturnsNullEncoding1252ThrowsNSEx_ExpectsLatin1()
        {
            // Arrange
            var expectedEncoding = Encoding.GetEncoding("Latin1");
            decimal? codePage = null;
            var encodingProvider = new TestEncodingProvider(
                encoding =>
                {
                    if (encoding == 0) // default system encoding
                    {
                        return null;
                    }
                    else if (encoding == 1252) // windows encoding
                    {
                        throw new NotSupportedException("expected exception");
                    }
                    else
                    {
                        Assert.Fail("should not have been called");
                        throw new InvalidOperationException();
                    }
                },
                x =>
                {
                    return expectedEncoding;
                });

            // Act
            var actualCSharp = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "C#", "foo71");
            var actualVBNet = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "VB", "foo72");

            // Assert
            Assert.AreEqual(actualCSharp.Encoding, expectedEncoding.WebName, "unexpected encoding");
            Assert.AreEqual(actualVBNet.Encoding, expectedEncoding.WebName, "unexpected encoding");
        }


        [TestMethod]
        public void WriteProjectInfoFile_Execute_WhenCSharpVBNetWithNullCodePageDefaultSystemEncodingReturnsNullAndEncoding1252ThrowsException_ExpectsNull()
        {
            // Arrange
            decimal? codePage = null;
            var encodingProvider = new TestEncodingProvider(
                encoding =>
                {
                    if (encoding == 0) // default system encoding
                    {
                        return null;
                    }
                    else if (encoding == 1252) // windows encoding
                    {
                        throw new Exception("expected exception");
                    }
                    else
                    {
                        Assert.Fail("should not have been called");
                        throw new InvalidOperationException();
                    }
                },
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                });

            // Act
            var actualCSharp = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "C#", "foo81");
            var actualVBNet = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "VB", "foo82");

            // Assert
            Assert.AreEqual(actualCSharp.Encoding, null, "unexpected encoding");
            Assert.AreEqual(actualVBNet.Encoding, null, "unexpected encoding");
        }

        [TestMethod]
        public void WriteProjectInfoFile_Execute_WhenNotCSharpWithNullCodePage_ExpectsNull()
        {
            // Arrange
            decimal? codePage = null;
            var encodingProvider = new TestEncodingProvider(
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                },
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                });

            // Act
            var actual = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "whatever", "foo9");

            // Assert
            Assert.AreEqual(actual.Encoding, null, "unexpected encoding");
        }

        [TestMethod]
        public void WriteProjectInfoFile_Execute_WhenNotCSharpVBNetWithInferiorToZeroCodePage_ExpectsNull()
        {
            // Arrange
            const decimal codePage = -1;
            var encodingProvider = new TestEncodingProvider(
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                },
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                });

            // Act
            var actual = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "whatever", "foo10");

            // Assert
            Assert.AreEqual(actual.Encoding, null, "unexpected encoding");
        }

        [TestMethod]
        public void WriteProjectInfoFile_Execute_WhenNotCSharpVBNetWithBiggerThanLongCodePage_ExpectsNull()
        {
            // Arrange
            var encodingProvider = new TestEncodingProvider(
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                },
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                });

            var codePage = (decimal)long.MaxValue + 1;

            // Act
            var actual = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "whatever", "foo11");

            // Assert
            Assert.AreEqual(actual.Encoding, null, "unexpected encoding");
        }

        [TestMethod]
        public void WriteProjectInfoFile_Execute_WhenNotCSharpVBNetWithNonExistingCodePage_ExpectsNull()
        {
            // Arrange
            const int codePage = 42;
            var encodingProvider = new TestEncodingProvider(
                x =>
                {
                    if (x == codePage)
                    {
                        throw new Exception("expected exception");
                    }
                    else
                    {
                        Assert.Fail("should not have been called");
                        throw new InvalidOperationException();
                    }
                },
                x =>
                {
                    Assert.Fail("should not have been called");
                    throw new InvalidOperationException();
                });

            // Act
            var actual = WriteProjectInfoFile_ExecuteAndReturn(encodingProvider, codePage, "whatever", "foo12");

            // Assert
            Assert.AreEqual(actual.Encoding, null, "unexpected encoding");
        }


        private ProjectInfo WriteProjectInfoFile_ExecuteAndReturn(IEncodingProvider encodingProvider, decimal? codePage, string projectLanguage, string folderName)
        {
            // Arrange
            WriteProjectInfoFile task = new WriteProjectInfoFile(encodingProvider);
            task.FullProjectPath = "c:\\fullPath\\project.proj";
            task.IsTest = true;
            task.ProjectName = "Foo";
            task.ProjectGuid = Guid.NewGuid().ToString("B");
            task.ProjectLanguage = projectLanguage;
            task.CodePage = codePage.ToString();
            task.OutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, folderName);

            // Act
            return ExecuteAndCheckSucceeds(task, task.OutputFolder);
        }

        #endregion

        #region Helper methods

        private static ITaskItem CreateAnalysisResultTaskItem(string id, string location, params string[] idAndValuePairs)
        {
            ITaskItem item = CreateMetadataItem(location, idAndValuePairs);
            item.SetMetadata(BuildTaskConstants.ResultMetadataIdProperty, id);
            return item;
        }

        private static ITaskItem CreateAnalysisSettingTaskItem(string key, string value)
        {
            ITaskItem item = new TaskItem(key);
            item.SetMetadata(BuildTaskConstants.SettingValueMetadataName, value);
            return item;
        }

        private static ITaskItem CreateMetadataItem(string itemSpec, params string[] idAndValuePairs)
        {
            ITaskItem item = new TaskItem(itemSpec);
            int remainder;
            Math.DivRem(idAndValuePairs.Length, 2, out remainder);
            Assert.AreEqual(0, remainder, "Test setup error: the supplied list should contain id-location pairs");

            for (int index = 0; index < idAndValuePairs.Length; index += 2)
            {
                item.SetMetadata(idAndValuePairs[index], idAndValuePairs[index + 1]);
            }
            return item;
        }

        #endregion

        #region Checks

        private ProjectInfo ExecuteAndCheckSucceeds(Task task, string testFolder)
        {
            string expectedOutputFile = Path.Combine(testFolder, ExpectedProjectInfoFileName);
            Assert.IsFalse(File.Exists(expectedOutputFile), "Test error: output file should not exist before the task is executed");

            bool result = task.Execute();

            Assert.IsTrue(result, "Expecting the task execution to succeed");
            Assert.IsTrue(File.Exists(expectedOutputFile), "Expected output file was not created by the task. Expected: {0}", expectedOutputFile);
            this.TestContext.AddResultFile(expectedOutputFile);

            ProjectInfo reloadedProjectInfo = ProjectInfo.Load(expectedOutputFile);
            Assert.IsNotNull(reloadedProjectInfo, "Not expecting the reloaded project info file to be null");
            return reloadedProjectInfo;
        }

        private static void AssertAnalysisResultExists(ProjectInfo actual, string expectedId, string expectedLocation)
        {
            Assert.IsNotNull(actual, "Supplied project info should not be null");
            Assert.IsNotNull(actual.AnalysisResults, "AnalysisResults should not be null");

            AnalysisResult result = actual.AnalysisResults.FirstOrDefault(ar => expectedId.Equals(ar.Id, StringComparison.InvariantCulture));
            Assert.IsNotNull(result, "AnalysisResult with the expected id does not exist. Id: {0}", expectedId);

            Assert.AreEqual(expectedLocation, result.Location, "Analysis result does not have the expected location");
        }

        private static void AssertExpectedAnalysisResultCount(int count, ProjectInfo actual)
        {
            Assert.IsNotNull(actual, "Supplied project info should not be null");
            Assert.IsNotNull(actual.AnalysisResults, "AnalysisResults should not be null");

            Assert.AreEqual(count, actual.AnalysisResults.Count, "Unexpected number of AnalysisResult items");
        }

        private static void AssertAnalysisSettingExists(ProjectInfo actual, string expectedId, string expectedValue)
        {
            Assert.IsNotNull(actual, "Supplied project info should not be null");
            Assert.IsNotNull(actual.AnalysisSettings, "AnalysisSettings should not be null");

            Property setting = actual.AnalysisSettings.FirstOrDefault(ar => expectedId.Equals(ar.Id, StringComparison.InvariantCulture));
            Assert.IsNotNull(setting, "AnalysisSetting with the expected id does not exist. Id: {0}", expectedId);

            Assert.AreEqual(expectedValue, setting.Value, "Setting does not have the expected value");
        }

        private static void AssertExpectedAnalysisSettingsCount(int count, ProjectInfo actual)
        {
            Assert.IsNotNull(actual, "Supplied project info should not be null");
            Assert.IsNotNull(actual.AnalysisSettings, "AnalysisSettings should not be null");

            Assert.AreEqual(count, actual.AnalysisSettings.Count, "Unexpected number of AnalysisSettings items");
        }

        #endregion
    }
}
