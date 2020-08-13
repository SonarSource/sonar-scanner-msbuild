/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor
{
    public class WebClientDownloader : IDownloader
    {
        private readonly ILogger logger;
        private readonly HttpClient client;

        public WebClientDownloader(string userName, string password, ILogger logger)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (password == null)
            {
                password = "";
            }

            if (this.client == null)
            {
                this.client = new HttpClient();
                this.client.DefaultRequestHeaders.Add(HttpRequestHeader.UserAgent.ToString(), $"ScannerMSBuild/{Utilities.ScannerVersion}");
            }

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
                this.client.DefaultRequestHeaders.Add(HttpRequestHeader.Authorization.ToString(), "Basic " + credentials);
            }
        }

        public string GetHeader(HttpRequestHeader header)
        {
            if (this.client.DefaultRequestHeaders.Contains(header.ToString()))
            {
                return string.Join(";", this.client.DefaultRequestHeaders.GetValues(header.ToString()));
            }

            return null;
        }

        #region IDownloaderMethods

        public async Task<Tuple<bool, string>> TryDownloadIfExists(string url, bool logPermissionDenied = false)
        {
            this.logger.LogDebug(Resources.MSG_Downloading, url);
            var response = await this.client.GetAsync(url);

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
                        this.logger.LogWarning(Resources.MSG_Forbidden_BrowsePermission);
                    response.EnsureSuccessStatusCode();
                    break;
                default:
                    response.EnsureSuccessStatusCode();
                    break;
            }

            return new Tuple<bool, string>(false, null);
        }

        public async Task<bool> TryDownloadFileIfExists(string url, string targetFilePath, bool logPermissionDenied = false)
        {
            this.logger.LogDebug(Resources.MSG_DownloadingFile, url, targetFilePath);
            var response = await this.client.GetAsync(url);

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
                        this.logger.LogWarning(Resources.MSG_Forbidden_BrowsePermission);
                    response.EnsureSuccessStatusCode();
                    break;
                default:
                    response.EnsureSuccessStatusCode();
                    break;
            }

            return false;
        }

        public async Task<string> Download(string url, bool logPermissionDenied = false)
        {
            this.logger.LogDebug(Resources.MSG_Downloading, url);
            var response = await this.client.GetAsync(url);

            if(response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            if (logPermissionDenied && response.StatusCode == HttpStatusCode.Forbidden)
            {
                this.logger.LogWarning(Resources.MSG_Forbidden_BrowsePermission);
                response.EnsureSuccessStatusCode();
            }

            return null;
        }

        #endregion IDownloaderMethods

        #region Private methods

        private static bool IsAscii(string s)
        {
            return !s.Any(c => c > sbyte.MaxValue);
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
            if (!this.disposed && disposing && this.client != null)
            {
                this.client.Dispose();
            }

            this.disposed = true;
        }

        #endregion IDisposable implementation
    }
}
