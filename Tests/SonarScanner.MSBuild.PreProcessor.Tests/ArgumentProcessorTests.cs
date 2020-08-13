/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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

using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Tests
{
    [TestClass]
    public class ArgumentProcessorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PreArgProc_MissingArguments()
        {
            // 0. Setup
            TestLogger logger;

            // 1. Null logger
            Action act = () => ArgumentProcessor.TryProcessArgs(null, null);
            act.Should().ThrowExactly<ArgumentNullException>();

            // 2. required argument missing
            logger = CheckProcessingFails(/* no command line args */);
            logger.AssertSingleErrorExists("/key:"); // we expect error with info about the missing required parameter, which should include the primary alias
            logger.AssertErrorsLogged(1);

            // 3. Only key and host URL are required
            var args = CheckProcessingSucceeds("/k:key", "/d:sonar.host.url=myurl");
            "key".Should().Be(args.ProjectKey);
            "myurl".Should().Be(args.SonarQubeUrl);

            // 4. Argument is present but has no value
            logger = CheckProcessingFails("/key:");
            logger.AssertSingleErrorExists("/key:");
            logger.AssertErrorsLogged(1);
        }

        [TestMethod]
        public void PreArgProc_DefaultHostUrl()
        {
            var args = CheckProcessingSucceeds("/k:key");
            "http://localhost:9000".Should().Be(args.SonarQubeUrl);
        }

        [TestMethod]
        [WorkItem(102)] // http://jira.sonarsource.com/browse/SONARMSBRU-102
        public void PreArgProc_ProjectKeyValidity()
        {
            // 0. Setup - none

            // 1. Invalid characters
            // Whitespace
            CheckProjectKeyIsInvalid("spaces in name");
            CheckProjectKeyIsInvalid("a\tb");
            CheckProjectKeyIsInvalid("a\rb");
            CheckProjectKeyIsInvalid("a\r\nb");

            // invalid non-alpha characters
            CheckProjectKeyIsInvalid("+a");
            CheckProjectKeyIsInvalid("b@");
            CheckProjectKeyIsInvalid("c~");
            CheckProjectKeyIsInvalid("d,");

            CheckProjectKeyIsInvalid("0"); // single numeric is not ok
            CheckProjectKeyIsInvalid("0123456789"); // all numeric is not ok

            // 2. Valid
            CheckProjectKeyIsValid("0123456789.abcdefghijklmnopqrstuvwxyz:-._ABCDEFGHIJKLMNOPQRSTUVWXYZ"); // all valid characters

            CheckProjectKeyIsValid("a"); // single alpha character
            CheckProjectKeyIsValid("_"); // single non-alpha character
            CheckProjectKeyIsValid(":"); // single non-alpha character
            CheckProjectKeyIsValid("-"); // single non-alpha character
            CheckProjectKeyIsValid("."); // single non-alpha character

            CheckProjectKeyIsValid(".-_:"); // all non-alpha characters

            CheckProjectKeyIsValid("0.1"); // numerics with any other valid character is ok
            CheckProjectKeyIsValid("_0"); // numeric with any other valid character is ok
            CheckProjectKeyIsValid("0."); // numeric with any other valid character is ok

            // 3. More realistic valid options
            CheckProjectKeyIsValid("myproject");
            CheckProjectKeyIsValid("my.Project");
            CheckProjectKeyIsValid("my_second_Project");
            CheckProjectKeyIsValid("my-other_Project");
        }

        [TestMethod]
        public void PreArgProc_UnrecognisedArguments()
        {
            // 0. Setup
            TestLogger logger;

            // 1. Additional unrecognized arguments
            logger = CheckProcessingFails("unrecog2", "/key:k1", "/name:n1", "/version:v1", "unrecog1", "/p:key=value", string.Empty);

            logger.AssertErrorDoesNotExist("/key:");
            logger.AssertErrorDoesNotExist("/name:");
            logger.AssertErrorDoesNotExist("/version:");

            logger.AssertSingleErrorExists("unrecog1");
            logger.AssertSingleErrorExists("unrecog2");
            logger.AssertSingleErrorExists("/p:key=value"); // /p: is no longer supported - should be /d:
            logger.AssertErrorsLogged(4); // unrecog1, unrecog2, /p: and the empty string

            // 2. Arguments using the wrong separator i.e. /k=k1  instead of /k:k1
            logger = CheckProcessingFails("/key=k1", "/name=n1", "/version=v1");

            // Expecting errors for the unrecognized arguments...
            logger.AssertSingleErrorExists("/key=k1");
            logger.AssertSingleErrorExists("/name=n1");
            logger.AssertSingleErrorExists("/version=v1");
            // ... and errors for the missing required arguments
            logger.AssertSingleErrorExists("/key:");
            logger.AssertErrorsLogged(4);
        }

        [TestMethod]
        public void ArgProc_InstallTargets()
        {
            ProcessedArgs actual;

            var validUrlArg = "/d:sonar.host.url=foo";

            // Valid
            // No install argument passed -> install targets
            actual = CheckProcessingSucceeds("/key:my.key", "/name:my name", "/version:1.0", validUrlArg);
            AssertExpectedInstallTargets(TargetsInstaller.DefaultInstallSetting, actual);

            // "true"-> install targets
            actual = CheckProcessingSucceeds("/key:my.key", "/name:my name", "/version:1.0", validUrlArg, "/install:true");
            AssertExpectedInstallTargets(true, actual);

            // Case insensitive "TrUe"-> install targets
            actual = CheckProcessingSucceeds("/key:my.key", "/name:my name", "/version:1.0", validUrlArg, "/install:TrUe");
            AssertExpectedInstallTargets(true, actual);

            // "false"-> don't install targets
            actual = CheckProcessingSucceeds("/key:my.key", "/name:my name", "/version:1.0", validUrlArg, "/install:false");
            AssertExpectedInstallTargets(false, actual);

            // Case insensitive "falSE"
            actual = CheckProcessingSucceeds("/key:my.key", "/name:my name", "/version:1.0", validUrlArg, "/install:falSE");
            AssertExpectedInstallTargets(false, actual);

            // Invalid value (only true and false are supported)
            var logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", "/install:1");
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("/install"); // we expect the error to include the first value and the duplicate argument

            // No value
            logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", "/install:");
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("/install"); // we expect the error to include the first value and the duplicate argument

            // Empty value -> parsing should fail
            logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", @"/install:"" """);
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("/install"); // we expect the error to include the first value and the duplicate argument

            // Duplicate value -> parsing should fail
            logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", "/install:true", "/install:false");
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("/install"); // we expect the error to include the first value and the duplicate argument
        }

        [TestMethod]
        public void PreArgProc_PropertiesFileSpecifiedOnCommandLine()
        {
            // 0. Setup
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var propertiesFilePath = Path.Combine(testDir, "mysettings.txt");

            // 1. File exists -> args ok
            var properties = new AnalysisProperties
            {
                new Property() { Id = "key1", Value = "value1" },
                new Property() { Id = SonarProperties.HostUrl, Value = "url" } // required property
            };
            properties.Save(propertiesFilePath);

            var result = CheckProcessingSucceeds("/k:key", "/n:name", "/v:version", "/s:" + propertiesFilePath);
            AssertExpectedValues("key", "name", "version", result);
            AssertExpectedPropertyValue("key1", "value1", result);

            // 2. File does not exist -> args not ok
            File.Delete(propertiesFilePath);

            var logger = CheckProcessingFails("/k:key", "/n:name", "/v:version", "/s:" + propertiesFilePath);
            logger.AssertErrorsLogged(1);
        }

        [TestMethod]
        public void PreArgProc_Aliases()
        {
            // 0. Setup
            ProcessedArgs actual;

            var validUrlArg = "/d:sonar.host.url=foo"; // this doesn't have an alias but does need to be supplied

            // Valid
            // Full names, no path
            actual = CheckProcessingSucceeds("/key:my.key", "/name:my name", "/version:1.0", validUrlArg);
            AssertExpectedValues("my.key", "my name", "1.0", actual);

            // Aliases, no path, different order
            actual = CheckProcessingSucceeds("/v:2.0", "/k:my.key", "/n:my name", validUrlArg);
            AssertExpectedValues("my.key", "my name", "2.0", actual);

            // Full names
            actual = CheckProcessingSucceeds("/key:my.key", "/name:my name", "/version:1.0", validUrlArg);
            AssertExpectedValues("my.key", "my name", "1.0", actual);

            // Aliases, different order
            actual = CheckProcessingSucceeds("/v:2:0", "/k:my.key", "/n:my name", validUrlArg);
            AssertExpectedValues("my.key", "my name", "2:0", actual);

            // Full names, wrong case -> ignored
            var logger = CheckProcessingFails("/KEY:my.key", "/nAme:my name", "/versIOn:1.0", validUrlArg);
            logger.AssertSingleErrorExists("/KEY:my.key");
            logger.AssertSingleErrorExists("/nAme:my name");
            logger.AssertSingleErrorExists("/versIOn:1.0");
        }

        [TestMethod]
        public void PreArgProc_Duplicates()
        {
            // 0. Setup
            TestLogger logger;

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

            logger.AssertErrorsLogged(2);
        }

        [TestMethod]
        public void PreArgProc_DynamicSettings()
        {
            // 0. Setup - none

            // 1. Args ok
            var result = CheckProcessingSucceeds(
                // Non-dynamic values
                "/key:my.key", "/name:my name", "/version:1.2",
                // Dynamic values
                "/d:sonar.host.url=required value",
                "/d:key1=value1",
                "/d:key2=value two with spaces"
                );

            AssertExpectedValues("my.key", "my name", "1.2", result);

            AssertExpectedPropertyValue(SonarProperties.HostUrl, "required value", result);
            AssertExpectedPropertyValue("key1", "value1", result);
            AssertExpectedPropertyValue("key2", "value two with spaces", result);

            result.GetAllProperties().Should().NotBeNull("GetAllProperties should not return null");
            result.GetAllProperties().Should().HaveCount(3, "Unexpected number of properties");
        }

        [TestMethod]
        public void PreArgProc_DynamicSettings_Invalid()
        {
            // Arrange
            TestLogger logger;

            // Act
            logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2",
                    "/d:invalid1 =aaa",
                    "/d:notkeyvalue",
                    "/d: spacebeforekey=bb",
                    "/d:missingvalue=",
                    "/d:validkey=validvalue");

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
                    "/d:dup1=value1", "/d:dup1=value2", "/d:dup2=value3", "/d:dup2=value4",
                    "/d:unique=value5");

            // Assert
            logger.AssertSingleErrorExists("dup1=value2", "value1");
            logger.AssertSingleErrorExists("dup2=value4", "value3");
            logger.AssertErrorsLogged(2);
        }

        [TestMethod]
        public void PreArgProc_Arguments_Duplicates_WithDifferentFlagsPrefixes()
        {
            // Arrange
            TestLogger logger;

            // Act
            logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", "-version:1.2",
                    "/d:dup1=value1", "-d:dup1=value2", "/d:dup2=value3", "/d:dup2=value4",
                    "/d:unique=value5");

            // Assert
            logger.AssertSingleErrorExists("-version:1.2", "1.2");
            logger.AssertErrorsLogged(1);
        }

        [TestMethod]
        public void PreArgProc_DynamicSettings_Duplicates_WithDifferentFlagsPrefixes()
        {
            // Arrange
            TestLogger logger;

            // Act
            logger = CheckProcessingFails("/key:my.key", "/name:my name",
                    "/d:dup1=value1", "-d:dup1=value2", "/d:dup2=value3", "/d:dup2=value4",
                    "/d:unique=value5");

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
                "/d:sonar.projectKey=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectKey, "/k");

            logger = CheckProcessingFails(
                "/key:my.key", "/name:my name", "/version:1.2",
                "/d:sonar.projectName=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectName, "/n");

            logger = CheckProcessingFails(
                "/key:my.key", "/name:my name", "/version:1.2",
                "/d:sonar.projectVersion=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectVersion, "/v");

            logger = CheckProcessingFails(
                "/key:my.key", "/name:my name", "/version:1.2", "/organization:my_org",
                "/d:sonar.organization=value1");
            logger.AssertSingleErrorExists(SonarProperties.Organization, "/o");

            // 2. Other values that can't be set

            logger = CheckProcessingFails(
                "/key:my.key", "/name:my name", "/version:1.2",
                "/d:sonar.working.directory=value1");
            logger.AssertSingleErrorExists(SonarProperties.WorkingDirectory);
        }

        [TestMethod]
        public void PreArgProc_Organization()
        {
            var args = CheckProcessingSucceeds("/key:my.key", "/organization:my_org");
            args.Organization.Should().Be("my_org");

            args = CheckProcessingSucceeds("/key:my.key", "/o:my_org");
            args.Organization.Should().Be("my_org");

            args = CheckProcessingSucceeds("/key:my.key");
            args.Organization.Should().BeNull();
        }

        #endregion Tests

        #region Checks

        private static TestLogger CheckProcessingFails(params string[] commandLineArgs)
        {
            var logger = new TestLogger();

            var result = TryProcessArgsIsolatedFromEnvironment(commandLineArgs, logger);

            result.Should().BeNull("Not expecting the arguments to be processed successfully");
            logger.AssertErrorsLogged();
            return logger;
        }

        private static void CheckProjectKeyIsInvalid(string projectKey)
        {
            TestLogger logger;

            var commandLineArgs = new string[] { "/k:" + projectKey, "/n:valid_name", "/v:1.0", "/d:" + SonarProperties.HostUrl + "=http://validUrl" };

            logger = CheckProcessingFails(commandLineArgs);
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("Invalid project key. Allowed characters are alphanumeric, '-', '_', '.' and ':', with at least one non-digit.");
        }

        private static void CheckProjectKeyIsValid(string projectKey)
        {
            var result = CheckProcessingSucceeds("/key:" + projectKey, "/name:valid name", "/version:1.0", "/d:sonar.host.url=http://valid");
            result.ProjectKey.Should().Be(projectKey, "Unexpected project key");
        }

        private static ProcessedArgs CheckProcessingSucceeds(params string[] commandLineArgs)
        {
            var logger = new TestLogger();

            var result = TryProcessArgsIsolatedFromEnvironment(commandLineArgs, logger);

            result.Should().NotBeNull("Expecting the arguments to be processed successfully");

            logger.AssertErrorsLogged(0);

            return result;
        }

        private static void AssertExpectedValues(string key, string name, string version, ProcessedArgs actual)
        {
            actual.ProjectKey.Should().Be(key, "Unexpected project key");
            actual.ProjectName.Should().Be(name, "Unexpected project name");
            actual.ProjectVersion.Should().Be(version, "Unexpected project version");
        }

        private static void AssertExpectedPropertyValue(string key, string value, ProcessedArgs actual)
        {
            // Test the GetSetting method
            var actualValue = actual.GetSetting(key);
            actualValue.Should().NotBeNull("Expected dynamic settings does not exist. Key: {0}", key);
            actualValue.Should().Be(value, "Dynamic setting does not have the expected value");

            // Check the public list of properties
            var found = Property.TryGetProperty(key, actual.GetAllProperties(), out Property match);
            found.Should().BeTrue("Failed to find the expected property. Key: {0}", key);
            match.Should().NotBeNull("Returned property should not be null. Key: {0}", key);
            match.Value.Should().Be(value, "Property does not have the expected value");
        }

        private static void AssertExpectedInstallTargets(bool expected, ProcessedArgs actual)
        {
            actual.InstallLoaderTargets.Should().Be(expected);
        }

        private static ProcessedArgs TryProcessArgsIsolatedFromEnvironment(string[] commandLineArgs, ILogger logger)
        {
            ProcessedArgs args = null;

            // Make sure the test isn't affected by the hosting environment
            // The SonarCloud VSTS extension sets additional properties in an environment variable that
            // would be picked up by the argument processor
            using (var scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(EnvScannerPropertiesProvider.ENV_VAR_KEY, null);

                args = ArgumentProcessor.TryProcessArgs(commandLineArgs, logger);
            }

            return args;
        }


        #endregion Checks
    }
}
