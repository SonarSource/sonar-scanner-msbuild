//-----------------------------------------------------------------------
// <copyright file="PreProcessorExeTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System;
using System.Diagnostics;
using System.IO;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class PreProcessorExeTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PreProc_MissingCommandLineArgs()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            using (PreprocessTestUtils.CreateValidTeamBuildScope("tfs uri", "build uri", testDir))
            {
                // Act and assert
                CheckExecutionFails();
                CheckExecutionFails("key");
                CheckExecutionFails("key", "name");
                CheckExecutionFails("key", "name", "version");
            }
        }

        [TestMethod]
        public void PreProc_MissingEnvVars()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            
            // 1. Missing tfs uri
            using (PreprocessTestUtils.CreateValidTeamBuildScope(null, "build uri", testDir))
            {
                // Act and assert
                CheckExecutionFails("key", "name", "version", "properties");
            }

            // 2. Missing build uri
            using (PreprocessTestUtils.CreateValidTeamBuildScope("tfs uri", null, testDir))
            {
                // Act and assert
                CheckExecutionFails("key", "name", "version", "properties");
            }

            // 3. Missing build directory
            using (PreprocessTestUtils.CreateValidTeamBuildScope("tfs uri", "build uri", null))
            {
                // Act and assert
                CheckExecutionFails("key", "name", "version", "properties");
            }

        }

        #endregion

        #region Checks

        private static void CheckExecutionFails(params string[] args)
        {
            Process p = Execute(args);

            Assert.AreNotEqual(0, p.ExitCode, "Expecting a non-zero exit code");
            string errorOutput = p.StandardError.ReadToEnd();
            Console.WriteLine("Error output: {0}", errorOutput);

            Assert.IsFalse(string.IsNullOrWhiteSpace(errorOutput), "Expecting error output if the process fails");
        }

        private static void CheckExecutionSucceeds(params string[] args)
        {
            Process p = Execute(args);

            Assert.AreEqual(0, p.ExitCode, "Expecting a zero exit code");
            string errorOutput = p.StandardError.ReadToEnd();
            Console.WriteLine("Error output: {0}", errorOutput);

            Assert.IsTrue(string.IsNullOrWhiteSpace(errorOutput), "Not expecting error output if the process succeeds");
        }

        private static Process Execute(string[] args)
        {
            ProcessStartInfo psi = new ProcessStartInfo()
            {
                FileName = typeof(SonarQube.TeamBuild.PreProcessor.Program).Assembly.Location,
                RedirectStandardError = true,
                UseShellExecute = false, // required if we want to capture the error output
                ErrorDialog = false,
                Arguments = string.Join(" ", args)
            };

            Process p = Process.Start(psi);
            p.WaitForExit(1000);
            Assert.IsTrue(p.HasExited, "Timed out waiting for the process to exit");
            return p;
        }

        #endregion
    }
}
