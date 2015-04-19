//-----------------------------------------------------------------------
// <copyright file="TestUtils.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

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
        public static string CreateTestSpecificFolder(TestContext testContext, string optionalSubDirName = "")
        {
            string fullPath = DoCreateTestSpecificFolder(testContext, optionalSubDirName, throwIfExists: true);
            return fullPath;
        }

        /// <summary>
        /// Ensures that a new folder specific to the current test exists and returns the
        /// full path to the new folder.
        /// </summary>
        public static string EnsureTestSpecificFolder(TestContext testContext, string optionalSubDirName = "")
        {
            string fullPath = DoCreateTestSpecificFolder(testContext, optionalSubDirName, throwIfExists: false);
            return fullPath;
        }

        /// <summary>
        /// Ensures that the ImportBefore targets exist in the specified folder
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
        /// Ensures the analysis targets exist in the specified folder
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

        public static string GetTestSpecificFolderName(TestContext testContext)
        {
            string fullPath = Path.Combine(testContext.DeploymentDirectory, testContext.TestName);
            return fullPath;
        }

        #endregion

        #region Private methods

        private static string DoCreateTestSpecificFolder(TestContext testContext, string optionalSubDirName, bool throwIfExists)
        {
            string fullPath = GetTestSpecificFolderName(testContext);
            if (!string.IsNullOrEmpty(optionalSubDirName))
            {
                fullPath = Path.Combine(fullPath, optionalSubDirName);
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
