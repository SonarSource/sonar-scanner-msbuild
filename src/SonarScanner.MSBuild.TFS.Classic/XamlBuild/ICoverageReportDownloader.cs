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

using System;

namespace SonarScanner.MSBuild.TFS.Classic.XamlBuild;

public interface ICoverageReportDownloader
{
    /// <summary>
    /// Downloads the specified files and returns a dictionary mapping the url to the name of the downloaded file
    /// </summary>
    /// <param name="tfsUri">The project collection URI.</param>
    /// <param name="reportUrl">The file to be downloaded.</param>
    /// <param name="newFullFileName">The name of the new file.</param>
    /// <param name="httpTimeout">The HTTP timeout.</param>
    /// <returns>True if the file was downloaded successfully, otherwise false.</returns>
    public bool DownloadReport(string tfsUri, string reportUrl, string newFullFileName, TimeSpan httpTimeout);
}
