//-----------------------------------------------------------------------
// <copyright file="TestUtils.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace TestUtilities
{
    public static class TestUtils
    {
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

        #endregion

        #region Private methods

        private static string DoCreateTestSpecificFolder(TestContext testContext, string optionalSubDirName, bool throwIfExists)
        {
            string fullPath = Path.Combine(testContext.TestDeploymentDir, testContext.TestName);
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

        #endregion

    }
}
