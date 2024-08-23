/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;

namespace TestUtilities;

public static class TestUtils
{
    // Target file names
    public const string AnalysisTargetFile = "SonarQube.Integration.targets";

    public const string ImportsBeforeFile = "SonarQube.Integration.ImportBefore.targets";

    /// <summary>
    /// Test class + Test name --> Test directory. Used to prevent creating multiple directories for the same test
    /// in case CreateTestSpecificFolder is called multiple times.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> testDirectoriesMap =
        new ConcurrentDictionary<string, string>();

    #region Public methods

    /// <summary>
    /// Creates a new folder specific to the current test and returns the
    /// full path to the new folder. This method will return the same path
    /// if called multiple times from within the same test.
    /// </summary>
    public static string CreateTestSpecificFolderWithSubPaths(TestContext testContext, params string[] subDirNames)
    {
        var fullPath = CreateTestSpecificFolder(testContext);
        if (subDirNames.Length > 0)
        {
            fullPath = Path.Combine(new[] { fullPath }.Concat(subDirNames).ToArray());
            Directory.CreateDirectory(fullPath);
        }

        return fullPath;
    }

    /// <summary>
    /// Creates a new text file in the specified directory
    /// </summary>
    /// <param name="substitutionArgs">Optional. Arguments that will be substituted into <param name="content">.</param></param>
    /// <returns>Returns the full path to the created file</returns>
    public static string CreateTextFile(string parentDir, string fileName, string content, params string[] substitutionArgs)
    {
        Directory.Exists(parentDir).Should().BeTrue("Test setup error: expecting the parent directory to exist: {0}", parentDir);
        var fullPath = Path.Combine(parentDir, fileName);

        var formattedContent = content;
        if (substitutionArgs != null && substitutionArgs.Length > 0)
        {
            formattedContent = string.Format(System.Globalization.CultureInfo.InvariantCulture, content, substitutionArgs);
        }

        File.WriteAllText(fullPath, formattedContent);
        return fullPath;
    }

    /// <summary>
    /// Deletes a file in the specified directory
    /// </summary>
    public static void DeleteTextFile(string parentDir, string fileName)
    {
        Directory.Exists(parentDir).Should().BeTrue("Test setup error: expecting the parent directory to exist: {0}", parentDir);
        var fullPath = Path.Combine(parentDir, fileName);
        File.Delete(fullPath);

        File.Exists(fullPath).Should().BeFalse("File still exists.");
    }

    /// <summary>
    /// Ensures that the ImportBefore targets exist in a test-specific folder
    /// </summary>
    public static string EnsureImportBeforeTargetsExists(TestContext testContext)
    {
        var filePath = Path.Combine(CreateTestSpecificFolder(testContext), ImportsBeforeFile);
        if (File.Exists(filePath))
        {
            testContext.WriteLine("ImportBefore target file already exists: {0}", filePath);
        }
        else
        {
            testContext.WriteLine("Extracting ImportBefore target file to {0}", filePath);
            CreateTestSpecificFolder(testContext);
            ExtractResourceToFile("TestUtilities.Embedded.SonarQube.Integration.ImportBefore.targets", filePath);
        }
        return filePath;
    }

    /// <summary>
    /// Ensures the analysis targets exist in a test-specific folder
    /// </summary>
    public static string EnsureAnalysisTargetsExists(TestContext testContext)
    {
        var filePath = Path.Combine(CreateTestSpecificFolder(testContext), AnalysisTargetFile);
        if (File.Exists(filePath))
        {
            testContext.WriteLine("Analysis target file already exists: {0}", filePath);
        }
        else
        {
            testContext.WriteLine("Extracting analysis target file to {0}", filePath);
            CreateTestSpecificFolder(testContext);
            ExtractResourceToFile("TestUtilities.Embedded.SonarQube.Integration.targets", filePath);
        }
        return filePath;
    }

    /// <summary>
    /// Ensures the default properties file exists in the specified folder
    /// </summary>
    public static string EnsureDefaultPropertiesFileExists(string targetDir, TestContext testContext)
    {
        var filePath = Path.Combine(targetDir, FilePropertyProvider.DefaultFileName);
        if (File.Exists(filePath))
        {
            testContext.WriteLine("Default properties file already exists: {0}", filePath);
        }
        else
        {
            testContext.WriteLine("Extracting default properties file to {0}", filePath);
            Directory.CreateDirectory(targetDir);
            ExtractResourceToFile("TestUtilities.Embedded.SonarQube.Analysis.xml", filePath);
        }
        return filePath;
    }

