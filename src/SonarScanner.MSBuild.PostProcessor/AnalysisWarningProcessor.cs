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

using System;
using System.Linq;
using Newtonsoft.Json;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PostProcessor;

// see https://github.com/SonarSource/sonar-dotnet-autoscan/blob/e6c57158bc8842b0aa495180f98819a16d0cbe54/AutoScan.NET/Logging/WarningsSerializer.cs#L25
public static class AnalysisWarningProcessor
{
    public static void Process(string[] warnings, string outputPath, IFileWrapper fileWrapper, ILogger logger)
    {
        _ = warnings ?? throw new ArgumentNullException(nameof(warnings));
        _ = outputPath ?? throw new ArgumentNullException(nameof(outputPath));
        _ = fileWrapper ?? throw new ArgumentNullException(nameof(fileWrapper));
        _ = logger ?? throw new ArgumentNullException(nameof(logger));

        if (warnings.Any())
        {
            foreach (var warning in warnings)
            {
                logger.LogWarning(warning);
            }
            var warningsJson = JsonConvert.SerializeObject(warnings.Select(x => new { text = x }).ToArray(), Formatting.Indented);
            fileWrapper.WriteAllText(outputPath, warningsJson);
        }
    }
}
