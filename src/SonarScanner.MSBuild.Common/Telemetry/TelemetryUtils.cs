﻿/*
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


using System.Runtime.InteropServices;

namespace SonarScanner.MSBuild.Common;

public static class TelemetryUtils
{
    public static void AddTelemetry(ILogger logger, AggregatePropertiesProvider aggregatedProperties)
    {
        foreach (var kvp in aggregatedProperties.GetAllPropertiesWithProvider().SelectMany(SelectMany))
        {
            logger.AddTelemetryMessage(kvp.Key, kvp.Value);
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> SelectMany(KeyValuePair<Property, IAnalysisPropertyProvider> argument)
    {
        var property = argument.Key;
        if (property.ContainsSensitiveData()
            // Further senstive parameters
            || property.IsKey(SonarProperties.Organization)
            || property.IsKey(SonarProperties.ProjectKey)
            // Should be extracted from ServerInfo
            || property.IsKey(SonarProperties.HostUrl)
            || property.IsKey(SonarProperties.ApiBaseUrl)
            || property.IsKey(SonarProperties.SonarcloudUrl)
            || property.IsKey(SonarProperties.Region))
        {
            return [];
        }
        var value = argument.Key.Value;
        var provider = argument.Value;
        if ((property.IsKey(SonarProperties.ClientCertPath)
            || property.IsKey(SonarProperties.TruststorePath)
            || property.IsKey(SonarProperties.JavaExePath)) && value is { } filePath)
        {
            // Don't put the file path in the telemetry. The file extension is indicator enough
            return MessagePair(provider, property, FileExtension(filePath));
        }
        else if ((property.IsKey(SonarProperties.PullRequestCacheBasePath)
            || property.IsKey(SonarProperties.VsCoverageXmlReportsPaths)
            || property.IsKey(SonarProperties.VsTestReportsPaths)
            || property.IsKey(SonarProperties.PluginCacheDirectory)
            || property.IsKey(SonarProperties.ProjectBaseDir)
            || property.IsKey(SonarProperties.UserHome)
            || property.IsKey(SonarProperties.WorkingDirectory)) && value is { } directoryPath)
        {
            // Don't write directories to telemetry. Just specify if the path was absolute or relative
            return MessagePair(provider, property, PathCharacteristics(directoryPath));
        }
        else if (property.IsKey(SonarProperties.OperatingSystem)
            || property.IsKey(SonarProperties.Architecture)
            || property.IsKey(SonarProperties.SourceEncoding)
            || property.IsKey(SonarProperties.JavaxNetSslTrustStoreType))
        {
            // Whitelist of the properties that are logged with their value
            return MessagePair(provider, property);
        }
        else
        {
            // Default: Write the source of the specified property but not its value
            return MessagePair(provider, property, null);
        }
    }

    private static string FileExtension(string filePath)
    {
        try
        {
            return Path.GetExtension(filePath);
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    private static string PathCharacteristics(string directoryPath)
    {
        try
        {
            if (Path.IsPathRooted(directoryPath))
            {
                return "rooted";
            }
            else
            {
                return "relative";
            }
        }
        catch (ArgumentException)
        {
            return "invalid";
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> MessagePair(IAnalysisPropertyProvider source, Property property, string value)
    {
        var telemetryKey = $"{ToTelemetryId(property.Id)}";
        yield return new($"{telemetryKey}.source", source.ProviderType.ToString());
        if (!string.IsNullOrWhiteSpace(value))
        {
            yield return new($"{telemetryKey}.value", value);
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> MessagePair(IAnalysisPropertyProvider source, Property property) =>
        MessagePair(source, property, property.Value);

    private static string ToTelemetryId(string property) =>
        $"dotnetenterprise.s4net.params.{property.ToLower().Replace('.', '_')}";
}
