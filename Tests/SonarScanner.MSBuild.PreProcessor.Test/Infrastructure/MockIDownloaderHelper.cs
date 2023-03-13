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
