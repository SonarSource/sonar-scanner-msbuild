/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities
{
    public static class TestUtils
    {
        // Target file names
        public const string AnalysisTargetFile = "SonarQube.Integration.targets";

        public const string ImportsBeforeFile = "SonarQube.Integration.ImportBefore.targets";

        #region Public methods

        /// <summary>
        /// Creates a new folder specific to the current test and returns the
        /// full path to the new folder.
        /// Throws if a test-specific folder already exists.
        /// </summary>
        public static string CreateTestSpecificFolder(TestContext testContext, params string[] optionalSubDirNames)
        {
            var fullPath = DoCreateTestSpecificFolder(testContext, optionalSubDirNames, throwIfExists: true);
            return fullPath;
        }

        /// <summary>
        /// Ensures that a new folder specific to the current test exists and returns the
        /// full path to the new folder.
        /// </summary>
        public static string EnsureTestSpecificFolder(TestContext testContext, params string[] optionalSubDirNames)
        {
            var fullPath = DoCreateTestSpecificFolder(testContext, optionalSubDirNames, throwIfExists: false);
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
        /// Ensures that the ImportBefore targets exist in a test-specific folder
        /// </summary>
        public static string EnsureImportBeforeTargetsExists(TestContext testContext)
        {
            var filePath = Path.Combine(GetTestSpecificFolderName(testContext), ImportsBeforeFile);
            if (File.Exists(filePath))
            {
                testContext.WriteLine("ImportBefore target file already exists: {0}", filePath);
            }
            else
            {
                testContext.WriteLine("Extracting ImportBefore target file to {0}", filePath);
                EnsureTestSpecificFolder(testContext);
                ExtractResourceToFile("TestUtilities.Embedded.SonarQube.Integration.ImportBefore.targets", filePath);
            }
            return filePath;
        }

        /// <summary>
        /// Ensures the analysis targets exist in a test-specific folder
        /// </summary>
        public static string EnsureAnalysisTargetsExists(TestContext testContext)
        {
            var filePath = Path.Combine(GetTestSpecificFolderName(testContext), AnalysisTargetFile);
            if (File.Exists(filePath))
            {
                testContext.WriteLine("Analysis target file already exists: {0}", filePath);
            }
            else
            {
                testContext.WriteLine("Extracting analysis target file to {0}", filePath);
                EnsureTestSpecificFolder(testContext);
                ExtractResourceToFile("TestUtilities.Embedded.SonarQube.Integration.targets", filePath);
            }
            return filePath;
        }

        /// <summary>
        /// Ensures the default properties file exists in the specified folder
        /// </summary>
        public static string EnsureDefaultPropertiesFileExists(string targetDir, TestContext testContext)
        {
            var filePath = Path.Combine(targetDir, SonarScanner.MSBuild.Common.FilePropertyProvider.DefaultFileName);
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

        public static string GetTestSpecificFolderName(TestContext testContext)
        {
            var fullPath = Path.Combine(testContext.DeploymentDirectory, testContext.TestName);
            return fullPath;
        }

        /// <summary>
        /// Creates a batch file with the name of the current test
        /// </summary>
        /// <returns>Returns the full file name of the new file</returns>
        public static string WriteBatchFileForTest(TestContext context, string content)
        {
            var fileName = Path.Combine(context.DeploymentDirectory, context.TestName + ".bat");
            File.Exists(fileName).Should().BeFalse("Not expecting a batch file to already exist: {0}", fileName);
            File.WriteAllText(fileName, content);
            return fileName;
        }

        #endregion Public methods

        #region Private methods

        private static string DoCreateTestSpecificFolder(TestContext testContext, string[] optionalSubDirNames, bool throwIfExists)
        {
            var fullPath = GetTestSpecificFolderName(testContext);
            if (optionalSubDirNames != null &&
                optionalSubDirNames.Any())
            {
                fullPath = Path.Combine(new[] { fullPath }.Concat(optionalSubDirNames).ToArray());
            }

            var exists = Directory.Exists(fullPath);

            if (exists)
            {
                throwIfExists.Should().BeFalse("Test-specific test folder should not already exist: {0}", fullPath);
            }
            else
            {
                Directory.CreateDirectory(fullPath);
            }

            return fullPath;
        }

        private static void ExtractResourceToFile(string resourceName, string filePath)
        {
            var stream = typeof(TestUtils).Assembly.GetManifestResourceStream(resourceName);
            using(var reader =  new StreamReader(stream))
            {
                File.WriteAllText(filePath, reader.ReadToEnd());
            }
        }

        #endregion Private methods
    }
}
