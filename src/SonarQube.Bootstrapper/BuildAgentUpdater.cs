//-----------------------------------------------------------------------
// <copyright file="BuildAgentUpdater.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;

namespace SonarQube.Bootstrapper
{
    public class BuildAgentUpdater : IBuildAgentUpdater
    {
        private const string SonarQubeIntegrationFilename = "SonarQube.TeamBuild.Integration.zip";
        private const string IntegrationUrlFormat = "{0}/static/csharp/" + SonarQubeIntegrationFilename;

        #region IAgentUpdater interface

        public void Update(string hostUrl, string targetDir, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(hostUrl))
            {
                throw new ArgumentNullException("hostUrl");
            }
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                throw new ArgumentNullException("targetDir");
            }
            
            if (hostUrl.EndsWith("/"))
            {
                hostUrl = hostUrl.Substring(0, hostUrl.Length - 1);
            }

            string integrationUrl = string.Format(IntegrationUrlFormat, hostUrl);
            string downloadedZipFilePath = Path.Combine(targetDir, SonarQubeIntegrationFilename);

            using (WebClient client = new WebClient())
            {
                logger.LogMessage(Resources.INFO_Downloading, SonarQubeIntegrationFilename, integrationUrl, downloadedZipFilePath);
                client.DownloadFile(integrationUrl, downloadedZipFilePath);
                ZipFile.ExtractToDirectory(downloadedZipFilePath, targetDir);
            }

        }

        #endregion
    }
}
