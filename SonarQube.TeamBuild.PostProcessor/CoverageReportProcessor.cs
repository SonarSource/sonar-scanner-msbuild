//-----------------------------------------------------------------------
// <copyright file="CoverageReportProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;

namespace SonarQube.TeamBuild.PostProcessor
{
    public class CoverageReportProcessor : ICoverageReportProcessor
    {
        public bool ProcessCoverageReports(AnalysisConfig config, TeamBuildSettings settings, ILogger logger)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            ICoverageReportProcessor codeCoverageProcessor = TryCreateCoverageReportProcessor(settings);
            if (codeCoverageProcessor != null &&
                !codeCoverageProcessor.ProcessCoverageReports(config, settings, logger))
            {
                return false;
            }

            return true;
        }
        
        /// <summary>
        /// Factory method to create a coverage report processor for the current build environment.
        /// </summary>
        private static ICoverageReportProcessor TryCreateCoverageReportProcessor(TeamBuildSettings settings)
        {
            ICoverageReportProcessor processor = null;

            if (settings.BuildEnvironment == BuildEnvironment.TeamBuild)
            {
                processor = new BuildVNextCoverageReportProcessor();
            }
            else if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                processor = new TfsLegacyCoverageReportProcessor();
            }
            return processor;
        }
    }
}
