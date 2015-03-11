//-----------------------------------------------------------------------
// <copyright file="CoverageReportProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
// TODO replace success codes with exceptions
namespace Sonar.TeamBuild.Integration
{
    public class CoverageReportProcessor
    {
        private const string XmlReportFileExtension = "coveragexml";

        private ICoverageUrlProvider urlProvider;
        private ICoverageReportConverter converter;
        private ICoverageReportDownloader downloader;

        public CoverageReportProcessor()
            : this(new CoverageReportUrlProvider(), new CoverageReportDownloader(), new CoverageReportConverter())
        {
        }

        internal CoverageReportProcessor(ICoverageUrlProvider urlProvider, ICoverageReportDownloader downloader, ICoverageReportConverter converter)
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

        #region Public methods

        public bool ProcessCoverageReports(AnalysisContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ILogger logger = context.Logger;
            Debug.Assert(logger != null, "Not expecting the logger to be null");

            if (!this.converter.Initialize(context.Logger))
            {
                // If we can't initialize the converter (e.g. we can't find the exe required to do the
                // conversion) there in there isn't any point in downloading the binary reports
                return false;
            }

            // Fetch all of the report URLs
            logger.LogMessage("Fetching code coverage report URLs...");
            IEnumerable<string> urls = this.urlProvider.GetCodeCoverageReportUrls(context.TfsUri, context.BuildUri, logger);
            Debug.Assert(urls != null, "Not expecting the returned list of urls to be null");

            bool result = true; // assume the best

            switch (urls.Count())
            {
                case 0:
                    logger.LogMessage("No code coverage reports were found for the current build.");
                    break;

                case 1:
                    string url = urls.First();
                    result = ProcessCodeCoverageReport(url, context);
                    break;

                default: // More than one
                    logger.LogError("More than one code coverage result file was created. Only one report can be uploaded to Sonar. Please modify the build definition so either Sonar analysis is disabled or the only platform/flavor is built");
                    result = false;
                    break;
            }

            return result;
        }

        #endregion

        #region Private methods

        private bool ProcessCodeCoverageReport(string reportUrl, AnalysisContext context)
        {
            string targetFileName = Path.Combine(context.SonarOutputDir, "CoverageReport.coverage");
            bool success = this.downloader.DownloadReport(context.SonarOutputDir, targetFileName, context.Logger);
         
            if (success)
            {
                string xmlFileName = Path.ChangeExtension(targetFileName, "coveragexml");
                Debug.Assert(!File.Exists(xmlFileName), "Not expecting a file with the name of the binary-to-XML conversion output to already exist: " + xmlFileName);
                this.converter.ConvertToXml(targetFileName, xmlFileName, context.Logger);

                context.Logger.LogMessage("Updating project info files with code coverage information...");
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
