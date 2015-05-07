//-----------------------------------------------------------------------
// <copyright file="TfsLegacyCoverageReportProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SonarQube.TeamBuild.Integration
{
    public class TfsLegacyCoverageReportProcessor : CoverageReportProcessorBase
    {
        public const string DownloadFileName = "VSCodeCoverageReport.coverage"; // was internal

        private ICoverageUrlProvider urlProvider;
        private ICoverageReportDownloader downloader;

        public TfsLegacyCoverageReportProcessor()
            : this(new CoverageReportUrlProvider(), new CoverageReportDownloader(), new CoverageReportConverter())
        {
        }

        public TfsLegacyCoverageReportProcessor(ICoverageUrlProvider urlProvider, ICoverageReportDownloader downloader, ICoverageReportConverter converter) // was internal
            : base(converter)
        {
            if (urlProvider == null)
            {
                throw new ArgumentNullException("urlProvider");
            }
            if (downloader == null)
            {
                throw new ArgumentNullException("downloader");
            }

            this.urlProvider = urlProvider;
            this.downloader = downloader;
        }

        #region Virtual methods

        protected override bool TryGetBinaryReportFile(AnalysisConfig config, TeamBuildSettings settings, ILogger logger, out string binaryFilePath)
        {
            IEnumerable<string> urls = this.urlProvider.GetCodeCoverageReportUrls(config.GetTfsUri(), config.GetBuildUri(), logger);
            Debug.Assert(urls != null, "Not expecting the returned list of urls to be null");

            bool continueProcessing = true;
            binaryFilePath = null;

            switch (urls.Count())
            {
                case 0:
                    logger.LogMessage(Resources.PROC_DIAG_NoCodeCoverageReportsFound);
                    break;

                case 1:
                    string url = urls.First();

                    string targetFileName = Path.Combine(config.SonarOutputDir, DownloadFileName);
                    bool result = this.downloader.DownloadReport(url, targetFileName, logger);

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

        #endregion
    }
}
