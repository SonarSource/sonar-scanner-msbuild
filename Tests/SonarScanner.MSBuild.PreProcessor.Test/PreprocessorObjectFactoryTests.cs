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
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
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

        [TestMethod]
        public async Task CreateSonarWebService_RequestServerVersionThrows_ShouldReturnNullAndLogError()
        {
            var sut = new PreprocessorObjectFactory(logger);
            var downloader =  new Mock<IDownloader>(MockBehavior.Strict);
            downloader.Setup(x => x.Download(It.IsAny<string>(), It.IsAny<bool>())).Throws<InvalidOperationException>();

            var result = await sut.CreateSonarWebServer(CreateValidArguments(), downloader.Object);

            result.Should().BeNull();
            logger.AssertNoWarningsLogged();
            logger.AssertSingleErrorExists("An error occured while querying the server version! Please check if the server is running and if the address is correct.");
        }

        [TestMethod]
        public async Task CreateSonarWebService_InvalidHostUrl_ReturnNullAndLogErrors()
        {
            var sut = new PreprocessorObjectFactory(logger);

            var result = await sut.CreateSonarWebServer(CreateValidArguments("http:/myhost:222"), Mock.Of<IDownloader>());

            result.Should().BeNull();
            logger.AssertSingleErrorExists("The value provided for the host URL parameter (http:/myhost:222) is not valid. Please make sure that you have entered a valid URL and try again.");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        public async Task CreateSonarWebService_MissingUriScheme_ReturnNullAndLogErrors()
        {
            var sut = new PreprocessorObjectFactory(logger);

            var result = await sut.CreateSonarWebServer(CreateValidArguments("myhost:222"), Mock.Of<IDownloader>());

            result.Should().BeNull();
            logger.AssertSingleErrorExists("The URL (myhost:222) provided does not contain the scheme. Please include 'http://' or 'https://' at the beginning.");
            logger.AssertNoWarningsLogged();
        }

        [DataTestMethod]
        [DataRow("8.0", typeof(SonarCloudWebServer))]
        [DataRow("9.9", typeof(SonarQubeWebServer))]
        public async Task CreateSonarWebServer_CorrectServiceType(string version, Type serviceType)
        {
            var sut = new PreprocessorObjectFactory(logger);
            var downloader = Mock.Of<IDownloader>(x => x.Download(It.IsAny<string>(), It.IsAny<bool>()) == Task.FromResult(version));

            var service = await sut.CreateSonarWebServer(CreateValidArguments(), downloader);

            service.Should().BeOfType(serviceType);
        }

        [TestMethod]
        public async Task CreateSonarWebServer_ValidCallSequence_ValidObjectReturned()
        {
            var downloader = Mock.Of<IDownloader>(x => x.Download("api/server/version", It.IsAny<bool>()) == Task.FromResult("8.9"));
            var validArgs = CreateValidArguments();
            var sut = new PreprocessorObjectFactory(logger);

            var server = await sut.CreateSonarWebServer(validArgs, downloader);
            
            server.Should().NotBeNull();
            sut.CreateTargetInstaller().Should().NotBeNull();
            sut.CreateRoslynAnalyzerProvider(server, string.Empty).Should().NotBeNull();
        }

        [TestMethod]
        public async Task CreateSonarWebService_WithoutOrganizationOnSonarCloud_ReturnsNullAndLogsAnError()
        {
            var downloader = Mock.Of<IDownloader>(x => x.Download("api/server/version", It.IsAny<bool>()) == Task.FromResult("8.0")); // SonarCloud
            var validArgs = CreateValidArguments(organization: null);
            var sut = new PreprocessorObjectFactory(logger);

            var server = await sut.CreateSonarWebServer(validArgs, downloader);

            server.Should().BeNull();
            logger.AssertSingleErrorExists(@"Organization parameter (/o:""<organization>"") is required and needs to be provided!");
        }

        [TestMethod]
        public void CreateRoslynAnalyzerProvider_NullServer_ThrowsArgumentNullException()
        {
            var sut = new PreprocessorObjectFactory(logger);

            Action act = () => sut.CreateRoslynAnalyzerProvider(null, string.Empty);

            act.Should().ThrowExactly<ArgumentNullException>();
        }

        private ProcessedArgs CreateValidArguments(string hostUrl = "http://myhost:222", string organization = "organization")
        {
            var cmdLineArgs = new ListPropertiesProvider(new[] { new Property(SonarProperties.HostUrl, hostUrl) });
            return new ProcessedArgs("key", "name", "version", organization, false, cmdLineArgs, new ListPropertiesProvider(), EmptyPropertyProvider.Instance, logger);
        }
    }
}
