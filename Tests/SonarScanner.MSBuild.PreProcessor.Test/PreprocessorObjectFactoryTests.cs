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
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;
using SonarScanner.MSBuild.PreProcessor.WebServer;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class PreprocessorObjectFactoryTests
    {
        private TestLogger logger;

        [TestInitialize]
        public void TestInitialize() =>
            logger = new TestLogger();

        [TestMethod]
        public void CreateSonarWebServer_ThrowsOnInvalidInput()
        {
            ((Func<PreprocessorObjectFactory>)(() => new PreprocessorObjectFactory(null))).Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");

            var sut = new PreprocessorObjectFactory(logger);
            sut.Invoking(x => x.CreateSonarWebServer(null).Result).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("args");
        }

        [DataTestMethod]
        [DataRow("https://sonarsource.com/", "https://sonarsource.com/api/server/version")]
        [DataRow("https://sonarsource.com", "https://sonarsource.com/api/server/version")]
        [DataRow("https://sonarsource.com/sonarlint", "https://sonarsource.com/sonarlint/api/server/version")]
        [DataRow("https://sonarsource.com/sonarlint/", "https://sonarsource.com/sonarlint/api/server/version")]
        public async Task CreateSonarWebServer_AppendSlashSuffixWhenMissing(string input, string expected)
        {
            var validArgs = CreateValidArguments(input);
            var sut = new PreprocessorObjectFactory(logger);
            var downloader = new Mock<IDownloader>();
            downloader.Setup(x => x.Download(It.Is<Uri>(uri => uri.ToString() == expected), false))
                .Returns(Task.FromResult("9.9"))
                .Verifiable();

            await sut.CreateSonarWebServer(validArgs, downloader.Object);

            downloader.VerifyAll();
        }

        [DataTestMethod]
        [DataRow("8.0", typeof(SonarCloudWebServer))]
        [DataRow("9.9", typeof(SonarQubeWebServer))]
        public async Task CreateSonarWebServer_CorrectServiceType(string version, Type serviceType)
        {
            var sut = new PreprocessorObjectFactory(logger);
            var downloader = Mock.Of<IDownloader>(x => x.Download(It.IsAny<Uri>(), false) == Task.FromResult(version));

            var service = await sut.CreateSonarWebServer(CreateValidArguments(), downloader);

            service.Should().BeOfType(serviceType);
        }

        [TestMethod]
        public async Task ValidCallSequence_ValidObjectReturned()
        {
            var downloader = new TestDownloader();
            downloader.Pages[new Uri("https://sonarsource.com/api/server/version")] = "8.9";
            var validArgs = CreateValidArguments();
            var sut = new PreprocessorObjectFactory(logger);

            var server = await sut.CreateSonarWebServer(validArgs, downloader);
            server.Should().NotBeNull();
            sut.CreateTargetInstaller().Should().NotBeNull();
            sut.CreateRoslynAnalyzerProvider(server).Should().NotBeNull();
        }

        [TestMethod]
        public void CreateRoslynAnalyzerProvider_NullServer_ThrowsArgumentNullException()
        {
            var sut = new PreprocessorObjectFactory(logger);

            Action act = () => sut.CreateRoslynAnalyzerProvider(null);
            act.Should().ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        public void CreateHttpClient_Authorization()
        {
            AuthorizationHeader(null, null).Should().BeNull();
            AuthorizationHeader(null, "password").Should().BeNull();
            AuthorizationHeader("da39a3ee5e6b4b0d3255bfef95601890afd80709", null).Should().Be("Basic ZGEzOWEzZWU1ZTZiNGIwZDMyNTViZmVmOTU2MDE4OTBhZmQ4MDcwOTo=");
            AuthorizationHeader("da39a3ee5e6b4b0d3255bfef95601890afd80709", string.Empty).Should().Be("Basic ZGEzOWEzZWU1ZTZiNGIwZDMyNTViZmVmOTU2MDE4OTBhZmQ4MDcwOTo=");
            AuthorizationHeader("admin", "password").Should().Be("Basic YWRtaW46cGFzc3dvcmQ=");

            static string AuthorizationHeader(string userName, string password) =>
                GetHeader(PreprocessorObjectFactory.CreateHttpClient(userName, password, null, null), "Authorization");
        }

        [TestMethod]
        public void CreateHttpClient_UserAgent()
        {
            var scannerVersion = typeof(WebClientDownloaderTest).Assembly.GetName().Version.ToDisplayString();
            var client = PreprocessorObjectFactory.CreateHttpClient(null, null, null, null);
            GetHeader(client, "User-Agent").Should().Be($"SonarScanner-for-.NET/{scannerVersion}");

            // This asserts wrong "UserAgent" header. Should be removed as part of https://github.com/SonarSource/sonar-scanner-msbuild/issues/1421
            GetHeader(client, "UserAgent").Should().Be($"ScannerMSBuild/{scannerVersion}");
        }

        [TestMethod]
        public void CreateHttpClient_SemicolonInUsername()
        {
            Action act = () => PreprocessorObjectFactory.CreateHttpClient("user:name", null, null, null);
            act.Should().ThrowExactly<ArgumentException>().WithMessage("username cannot contain the ':' character due to basic authentication limitations");
        }

        [TestMethod]
        public void CreateHttpClient_AccentsInUsername()
        {
            Action act = () => PreprocessorObjectFactory.CreateHttpClient("héhé", "password", null, null);
            act.Should().ThrowExactly<ArgumentException>().WithMessage("username and password should contain only ASCII characters due to basic authentication limitations");
        }

        [TestMethod]
        public void CreateHttpClient_AccentsInPassword()
        {
            Action act = () => PreprocessorObjectFactory.CreateHttpClient("username", "héhé", null, null);
            act.Should().ThrowExactly<ArgumentException>().WithMessage("username and password should contain only ASCII characters due to basic authentication limitations");
        }

        [TestMethod]
        public void CreateHttpClient_UsingClientCert()
        {
            Action act = () => PreprocessorObjectFactory.CreateHttpClient(null, null, "certtestsonar.pem", "dummypw");
            act.Should().NotThrow();
        }

        [TestMethod]
        public void CreateHttpClient_MissingCert() =>
            FluentActions.Invoking(() => PreprocessorObjectFactory.CreateHttpClient(null, null, "missingcert.pem", "dummypw")).Should().Throw<CryptographicException>();

        private ProcessedArgs CreateValidArguments(string hostUrl = "https://sonarsource.com")
        {
            var cmdLineArgs = new ListPropertiesProvider(new[] { new Property(SonarProperties.HostUrl, hostUrl) });
            return new ProcessedArgs("key", "name", "version", "organization", false, cmdLineArgs, new ListPropertiesProvider(), EmptyPropertyProvider.Instance, logger);
        }

        private static string GetHeader(HttpClient client, string header) =>
            client.DefaultRequestHeaders.Contains(header)
                ? string.Join(";", client.DefaultRequestHeaders.GetValues(header))
                : null;
    }
}
