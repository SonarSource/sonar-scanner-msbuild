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
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;

namespace SonarQube.TeamBuild.PostProcessor
{
    public class CoverageReportProcessorFactory : ICoverageReportProcessorFactory
    {
        private readonly ILegacyTeamBuildFactory legacyTeamBuildFactory;
        private readonly ILogger logger;

        public CoverageReportProcessorFactory(ILegacyTeamBuildFactory legacyTeamBuildFactory, ILogger logger)
        {
            this.legacyTeamBuildFactory = legacyTeamBuildFactory ?? throw new ArgumentNullException(nameof(legacyTeamBuildFactory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Factory method to create a coverage report processor for the current build environment.
        /// </summary>
        public ICoverageReportProcessor Create(ITeamBuildSettings settings)
        {
            if (settings.BuildEnvironment == BuildEnvironment.TeamBuild)
            {
                return new CoverageReportProcessor(
                    new CoverageReportConverter(),
                    new TfsCoverageReportLocator(logger),
                    logger);
            }
            else if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                return new CoverageReportProcessor(
                    new CoverageReportConverter(),
                    legacyTeamBuildFactory.BuildTfsLegacyCoverageReportLocator(logger),
                    logger);
            }
            return null;
        }
    }
}
