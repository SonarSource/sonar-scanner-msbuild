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

using System.Collections.Generic;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.TFS.Interfaces;

namespace SonarScanner.MSBuild.TFS
{
    public class BuildVNextCoverageReportProcessor : CoverageReportProcessorBase
    {
        public BuildVNextCoverageReportProcessor(ICoverageReportConverter converter, ILogger logger)
            : base(converter, logger)
        {
        }

        protected override bool TryGetVsCoverageFiles(AnalysisConfig config, ITeamBuildSettings settings, out IEnumerable<string> binaryFilePaths)
        {
            binaryFilePaths = new TrxFileReader(Logger).FindCodeCoverageFiles(settings.BuildDirectory);

            return true; // there aren't currently any conditions under which we'd want to stop processing
        }

        protected override bool TryGetTrxFiles(AnalysisConfig config, ITeamBuildSettings settings, out IEnumerable<string> trxFilePaths)
        {
            trxFilePaths = new TrxFileReader(Logger).FindTrxFiles(settings.BuildDirectory);

            return true;
        }
    }
}
