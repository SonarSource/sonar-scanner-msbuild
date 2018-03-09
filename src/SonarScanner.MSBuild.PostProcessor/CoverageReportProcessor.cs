/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System;
using System.Diagnostics;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.Integration.Interfaces;

namespace SonarQube.TeamBuild.PostProcessor
{
    public class CoverageReportProcessor : ICoverageReportProcessor
    {
        private ICoverageReportProcessor processor;
        private bool initialisedSuccesfully;

        private readonly ILegacyTeamBuildFactory legacyTeamBuildFactory;
        private readonly ICoverageReportConverter coverageReportConverter;

        public CoverageReportProcessor(ILegacyTeamBuildFactory legacyTeamBuildFactory,
            ICoverageReportConverter coverageReportConverter)
        {
            this.legacyTeamBuildFactory
                = legacyTeamBuildFactory ?? throw new ArgumentNullException(nameof(legacyTeamBuildFactory));
            this.coverageReportConverter
                = coverageReportConverter ?? throw new ArgumentNullException(nameof(coverageReportConverter));
        }

        public bool Initialise(AnalysisConfig config, ITeamBuildSettings settings, ILogger logger)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            TryCreateCoverageReportProcessor(settings);

            initialisedSuccesfully = (processor != null && processor.Initialise(config, settings, logger));
            return initialisedSuccesfully;
        }

        public bool ProcessCoverageReports()
        {
            Debug.Assert(initialisedSuccesfully, "Initialization failed, cannot process coverage reports");

            // If we return false then processing will stop so if in doubt return true
            return processor?.ProcessCoverageReports() ?? true;
        }

        /// <summary>
        /// Factory method to create a coverage report processor for the current build environment.
        /// </summary>
        private void TryCreateCoverageReportProcessor(ITeamBuildSettings settings)
        {
            if (settings.BuildEnvironment == BuildEnvironment.TeamBuild)
            {
                processor = new BuildVNextCoverageReportProcessor(coverageReportConverter);
            }
            else if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                processor = legacyTeamBuildFactory.BuildTfsLegacyCoverageReportProcessor();
            }
        }
    }
}
