//-----------------------------------------------------------------------
// <copyright file="WebClientDownloader.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace SonarQube.TeamBuild.PreProcessor
{
    public class WebClientDownloader : IDownloader
    {
        private readonly WebClient Client;

        public WebClientDownloader(WebClient client, string username, string password)
        {
            Client = client;
            if (username != null && password != null)
            {
                if (username.Contains(':'))
                {
                    throw new ArgumentException("username cannot contain the ':' character due to basic authentication limitations");
                }
                if (!IsAscii(username) || !IsAscii(password))
                {
                    throw new ArgumentException("username and password should contain only ASCII characters due to basic authentication limitations");
                }

                var credentials = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:{1}", username, password);
                credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
                Client.Headers[HttpRequestHeader.Authorization] = "Basic " + credentials;
            }
        }

        private static bool IsAscii(string s)
        {
            return !s.Any(c => c > sbyte.MaxValue);
        }

        public bool TryDownloadIfExists(string url, out string contents)
        {
            try
            {
                contents = Client.DownloadString(url);
                return true;
            }
            catch (WebException e)
            {
                var response = e.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    contents = null;
                    return false;
                }

                throw;
            }
        }

        public string Download(string url)
        {
            return Client.DownloadString(url);
        }

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
                if (this.Client != null)
                {
                    this.Client.Dispose();
                }
            }

            this.disposed = true;
        }

        #endregion
    }
}
