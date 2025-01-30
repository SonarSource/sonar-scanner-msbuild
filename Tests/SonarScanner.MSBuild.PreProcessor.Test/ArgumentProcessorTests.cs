/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test;

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
        args.ServerInfo.Should().NotBeNull();
        args.ServerInfo.ServerUrl.Should().Be("myurl");
        args.ServerInfo.IsSonarCloud.Should().Be(false);

        // 4. Argument is present but has no value
        logger = CheckProcessingFails("/key:");
        logger.AssertSingleErrorExists("/key:");
        logger.AssertErrorsLogged(1);
    }

    [TestMethod]
    public void PreArgProc_HostAndSonarcloudUrlError()
    {
        var logger = CheckProcessingFails("/k:key", "/d:sonar.host.url=firstUrl", "/d:sonar.scanner.sonarcloudUrl=secondUrl");
        logger.AssertErrorLogged("The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set and are different. " +
            "Please set either 'sonar.host.url' for SonarQube or 'sonar.scanner.sonarcloudUrl' for SonarCloud.");
    }

    [TestMethod]
    public void PreArgProc_DefaultHostUrl()
    {
        var args = CheckProcessingSucceeds("/k:key");
        args.ServerInfo.Should().NotBeNull();
        args.ServerInfo.IsSonarCloud.Should().BeTrue();
        args.ServerInfo.ServerUrl.Should().Be("https://sonarcloud.io");
    }

    [TestMethod]
    public void PreArgProc_ApiBaseUrl_Set()
    {
        var logger = new TestLogger();
        var args = CheckProcessingSucceeds(
            logger,
            Substitute.For<IFileWrapper>(),
            Substitute.For<IDirectoryWrapper>(),
            "/k:key",
            "/d:sonar.scanner.apiBaseUrl=test");

        args.ServerInfo.ApiBaseUrl.Should().Be("test");
        logger.AssertDebugLogged($"Server Url: https://sonarcloud.io");
        logger.AssertDebugLogged($"Api Url: test");
        logger.AssertDebugLogged("Is SonarCloud: True");
    }

    [DataTestMethod]
    [DataRow("test")]
    [DataRow("https://sonarcloud.io")]
    [DataRow("https://sonar-test.io")]
    [DataRow("https://www.sonarcloud.io")]
    public void PreArgProc_ApiBaseUrl_NotSet_SonarCloudDefault(string sonarcloudUrl)
    {
        var logger = new TestLogger();
        var args = CheckProcessingSucceeds(
            logger,
            Substitute.For<IFileWrapper>(),
            Substitute.For<IDirectoryWrapper>(),
            "/k:key",
            $"/d:sonar.scanner.sonarcloudUrl={sonarcloudUrl}");

        args.ServerInfo.ApiBaseUrl.Should().Be("https://api.sonarcloud.io", because: "it is not so easy to transform the api url for a user specified sonarcloudUrl (Subdomain change).");
        logger.AssertDebugLogged($"Server Url: {sonarcloudUrl}");
        logger.AssertDebugLogged($"Api Url: https://api.sonarcloud.io");
        logger.AssertDebugLogged("Is SonarCloud: True");
    }

    [DataTestMethod]
    [DataRow("http://host", "http://host/api/v2")]
    [DataRow("http://host/", "http://host/api/v2")]
    [DataRow("http://host ", "http://host /api/v2")]
    [DataRow("http://host///", "http://host/api/v2")]
    [DataRow(@"http://host\", @"http://host\/api/v2")]
    [DataRow("/", "/api/v2")]
    public void PreArgProc_ApiBaseUrl_NotSet_SonarQubeDefault(string hostUri, string expectedApiUri)
    {
        var logger = new TestLogger();
        var args = CheckProcessingSucceeds(
            logger,
            Substitute.For<IFileWrapper>(),
            Substitute.For<IDirectoryWrapper>(),
            "/k:key",
            $"/d:sonar.host.url={hostUri}");

        args.ServerInfo.ApiBaseUrl.Should().Be(expectedApiUri);
        logger.AssertDebugLogged($"Server Url: {hostUri}");
        logger.AssertDebugLogged($"Api Url: {expectedApiUri}");
        logger.AssertDebugLogged("Is SonarCloud: False");
    }

    [DataTestMethod]
    [DataRow("/d:sonar.scanner.os=macos", "macos")]
    [DataRow("/d:sonar.scanner.os=Something", "Something")]
    [DataRow("/d:sonar.scanner.os=1", "1")]
    public void PreArgProc_OperatingSystem_Set(string parameter, string expectedValue) =>
        CheckProcessingSucceeds("/k:key", parameter).OperatingSystem.Should().Be(expectedValue);

    [TestMethod]
    public void PreArgProc_OperatingSystem_NotSet_Windows() =>
        CheckProcessingSucceeds("/k:key").OperatingSystem.Should().Be("windows");

    [DataTestMethod]
    [DataRow("/d:sonar.scanner.arch=a1", "a1")]
    [DataRow("/d:sonar.scanner.arch=b2", "b2")]
    public void PreArgProc_Architecture_Set(string parameter, string expectedValue) =>
        CheckProcessingSucceeds("/k:key", parameter).Architecture.Should().Be(expectedValue);

    [TestMethod]
    public void PreArgProc_Architecture_NotSet() =>
        CheckProcessingSucceeds("/k:key").Architecture.Should().Be(RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant());

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

        logger.AssertNoErrorsLogged("/key:");
        logger.AssertNoErrorsLogged("/name:");
        logger.AssertNoErrorsLogged("/version:");

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
            new("key1", "value1"),
            new(SonarProperties.HostUrl, "url") // required property
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
    public void PreArgProc_With_PropertiesFileSpecifiedOnCommandLine_Organization_Set_Only_In_It_Should_Fail()
    {
        // 0. Setup
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var propertiesFilePath = Path.Combine(testDir, "mysettings.txt");

        // 1. File exists -> args ok
        var properties = new AnalysisProperties
        {
            new(SonarProperties.Organization, "myorg1"),
            new(SonarProperties.HostUrl, "url") // required property
        };
        properties.Save(propertiesFilePath);

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

        result.AllProperties().Should().NotBeNull("GetAllProperties should not return null");
        result.AllProperties().Should().HaveCount(3, "Unexpected number of properties");
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
        logger.AssertErrorsLogged(3);
        logger.AssertSingleErrorExists("dup1=value2", "value1");
        logger.AssertSingleErrorExists("dup2=value4", "value3");
        logger.AssertSingleErrorExists("version:1.2", "1.2");
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

    [DataTestMethod]
    [DataRow(new string[] { }, 100, null)]
    [DataRow(new[] { "/d:sonar.http.timeout=1" }, 1, null)]
    [DataRow(new[] { "/d:sonar.http.timeout=2" }, 2, null)]
    [DataRow(new[] { "/d:sonar.http.timeout=invalid" }, 100, new[] { "sonar.http.timeout", "invalid", "100" })]
    [DataRow(new[] { "/d:sonar.http.timeout=-1" }, 100, new[] { "sonar.http.timeout", "-1", "100" })]
    [DataRow(new[] { "/d:sonar.http.timeout=0" }, 100, new[] { "sonar.http.timeout", "0", "100" })]
    [DataRow(new[] { "/d:sonar.scanner.connectTimeout=1" }, 1, null)]
    [DataRow(new[] { "/d:sonar.scanner.connectTimeout=0" }, 100, new[] { "sonar.scanner.connectTimeout", "0", "100" })]
    [DataRow(new[] { "/d:sonar.http.timeout=1", "/d:sonar.scanner.connectTimeout=1" }, 1, null)]
    [DataRow(new[] { "/d:sonar.scanner.connectTimeout=11", "/d:sonar.http.timeout=22" }, 22, null)]
    [DataRow(new[] { "/d:sonar.http.timeout=22", "/d:sonar.scanner.connectTimeout=11" }, 22, null)]
    [DataRow(new[] { "/d:sonar.http.timeout=22", "/d:sonar.scanner.connectTimeout=invalid" }, 22, null)]
    [DataRow(new[] { "/d:sonar.http.timeout=invalid", "/d:sonar.scanner.connectTimeout=11" }, 11, new[] { "sonar.http.timeout", "invalid", "11" })]
    [DataRow(new[] { "/d:sonar.scanner.socketTimeout=11" }, 100, null)] // sonar.scanner.socketTimeout is ignored on the .Net side
    [DataRow(new[] { "/d:sonar.scanner.responseTimeout=11" }, 100, null)] // sonar.scanner.responseTimeout is ignored on the .Net side
    [DataRow(new[] { "/d:sonar.http.timeout=11", "/d:sonar.scanner.connectTimeout=22", "/d:sonar.scanner.socketTimeout=33", "/d:sonar.scanner.responseTimeout=44" }, 11, null)]
    [DataRow(new[] { "/d:sonar.scanner.connectTimeout=11", "/d:sonar.scanner.socketTimeout=22", "/d:sonar.scanner.responseTimeout=33" }, 11, null)]
    public void PreArgProc_HttpTimeout(string[] timeOuts, int expectedTimeoutSeconds, string[] expectedWarningParts)
    {
        TestLogger logger = new();
        const string warningTemplate = "The specified value `{0}` for `{1}` cannot be parsed. The default value of {2}s will be used. " +
            "Please remove the parameter or specify the value in seconds, greater than 0.";
        var args = CheckProcessingSucceeds(logger, Substitute.For<IFileWrapper>(), Substitute.For<IDirectoryWrapper>(), ["/key:k", .. timeOuts]);
        args.HttpTimeout.Should().Be(TimeSpan.FromSeconds(expectedTimeoutSeconds));
        if (expectedWarningParts is { } warningParts)
        {
            logger.AssertWarningLogged(string.Format(warningTemplate, warningParts));
        }
        else
        {
            logger.AssertNoWarningsLogged();
        }
    }

    [DataTestMethod]
    [DataRow(@"C:\Program Files\Java\jdk1.6.0_30\bin\java.exe")]
    [DataRow(@"C:Program Files\Java\jdk1.6.0_30\bin\java.exe")]
    [DataRow(@"\jdk1.6.0_30\bin\java.exe")]
    public void PreArgProc_JavaExePath_SetValid(string javaExePath)
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(javaExePath).Returns(true);
        CheckProcessingSucceeds(new TestLogger(), fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key", $"/d:sonar.scanner.javaExePath={javaExePath}").JavaExePath.Should().Be(javaExePath);
    }

    [DataTestMethod]
    [DataRow(@"jdk1.6.0_30\bin\java.exe")]
    [DataRow(@"C:Program Files\Java\jdk1.6.0_30\bin\java")]
    [DataRow(@"not a path")]
    [DataRow(@" ")]
    public void PreArgProc_JavaExePath_SetInvalid(string javaExePath)
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(javaExePath).Returns(false);
        var logger = CheckProcessingFails(fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key", $"/d:sonar.scanner.javaExePath={javaExePath}");
        logger.AssertErrorLogged("The argument 'sonar.scanner.javaExePath' contains an invalid path. Please make sure the path is correctly pointing to the java executable.");
    }

    [TestMethod]
    public void PreArgProc_JavaExePath_NotSet() =>
        CheckProcessingSucceeds("/k:key").JavaExePath.Should().BeNull();

    [DataTestMethod]
    [DataRow("true", true)]
    [DataRow("True", true)]
    [DataRow("false", false)]
    [DataRow("False", false)]
    public void PreArgProc_SkipJreProvisioning_SetValid(string skipJreProvisioning, bool result) =>
        CheckProcessingSucceeds("/k:key", $"/d:sonar.scanner.skipJreProvisioning={skipJreProvisioning}").SkipJreProvisioning.Should().Be(result);

    [DataTestMethod]
    [DataRow("gibberish")]
    [DataRow(" ")]
    public void PreArgProc_SkipJreProvisioning_SetInvalid(string skipJreProvisioning)
    {
        var logger = CheckProcessingFails("/k:key", $"/d:sonar.scanner.skipJreProvisioning={skipJreProvisioning}");
        logger.AssertErrorLogged("The argument 'sonar.scanner.skipJreProvisioning' has an invalid value. Please ensure it is set to either 'true' or 'false'.");
    }

    [TestMethod]
    public void PreArgProc_SkipJreProvisioning_NotSet() =>
        CheckProcessingSucceeds("/k:key").SkipJreProvisioning.Should().BeFalse();

    [DataTestMethod]
    [DataRow("true", true)]
    [DataRow("True", true)]
    [DataRow("false", false)]
    [DataRow("False", false)]
    public void PreArgProc_ScanAllAnalysis_SetValid(string scanAll, bool result) =>
        CheckProcessingSucceeds("/k:key", $"/d:sonar.scanner.scanAll={scanAll}").ScanAllAnalysis.Should().Be(result);

    [DataTestMethod]
    [DataRow("gibberish")]
    [DataRow(" ")]
    public void PreArgProc_ScanAllAnalysis_SetInvalid(string scanAll)
    {
        var logger = CheckProcessingFails("/k:key", $"/d:sonar.scanner.scanAll={scanAll}");
        logger.AssertErrorLogged("The argument 'sonar.scanner.scanAll' has an invalid value. Please ensure it is set to either 'true' or 'false'.");
    }

    [TestMethod]
    public void PreArgProc_ScanAllAnalysis_NotSet() =>
        CheckProcessingSucceeds("/k:key").ScanAllAnalysis.Should().BeTrue();

    [TestMethod]
    public void PreArgProc_UserHome_NotSet() =>
        CheckProcessingSucceeds("/k:key").UserHome.Should().Be(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sonar"));

    [TestMethod]
    public void PreArgProc_UserHome_NotSet_CreatedIfNotExists()
    {
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        var defaultUserHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sonar");
        directoryWrapper.Exists(defaultUserHome).Returns(false);
        CheckProcessingSucceeds(new TestLogger(), Substitute.For<IFileWrapper>(), directoryWrapper, "/k:key").UserHome.Should().Be(defaultUserHome);
        directoryWrapper.Received(1).CreateDirectory(defaultUserHome);
    }

    [TestMethod]
    public void PreArgProc_UserHome_NotSet_CreationFails()
    {
        var logger = new TestLogger();
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        var defaultUserHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sonar");
        directoryWrapper.Exists(defaultUserHome).Returns(false);
        directoryWrapper.When(x => x.CreateDirectory(defaultUserHome)).Do(_ => throw new IOException("Directory can not be created."));
        CheckProcessingSucceeds(logger, Substitute.For<IFileWrapper>(), directoryWrapper, "/k:key").UserHome.Should().BeNull();
        directoryWrapper.Received(1).CreateDirectory(defaultUserHome);
        logger.AssertWarningLogged($"Failed to create the default user home directory '{defaultUserHome}' with exception 'Directory can not be created.'.");
    }

    [DataTestMethod]
    [DataRow("Test")]
    [DataRow(@"""Test""")]
    [DataRow("'Test'")]
    [DataRow(@"C:\Users\Some Name")]
    [DataRow(@"""C:\Users\Some Name""")]
    public void PreArgProc_UserHome_Set_DirectoryExists(string path)
    {
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(path).Returns(true);
        CheckProcessingSucceeds(new TestLogger(), Substitute.For<IFileWrapper>(), directoryWrapper, "/k:key", $"/d:sonar.userHome={path}").UserHome.Should().Be(path);
    }

    [DataTestMethod]
    [DataRow("Test")]
    [DataRow(@"""Test""")]
    [DataRow("'Test'")]
    [DataRow(@"C:\Users\Some Name")]
    [DataRow(@"""C:\Users\Some Name""")]
    public void PreArgProc_UserHome_Set_DirectoryExistsNot_CanBeCreated(string path)
    {
        var logger = new TestLogger();
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(path).Returns(false);
        CheckProcessingSucceeds(logger, Substitute.For<IFileWrapper>(), directoryWrapper, "/k:key", $"/d:sonar.userHome={path}");
        directoryWrapper.Received(1).CreateDirectory(path);
        logger.AssertDebugLogged($"Created the sonar.userHome directory at '{path}'.");
    }

    [DataTestMethod]
    [DataRow("Test")]
    [DataRow(@"""Test""")]
    [DataRow("'Test'")]
    [DataRow(@"C:\Users\Some Name")]
    [DataRow(@"""C:\Users\Some Name""")]
    public void PreArgProc_UserHome_Set_DirectoryExistsNot_CanNotBeCreated(string path)
    {
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(path).Returns(false);
        directoryWrapper.When(x => x.CreateDirectory(path)).Do(_ => throw new IOException("Directory creation failed."));
        var logger = CheckProcessingFails(Substitute.For<IFileWrapper>(), directoryWrapper, "/k:key", $"/d:sonar.userHome={path}");
        directoryWrapper.Received(1).CreateDirectory(path);
        logger.AssertErrorLogged($"The attempt to create the directory specified by 'sonar.userHome' at '{path}' failed with error 'Directory creation failed.'. " +
            "Provide a valid path for 'sonar.userHome' to a directory that can be created.");
    }

    [DataTestMethod]
    [DataRow("Test.pfx", "changeit")]
    [DataRow(@"""Test.p12""", " ")]
    [DataRow("'Test.pfx'", @"""changeit""")]
    [DataRow(@"C:\Users\Some Name.pfx", @"""special characters äöü""")]
    [DataRow(@"""C:\Users\Some Name.pfx""", "ghws9uEo3GE%X!")]
    public void PreArgProc_TruststorePath_Password(string path, string password)
    {
        var logger = new TestLogger();
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(Arg.Any<string>()).Returns(true);
        var result = CheckProcessingSucceeds(logger, fileWrapper, Substitute.For<IDirectoryWrapper>(),
            "/k:key",
            $"/d:sonar.scanner.truststorePath={path}",
            $"/d:sonar.scanner.truststorePassword={password}");
        result.TruststorePath.Should().Be(path);
        result.TruststorePassword.Should().Be(password);
    }

    [TestMethod]
    public void PreArgProc_TruststorePath()
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(Arg.Any<string>()).Returns(true);
        var result = CheckProcessingSucceeds(new TestLogger(), fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key", @"/d:sonar.scanner.truststorePath=""c:\test.pfx""");
        result.TruststorePath.Should().Be(@"""c:\test.pfx""");
        result.TruststorePassword.Should().BeNull();
    }

    [TestMethod]
    public void PreArgProc_Fail_TruststorePassword_Only()
    {
        var logger = CheckProcessingFails("/k:key", @"/d:sonar.scanner.truststorePassword=changeit");
        logger.Errors.Should().Contain("'sonar.scanner.truststorePath' must be specified when 'sonar.scanner.truststorePassword' is provided.");
    }

    [DataTestMethod]
    [DataRow(@"/d:sonar.scanner.truststorePassword=changeit", "changeit")]
    [DataRow(@"/d:sonar.scanner.truststorePassword=changeit now", "changeit now")]

    // https://sonarsource.atlassian.net/browse/SCAN4NET-204
    [DataRow(@"/d:sonar.scanner.truststorePassword=""changeit now""", @"""changeit now""")] // should be 'changeit now' without double quotes
    [DataRow(@"/d:sonar.scanner.truststorePassword=""hjdska/msm^#&%!""", @"""hjdska/msm^#&%!""")] // should be 'hjdska/msm^#&%!' without double quotes
    // [DataRow(@"/d:sonar.scanner.truststorePassword=", null)] // empty password should be allowed
    public void PreArgProc_TruststorePassword_Quoted(string passwordProperty, string parsedPassword)
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(Arg.Any<string>()).Returns(true);
        var result = CheckProcessingSucceeds(new(), fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key", @"/d:sonar.scanner.truststorePath=test.pfx", passwordProperty);
        result.TruststorePassword.Should().Be(parsedPassword);
    }

    [TestMethod]
    public void PreArgProc_TruststorePath_FileNotExists()
    {
        const string fileName = "test.pfx";
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(fileName).Returns(false);
        var log = CheckProcessingFails(fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key", $"/d:sonar.scanner.truststorePath={fileName}");
        log.AssertErrorLogged($"The specified sonar.scanner.truststorePath file '{fileName}' can not be found.");
    }

    [TestMethod]
    public void PreArgProc_TruststorePath_FileNotOpen()
    {
        const string fileName = "test.pfx";
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(fileName).Returns(true);
        fileWrapper.Open(fileName).Throws(new IOException("File can not be opened."));
        var log = CheckProcessingFails(fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key", $"/d:sonar.scanner.truststorePath={fileName}");
        log.AssertErrorLogged($"The sonar.scanner.truststorePath file '{fileName}' can not be opened. Details: System.IO.IOException: File can not be opened.");
    }

    #endregion Tests

    #region Checks

    private static TestLogger CheckProcessingFails(params string[] commandLineArgs) =>
        CheckProcessingFails(Substitute.For<IFileWrapper>(), Substitute.For<IDirectoryWrapper>(), commandLineArgs);

    private static TestLogger CheckProcessingFails(IFileWrapper fileWrapper, IDirectoryWrapper directoryWrapper, params string[] commandLineArgs)
    {
        var logger = new TestLogger();

        var result = TryProcessArgsIsolatedFromEnvironment(commandLineArgs, fileWrapper, directoryWrapper, logger);

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

    private static ProcessedArgs CheckProcessingSucceeds(params string[] commandLineArgs) =>
        CheckProcessingSucceeds(new TestLogger(), Substitute.For<IFileWrapper>(), Substitute.For<IDirectoryWrapper>(), commandLineArgs);

    private static ProcessedArgs CheckProcessingSucceeds(TestLogger logger, IFileWrapper fileWrapper, IDirectoryWrapper directoryWrapper, params string[] commandLineArgs)
    {
        var result = TryProcessArgsIsolatedFromEnvironment(commandLineArgs, fileWrapper, directoryWrapper, logger);

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
        var found = Property.TryGetProperty(key, actual.AllProperties(), out Property match);
        found.Should().BeTrue("Failed to find the expected property. Key: {0}", key);
        match.Should().NotBeNull("Returned property should not be null. Key: {0}", key);
        match.Value.Should().Be(value, "Property does not have the expected value");
    }

    private static void AssertExpectedInstallTargets(bool expected, ProcessedArgs actual)
    {
        actual.InstallLoaderTargets.Should().Be(expected);
    }

    private static ProcessedArgs TryProcessArgsIsolatedFromEnvironment(string[] commandLineArgs, IFileWrapper fileWrapper, IDirectoryWrapper directoryWrapper, ILogger logger)
    {
        // Make sure the test isn't affected by the hosting environment
        // The SonarCloud AzDO extension sets additional properties in an environment variable that
        // would be picked up by the argument processor
        using var scope = new EnvironmentVariableScope().SetVariable(EnvScannerPropertiesProvider.ENV_VAR_KEY, null);
        return ArgumentProcessor.TryProcessArgs(commandLineArgs, fileWrapper, directoryWrapper, logger);
    }

    #endregion Checks
}
