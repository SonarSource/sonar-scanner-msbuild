/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
 * mailto: info AT sonarsource DOT com
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

using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Tasks.UnitTest;

[TestClass]
public class IsTestFileByNameTests
{
    public TestContext TestContext { get; set; }

    #region Tests

    [TestMethod]
    public void IsTestFile_NoRegex()
    {
        // Arrange
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        // Check file names
        CheckFilePathIsNotTest(testFolder, "test"); // file name alone, no extension
        CheckFilePathIsNotTest(testFolder, "test.csproj"); // file name alone
        CheckFilePathIsNotTest(testFolder, "proj.test"); // ".test" extension
        CheckFilePathIsNotTest(testFolder, "proj.AtestB"); // part of the extension

        CheckFilePathIsNotTest(testFolder, "..\\..\\abc\\test.csproj"); // with relative path
        CheckFilePathIsNotTest(testFolder, "f:\\abc\\test.csproj"); // with absolute path
        CheckFilePathIsNotTest(testFolder, "d:\\abc\\TEST.csproj"); // case-sensitivity
        CheckFilePathIsNotTest(testFolder, "d:\\abc\\Another.test.vbproj"); // training "test"
        CheckFilePathIsNotTest(testFolder, "d:\\abc\\test.foo.proj"); // leading "test"
        CheckFilePathIsNotTest(testFolder, "d:\\abc\\XXXTesTyyy.proj"); // contained "test"

        CheckFilePathIsNotTest(testFolder, "c:\\aFile.csproj"); // doesn't contain "test"
        CheckFilePathIsNotTest(testFolder, "c:\\notATesFile.csproj"); // doesn't contain "test"

        // Check for directory called "test"
        CheckFilePathIsNotTest(testFolder, "c:\\test\\my.csproj");
        CheckFilePathIsNotTest(testFolder, "c:\\aaa\\test\\bbb\\my.csproj"); // embedded in path
        CheckFilePathIsNotTest(testFolder, "..\\test\\bbb\\my.csproj"); // relative
        CheckFilePathIsNotTest(testFolder, ".\\TesT\\bbb\\my.csproj"); // case-sensitivity

        CheckFilePathIsNotTest(testFolder, "..\\Atest\\a.b"); // prefixed
        CheckFilePathIsNotTest(testFolder, "..\\testX\\a.b"); // suffixed
        CheckFilePathIsNotTest(testFolder, "..\\XXXtestYYY\\a.b"); // suffixed

        // Check for directory called "tests"
        CheckFilePathIsNotTest(testFolder, "c:\\tests\\my.csproj");
        CheckFilePathIsNotTest(testFolder, "c:\\aaa\\tests\\bbb\\my.csproj"); // embedded in path
        CheckFilePathIsNotTest(testFolder, "..\\tests\\bbb\\my.csproj"); // relative
        CheckFilePathIsNotTest(testFolder, ".\\TesTs\\bbb\\my.csproj"); // case-sensitivity

        CheckFilePathIsNotTest(testFolder, "..\\Atests\\a.b"); // prefixed
        CheckFilePathIsNotTest(testFolder, "..\\testsX\\a.b"); // suffixed
        CheckFilePathIsNotTest(testFolder, "..\\XXXtestsYYY\\a.b"); // suffixed

        // By default, "Test" in the project name doesn't indicate a test without explicit configuration
        CheckFilePathIsNotTest(testFolder, @"Test.csproj");
        CheckFilePathIsNotTest(testFolder, @"Test.vbproj");
        CheckFilePathIsNotTest(testFolder, @"Tests.csproj");
        CheckFilePathIsNotTest(testFolder, @"Tests.vbproj");
        CheckFilePathIsNotTest(testFolder, @"C:\Foo\MyProject.Test.csproj");
        CheckFilePathIsNotTest(testFolder, @"C:\Foo\MyProject.Test.vbproj");
        CheckFilePathIsNotTest(testFolder, @"C:\Foo\MyProject.Tests.csproj");
        CheckFilePathIsNotTest(testFolder, @"C:\Foo\MyProject.Tests.vbproj");
        CheckFilePathIsNotTest(testFolder, @"C:\Foo\MyProject.UnitTest.csproj");
        CheckFilePathIsNotTest(testFolder, @"C:\Foo\MyProject.UnitTest.vbproj");
        CheckFilePathIsNotTest(testFolder, @"C:\Foo\MyProject.UnitTests.csproj");
        CheckFilePathIsNotTest(testFolder, @"C:\Foo\MyProject.UnitTests.vbproj");

        // Doesn't end with "Test"
        CheckFilePathIsNotTest(testFolder, @"C:\Foo\TestMyProject.csproj");
        CheckFilePathIsNotTest(testFolder, @"C:\Foo\TestMyProject.vbproj");
        CheckFilePathIsNotTest(testFolder, @"C:\Tests\MyProject.csproj");
        CheckFilePathIsNotTest(testFolder, @"C:\Tests\MyProject.vbproj");

        // Case mismatch
        CheckFilePathIsNotTest(testFolder, @"Cutest.csproj");
        CheckFilePathIsNotTest(testFolder, @"Cutest.vbproj");
        CheckFilePathIsNotTest(testFolder, @"C:\Foo\MyProject.Unittest.csproj");
        CheckFilePathIsNotTest(testFolder, @"C:\Foo\MyProject.Unittest.vbproj");

        // Not expected extension
        CheckFilePathIsNotTest(testFolder, @"Test.proj");
    }

