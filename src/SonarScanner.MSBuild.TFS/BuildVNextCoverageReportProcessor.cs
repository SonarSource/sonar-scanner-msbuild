/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

namespace SonarScanner.MSBuild.TFS;

public class BuildVNextCoverageReportProcessor : CoverageReportProcessorBaseCopy
{
    internal bool TrxFilesLocated { get; private set; }

    private readonly IBuildVNextCoverageSearchFallback searchFallback;

    public BuildVNextCoverageReportProcessor(ICoverageReportConverter converter, ILogger logger)
        : this(converter, logger, new BuildVNextCoverageSearchFallback(logger))
    {
    }

    internal /* for testing */ BuildVNextCoverageReportProcessor(ICoverageReportConverter converter, ILogger logger,
        IBuildVNextCoverageSearchFallback searchFallback)
        : base(converter, logger)
    {
        this.searchFallback = searchFallback;
    }

    protected override bool TryGetVsCoverageFiles(AnalysisConfig config, IBuildSettings settings, out IEnumerable<string> binaryFilePaths)
    {
        binaryFilePaths = new TrxFileReader(Logger).FindCodeCoverageFiles(settings.BuildDirectory);

        // Fallback to workround SONARAZDO-179: if the standard searches for .trx/.converage failed
        // then try the fallback method to find coverage files
        if (!TrxFilesLocated && (binaryFilePaths == null || !binaryFilePaths.Any()))
        {
            Logger.LogInfo("Did not find any binary coverage files in the expected location.");
            binaryFilePaths = searchFallback.FindCoverageFiles();
        }
        else
        {
            Logger.LogDebug("Not using the fallback mechanism to detect binary coverage files.");
        }

        return true; // there aren't currently any conditions under which we'd want to stop processing
    }

    protected override bool TryGetTrxFiles(IBuildSettings settings, out IEnumerable<string> trxFilePaths)
    {
        trxFilePaths = new TrxFileReader(Logger).FindTrxFiles(settings.BuildDirectory);

        TrxFilesLocated = trxFilePaths != null && trxFilePaths.Any();
        return true;
    }

    // We can't make the override internal, so this is a pass-through for testing
    internal /* for testing */ bool TryGetVsCoverageFilesAccessor(AnalysisConfig config, IBuildSettings settings, out IEnumerable<string> binaryFilePaths) =>
        TryGetVsCoverageFiles(config, settings, out binaryFilePaths);

    internal /* for testing */ bool TryGetTrxFilesAccessor(AnalysisConfig config, IBuildSettings settings, out IEnumerable<string> trxFilePaths) =>
        this.TryGetTrxFiles(settings, out trxFilePaths);

}
