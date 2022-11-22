/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
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
        public void Factory_ThrowsOnInvalidInput()
        {
            Action ctor = () => new PreprocessorObjectFactory(null);
            ctor.Should().ThrowExactly<ArgumentNullException>();

            var sut = new PreprocessorObjectFactory(logger);
            Action callCreateSonarQubeServer = () => sut.CreateSonarQubeServer(null);
            callCreateSonarQubeServer.Should().ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        public void Factory_ValidCallSequence_ValidObjectReturned()
        {
            var validArgs = CreateValidArguments();
            var sut = new PreprocessorObjectFactory(logger);

            object actual = sut.CreateSonarQubeServer(validArgs);
            actual.Should().NotBeNull();

            actual = sut.CreateTargetInstaller();
            actual.Should().NotBeNull();

            actual = sut.CreateRoslynAnalyzerProvider();
            actual.Should().NotBeNull();
        }

        [TestMethod]
        public void Factory_InvalidCallSequence_Fails()
        {
            var sut = new PreprocessorObjectFactory(logger);

            Action act = () => sut.CreateRoslynAnalyzerProvider();
            act.Should().ThrowExactly<InvalidOperationException>();
        }

        [TestMethod]
        public void CreateHttpClient_Authorization()
        {
            GetHeader(PreprocessorObjectFactory.CreateHttpClient(null, null, null, null), HttpRequestHeader.Authorization).Should().BeNull();
            GetHeader(PreprocessorObjectFactory.CreateHttpClient(null, "password", null, null), HttpRequestHeader.Authorization).Should().BeNull();
            GetHeader(PreprocessorObjectFactory.CreateHttpClient("da39a3ee5e6b4b0d3255bfef95601890afd80709", string.Empty, null, null), HttpRequestHeader.Authorization)
                .Should().Be("Basic ZGEzOWEzZWU1ZTZiNGIwZDMyNTViZmVmOTU2MDE4OTBhZmQ4MDcwOTo=");
            GetHeader(PreprocessorObjectFactory.CreateHttpClient("admin", "password", null, null), HttpRequestHeader.Authorization).Should().Be("Basic YWRtaW46cGFzc3dvcmQ=");
        }

        [TestMethod]
        public void CreateHttpClient_UserAgent()
        {
            var userAgent = GetHeader(PreprocessorObjectFactory.CreateHttpClient(null, null, null, null), HttpRequestHeader.UserAgent);

            var scannerVersion = typeof(WebClientDownloaderTest).Assembly.GetName().Version.ToDisplayString();
            userAgent.Should().Be($"ScannerMSBuild/{scannerVersion}");
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

        private static string GetHeader(HttpClient client, HttpRequestHeader header) =>
            client.DefaultRequestHeaders.Contains(header.ToString())
                ? string.Join(";", client.DefaultRequestHeaders.GetValues(header.ToString()))
                : null;

        private static ProcessedArgs CreateValidArguments()
        {
            var logger = new TestLogger();
            var cmdLineArgs = new ListPropertiesProvider();
            cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://sonarsource.com");

            return new ProcessedArgs("key", "name", "version", "organization", false, cmdLineArgs, new ListPropertiesProvider(), EmptyPropertyProvider.Instance, logger);
        }
    }
}
