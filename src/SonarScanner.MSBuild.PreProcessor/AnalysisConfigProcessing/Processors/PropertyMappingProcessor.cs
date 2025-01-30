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
using System.Collections.Generic;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.AnalysisConfigProcessing.Processors;

/// <summary>
/// Map property name to another property name and/or value to match Scanner CLI 5 expectations.
/// </summary>
public class PropertyMappingProcessor(ProcessedArgs localSettings, IDictionary<string, string> serverProperties)
    : AnalysisConfigProcessorBase(localSettings, serverProperties)
{
    private static readonly IDictionary<string, string> MappedId = new Dictionary<string, string>
    {
        { "sonar.scanner.truststorePath", "javax.net.ssl.trustStore" },
        { "sonar.scanner.truststorePassword", "javax.net.ssl.trustStorePassword" }
    };

    private static readonly IDictionary<string, Func<string, string>> MappedValue = new Dictionary<string, Func<string, string>>
    {
        { "sonar.scanner.truststorePath", ConvertToJavaPath },
    };

    public override void Update(AnalysisConfig config)
    {
        foreach (var property in config.LocalSettings)
        {
            if (MappedValue.TryGetValue(property.Id, out var mappedValue))
            {
                property.Value = mappedValue(property.Value);
            }
            if (MappedId.TryGetValue(property.Id, out var mappedProperty))
            {
                property.Id = mappedProperty;
            }
        }
    }

    private static string ConvertToJavaPath(string path) =>
        path?.Replace('\\', '/');
}
