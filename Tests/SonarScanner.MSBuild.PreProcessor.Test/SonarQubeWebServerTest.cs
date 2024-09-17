/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.JreResolution;
using SonarScanner.MSBuild.PreProcessor.Protobuf;
using SonarScanner.MSBuild.PreProcessor.WebServer;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class SonarQubeWebServerTest
{
    private const string ProjectKey = "project-key";
    private const string ProjectBranch = "project-branch";

    [TestMethod]
    public void Ctor_LogsServerTypeAndVersion()
    {
        var logger = new TestLogger();
        CreateServer(logger: logger);

        logger.AssertInfoMessageExists("Using SonarQube v9.9.");
    }

    [DataTestMethod]
    [DataRow("7.9.0.5545", false, false)]
    [DataRow("8.0.0.18670", false, false)]
    [DataRow("8.8.0.1121", false, false)]
    [DataRow("8.9.0.0", true, true)]
    [DataRow("9.0.0.1121", true, true)]
    [DataRow("9.9.0.0", true, false)]
    [DataRow("10.15.0.1121", true, false)]
    public void IsServerVersionSupported(string sqVersion, bool expected, bool shouldLogWarning)
    {
        var logger = new TestLogger();
        var sut = CreateServer(version: new Version(sqVersion), logger: logger);
        sut.IsServerVersionSupported().Should().Be(expected);
        if (!expected)
        {
            logger.AssertErrorLogged("SonarQube versions below 8.9 are not supported anymore by the SonarScanner for .NET. Please upgrade your SonarQube version to 8.9 or above or use an older version of the scanner (< 6.0.0), to be able to run the analysis.");
        }
        if (shouldLogWarning)
        {
            var expectedWarningMessage = "SonarQube versions below 9.9 will be unsupported by the SonarScanner for .NET starting from January 2025. Please upgrade to a newer SonarQube version.";
            logger.AssertWarningLogged(expectedWarningMessage);
            logger.AssertUIWarningsLogged(expectedWarningMessage);
        }
    }

    [DataTestMethod]
    [DataRow("{ }")]
    [DataRow(@"{ ""isValidLicense"": false }")]
    public async Task IsServerLicenseValid_Commercial_AuthNotForced_LicenseIsInvalid(string responseContent)
    {
        var logger = new TestLogger();
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(responseContent) };
        var downloader = Substitute.For<IDownloader>();
        downloader.DownloadResource(Arg.Any<string>()).Returns(Task.FromResult(response));
        downloader.GetBaseUrl().Returns("host");
        var sut = CreateServer(downloader, logger: logger);

        var isValid = await sut.IsServerLicenseValid();

        isValid.Should().BeFalse();
        logger.AssertSingleErrorExists("Your SonarQube instance seems to have an invalid license. Please check it. Server url: host");
        logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_Commercial_AuthNotForced_LicenseIsValid()
    {
        var logger = new TestLogger();
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(@"{ ""isValidLicense"": true }") };
        var downloader = Substitute.For<IDownloader>();
        downloader.DownloadResource(Arg.Any<string>()).Returns(Task.FromResult(response));
        var sut = CreateServer(downloader, logger: logger);

        var isValid = await sut.IsServerLicenseValid();

        isValid.Should().BeTrue();
        logger.AssertNoErrorsLogged();
        logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_Commercial_AuthForced_WithoutCredentials_ShouldReturnFalseAndLogError()
    {
        var logger = new TestLogger();
        var downloader = Substitute.For<IDownloader>();
        downloader.DownloadResource("api/editions/is_valid_license").Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized }));
        var sut = CreateServer(downloader, logger: logger);

        var result = await sut.IsServerLicenseValid();

        result.Should().BeFalse();
        logger.AssertSingleErrorExists("Unauthorized: Access is denied due to invalid credentials. Please check the authentication parameters.");
        logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_ServerNotLicensed()
    {
        var logger = new TestLogger();
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound, Content = new StringContent(@"{""errors"":[{""msg"":""License not found""}]}") };
        var downloader = Substitute.For<IDownloader>();
        downloader.DownloadResource(Arg.Any<string>()).Returns(Task.FromResult(response));
        downloader.GetBaseUrl().Returns("host");
        var sut = CreateServer(downloader, logger: logger);

        var result = await sut.IsServerLicenseValid();

        result.Should().BeFalse();
        logger.AssertSingleErrorExists("Your SonarQube instance seems to have an invalid license. Please check it. Server url: host");
        logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_CE_SkipLicenseCheck()
    {
        var logger = new TestLogger();
        var response = new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound, Content = new StringContent(@"{""errors"":[{""msg"":""Unknown url: /api/editions/is_valid_license""}]}") };
        var downloader = Substitute.For<IDownloader>();
        downloader.DownloadResource(Arg.Any<string>()).Returns(Task.FromResult(response));
        var sut = CreateServer(downloader, logger: logger);

        var result = await sut.IsServerLicenseValid();

        result.Should().BeTrue();
        logger.AssertNoErrorsLogged();
        logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task IsServerLicenseValid_RequestUrl()
    {
        var downloader = Substitute.For<IDownloader>();
        downloader.DownloadResource("api/editions/is_valid_license")
            .Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(@"{ ""isValidLicense"": true }") }));
        var sut = CreateServer(downloader);

        var isValid = await sut.IsServerLicenseValid();

        isValid.Should().BeTrue();
        await downloader.Received().DownloadResource("api/editions/is_valid_license");
    }

    [DataTestMethod]
    [DataRow("7.9.0.5545", false)]
    [DataRow("8.0.0.18670", false)]
    [DataRow("8.8.0.1121", false)]
    [DataRow("8.9.0.0", false)]
    [DataRow("9.0.0.1121", false)]
    [DataRow("10.5.1.90531", false)]
    [DataRow("10.6.0.92166", true)] // First version with JRE provisioning
    [DataRow("10.15.0.1121", true)]
    public void SupportsJreProvisioningVersionSupported(string sqVersion, bool expected)
    {
        var sut = CreateServer(version: new Version(sqVersion));
        sut.SupportsJreProvisioning.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("foo bar", "my org")]
    public async Task DownloadQualityProfile_OrganizationProfile_QualityProfileUrlContainsOrganization(string projectKey, string organization)
    {
        const string profileKey = "orgProfile";
        const string language = "cs";
        var downloader = Substitute.For<IDownloader>();
        var downloadResult = Tuple.Create(true, $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}");
        downloader.TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
        var sut = CreateServer(downloader, new Version("9.9"), organization: organization);

        var result = await sut.DownloadQualityProfile(projectKey, null, language);

        result.Should().Be(profileKey);
    }

    [TestMethod]
    [DataRow("foo bar", "my org")]
    public async Task DownloadQualityProfile_SQ62OrganizationProfile_QualityProfileUrlDoesNotContainsOrganization(string projectKey, string organization)
    {
        const string profileKey = "orgProfile";
        const string language = "cs";
        var qualityProfileUrl = $"api/qualityprofiles/search?project={WebUtility.UrlEncode($"{projectKey}")}";
        var downloader = Substitute.For<IDownloader>();
        var downloadResult = Tuple.Create(true, $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}");
        downloader.TryDownloadIfExists(qualityProfileUrl, Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
        var sut = CreateServer(downloader, new Version("6.2"), organization: organization);

        var result = await sut.DownloadQualityProfile(projectKey, null, language);

        result.Should().Be(profileKey);
    }

    [TestMethod]
    public void DownloadQualityProfile_MultipleQPForSameLanguage_ShouldThrow()
    {
        var downloadResult = Tuple.Create(true, "{ profiles: [{\"key\":\"profile1k\",\"name\":\"profile1\",\"language\":\"cs\", \"isDefault\": false}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"cs\", \"isDefault\": true}]}");
        var downloader = Substitute.For<IDownloader>();
        downloader.TryDownloadIfExists("api/qualityprofiles/search?project=foo+bar", Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));

        var sut = CreateServer(downloader, new Version("9.9"));

        // ToDo: This behavior is confusing, and not all the parsing errors should lead to this. See: https://github.com/SonarSource/sonar-scanner-msbuild/issues/1468
        ((Func<string>)(() => sut.DownloadQualityProfile("foo bar", null, "cs").Result))
            .Should()
            .ThrowExactly<AggregateException>()
            .WithInnerExceptionExactly<AnalysisException>()
            .WithMessage("It seems that you are using an old version of SonarQube which is not supported anymore. Please update to at least 6.7.");
    }

    [TestMethod]
    public void DownloadProperties_Sq63()
    {
        var downloader = Substitute.For<IDownloader>();
        downloader.TryDownloadIfExists("api/settings/values?component=comp", Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(true, @"{settings: [
                    {
                        key: ""sonar.core.id"",
                        value: ""AVrrKaIfChAsLlov22f0"",
                        inherited: true
                    },
                    {
                        key: ""sonar.exclusions"",
                        values: [ ""myfile"", ""myfile2"" ]
                    },
                    {
                        key: ""sonar.junit.reportsPath"",
                        value: ""testing.xml""
                    },
                    {
                        key: ""sonar.issue.ignore.multicriteria"",
                        fieldValues: [
                            {
                                resourceKey: ""prop1"",
                                ruleKey: """"
                            },
                            {
                                resourceKey: ""prop2"",
                                ruleKey: """"
                            }
                        ]
                    }
                ]}")));
        var sut = CreateServer(downloader, new Version("6.3"));

        var result = sut.DownloadProperties("comp", null).Result;

        result.Should().HaveCount(7);
        result["sonar.exclusions"].Should().Be("myfile,myfile2");
        result["sonar.junit.reportsPath"].Should().Be("testing.xml");
        result["sonar.issue.ignore.multicriteria.1.resourceKey"].Should().Be("prop1");
        result["sonar.issue.ignore.multicriteria.1.ruleKey"].Should().Be(string.Empty);
        result["sonar.issue.ignore.multicriteria.2.resourceKey"].Should().Be("prop2");
        result["sonar.issue.ignore.multicriteria.2.ruleKey"].Should().Be(string.Empty);
    }

    [TestMethod]
    public async Task DownloadProperties_Sq63_NoComponentSettings_FallsBackToCommon()
    {
        const string componentName = "nonexistent-component";
        var downloader = Substitute.For<IDownloader>();
        downloader.TryDownloadIfExists($"api/settings/values?component={componentName}", Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(false, (string)null)));
        downloader.Download("api/settings/values", Arg.Any<bool>())
            .Returns(Task.FromResult(@"{ settings: [ { key: ""key"", value: ""42"" } ] }"));
        var sut = CreateServer(downloader, new Version("6.3"));

        var result = await sut.DownloadProperties(componentName, null);

        result.Should().ContainSingle().And.ContainKey("key");
        result["key"].Should().Be("42");
    }

    [TestMethod]
    public async Task DownloadProperties_Sq63_MissingValue_Throws()
    {
        const string componentName = "nonexistent-component";
        var downloader = Substitute.For<IDownloader>();
        downloader.TryDownloadIfExists($"api/settings/values?component={componentName}", Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(false, (string)null)));
        downloader.Download("api/settings/values", Arg.Any<bool>())
            .Returns(Task.FromResult(@"{ settings: [ { key: ""key"" } ] }"));
        var sut = CreateServer(downloader, new Version("6.3"));

        await sut.Invoking(async x => await x.DownloadProperties(componentName, null)).Should().ThrowAsync<ArgumentException>().WithMessage("Invalid property");
    }

    [TestMethod]
    public void DownloadProperties_NullProjectKey_Throws()
    {
        var sut = CreateServer();
        Action act = () => _ = sut.DownloadProperties(null, null).Result;

        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("projectKey");
    }

    [TestMethod]
    public async Task DownloadProperties_ProjectWithBranch_SuccessfullyRetrieveProperties()
    {
        var downloader = Substitute.For<IDownloader>();
        downloader.Download("api/properties?resource=foo+bar%3AaBranch", Arg.Any<bool>())
            .Returns(Task.FromResult("[{\"key\": \"sonar.property1\",\"value\": \"anotherValue1\"},{\"key\": \"sonar.property2\",\"value\": \"anotherValue2\"}]"));
        var sut = CreateServer(downloader, new Version("5.6"));
        var expected = new Dictionary<string, string>
        {
            ["sonar.property1"] = "anotherValue1",
            ["sonar.property2"] = "anotherValue2"
        };

        var result = await sut.DownloadProperties("foo bar", "aBranch");

        result.Should().HaveCount(expected.Count);
        result.Should().Equal(expected);
        await downloader.Received().Download("api/properties?resource=foo+bar%3AaBranch", Arg.Any<bool>());
    }

    [TestMethod]
    public async Task DownloadProperties_ProjectWithoutBranch_SuccessfullyRetrieveProperties()
    {
        var downloader = Substitute.For<IDownloader>();
        downloader.Download("api/properties?resource=foo+bar", Arg.Any<bool>())
            .Returns(Task.FromResult("[{\"key\": \"sonar.property1\",\"value\": \"anotherValue1\"},{\"key\": \"sonar.property2\",\"value\": \"anotherValue2\"}]"));
        var sut = CreateServer(downloader, new Version("5.6"));
        var expected = new Dictionary<string, string>
        {
            ["sonar.property1"] = "anotherValue1",
            ["sonar.property2"] = "anotherValue2"
        };

        var result = await sut.DownloadProperties("foo bar", null);

        result.Should().HaveCount(expected.Count);
        result.Should().Equal(expected);
        await downloader.Received().Download("api/properties?resource=foo+bar", Arg.Any<bool>());
    }

    [TestMethod]
    public async Task DownloadProperties_Old_Forbidden()
    {
        var downloader = Substitute.For<IDownloader>();
        downloader.Download($"api/properties?resource={ProjectKey}", Arg.Any<bool>())
            .Returns(Task.FromException<string>(new HttpRequestException("Forbidden")));
        var sut = CreateServer(downloader, new Version("1.2.3.4"));

        Func<Task> action = async () => await sut.DownloadProperties(ProjectKey, null);

        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [TestMethod]
    public void DownloadProperties_Sq63plus_Forbidden()
    {
        var downloader = Substitute.For<IDownloader>();
        downloader.TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(Task.FromException<Tuple<bool, string>>(new HttpRequestException("Forbidden")));
        var sut = CreateServer(downloader, new Version("6.3.0.0"));

        Action action = () => _ = sut.DownloadProperties(ProjectKey, null).Result;

        action.Should().Throw<HttpRequestException>();
    }

    [TestMethod]
    public async Task DownloadProperties_SQ63AndHigherWithProject_ShouldBeEmpty()
    {
        var downloader = Substitute.For<IDownloader>();
        downloader.TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(Tuple.Create(true, "{ settings: [ ] }")));
        var sut = CreateServer(downloader, new Version("6.3"));

        var properties = await sut.DownloadProperties("key", null);

        properties.Should().BeEmpty();
        await downloader.Received().TryDownloadIfExists("api/settings/values?component=key", true);
    }

    [TestMethod]
    public async Task DownloadProperties_OlderThanSQ63_ShouldBeEmpty()
    {
        var downloader = Substitute.For<IDownloader>();
        downloader.Download(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult("[]"));
        var sut = CreateServer(downloader, new Version("6.2.9"));

        var properties = await sut.DownloadProperties("key", null);

        properties.Should().BeEmpty();
        await downloader.Received().Download(Arg.Any<string>(), Arg.Any<bool>());
    }

    [TestMethod]
    public async Task DownloadProperties_SQ63AndHigherWithoutProject_ShouldBeEmpty()
    {
        var downloader = Substitute.For<IDownloader>();
        downloader.TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(Tuple.Create(false, (string)null)));
        downloader.Download(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult("{ settings: [ ] }"));
        var sut = CreateServer(downloader, new Version("6.3"));

        var properties = await sut.DownloadProperties("key", null);

        properties.Should().BeEmpty();
        await downloader.Received().TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>());
        await downloader.Received().Download(Arg.Any<string>(), Arg.Any<bool>());
    }

    [TestMethod]
    public async Task DownloadCache_NullArgument()
    {
        var sut = CreateServer();
        (await sut.Invoking(x => x.DownloadCache(null)).Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("localSettings");
    }

    [TestMethod]
    [DataRow("9.8", "", "", "Incremental PR analysis is available starting with SonarQube 9.9 or later.")]
    [DataRow("9.9", "", "", "Incremental PR analysis: ProjectKey parameter was not provided.")]
    [DataRow("9.9", "BestProject", "", "Incremental PR analysis: Base branch parameter was not provided.")]
    public async Task DownloadCache_InvalidArguments(string version, string projectKey, string branch, string debugMessage)
    {
        var logger = new TestLogger();
        var sut = CreateServer(version: new Version(version), logger: logger);
        var localSettings = CreateLocalSettings(projectKey, branch);

        var result = await sut.DownloadCache(localSettings);

        result.Should().BeEmpty();
        logger.AssertSingleInfoMessageExists(debugMessage);
    }

    [DataTestMethod]
    [DataRow("Jenkins", "ghprbTargetBranch")]
    [DataRow("Jenkins", "gitlabTargetBranch")]
    [DataRow("Jenkins", "BITBUCKET_TARGET_BRANCH")]
    [DataRow("GitHub Actions", "GITHUB_BASE_REF")]
    [DataRow("GitLab", "CI_MERGE_REQUEST_TARGET_BRANCH_NAME")]
    [DataRow("BitBucket Pipelines", "BITBUCKET_PR_DESTINATION_BRANCH")]
    public async Task DownloadCache_AutomaticallyDeduceBaseBranch(string provider, string variableName)
    {
        var logger = new TestLogger();
        using var environment = new EnvironmentVariableScope().SetVariable(variableName, "branch-42");
        var downloader = Substitute.For<IDownloader>();
        MockStreamDownload(downloader, new MemoryStream());
        var sut = CreateServer(downloader, logger: logger);
        var localSettings = CreateLocalSettings(ProjectKey, null);

        await sut.DownloadCache(localSettings);

        logger.AssertInfoMessageExists($"Incremental PR analysis: Automatically detected base branch 'branch-42' from CI Provider '{provider}'.");
    }

    [DataTestMethod]
    [DataRow("ghprbTargetBranch")]
    [DataRow("gitlabTargetBranch")]
    [DataRow("BITBUCKET_TARGET_BRANCH")]
    [DataRow("GITHUB_BASE_REF")]
    [DataRow("CI_MERGE_REQUEST_TARGET_BRANCH_NAME")]
    [DataRow("BITBUCKET_PR_DESTINATION_BRANCH")]
    public async Task DownloadCache_UserInputSupersedesAutomaticDetection(string variableName)
    {
        var logger = new TestLogger();
        using var environment = new EnvironmentVariableScope().SetVariable(variableName, "wrong_branch");
        var downloader = Substitute.For<IDownloader>();
        MockStreamDownload(downloader, new MemoryStream());
        var sut = CreateServer(downloader, logger: logger);
        var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);

        await sut.DownloadCache(localSettings);

        logger.AssertSingleInfoMessageExists("Downloading cache. Project key: project-key, branch: project-branch.");
    }

    [TestMethod]
    public async Task DownloadCache_RequestUrl()
    {
        using Stream stream = new MemoryStream();
        var downloader = Substitute.For<IDownloader>();
        downloader.DownloadStream("api/analysis_cache/get?project=project-key&branch=project-branch").Returns(Task.FromResult(stream));
        var sut = CreateServer(downloader);
        var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);

        var result = await sut.DownloadCache(localSettings);

        result.Should().BeEmpty();
        await downloader.Received().DownloadStream("api/analysis_cache/get?project=project-key&branch=project-branch");
    }

    [TestMethod]
    public async Task DownloadCache_DeserializesMessage()
    {
        var logger = new TestLogger();
        using var stream = CreateCacheStream(new SensorCacheEntry { Key = "key", Data = ByteString.CopyFromUtf8("value") });
        var downloader = Substitute.For<IDownloader>();
        MockStreamDownload(downloader, stream);
        var sut = CreateServer(downloader, logger: logger);
        var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);

        var result = await sut.DownloadCache(localSettings);

        result.Should().ContainSingle();
        result.Single(x => x.Key == "key").Data.ToStringUtf8().Should().Be("value");
        logger.AssertInfoLogged("Downloading cache. Project key: project-key, branch: project-branch.");
    }

    [TestMethod]
    public async Task DownloadCache_WhenDownloadStreamReturnsNull_ReturnsEmpty()
    {
        var logger = new TestLogger();
        var downloader = Substitute.For<IDownloader>();
        MockStreamDownload(downloader, null);
        var sut = CreateServer(downloader, logger: logger);

        var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);
        var result = await sut.DownloadCache(localSettings);

        result.Should().BeEmpty();
        logger.AssertNoWarningsLogged();
        logger.AssertNoErrorsLogged(); // There are no errors or warnings logs but we will display an info message in the caller: "Cache data is empty. A full analysis will be performed."
    }

    [TestMethod]
    public async Task DownloadCache_WhenDownloadStreamReturnsEmpty_ReturnsEmpty()
    {
        var logger = new TestLogger();
        var downloader = Substitute.For<IDownloader>();
        MockStreamDownload(downloader, new MemoryStream());
        var sut = CreateServer(downloader, logger: logger);
        var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);

        var result = await sut.DownloadCache(localSettings);

        result.Should().BeEmpty();
        logger.DebugMessages.Should().BeEmpty();
    }

    [TestMethod]
    public async Task DownloadCache_WhenDownloadStreamThrows_ReturnsEmptyAndLogsException()
    {
        var logger = new TestLogger();
        var downloader = Substitute.For<IDownloader>();
        downloader.DownloadStream(Arg.Any<string>()).Returns(Task.FromException<Stream>(new HttpRequestException()));
        var sut = CreateServer(downloader, logger: logger);
        var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);

        var result = await sut.DownloadCache(localSettings);

        result.Should().BeEmpty();
        logger.AssertSingleWarningExists("Incremental PR analysis: an error occurred while retrieving the cache entries! Exception of type 'System.Net.Http.HttpRequestException' was thrown.");
    }

    [TestMethod]
    public async Task DownloadCache_WhenCacheStreamReadThrows_ReturnsEmptyAndLogsException()
    {
        var logger = new TestLogger();
        var stream = Substitute.For<Stream>();
        stream.Length.Returns(x => throw new InvalidOperationException());
        var downloader = Substitute.For<IDownloader>();
        MockStreamDownload(downloader, stream);
        var sut = CreateServer(downloader, logger: logger);
        var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);

        var result = await sut.DownloadCache(localSettings);

        result.Should().BeEmpty();
        logger.AssertSingleWarningExists("Incremental PR analysis: an error occurred while retrieving the cache entries! Operation is not valid due to the current state of the object.");
    }

    [TestMethod]
    public async Task DownloadCache_WhenCacheStreamDeserializeThrows_ReturnsEmptyAndLogsException()
    {
        var logger = new TestLogger();
        var downloader = Substitute.For<IDownloader>();
        MockStreamDownload(downloader, new MemoryStream([42, 42])); // this is a random byte array that fails deserialization
        var sut = CreateServer(downloader, logger: logger);
        var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);

        var result = await sut.DownloadCache(localSettings);

        result.Should().BeEmpty();
        logger.AssertSingleWarningExists("Incremental PR analysis: an error occurred while retrieving the cache entries! While parsing a protocol message, the input ended unexpectedly in the middle of a field.  This could mean either that the input has been truncated or that an embedded message misreported its own length.");
    }

    [TestMethod]
    public async Task DownloadRules_SonarQubeVersion98()
    {
        var downloader = Substitute.For<IDownloader>();
        downloader
            .Download("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1")
            .Returns("""
                {
                paging: {
                    total: 3,
                    pageIndex: 1,
                    pageSize: 500
                },
                rules: [
                    {
                        "key": "csharpsquid:S2757",
                        "repo": "csharpsquid",
                        "type": "BUG"
                    }
                ]}
                """);
        var sut = CreateServer(downloader, new Version("9.8"));

        var rules = await sut.DownloadRules("qp");

        rules.Should().ContainSingle();

        rules[0].RepoKey.Should().Be("csharpsquid");
        rules[0].RuleKey.Should().Be("S2757");
        rules[0].InternalKeyOrKey.Should().Be("S2757");
        rules[0].Parameters.Should().BeNull();
        rules[0].IsActive.Should().BeFalse();
    }

    [TestMethod]
    public async Task DownloadRules_SonarQubeVersion89()
    {
        var downloader = Substitute.For<IDownloader>();
        downloader
            .Download("api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1")
            .Returns("""
                {
                "total": 3,
                "p": 1,
                "ps": 500,
                "rules": [
                    {
                        "key": "csharpsquid:S2757",
                        "repo": "csharpsquid",
                        "type": "BUG"
                    }
                ]}
                """);
        var sut = CreateServer(downloader, new Version("8.9"));

        var rules = await sut.DownloadRules("qp");

        rules.Should().ContainSingle();

        rules[0].RepoKey.Should().Be("csharpsquid");
        rules[0].RuleKey.Should().Be("S2757");
        rules[0].InternalKeyOrKey.Should().Be("S2757");
        rules[0].Parameters.Should().BeNull();
        rules[0].IsActive.Should().BeFalse();
    }

    [TestMethod]
    public async Task DownloadJreAsync_Success()
    {
        var logger = new TestLogger();
        Stream expected = new MemoryStream([1, 2, 3]);
        var downloader = Substitute.For<IDownloader>();
        downloader
            .DownloadStream(
                "analysis/jres/someId",
                Arg.Is<Dictionary<string, string>>(x => x.Single().Key == "Accept" && x.Single().Value == "application/octet-stream"))
            .Returns(Task.FromResult(expected));

        var sut = CreateServer(downloader, logger: logger);
        var actual = await sut.DownloadJreAsync(new JreMetadata("someId", null, null, null, null));

        ((MemoryStream)actual).ToArray().Should().BeEquivalentTo([1, 2, 3]);
        logger.AssertDebugLogged("Downloading Java JRE from analysis/jres/someId.");
    }

    [TestMethod]
    public async Task DownloadJreAsync_DownloadThrows_Failure()
    {
        var downloader = Substitute.For<IDownloader>();
        downloader
            .DownloadStream(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .Throws<HttpRequestException>();
        var sut = CreateServer(downloader);

        await sut.Invoking(async x => await x.DownloadJreAsync(new(null, null, null, null, null))).Should().ThrowAsync<HttpRequestException>();
    }

    [TestMethod]
    public async Task DownloadJreAsync_NullMetadata_Failure() =>
        await CreateServer().Invoking(async x => await x.DownloadJreAsync(null)).Should().ThrowAsync<NullReferenceException>();

    private static Stream CreateCacheStream(IMessage message)
    {
        var stream = new MemoryStream();
        message.WriteDelimitedTo(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private void MockStreamDownload(IDownloader downloader, Stream stream) =>
        downloader.DownloadStream(Arg.Any<string>()).Returns(Task.FromResult(stream));

    private static ProcessedArgs CreateLocalSettings(string projectKey, string branch, string organization = "placeholder", string token = "placeholder")
    {
        var args = Substitute.For<ProcessedArgs>();
        args.ProjectKey.Returns(projectKey);
        args.Organization.Returns(organization);
        args.TryGetSetting(SonarProperties.PullRequestBase, out Arg.Any<string>()).Returns(x =>
        {
            x[1] = branch;
            return !string.IsNullOrWhiteSpace(branch);
        });
        args.TryGetSetting(SonarProperties.SonarUserName, out Arg.Any<string>()).Returns(x =>
        {
            x[1] = token;
            return !string.IsNullOrWhiteSpace(token);
        });
        return args;
    }

    private SonarQubeWebServer CreateServer(IDownloader downloader = null, Version version = null, ILogger logger = null, string organization = null)
    {
        version ??= new("9.9");
        downloader ??= Substitute.For<IDownloader>();
        logger ??= Substitute.For<ILogger>();
        return new SonarQubeWebServer(downloader, downloader, version, logger, organization);
    }
}
