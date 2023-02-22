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
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SonarScanner.MSBuild.PreProcessor.Test.Infrastructure
{
    public sealed class TestDownloader : IDownloader
    {
        public readonly IDictionary<Uri, string> Pages = new Dictionary<Uri, string>();
        public List<Uri> AccessedUrls = new();

        private HttpStatusCode expectedHttpStatusCode;
        private bool isCEEdition;

        public Task<Tuple<bool, string>> TryDownloadIfExists(Uri url, bool logPermissionDenied = false)
        {
            AccessedUrls.Add(url);
            return Pages.ContainsKey(url)
                ? Task.FromResult(new Tuple<bool, string>(true, Pages[url]))
                : Task.FromResult(new Tuple<bool, string>(false, null));
        }

        public Task<string> Download(Uri url, bool logPermissionDenied = false)
        {
            AccessedUrls.Add(url);
            return Pages.ContainsKey(url)
                ? Task.FromResult(Pages[url])
                : throw new ArgumentException("Cannot find URL " + url);
        }

        public Task<Stream> DownloadStream(Uri url) =>
            throw new NotImplementedException();

        public Task<bool> TryDownloadFileIfExists(Uri url, string targetFilePath, bool logPermissionDenied = false)
        {
            AccessedUrls.Add(url);

            if (Pages.ContainsKey(url))
            {
                File.WriteAllText(targetFilePath, Pages[url]);
                return Task.FromResult(true);
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public void Dispose()
        {
            // Nothing to do here
        }

        public void ConfigureGetLicenseInformationMock(HttpStatusCode expectedStatusCode, string expectedReturnMessage, bool isCEEdition)
        {
            expectedHttpStatusCode = expectedStatusCode;
            this.isCEEdition = isCEEdition;
        }

        public Task<HttpResponseMessage> TryGetLicenseInformation(Uri url)
        {
            AccessedUrls.Add(url);
            if (Pages.ContainsKey(url))
            {
                // returns 200
                return Task.FromResult(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(Pages[url])
                });
            }
            else
            {
                // returns either 404 or 401
                if (expectedHttpStatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new ArgumentException("The token you provided doesn't have sufficient rights to check license.");
                }

                if (expectedHttpStatusCode == HttpStatusCode.NotFound)
                {
                    if (isCEEdition)
                    {
                        return Task.FromResult(new HttpResponseMessage()
                        {
                            StatusCode = HttpStatusCode.NotFound,
                            Content = new StringContent(@"{""errors"":[{""msg"":""Unknown url: /api/editions/is_valid_license""}]} ")
                        });
                    }
                    else
                    {
                        return Task.FromResult(new HttpResponseMessage()
                        {
                            StatusCode = HttpStatusCode.NotFound,
                            Content = new StringContent(@"{ ""errors"" : [ { ""msg"": ""License not found"" } ] } ")
                        });
                    }
                }

                return Task.FromResult<HttpResponseMessage>(null);
            }
        }
    }
}
