using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;
using SonarScanner.MSBuild.PreProcessor.WebService;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class SonarCloudWebServiceTest
    {
        private const string ProjectKey = "project-key";
        private const string ProjectBranch = "project-branch";

        private Uri serverUrl;
        private SonarCloudWebService sut;
        private TestDownloader downloader;
        private Uri uri;
        private Version version;
        private TestLogger logger;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Init()
        {
            serverUrl = new Uri("http://localhost/relative/");

            downloader = new TestDownloader();
            uri = new Uri("http://myhost:222");
            version = new Version("5.6");
            logger = new TestLogger();
            sut = new SonarCloudWebService(downloader, uri, version, logger);
        }

        [TestCleanup]
        public void Cleanup() =>
            sut?.Dispose();

        [TestMethod]
        public void IsLicenseValid_IsSonarCloud_ShouldReturnTrue()
        {
            var sut = new SonarCloudWebService(downloader, uri, version, logger);
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = "8.0.0.68001";

            sut.IsServerLicenseValid().Result.Should().BeTrue();
        }

        [TestMethod]
        public void WarnIfDeprecated_ShouldNotWarn()
        {
            sut = new SonarCloudWebService(downloader, uri, new Version("0.0.1"), logger);

            sut.WarnIfSonarQubeVersionIsDeprecated();

            logger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        public void IsLicenseValid_AlwaysValid()
        {
            downloader.Pages[new Uri("http://myhost:222/api/editions/is_valid_license")] = @"{ ""isValidLicense"": false }";
            sut.IsServerLicenseValid().Result.Should().BeTrue();
        }


        [TestMethod]
        public void GetProperties_Success()
        {
            downloader.Pages[new Uri("http://myhost:222/api/settings/values?component=comp")] =
                @"{ settings: [
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
                ]}";

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
        public void GetProperties_NullProjectKey_Throws()
        {
            // Arrange
            var testSubject = new SonarQubeWebService(new TestDownloader(), uri, version, logger);
            Action act = () => _ = testSubject.GetProperties(null, null).Result;

            // Act & Assert
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("projectKey");
        }

        [TestMethod]
        public async Task IsServerLicenseValid_AlwaysTrue()
        {
            var isValid = await sut.IsServerLicenseValid();

            isValid.Should().BeTrue();
            logger.AssertDebugMessageExists("SonarCloud detected, skipping license check.");
        }
    }
}
