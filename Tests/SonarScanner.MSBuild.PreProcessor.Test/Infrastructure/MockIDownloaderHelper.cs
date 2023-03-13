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
            if (requestUri is null)
            {
                mock.Setup(x => x.TryDownloadIfExists(It.IsAny<Uri>(), It.IsAny<bool>()))
                    .ReturnsAsync(Tuple.Create(response != null, response));
            }
            else
            {
                mock.Setup(x => x.TryDownloadIfExists(It.Is<Uri>(downloadUri => downloadUri == requestUri), It.IsAny<bool>()))
                    .ReturnsAsync(Tuple.Create(response != null, response));
            }
            return mock;
        }

        public static Mock<IDownloader> SetupDownload(this Mock<IDownloader> mock, Uri requestUri, string response)
        {
            if (requestUri is null)
            {
                mock.Setup(x => x.Download(It.IsAny<Uri>(), It.IsAny<bool>()))
                    .ReturnsAsync(response);
            }
            else
            {
                mock.Setup(x => x.Download(It.Is<Uri>(downloadUri => downloadUri == requestUri), It.IsAny<bool>()))
                    .ReturnsAsync(response);
            }
            return mock;
        }
    }
}
