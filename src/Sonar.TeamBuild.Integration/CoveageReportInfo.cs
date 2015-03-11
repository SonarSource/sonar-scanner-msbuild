//-----------------------------------------------------------------------
// <copyright file="CoverageReportInfo.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Sonar.TeamBuild.Integration
{
    internal class CoverageReportInfo
    {
        public string ReportUrl { get; set; }


        public string FullBinaryFilePath { get; set; }


        public string FullXmlFilePath { get; set; }
    }
}
