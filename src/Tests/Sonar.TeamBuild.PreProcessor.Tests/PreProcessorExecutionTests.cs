//-----------------------------------------------------------------------
// <copyright file="PreProcessorExecutionTests.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sonar.Common;
using Sonar.TeamBuild.Integration;
using System;
using System.Diagnostics;
using System.IO;
using TestUtilities;

namespace Sonar.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class PreProcessorExecutionTests
    {

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PreProc_MissingCommandLineArgs()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            using (CreateValidScope("tfs uri", "build uri", testDir))
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
            using (CreateValidScope(null, "build uri", testDir))
            {
                // Act and assert
                CheckExecutionFails("key", "name", "version", "properties");
            }

            // 2. Missing build uri
            using (CreateValidScope("tfs uri", null, testDir))
            {
                // Act and assert
                CheckExecutionFails("key", "name", "version", "properties");
            }

            // 3. Missing build directory
            using (CreateValidScope("tfs uri", "build uri", null))
            {
                // Act and assert
                CheckExecutionFails("key", "name", "version", "properties");
            }

        }

        [TestMethod]
        public void PreProc_ConfigFileCreated()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string expectedConfigFileName;
            using (CreateValidScope("tfs uri", "build uri", testDir))
            {
                TeamBuildSettings settings = TeamBuildSettings.GetSettingsFromEnvironment(new ConsoleLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                expectedConfigFileName = settings.AnalysisConfigFilePath;

                // Act
                CheckExecutionSucceeds("key", "name", "version", "properties");
            }

            // Assert
            Assert.IsTrue(File.Exists(expectedConfigFileName), "Config file does not exist: {0}", expectedConfigFileName);
            AnalysisConfig config = AnalysisConfig.Load(expectedConfigFileName);
            Assert.IsTrue(Directory.Exists(config.SonarOutputDir), "Output directory was not created: {0}", config.SonarOutputDir);
            Assert.IsTrue(Directory.Exists(config.SonarConfigDir), "Config directory was not created: {0}", config.SonarConfigDir);
            Assert.AreEqual("key", config.SonarProjectKey);
            Assert.AreEqual("name", config.SonarProjectName);
            Assert.AreEqual("version", config.SonarProjectVersion);
            Assert.AreEqual("properties", config.SonarRunnerPropertiesPath);
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Creates and returns an environment scope that contains all of the required
        /// TeamBuild environment variables
        /// </summary>
        private static EnvironmentVariableScope CreateValidScope(string tfsUri, string buildUri, string buildDir)
        {
            EnvironmentVariableScope scope = new EnvironmentVariableScope();
            scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.TfsCollectionUri, tfsUri);
            scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildUri, buildUri);
            scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildDirectory, buildDir);
            return scope;
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
                FileName = typeof(Sonar.TeamBuild.PreProcessor.Program).Assembly.Location,
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
