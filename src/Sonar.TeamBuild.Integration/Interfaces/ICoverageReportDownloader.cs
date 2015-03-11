//-----------------------------------------------------------------------
// <copyright file="ICoverageReportDownloader.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;

namespace Sonar.TeamBuild.Integration
{
    internal interface ICoverageReportDownloader
    {
        /// <summary>
        /// Downloads the specified files and returns a dictionary mapping the url to the name of the downloaded file
        /// </summary>
        /// <param name="downloadDir">The directory into which the files should be downloaded</param>
        /// <param name="urls">The files to be downloaded</param>
        /// <returns>A dictionary mapping the url to the name of the downloaded file</returns>
        IDictionary<string, string> DownloadReports(string downloadDir, IEnumerable<string> urls);
    }
}
