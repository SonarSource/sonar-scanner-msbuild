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
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SonarScanner.MSBuild.PreProcessor.Test.Infrastructure
{
    public sealed class TestDownloader : IDownloader
    {
        public readonly IDictionary<Uri, string> Pages = new Dictionary<Uri, string>();

        private readonly Uri baseUri;

        public TestDownloader(string baseUri)
        {
            this.baseUri = new Uri(baseUri);
        }

        public Task<Tuple<bool, string>> TryDownloadIfExists(Uri url, bool logPermissionDenied = false) =>
            Pages.ContainsKey(url)
                ? Task.FromResult(new Tuple<bool, string>(true, Pages[url]))
                : Task.FromResult(new Tuple<bool, string>(false, null));

        public Task<string> Download(Uri url, bool logPermissionDenied = false) =>
            Pages.ContainsKey(url)
                ? Task.FromResult(Pages[url])
                : throw new ArgumentException("Cannot find URL " + url);

        Task<Stream> IDownloader.DownloadStream(Uri url) => throw new NotImplementedException();

        Task<bool> IDownloader.TryDownloadFileIfExists(Uri url, string targetFilePath, bool logPermissionDenied) => throw new NotImplementedException();

        void IDisposable.Dispose()
        {
            // Nothing to do here
        }

        Task<HttpResponseMessage> IDownloader.TryGetLicenseInformation(Uri url) => throw new NotImplementedException();

        public Uri GetBaseUri() => baseUri;
    }
}
