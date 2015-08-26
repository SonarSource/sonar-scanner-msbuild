//-----------------------------------------------------------------------
// <copyright file="CoverageReportProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System;
using System.Diagnostics;

namespace SonarQube.TeamBuild.PostProcessor
{
    public class CoverageReportProcessor : ICoverageReportProcessor
    {
        private AnalysisConfig config;
        private TeamBuildSettings settings;
        private ILogger logger;
        private ICoverageReportProcessor processor;

        private bool initialised;
        private bool initialisedSuccesfully;

        public bool Initialise(AnalysisConfig config, TeamBuildSettings settings, ILogger logger)
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

            Debug.Assert(!this.initialised, "Please call Initialize only once");
            this.initialised = true;

            this.config = config;
            this.settings = settings;
            this.logger = logger;

            this.TryCreateCoverageReportProcessor();

            this.initialisedSuccesfully = (this.processor != null && this.processor.Initialise(config, settings, logger));

            return this.initialisedSuccesfully;
        }

        public bool ProcessCoverageReports()
        {
            Debug.Assert(!this.initialised, "Please call Initialise first");
            Debug.Assert(!this.initialisedSuccesfully, "Initialization failed, cannot process coverage reports");

            return this.processor.ProcessCoverageReports();
        }

        /// <summary>
        /// Factory method to create a coverage report processor for the current build environment.
        /// </summary>
        private void TryCreateCoverageReportProcessor()
        {
            if (this.settings.BuildEnvironment == BuildEnvironment.TeamBuild)
            {
                this.processor = new BuildVNextCoverageReportProcessor();
            }

            else if (this.settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                this.processor = new TfsLegacyCoverageReportProcessor();
            }
        }
    }
}