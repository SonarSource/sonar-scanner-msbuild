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

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PreArgProc_MissingArguments()
        {
            // 0. Setup
            TestLogger logger;

            // 1. Null logger
            AssertException.Expects<ArgumentNullException>(() => ArgumentProcessor.TryProcessArgs(null, null));

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                CreateRunnerFilesInScope(scope);

                // 2. All required arguments missing
                logger = CheckProcessingFails(/* no command line args */);
                logger.AssertSingleErrorExists("/key:"); // we expect errors with info about the missing required parameters, which should include the primary alias
                logger.AssertSingleErrorExists("/name:");
                logger.AssertSingleErrorExists("/version:");
                logger.AssertErrorDoesNotExist("/runner:");
                logger.AssertErrorsLogged(3);

                // 3. Some required arguments missing
                logger = CheckProcessingFails("/k:key", "/v:version");

                logger.AssertErrorDoesNotExist("/key:");
                logger.AssertErrorDoesNotExist("/version:");
                logger.AssertErrorDoesNotExist("/runner:");

                logger.AssertSingleErrorExists("/name:");
                logger.AssertErrorsLogged(1);

                // 4. Argument is present but has no value
                logger = CheckProcessingFails("/key:k1", "/name:n1", "/version:");

                logger.AssertErrorDoesNotExist("/key:");
                logger.AssertErrorDoesNotExist("/name:");
                logger.AssertErrorDoesNotExist("/runner:");

                logger.AssertSingleErrorExists("/version:");
                logger.AssertErrorsLogged(1);
            }
        }

        [TestMethod]
        public void PreArgProc_UnrecognisedArguments()
        {
            // 0. Setup
            TestLogger logger;

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                CreateRunnerFilesInScope(scope);

                // 1. Additional unrecognised arguments
                logger = CheckProcessingFails("unrecog2", "/key:k1", "/name:n1", "/version:v1", "unrecog1", string.Empty);

                logger.AssertErrorDoesNotExist("/key:");
                logger.AssertErrorDoesNotExist("/name:");
                logger.AssertErrorDoesNotExist("/version:");
                logger.AssertErrorDoesNotExist("/runner:");

                logger.AssertSingleErrorExists("unrecog1");
                logger.AssertSingleErrorExists("unrecog2");
                logger.AssertErrorsLogged(3); // unrecog1, unrecog2, and the empty string

                // 2. Arguments using the wrong separator i.e. /k=k1  instead of /k:k1
                logger = CheckProcessingFails("/key=k1", "/name=n1", "/version=v1", "/runnerProperties=foo");

                // Expecting errors for the unrecognised arguments...
                logger.AssertSingleErrorExists("/key=k1");
                logger.AssertSingleErrorExists("/name=n1");
                logger.AssertSingleErrorExists("/version=v1");
                logger.AssertSingleErrorExists("/runnerProperties=foo");
                // ... and errors for the missing required arguments
                logger.AssertSingleErrorExists("/key:");
                logger.AssertSingleErrorExists("/name:");
                logger.AssertSingleErrorExists("/version:");
                logger.AssertErrorsLogged(7);
            }
        }

        [TestMethod]
        public void PreArgProc_PropertiesFileSpecifiedOnCommandLine()
        {
            // 0. Setup
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string propertiesFilePath = Path.Combine(testDir, ActualRunnerPropertiesFileName);

            // 1. File exists -> args ok
            File.WriteAllText(propertiesFilePath, "# empty properties file");

            ProcessedArgs result = CheckProcessingSucceeds("/k:key", "/n:name", "/v:version", "/r:" + propertiesFilePath);
            AssertExpectedValues("key", "name", "version", propertiesFilePath, result);

            // 2. File does not exist -> args not ok
            File.Delete(propertiesFilePath);
            
            TestLogger logger = CheckProcessingFails("/k:key", "/n:name", "/v:version", "/r:" + propertiesFilePath);
            logger.AssertErrorsLogged(1);
        }
        
        [TestMethod]
        public void PreArgProc_PropertiesFileFoundViaEnvPath()
        {
            // If the properties file isn't specified on the command line then we
            // attempt to locate it relative to the sonar-runner executable.
            // This only works if the environment variable that specifies the location
            // of the sonar-runner executable is set.
            
            // 0. Setup
            // Create a properties file that will be referenced explicitly on the command line
            string cmdLinePropertiesDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "configDir");
            string cmdLinePropertiesFilePath = Path.Combine(cmdLinePropertiesDir, "dummy.properties.txt"); // should be able to specify any file name on the command line
            File.WriteAllText(cmdLinePropertiesFilePath, "# CMD LINE empty properties file");

            ProcessedArgs result;

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                string envPropertiesFilePath = CreateRunnerFilesInScope(scope);
                
                // 1. Found via path -> args ok
                File.WriteAllText(envPropertiesFilePath, "# ENV empty properties file"); // Create the expected file

                result = CheckProcessingSucceeds("/k:key 1", "/n:name 2", "/v:version 3");
                AssertExpectedValues("key 1", "name 2", "version 3", envPropertiesFilePath, result);

                // 2. Not found via path (file does not exist) -> args not ok
                File.Delete(envPropertiesFilePath);

                using (new AssertIgnoreScope()) // expecting an assert if the exe can be found but the properties can't
                {
                    TestLogger logger = CheckProcessingFails("/k:key 1", "/n:name 2", "/v:version 3");
                    logger.AssertErrorsLogged(1);
                }

                // 3. Command line arg should override path -> args ok
                result = CheckProcessingSucceeds("/k:key 1", "/n:name 2", "/v:version 3", "/r:" + cmdLinePropertiesFilePath);
                AssertExpectedValues("key 1", "name 2", "version 3", cmdLinePropertiesFilePath, result);
            }
        }

        [TestMethod]
        public void PreArgProc_Aliases()
        {
            // 0. Setup
            ProcessedArgs actual;

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                string envPropertiesFilePath = CreateRunnerFilesInScope(scope);

                // Valid
                // Full names, no path
                actual = CheckProcessingSucceeds("/key:my.key", "/name:my name", "/version:1.0");
                AssertExpectedValues("my.key", "my name", "1.0", envPropertiesFilePath, actual);

                // Aliases, no path, different order
                actual = CheckProcessingSucceeds("/v:2.0", "/k:my.key", "/n:my name");
                AssertExpectedValues("my.key", "my name", "2.0", envPropertiesFilePath, actual);

                // Full names with path, casing
                actual = CheckProcessingSucceeds("/KEY:my.key", "/nAme:my name", "/version:1.0", @"/runnerProperties:" + envPropertiesFilePath);
                AssertExpectedValues("my.key", "my name", "1.0", envPropertiesFilePath, actual);

                // Aliases, no path, different order
                actual = CheckProcessingSucceeds(@"/r:" + envPropertiesFilePath, "/v:2:0", "/k:my.key", "/n:my name");
                AssertExpectedValues("my.key", "my name", "2:0", envPropertiesFilePath, actual);
            }
        }

        [TestMethod]
        public void PreArgProc_Duplicates()
        {
            // 0. Setup
            TestLogger logger = new TestLogger();

            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                CreateRunnerFilesInScope(scope);

                // 1. Duplicate key using alias
                logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", "/k:key2");
                logger.AssertErrorsLogged(1);
                logger.AssertSingleErrorExists("/k:key2", "my.key"); // we expect the error to include the first value and the duplicate argument

                // 2. Duplicate name, not using alias
                logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", "/NAME:dupName");
                logger.AssertErrorsLogged(1);
                logger.AssertSingleErrorExists("/NAME:dupName", "my name");

                // 3. Duplicate version, not using alias
                logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", "/v:version2.0");
                logger.AssertErrorsLogged(1);
                logger.AssertSingleErrorExists("/v:version2.0", "1.2");

                // Duplicate key (specified three times)
                logger = CheckProcessingFails("/key:my.key", "/k:k2", "/k:key3");

                logger.AssertSingleErrorExists("/k:k2", "my.key"); // Warning about key appears twice
                logger.AssertSingleErrorExists("/k:key3", "my.key");

                // ... and there should be warnings about other missing args too
                logger.AssertSingleErrorExists("/version:");
                logger.AssertSingleErrorExists("/name:");

                logger.AssertErrorsLogged(4);
            }
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Creates the sonar runner file structure required for the
        /// product "FileLocator" code to work and create a sonar-runner properties
        /// file containing the specified host url setting
        /// </summary>
        /// <returns>Returns the path of the runner bin directory</returns>
        private string CreateRunnerFilesInScope(EnvironmentVariableScope scope)
        {
            string runnerConfDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "conf");
            string runnerBinDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "bin");

            // Create a sonar-runner.properties file
            string runnerExe = Path.Combine(runnerBinDir, "sonar-runner.bat");
            File.WriteAllText(runnerExe, "dummy content - only the existence of the file matters");
            string configFile = Path.Combine(runnerConfDir, ActualRunnerPropertiesFileName);
            File.WriteAllText(configFile, "# dummy properties file content");

            scope.SetPath(runnerBinDir);

            Assert.IsFalse(string.IsNullOrWhiteSpace(FileLocator.FindDefaultSonarRunnerExecutable()), "Test setup error: failed to locate the created runner executable file");

            return configFile;
        }

        #endregion

        #region Checks

        private static TestLogger CheckProcessingFails(params string[] commandLineArgs)
        {
            TestLogger logger = new TestLogger();

            ProcessedArgs result = ArgumentProcessor.TryProcessArgs(commandLineArgs, logger);

            Assert.IsNull(result, "Not expecting the arguments to be processed succesfully");
            logger.AssertErrorsLogged();
            return logger;
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

        private static void AssertExpectedValues(string key, string name, string version, string path, ProcessedArgs actual)
        {
            Assert.AreEqual(key, actual.ProjectKey, "Unexpected project key");
            Assert.AreEqual(name, actual.ProjectName, "Unexpected project name");
            Assert.AreEqual(version, actual.ProjectVersion, "Unexpected project version");
            Assert.AreEqual(path, actual.RunnerPropertiesPath, "Unexpected runner properties path version");
        }

        #endregion
    }
}
