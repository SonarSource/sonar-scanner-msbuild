/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using Microsoft.TeamFoundation.Client;
using Microsoft.VisualStudio.Services.Common;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.TFS.Classic.XamlBuild;

[ExcludeFromCodeCoverage] // non-mockable
internal class CoverageReportDownloader : ICoverageReportDownloader
{
    private readonly ILogger logger;

    public CoverageReportDownloader(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool DownloadReport(string tfsUri, string reportUrl, string newFullFileName, TimeSpan httpTimeout)
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

        var downloadDir = Path.GetDirectoryName(newFullFileName);
        Utilities.EnsureDirectoryExists(downloadDir, logger);

        InternalDownloadReport(tfsUri, reportUrl, newFullFileName, httpTimeout);

        return true;
    }

    private void InternalDownloadReport(string tfsUri, string reportUrl, string reportDestinationPath, TimeSpan httpTimeout)
    {
        var vssHttpMessageHandler = GetHttpHandler(tfsUri);

        logger.LogInfo(Resources.DOWN_DIAG_DownloadCoverageReportFromTo, reportUrl, reportDestinationPath);

        using var httpClient = new HttpClient(vssHttpMessageHandler);
        httpClient.Timeout = httpTimeout;
        using var response = httpClient.GetAsync(reportUrl).Result;
        if (response.IsSuccessStatusCode)
        {
            using var fileStream = new FileStream(reportDestinationPath, FileMode.Create, FileAccess.Write);
            response.Content.CopyToAsync(fileStream).Wait();
        }
        else
        {
            this.logger.LogError(Resources.PROC_ERROR_FailedToDownloadReportReason, reportUrl, response.StatusCode, response.ReasonPhrase);
        }
    }

    private VssHttpMessageHandler GetHttpHandler(string tfsUri)
    {
        VssCredentials vssCreds;
        var tfsCollectionUri = new Uri(tfsUri);

        using (var collection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(tfsCollectionUri))
        {
            // Build agents run non-attended and most often non-interactive so make sure not to create a credential prompt
            collection.ClientCredentials.AllowInteractive = false;
            collection.EnsureAuthenticated();

            this.logger.LogInfo(Resources.DOWN_DIAG_ConnectedToTFS, tfsUri);

            // We need VSS credentials that encapsulate all types of credentials (NetworkCredentials for TFS, OAuth for VSO)
            var connection = collection as TfsConnection;
            vssCreds = TfsClientCredentialsConverter.ConvertToVssCredentials(connection.ClientCredentials, tfsCollectionUri);
        }

        Debug.Assert(vssCreds != null, "Not expecting ConvertToVssCredentials ");
        var vssHttpMessageHandler = new VssHttpMessageHandler(vssCreds, new VssHttpRequestSettings());

        return vssHttpMessageHandler;
    }
}
