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
using System.Net;

namespace Sonar.TeamBuild.Integration
{
    public class CoverageReportProcessor
    {
        private const string XmlReportFileExtension = "coveragexml";

        private ICoverageUrlProvider urlProvider;
        private ICoverageReportConverter converter;

        public static CoverageReportProcessor CreateHandler(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            ICoverageReportConverter converter = new CoverageReportConverter();
            if (!converter.Initialize(logger))
            {
                // If we can't initialize the converter (e.g. we can't find the exe required to do the
                // conversion) there in there isn't any point in downloading the binary reports
                return null;
            }

            return new CoverageReportProcessor(new CoverageReportUrlProvider(), converter);
        }

        private CoverageReportProcessor(ICoverageUrlProvider urlProvider, ICoverageReportConverter converter)
        {
            if (urlProvider == null)
            {
                throw new ArgumentNullException("urlProvider");
            }
            if (converter == null)
            {
                throw new ArgumentNullException("converter");
            }

            this.urlProvider = urlProvider;
            this.converter = converter;
        }

        #region Public methods

        public IEnumerable<string> DownloadCoverageReports(string tfsUri, string buildUri, string downloadToDir, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(tfsUri))
            {
                throw new ArgumentNullException("tfsUri");
            }
            if (string.IsNullOrWhiteSpace(buildUri))
            {
                throw new ArgumentNullException("buildUri");
            }
            if (string.IsNullOrWhiteSpace(downloadToDir))
            {
                throw new ArgumentNullException("downloadToDir");
            }
            
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            
            // Fetch all of the report URLs
            logger.LogMessage("Fetching code coverage report URLs...");
            IEnumerable<string> urls = this.urlProvider.GetCodeCoverageReportUrls(tfsUri, buildUri, logger);

            IList<CoverageReportInfo> reportInfo = null;

            if (urls.Any())
            {
                logger.LogMessage("Downloading coverage reports...");
                reportInfo = this.DownloadReports(downloadToDir, urls, logger);

                logger.LogMessage("Converting coverage reports to XML...");
                ConvertReportsToXml(reportInfo, logger);
                logger.LogMessage("...done.");
            }
            else
            {
                logger.LogMessage("No code coverage reports were found for this build. BuildUri: {0}", buildUri);
            }
            logger.LogMessage("Finished processing code coverage reports.");

            if (reportInfo == null)
            {
                return null;
            }
            else
            {
                return reportInfo.Select(r => r.FullXmlFilePath).Where(s => !string.IsNullOrWhiteSpace(s));
            }
        }

        #endregion

        #region Private methods

        private IList<CoverageReportInfo> DownloadReports(string downloadDir, IEnumerable<string> urls, ILogger logger)
        {
            if (!Directory.Exists(downloadDir))
            {
                Directory.CreateDirectory(downloadDir);
            }

            WebClient myWebClient = new WebClient();
            myWebClient.UseDefaultCredentials = true;

            string fileRoot = downloadDir + @"\build_{0}_{1}.coverage";
            DateTime downloadTime = DateTime.Now;
            int counter = 1;

            IList<CoverageReportInfo> reports = new List<CoverageReportInfo>();

            foreach (string url in urls)
            {
                string localFilePath = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    fileRoot, downloadTime.ToString("hh.mm.ss"), counter);

                logger.LogMessage("Download url: {0}", url);
                logger.LogMessage("Downloading file to : {0}", localFilePath);

                myWebClient.DownloadFile(url, localFilePath);
                counter++;

                CoverageReportInfo report = new CoverageReportInfo()
                {
                    ReportUrl = url, FullBinaryFilePath = localFilePath
                };
                reports.Add(report);
            }

            return reports;
        }
        
        private void ConvertReportsToXml(IList<CoverageReportInfo> reports, ILogger logger)
        {
            foreach(CoverageReportInfo report in reports)
            {
                Debug.Assert(File.Exists(report.FullBinaryFilePath), "Binary report file does not exist: " + report.FullBinaryFilePath);
            
                string xmlFileName = Path.ChangeExtension(report.FullBinaryFilePath, XmlReportFileExtension);

                this.converter.ConvertToXml(report.FullBinaryFilePath, xmlFileName, logger);

                report.FullXmlFilePath = xmlFileName;
            }
        }

        #endregion
    }
}