    [TestMethod]
    public void IsTestFile_InvalidRegexInConfig()
    {
        // Arrange
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var invalidRegEx = "Invalid regex ((";
        EnsureAnalysisConfig(testFolder, invalidRegEx);

        var dummyEngine = new DummyBuildEngine();
        var task = new IsTestFileByName
        {
            BuildEngine = dummyEngine,
            FullFilePath = "Path",
            AnalysisConfigDir = testFolder
        };

        var result = task.Execute();

        result.Should().BeFalse("Expecting the task to fail");
        dummyEngine.AssertSingleErrorExists(invalidRegEx); // expecting the invalid expression to appear in the error
    }

    [TestMethod] // Regression test for bug http://jira.codehaus.org/browse/SONARMSBRU-11
    public void IsTestFile_TimeoutIfConfigLocked_TaskFails()
    {
        // Arrange
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var configFile = EnsureAnalysisConfig(testFolder, ".XX.");

        var dummyEngine = new DummyBuildEngine();
        var task = new IsTestFileByName
        {
            BuildEngine = dummyEngine,
            FullFilePath = "XXX.proj",
            AnalysisConfigDir = testFolder
        };

        var result = true;
        TaskUtilitiesTests.PerformOpOnLockedFile(configFile, () => result = task.Execute(), shouldTimeoutReadingConfig: true);

        result.Should().BeFalse("Expecting the task to fail if the config file could not be read");
        dummyEngine.AssertNoWarnings();
        dummyEngine.AssertSingleErrorExists();
    }

    [TestMethod]
    public void IsTestFile_RegExFromConfig()
    {
        // 0. Setup
        var testFolder = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        // 1a. Check the config setting is used if valid
        EnsureAnalysisConfig(testFolder, ".*aProject.*");
        CheckFilePathIsNotTest(testFolder, "c:\\test\\mytest.proj");
        CheckFilePathIsTest(testFolder, "c:\\aProject.proj");

        // 1b. Check another config valid config setting
        EnsureAnalysisConfig(testFolder, ".*\\\\test\\\\.*");
        CheckFilePathIsTest(testFolder, "c:\\test\\mytest.proj");
        CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");

        // 2. Check the default is used if the setting is missing
        EnsureAnalysisConfig(testFolder, null);
        CheckFilePathIsNotTest(testFolder, "c:\\test\\mytest.proj");
        CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");

        // 3a. Check the default is used if the setting is empty
        EnsureAnalysisConfig(testFolder, "");
        CheckFilePathIsNotTest(testFolder, "c:\\test\\mytest.proj");
        CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");

        // 3b. Check the default is used if the setting contains only whitespaces
        EnsureAnalysisConfig(testFolder, " ");
        CheckFilePathIsNotTest(testFolder, "c:\\test\\mytest.proj");
        CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");
    }

    #endregion Tests

    #region Private methods

    /// <summary>
    /// Ensures an analysis config file exists in the specified directory,
    /// replacing one if it already exists.
    /// If the supplied "regExExpression" is not null then the appropriate setting
    /// entry will be created in the file
    /// </summary>
    private static string EnsureAnalysisConfig(string parentDir, string regExExpression)
    {
        var config = new AnalysisConfig();
        if (regExExpression != null)
        {
            config.LocalSettings = new AnalysisProperties { new(IsTestFileByName.TestRegExSettingId, regExExpression) };
        }

        var fullPath = Path.Combine(parentDir, FileConstants.ConfigFileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        config.Save(fullPath);
        return fullPath;
    }

    #endregion Private methods

    #region Checks

    private static void CheckFilePathIsTest(string analysisDir, string fullFileName)
    {
        var isTest = ExecuteAndCheckSuccess(analysisDir, fullFileName);
        isTest.Should().BeTrue("Expecting the file name to be recognized as a test file. Name: {0}", fullFileName);
    }

    private static void CheckFilePathIsNotTest(string analysisDir, string fullFileName)
    {
        var isTest = ExecuteAndCheckSuccess(analysisDir, fullFileName);
        isTest.Should().BeFalse("Not expecting the file name to be recognized as a test file. Name: {0}", fullFileName);
    }

    private static bool ExecuteAndCheckSuccess(string analysisDir, string fullFileName)
    {
        var dummyEngine = new DummyBuildEngine();
        var task = new IsTestFileByName
        {
            BuildEngine = dummyEngine,
            FullFilePath = fullFileName,
            AnalysisConfigDir = analysisDir
        };

        var taskSucess = task.Execute();
        taskSucess.Should().BeTrue("Expecting the task to succeed");
        dummyEngine.AssertNoErrors();
        dummyEngine.AssertNoWarnings();

        return task.IsTest;
    }

    #endregion Checks
}
