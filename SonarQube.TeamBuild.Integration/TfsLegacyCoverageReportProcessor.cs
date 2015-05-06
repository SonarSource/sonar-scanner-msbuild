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
    public class TfsLegacyCoverageReportProcessor : ICoverageReportProcessor
    {
        public const string DownloadFileName = "VSCodeCoverageReport.coverage"; // was internal

        private const string XmlReportFileExtension = "coveragexml";

        private ICoverageUrlProvider urlProvider;
        private ICoverageReportConverter converter;
        private ICoverageReportDownloader downloader;

        public TfsLegacyCoverageReportProcessor()
            : this(new CoverageReportUrlProvider(), new CoverageReportDownloader(), new CoverageReportConverter())
        {
        }

        public TfsLegacyCoverageReportProcessor(ICoverageUrlProvider urlProvider, ICoverageReportDownloader downloader, ICoverageReportConverter converter) // was internal
        {
            if (urlProvider == null)
            {
                throw new ArgumentNullException("urlProvider");
            }
            if (converter == null)
            {
                throw new ArgumentNullException("converter");
            }
            if (downloader == null)
            {
                throw new ArgumentNullException("downloader");
            }

            this.urlProvider = urlProvider;
            this.converter = converter;
            this.downloader = downloader;
        }

        #region ICoverageReportProcessor methods

        public bool ProcessCoverageReports(AnalysisConfig context, TeamBuildSettings settings, ILogger logger)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            
            if (!this.converter.Initialize(logger))
            {
                // If we can't initialize the converter (e.g. we can't find the exe required to do the
                // conversion) there in there isn't any point in downloading the binary reports
                return false;
            }

            // Fetch all of the report URLs
            logger.LogMessage(Resources.PROC_DIAG_FetchingCoverageReportInfoFromServer);
            IEnumerable<string> urls = this.urlProvider.GetCodeCoverageReportUrls(context.GetTfsUri(), context.GetBuildUri(), logger);
            Debug.Assert(urls != null, "Not expecting the returned list of urls to be null");

            bool result = true; // assume the best

            switch (urls.Count())
            {
                case 0:
                    logger.LogMessage(Resources.PROC_DIAG_NoCodeCoverageReportsFound);
                    break;

                case 1:
                    string url = urls.First();

                    string targetFileName = Path.Combine(context.SonarOutputDir, DownloadFileName);
                    result = this.downloader.DownloadReport(url, targetFileName, logger);

                    if (result)
                    {
                        result = ProcessCodeCoverageReport(url, context, this.converter, logger);
                    }

                    break;

                default: // More than one
                    logger.LogError(Resources.PROC_ERROR_MultipleCodeCoverageReportsFound);
                    result = false;
                    break;
            }

            return result;
        }

        #endregion

        #region Private methods

        //TODO: refactor the legacy and vNext code. The code to locate the code coverage file in each
        // case is different, but the rest of the code is common.
        internal static bool ProcessCodeCoverageReport(string binaryCoverageFilePath, AnalysisConfig context, ICoverageReportConverter converter, ILogger logger)
        {
            bool success = false;
            string xmlFileName = Path.ChangeExtension(binaryCoverageFilePath, XmlReportFileExtension);

            Debug.Assert(!File.Exists(xmlFileName), "Not expecting a file with the name of the binary-to-XML conversion output to already exist: " + xmlFileName);
            success = converter.ConvertToXml(binaryCoverageFilePath, xmlFileName, logger);

            if (success)
            {
                logger.LogMessage(Resources.PROC_DIAG_UpdatingProjectInfoFiles);
                InsertCoverageAnalysisResults(context.SonarOutputDir, xmlFileName);
            }

            return success;
        }

        /// <summary>
        /// Insert code coverage results information into each projectinfo file
        /// </summary>
        private static void InsertCoverageAnalysisResults(string sonarOutputDir, string coverageFilePath)
        {
            foreach (string projectFolderPath in Directory.GetDirectories(sonarOutputDir))
            {
                ProjectInfo projectInfo = null;

                string projectInfoPath = Path.Combine(projectFolderPath, FileConstants.ProjectInfoFileName);

                if (File.Exists(projectInfoPath))
                {
                    projectInfo = ProjectInfo.Load(projectInfoPath);
                    projectInfo.AnalysisResults.Add(new AnalysisResult() { Id = AnalysisType.VisualStudioCodeCoverage.ToString(), Location = coverageFilePath });
                    projectInfo.Save(projectInfoPath);
                }
            }
        }

        #endregion
    }
}
