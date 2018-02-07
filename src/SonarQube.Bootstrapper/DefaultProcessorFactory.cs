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
using SonarQube.TeamBuild.PostProcessor;
using SonarQube.TeamBuild.PostProcessor.Interfaces;
using SonarQube.TeamBuild.PreProcessor;
using SonarScanner.Shim;

namespace SonarQube.Bootstrapper
{
    public class DefaultProcessorFactory : IProcessorFactory
    {
        private readonly ILogger logger;
        private readonly ILegacyTeamBuildFactory legacyTeamBuildFactory;

        public DefaultProcessorFactory(ILogger logger, ILegacyTeamBuildFactory legacyTeamBuildFactory)
        {
            this.logger = logger;
            this.legacyTeamBuildFactory
                = legacyTeamBuildFactory ?? throw new ArgumentNullException(nameof(legacyTeamBuildFactory));
        }

        public IMSBuildPostProcessor CreatePostProcessor()
        {
            return new MSBuildPostProcessor(
                new CoverageReportProcessor(legacyTeamBuildFactory),
                new SonarScannerWrapper(),
                new SummaryReportBuilder(legacyTeamBuildFactory),
                logger,
                new TargetsUninstaller());
        }

        public ITeamBuildPreProcessor CreatePreProcessor()
        {
            IPreprocessorObjectFactory factory = new PreprocessorObjectFactory();
            return new TeamBuildPreProcessor(factory, logger);
        }
    }
}
