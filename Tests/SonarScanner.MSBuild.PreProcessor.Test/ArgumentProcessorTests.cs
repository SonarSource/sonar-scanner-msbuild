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
    public void PreArgProc_NullRuntimeThrows() =>
        FluentActions.Invoking(() => ArgumentProcessor.TryProcessArgs(null, null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("runtime");

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
    public void PreArgProc_NoArguments_ProcessingFails() =>
        CheckProcessingFails()
            .Should().HaveErrorLoggedOnce("A required argument is missing: /key:[SonarQube/SonarCloud project key]")
            .And.HaveErrorsLogged(1);

    [TestMethod]
    public void PreArgProc_KeyHasNoValue_ProcessingFails() =>
        CheckProcessingFails("/key:")
            .Should().HaveErrorLoggedOnce("A required argument is missing: /key:[SonarQube/SonarCloud project key]")
            .And.HaveErrorsLogged(1);

    [TestMethod]
    public void PreArgProc_HostAndSonarcloudUrlError() =>
        CheckProcessingFails("/k:key", "/d:sonar.host.url=firstUrl", "/d:sonar.scanner.sonarcloudUrl=secondUrl").Logger
            .Should().HaveErrors("The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set and are different. "
            + "Please set either 'sonar.host.url' for SonarQube or 'sonar.scanner.sonarcloudUrl' for SonarCloud.");

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
        var runtime = new TestRuntime();
        CheckProcessingSucceeds(runtime, "/k:key", "/d:sonar.scanner.apiBaseUrl=test").ServerInfo.ApiBaseUrl.Should().Be("test");
        runtime.Should().HaveDebugsLogged(
            "Server Url: https://sonarcloud.io",
            "Api Url: test",
            "Is SonarCloud: True");
    }

    [TestMethod]
    [DataRow("test")]
    [DataRow("https://sonarcloud.io")]
    [DataRow("https://sonar-test.io")]
    [DataRow("https://www.sonarcloud.io")]
    public void PreArgProc_ApiBaseUrl_NotSet_SonarCloudDefault(string sonarcloudUrl)
    {
        var runtime = new TestRuntime();
        CheckProcessingSucceeds(runtime, "/k:key", $"/d:sonar.scanner.sonarcloudUrl={sonarcloudUrl}").ServerInfo.ApiBaseUrl
            .Should().Be("https://api.sonarcloud.io", because: "it is not so easy to transform the api url for a user specified sonarcloudUrl (Subdomain change).");
        runtime.Should().HaveDebugsLogged(
            $"Server Url: {sonarcloudUrl}",
            "Api Url: https://api.sonarcloud.io",
            "Is SonarCloud: True");
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
        var runtime = new TestRuntime();
        CheckProcessingSucceeds(runtime, "/k:key", $"/d:sonar.host.url={hostUri}").ServerInfo.ApiBaseUrl.Should().Be(expectedApiUri);
        runtime.Should().HaveDebugsLogged(
            $"Server Url: {hostUri}",
            $"Api Url: {expectedApiUri}",
            "Is SonarCloud: False");
    }

    [TestMethod]
    [DataRow("us")]
    [DataRow("US")]
    [DataRow("uS")]
    [DataRow("Us")]
    public void PreArgProc_Region_US(string region)
    {
        var runtime = new TestRuntime();
        CheckProcessingSucceeds(runtime, "/k:key", $"/d:sonar.region={region}").ServerInfo
            .Should().Be(new CloudHostInfo("https://sonarqube.us", "https://api.sonarqube.us", "us"));
        runtime.Should().HaveDebugsLogged(
            "Server Url: https://sonarqube.us",
            "Api Url: https://api.sonarqube.us",
            "Is SonarCloud: True");
    }

    [TestMethod]
    [DataRow(" ")]          // "" is PreArgProc_Region_Invalid
    [DataRow("  ")]
    public void PreArgProc_Region_EU(string region)
    {
        var runtime = new TestRuntime();
        CheckProcessingSucceeds(runtime, "/k:key", $"/d:sonar.region={region}").ServerInfo.Should().BeOfType<CloudHostInfo>()
            .Which.Should().BeEquivalentTo(new CloudHostInfo("https://sonarcloud.io", "https://api.sonarcloud.io", string.Empty));
        runtime.Should().HaveDebugsLogged(
            "Server Url: https://sonarcloud.io",
            "Api Url: https://api.sonarcloud.io",
            "Is SonarCloud: True");
    }

    [TestMethod]
    [DataRow("eu")]
    [DataRow("default")]
    [DataRow("global")]
    [DataRow(@"""us""")]
    public void PreArgProc_Region_Unknown(string region) =>
        CheckProcessingFails("/k:key", $"/d:sonar.region={region}").Logger
            .Should().HaveErrors($"Unsupported region '{region}'. List of supported regions: 'us'. Please check the 'sonar.region' property.");

    [TestMethod]
    public void PreArgProc_Region_Invalid() =>
        CheckProcessingFails("/k:key", "/d:sonar.region=").Logger
            .Should().HaveErrors("The format of the analysis property sonar.region= is invalid");

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
    public void PreArgProc_Region_Overrides(string region,
                                            string hostOverride,
                                            string sonarClourUrlOverride,
                                            string apiOverride,
                                            Type expectedHostInfoType,
                                            string expectedHostUri,
                                            string expectedApiUri,
                                            params string[] expectedWarnings)
    {
        var runtime = new TestRuntime();
        var args = CheckProcessingSucceeds(
            runtime,
            [
                "/k:key",
                .. region is null ? Array.Empty<string>() : [$"/d:{SonarProperties.Region}={region}"],
                .. hostOverride is null ? Array.Empty<string>() : [$"/d:{SonarProperties.HostUrl}={hostOverride}"],
                .. sonarClourUrlOverride is null ? Array.Empty<string>() : [$"/d:{SonarProperties.SonarcloudUrl}={sonarClourUrlOverride}"],
                .. apiOverride is null ? Array.Empty<string>() : [$"/d:{SonarProperties.ApiBaseUrl}={apiOverride}"],
            ]);

        args.ServerInfo.Should().BeOfType(expectedHostInfoType);
        args.ServerInfo.Should().BeEquivalentTo(new { ServerUrl = expectedHostUri, ApiBaseUrl = expectedApiUri, IsSonarCloud = expectedHostInfoType == typeof(CloudHostInfo) });
        runtime.Should().HaveDebugsLogged(
            $"Server Url: {expectedHostUri}",
            $"Api Url: {expectedApiUri}",
            $"Is SonarCloud: {args.ServerInfo.IsSonarCloud}");
        runtime.Logger.Warnings.Should().BeEquivalentTo(expectedWarnings);
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
        var runtime = new TestRuntime { OperatingSystem = new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()) };
        CheckProcessingSucceeds(runtime, "/k:key").OperatingSystem.Should().Be(expected);
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
        var runtime = CheckProcessingFails("/k:" + projectKey, "/n:valid_name", "/v:1.0", "/d:" + SonarProperties.HostUrl + "=http://validUrl");
        runtime.Should().HaveErrorLoggedOnce("Invalid project key. Allowed characters are alphanumeric, '-', '_', '.' and ':', with at least one non-digit.")
            .And.HaveErrorsLogged(1);
    }

    [DataRow("unrecog2", "unrecog1", "/p:key=value", "")]   // /p: is no longer supported - should be /d:
    [DataRow("/key=k1", "/name=n1", "/version=v1")]         // Arguments using the wrong separator i.e. /k=k1 instead of /k:k1
    [TestMethod]
    public void PreArgProc_UnrecognisedArguments(params string[] args)
    {
        var runtime = CheckProcessingFails([.. args, "/key:k1", "/name:n1", "/version:v1"]);

        runtime.Should().NotHaveErrorLogged("/key:")
            .And.NotHaveErrorLogged("/name:")
            .And.NotHaveErrorLogged("/version:");
        foreach (var arg in args.Where(x => x is not "")) // we still log an error for "", but we cannot match on it
        {
            runtime.Should().HaveErrorLoggedOnce("Unrecognized command line argument: " + arg);
        }
        runtime.Should().HaveErrorsLogged(args.Length);
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
    public void ArgProc_InstallTargets_Invalid(string installValue) =>
        CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", $"/install:{installValue}")
            .Should().HaveErrorLoggedOnce($"""Invalid value for /install: {installValue}. Valid values are "true" or "false".""")
            .And.HaveErrorsLogged(1);

    [TestMethod]
    public void ArgProc_InstallTargets_Duplicate() =>
        CheckProcessingFails("/key:my.key", "/name:my name", "/version:1.2", "/install:true", "/install:false")
            .Should().HaveErrorLoggedOnce("A value has already been supplied for this argument: /install:false. Existing: 'true'")
            .And.HaveErrorsLogged(1);

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
        var runtime = CheckProcessingFails("/k:key", "/n:name", "/v:version", "/s:some/File/Path");
        runtime.Should().HaveErrorsLogged(1);
        runtime.Logger.Errors.Should().Contain(x => x.Contains("Unable to find the analysis settings file") && x.Contains("Please fix the path to this settings file."));
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

        var runtime = CheckProcessingFails("/k:key", "/n:name", "/v:version", "/s:" + propertiesFilePath);
        runtime.Should()
            .HaveErrorLoggedOnce("sonar.organization parameter has been detected in the provided SonarQube.Analysis.xml config file. Please pass it in the command line instead, using /o: flag.")
            .And.HaveErrorsLogged(1);
    }

    [DataRow("/key:my.key", "/name:my name", "/version:1.0")]
    [DataRow("/v:1.0", "/k:my.key", "/n:my name")]
    [DataRow("/k:my.key", "/v:1.0", "/n:my name")]
    [TestMethod]
    public void PreArgProc_Aliases_Valid(params string[] args) =>
        AssertExpectedValues("my.key", "my name", "1.0", CheckProcessingSucceeds([.. args, "/d:sonar.host.url=foo"]));

    [TestMethod]
    // Full names, wrong case -> ignored
    public void PreArgProc_Aliases_Invalid() =>
        CheckProcessingFails("/KEY:my.key", "/nAme:my name", "/versIOn:1.0", "/d:sonar.host.url=foo")
            .Should().HaveErrorsLogged(
            "Unrecognized command line argument: /KEY:my.key",
            "Unrecognized command line argument: /nAme:my name",
            "Unrecognized command line argument: /versIOn:1.0");

    [DataRow(new[] { "/key:my.key", "/name:my name", "/version:1.2", "/k:key2" }, "A value has already been supplied for this argument: /k:key2. Existing: 'my.key'")]
    [DataRow(new[] { "/key:my.key", "/name:my name", "/version:1.2", "/name:dupName" }, "A value has already been supplied for this argument: /name:dupName. Existing: 'my name'")]
    [DataRow(new[] { "/key:my.key", "/name:my name", "/version:1.2", "/v:version2.0" }, "A value has already been supplied for this argument: /v:version2.0. Existing: '1.2'")]
    [TestMethod]
    public void PreArgProc_SingleDuplicate_ProcessingFails(string[] args, string expectedError) =>
        CheckProcessingFails(args)
            .Should().HaveErrorLoggedOnce(expectedError)
            .And.HaveErrorsLogged(1);

    [TestMethod]
    public void PreArgProc_MultipleDuplicates_ProcessingFails() =>
        CheckProcessingFails("/key:my.key", "/k:k2", "/k:key3")
            .Should().HaveErrorsLogged(
            "A value has already been supplied for this argument: /k:k2. Existing: 'my.key'",
            "A value has already been supplied for this argument: /k:key3. Existing: 'my.key'")
            .And.HaveErrorsLogged(2);

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

        result.AllProperties().Should().NotBeNull()
            .And.HaveCount(3);
    }

    [TestMethod]
    public void PreArgProc_DynamicSettings_Invalid()
    {
        var runtime = CheckProcessingFails(
            "/key:my.key",
            "/name:my name",
            "/version:1.2",
            "/d:invalid1 =aaa",
            "/d:notkeyvalue",
            "/d: spacebeforekey=bb",
            "/d:missingvalue=",
            "/d:validkey=validvalue");

        runtime.Should().HaveErrorsLogged(
            "The format of the analysis property invalid1 =aaa is invalid",
            "The format of the analysis property notkeyvalue is invalid",
            "The format of the analysis property  spacebeforekey=bb is invalid",
            "The format of the analysis property missingvalue= is invalid")
            .And.HaveErrorsLogged(4);
    }

    [TestMethod]
    public void PreArgProc_DynamicSettings_Duplicates()
    {
        var runtime = CheckProcessingFails(
            "/key:my.key",
            "/name:my name",
            "/version:1.2",
            "/d:dup1=value1",
            "/d:dup1=value2",
            "/d:dup2=value3",
            "/d:dup2=value4",
            "/d:unique=value5");

        runtime.Should().HaveErrorsLogged(
            "A value has already been supplied for this property. Key: dup1=value2, existing value: value1",
            "A value has already been supplied for this property. Key: dup2=value4, existing value: value3")
            .And.HaveErrorsLogged(2);
    }

    [TestMethod]
    public void PreArgProc_Arguments_Duplicates_WithDifferentFlagsPrefixes() =>
        CheckProcessingFails(
            "/key:my.key",
            "/name:my name",
            "/version:1.2",
            "-version:1.2",
            "/d:dup1=value1",
            "-d:dup1=value2",
            "/d:dup2=value3",
            "/d:dup2=value4",
            "/d:unique=value5")
            .Should().HaveErrorsLogged(
            "A value has already been supplied for this property. Key: dup1=value2, existing value: value1",
            "A value has already been supplied for this property. Key: dup2=value4, existing value: value3",
            "A value has already been supplied for this argument: -version:1.2. Existing: '1.2'")
            .And.HaveErrorsLogged(3);

    [TestMethod]
    public void PreArgProc_DynamicSettings_Duplicates_WithDifferentFlagsPrefixes() =>
        CheckProcessingFails("/key:my.key", "/name:my name", "/d:dup1=value1", "-d:dup1=value2", "/d:dup2=value3", "/d:dup2=value4", "/d:unique=value5")
            .Should().HaveErrorsLogged(
            "A value has already been supplied for this property. Key: dup1=value2, existing value: value1",
            "A value has already been supplied for this property. Key: dup2=value4, existing value: value3")
            .And.HaveErrorsLogged(2);

    [DataRow(
        new[] { "/d:sonar.projectKey=value1" },
        "Please use the parameter prefix '/k:' to define the key of the project instead of injecting this key with the help of the 'sonar.projectKey' property.")]
    [DataRow(
        new[] { "/d:sonar.projectName=value1" },
        "Please use the parameter prefix '/n:' to define the name of the project instead of injecting this name with the help of the 'sonar.projectName' property.")]
    [DataRow(
        new[] { "/d:sonar.projectVersion=value1" },
        "Please use the parameter prefix '/v:' to define the version of the project instead of injecting this version with the help of the 'sonar.projectVersion' property.")]
    [DataRow(
        new[] { "/organization:my_org", "/d:sonar.organization=value1" },
        "Please use the parameter prefix '/o:' to define the organization of the project instead of injecting this organization with the help of the 'sonar.organization' property.")]
    [DataRow(
        new[] { "/key:my.key", "/name:my name", "/version:1.2", "/d:sonar.working.directory=value1" },
        "The property 'sonar.working.directory' is automatically set by the SonarScanner for .NET and cannot be overridden on the command line.")]
    [TestMethod]
    public void PreArgProc_Disallowed_DynamicSettings_ProcessingFails(string[] args, string error) =>
        CheckProcessingFails([.. args, "/key:my.key", "/name:my name", "/version:1.2"]).Logger
            .Should().HaveErrorOnce(error);

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
        var runtime = new TestRuntime();
        const string warningTemplate = "The specified value `{0}` for `{1}` cannot be parsed. The default value of {2}s will be used. "
            + "Please remove the parameter or specify the value in seconds, greater than 0.";
        var args = CheckProcessingSucceeds(runtime, ["/key:k", .. timeOuts]);
        args.HttpTimeout.Should().Be(TimeSpan.FromSeconds(expectedTimeoutSeconds));
        if (expectedWarningParts is { } warningParts)
        {
            runtime.Should().HaveWarningsLogged(string.Format(warningTemplate, warningParts));
        }
        else
        {
            runtime.Should().HaveNoWarningsLogged();
        }
    }

    [TestMethod]
    [DataRow(@"C:\Program Files\Java\jdk1.6.0_30\bin\java.exe")]
    [DataRow(@"C:Program Files\Java\jdk1.6.0_30\bin\java.exe")]
    [DataRow(@"\jdk1.6.0_30\bin\java.exe")]
    public void PreArgProc_JavaExePath_SetValid(string javaExePath)
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(javaExePath).Returns(true);
        CheckProcessingSucceeds(runtime, "/k:key", $"/d:sonar.scanner.javaExePath={javaExePath}").JavaExePath.Should().Be(javaExePath);
    }

    [TestMethod]
    [DataRow(@"jdk1.6.0_30\bin\java.exe")]
    [DataRow(@"C:Program Files\Java\jdk1.6.0_30\bin\java")]
    [DataRow(@"not a path")]
    [DataRow(@" ")]
    public void PreArgProc_JavaExePath_SetInvalid(string javaExePath)
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(javaExePath).Returns(false);
        CheckProcessingFails(runtime, "/k:key", $"/d:sonar.scanner.javaExePath={javaExePath}");
        runtime.Should().HaveErrorsLogged("The argument 'sonar.scanner.javaExePath' contains an invalid path. Please make sure the path is correctly pointing to the java executable.");
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
    public void PreArgProc_SkipJreProvisioning_SetInvalid(string skipJreProvisioning) =>
        CheckProcessingFails("/k:key", $"/d:sonar.scanner.skipJreProvisioning={skipJreProvisioning}").Logger
            .Should().HaveErrors("The argument 'sonar.scanner.skipJreProvisioning' has an invalid value. Please ensure it is set to either 'true' or 'false'.");

    [TestMethod]
    public void PreArgProc_SkipJreProvisioning_NotSet() =>
        CheckProcessingSucceeds("/k:key").SkipJreProvisioning.Should().BeFalse();

    [TestMethod]
    public void PreArgProc_EngineJarPath_SetValid()
    {
        var runtime = new TestRuntime();
        runtime.File.Exists("scanner-engine.jar").Returns(true);
        CheckProcessingSucceeds(runtime, "/k:key", "/d:sonar.scanner.engineJarPath=scanner-engine.jar")
            .EngineJarPath
            .Should().Be("scanner-engine.jar");
    }

    [TestMethod]
    public void PreArgProc_EngineJarPath_SetInvalid()
    {
        var runtime = new TestRuntime();
        runtime.File.Exists("scanner-engine.jar").Returns(false);
        CheckProcessingFails(runtime, "/k:key", "/d:sonar.scanner.engineJarPath=scanner-engine.jar");
        runtime.Should().HaveErrorsLogged("The argument 'sonar.scanner.engineJarPath' contains an invalid path. Please make sure the path is correctly pointing to the scanner engine jar.");
    }

    [TestMethod]
    public void PreArgProc_EngineJarPath_NotSet() =>
        CheckProcessingSucceeds("/k:key").EngineJarPath.Should().BeNull();

    [TestMethod]
    [DataRow("true", true)]
    [DataRow("True", true)]
    [DataRow("false", false)]
    [DataRow("False", false)]
    public void PreArgProc_UseSonarScannerCli_SetValid(string useSonarScannerCli, bool result) =>
        CheckProcessingSucceeds("/k:key", $"/d:sonar.scanner.useSonarScannerCLI={useSonarScannerCli}").UseSonarScannerCli.Should().Be(result);

    [TestMethod]
    [DataRow("gibberish")]
    [DataRow(" ")]
    public void PreArgProc_UseSonarScannerCli_SetInvalid(string useSonarScannerCli) =>
        CheckProcessingFails("/k:key", $"/d:sonar.scanner.useSonarScannerCLI={useSonarScannerCli}").Logger
            .Should().HaveErrors("The argument 'sonar.scanner.useSonarScannerCLI' has an invalid value. Please ensure it is set to either 'true' or 'false'.");

    [TestMethod]
    public void PreArgProc_UseSonarScannerCli_NotSet() =>
        CheckProcessingSucceeds("/k:key").UseSonarScannerCli.Should().BeFalse();

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
    public void PreArgProc_ScanAllAnalysis_SetInvalid(string scanAll) =>
        CheckProcessingFails("/k:key", $"/d:sonar.scanner.scanAll={scanAll}").Logger
            .Should().HaveErrors("The argument 'sonar.scanner.scanAll' has an invalid value. Please ensure it is set to either 'true' or 'false'.");

    [TestMethod]
    public void PreArgProc_ScanAllAnalysis_NotSet() =>
        CheckProcessingSucceeds("/k:key").ScanAllAnalysis.Should().BeTrue();

    [TestMethod]
    public void PreArgProc_UserHome_NotSet()
    {
        var runtime = new TestRuntime { OperatingSystem = new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()) };
        CheckProcessingSucceeds(runtime, "/k:key").UserHome.Should().Be(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sonar"));
    }

    [TestMethod]
    public void PreArgProc_UserHome_NotSet_CreatedIfNotExists()
    {
        var defaultUserHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sonar");
        var runtime = new TestRuntime { OperatingSystem = new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()) };
        runtime.Directory.Exists(defaultUserHome).Returns(false);
        CheckProcessingSucceeds(runtime, "/k:key").UserHome.Should().Be(defaultUserHome);
        runtime.Directory.Received(1).CreateDirectory(defaultUserHome);
    }

    [TestMethod]
    public void PreArgProc_UserHome_NotSet_CreationFails()
    {
        var defaultUserHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sonar");
        var runtime = new TestRuntime { OperatingSystem = new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()) };
        runtime.Directory.Exists(defaultUserHome).Returns(false);
        runtime.Directory.When(x => x.CreateDirectory(defaultUserHome)).Do(_ => throw new IOException("Directory can not be created."));
        CheckProcessingSucceeds(runtime, "/k:key").UserHome.Should().BeNull();
        runtime.Directory.Received(1).CreateDirectory(defaultUserHome);
        runtime.Should().HaveWarningsLogged($"Failed to create the default user home directory '{defaultUserHome}' with exception 'Directory can not be created.'.");
    }

    [TestMethod]
    [DataRow("Test")]
    [DataRow(@"""Test""")]
    [DataRow("'Test'")]
    [DataRow(@"C:\Users\Some Name")]
    [DataRow(@"""C:\Users\Some Name""")]
    public void PreArgProc_UserHome_Set_DirectoryExists(string path)
    {
        var runtime = new TestRuntime();
        runtime.Directory.Exists(path).Returns(true);
        CheckProcessingSucceeds(runtime, "/k:key", $"/d:sonar.userHome={path}").UserHome.Should().Be(path);
    }

    [TestMethod]
    [DataRow("Test")]
    [DataRow(@"""Test""")]
    [DataRow("'Test'")]
    [DataRow(@"C:\Users\Some Name")]
    [DataRow(@"""C:\Users\Some Name""")]
    public void PreArgProc_UserHome_Set_DirectoryExistsNot_CanBeCreated(string path)
    {
        var runtime = new TestRuntime();
        runtime.Directory.Exists(path).Returns(false);
        CheckProcessingSucceeds(runtime, "/k:key", $"/d:sonar.userHome={path}");
        runtime.Directory.Received(1).CreateDirectory(path);
        runtime.Should().HaveDebugsLogged($"Created the sonar.userHome directory at '{path}'.");
    }

    [TestMethod]
    [DataRow("Test")]
    [DataRow(@"""Test""")]
    [DataRow("'Test'")]
    [DataRow(@"C:\Users\Some Name")]
    [DataRow(@"""C:\Users\Some Name""")]
    public void PreArgProc_UserHome_Set_DirectoryExistsNot_CanNotBeCreated(string path)
    {
        var runtime = new TestRuntime();
        runtime.Directory.Exists(path).Returns(false);
        runtime.Directory.When(x => x.CreateDirectory(path)).Do(_ => throw new IOException("Directory creation failed."));
        CheckProcessingFails(runtime, "/k:key", $"/d:sonar.userHome={path}");
        runtime.Directory.Received(1).CreateDirectory(path);
        runtime.Should().HaveErrorsLogged($"The attempt to create the directory specified by 'sonar.userHome' at '{path}' failed with error 'Directory creation failed.'. "
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
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Any<string>()).Returns(true);
        var result = CheckProcessingSucceeds(
            runtime,
            "/k:key",
            $"/d:sonar.scanner.truststorePath={path}",
            $"/d:sonar.scanner.truststorePassword={password}");
        result.TruststorePath.Should().Be(path);
        result.TruststorePassword.Should().Be(password);
    }

    [TestMethod]
    public void PreArgProc_TruststorePath()
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Any<string>()).Returns(true);
        var result = CheckProcessingSucceeds(runtime, "/k:key", @"/d:sonar.scanner.truststorePath=""c:\test.pfx""");
        result.TruststorePath.Should().Be(@"""c:\test.pfx""");
        result.TruststorePassword.Should().Be("changeit");
    }

    [TestMethod]
    public void PreArgProc_Fail_TruststorePassword_Only() =>
        CheckProcessingFails("/k:key", @"/d:sonar.scanner.truststorePassword=changeit").Logger.Errors
            .Should().Contain("'sonar.scanner.truststorePath' must be specified when 'sonar.scanner.truststorePassword' is provided.");

    [TestMethod]
    [DataRow(@"/d:sonar.scanner.truststorePassword=changeit", "changeit")]
    [DataRow(@"/d:sonar.scanner.truststorePassword=changeit now", "changeit now")]
    // https://sonarsource.atlassian.net/browse/SCAN4NET-204
    [DataRow(@"/d:sonar.scanner.truststorePassword=""changeit now""", @"""changeit now""")] // should be 'changeit now' without double quotes
    [DataRow(@"/d:sonar.scanner.truststorePassword=""hjdska/msm^#&%!""", @"""hjdska/msm^#&%!""")] // should be 'hjdska/msm^#&%!' without double quotes
    // [DataRow(@"/d:sonar.scanner.truststorePassword=", null)] // empty password should be allowed
    public void PreArgProc_TruststorePassword_Quoted(string passwordProperty, string parsedPassword)
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Any<string>()).Returns(true);
        var result = CheckProcessingSucceeds(runtime, "/k:key", @"/d:sonar.scanner.truststorePath=test.pfx", passwordProperty);
        result.TruststorePassword.Should().Be(parsedPassword);
    }

    [TestMethod]
    public void PreArgProc_TruststorePathAndPassword_DefaultValues()
    {
        var truststorePath = Path.Combine(".sonar", "ssl", "truststore.p12");
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Any<string>()).Returns(true);
        runtime.File.Open(Arg.Any<string>()).Returns(new MemoryStream());

        var result = CheckProcessingSucceeds(runtime, "/k:key");
        runtime.Logger.DebugMessages.Should().Contain("No truststore provided; attempting to use the default location.");
        runtime.Logger.DebugMessages.Should().ContainMatch($"Fall back on using the truststore from the default location at *{truststorePath}.");
        result.TruststorePath.Should().EndWith(truststorePath);
        result.TruststorePassword.Should().Be("changeit");
    }

    [TestMethod]
    public void PreArgProc_TruststorePathAndPassword_DefaultValuesTruststoreNotFound()
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Any<string>()).Returns(false);

        var result = CheckProcessingSucceeds(runtime, "/k:key");
        runtime.Logger.DebugMessages.Should().Contain("No truststore provided; attempting to use the default location.");
        runtime.Logger.DebugMessages.Should().ContainMatch("No truststore found at the default location; proceeding without a truststore.");
        result.TruststorePath.Should().BeNull();
        result.TruststorePassword.Should().BeNull();
    }

    [TestMethod]
    public void PreArgProc_TruststorePathAndPassword_DefaultValuesTruststoreCannotOpenPasswordProvided()
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Any<string>()).Returns(true);
        runtime.File.Open(Arg.Any<string>()).Throws(new IOException());

        var result = CheckProcessingSucceeds(runtime, "/k:key", "/d:sonar.scanner.truststorePassword=changeit");
        runtime.Logger.DebugMessages.Should().Contain("No truststore provided; attempting to use the default location.");
        runtime.Logger.DebugMessages.Should().ContainMatch("No truststore found at the default location; proceeding without a truststore.");
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
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Any<string>()).Returns(true);
        runtime.File.Open(Arg.Any<string>()).Throws(exception);

        var result = CheckProcessingSucceeds(runtime, "/k:key");
        result.TruststorePath.Should().BeNull();
        result.TruststorePassword.Should().BeNull();
        runtime.Logger.DebugMessages.Should()
            .ContainMatch(
                $"The sonar.scanner.truststorePath file '*.sonar{Separator}ssl{Separator}truststore.p12' can not be opened. Details: {exceptionType.FullName}: {exception.Message}");
    }

    [TestMethod]
    public void PreArgProc_TruststorePathAndPassword_DefaultValuesTruststoreNotFoundPasswordProvided()
    {
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Any<string>()).Returns(false);

        CheckProcessingFails(runtime, "/k:key", "/d:sonar.scanner.truststorePassword=\"changeit\"");

        runtime.Should().HaveErrorsLogged("'sonar.scanner.truststorePath' must be specified when 'sonar.scanner.truststorePassword' is provided.");
    }

    [TestMethod]
    [DataRow(@"C:\sonar")]
    [DataRow(@"""C:\sonar""")]
    [DataRow(@"'C:\sonar'")]
    public void PreArgProc_TruststorePathAndPasswordSonarUserHomeEnvSet_DefaultValues(string sonarUserHome)
    {
        var truststorePath = Path.Combine(sonarUserHome.Trim('\'', '"'), "ssl", "truststore.p12");
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Any<string>()).Returns(true);
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable("SONAR_USER_HOME", sonarUserHome);

        var result = CheckProcessingSucceeds(runtime, "/k:key");
        runtime.Logger.DebugMessages.Should().Contain("No truststore provided; attempting to use the default location.");
        runtime.Logger.DebugMessages.Should().Contain($"Fall back on using the truststore from the default location at {truststorePath}.");
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
        var runtime = new TestRuntime();
        runtime.File.Exists(Arg.Any<string>()).Returns(true);

        var result = CheckProcessingSucceeds(runtime, "/k:key", $"/d:sonar.userHome={sonarUserHome}");
        runtime.Logger.DebugMessages.Should().Contain("No truststore provided; attempting to use the default location.");
        runtime.Logger.DebugMessages.Should().Contain($"Fall back on using the truststore from the default location at {truststorePath}.");
        result.TruststorePath.Should().Be(truststorePath);
        result.TruststorePassword.Should().Be("changeit");
    }

    [TestMethod]
    public void PreArgProc_TruststorePath_FileNotExists()
    {
        const string fileName = "test.pfx";
        var runtime = new TestRuntime();
        runtime.File.Exists(fileName).Returns(false);
        CheckProcessingFails(runtime, "/k:key", $"/d:sonar.scanner.truststorePath={fileName}");
        runtime.Should().HaveErrorsLogged($"The specified sonar.scanner.truststorePath file '{fileName}' can not be found.");
    }

    [TestMethod]
    public void PreArgProc_TruststorePath_FileNotOpen()
    {
        const string fileName = "test.pfx";
        var runtime = new TestRuntime();
        runtime.File.Exists(fileName).Returns(true);
        runtime.File.Open(fileName).Throws(new IOException("File can not be opened."));
        CheckProcessingFails(runtime, "/k:key", $"/d:sonar.scanner.truststorePath={fileName}");
        runtime.Should().HaveErrorsLogged($"The sonar.scanner.truststorePath file '{fileName}' can not be opened. Details: System.IO.IOException: File can not be opened.");
    }

    private static TestRuntime CheckProcessingFails(params string[] commandLineArgs)
    {
        var runtime = new TestRuntime();
        CheckProcessingFails(runtime, commandLineArgs);
        return runtime;
    }

    private static void CheckProcessingFails(TestRuntime runtime, params string[] commandLineArgs)
    {
        var result = ArgumentProcessor.TryProcessArgs(commandLineArgs, runtime);

        result.Should().BeNull();
        runtime.Should().HaveErrorsLogged();
    }

    private static ProcessedArgs CheckProcessingSucceeds(params string[] commandLineArgs) =>
        CheckProcessingSucceeds(new TestRuntime(), commandLineArgs);

    private static ProcessedArgs CheckProcessingSucceeds(TestRuntime runtime, params string[] commandLineArgs)
    {
        var result = ArgumentProcessor.TryProcessArgs(commandLineArgs, runtime);
        result.Should().NotBeNull();
        runtime.Should().HaveNoErrorsLogged();
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
        var actualValue = actual.Setting(key);
        actualValue.Should().NotBeNull("Expected dynamic settings does not exist. Key: {0}", key);
        actualValue.Should().Be(value);

        // Check the public list of properties
        var found = Property.TryGetProperty(key, actual.AllProperties(), out var match);
        found.Should().BeTrue("Failed to find the expected property. Key: {0}", key);
        match.Should().NotBeNull("Returned property should not be null. Key: {0}", key);
        match.Value.Should().Be(value);
    }
}
