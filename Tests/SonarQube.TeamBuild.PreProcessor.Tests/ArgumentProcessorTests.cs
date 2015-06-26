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
using System.Linq;
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


            // 2. All required arguments missing
            logger = CheckProcessingFails(/* no command line args */);
            logger.AssertSingleErrorExists("/key:"); // we expect errors with info about the missing required parameters, which should include the primary alias
            logger.AssertSingleErrorExists("/name:");
            logger.AssertSingleErrorExists("/version:");
            logger.AssertErrorsLogged(3);

            // 3. Some required arguments missing
            logger = CheckProcessingFails("/k:key", "/v:version");

            logger.AssertErrorDoesNotExist("/key:");
            logger.AssertErrorDoesNotExist("/version:");

            logger.AssertSingleErrorExists("/name:");
            logger.AssertErrorsLogged(1);

            // 4. Argument is present but has no value
            logger = CheckProcessingFails("/key:k1", "/name:n1", "/version:");

            logger.AssertErrorDoesNotExist("/key:");
            logger.AssertErrorDoesNotExist("/name:");

            logger.AssertSingleErrorExists("/version:");
            logger.AssertErrorsLogged(1);
        }

        [TestMethod]
        public void PreArgProc_UnrecognisedArguments()
        {
            // 0. Setup
            TestLogger logger;

            // 1. Additional unrecognised arguments
            logger = CheckProcessingFails("unrecog2", "/key:k1", "/name:n1", "/version:v1", "unrecog1", string.Empty);

            logger.AssertErrorDoesNotExist("/key:");
            logger.AssertErrorDoesNotExist("/name:");
            logger.AssertErrorDoesNotExist("/version:");

            logger.AssertSingleErrorExists("unrecog1");
            logger.AssertSingleErrorExists("unrecog2");
            logger.AssertErrorsLogged(3); // unrecog1, unrecog2, and the empty string

            // 2. Arguments using the wrong separator i.e. /k=k1  instead of /k:k1
            logger = CheckProcessingFails("/key=k1", "/name=n1", "/version=v1");

            // Expecting errors for the unrecognised arguments...
            logger.AssertSingleErrorExists("/key=k1");
            logger.AssertSingleErrorExists("/name=n1");
            logger.AssertSingleErrorExists("/version=v1");
            // ... and errors for the missing required arguments
            logger.AssertSingleErrorExists("/key:");
            logger.AssertSingleErrorExists("/name:");
            logger.AssertSingleErrorExists("/version:");
            logger.AssertErrorsLogged(6);
        }

        [TestMethod]
        public void PreArgProc_PropertiesFileSpecifiedOnCommandLine()
        {
            // 0. Setup
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string propertiesFilePath = Path.Combine(testDir, ActualRunnerPropertiesFileName);

            // 1. File exists -> args ok
            AnalysisProperties properties = new AnalysisProperties();
            properties.Save(propertiesFilePath);

            ProcessedArgs result = CheckProcessingSucceeds("/k:key", "/n:name", "/v:version", "/s:" + propertiesFilePath);
            AssertExpectedValues("key", "name", "version", result);

            // 2. File does not exist -> args not ok
            File.Delete(propertiesFilePath);
            
            TestLogger logger = CheckProcessingFails("/k:key", "/n:name", "/v:version", "/s:" + propertiesFilePath);
            logger.AssertErrorsLogged(1);
        }

        [TestMethod]
        public void PreArgProc_Aliases()
        {
            // 0. Setup
            ProcessedArgs actual;

            // Valid
            // Full names, no path
            actual = CheckProcessingSucceeds("/key:my.key", "/name:my name", "/version:1.0");
            AssertExpectedValues("my.key", "my name", "1.0", actual);

            // Aliases, no path, different order
            actual = CheckProcessingSucceeds("/v:2.0", "/k:my.key", "/n:my name");
            AssertExpectedValues("my.key", "my name", "2.0", actual);

            // Full names
            actual = CheckProcessingSucceeds("/key:my.key", "/name:my name", "/version:1.0");
            AssertExpectedValues("my.key", "my name", "1.0", actual);

            // Aliases, different order
            actual = CheckProcessingSucceeds("/v:2:0", "/k:my.key", "/n:my name");
            AssertExpectedValues("my.key", "my name", "2:0", actual);

            // Full names, wrong case -> ignored
            TestLogger logger = CheckProcessingFails("/KEY:my.key", "/nAme:my name", "/versIOn:1.0");
            logger.AssertSingleErrorExists("/KEY:my.key");
            logger.AssertSingleErrorExists("/nAme:my name");
            logger.AssertSingleErrorExists("/versIOn:1.0");
        }

        [TestMethod]
        public void PreArgProc_Duplicates()
        {
            // 0. Setup
            TestLogger logger = new TestLogger();

            // 1. Duplicate key using alias
            logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", "/k:key2");
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("/k:key2", "my.key"); // we expect the error to include the first value and the duplicate argument

            // 2. Duplicate name, not using alias
            logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", "/name:dupName");
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("/name:dupName", "my name");

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

        [TestMethod]
        public void PreArgProc_DynamicSettings()
        {
            // 0. Setup - none

            // 1. Args ok            
            ProcessedArgs result = CheckProcessingSucceeds(
                "/key:my.key", "/name:my name", "/version:1.2",
                "/p:key1=value1", "/p:key2=value two with spaces");

            AssertExpectedValues("my.key", "my name", "1.2", result);

            AssertExpectedDynamicValue("key1", "value1", result);
            AssertExpectedDynamicValue("key2", "value two with spaces", result);

            Assert.IsNotNull(result.GetAllProperties(), "GetAllProperties should not return null");
            Assert.AreEqual(5, result.GetAllProperties().Count(), "Unexpected number of properties");
        }

        [TestMethod]
        public void PreArgProc_DynamicSettings_Invalid()
        {
            // Arrange
            TestLogger logger;

            // Act 
            logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2",
                    "/p:invalid1 =aaa",
                    "/p:notkeyvalue",
                    "/p: spacebeforekey=bb",
                    "/p:missingvalue=",
                    "/p:validkey=validvalue");

            // Assert
            logger.AssertSingleErrorExists("invalid1 =aaa");
            logger.AssertSingleErrorExists("notkeyvalue");
            logger.AssertSingleErrorExists(" spacebeforekey=bb");
            logger.AssertSingleErrorExists("missingvalue=");

            logger.AssertErrorsLogged(4);
        }

        [TestMethod]
        public void PreArgProc_DynamicSettings_Duplicates()
        {
            // Arrange
            TestLogger logger;

            // Act 
            logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2",
                    "/p:dup1=value1", "/p:dup1=value2", "/p:dup2=value3", "/p:dup2=value4",
                    "/p:unique=value5");

            // Assert
            logger.AssertSingleErrorExists("dup1=value2", "value1");
            logger.AssertSingleErrorExists("dup2=value4", "value3");
            logger.AssertErrorsLogged(2);
        }

        [TestMethod]
        public void PreArgProc_Disallowed_DynamicSettings()
        {
            // 0. Setup
            TestLogger logger;

            // 1. Named arguments cannot be overridden
            logger = CheckProcessingFails(
                "/key:my.key", "/name:my name", "/version:1.2",
                "/p:sonar.projectKey=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectKey, "/k");


            logger = CheckProcessingFails(
                "/key:my.key", "/name:my name", "/version:1.2",
                "/p:sonar.projectName=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectName, "/n");


            logger = CheckProcessingFails(
                "/key:my.key", "/name:my name", "/version:1.2",
                "/p:sonar.projectVersion=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectVersion, "/v");


            // 2. Other values that can't be set
            logger = CheckProcessingFails(
                "/key:my.key", "/name:my name", "/version:1.2",
                "/p:sonar.projectBaseDir=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectBaseDir);


            logger = CheckProcessingFails(
                "/key:my.key", "/name:my name", "/version:1.2",
                "/p:sonar.working.directory=value1");
            logger.AssertSingleErrorExists(SonarProperties.WorkingDirectory);

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

        private static void AssertExpectedValues(string key, string name, string version, ProcessedArgs actual)
        {
            Assert.AreEqual(key, actual.ProjectKey, "Unexpected project key");
            Assert.AreEqual(name, actual.ProjectName, "Unexpected project name");
            Assert.AreEqual(version, actual.ProjectVersion, "Unexpected project version");
        }

        private static void AssertExpectedDynamicValue(string key, string value, ProcessedArgs actual)
        {
            // Test the GetSetting method
            string actualValue = actual.GetSetting(key);
            Assert.IsNotNull(actualValue, "Expected dynamic settings does not exist. Key: {0}", key);
            Assert.AreEqual(value, actualValue, "Dynamic setting does not have the expected value");

            // Check the public list of properties
            Property match;
            bool found = Property.TryGetProperty(key, actual.GetAllProperties(), out match);
            Assert.IsTrue(found, "Failed to find the expected property. Key: {0}", key);
            Assert.IsNotNull(match, "Returned property should not be null. Key: {0}", key);
            Assert.AreEqual(value, match.Value, "Property does not have the expected value");
        }

        #endregion
    }
}
