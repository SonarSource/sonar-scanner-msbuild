/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarQube.Client;
using SonarQube.Client.Models;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Tests
{
    [TestClass]
    public class SonarQubeServerTest
    {
        private Mock<ISonarQubeService> serviceMock;
        private TestLogger logger;
        private Mock<IFileWrapper> fileWrapperMock;
        private SonarQubeServer server;

        [TestInitialize]
        public void TestInitialize()
        {
            logger = new TestLogger();

            var connectionInfo = new ConnectionInformation(new Uri("http://localhost"));

            serviceMock = new Mock<ISonarQubeService>(MockBehavior.Strict);

            serviceMock.Setup(x => x.ConnectAsync(connectionInfo, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            fileWrapperMock = new Mock<IFileWrapper>();

            server = new SonarQubeServer(serviceMock.Object, connectionInfo, logger);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            serviceMock.VerifyAll();
        }

        [TestMethod]
        public void GetServerVersion_Returns_Version()
        {
            serviceMock
                .SetupGet(x => x.SonarQubeVersion)
                .Returns(new Version("1.2.3"));

            var result = server.GetServerVersion();

            result.Major.Should().Be(1);
            result.Minor.Should().Be(2);
            result.Revision.Should().Be(3);
        }

        [TestMethod]
        public void GetActiveRules_Throws_HttpRequestException()
        {
            serviceMock
                .Setup(x => x.GetRulesAsync(true, "quality-profile", CancellationToken.None))
                .Throws(GetHttpRequestException(HttpStatusCode.NotFound));

            var action = new Action(() => server.GetActiveRules("quality-profile"));

            action.Should().ThrowExactly<HttpRequestException>();

            logger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        public void GetActiveRules_Returns_Rules()
        {
            IList<SonarQubeRule> rules = new []
            {
                new SonarQubeRule("rule1", "repo1", true),
                new SonarQubeRule("rule2", "repo2", true),
            };

            serviceMock
                .Setup(x => x.GetRulesAsync(true, "quality-profile", CancellationToken.None))
                .Returns(Task.FromResult(rules));

            server.GetActiveRules("quality-profile").Should().BeEquivalentTo(rules);
        }

        [TestMethod]
        public void GetInactiveRules_Throws_HttpRequestException()
        {
            serviceMock
                .Setup(x => x.GetRulesAsync(false, "quality-profile", CancellationToken.None))
                .Throws(GetHttpRequestException(HttpStatusCode.NotFound));

            var action = new Action(() => server.GetInactiveRules("quality-profile", "ignored"));

            action.Should().ThrowExactly<HttpRequestException>();

            logger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        public void GetInactiveRules_Returns_Rules()
        {
            IList<SonarQubeRule> rules = new[]
            {
                new SonarQubeRule("rule1", "repo1", false),
                new SonarQubeRule("rule2", "repo2", false),
            };

            serviceMock
                .Setup(x => x.GetRulesAsync(false, "quality-profile", CancellationToken.None))
                .Returns(Task.FromResult(rules));

            server.GetInactiveRules("quality-profile", "ignored").Should().BeEquivalentTo(rules);
        }

        [TestMethod]
        public void GetAllLanguages_Throws_HttpRequestException()
        {
            serviceMock
                .Setup(x => x.GetAllLanguagesAsync(CancellationToken.None))
                .Throws(GetHttpRequestException(HttpStatusCode.NotFound));

            var action = new Action(() => server.GetAllLanguages());

            action.Should().ThrowExactly<HttpRequestException>();

            logger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        public void GetAllLanguages_Returns_Languages()
        {
            IList<SonarQubeLanguage> languages = new[]
            {
                new SonarQubeLanguage("csharp", "C#"),
                new SonarQubeLanguage("vbnet", "VB.NET"),
            };

            serviceMock
                .Setup(x => x.GetAllLanguagesAsync(CancellationToken.None))
                .Returns(Task.FromResult(languages));

            var result = server.GetAllLanguages();

            result.Should().BeEquivalentTo("csharp", "vbnet");

            logger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        public void GetAllLanguages_Returns_Null()
        {
            // This should never happen, ISonarQubeService is not supposed to return null.
            // We code it defensively, though.
            serviceMock
                .Setup(x => x.GetAllLanguagesAsync(CancellationToken.None))
                .Returns(Task.FromResult((IList<SonarQubeLanguage>)null));

            var result = server.GetAllLanguages();

            result.Should().BeNull();

            logger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        public void GetProperties_Throws_HttpRequestException_Forbidden()
        {
            serviceMock
                .Setup(x => x.GetAllPropertiesAsync("project-key:branch", CancellationToken.None))
                .Throws(GetHttpRequestException(HttpStatusCode.Forbidden));

            var action = new Action(() => server.GetProperties("project-key", "branch"));

            action.Should().ThrowExactly<HttpRequestException>();

            logger.Warnings.Should()
                .Contain("To analyze private projects make sure the scanner user has 'Browse' permission.");
        }

        [TestMethod]
        public void GetProperties_Throws_HttpRequestException_Other()
        {
            serviceMock
                .Setup(x => x.GetRulesAsync(false, "quality-profile", CancellationToken.None))
                .Throws(GetHttpRequestException(HttpStatusCode.NotFound));

            var action = new Action(() => server.GetInactiveRules("quality-profile", "ignored"));

            action.Should().ThrowExactly<HttpRequestException>();

            logger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        public void GetProperties_Returns_Properties()
        {
            IList<SonarQubeProperty> properties = new[]
            {
                new SonarQubeProperty("key1", "value1"),
                new SonarQubeProperty("key2", "value2"),
            };

            serviceMock
                .Setup(x => x.GetAllPropertiesAsync("project-key:branch-name", CancellationToken.None))
                .Returns(Task.FromResult(properties));

            var result = server.GetProperties("project-key", "branch-name");

            result.Keys.Should().BeEquivalentTo("key1", "key2");
            result.Values.Should().BeEquivalentTo("value1", "value2");
        }

        [TestMethod]
        public void GetProperties_Service_Returns_Null()
        {
            // This should never happen, ISonarQubeService is not supposed to return null.
            // We code it defensively, though.
            serviceMock
                .Setup(x => x.GetAllPropertiesAsync("project-key:branch-name", CancellationToken.None))
                .Returns(Task.FromResult<IList<SonarQubeProperty>>(null));

            var result = server.GetProperties("project-key", "branch-name");

            result.Should().BeNull();
        }

        [TestMethod]
        public void GetProperties_TestProjectPattern_Old_Property_Replaced()
        {
            IList<SonarQubeProperty> properties = new[]
            {
                // This is the old, deprecated property key, it should be replaced with a new one
                new SonarQubeProperty("sonar.cs.msbuild.testProjectPattern", "some pattern"),
            };

            serviceMock
                .Setup(x => x.GetAllPropertiesAsync("project-key:branch-name", CancellationToken.None))
                .Returns(Task.FromResult(properties));

            var result = server.GetProperties("project-key", "branch-name");

            result.Keys.Should().BeEquivalentTo("sonar.msbuild.testProjectPattern"); // used to be "sonar.cs.msbuild.testProjectPattern"
            result.Values.Should().BeEquivalentTo("some pattern");

            logger.Warnings.Should()
                .Contain("The property 'sonar.cs.msbuild.testProjectPattern' defined in SonarQube is deprecated. Set the property 'sonar.msbuild.testProjectPattern' in the scanner instead.");
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_Throws_HttpRequestException_NotFound()
        {
            serviceMock
                .Setup(x => x.DownloadStaticFileAsync("plugin-key", "file-name.ext", CancellationToken.None))
                .Throws(GetHttpRequestException(HttpStatusCode.NotFound));

            var result = server.TryDownloadEmbeddedFile("plugin-key", "file-name.ext", "ignored");

            result.Should().BeFalse();

            logger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_Throws_HttpRequestException_Other()
        {
            serviceMock
                .Setup(x => x.DownloadStaticFileAsync("plugin-key", "file-name.ext", CancellationToken.None))
                .Throws(GetHttpRequestException(HttpStatusCode.Forbidden));

            var action = new Action(() => server.TryDownloadEmbeddedFile("plugin-key", "file-name.ext", "ignored"));

            action.Should().ThrowExactly<HttpRequestException>();

            logger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        public void TryDownloadEmbeddedFile_Throws_HttpRequestException_Other()
        {
            serviceMock
                .Setup(x => x.DownloadStaticFileAsync("plugin-key", "file-name.ext", CancellationToken.None))
                .Throws(GetHttpRequestException(HttpStatusCode.Forbidden));

            var action = new Action(() => server.TryDownloadEmbeddedFile("plugin-key", "file-name.ext", "ignored"));

            action.Should().ThrowExactly<HttpRequestException>();

            logger.Warnings.Should().BeEmpty();
        }

        private static Exception GetHttpRequestException(HttpStatusCode statusCode)
        {
            var responseMock = new Mock<HttpWebResponse>();

            responseMock
                .SetupGet(x => x.StatusCode)
                .Returns(statusCode);

            return new HttpRequestException("message",
                new WebException("message", null, WebExceptionStatus.ProtocolError, responseMock.Object));
        }
    }
}
