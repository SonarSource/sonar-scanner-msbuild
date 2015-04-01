//-----------------------------------------------------------------------
// <copyright file="ArgumentProcessorTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.IO;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class ArgumentProcessorTests
    {
        private const string ActualRunnerPropertiesFileName = "sonar-runner.properties";

        private const string ValidTfsUri = "http://tfs";
        private const string ValidBuildUri = "http://build";

        public TestContext TestContext { get; set; }


        #region Tests

        [TestMethod]
        public void PreArgProc_WrongNumberOfArguments()
        {
            // 0. Setup
            TestLogger logger = new TestLogger();

            // 1. Null logger
            AssertException.Expects<ArgumentNullException>(() => ArgumentProcessor.TryProcessArgs(null, null));

            // 2. Insufficient or too many command line arguments
            using (PreprocessTestUtils.CreateValidTeamBuildScope(ValidTfsUri, ValidBuildUri, this.TestContext.TestDeploymentDir))
            {
                // Too few
                CheckProcessingFails(/* no command line args */);
                CheckProcessingFails("key");
                CheckProcessingFails("key", "name");


                // Too many
                CheckProcessingFails("key", "name", "version", "properties", "too many args");
                CheckProcessingFails("key", "name", "version", "properties", "too many args", "yet more unexpected args");
            }
        }

        [TestMethod]
        public void PreArgProc_PropertiesFileSpecifiedOnCommandLine()
        {
            // 0. Setup
            TestLogger logger = new TestLogger();

            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string propertiesFilePath = Path.Combine(testDir, "sonar-runner.properties");

            // 1. File does not exist -> args not ok
            CheckProcessingFails("key", "name", "version", propertiesFilePath);

            // 2. File exists -> args ok
            File.WriteAllText(propertiesFilePath, "# empty properties file");
            ProcessedArgs result = CheckProcessingSucceeds("key", "name", "version", propertiesFilePath);

            Assert.AreEqual("key", result.ProjectKey);
            Assert.AreEqual("name", result.ProjectName);
            Assert.AreEqual("version", result.ProjectVersion);
            Assert.AreEqual(propertiesFilePath, result.RunnerPropertiesPath);
        }
        
        [TestMethod]
        public void PreArgProc_PropertiesFileFoundViaEnvPath()
        {
            // If the properties file isn't specified on the command line then we
            // attempt to locate it relative to the sonar-runner executable.
            // This only works if the environment variable that specifies the location
            // of the sonar-runner executable is set.
            
            // 0. Setup
            TestLogger logger = new TestLogger();

            // Create the expected sonar-runner directory structure
            string sonarRootDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "runnerDir");
            string confDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "runnerDir\\conf");
            string runnerBinDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "runnerDir\\lib");

            // Create an exe and properties file within that directory structure
            string exeFilePath = Path.Combine(runnerBinDir, SonarQube.Common.FileLocator.SonarRunnerFileName);
            File.WriteAllText(exeFilePath, "dummy executable file"); // the exe file needs to exist, but it doesn't matter what it contains

            string envPropertiesFilePath = Path.Combine(confDir, ActualRunnerPropertiesFileName);

            // Create another properties file in a different directory (will be referenced explicitly on the command line)
            string cmdLinePropertiesDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "anotherDir");
            string cmdLinePropertiesFilePath = Path.Combine(runnerBinDir, "dummy.properties.txt"); // should be able to specify any file name on the command line
            File.WriteAllText(cmdLinePropertiesFilePath, "# CMD LINE empty properties file");

            ProcessedArgs result;

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetPath(runnerBinDir);
                Assert.IsFalse(string.IsNullOrWhiteSpace(FileLocator.FindDefaultSonarRunnerExecutable()), "Test setup error: failed to locate the created runner executable file");

                // 1. Not found via path (file does not exist) -> args not ok
                using (new AssertIgnoreScope()) // expecting an assert if the exe can be found but the properties can't
                {
                    CheckProcessingFails("key", "name", "version");
                }

                // 2. Found via path -> args ok
                File.WriteAllText(envPropertiesFilePath, "# ENV empty properties file"); // Create the expected file

                result = CheckProcessingSucceeds("key 1", "name 2", "version 3");

                Assert.AreEqual(envPropertiesFilePath, result.RunnerPropertiesPath);
                Assert.AreEqual("key 1", result.ProjectKey);
                Assert.AreEqual("name 2", result.ProjectName);
                Assert.AreEqual("version 3", result.ProjectVersion);

                // 3. Command line arg should override path -> args ok
                result = CheckProcessingSucceeds("key 1", "name 2", "version 3", cmdLinePropertiesFilePath);
                Assert.AreEqual(cmdLinePropertiesFilePath, result.RunnerPropertiesPath);
            }
        }

        #endregion

        #region Private methods

        private static void CheckProcessingFails(params string[] commandLineArgs)
        {
            TestLogger logger = new TestLogger();

            ProcessedArgs result = ArgumentProcessor.TryProcessArgs(commandLineArgs, logger);

            Assert.IsNull(result, "Not expecting the arguments to be processed succesfully");
            logger.AssertErrorsLogged();

        }

        private static ProcessedArgs CheckProcessingSucceeds(params string[] commandLineArgs)
        {
            TestLogger logger = new TestLogger();
            ProcessedArgs result = null;

            result = ArgumentProcessor.TryProcessArgs(commandLineArgs, logger);

            Assert.IsNotNull(result, "Expecting the arguments to be processed succesfully");
            
            logger.AssertErrorsLogged(0);
            
            return result;
        }

        #endregion
    }
}
