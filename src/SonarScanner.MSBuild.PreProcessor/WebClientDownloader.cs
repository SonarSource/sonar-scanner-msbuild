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
using System.Linq;
using System.Net;
using System.Text;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor
{
    public class WebClientDownloader : IDownloader
    {
        private readonly ILogger logger;
        private readonly CustomWebClient client;

        // WebClient resets certain headers after each request: Accept, Connection, Content-Type, Expect, Referer, User-Agent.
        // This class keeps the User Agent across requests.
        // See https://github.com/SonarSource/sonar-scanner-msbuild/issues/459
        private class CustomWebClient : WebClient
        {
            public string UserAgent { get; private set; }

            public CustomWebClient(string userAgent)
            {
                UserAgent = userAgent;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address) as HttpWebRequest;
                request.UserAgent = UserAgent;
                return request;
            }
        }

        public WebClientDownloader(string userName, string password, ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // SONARMSBRU-169 Support TLS versions 1.0, 1.1 and 1.2
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            if (password == null)
            {
                password = "";
            }

            client = new CustomWebClient($"ScannerMSBuild/{Utilities.ScannerVersion}");

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
            return header == HttpRequestHeader.UserAgent
                ? client.UserAgent
                : client.Headers[header];
        }

        #region IDownloaderMethods

        public bool TryDownloadIfExists(string url, out string contents)
        {
            logger.LogDebug(Resources.MSG_Downloading, url);
            string data = null;
            var success = DoIgnoringMissingUrls(() => data = client.DownloadString(url));
            contents = data;
            return success;
        }

        public bool TryDownloadFileIfExists(string url, string targetFilePath)
        {
            logger.LogDebug(Resources.MSG_DownloadingFile, url, targetFilePath);
            return DoIgnoringMissingUrls(() => client.DownloadFile(url, targetFilePath));
        }

        public string Download(string url)
        {
            logger.LogDebug(Resources.MSG_Downloading, url);
            return client.DownloadString(url);
        }

        #endregion IDownloaderMethods

        #region Private methods

        private static bool IsAscii(string s)
        {
            return !s.Any(c => c > sbyte.MaxValue);
        }

        /// <summary>
        /// Performs the specified web operation
        /// </summary>
        /// <returns>True if the operation completed successfully, false if the url could not be found.
        /// Other web failures will be thrown as exceptions.</returns>
        private static bool DoIgnoringMissingUrls(Action op)
        {
            try
            {
                op();
                return true;
            }
            catch (WebException e)
            {
                if (e.Response is HttpWebResponse response && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return false;
                }
                throw;
            }
        }

        #endregion Private methods

        #region IDisposable implementation

        private bool disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed && disposing && client != null)
            {
                client.Dispose();
            }

            disposed = true;
        }

        #endregion IDisposable implementation
    }
}
