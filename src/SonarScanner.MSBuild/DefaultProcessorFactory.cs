/*
 * SonarScanner for MSBuild
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
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.TFS;
using SonarScanner.MSBuild.PostProcessor;
using SonarScanner.MSBuild.PostProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor;
using SonarScanner.MSBuild.Shim;

namespace SonarScanner.MSBuild
{
    public class DefaultProcessorFactory : IProcessorFactory
    {
        private readonly ILogger logger;
        private readonly ILegacyTeamBuildFactory legacyTeamBuildFactory;
        private readonly ICoverageReportConverter coverageReportConverter;

        public DefaultProcessorFactory(ILogger logger,
            ILegacyTeamBuildFactory legacyTeamBuildFactory,
            ICoverageReportConverter coverageReportConverter)
        {
            this.logger = logger;
            this.legacyTeamBuildFactory
                = legacyTeamBuildFactory ?? throw new ArgumentNullException(nameof(legacyTeamBuildFactory));
            this.coverageReportConverter
                = coverageReportConverter ?? throw new ArgumentNullException(nameof(coverageReportConverter));
        }

        public IMSBuildPostProcessor CreatePostProcessor()
        {
            return new MSBuildPostProcessor(
                new CoverageReportProcessor(legacyTeamBuildFactory, coverageReportConverter, logger),
                new SonarScannerWrapper(logger),
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
