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
        bool ProcessCoverageReports(AnalysisConfig context, TeamBuildSettings settings, ILogger logger);
    }
}
