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

/// <summary>
/// This class handles the analysis scope properties that should be ignored and throw a warning if they are set, as they provide no benefit to the analysis configuration.
/// Furthermore, some of them like sonar.sources and sonar.tests lead to double-indexing.
/// </summary>
public class AnalysisScopeProcessor : AnalysisConfigProcessorBase
{
    // Properties that should be replaced by empty values in the local settings, as they can be set both in the server and locally
    public static readonly string[] ReplacedParameters =
    [
        "sonar.inclusions",
        "sonar.test.inclusions",
    ];

    // sonar.sources and sonar.tests cannot be set server-side, so we don't need to replace them, just remove them.
    // Also, we are setting them manually on the end step, so we should not override them here.
    public static readonly string[] IgnoredParameters =
    [
        "sonar.sources",
        "sonar.tests",
        ..ReplacedParameters
    ];

    public override void UpdateConfig(AnalysisConfig config, ProcessedArgs localSettings, IDictionary<string, string> serverProperties)
    {
        var localProperties = localSettings.AllProperties().ToArray();
        var shouldBeIgnored = IgnoredParameters
            .Select(x => new { Key = x, Value = PropertyValue(localProperties, serverProperties, x) })
            .Where(x => x.Value is not null)
            .ToArray();

        if (shouldBeIgnored.Length > 0)
        {
            foreach (var propertyName in shouldBeIgnored.Select(x => x.Key))
            {
                config.LocalSettings.RemoveAll(x => x.Id == propertyName);
                if (ReplacedParameters.Contains(propertyName))
                {
                    AddSetting(config.LocalSettings, propertyName, " "); // Empty string fails the analysis, need the whitespace.
                }
            }
        }
    }
}
