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
using SonarQube.TeamBuild.Integration.Interfaces;

namespace SonarQube.TeamBuild.Integration.Legacy
{
    public class TfsLegacyCoverageReportProcessor : CoverageReportProcessorBase
    {
        public const string DownloadFileName = "VSCodeCoverageReport.coverage"; // was internal

        private readonly ICoverageUrlProvider urlProvider;
        private readonly ICoverageReportDownloader downloader;

        public TfsLegacyCoverageReportProcessor()
            : this(new CoverageReportUrlProvider(), new CoverageReportDownloader(), new CoverageReportConverter())
        {
        }

        public TfsLegacyCoverageReportProcessor(ICoverageUrlProvider urlProvider, ICoverageReportDownloader downloader,
            ICoverageReportConverter converter) // was internal
            : base(converter)
        {
            this.urlProvider = urlProvider ?? throw new ArgumentNullException("urlProvider");
            this.downloader = downloader ?? throw new ArgumentNullException("downloader");
        }

        #region Virtual methods

        protected override bool TryGetBinaryReportFile(AnalysisConfig config, ITeamBuildSettings settings, ILogger logger,
            out string binaryFilePath)
        {
            var urls = urlProvider.GetCodeCoverageReportUrls(config.GetTfsUri(), config.GetBuildUri(), logger);
            Debug.Assert(urls != null, "Not expecting the returned list of urls to be null");

            var continueProcessing = true;
            binaryFilePath = null;

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
                        binaryFilePath = targetFileName;
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

        #endregion Virtual methods
    }
}
