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
using System.Net.Http;
using System.Threading.Tasks;

namespace SonarScanner.MSBuild.PreProcessor
{
    /// <summary>
    /// Interface introduced for testability.
    /// </summary>
    public interface IDownloader : IDisposable
    {
        /// <summary>
        /// Attempts to download the specified page
        /// </summary>
        /// <returns>False if the url does not exist, true if the contents were downloaded successfully.
        /// Exceptions are thrown for other web failures.</returns>
        Task<Tuple<bool, string>> TryDownloadIfExists(Uri url, bool logPermissionDenied = false);

        /// <summary>
        /// Attempts to download the specified file
        /// </summary>
        /// <param name="targetFilePath">The file to which the downloaded data should be saved</param>
        /// <returns>False if the url does not exist, true if the data was downloaded successfully.
        /// Exceptions are thrown for other web failures.</returns>
        Task<bool> TryDownloadFileIfExists(Uri url, string targetFilePath, bool logPermissionDenied = false);

        Task<string> Download(Uri url, bool logPermissionDenied = false);

        Task<HttpResponseMessage> TryGetLicenseInformation(Uri url);
    }
}