    public static string CreateValidEmptyRuleset(string parentDir, string fileNameWithoutExtension) =>
       CreateTextFile(parentDir, fileNameWithoutExtension + ".ruleset", @"<?xml version='1.0' encoding='utf-8'?>
<RuleSet xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' Name='x' Description='x' ToolsVersion='14.0'>
</RuleSet>");

    /// <summary>
    /// Creates a batch file with the name of the current test
    /// </summary>
    /// <returns>Returns the full file name of the new file</returns>
    public static string WriteBatchFileForTest(TestContext context, string content)
    {
        var testPath = CreateTestSpecificFolder(context);
        var fileName = Path.Combine(testPath, context.TestName + ".bat");
        File.Exists(fileName).Should().BeFalse("Not expecting a batch file to already exist: {0}", fileName);
        File.WriteAllText(fileName, content);
        return fileName;
    }

    /// <summary>
    /// Creates a project info under the specified analysis root directory
    /// together with the supporting project and content files, along with GUID and additional properties (if specified)
    /// </summary>
    public static string CreateProjectWithFiles(
        TestContext testContext,
        string projectName,
        string projectLanguage,
        string analysisRootPath,
        Guid projectGuid,
        bool createContentFiles = true,
        AnalysisProperties additionalProperties = null)
    {
        // Create a project with content files in a new subdirectory
        var projectDir = CreateTestSpecificFolderWithSubPaths(testContext, Path.Combine("projects", projectName));
        var projectFilePath = Path.Combine(projectDir, Path.ChangeExtension(projectName, "proj"));
        CreateEmptyFile(projectDir, Path.ChangeExtension(projectName, "proj"));

        // Create a project info file in the correct location under the analysis root
        var contentProjectInfo = CreateProjectInfoInSubDir(
            analysisRootPath,
            projectName,
            projectLanguage,
            projectGuid,
            ProjectType.Product,
            false,
            projectFilePath,
            "UTF-8",
            additionalProperties); // not excluded

        // Create content / managed files if required
        if (createContentFiles)
        {
            var contentFile = CreateEmptyFile(projectDir, "contentFile1.txt");
            var contentFileList = CreateFile(projectDir, "contentList.txt", contentFile);
            AddAnalysisResult(contentProjectInfo, AnalysisType.FilesToAnalyze, contentFileList);
        }

        return contentProjectInfo;
    }

    /// <summary>
    /// Creates a project info under the specified analysis root directory
    /// together with the supporting project and content files, along with additional properties (if specified)
    /// </summary>
    public static string CreateProjectWithFiles(TestContext testContext, string projectName, string analysisRootPath, bool createContentFiles = true, AnalysisProperties additionalProperties = null)
    {
        return CreateProjectWithFiles(testContext, projectName, null, analysisRootPath, Guid.NewGuid(), createContentFiles, additionalProperties);
    }

    public static string CreateEmptyFile(string parentDir, string fileName)
    {
        return CreateFile(parentDir, fileName, string.Empty);
    }

    public static string CreateFile(string parentDir, string fileName, string content)
    {
        var fullPath = Path.Combine(parentDir, fileName);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    /// <summary>
    /// Creates a new project info file in a new subdirectory with the given additional properties.
    /// </summary>
    public static string CreateProjectInfoInSubDir(
        string parentDir,
        string projectName,
        string projectLanguage,
        Guid projectGuid,
        ProjectType projectType,
        bool isExcluded,
        string fullProjectPath,
        string encoding,
        AnalysisProperties additionalProperties = null)
    {
        var newDir = Path.Combine(parentDir, projectName);
        Directory.CreateDirectory(newDir); // ensure the directory exists

        var project = new ProjectInfo()
        {
            FullPath = fullProjectPath,
            ProjectName = projectName,
            ProjectLanguage = projectLanguage,
            ProjectGuid = projectGuid,
            ProjectType = projectType,
            IsExcluded = isExcluded,
            Encoding = encoding
        };

        if (additionalProperties != null)
        {
            project.AnalysisSettings = additionalProperties;
        }

        var filePath = Path.Combine(newDir, FileConstants.ProjectInfoFileName);
        project.Save(filePath);
        return filePath;
    }

    public static void AddAnalysisResult(string projectInfoFile, AnalysisType resultType, string location)
    {
        var projectInfo = ProjectInfo.Load(projectInfoFile);
        projectInfo.AddAnalyzerResult(resultType, location);
        projectInfo.Save(projectInfoFile);
    }

    #endregion Public methods

    #region Private methods

    private static string CreateTestSpecificFolder(TestContext testContext) =>
        testDirectoriesMap.GetOrAdd(
            testContext.FullyQualifiedTestClassName + testContext.TestName,
            testName =>
            {
                var uniqueDir = UniqueDirectory.CreateNext(testContext.TestDir);
                // Save the unique directory name into a file to improve the debugging experience.
                File.AppendAllText(Path.Combine(testContext.TestDir, "testmap.txt"), $"{testContext.TestName} : {uniqueDir}{Environment.NewLine}");
                return Path.Combine(testContext.TestDir, uniqueDir);
            });

    private static void ExtractResourceToFile(string resourceName, string filePath)
    {
        var stream = typeof(TestUtils).Assembly.GetManifestResourceStream(resourceName);
        using (var reader =  new StreamReader(stream))
        {
            File.WriteAllText(filePath, reader.ReadToEnd());
        }
    }

    #endregion Private methods
}
