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
using System.Net;
using System.Net.Http;
using Moq;

namespace SonarScanner.MSBuild.PreProcessor.Test.Infrastructure
{
    internal static class MockIDownloaderHelper
    {
        public static Mock<IDownloader> CreateMock()
        {
            var mock = new Mock<IDownloader>(MockBehavior.Strict);
            mock.Setup(x => x.Dispose());
            return mock;
        }

        public static Mock<IDownloader> SetupTryDownloadIfExists(this Mock<IDownloader> mock, Uri requestUri = null, string response = null)
        {
            mock.Setup(x => x.TryDownloadIfExists(It.Is<Uri>(downloadUri => requestUri == null || downloadUri == requestUri), It.IsAny<bool>()))
                .ReturnsAsync(Tuple.Create(response != null, response))
                .Verifiable();
            return mock;
        }

        public static Mock<IDownloader> SetupTryDownloadIfExists(this Mock<IDownloader> mock, Uri requestUri, Exception exception)
        {
            mock.Setup(x => x.TryDownloadIfExists(It.Is<Uri>(downloadUri => requestUri == null || downloadUri == requestUri), It.IsAny<bool>()))
                .Throws(exception)
                .Verifiable();
            return mock;
        }

        public static Mock<IDownloader> SetupTryDownloadFileIfExists(this Mock<IDownloader> mock, Uri requestUri = null, bool response = false)
        {
            mock.Setup(x => x.TryDownloadFileIfExists(It.Is<Uri>(downloadUri => requestUri == null || downloadUri == requestUri), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(response)
                .Verifiable();
            return mock;
        }

        public static Mock<IDownloader> SetupDownload(this Mock<IDownloader> mock, Uri requestUri, string response = null)
        {
            mock.Setup(x => x.Download(It.Is<Uri>(downloadUri => requestUri == null || downloadUri == requestUri), It.IsAny<bool>()))
                .ReturnsAsync(response)
                .Verifiable();
            return mock;
        }

        public static Mock<IDownloader> SetupDownload(this Mock<IDownloader> mock, Uri requestUri, Exception exception)
        {
            mock.Setup(x => x.Download(It.Is<Uri>(downloadUri => requestUri == null || downloadUri == requestUri), It.IsAny<bool>()))
                .Throws(exception)
                .Verifiable();
            return mock;
        }

        public static Mock<IDownloader> SetupDownload(this Mock<IDownloader> mock, string response) =>
            mock.SetupDownload(null, response);

        public static Mock<IDownloader> SetupDownloadStream(this Mock<IDownloader> mock, Uri requestUri, Stream response)
        {
            mock.Setup(x => x.DownloadStream(It.Is<Uri>(downloadUri => requestUri == null || downloadUri == requestUri)))
                .ReturnsAsync(response)
                .Verifiable();
            return mock;
        }

        public static Mock<IDownloader> SetupDownloadStream(this Mock<IDownloader> mock, Uri requestUri, Exception exception)
        {
            mock.Setup(x => x.DownloadStream(It.Is<Uri>(downloadUri => requestUri == null || downloadUri == requestUri)))
                .Throws(exception)
                .Verifiable();
            return mock;
        }

        public static Mock<IDownloader> SetupDownloadStream(this Mock<IDownloader> mock, Stream response) =>
            mock.SetupDownloadStream(null, response);

        public static Mock<IDownloader> SetupDownloadStream(this Mock<IDownloader> mock, Exception response) =>
            mock.SetupDownloadStream(null, response);

        public static Mock<IDownloader> SetupGetBaseUri(this Mock<IDownloader> mock, Uri baseUri)
        {
            mock.Setup(x => x.GetBaseUri())
                .Returns(baseUri)
                .Verifiable();
            return mock;
        }

        public static Mock<IDownloader> SetupGetBaseUri(this Mock<IDownloader> mock, string baseUri) =>
            mock.SetupGetBaseUri(new Uri(baseUri));

        public static Mock<IDownloader> SetupTryGetLicenseInformation(this Mock<IDownloader> mock, Uri requestUri = null, string response = null, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            mock.Setup(x => x.TryGetLicenseInformation(It.Is<Uri>(downloadUri => requestUri == null || downloadUri == requestUri)))
                .ReturnsAsync(new HttpResponseMessage() { StatusCode = statusCode, Content = new StringContent(response) })
                .Verifiable();
            return mock;
        }
    }
}
