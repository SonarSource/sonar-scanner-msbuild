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

        // TODO Maybe have a GetPropertiesImpl and wrap this to avoid duplication
        public override async Task<IDictionary<string, string>> GetProperties(string projectKey, string projectBranch)
        {
            Contract.ThrowIfNullOrWhitespace(projectKey, nameof(projectKey));
            var projectId = GetProjectIdentifier(projectKey, projectBranch);
            return await GetComponentProperties(projectId);
        }

        public override void WarnIfSonarQubeVersionIsDeprecated() { }

        public override Task<bool> IsServerLicenseValid()
        {
            logger.LogDebug(Resources.MSG_SonarCloudDetected_SkipLicenseCheck);
            return Task.FromResult(true);
        }

        public override bool IsSonarCloud() => true;
    }
}
