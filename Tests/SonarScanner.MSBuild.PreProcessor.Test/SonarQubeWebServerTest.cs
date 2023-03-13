/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
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
using Moq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Protobuf;
using SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;
using SonarScanner.MSBuild.PreProcessor.WebServer;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class SonarQubeWebServerTest
    {
        private const string TestUrl = "http://myhost:222";
        private const string ProjectKey = "project-key";
        private const string ProjectBranch = "project-branch";

        private readonly TestDownloader downloader;
        private readonly Uri uri;
        private readonly Version version;
        private readonly TestLogger logger;
        private readonly Uri serverUrl;

        private SonarQubeWebServer sut;

        public SonarQubeWebServerTest()
        {
            serverUrl = new Uri("http://localhost/relative/");
            downloader = new TestDownloader();
            uri = new Uri(TestUrl);
            version = new Version("9.9");
            logger = new TestLogger();
        }

        [TestInitialize]
        public void Init() =>
            sut = new SonarQubeWebServer(downloader, version, logger, null);

        [TestCleanup]
        public void Cleanup() =>
            sut?.Dispose();

        [DataTestMethod]
        [DataRow("7.9.0.5545", DisplayName = "7.9 LTS")]
        [DataRow("8.0.0.18670", DisplayName = "SonarCloud")]
        [DataRow("8.8.0.1121")]
        [DataRow("9.0.0.1121")]
        [DataRow("10.15.0.1121")]
        public void WarnIfDeprecated_ShouldNotWarn(string sqVersion)
        {
            sut = new SonarQubeWebServer(downloader, new Version(sqVersion), logger, null);

            logger.Warnings.Should().BeEmpty();
        }

        [DataTestMethod]
        [DataRow("6.7.0.2232")]
        [DataRow("7.0.0.2232")]
        [DataRow("7.8.0.2232")]
        public void WarnIfDeprecated_ShouldWarn(string sqVersion)
        {
            sut = new SonarQubeWebServer(downloader, new Version(sqVersion), logger, null);

            logger.AssertSingleWarningExists("The version of SonarQube you are using is deprecated. Analyses will fail starting 6.0 release of the Scanner for .NET");
        }

        [TestMethod]
        public void IsServerLicenseValid_Commercial_AuthNotForced_LicenseIsInvalid()
        {
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(TestUrl)
                                 .SetupTryGetLicenseInformation(new Uri($"{TestUrl}/api/editions/is_valid_license"), @"{ ""isValidLicense"": false }");
            sut = new SonarQubeWebServer(downloaderMock.Object, version, logger, null);

            sut.IsServerLicenseValid().Result.Should().BeFalse();
        }

        [TestMethod]
        public void IsServerLicenseValid_Commercial_AuthNotForced_LicenseIsValid()
        {
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(TestUrl)
                                 .SetupTryGetLicenseInformation(new Uri($"{TestUrl}/api/editions/is_valid_license"), @"{ ""isValidLicense"": true }");
            sut = new SonarQubeWebServer(downloaderMock.Object, version, logger, null);

            sut.IsServerLicenseValid().Result.Should().BeTrue();
        }

        [TestMethod]
        public void IsServerLicenseValid_Commercial_AuthForced_WithoutCredentials_ShouldThrow()
        {
            downloader.ConfigureGetLicenseInformationMock(HttpStatusCode.Unauthorized, string.Empty, false);

            ((Func<bool>)(() => sut.IsServerLicenseValid().Result)).Should().ThrowExactly<AggregateException>();
        }

        [TestMethod]
        public void IsServerLicenseValid_ServerNotLicensed()
        {
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(TestUrl)
                                 .SetupTryGetLicenseInformation(new Uri($"{TestUrl}/api/editions/is_valid_license"), @"{ ""errors"":[{""msg"":""License not found""}] }", HttpStatusCode.NotFound);
            sut = new SonarQubeWebServer(downloaderMock.Object, version, logger, null);

            sut.IsServerLicenseValid().Result.Should().BeFalse();
        }

        [TestMethod]
        public void IsServerLicenseValid_CE_SkipLicenseCheck()
        {
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(TestUrl)
                                 .SetupTryGetLicenseInformation(new Uri(new Uri(TestUrl), "api/editions/is_valid_license"),
                                     @"{""errors"":[{""msg"":""Unknown url: /api/editions/is_valid_license""}]} ", HttpStatusCode.NotFound);

            sut = new SonarQubeWebServer(downloaderMock.Object, version, logger, null);

            sut.IsServerLicenseValid().Result.Should().BeTrue();
        }

        [TestMethod]
        [DataRow("foo bar", "my org")]
        public async Task TryGetQualityProfile_OrganizationProfile_QualityProfileUrlContainsOrganization(string projectKey, string organization)
        {
            const string profileKey = "orgProfile";
            const string language = "cs";
            var qualityProfileUri = new Uri(uri, $"api/qualityprofiles/search?project={WebUtility.UrlEncode($"{projectKey}")}&organization={WebUtility.UrlEncode($"{organization}")}");
            var mockDownloader = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(uri)
                                 .SetupTryDownloadIfExists(qualityProfileUri, $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}");
            sut = new SonarQubeWebServer(mockDownloader.Object, new Version("9.9"), logger, organization);

            var result = await sut.TryGetQualityProfile(projectKey, null, language);

            result.Item1.Should().BeTrue();
            result.Item2.Should().Be(profileKey);
        }

        [TestMethod]
        [DataRow("foo bar", "my org")]
        public async Task TryGetQualityProfile_SQ62OrganizationProfile_QualityProfileUrlDoesNotContainsOrganization(string projectKey, string organization)
        {
            const string profileKey = "orgProfile";
            const string language = "cs";
            var qualityProfileUri = new Uri(uri, $"api/qualityprofiles/search?project={WebUtility.UrlEncode($"{projectKey}")}");
            var mockDownloader = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(uri)
                                 .SetupTryDownloadIfExists(qualityProfileUri, $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}")
                                 .Object;
            sut = new SonarQubeWebServer(mockDownloader, new Version("6.2"), logger, organization);

            var result = await sut.TryGetQualityProfile(projectKey, null, language);

            result.Item1.Should().BeTrue();
            result.Item2.Should().Be(profileKey);
        }

        [TestMethod]
        public void TryGetQualityProfile_MultipleQPForSameLanguage_ShouldThrow()
        {
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(TestUrl)
                                 .SetupTryDownloadIfExists(new Uri(new Uri(TestUrl), "api/qualityprofiles/search?project=foo+bar"),
                                     "{ profiles: [{\"key\":\"profile1k\",\"name\":\"profile1\",\"language\":\"cs\", \"isDefault\": false}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"cs\", \"isDefault\": true}]}");
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("9.9"), logger, null);

            // ToDo: This behavior is confusing, and not all the parsing errors should lead to this. See: https://github.com/SonarSource/sonar-scanner-msbuild/issues/1468
            ((Func<Tuple<bool, string>>)(() => sut.TryGetQualityProfile("foo bar", null, "cs").Result))
                .Should()
                .ThrowExactly<AggregateException>()
                .WithInnerExceptionExactly<AnalysisException>()
                .WithMessage("It seems that you are using an old version of SonarQube which is not supported anymore. Please update to at least 6.7.");
        }

        [TestMethod]
        public void GetProperties_Sq63()
        {
            var downloaderMock = MockIDownloaderHelper
                    .CreateMock()
                    .SetupGetBaseUri(TestUrl)
                    .SetupTryDownloadIfExists(new Uri(new Uri(TestUrl), "api/settings/values?component=comp"), @"{ settings: [
                  {
                    key: ""sonar.core.id"",
                    value: ""AVrrKaIfChAsLlov22f0"",
                    inherited: true
                  },
                  {
                    key: ""sonar.exclusions"",
                    values: [
                      ""myfile"",
                      ""myfile2""
                    ]
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
                ]}");
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("6.3"), logger, null);

            var result = sut.GetProperties("comp", null).Result;

            result.Should().HaveCount(7);
            result["sonar.exclusions"].Should().Be("myfile,myfile2");
            result["sonar.junit.reportsPath"].Should().Be("testing.xml");
            result["sonar.issue.ignore.multicriteria.1.resourceKey"].Should().Be("prop1");
            result["sonar.issue.ignore.multicriteria.1.ruleKey"].Should().Be(string.Empty);
            result["sonar.issue.ignore.multicriteria.2.resourceKey"].Should().Be("prop2");
            result["sonar.issue.ignore.multicriteria.2.ruleKey"].Should().Be(string.Empty);
        }

        [TestMethod]
        public async Task GetProperties_Sq63_NoComponentSettings_FallsBackToCommon()
        {
            const string componentName = "nonexistent-component";
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(TestUrl)
                                 .SetupTryDownloadIfExists(new Uri(new Uri(TestUrl), $"api/settings/values?component={componentName}"))
                                 .SetupDownload(new Uri(new Uri(TestUrl), "api/settings/values"), @"{ settings: [ { key: ""key"", value: ""42"" } ]}");
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("6.3"), logger, null);

            var result = await sut.GetProperties(componentName, null);

            result.Should().ContainSingle().And.ContainKey("key");
            result["key"].Should().Be("42");
        }

        [TestMethod]
        public async Task GetProperties_Sq63_MissingValue_Throws()
        {
            const string componentName = "nonexistent-component";
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(TestUrl)
                                 .SetupTryDownloadIfExists(new Uri(new Uri(TestUrl), $"api/settings/values?component={componentName}"))
                                 .SetupDownload(new Uri(new Uri(TestUrl), "api/settings/values"), @"{ settings: [ { key: ""key"" } ]}");
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("6.3"), logger, null);

            await sut.Invoking(async x => await x.GetProperties(componentName, null)).Should().ThrowAsync<ArgumentException>().WithMessage("Invalid property");
        }

        [TestMethod]
        public void GetProperties_NullProjectKey_Throws()
        {
            var testSubject = new SonarQubeWebServer(new TestDownloader(), version, logger, null);
            Action act = () => _ = testSubject.GetProperties(null, null).Result;

            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("projectKey");
        }

        [TestMethod]
        public void GetProperties()
        {
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(TestUrl)
                                 .SetupDownload(new Uri("http://myhost:222/api/properties?resource=foo+bar"), "[{\"key\": \"sonar.property1\",\"value\": \"value1\"},{\"key\": \"sonar.property2\",\"value\": \"value2\"},{\"key\": \"sonar.cs.msbuild.testProjectPattern\",\"value\": \"pattern\"}]")
                                 .SetupDownload(new Uri("http://myhost:222/api/properties?resource=foo+bar%3AaBranch"), "[{\"key\": \"sonar.property1\",\"value\": \"anotherValue1\"},{\"key\": \"sonar.property2\",\"value\": \"anotherValue2\"}]");

            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("5.6"), logger, null);

            // This test includes a regression scenario for SONARMSBRU-187:
            // Requesting properties for project:branch should return branch-specific data

            // Check that properties are correctly defaulted as well as branch-specific
            downloader.Pages[new Uri("http://myhost:222/api/properties?resource=foo+bar")] =
                "[{\"key\": \"sonar.property1\",\"value\": \"value1\"},{\"key\": \"sonar.property2\",\"value\": \"value2\"},{\"key\": \"sonar.cs.msbuild.testProjectPattern\",\"value\": \"pattern\"}]";
            downloader.Pages[new Uri("http://myhost:222/api/properties?resource=foo+bar%3AaBranch")] =
                "[{\"key\": \"sonar.property1\",\"value\": \"anotherValue1\"},{\"key\": \"sonar.property2\",\"value\": \"anotherValue2\"}]";

            // default
            var expected1 = new Dictionary<string, string>
            {
                ["sonar.property1"] = "value1",
                ["sonar.property2"] = "value2",
                ["sonar.msbuild.testProjectPattern"] = "pattern"
            };
            var actual1 = sut.GetProperties("foo bar", null).Result;

            actual1.Should().HaveCount(expected1.Count);
            actual1.Should().NotBeSameAs(expected1);

            // branch specific
            var expected2 = new Dictionary<string, string>
            {
                ["sonar.property1"] = "anotherValue1",
                ["sonar.property2"] = "anotherValue2"
            };
            var actual2 = sut.GetProperties("foo bar", "aBranch").Result;

            actual2.Should().HaveCount(expected2.Count);
            actual2.Should().NotBeSameAs(expected2);
        }

        [TestMethod]
        public async Task GetProperties_Old_Forbidden()
        {
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(TestUrl)
                                 .SetupDownload(new Uri(new Uri(TestUrl), $"api/properties?resource={ProjectKey}"), new HttpRequestException("Forbidden"));

            var service = new SonarQubeWebServer(downloaderMock.Object, new Version("1.2.3.4"), logger, null);

            Func<Task> action = async () => await service.GetProperties(ProjectKey, null);
            await action.Should().ThrowAsync<HttpRequestException>();

            logger.Errors.Should().HaveCount(1);
        }

        [TestMethod]
        public void GetProperties_Sq63plus_Forbidden()
        {
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(TestUrl)
                                 .SetupTryDownloadIfExists(new Uri(new Uri(TestUrl), $"api/settings/values?component={ProjectKey}"), new HttpRequestException("Forbidden"));

            var service = new SonarQubeWebServer(downloaderMock.Object, new Version("6.3.0.0"), logger, null);

            Action action = () => _ = service.GetProperties(ProjectKey, null).Result;
            action.Should().Throw<HttpRequestException>();

            logger.Errors.Should().HaveCount(1);
        }

        [TestMethod]
        [DataRow("http://myhost:222/")]
        [DataRow("http://myhost:222/sonar/")]
        public async Task GetProperties_SQ63AndHigherWithProject_ShouldBeEmpty(string hostUrl)
        {
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(hostUrl)
                                 .SetupTryDownloadIfExists(new Uri(WebUtils.CreateUri(hostUrl), "api/settings/values?component=key"), "{ settings: [ ] }");
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("6.3"), logger, null);

            var properties = await sut.GetProperties("key", null);

            properties.Should().BeEmpty();
        }

        [TestMethod]
        [DataRow("http://myhost:222/")]
        [DataRow("http://myhost:222/sonar/")]
        public async Task GetProperties_OlderThanSQ63_ShouldBeEmpty(string hostUrl)
        {
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(hostUrl)
                                 .SetupDownload(new Uri(WebUtils.CreateUri(hostUrl), "api/properties?resource=key"), "[]");
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("6.2.9"), logger, null);

            var properties = await sut.GetProperties("key", null);

            properties.Should().BeEmpty();
        }

        [TestMethod]
        [DataRow("http://myhost:222/")]
        [DataRow("http://myhost:222/sonar/")]
        public async Task GetProperties_SQ63AndHigherWithoutProject_ShouldBeEmpty(string hostUrl)
        {
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(hostUrl)
                                 .SetupTryDownloadIfExists(new Uri(WebUtils.CreateUri(hostUrl), "api/settings/values?component=key"))
                                 .SetupDownload(new Uri(WebUtils.CreateUri(hostUrl), "api/settings/values"), "{ settings: [ ] }");
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("6.3"), logger, null);

            var properties = await sut.GetProperties("key", null);

            properties.Should().BeEmpty();
        }

        [TestMethod]
        [DataRow("http://myhost:222/", "http://myhost:222/api/editions/is_valid_license")]
        [DataRow("http://myhost:222/sonar/", "http://myhost:222/sonar/api/editions/is_valid_license")]
        public async Task IsServerLicenseValid_RequestUrl(string hostUrl, string licenseUrl)
        {
            var downloaderMock = MockIDownloaderHelper
                                 .CreateMock()
                                 .SetupGetBaseUri(TestUrl)
                                 .SetupTryGetLicenseInformation(new Uri($"{TestUrl}/api/editions/is_valid_license"), @"{ ""isValidLicense"": true }");
            sut = new SonarQubeWebServer(downloaderMock.Object, version, logger, null);

            var isValid = await sut.IsServerLicenseValid();

            isValid.Should().BeTrue();
        }

        [TestMethod]
        public async Task DownloadCache_NullArgument()
        {
            (await sut.Invoking(x => x.DownloadCache(null)).Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("localSettings");
        }

        [TestMethod]
        [DataRow("9.8", "", "", "Incremental PR analysis is available starting with SonarQube 9.9 or later.")]
        [DataRow("9.9", "", "", "Incremental PR analysis: ProjectKey parameter was not provided.")]
        [DataRow("9.9", "BestProject", "", "Incremental PR analysis: Base branch parameter was not provided.")]
        public async Task DownloadCache_InvalidArguments(string version, string projectKey, string branch, string debugMessage)
        {
            sut = new SonarQubeWebServer(downloader, new Version(version), logger, null);
            var localSettings = CreateLocalSettings(projectKey, branch);

            var result = await sut.DownloadCache(localSettings);

            result.Should().BeEmpty();
            logger.AssertSingleInfoMessageExists(debugMessage);
        }

        [TestMethod]
        [DataRow("http://myhost:222/api/analysis_cache/get?project=project-key&branch=project-branch")]
        public async Task DownloadCache_RequestUrl(string downloadUrl)
        {
            using Stream stream = new MemoryStream();
            var mockDownloader = MockIDownloaderHelper.CreateMock()
                                                      .SetupGetBaseUri(TestUrl)
                                                      .SetupDownloadStream(new Uri(downloadUrl), stream);
            sut = new SonarQubeWebServer(mockDownloader.Object, version, logger, null);
            var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);

            var result = await sut.DownloadCache(localSettings);

            result.Should().BeEmpty();
            mockDownloader.Verify();
        }

        [TestMethod]
        public async Task DownloadCache_DeserializesMessage()
        {
            using var stream = CreateCacheStream(new SensorCacheEntry { Key = "key", Data = ByteString.CopyFromUtf8("value") });
            sut = new SonarQubeWebServer(MockIDownloader(stream), version, logger, null);
            var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);

            var result = await sut.DownloadCache(localSettings);

            result.Should().ContainSingle();
            result.Single(x => x.Key == "key").Data.ToStringUtf8().Should().Be("value");
            logger.AssertInfoLogged("Downloading cache. Project key: project-key, branch: project-branch.");
        }

        [TestMethod]
        public async Task DownloadCache_WhenDownloadStreamReturnsNull_ReturnsEmptyAndLogsException()
        {
            sut = new SonarQubeWebServer(MockIDownloader(null), version, logger, null);

            var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);
            var result = await sut.DownloadCache(localSettings);

            result.Should().BeEmpty();
            logger.AssertSingleWarningExists("Incremental PR analysis: an error occurred while retrieving the cache entries! Object reference not set to an instance of an object.");
        }

        [TestMethod]
        public async Task DownloadCache_WhenDownloadStreamReturnsEmpty_ReturnsEmpty()
        {
            sut = new SonarQubeWebServer(MockIDownloader(new MemoryStream()), version, logger, null);

            var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);
            var result = await sut.DownloadCache(localSettings);

            result.Should().BeEmpty();
            logger.DebugMessages.Should().BeEmpty();
        }

        [TestMethod]
        public async Task DownloadCache_WhenDownloadStreamThrows_ReturnsEmptyAndLogsException()
        {
            var downloaderMock = MockIDownloaderHelper.CreateMock().SetupGetBaseUri(TestUrl).SetupDownloadStream(new HttpRequestException());
            sut = new SonarQubeWebServer(downloaderMock.Object, version, logger, null);

            var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);
            var result = await sut.DownloadCache(localSettings);

            result.Should().BeEmpty();
            logger.AssertSingleWarningExists("Incremental PR analysis: an error occurred while retrieving the cache entries! Exception of type 'System.Net.Http.HttpRequestException' was thrown.");
        }

        [TestMethod]
        public async Task DownloadCache_WhenCacheStreamReadThrows_ReturnsEmptyAndLogsException()
        {
            var streamMock = new Mock<Stream>();
            streamMock.Setup(x => x.Length).Throws<InvalidOperationException>();
            sut = new SonarQubeWebServer(MockIDownloader(streamMock.Object), version, logger, null);
            var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);

            var result = await sut.DownloadCache(localSettings);

            result.Should().BeEmpty();
            logger.AssertSingleWarningExists("Incremental PR analysis: an error occurred while retrieving the cache entries! Operation is not valid due to the current state of the object.");
        }

        [TestMethod]
        public async Task DownloadCache_WhenCacheStreamDeserializeThrows_ReturnsEmptyAndLogsException()
        {
            var invalidProtoStream = new MemoryStream(new byte[] { 42, 42 }); // this is a random byte array that fails deserialization
            sut = new SonarQubeWebServer(MockIDownloader(invalidProtoStream), version, logger, null);
            var localSettings = CreateLocalSettings(ProjectKey, ProjectBranch);

            var result = await sut.DownloadCache(localSettings);

            result.Should().BeEmpty();
            logger.AssertSingleWarningExists("Incremental PR analysis: an error occurred while retrieving the cache entries! While parsing a protocol message, the input ended unexpectedly in the middle of a field.  This could mean either that the input has been truncated or that an embedded message misreported its own length.");
        }

        private static Stream CreateCacheStream(IMessage message)
        {
            var stream = new MemoryStream();
            message.WriteDelimitedTo(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        private static IDownloader MockIDownloader(Stream stream) =>
            MockIDownloaderHelper.CreateMock().SetupGetBaseUri(TestUrl).SetupDownloadStream(stream).Object;

        private static ProcessedArgs CreateLocalSettings(string projectKey, string branch, string organization = "placeholder", string token = "placeholder")
        {
            var args = new Mock<ProcessedArgs>();
            args.SetupGet(a => a.ProjectKey).Returns(projectKey);
            args.SetupGet(a => a.Organization).Returns(organization);
            args.Setup(a => a.TryGetSetting(It.Is<string>(x => x == SonarProperties.PullRequestBase), out branch)).Returns(!string.IsNullOrWhiteSpace(branch));
            args.Setup(a => a.TryGetSetting(It.Is<string>(x => x == SonarProperties.SonarUserName), out token)).Returns(!string.IsNullOrWhiteSpace(token));
            return args.Object;
        }
    }
}
