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
        public CoverageReportProcessor(ILegacyTeamBuildFactory legacyTeamBuildFactory)
        {
            this.legacyTeamBuildFactory
                = legacyTeamBuildFactory ?? throw new ArgumentNullException(nameof(legacyTeamBuildFactory));
        }

        public bool Initialise(AnalysisConfig config, ITeamBuildSettings settings, ILogger logger)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            TryCreateCoverageReportProcessor(settings);

            initialisedSuccesfully = (processor != null && processor.Initialise(config, settings, logger));
            return initialisedSuccesfully;
        }

        public bool ProcessCoverageReports()
        {
            Debug.Assert(initialisedSuccesfully, "Initialization failed, cannot process coverage reports");

            return processor.ProcessCoverageReports();
        }

        /// <summary>
        /// Factory method to create a coverage report processor for the current build environment.
        /// </summary>
        private void TryCreateCoverageReportProcessor(ITeamBuildSettings settings)
        {
            if (settings.BuildEnvironment == BuildEnvironment.TeamBuild)
            {
                processor = new BuildVNextCoverageReportProcessor();
            }
            else if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                processor = legacyTeamBuildFactory.BuildTfsLegacyCoverageReportProcessor();
            }
        }
    }
}
