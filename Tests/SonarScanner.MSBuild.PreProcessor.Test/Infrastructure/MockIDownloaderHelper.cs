using System;
using Moq;

namespace SonarScanner.MSBuild.PreProcessor.Test.Infrastructure
{
    internal static class MockIDownloaderHelper
    {
        /// <summary>
        /// Create basic mocked instance of <see cref="IDownloader"/> with
        /// </summary>
        /// <returns>The mocked instance <see cref="IDownloader"/>.</returns>
        public static Mock<IDownloader> CreateMock()
        {
            var mock = new Mock<IDownloader>(MockBehavior.Strict);
            mock.Setup(x => x.Dispose());

            return mock;
        }

        /// <summary>
        /// Setup the <see cref="IDownloader.TryDownloadIfExists"/> method for the current mocked <see cref="IDownloader"/>.
        /// </summary>
        /// <param name="mock">The mocked <see cref="IDownloader"/>.</param>
        /// <param name="requestUri"><see cref="Uri"/> of the request resource, if null, will match any url.</param>
        /// <param name="response">Response content, if null, mocked response will be: <code>(false, null)</code></param>
        /// <returns>The mocked <see cref="IDownloader"/>.</returns>
        public static Mock<IDownloader> SetupTryDownloadIfExists(this Mock<IDownloader> mock, Uri requestUri = null, string response = null)
        {
            if (requestUri is null)
            {
                mock
                    .Setup(x => x.TryDownloadIfExists(It.IsAny<Uri>(), It.IsAny<bool>()))
                    .ReturnsAsync(Tuple.Create(response != null, response));
            }
            else
            {
                mock
                    .Setup(x => x.TryDownloadIfExists(It.Is<Uri>(dlUri => dlUri == requestUri), It.IsAny<bool>()))
                    .ReturnsAsync(Tuple.Create(response != null, response));
            }

            return mock;
        }

        /// <summary>
        /// Setup the <see cref="IDownloader.Download"/> method for the current mocked <see cref="IDownloader"/>.
        /// </summary>
        /// <param name="mock">The mocked <see cref="IDownloader"/>.</param>
        /// <param name="requestUri"><see cref="Uri"/> of the request resource, if null, will match any url.</param>
        /// <param name="response">Response content, if null, mocked response will be: <code>(false, null)</code></param>
        /// <returns>The mocked <see cref="IDownloader"/>.</returns>
        public static Mock<IDownloader> SetupDownload(this Mock<IDownloader> mock, Uri requestUri, string response)
        {
            if (requestUri is null)
            {
                mock
                    .Setup(x => x.Download(It.IsAny<Uri>(), It.IsAny<bool>()))
                    .ReturnsAsync(response);
            }
            else
            {
                mock
                    .Setup(x => x.Download(It.Is<Uri>(dlUri => dlUri == requestUri), It.IsAny<bool>()))
                    .ReturnsAsync(response);
            }

            return mock;
        }
    }
}
