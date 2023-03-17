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

        private SonarQubeWebServer sut;

        public SonarQubeWebServerTest()
        {
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
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(TestUrl));
            downloaderMock.Setup(x => x.TryGetLicenseInformation(It.IsAny<Uri>()))
                          .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(@"{ ""isValidLicense"": false }") });
            sut = new SonarQubeWebServer(downloaderMock.Object, version, logger, null);

            sut.IsServerLicenseValid().Result.Should().BeFalse();
        }

        [TestMethod]
        public void IsServerLicenseValid_Commercial_AuthNotForced_LicenseIsValid()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(TestUrl));
            downloaderMock.Setup(x => x.TryGetLicenseInformation(new Uri($"{TestUrl}/api/editions/is_valid_license")))
                          .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(@"{ ""isValidLicense"": true }") });
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
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(TestUrl));
            downloaderMock.Setup(x => x.TryGetLicenseInformation(new Uri($"{TestUrl}/api/editions/is_valid_license")))
                          .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound, Content = new StringContent(@"{""errors"":[{""msg"":""License not found""}]}") });
            sut = new SonarQubeWebServer(downloaderMock.Object, version, logger, null);

            sut.IsServerLicenseValid().Result.Should().BeFalse();
        }

        [TestMethod]
        public void IsServerLicenseValid_CE_SkipLicenseCheck()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(TestUrl));
            downloaderMock.Setup(x => x.TryGetLicenseInformation(new Uri($"{TestUrl}/api/editions/is_valid_license")))
                          .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound, Content = new StringContent(@"{""errors"":[{""msg"":""Unknown url: /api/editions/is_valid_license""}]}") });
            sut = new SonarQubeWebServer(downloaderMock.Object, version, logger, null);

            sut.IsServerLicenseValid().Result.Should().BeTrue();
        }

        [TestMethod]
        [DataRow("foo bar", "my org")]
        public async Task TryGetQualityProfile_OrganizationProfile_QualityProfileUrlContainsOrganization(string projectKey, string organization)
        {
            const string profileKey = "orgProfile";
            const string language = "cs";
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(uri);
            downloaderMock.Setup(x => x.TryDownloadIfExists(It.IsAny<Uri>(), It.IsAny<bool>()))
                          .ReturnsAsync(Tuple.Create(true, $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}"));
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("9.9"), logger, organization);

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
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(uri);
            downloaderMock.Setup(x => x.TryDownloadIfExists(new Uri(uri, $"api/qualityprofiles/search?project={WebUtility.UrlEncode($"{projectKey}")}"), It.IsAny<bool>()))
                          .ReturnsAsync(Tuple.Create(true, $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}"));
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("6.2"), logger, organization);

            var result = await sut.TryGetQualityProfile(projectKey, null, language);

            result.Item1.Should().BeTrue();
            result.Item2.Should().Be(profileKey);
        }

        [TestMethod]
        public void TryGetQualityProfile_MultipleQPForSameLanguage_ShouldThrow()
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(TestUrl));
            downloaderMock.Setup(x => x.TryDownloadIfExists(new Uri($"{TestUrl}/api/qualityprofiles/search?project=foo+bar"), It.IsAny<bool>()))
                          .ReturnsAsync(Tuple.Create(true, "{ profiles: [{\"key\":\"profile1k\",\"name\":\"profile1\",\"language\":\"cs\", \"isDefault\": false}, {\"key\":\"profile4k\",\"name\":\"profile4\",\"language\":\"cs\", \"isDefault\": true}]}"));
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
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(TestUrl));
            downloaderMock.Setup(x => x.TryDownloadIfExists(new Uri($"{TestUrl}/api/settings/values?component=comp"), It.IsAny<bool>()))
                          .ReturnsAsync(Tuple.Create(true, @"{ settings: [
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
                ]}"));
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
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(TestUrl));
            downloaderMock.Setup(x => x.TryDownloadIfExists(new Uri($"{TestUrl}/api/settings/values?component={componentName}"), It.IsAny<bool>())).ReturnsAsync(Tuple.Create(false, (string)null));
            downloaderMock.Setup(x => x.Download(new Uri($"{TestUrl}/api/settings/values"), It.IsAny<bool>())).ReturnsAsync(@"{ settings: [ { key: ""key"", value: ""42"" } ]}");
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("6.3"), logger, null);

            var result = await sut.GetProperties(componentName, null);

            result.Should().ContainSingle().And.ContainKey("key");
            result["key"].Should().Be("42");
        }

        [TestMethod]
        public async Task GetProperties_Sq63_MissingValue_Throws()
        {
            const string componentName = "nonexistent-component";
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(TestUrl));
            downloaderMock.Setup(x => x.TryDownloadIfExists(new Uri($"{TestUrl}/api/settings/values?component={componentName}"), It.IsAny<bool>())).ReturnsAsync(Tuple.Create(false, (string)null));
            downloaderMock.Setup(x => x.Download(new Uri($"{TestUrl}/api/settings/values"), It.IsAny<bool>())).ReturnsAsync(@"{ settings: [ { key: ""key"" } ]}");
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
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(TestUrl));
            downloaderMock.Setup(x => x.Download(new Uri("http://myhost:222/api/properties?resource=foo+bar"), It.IsAny<bool>()))
                          .ReturnsAsync("[{\"key\": \"sonar.property1\",\"value\": \"value1\"},{\"key\": \"sonar.property2\",\"value\": \"value2\"},{\"key\": \"sonar.cs.msbuild.testProjectPattern\",\"value\": \"pattern\"}]");
            // This test includes a regression scenario for SONARMSBRU-187:
            // Requesting properties for project:branch should return branch-specific data

            // Check that properties are correctly defaulted as well as branch-specific
            downloaderMock.Setup(x => x.Download(new Uri("http://myhost:222/api/properties?resource=foo+bar%3AaBranch"), It.IsAny<bool>()))
                          .ReturnsAsync("[{\"key\": \"sonar.property1\",\"value\": \"anotherValue1\"},{\"key\": \"sonar.property2\",\"value\": \"anotherValue2\"}]");
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("5.6"), logger, null);

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
            var downloaderMock = new Mock<IDownloader>(MockBehavior.Strict);
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(TestUrl));
            downloaderMock.Setup(x => x.Download(new Uri($"{TestUrl}/api/properties?resource={ProjectKey}"), It.IsAny<bool>())).Throws(new HttpRequestException("Forbidden"));

            var service = new SonarQubeWebServer(downloaderMock.Object, new Version("1.2.3.4"), logger, null);

            Func<Task> action = async () => await service.GetProperties(ProjectKey, null);
            await action.Should().ThrowAsync<HttpRequestException>();

            logger.Errors.Should().HaveCount(1);
        }

        [TestMethod]
        public void GetProperties_Sq63plus_Forbidden()
        {
            var downloaderMock = new Mock<IDownloader>(MockBehavior.Strict);
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(TestUrl));
            downloaderMock.Setup(x => x.TryDownloadIfExists(It.IsAny<Uri>(), It.IsAny<bool>())).Throws(new HttpRequestException("Forbidden"));

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
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(hostUrl));
            downloaderMock.Setup(x => x.TryDownloadIfExists(It.IsAny<Uri>(), It.IsAny<bool>())).ReturnsAsync(Tuple.Create(true, "{ settings: [ ] }")).Verifiable();
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("6.3"), logger, null);

            var properties = await sut.GetProperties("key", null);

            properties.Should().BeEmpty();
            downloaderMock.Verify();
        }

        [TestMethod]
        [DataRow("http://myhost:222/")]
        [DataRow("http://myhost:222/sonar/")]
        public async Task GetProperties_OlderThanSQ63_ShouldBeEmpty(string hostUrl)
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(hostUrl));
            downloaderMock.Setup(x => x.Download(It.IsAny<Uri>(), It.IsAny<bool>())).ReturnsAsync("[]").Verifiable();
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("6.2.9"), logger, null);

            var properties = await sut.GetProperties("key", null);

            properties.Should().BeEmpty();
            downloaderMock.Verify();
        }

        [TestMethod]
        [DataRow("http://myhost:222/")]
        [DataRow("http://myhost:222/sonar/")]
        public async Task GetProperties_SQ63AndHigherWithoutProject_ShouldBeEmpty(string hostUrl)
        {
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(hostUrl));
            downloaderMock.Setup(x => x.TryDownloadIfExists(It.IsAny<Uri>(), It.IsAny<bool>())).ReturnsAsync(Tuple.Create(false, (string)null)).Verifiable();
            downloaderMock.Setup(x => x.Download(It.IsAny<Uri>(), It.IsAny<bool>())).ReturnsAsync("{ settings: [ ] }").Verifiable();
            sut = new SonarQubeWebServer(downloaderMock.Object, new Version("6.3"), logger, null);

            var properties = await sut.GetProperties("key", null);

            properties.Should().BeEmpty();
            downloaderMock.Verify();
        }

        [TestMethod]
        [DataRow("http://myhost:222/", "http://myhost:222/api/editions/is_valid_license")]
        [DataRow("http://myhost:222/sonar/", "http://myhost:222/sonar/api/editions/is_valid_license")]
        public async Task IsServerLicenseValid_RequestUrl(string hostUrl, string licenseUrl)
        {
            var mockDownloader = new Mock<IDownloader>();
            mockDownloader.Setup(x => x.GetBaseUri()).Returns(new Uri(hostUrl));
            mockDownloader.Setup(x => x.TryGetLicenseInformation(new Uri(licenseUrl)))
                          .ReturnsAsync(new HttpResponseMessage { Content = new StringContent(@"{ ""isValidLicense"": true }"), StatusCode = HttpStatusCode.OK}).Verifiable();
            sut = new SonarQubeWebServer(mockDownloader.Object, version, logger, null);

            var isValid = await sut.IsServerLicenseValid();

            isValid.Should().BeTrue();
            mockDownloader.Verify();
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
        [DataRow("http://myhost:222", "http://myhost:222/api/analysis_cache/get?project=project-key&branch=project-branch")]
        [DataRow("http://myhost:222/", "http://myhost:222/api/analysis_cache/get?project=project-key&branch=project-branch")]
        [DataRow("http://myhost:222/sonar/", "http://myhost:222/sonar/api/analysis_cache/get?project=project-key&branch=project-branch")]
        public async Task DownloadCache_RequestUrl(string hostUrl, string downloadUrl)
        {
            using Stream stream = new MemoryStream();
            var mockDownloader = new Mock<IDownloader>();
            mockDownloader.Setup(x => x.GetBaseUri()).Returns(new Uri(hostUrl));
            mockDownloader.Setup(x => x.DownloadStream(new Uri(downloadUrl))).ReturnsAsync(stream).Verifiable();
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
            var downloaderMock = new Mock<IDownloader>();
            downloaderMock.Setup(x => x.GetBaseUri()).Returns(new Uri(TestUrl));
            downloaderMock.Setup(x => x.DownloadStream(It.IsAny<Uri>())).ThrowsAsync(new HttpRequestException());
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
            Mock.Of<IDownloader>(x => x.DownloadStream(It.IsAny<Uri>()) == Task.FromResult(stream) && x.GetBaseUri() == new Uri(TestUrl));

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
