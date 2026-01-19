/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

using System.Collections.Generic;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.TFS;

public interface ICoverageUrlProvider // was internal
{
    /// <summary>
    /// Builds and returns the download URLs for all code coverage reports for the specified build
    /// </summary>
    /// <param name="tfsUri">The URI of the TFS collection</param>
    /// <parparam name="buildUri">The URI of the build for which data should be retrieved</parparam>
    IEnumerable<string> GetCodeCoverageReportUrls(string tfsUri, string buildUri);
}
