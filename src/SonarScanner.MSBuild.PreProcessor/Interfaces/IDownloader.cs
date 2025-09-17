/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

using System.Net.Http;

namespace SonarScanner.MSBuild.PreProcessor;

public interface IDownloader : IDisposable
{
    Uri BaseUrl { get; }

    Task<Tuple<bool, string>> TryDownloadIfExists(Uri url, bool logPermissionDenied = false);

    Task<bool> TryDownloadFileIfExists(Uri url, string targetFilePath, bool logPermissionDenied = false);

    Task<string> Download(Uri url, bool logPermissionDenied = false, LoggerVerbosity failureVerbosity = LoggerVerbosity.Info);

    Task<Stream> DownloadStream(Uri url, Dictionary<string, string> headers = null);

    Task<HttpResponseMessage> DownloadResource(Uri url);
}
