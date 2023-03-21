﻿/*
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
using System.Net;
using System.Net.Http;
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
        public async Task CreateSonarWebService_RequestServerVersionFailedDueToGenericException_ShouldReturnNullAndLogAnError()
        {
            var sut = new PreprocessorObjectFactory(logger);
            var downloader =  new Mock<IDownloader>(MockBehavior.Strict);
            downloader.Setup(x => x.Download(It.IsAny<Uri>(), It.IsAny<bool>())).Throws<InvalidOperationException>();

            var result = await sut.CreateSonarWebServer(CreateValidArguments(), downloader.Object);

            result.Should().BeNull();
            logger.AssertNoWarningsLogged();
            logger.AssertSingleErrorExists("An error occured when calling 'http://myhost:222/api/server/version': Operation is not valid due to the current state of the object.");
        }

        [TestMethod]
        public async Task CreateSonarWebService_RequestServerVersionFailedDueToHttpRequestException_ShouldReturnNullAndLogAnError()
        {
            var sut = new PreprocessorObjectFactory(logger);
            var exception = new HttpRequestException(string.Empty, new WebException(string.Empty, WebExceptionStatus.ConnectFailure));
            var downloader =  new Mock<IDownloader>(MockBehavior.Strict);
            downloader.Setup(x => x.Download(It.IsAny<Uri>(), It.IsAny<bool>())).Throws(exception);

            var result = await sut.CreateSonarWebServer(CreateValidArguments(), downloader.Object);

            result.Should().BeNull();
            logger.AssertNoWarningsLogged();
            logger.AssertSingleErrorExists("Unable to connect to server. Please check if the server is running and if the address is correct. Url: 'http://myhost:222/api/server/version'.");
        }

        [DataTestMethod]
        [DataRow("8.0", typeof(SonarCloudWebServer))]
        [DataRow("9.9", typeof(SonarQubeWebServer))]
        public async Task CreateSonarWebServer_CorrectServiceType(string version, Type serviceType)
        {
            var sut = new PreprocessorObjectFactory(logger);
            var downloader = new Mock<IDownloader>(MockBehavior.Strict);
            downloader.Setup(x => x.Download(It.IsAny<Uri>(), It.IsAny<bool>())).ReturnsAsync(version);

            var service = await sut.CreateSonarWebServer(CreateValidArguments(), downloader.Object);

            service.Should().BeOfType(serviceType);
        }

        [TestMethod]
        public async Task ValidCallSequence_ValidObjectReturned()
        {
            var downloader = new Mock<IDownloader>(MockBehavior.Strict);
            downloader.Setup(x => x.Download(new Uri("http://myhost:222/api/server/version"), It.IsAny<bool>())).ReturnsAsync("8.9");
            var validArgs = CreateValidArguments();
            var sut = new PreprocessorObjectFactory(logger);

            var server = await sut.CreateSonarWebServer(validArgs, downloader.Object);
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

        private ProcessedArgs CreateValidArguments(string hostUrl = "http://myhost:222")
        {
            var cmdLineArgs = new ListPropertiesProvider(new[] { new Property(SonarProperties.HostUrl, hostUrl) });
            return new ProcessedArgs("key", "name", "version", "organization", false, cmdLineArgs, new ListPropertiesProvider(), EmptyPropertyProvider.Instance, logger);
        }
    }
}
