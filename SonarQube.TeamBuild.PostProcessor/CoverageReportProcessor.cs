/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
 
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