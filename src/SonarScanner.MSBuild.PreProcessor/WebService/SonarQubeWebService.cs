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
        { }

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
        public override void WarnIfSonarQubeVersionIsDeprecated()
        {
            if (serverVersion.CompareTo(new Version(7, 9)) < 0)
            {
                logger.LogWarning(Resources.WARN_SonarQubeDeprecated);
            }
        }

        public override async Task<IDictionary<string, string>> GetProperties(string projectKey, string projectBranch)
        {
            Contract.ThrowIfNullOrWhitespace(projectKey, nameof(projectKey));

            var projectId = GetProjectIdentifier(projectKey, projectBranch);

            return serverVersion.CompareTo(new Version(6, 3)) >= 0
                       ? await GetComponentProperties(projectId)
                       : await GetComponentPropertiesLegacy(projectId);
        }

        public override bool IsSonarCloud() => false;

        protected override Uri AddOrganization(Uri uri, string organization)
        {
            if (string.IsNullOrEmpty(organization))
            {
                return uri;
            }
            return serverVersion.CompareTo(new Version(6, 3)) >= 0
                       ? new Uri(uri + $"&organization={WebUtility.UrlEncode(organization)}")
                       : uri;
        }

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
