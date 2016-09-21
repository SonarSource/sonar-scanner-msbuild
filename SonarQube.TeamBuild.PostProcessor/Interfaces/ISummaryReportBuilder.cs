//-----------------------------------------------------------------------
// <copyright file="ISummaryReportBuilder.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration.Interfaces;
using SonarScanner.Shim;

namespace SonarQube.TeamBuild.PostProcessor
{
    /// <summary>
    /// Encapsulates summary report building functionality
    /// </summary>
    /// <remarks>Interface added for testability</remarks>
    public interface ISummaryReportBuilder
    {
        void GenerateReports(ITeamBuildSettings settings, AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger);
    }
}
