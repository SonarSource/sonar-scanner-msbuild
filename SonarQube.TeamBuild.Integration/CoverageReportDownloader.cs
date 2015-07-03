//-----------------------------------------------------------------------
// <copyright file="CoverageReportDownloader.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.TeamFoundation.Client;
using Microsoft.VisualStudio.Services.Common;
using SonarQube.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace SonarQube.TeamBuild.Integration
{
    internal class CoverageReportDownloader : ICoverageReportDownloader
    {
        public bool DownloadReport(string tfsUri, string reportUrl, string newFullFileName, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(tfsUri))
            {
                throw new ArgumentNullException("tfsUri");
            }
            if (string.IsNullOrWhiteSpace(reportUrl))
            {
                throw new ArgumentNullException("reportUrl");
            }
            if (string.IsNullOrWhiteSpace(newFullFileName))
            {
                throw new ArgumentNullException("newFullFileName");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            string downloadDir = Path.GetDirectoryName(newFullFileName);
            Utilities.EnsureDirectoryExists(downloadDir, logger);

            InternalDownloadReport(tfsUri, reportUrl, newFullFileName, logger);

            return true;
        }

        private void InternalDownloadReport(string tfsUri, string reportUrl, string reportDestinationPath, ILogger logger)
        {
            VssHttpMessageHandler vssHttpMessageHandler = GetHttpHandler(tfsUri, logger);
            HttpClient httpClient = new HttpClient(vssHttpMessageHandler);

            logger.LogMessage(Resources.DOWN_DIAG_DownloadCoverageReportFromTo, reportUrl, reportDestinationPath);
            using (HttpResponseMessage response = httpClient.GetAsync(reportUrl).Result)
            {
                if (response.IsSuccessStatusCode)
                {
                    using (FileStream fileStream = new FileStream(reportDestinationPath, FileMode.Create, FileAccess.Write))
                    {
                        response.Content.CopyToAsync(fileStream).Wait();
                    }
                }
            }
        }

        private VssHttpMessageHandler GetHttpHandler(string tfsUri, ILogger logger)
        {
            VssCredentials vssCreds = null;
            Uri tfsCollectionUri = new Uri(tfsUri);

            using (TfsTeamProjectCollection collection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(tfsCollectionUri))
            {
                // Build agents run non-attended and most often non-interactive so make sure not to create a credential prompt
                collection.ClientCredentials.AllowInteractive = false;
                collection.EnsureAuthenticated();

                logger.LogMessage(Resources.DOWN_DIAG_ConnectedToTFS, tfsUri);

                // We need VSS credentials that encapsulate all types of credentials (NetworkCredentials for TFS, OAuth for VSO)
                TfsConnection connection = collection as TfsConnection;
                vssCreds = connection.ClientCredentials.ConvertToVssCredentials(tfsCollectionUri);
            }

            Debug.Assert(vssCreds != null, "Not expecting ConvertToVssCredentials ");
            VssHttpMessageHandler vssHttpMessageHandler = new VssHttpMessageHandler(vssCreds, new VssHttpRequestSettings());

            return vssHttpMessageHandler;
        }
    }
}