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
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.WebService
{
    internal class SonarQubeWebService : SonarWebService
    {
        public SonarQubeWebService(IDownloader downloader, Uri serverUri, Version serverVersion, ILogger logger)
            : base(downloader, serverUri, serverVersion, logger)
        {
            // ToDo: Fail fast after release of S4NET 6.0
            if (serverVersion.CompareTo(new Version(7, 9)) < 0)
            {
                logger.LogWarning(Resources.WARN_SonarQubeDeprecated);
            }
        }

        public override async Task<bool> IsServerLicenseValid()
        {
            logger.LogDebug(Resources.MSG_CheckingLicenseValidity);
            var uri = GetUri("api/editions/is_valid_license");
            var response = await downloader.TryGetLicenseInformation(uri);
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                if (json["errors"]?.Any(x => x["msg"]?.Value<string>() == "License not found") == true)
                {
                    return false;
                }

                logger.LogDebug(Resources.MSG_CE_Detected_LicenseValid);
                return true;
            }
            else
            {
                return json["isValidLicense"].ToObject<bool>();
            }
        }

        public override async Task<IDictionary<string, string>> GetProperties(string projectKey, string projectBranch)
        {
            Contract.ThrowIfNullOrWhitespace(projectKey, nameof(projectKey));

            var projectId = GetComponentIdentifier(projectKey, projectBranch);

            return serverVersion.CompareTo(new Version(6, 3)) >= 0
                       ? await DownloadComponentProperties(projectId)
                       : await GetComponentPropertiesLegacy(projectId);
        }

        public override bool IsSonarCloud() => false;

        protected override Uri AddOrganization(Uri uri, string organization) =>
            string.IsNullOrEmpty(organization) || serverVersion.CompareTo(new Version(6, 3)) < 0
                ? uri
                : new Uri(uri + $"&organization={WebUtility.UrlEncode(organization)}");

        private async Task<IDictionary<string, string>> GetComponentPropertiesLegacy(string projectId)
        {
            var uri = GetUri("api/properties?resource={0}", projectId);
            logger.LogDebug(Resources.MSG_FetchingProjectProperties, projectId, uri);
            var result = await ExecuteWithLogs(async () =>
            {
                var contents = await downloader.Download(uri, true);
                var properties = JArray.Parse(contents);
                return properties.ToDictionary(p => p["key"].ToString(), p => p["value"].ToString());
            }, uri);

            return CheckTestProjectPattern(result);
        }
    }
}
