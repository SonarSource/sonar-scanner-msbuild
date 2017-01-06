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
using System;
using System.IO;
using System.Linq;

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
            string fullPath = DoCreateTestSpecificFolder(testContext, optionalSubDirNames, throwIfExists: true);
            return fullPath;
        }

        /// <summary>
        /// Ensures that a new folder specific to the current test exists and returns the
        /// full path to the new folder.
        /// </summary>
        public static string EnsureTestSpecificFolder(TestContext testContext, params string[] optionalSubDirNames)
        {
            string fullPath = DoCreateTestSpecificFolder(testContext, optionalSubDirNames, throwIfExists: false);
            return fullPath;
        }

        /// <summary>
        /// Creates a new text file in the specified directory
        /// </summary>
        /// <param name="substitutionArgs">Optional. Arguments that will be substituted into <param name="content">.</param></param>
        /// <returns>Returns the full path to the created file</returns>
        public static string CreateTextFile(string parentDir, string fileName, string content, params string[] substitutionArgs)
        {
            Assert.IsTrue(Directory.Exists(parentDir), "Test setup error: expecting the parent directory to exist: {0}", parentDir);
            string fullPath = Path.Combine(parentDir, fileName);

            string formattedContent = content;
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
            string filePath = Path.Combine(GetTestSpecificFolderName(testContext), ImportsBeforeFile);
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
            string filePath = Path.Combine(GetTestSpecificFolderName(testContext), AnalysisTargetFile);
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
            string filePath = Path.Combine(targetDir, SonarQube.Common.FilePropertyProvider.DefaultFileName);
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

        public static string GetTestSpecificFolderName(TestContext testContext)
        {
            string fullPath = Path.Combine(testContext.DeploymentDirectory, testContext.TestName);
            return fullPath;
        }

        /// <summary>
        /// Creates a batch file with the name of the current test
        /// </summary>
        /// <returns>Returns the full file name of the new file</returns>
        public static string WriteBatchFileForTest(TestContext context, string content)
        {
            string fileName = Path.Combine(context.DeploymentDirectory, context.TestName + ".bat");
            Assert.IsFalse(File.Exists(fileName), "Not expecting a batch file to already exist: {0}", fileName);
            File.WriteAllText(fileName, content);
            return fileName;
        }


        #endregion

        #region Private methods

        private static string DoCreateTestSpecificFolder(TestContext testContext, string[] optionalSubDirNames, bool throwIfExists)
        {
            string fullPath = GetTestSpecificFolderName(testContext);
            if (optionalSubDirNames != null &&
                optionalSubDirNames.Any())
            {
                fullPath = Path.Combine(new[] { fullPath }.Concat(optionalSubDirNames).ToArray());
            }

            bool exists = Directory.Exists(fullPath);

            if (exists)
            {
                if (throwIfExists)
                {
                    Assert.Fail("Test-specific test folder should not already exist: {0}", fullPath);
                }
            }
            else
            {
                Directory.CreateDirectory(fullPath);
            }

            return fullPath;
        }

        private static void ExtractResourceToFile(string resourceName, string filePath)
        {
            Stream stream = typeof(TestUtils).Assembly.GetManifestResourceStream(resourceName);
            using(StreamReader reader =  new StreamReader(stream))
            {
                File.WriteAllText(filePath, reader.ReadToEnd());
            }
        }

        #endregion
    }
}
