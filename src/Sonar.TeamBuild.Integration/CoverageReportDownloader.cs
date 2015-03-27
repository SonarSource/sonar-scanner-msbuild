//-----------------------------------------------------------------------
// <copyright file="CoverageReportDownloader.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.IO;
using System.Net;

namespace SonarQube.TeamBuild.Integration
{
    internal class CoverageReportDownloader : ICoverageReportDownloader
    {
        public bool DownloadReport(string reportUrl, string newFullFileName, ILogger logger)
        {
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
            if (!Directory.Exists(downloadDir))
            {
                Directory.CreateDirectory(downloadDir);
            }

            WebClient myWebClient = new WebClient();
            myWebClient.UseDefaultCredentials = true;

            logger.LogMessage(Resources.DOWN_DIAG_DownloadCoverageReportFromTo, reportUrl, newFullFileName);

            myWebClient.DownloadFile(reportUrl, newFullFileName);

            return true;
        }
        
    }
}
