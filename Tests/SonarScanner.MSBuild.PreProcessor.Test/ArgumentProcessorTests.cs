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
#pragma warning disable S3994 // we are specifically testing string urls

using System.Runtime.InteropServices;
using NSubstitute.ExceptionExtensions;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class ArgumentProcessorTests
{
    private static readonly char Separator = Path.DirectorySeparatorChar;

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void PreArgProc_NullLogger_Throws()
    {
        Action act = () => ArgumentProcessor.TryProcessArgs(null, null);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    public void PreArgProc_RequiredArguments_ProcessingSucceeds()
    {
        var args = CheckProcessingSucceeds("/k:key", "/d:sonar.host.url=myurl");
        "key".Should().Be(args.ProjectKey);
        args.ServerInfo.Should().NotBeNull();
        args.ServerInfo.ServerUrl.Should().Be("myurl");
        args.ServerInfo.IsSonarCloud.Should().Be(false);
    }

    [TestMethod]
    public void PreArgProc_NoArguments_ProcessingFails()
    {
        var logger = CheckProcessingFails();
        logger.AssertSingleErrorExists("/key:"); // we expect error with info about the missing required parameter, which should include the primary alias
        logger.AssertErrorsLogged(1);
    }

    [TestMethod]
    public void PreArgProc_KeyHasNoValue_ProcessingFails()
    {
        var logger = CheckProcessingFails("/key:");
        logger.AssertSingleErrorExists("/key:");
        logger.AssertErrorsLogged(1);
    }

    [TestMethod]
    public void PreArgProc_HostAndSonarcloudUrlError()
    {
        var logger = CheckProcessingFails("/k:key", "/d:sonar.host.url=firstUrl", "/d:sonar.scanner.sonarcloudUrl=secondUrl");
        logger.AssertErrorLogged("The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set and are different. "
            + "Please set either 'sonar.host.url' for SonarQube or 'sonar.scanner.sonarcloudUrl' for SonarCloud.");
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
        logger.AssertDebugLogged("Server Url: https://sonarcloud.io");
        logger.AssertDebugLogged("Api Url: test");
        logger.AssertDebugLogged("Is SonarCloud: True");
    }

    [TestMethod]
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
        logger.AssertDebugLogged("Api Url: https://api.sonarcloud.io");
        logger.AssertDebugLogged("Is SonarCloud: True");
    }

    [TestMethod]
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

    [TestMethod]
    [DataRow("us")]
    [DataRow("US")]
    [DataRow("uS")]
    [DataRow("Us")]
    public void PreArgProc_Region_US(string region)
    {
        var logger = new TestLogger();
        var args = CheckProcessingSucceeds(
            logger,
            Substitute.For<IFileWrapper>(),
            Substitute.For<IDirectoryWrapper>(),
            "/k:key",
            $"/d:sonar.region={region}");

        args.ServerInfo.Should().BeOfType<CloudHostInfo>().Which.Should().BeEquivalentTo(new CloudHostInfo("https://sonarqube.us", "https://api.sonarqube.us", "us"));
        logger.AssertDebugLogged("Server Url: https://sonarqube.us");
        logger.AssertDebugLogged("Api Url: https://api.sonarqube.us");
        logger.AssertDebugLogged("Is SonarCloud: True");
    }

    [TestMethod]
    [DataRow(" ")]          // "" is PreArgProc_Region_Invalid
    [DataRow("  ")]
    public void PreArgProc_Region_EU(string region)
    {
        var logger = new TestLogger();
        var args = CheckProcessingSucceeds(
            logger,
            Substitute.For<IFileWrapper>(),
            Substitute.For<IDirectoryWrapper>(),
            "/k:key",
            $"/d:sonar.region={region}");

        args.ServerInfo.Should().BeOfType<CloudHostInfo>().Which.Should().BeEquivalentTo(new CloudHostInfo("https://sonarcloud.io", "https://api.sonarcloud.io", string.Empty));
        logger.AssertDebugLogged("Server Url: https://sonarcloud.io");
        logger.AssertDebugLogged("Api Url: https://api.sonarcloud.io");
        logger.AssertDebugLogged("Is SonarCloud: True");
    }

    [TestMethod]
    [DataRow("eu")]
    [DataRow("default")]
    [DataRow("global")]
    [DataRow(@"""us""")]
    public void PreArgProc_Region_Unknown(string region)
    {
        var logger = CheckProcessingFails("/k:key", $"/d:sonar.region={region}");
        logger.AssertErrorLogged($"Unsupported region '{region}'. List of supported regions: 'us'. Please check the 'sonar.region' property.");
    }

    [TestMethod]
    public void PreArgProc_Region_Invalid() =>
        CheckProcessingFails("/k:key", "/d:sonar.region=")
            .AssertErrorLogged("The format of the analysis property sonar.region= is invalid");

    [TestMethod]
    [DataRow("us", null, null, null, typeof(CloudHostInfo), "https://sonarqube.us", "https://api.sonarqube.us")]
    [DataRow("us", null, "https://cloud", "https://api", typeof(CloudHostInfo), "https://cloud", "https://api",
        @"The sonar.region parameter is set to ""us"". The setting will be overriden by one or more of the properties sonar.host.url, sonar.scanner.sonarcloudUrl, or sonar.scanner.apiBaseUrl.")]
    [DataRow("us", null, "https://cloud", null, typeof(CloudHostInfo), "https://cloud", "https://api.sonarqube.us",
        @"The sonar.region parameter is set to ""us"". The setting will be overriden by one or more of the properties sonar.host.url, sonar.scanner.sonarcloudUrl, or sonar.scanner.apiBaseUrl.")]
    [DataRow("us", "https://cloud", "https://cloud", "https://api", typeof(CloudHostInfo), "https://cloud", "https://api",
        @"The sonar.region parameter is set to ""us"". The setting will be overriden by one or more of the properties sonar.host.url, sonar.scanner.sonarcloudUrl, or sonar.scanner.apiBaseUrl.",
        @"The sonar.region parameter is set to ""us"". The setting will be overriden by one or more of the properties sonar.host.url, sonar.scanner.sonarcloudUrl, or sonar.scanner.apiBaseUrl.",
        @"The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set. Please set only 'sonar.scanner.sonarcloudUrl'.")]
    [DataRow("us", "https://host", null, "https://api", typeof(ServerHostInfo), "https://host", "https://api",
        @"The sonar.region parameter is set to ""us"". The setting will be overriden by one or more of the properties sonar.host.url, sonar.scanner.sonarcloudUrl, or sonar.scanner.apiBaseUrl.")]
    [DataRow("us", "https://host", null, null, typeof(ServerHostInfo), "https://host", "https://host/api/v2",
        @"The sonar.region parameter is set to ""us"". The setting will be overriden by one or more of the properties sonar.host.url, sonar.scanner.sonarcloudUrl, or sonar.scanner.apiBaseUrl.")]
    [DataRow(null, null, null, null, typeof(CloudHostInfo), "https://sonarcloud.io", "https://api.sonarcloud.io")]
    [DataRow(null, null, "https://cloud", "https://api", typeof(CloudHostInfo), "https://cloud", "https://api")]
    [DataRow(null, null, "https://cloud", null, typeof(CloudHostInfo), "https://cloud", "https://api.sonarcloud.io")]
    [DataRow(null, "https://cloud", "https://cloud", "https://api", typeof(CloudHostInfo), "https://cloud", "https://api",
        @"The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set. Please set only 'sonar.scanner.sonarcloudUrl'.")]
    [DataRow(null, "https://host", null, "https://api", typeof(ServerHostInfo), "https://host", "https://api")]
    [DataRow(null, "https://host", null, null, typeof(ServerHostInfo), "https://host", "https://host/api/v2")]
    [DataRow(null, "https://SONARQUBE.us/", null, null, typeof(CloudHostInfo), "https://SONARQUBE.us/", "https://api.sonarqube.us")]
    [DataRow(null, null, "https://SONARQUBE.us/", null, typeof(CloudHostInfo), "https://SONARQUBE.us/", "https://api.sonarqube.us")]
    [DataRow(null, "https://sonarqube.us", null, "https://api", typeof(CloudHostInfo), "https://sonarqube.us", "https://api")]
    [DataRow(null, null, "https://sonarqube.us", "https://api", typeof(CloudHostInfo), "https://sonarqube.us", "https://api")]
    [DataRow("US", "https://SONARQUBE.us/", null, null, typeof(CloudHostInfo), "https://SONARQUBE.us/", "https://api.sonarqube.us",
        @"The sonar.region parameter is set to ""US"". The setting will be overriden by one or more of the properties sonar.host.url, sonar.scanner.sonarcloudUrl, or sonar.scanner.apiBaseUrl.")]
    [DataRow(null, "https://SONARCLOUD.io/", null, null, typeof(CloudHostInfo), "https://SONARCLOUD.io/", "https://api.sonarcloud.io")]
    [DataRow(null, null, "https://SONARCLOUD.io/", null, typeof(CloudHostInfo), "https://SONARCLOUD.io/", "https://api.sonarcloud.io")]
    [DataRow(null, "https://sonarcloud.io", null, "https://api", typeof(CloudHostInfo), "https://sonarcloud.io", "https://api")]
    [DataRow(null, null, "https://sonarcloud.io", "https://api", typeof(CloudHostInfo), "https://sonarcloud.io", "https://api")]
    [DataRow("us", "https://sonarcloud.io", null, null, typeof(CloudHostInfo), "https://sonarcloud.io", "https://api.sonarcloud.io",
        @"The sonar.region parameter is set to ""us"". The setting will be overriden by one or more of the properties sonar.host.url, sonar.scanner.sonarcloudUrl, or sonar.scanner.apiBaseUrl.")]
    [DataRow("us", null, "https://sonarcloud.io", null, typeof(CloudHostInfo), "https://sonarcloud.io", "https://api.sonarcloud.io",
        @"The sonar.region parameter is set to ""us"". The setting will be overriden by one or more of the properties sonar.host.url, sonar.scanner.sonarcloudUrl, or sonar.scanner.apiBaseUrl.")]
    public void PreArgProc_Region_Overrides(
        string region,
        string hostOverride,
        string sonarClourUrlOverride,
        string apiOverride,
        Type expectedHostInfoType,
        string expectedHostUri,
        string expectedApiUri,
        params string[] expectedWarnings)
    {
        var logger = new TestLogger();
        var args = CheckProcessingSucceeds(
            logger,
            Substitute.For<IFileWrapper>(),
            Substitute.For<IDirectoryWrapper>(),
            [
                "/k:key",
                .. region is null ? Array.Empty<string>() : [$"/d:{SonarProperties.Region}={region}"],
                .. hostOverride is null ? Array.Empty<string>() : [$"/d:{SonarProperties.HostUrl}={hostOverride}"],
                .. sonarClourUrlOverride is null ? Array.Empty<string>() : [$"/d:{SonarProperties.SonarcloudUrl}={sonarClourUrlOverride}"],
                .. apiOverride is null ? Array.Empty<string>() : [$"/d:{SonarProperties.ApiBaseUrl}={apiOverride}"],
            ]);

        args.ServerInfo.Should().BeOfType(expectedHostInfoType);
        args.ServerInfo.Should().BeEquivalentTo(new { ServerUrl = expectedHostUri, ApiBaseUrl = expectedApiUri, IsSonarCloud = expectedHostInfoType == typeof(CloudHostInfo) });
        logger.AssertDebugLogged($"Server Url: {expectedHostUri}");
        logger.AssertDebugLogged($"Api Url: {expectedApiUri}");
        logger.AssertDebugLogged($"Is SonarCloud: {args.ServerInfo.IsSonarCloud}");
        logger.Warnings.Should().BeEquivalentTo(expectedWarnings);
    }

    [TestMethod]
    [DataRow("/d:sonar.scanner.os=macos", "macos")]
    [DataRow("/d:sonar.scanner.os=Something", "Something")]
    [DataRow("/d:sonar.scanner.os=1", "1")]
    public void PreArgProc_OperatingSystem_Set(string parameter, string expectedValue) =>
        CheckProcessingSucceeds("/k:key", parameter).OperatingSystem.Should().Be(expectedValue);

    [TestMethod]
    public void PreArgProc_OperatingSystem_NotSet()
    {
        var expected = "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            expected = "macos";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            expected = "linux";
        }
        CheckProcessingSucceeds("/k:key").OperatingSystem.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("/d:sonar.scanner.arch=a1", "a1")]
    [DataRow("/d:sonar.scanner.arch=b2", "b2")]
    public void PreArgProc_Architecture_Set(string parameter, string expectedValue) =>
        CheckProcessingSucceeds("/k:key", parameter).Architecture.Should().Be(expectedValue);

    [TestMethod]
    public void PreArgProc_Architecture_NotSet() =>
        CheckProcessingSucceeds("/k:key").Architecture.Should().Be(RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant());

    [DataRow("0123456789.abcdefghijklmnopqrstuvwxyz:-._ABCDEFGHIJKLMNOPQRSTUVWXYZ")] // all valid characters
    [DataRow("a")]
    [DataRow("_")]
    [DataRow(":")]
    [DataRow("-")]
    [DataRow(".")]
    [DataRow(".-_:")]
    [DataRow("0.1")]    // numerics with any other valid character is ok
    [DataRow("_0")]
    [DataRow("0.")]
    [DataRow("myproject")]
    [DataRow("my.Project")]
    [DataRow("my_second_Project")]
    [DataRow("my-other_Project")]
    [TestMethod]
    public void PreArgProc_ProjectKey_Valid(string projectKey) =>
        CheckProcessingSucceeds("/key:" + projectKey, "/name:valid name", "/version:1.0", "/d:sonar.host.url=http://valid")
            .ProjectKey
            .Should().Be(projectKey, "Unexpected project key");

    [DataRow("spaces in name")]
    [DataRow("a\tb")]
    [DataRow("a\rb")]
    [DataRow("a\r\nb")]
    [DataRow("+a")]         // invalid non-alpha characters
    [DataRow("b@")]
    [DataRow("c~")]
    [DataRow("d,")]
    [DataRow("0")]          // single numeric is not ok
    [DataRow("0123456789")] // all numeric is not ok
    [TestMethod]
    public void PreArgProc_ProjectKey_Invalid(string projectKey)
    {
        var logger = CheckProcessingFails("/k:" + projectKey, "/n:valid_name", "/v:1.0", "/d:" + SonarProperties.HostUrl + "=http://validUrl");
        logger.AssertErrorsLogged(1);
        logger.AssertSingleErrorExists("Invalid project key. Allowed characters are alphanumeric, '-', '_', '.' and ':', with at least one non-digit.");
    }

    [DataRow("unrecog2", "unrecog1", "/p:key=value", "")]   // /p: is no longer supported - should be /d:
    [DataRow("/key=k1", "/name=n1", "/version=v1")]         // Arguments using the wrong separator i.e. /k=k1 instead of /k:k1
    [TestMethod]
    public void PreArgProc_UnrecognisedArguments(params string[] args)
    {
        var logger = CheckProcessingFails([.. args, "/key:k1", "/name:n1", "/version:v1"]);

        logger.AssertNoErrorsLogged("/key:");
        logger.AssertNoErrorsLogged("/name:");
        logger.AssertNoErrorsLogged("/version:");
        foreach (var arg in args.Where(x => x is not "")) // we still log an error for "", but we cannot match on it
        {
            logger.AssertSingleErrorExists(arg);
        }
        logger.AssertErrorsLogged(args.Length);
    }

    [DataRow(TargetsInstaller.DefaultInstallSetting, null)]
    [DataRow(true, "true")]
    [DataRow(true, "TrUe")]
    [DataRow(false, "false")]
    [DataRow(false, "falSE")]
    [TestMethod]
    public void ArgProc_InstallTargets_Valid(bool expected, string installValue)
    {
        var args = new[] { "/key:my.key", "/name:my name", "/version:1.0", "/d:sonar.host.url=foo" };
        if (installValue is not null)
        {
            args = [.. args, $"/install:{installValue}"];
        }
        CheckProcessingSucceeds(args).InstallLoaderTargets.Should().Be(expected);
    }

    [DataRow("1")]
    [DataRow("")]
    [DataRow("\" \"")]
    [TestMethod]
    public void ArgProc_InstallTargets_Invalid(string installValue)
    {
        var logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", $"/install:{installValue}");
        logger.AssertErrorsLogged(1);
        logger.AssertSingleErrorExists("/install");
    }

    [TestMethod]
    public void ArgProc_InstallTargets_Duplicate()
    {
        var logger = CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", "/install:true", "/install:false");
        // we expect the error to include the first value and the duplicate argument
        logger.AssertErrorsLogged(1);
        logger.AssertSingleErrorExists("/install");
    }

    [TestMethod]
    public void PreArgProc_PropertiesFileSpecifiedOnCommandLine_Exists()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var propertiesFilePath = Path.Combine(testDir, "mysettings.txt");
        var properties = new AnalysisProperties
        {
            new("key1", "value1"), new(SonarProperties.HostUrl, "url") // required property
        };
        properties.Save(propertiesFilePath);

        var result = CheckProcessingSucceeds("/k:key", "/n:name", "/v:version", "/s:" + propertiesFilePath);

        AssertExpectedValues("key", "name", "version", result);
        AssertExpectedPropertyValue("key1", "value1", result);
    }

    [TestMethod]
    public void PreArgProc_PropertiesFileSpecifiedOnCommandLine_DoesNotExist()
    {
        var logger = CheckProcessingFails("/k:key", "/n:name", "/v:version", "/s:some/File/Path");
        logger.AssertErrorsLogged(1);
        logger.AssertSingleErrorExists("Unable to find the analysis settings file", "Please fix the path to this settings file.");
    }

    [TestMethod]
    public void PreArgProc_With_PropertiesFileSpecifiedOnCommandLine_Organization_Set_Only_In_It_Should_Fail()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var propertiesFilePath = Path.Combine(testDir, "mysettings.txt");
        var properties = new AnalysisProperties
        {
            new(SonarProperties.Organization, "myorg1"), new(SonarProperties.HostUrl, "url") // required property
        };
        properties.Save(propertiesFilePath);

        var logger = CheckProcessingFails("/k:key", "/n:name", "/v:version", "/s:" + propertiesFilePath);
        logger.AssertErrorsLogged(1);
        logger.AssertSingleErrorExists("sonar.organization parameter has been detected in the provided SonarQube.Analysis.xml config file. Please pass it in the command line instead, using /o: flag");
    }

    [DataRow("/key:my.key", "/name:my name", "/version:1.0")]
    [DataRow("/v:1.0", "/k:my.key", "/n:my name")]
    [DataRow("/k:my.key", "/v:1.0", "/n:my name")]
    [TestMethod]
    public void PreArgProc_Aliases_Valid(params string[] args) =>
        AssertExpectedValues("my.key", "my name", "1.0", CheckProcessingSucceeds([.. args, "/d:sonar.host.url=foo"]));

    [TestMethod]
    // Full names, wrong case -> ignored
    public void PreArgProc_Aliases_Invalid()
    {
        var logger = CheckProcessingFails("/KEY:my.key", "/nAme:my name", "/versIOn:1.0", "/d:sonar.host.url=foo");
        logger.AssertSingleErrorExists("/KEY:my.key");
        logger.AssertSingleErrorExists("/nAme:my name");
        logger.AssertSingleErrorExists("/versIOn:1.0");
    }

    [DataRow(new string[] { "/key:my.key", "/name:my name", "/version:1.2", "/k:key2" }, new string[] { "/k:key2", "my.key" })]
    [DataRow(new string[] { "/key:my.key", "/name:my name", "/version:1.2", "/name:dupName" }, new string[] { "/name:dupName", "my name" })]
    [DataRow(new string[] { "/key:my.key", "/name:my name", "/version:1.2", "/v:version2.0" }, new string[] { "/v:version2.0", "1.2" })]
    [TestMethod]
    public void PreArgProc_SingleDuplicate_ProcessingFails(string[] args, string[] expectedError)
    {
        var logger = CheckProcessingFails(args);
        logger.AssertErrorsLogged(1);
        logger.AssertSingleErrorExists(expectedError); // we expect the error to include the first value and the duplicate argument
    }

    [TestMethod]
    public void PreArgProc_MultipleDuplicates_ProcessingFails()
    {
        var logger = CheckProcessingFails("/key:my.key", "/k:k2", "/k:key3");
        logger.AssertSingleErrorExists("/k:k2", "my.key"); // Warning about key appears twice
        logger.AssertSingleErrorExists("/k:key3", "my.key");
        logger.AssertErrorsLogged(2);
    }

    [TestMethod]
    public void PreArgProc_DynamicSettings()
    {
        var result = CheckProcessingSucceeds(
            // Non-dynamic values
            "/key:my.key",
            "/name:my name",
            "/version:1.2",
            // Dynamic values
            "/d:sonar.host.url=required value",
            "/d:key1=value1",
            "/d:key2=value two with spaces");

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
        var logger = CheckProcessingFails(
            "/key:my.key",
            "/name:my name",
            "/version:1.2",
            "/d:invalid1 =aaa",
            "/d:notkeyvalue",
            "/d: spacebeforekey=bb",
            "/d:missingvalue=",
            "/d:validkey=validvalue");

        logger.AssertSingleErrorExists("invalid1 =aaa");
        logger.AssertSingleErrorExists("notkeyvalue");
        logger.AssertSingleErrorExists(" spacebeforekey=bb");
        logger.AssertSingleErrorExists("missingvalue=");
        logger.AssertErrorsLogged(4);
    }

    [TestMethod]
    public void PreArgProc_DynamicSettings_Duplicates()
    {
        var logger = CheckProcessingFails(
            "/key:my.key",
            "/name:my name",
            "/version:1.2",
            "/d:dup1=value1",
            "/d:dup1=value2",
            "/d:dup2=value3",
            "/d:dup2=value4",
            "/d:unique=value5");

        logger.AssertSingleErrorExists("dup1=value2", "value1");
        logger.AssertSingleErrorExists("dup2=value4", "value3");
        logger.AssertErrorsLogged(2);
    }

    [TestMethod]
    public void PreArgProc_Arguments_Duplicates_WithDifferentFlagsPrefixes()
    {
        var logger = CheckProcessingFails(
            "/key:my.key",
            "/name:my name",
            "/version:1.2",
            "-version:1.2",
            "/d:dup1=value1",
            "-d:dup1=value2",
            "/d:dup2=value3",
            "/d:dup2=value4",
            "/d:unique=value5");

        logger.AssertErrorsLogged(3);
        logger.AssertSingleErrorExists("dup1=value2", "value1");
        logger.AssertSingleErrorExists("dup2=value4", "value3");
        logger.AssertSingleErrorExists("version:1.2", "1.2");
    }

    [TestMethod]
    public void PreArgProc_DynamicSettings_Duplicates_WithDifferentFlagsPrefixes()
    {
        var logger = CheckProcessingFails("/key:my.key", "/name:my name", "/d:dup1=value1", "-d:dup1=value2", "/d:dup2=value3", "/d:dup2=value4", "/d:unique=value5");

        logger.AssertSingleErrorExists("dup1=value2", "value1");
        logger.AssertSingleErrorExists("dup2=value4", "value3");
        logger.AssertErrorsLogged(2);
    }

    [DataRow(new string[] { "/d:sonar.projectKey=value1" }, new string[] { SonarProperties.ProjectKey, "/k" })]
    [DataRow(new string[] { "/d:sonar.projectName=value1" }, new string[] { SonarProperties.ProjectName, "/n" })]
    [DataRow(new string[] { "/d:sonar.projectVersion=value1" }, new string[] { SonarProperties.ProjectVersion, "/v" })]
    [DataRow(new string[] { "/organization:my_org", "/d:sonar.organization=value1" }, new string[] { SonarProperties.Organization, "/o" })]
    [DataRow(new string[] { "/key:my.key", "/name:my name", "/version:1.2", "/d:sonar.working.directory=value1" }, new string[] { SonarProperties.WorkingDirectory })]
    [TestMethod]
    public void PreArgProc_Disallowed_DynamicSettings_ProcessingFails(string[] args, string[] errors) =>
        CheckProcessingFails([.. args, "/key:my.key", "/name:my name", "/version:1.2"]).AssertSingleErrorExists(errors);

    [DataRow("/organization:my_org", "my_org")]
    [DataRow("/o:my_org", "my_org")]
    [TestMethod]
    public void PreArgProc_Organization(string arg, string expected) =>
        CheckProcessingSucceeds("/key:my.key", arg).Organization.Should().Be(expected);

    [TestMethod]
    public void PreArgProc_Organization_NotSet() =>
        CheckProcessingSucceeds("/key:my.key").Organization.Should().BeNullOrEmpty();

    [TestMethod]
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
        var logger = new TestLogger();
        const string warningTemplate = "The specified value `{0}` for `{1}` cannot be parsed. The default value of {2}s will be used. "
            + "Please remove the parameter or specify the value in seconds, greater than 0.";
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

    [TestMethod]
    [DataRow(@"C:\Program Files\Java\jdk1.6.0_30\bin\java.exe")]
    [DataRow(@"C:Program Files\Java\jdk1.6.0_30\bin\java.exe")]
    [DataRow(@"\jdk1.6.0_30\bin\java.exe")]
    public void PreArgProc_JavaExePath_SetValid(string javaExePath)
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(javaExePath).Returns(true);
        CheckProcessingSucceeds(new TestLogger(), fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key", $"/d:sonar.scanner.javaExePath={javaExePath}").JavaExePath.Should().Be(javaExePath);
    }

    [TestMethod]
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

    [TestMethod]
    [DataRow("true", true)]
    [DataRow("True", true)]
    [DataRow("false", false)]
    [DataRow("False", false)]
    public void PreArgProc_SkipJreProvisioning_SetValid(string skipJreProvisioning, bool result) =>
        CheckProcessingSucceeds("/k:key", $"/d:sonar.scanner.skipJreProvisioning={skipJreProvisioning}").SkipJreProvisioning.Should().Be(result);

    [TestMethod]
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

    [TestMethod]
    public void PreArgProc_EngineJarPath_SetValid()
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists("scanner-engine.jar").Returns(true);
        CheckProcessingSucceeds(new TestLogger(), fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key", "/d:sonar.scanner.engineJarPath=scanner-engine.jar")
            .EngineJarPath
            .Should().Be("scanner-engine.jar");
    }

    [TestMethod]
    public void PreArgProc_EngineJarPath_SetInvalid()
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists("scanner-engine.jar").Returns(false);
        var logger = CheckProcessingFails(fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key", "/d:sonar.scanner.engineJarPath=scanner-engine.jar");
        logger.AssertErrorLogged("The argument 'sonar.scanner.engineJarPath' contains an invalid path. Please make sure the path is correctly pointing to the scanner engine jar.");
    }

    [TestMethod]
    public void PreArgProc_EngineJarPath_NotSet() =>
        CheckProcessingSucceeds("/k:key").EngineJarPath.Should().BeNull();

    [TestMethod]
    [DataRow("true", true)]
    [DataRow("True", true)]
    [DataRow("false", false)]
    [DataRow("False", false)]
    public void PreArgProc_ScanAllAnalysis_SetValid(string scanAll, bool result) =>
        CheckProcessingSucceeds("/k:key", $"/d:sonar.scanner.scanAll={scanAll}").ScanAllAnalysis.Should().Be(result);

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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
        logger.AssertErrorLogged($"The attempt to create the directory specified by 'sonar.userHome' at '{path}' failed with error 'Directory creation failed.'. "
            + "Provide a valid path for 'sonar.userHome' to a directory that can be created.");
    }

    [TestMethod]
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
        var result = CheckProcessingSucceeds(
            logger,
            fileWrapper,
            Substitute.For<IDirectoryWrapper>(),
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
        result.TruststorePassword.Should().Be("changeit");
    }

    [TestMethod]
    public void PreArgProc_Fail_TruststorePassword_Only()
    {
        var logger = CheckProcessingFails("/k:key", @"/d:sonar.scanner.truststorePassword=changeit");
        logger.Errors.Should().Contain("'sonar.scanner.truststorePath' must be specified when 'sonar.scanner.truststorePassword' is provided.");
    }

    [TestMethod]
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
    public void PreArgProc_TruststorePathAndPassword_DefaultValues()
    {
        var logger = new TestLogger();
        var truststorePath = Path.Combine(".sonar", "ssl", "truststore.p12");
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(Arg.Any<string>()).Returns(true);
        fileWrapper.Open(Arg.Any<string>()).Returns(new MemoryStream());

        var result = CheckProcessingSucceeds(logger, fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key");
        logger.DebugMessages.Should().Contain("No truststore provided; attempting to use the default location.");
        logger.DebugMessages.Should().ContainMatch($"Fall back on using the truststore from the default location at *{truststorePath}.");
        result.TruststorePath.Should().EndWith(truststorePath);
        result.TruststorePassword.Should().Be("changeit");
    }

    [TestMethod]
    public void PreArgProc_TruststorePathAndPassword_DefaultValuesTruststoreNotFound()
    {
        var logger = new TestLogger();
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(Arg.Any<string>()).Returns(false);

        var result = CheckProcessingSucceeds(logger, fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key");
        logger.DebugMessages.Should().Contain("No truststore provided; attempting to use the default location.");
        logger.DebugMessages.Should().ContainMatch("No truststore found at the default location; proceeding without a truststore.");
        result.TruststorePath.Should().BeNull();
        result.TruststorePassword.Should().BeNull();
    }

    [TestMethod]
    public void PreArgProc_TruststorePathAndPassword_DefaultValuesTruststoreCannotOpenPasswordProvided()
    {
        var logger = new TestLogger();
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(Arg.Any<string>()).Returns(true);
        fileWrapper.Open(Arg.Any<string>()).Throws(new IOException());

        var result = CheckProcessingSucceeds(logger, fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key", "/d:sonar.scanner.truststorePassword=changeit");
        logger.DebugMessages.Should().Contain("No truststore provided; attempting to use the default location.");
        logger.DebugMessages.Should().ContainMatch("No truststore found at the default location; proceeding without a truststore.");
        result.TruststorePath.Should().BeNull();
        result.TruststorePassword.Should().BeNull();
    }

    [TestMethod]
    [DataRow(typeof(ArgumentException))]
    [DataRow(typeof(ArgumentNullException))]
    [DataRow(typeof(ArgumentOutOfRangeException))]
    [DataRow(typeof(DirectoryNotFoundException))]
    [DataRow(typeof(FileNotFoundException))]
    [DataRow(typeof(IOException))]
    [DataRow(typeof(NotSupportedException))]
    [DataRow(typeof(PathTooLongException))]
    [DataRow(typeof(UnauthorizedAccessException))]
    public void PreArgProc_TruststorePathAndPassword_DefaultValuesTruststoreCannotOpen(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType);
        var logger = new TestLogger();
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(Arg.Any<string>()).Returns(true);
        fileWrapper.Open(Arg.Any<string>()).Throws(exception);

        var result = CheckProcessingSucceeds(logger, fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key");
        result.TruststorePath.Should().BeNull();
        result.TruststorePassword.Should().BeNull();
        logger.DebugMessages.Should()
            .ContainMatch(
                $"The sonar.scanner.truststorePath file '*.sonar{Separator}ssl{Separator}truststore.p12' can not be opened. Details: {exceptionType.FullName}: {exception.Message}");
    }

    [TestMethod]
    public void PreArgProc_TruststorePathAndPassword_DefaultValuesTruststoreNotFoundPasswordProvided()
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(Arg.Any<string>()).Returns(false);

        var logger = CheckProcessingFails(fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key", "/d:sonar.scanner.truststorePassword=\"changeit\"");

        logger.AssertErrorLogged("'sonar.scanner.truststorePath' must be specified when 'sonar.scanner.truststorePassword' is provided.");
    }

    [TestMethod]
    [DataRow(@"C:\sonar")]
    [DataRow(@"""C:\sonar""")]
    [DataRow(@"'C:\sonar'")]
    public void PreArgProc_TruststorePathAndPasswordSonarUserHomeEnvSet_DefaultValues(string sonarUserHome)
    {
        var truststorePath = Path.Combine(sonarUserHome.Trim('\'', '"'), "ssl", "truststore.p12");
        var logger = new TestLogger();
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(Arg.Any<string>()).Returns(true);
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_USER_HOME", sonarUserHome);

        var result = CheckProcessingSucceeds(logger, fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key");
        logger.DebugMessages.Should().Contain("No truststore provided; attempting to use the default location.");
        logger.DebugMessages.Should().Contain($"Fall back on using the truststore from the default location at {truststorePath}.");
        result.TruststorePath.Should().Be(truststorePath);
        result.TruststorePassword.Should().Be("changeit");
    }

    [TestMethod]
    [DataRow(@"C:\sonar")]
    [DataRow(@"""C:\sonar""")]
    [DataRow(@"'C:\sonar'")]
    public void PreArgProc_TruststorePathAndPasswordSonarUserHomePropSet_DefaultValues(string sonarUserHome)
    {
        var truststorePath = Path.Combine(sonarUserHome.Trim('\'', '"'), "ssl", "truststore.p12");
        var logger = new TestLogger();
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(Arg.Any<string>()).Returns(true);

        var result = CheckProcessingSucceeds(logger, fileWrapper, Substitute.For<IDirectoryWrapper>(), "/k:key", $"/d:sonar.userHome={sonarUserHome}");
        logger.DebugMessages.Should().Contain("No truststore provided; attempting to use the default location.");
        logger.DebugMessages.Should().Contain($"Fall back on using the truststore from the default location at {truststorePath}.");
        result.TruststorePath.Should().Be(truststorePath);
        result.TruststorePassword.Should().Be("changeit");
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

    private static ProcessedArgs TryProcessArgsIsolatedFromEnvironment(string[] commandLineArgs, IFileWrapper fileWrapper, IDirectoryWrapper directoryWrapper, ILogger logger)
    {
        // Make sure the test isn't affected by the hosting environment
        // The SonarCloud AzDO extension sets additional properties in an environment variable that
        // would be picked up by the argument processor
        using var scope = new EnvironmentVariableScope().SetVariable(EnvironmentVariables.SonarQubeScannerParams, null);
        return ArgumentProcessor.TryProcessArgs(commandLineArgs, fileWrapper, directoryWrapper, logger);
    }
}
