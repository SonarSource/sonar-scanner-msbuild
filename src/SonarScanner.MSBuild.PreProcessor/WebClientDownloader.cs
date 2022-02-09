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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor
{
    public class WebClientDownloader : IDownloader
    {
        private readonly ILogger logger;
        private readonly HttpClient client;

        public WebClientDownloader(string userName, string password, ILogger logger, string clientCertPath = null, string clientCertPassword = null)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            password = password ?? string.Empty;

            if (clientCertPath != null && clientCertPassword != null) // password mandatory, as to use client cert in .jar it cannot be with empty password
            {
                var clientHandler = new HttpClientHandler { ClientCertificateOptions = ClientCertificateOption.Manual };
                clientHandler.ClientCertificates.Add(new X509Certificate2(clientCertPath, clientCertPassword));
                client = new HttpClient(clientHandler);
            }
            else
            {
                client = new HttpClient();
            }

            client.DefaultRequestHeaders.Add(HttpRequestHeader.UserAgent.ToString(), $"ScannerMSBuild/{Utilities.ScannerVersion}");

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
                client.DefaultRequestHeaders.Add(HttpRequestHeader.Authorization.ToString(), "Basic " + credentials);
            }
        }

        public string GetHeader(HttpRequestHeader header) =>
            client.DefaultRequestHeaders.Contains(header.ToString())
                ? string.Join(";", client.DefaultRequestHeaders.GetValues(header.ToString()))
                : null;

        #region IDownloaderMethods
        public async Task<HttpResponseMessage> TryGetLicenseInformation(Uri url)
        {
            logger.LogDebug(Resources.MSG_Downloading, url);
            var response = await client.GetAsync(url).ConfigureAwait(false);

            return response.StatusCode == HttpStatusCode.Unauthorized
                ? throw new ArgumentException(Resources.ERR_TokenWithoutSufficientRights)
                : response;
        }

        public async Task<Tuple<bool, string>> TryDownloadIfExists(Uri url, bool logPermissionDenied = false)
        {
            logger.LogDebug(Resources.MSG_Downloading, url);
            var response = await client.GetAsync(url).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return new Tuple<bool, string>(true, await response.Content.ReadAsStringAsync());
            }

            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    return new Tuple<bool, string>(false, null);
                case HttpStatusCode.Forbidden:
                    if (logPermissionDenied)
                    {
                        logger.LogWarning(Resources.MSG_Forbidden_BrowsePermission);
                    }
                    response.EnsureSuccessStatusCode();
                    break;
                default:
                    response.EnsureSuccessStatusCode();
                    break;
            }

            return new Tuple<bool, string>(false, null);
        }

        public async Task<bool> TryDownloadFileIfExists(Uri url, string targetFilePath, bool logPermissionDenied = false)
        {
            logger.LogDebug(Resources.MSG_DownloadingFile, url, targetFilePath);
            var response = await client.GetAsync(url).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write))
                {
                    await contentStream.CopyToAsync(fileStream);
                    return true;
                }
            }

            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    return false;
                case HttpStatusCode.Forbidden:
                    if (logPermissionDenied)
                    {
                        logger.LogWarning(Resources.MSG_Forbidden_BrowsePermission);
                    }
                    response.EnsureSuccessStatusCode();
                    break;
                default:
                    response.EnsureSuccessStatusCode();
                    break;
            }

            return false;
        }

        public async Task<string> Download(Uri url, bool logPermissionDenied = false)
        {
            logger.LogDebug(Resources.MSG_Downloading, url);
            var response = await client.GetAsync(url).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            if (logPermissionDenied && response.StatusCode == HttpStatusCode.Forbidden)
            {
                logger.LogWarning(Resources.MSG_Forbidden_BrowsePermission);
                response.EnsureSuccessStatusCode();
            }

            return null;
        }

        #endregion IDownloaderMethods

        #region Private methods

        private static bool IsAscii(string s) =>
            !s.Any(c => c > sbyte.MaxValue);

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
