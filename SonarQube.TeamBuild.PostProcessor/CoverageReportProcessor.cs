//-----------------------------------------------------------------------
// <copyright file="CoverageReportProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.Integration.Interfaces;
using System;
using System.Diagnostics;

namespace SonarQube.TeamBuild.PostProcessor
{
    public class CoverageReportProcessor : ICoverageReportProcessor
    {
        private ICoverageReportProcessor processor;

        private bool initialisedSuccesfully;

        public bool Initialise(AnalysisConfig config, ITeamBuildSettings settings, ILogger logger)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            this.TryCreateCoverageReportProcessor(settings);

            this.initialisedSuccesfully = (this.processor != null && this.processor.Initialise(config, settings, logger));
            return this.initialisedSuccesfully;
        }

        public bool ProcessCoverageReports()
        {
            Debug.Assert(this.initialisedSuccesfully, "Initialization failed, cannot process coverage reports");

            return this.processor.ProcessCoverageReports();
        }

        /// <summary>
        /// Factory method to create a coverage report processor for the current build environment.
        /// </summary>
        private void TryCreateCoverageReportProcessor(ITeamBuildSettings settings)
        {
            if (settings.BuildEnvironment == BuildEnvironment.TeamBuild)
            {
                this.processor = new BuildVNextCoverageReportProcessor();
            }
            else if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                this.processor = new TfsLegacyCoverageReportProcessor();
            }
        }
    }
}