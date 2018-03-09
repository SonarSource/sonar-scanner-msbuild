/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Microsoft.TeamFoundation.Client;
using Microsoft.VisualStudio.Services.Common;
using SonarQube.Common;

namespace SonarQube.TeamBuild.Integration.Classic.XamlBuild
{
    internal class CoverageReportDownloader : ICoverageReportDownloader
    {
        public bool DownloadReport(string tfsUri, string reportUrl, string newFullFileName, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(tfsUri))
            {
                throw new ArgumentNullException(nameof(tfsUri));
            }
            if (string.IsNullOrWhiteSpace(reportUrl))
            {
                throw new ArgumentNullException(nameof(reportUrl));
            }
            if (string.IsNullOrWhiteSpace(newFullFileName))
            {
                throw new ArgumentNullException(nameof(newFullFileName));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            var downloadDir = Path.GetDirectoryName(newFullFileName);
            Utilities.EnsureDirectoryExists(downloadDir, logger);

            InternalDownloadReport(tfsUri, reportUrl, newFullFileName, logger);

            return true;
        }

        private void InternalDownloadReport(string tfsUri, string reportUrl, string reportDestinationPath, ILogger logger)
        {
            var vssHttpMessageHandler = GetHttpHandler(tfsUri, logger);

            logger.LogInfo(Resources.DOWN_DIAG_DownloadCoverageReportFromTo, reportUrl, reportDestinationPath);

            using (var httpClient = new HttpClient(vssHttpMessageHandler))
            using (var response = httpClient.GetAsync(reportUrl).Result)
            {
                if (response.IsSuccessStatusCode)
                {
                    using (var fileStream = new FileStream(reportDestinationPath, FileMode.Create, FileAccess.Write))
                    {
                        response.Content.CopyToAsync(fileStream).Wait();
                    }
                }
                else
                {
                    logger.LogError(Resources.PROC_ERROR_FailedToDownloadReportReason, reportUrl, response.StatusCode, response.ReasonPhrase);
                }
            }
        }

        private VssHttpMessageHandler GetHttpHandler(string tfsUri, ILogger logger)
        {
            VssCredentials vssCreds;
            var tfsCollectionUri = new Uri(tfsUri);

            using (var collection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(tfsCollectionUri))
            {
                // Build agents run non-attended and most often non-interactive so make sure not to create a credential prompt
                collection.ClientCredentials.AllowInteractive = false;
                collection.EnsureAuthenticated();

                logger.LogInfo(Resources.DOWN_DIAG_ConnectedToTFS, tfsUri);

                // We need VSS credentials that encapsulate all types of credentials (NetworkCredentials for TFS, OAuth for VSO)
                var connection = collection as TfsConnection;
                vssCreds = TfsClientCredentialsConverter.ConvertToVssCredentials(connection.ClientCredentials, tfsCollectionUri);
            }

            Debug.Assert(vssCreds != null, "Not expecting ConvertToVssCredentials ");
            var vssHttpMessageHandler = new VssHttpMessageHandler(vssCreds, new VssHttpRequestSettings());

            return vssHttpMessageHandler;
        }
    }
}
