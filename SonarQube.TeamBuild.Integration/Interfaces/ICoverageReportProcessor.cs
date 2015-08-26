//-----------------------------------------------------------------------
// <copyright file="ICoverageReportProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;

namespace SonarQube.TeamBuild.Integration
{
    public interface ICoverageReportProcessor
    {
        /// <summary>
        /// Initialises the converter
        /// </summary>
        /// <returns>Operation success</returns>
        bool Initialise(AnalysisConfig config, TeamBuildSettings settings, ILogger logger);

        /// <summary>
        /// Locate, download and convert the code coverage report
        /// </summary>
        /// <returns>Operation success</returns>
        bool ProcessCoverageReports();
    }
}
