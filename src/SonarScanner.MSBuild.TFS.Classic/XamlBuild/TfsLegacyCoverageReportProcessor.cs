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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;

namespace SonarScanner.MSBuild.TFS.Classic.XamlBuild;

public class TfsLegacyCoverageReportProcessor : CoverageReportProcessorBase
{
    public const string DownloadFileName = "VSCodeCoverageReport.coverage"; // was internal

    private readonly ICoverageUrlProvider urlProvider;
    private readonly ICoverageReportDownloader downloader;

    public TfsLegacyCoverageReportProcessor(ILogger logger)
        : this(new CoverageReportUrlProvider(logger), new CoverageReportDownloader(logger), new BinaryToXmlCoverageReportConverter(logger), logger)
    {
    }

    public TfsLegacyCoverageReportProcessor(ICoverageUrlProvider urlProvider, ICoverageReportDownloader downloader,
        ICoverageReportConverter converter, ILogger logger) // was internal
        : base(converter, logger)
    {
        this.urlProvider = urlProvider ?? throw new ArgumentNullException(nameof(urlProvider));
        this.downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
    }

    protected override bool TryGetVsCoverageFiles(AnalysisConfig config, IBuildSettings settings, out IEnumerable<string> binaryFilePaths)
    {
        var urls = urlProvider.GetCodeCoverageReportUrls(config.GetTfsUri(), config.GetBuildUri());
        Debug.Assert(urls != null, "Not expecting the returned list of urls to be null");

        if (!urls.Any())
        {
            Logger.LogInfo(Resources.PROC_DIAG_NoCodeCoverageReportsFound);
            binaryFilePaths = Enumerable.Empty<string>();
            return true;
        }

        var downloadedReports = new List<string>();
        foreach (var url in urls)
        {
            var targetFileName = Path.Combine(config.SonarOutputDir, DownloadFileName);
            var localSettings = config.GetAnalysisSettings(false, Logger);
            var httpTimeout = TimeoutProvider.HttpTimeout(localSettings, Logger);
            var result = downloader.DownloadReport(config.GetTfsUri(), url, targetFileName, httpTimeout);
            if (result)
            {
                downloadedReports.Add(targetFileName);
            }
            else
            {
                Logger.LogError(Resources.PROC_ERROR_FailedToDownloadReport);
                binaryFilePaths = Enumerable.Empty<string>();
                return false;
            }
        }

        binaryFilePaths = downloadedReports;
        return true;
    }

    protected override bool TryGetTrxFiles(IBuildSettings settings, out IEnumerable<string> trxFilePaths)
    {
        trxFilePaths = Enumerable.Empty<string>();
        return false;
    }
}
