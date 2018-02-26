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
using System.Linq;
using SonarQube.Common;

namespace SonarQube.TeamBuild.Integration.XamlBuild
{
    public class TfsLegacyCoverageReportLocator : ICoverageReportLocator
    {
        public const string DownloadFileName = "VSCodeCoverageReport.coverage"; // was internal

        private readonly ICoverageUrlProvider urlProvider;
        private readonly ICoverageReportDownloader downloader;
        private readonly ILogger logger;

        public TfsLegacyCoverageReportLocator(ICoverageUrlProvider urlProvider, ICoverageReportDownloader downloader, ILogger logger)
        {
            this.urlProvider = urlProvider ?? throw new ArgumentNullException(nameof(urlProvider));
            this.downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool TryGetBinaryCoveragePath(AnalysisConfig config, ITeamBuildSettings settings, out string binaryCoveragePath)
        {
            var urls = urlProvider.GetCodeCoverageReportUrls(config.GetTfsUri(), config.GetBuildUri(), logger);
            Debug.Assert(urls != null, "Not expecting the returned list of urls to be null");

            var continueProcessing = true;
            binaryCoveragePath = null;

            switch (urls.Count())
            {
                case 0:
                    logger.LogInfo(Resources.PROC_DIAG_NoCodeCoverageReportsFound);
                    break;

                case 1:
                    var url = urls.First();

                    var targetFileName = Path.Combine(config.SonarOutputDir, DownloadFileName);
                    var result = downloader.DownloadReport(config.GetTfsUri(), url, targetFileName, logger);

                    if (result)
                    {
                        binaryCoveragePath = targetFileName;
                    }
                    else
                    {
                        continueProcessing = false;
                        logger.LogError(Resources.PROC_ERROR_FailedToDownloadReport);
                    }
                    break;

                default: // More than one
                    continueProcessing = false;
                    logger.LogError(Resources.PROC_ERROR_MultipleCodeCoverageReportsFound);
                    break;
            }

            return continueProcessing;
        }

        public bool TryGetTestResultsPath(AnalysisConfig config, ITeamBuildSettings settings, out string testResultsPath)
        {
            testResultsPath = null;
            return false;
        }
    }
}
