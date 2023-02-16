using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;
using SonarScanner.MSBuild.PreProcessor.WebService;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    internal class SonarCloudWebServiceTest
    {
        private const string ProjectKey = "project-key";
        private const string ProjectBranch = "project-branch";

        private Uri serverUrl;
        private SonarCloudWebService ws;
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
            ws = new SonarCloudWebService(downloader, uri, version, logger);
        }

        [TestCleanup]
        public void Cleanup() =>
            ws?.Dispose();

        [TestMethod]
        public void IsLicenseValid_IsSonarCloud_ShouldReturnTrue()
        {
            var sut = new SonarCloudWebService(downloader, uri, version, logger);
            downloader.Pages[new Uri("http://myhost:222/api/server/version")] = "8.0.0.68001";

            sut.IsServerLicenseValid().Result.Should().BeTrue();
        }
    }
}
