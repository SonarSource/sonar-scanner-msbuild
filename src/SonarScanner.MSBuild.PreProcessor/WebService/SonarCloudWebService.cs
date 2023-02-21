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
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.WebService
{
    internal class SonarCloudWebService : SonarWebService
    {
        public SonarCloudWebService(IDownloader downloader, Uri serverUri, Version serverVersion, ILogger logger)
            : base(downloader, serverUri, serverVersion, logger)
        { }

        public override async Task<IDictionary<string, string>> GetProperties(string projectKey, string projectBranch)
        {
            Contract.ThrowIfNullOrWhitespace(projectKey, nameof(projectKey));
            var projectId = GetComponentIdentifier(projectKey, projectBranch);
            return await DownloadComponentProperties(projectId);
        }

        public override Task<bool> IsServerLicenseValid()
        {
            logger.LogDebug(Resources.MSG_SonarCloudDetected_SkipLicenseCheck);
            return Task.FromResult(true);
        }

        public override bool IsSonarCloud() => true;
    }
}
