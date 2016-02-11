//-----------------------------------------------------------------------
// <copyright file="WebClientDownloader.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;
using System.Text;
using System.Net;
using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor
{
    public class WebClientDownloader : IDownloader
    {
        private readonly ILogger logger;
        private readonly WebClient client;

        public WebClientDownloader(string userName, string password, ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            this.logger = logger;

            // SONARMSBRU-169 Support TLS versions 1.0, 1.1 and 1.2
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            if (password == null)
            {
                password = "";
            }

            this.client = new WebClient();
            if (userName != null)
            {
                if (userName.Contains(':'))
                {
                    throw new ArgumentException(Resources.WCD_UserNameCannotContainColon);
                }
                if (!IsAscii(userName) || !IsAscii(password))
                {
                    throw new ArgumentException(Resources.WCD_UserNameMustBeAscii);
                }

                var credentials = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:{1}", userName, password);
                credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
                client.Headers[HttpRequestHeader.Authorization] = "Basic " + credentials;
            }
        }

        public string GetHeader(HttpRequestHeader header)
        {
            return this.client.Headers[header];
        }
 
        #region IDownloaderMethods

        public bool TryDownloadIfExists(string url, out string contents)
        {
            this.logger.LogDebug(Resources.MSG_Downloading, url);
            string data = null;

            bool success = DoIgnoringMissingUrls(() => data = client.DownloadString(url));
            contents = data;
            return success;
        }

        public bool TryDownloadFileIfExists(string url, string targetFilePath)
        {
            this.logger.LogDebug(Resources.MSG_DownloadingFile, url, targetFilePath);

            return DoIgnoringMissingUrls(() => client.DownloadFile(url, targetFilePath));
        }

        public string Download(string url)
        {
            this.logger.LogDebug(Resources.MSG_Downloading, url);

            return client.DownloadString(url);
        }

        #endregion

        #region Private methods

        private static bool IsAscii(string s)
        {
            return !s.Any(c => c > sbyte.MaxValue);
        }

        /// <summary>
        /// Performs the specified web operation
        /// </summary>
        /// <returns>True if the operation completed successfully, false if the url could not be found.
        /// Other web failures will be thrown as expections.</returns>
        private static bool DoIgnoringMissingUrls(Action op)
        {
            try
            {
                op();
                return true;
            }
            catch (WebException e)
            {
                var response = e.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return false;
                }
                throw;
            }
        }

        #endregion

        #region IDisposable implementation

        private bool disposed;

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed && disposing)
            {
                if (this.client != null)
                {
                    this.client.Dispose();
                }
            }

            this.disposed = true;
        }

        #endregion
    }
}
