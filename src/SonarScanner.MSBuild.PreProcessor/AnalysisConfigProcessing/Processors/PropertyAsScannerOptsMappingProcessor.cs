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
using System.IO;
using System.Linq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.AnalysisConfigProcessing.Processors;

/// <summary>
/// Map property name to another property name and/or value to pass them through the SONAR_SCANNER_OPTS
/// environment variable to Scanner CLI 5.
/// </summary>
public class PropertyAsScannerOptsMappingProcessor : AnalysisConfigProcessorBase
{
    private static readonly IDictionary<string, string> MappedId = new Dictionary<string, string>
    {
        { SonarProperties.TruststorePath, "javax.net.ssl.trustStore" },
        { SonarProperties.TruststorePassword, "javax.net.ssl.trustStorePassword" }
    };

    private readonly IOperatingSystemProvider operatingSystemProvider;
    private readonly IDictionary<string, List<Func<string, string>>> mappedValue;

    public PropertyAsScannerOptsMappingProcessor(
        ProcessedArgs localSettings,
        IDictionary<string, string> serverProperties,
        IOperatingSystemProvider operatingSystemProvider) : base(localSettings, serverProperties)
    {
        this.operatingSystemProvider = operatingSystemProvider;
        mappedValue = new Dictionary<string, List<Func<string, string>>>
        {
            { SonarProperties.TruststorePath, [ConvertToJavaPath, EnsureSurroundedByQuotes] },
            { SonarProperties.TruststorePassword, [EnsureSurroundedByQuotes] },
        };
    }

    public override void Update(AnalysisConfig config)
    {
        var toRemove = new List<Property>();

        foreach (var property in config.LocalSettings)
        {
            if (mappedValue.TryGetValue(property.Id, out var value))
            {
                property.Value = value.Aggregate(property.Value, (current, fn) => fn(current));
            }
            if (MappedId.TryGetValue(property.Id, out var mappedProperty))
            {
                property.Id = mappedProperty;
                config.ScannerOptsSettings.Add(property);
                toRemove.Add(property);
            }
        }

        config.LocalSettings.RemoveAll(x => toRemove.Contains(x));
    }

    private static string ConvertToJavaPath(string path) =>
        path?.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    // We need to make sure that the value is surrounded by quotes in the case it
    // contains spaces.
    // This might not work well with passwords that contain quotes, we try to escape
    // them here as much as we can.
    // If the value is surrounded by quotes, we assume that all the characters are
    // properly escaped if needed.
    private string EnsureSurroundedByQuotes(string str)
    {
        if (str is null
            || operatingSystemProvider.IsUnix()
            || (str.StartsWith("\"") && str.EndsWith("\"")))
        {
            return str;
        }

        return $@"""{str}""";
    }
}
