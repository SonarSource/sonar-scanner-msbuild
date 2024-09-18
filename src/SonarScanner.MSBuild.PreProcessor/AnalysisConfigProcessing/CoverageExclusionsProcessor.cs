/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

using System.Collections.Generic;
using System.Linq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.AnalysisConfigProcessing;

// See https://sonarsource.atlassian.net/browse/SCAN4NET-29
// This class is a hack and should be removed when we properly support excluding coverage files in the scanner-engine (https://sonarsource.atlassian.net/browse/SCANENGINE-18).
// The idea is that we are manually adding the coverage paths to the exclusions, so that they do not appear on the analysis.
public class CoverageExclusionsProcessor : AnalysisConfigProcessorBase
{
    private const string SonarExclusions = "sonar.exclusions";
    private const string VsCoverageReportsPaths = "sonar.cs.vscoveragexml.reportsPaths";
    private const string OpenCoverReportsPaths = "sonar.cs.opencover.reportsPaths";
    private const string DotCoverReportsPaths = "sonar.cs.dotcover.reportsPaths";

    public override void UpdateConfig(AnalysisConfig config, ProcessedArgs localSettings, IDictionary<string, string> serverProperties)
    {
        var coveragePaths = CoveragePaths(localSettings, serverProperties);
        if (localSettings.ScanAllAnalysis  // If scanAll analysis is disabled, we will not pick up the coverage files anyways
            && coveragePaths.Length > 0)   // If there are no coverage files, there is nothing to exclude
        {
            UpdateConfig(config, localSettings, serverProperties, string.Join(",", coveragePaths));
        }
    }

    private static void UpdateConfig(AnalysisConfig config, ProcessedArgs localSettings, IDictionary<string, string> serverProperties, string coveragePaths)
    {
        var localExclusions = localSettings.GetSetting(SonarExclusions, string.Empty);
        var serverExclusions = serverProperties.ContainsKey(SonarExclusions) ? serverProperties[SonarExclusions] : string.Empty;

        if (string.IsNullOrEmpty(localExclusions) && string.IsNullOrEmpty(serverExclusions))
        {
            localExclusions = coveragePaths;
        }
        else if (string.IsNullOrEmpty(localExclusions))
        {
            localExclusions = string.Join(",", serverExclusions, coveragePaths);
        }
        else
        {
            localExclusions += "," + coveragePaths;
        }
        // Recreate LocalSettings property
        if (config.LocalSettings.Exists(x => x.Id == SonarExclusions)
            || !string.IsNullOrWhiteSpace(localExclusions))
        {
            config.LocalSettings.RemoveAll(x => x.Id == SonarExclusions);
            AddSetting(config.LocalSettings, SonarExclusions, localExclusions);
        }
    }

    private static string[] CoveragePaths(ProcessedArgs localSettings, IDictionary<string, string> serverProperties)
    {
        var localProperties = localSettings.AllProperties().ToArray();
        var coveragePaths = new List<string>
            {
                PropertyValue(localProperties, serverProperties, VsCoverageReportsPaths),
                PropertyValue(localProperties, serverProperties, OpenCoverReportsPaths),
                CoveragePathsAndDirectories(localProperties, serverProperties, DotCoverReportsPaths),
            };

        return coveragePaths.Where(x => x is not null).ToArray();
    }

    private static string CoveragePathsAndDirectories(Property[] localProperties, IDictionary<string, string> serverProperties, string propertyName)
    {
        if (PropertyValue(localProperties, serverProperties, propertyName) is { } coveragePaths)
        {
            var paths = new List<string>();
            foreach (var path in coveragePaths.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                paths.Add(path);

                var lastDot = path.LastIndexOf('.');
                if (lastDot == -1)          // coverage -> coverage/**
                {
                    paths.Add($"{path}/**");
                }
                else if (lastDot > 0)       // coverage.one.html -> coverage.one/**
                {
                    paths.Add($"{path.Substring(0, lastDot)}/**");
                }
            }
            return string.Join(",", paths);
        }
        return null;
    }
}
