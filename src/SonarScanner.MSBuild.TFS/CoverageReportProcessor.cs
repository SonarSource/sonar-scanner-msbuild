/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
 * mailto: info AT sonarsource DOT com
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

using SonarScanner.MSBuild.Common.TFS;

namespace SonarScanner.MSBuild.TFS;

public class CoverageReportProcessor : ICoverageReportProcessor
{
    private readonly ILegacyTeamBuildFactory legacyTeamBuildFactory;
    private ICoverageReportProcessor processor;
    private bool initializedSuccessfully;

    public CoverageReportProcessor(ILegacyTeamBuildFactory legacyTeamBuildFactory)
    {
        this.legacyTeamBuildFactory
            = legacyTeamBuildFactory ?? throw new ArgumentNullException(nameof(legacyTeamBuildFactory));
    }

    public bool Initialize(AnalysisConfig config, IBuildSettings settings, string propertiesFilePath)
    {
        _ = settings ?? throw new ArgumentNullException(nameof(settings));

        TryCreateCoverageReportProcessor(settings);

        initializedSuccessfully = processor is not null && processor.Initialize(config, settings, propertiesFilePath);
        return initializedSuccessfully;
    }

    public bool ProcessCoverageReports(ILogger logger)
    {
        Debug.Assert(initializedSuccessfully, "Initialization failed, cannot process coverage reports");

        // If we return false then processing will stop so if in doubt return true
        return processor?.ProcessCoverageReports(logger) ?? true;
    }

    /// <summary>
    /// Factory method to create a coverage report processor for the current build environment.
    /// </summary>
    private void TryCreateCoverageReportProcessor(IBuildSettings settings)
    {
        if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild && !BuildSettings.SkipLegacyCodeCoverageProcessing)
        {
            processor = legacyTeamBuildFactory.BuildTfsLegacyCoverageReportProcessor();
        }
    }
}
