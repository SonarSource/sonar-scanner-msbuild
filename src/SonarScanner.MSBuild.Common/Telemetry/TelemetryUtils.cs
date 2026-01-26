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

using System.Text.RegularExpressions;

namespace SonarScanner.MSBuild.Common;

public static class TelemetryUtils
{
    // See https://github.com/SonarSource/sonar-dotnet-enterprise/blob/master/sonar-dotnet-core/src/main/java/org/sonarsource/dotnet/shared/plugins/telemetryjson/TelemetryUtils.java
    private static readonly Regex SanitizeKeyRegex = new("[^a-zA-Z0-9]", RegexOptions.Compiled, RegexConstants.DefaultTimeout);

    public static void AddTelemetry(ITelemetry telemetry, AggregatePropertiesProvider aggregatedProperties)
    {
        foreach (var kvp in aggregatedProperties.GetAllPropertiesWithProvider().SelectMany(SelectManyTelemetryProperties))
        {
            telemetry[kvp.Key] = kvp.Value;
        }
    }

    public static void AddTelemetry(ITelemetry telemetry, HostInfo serverInfo)
    {
        if (serverInfo is null)
        {
            return;
        }

        string serverUrl;
        if (serverInfo is CloudHostInfo cloudServerInfo)
        {
            telemetry[TelemetryKeys.ServerInfoRegion] = string.IsNullOrWhiteSpace(cloudServerInfo.Region) ? TelemetryValues.ServerInfoRegion.Default : cloudServerInfo.Region;
            serverUrl = CloudHostInfo.IsKnownUrl(cloudServerInfo.ServerUrl) ? cloudServerInfo.ServerUrl : TelemetryValues.ServerInfoServerUrl.CustomUrl;
        }
        else
        {
            serverUrl = serverInfo.ServerUrl == "http://localhost:9000" ? TelemetryValues.ServerInfoServerUrl.Localhost : TelemetryValues.ServerInfoServerUrl.CustomUrl;
        }

        telemetry[TelemetryKeys.ServerInfoProduct] = serverInfo.IsSonarCloud ? TelemetryValues.Product.Cloud : TelemetryValues.Product.Server;
        telemetry[TelemetryKeys.ServerInfoServerUrl] = serverUrl;
    }

    public static void AddCIEnvironmentTelemetry(ITelemetry telemetry)
    {
        if (CIPlatformDetector.Detect() is { } ciPlatform and not CIPlatform.None)
        {
            telemetry["dotnetenterprise.s4net.ci_platform"] = ciPlatform.ToString();
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> SelectManyTelemetryProperties(KeyValuePair<Property, IAnalysisPropertyProvider> argument)
    {
        var property = argument.Key;
        var value = argument.Key.Value;
        var provider = argument.Value;
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
        else if (value is { } filePath
            && (property.IsKey(SonarProperties.ClientCertPath)
                || property.IsKey(SonarProperties.TruststorePath)
                || property.IsKey(SonarProperties.JavaExePath)))
        {
            // Don't put the file path in the telemetry. The file extension is indicator enough
            return MessagePair(provider, property, FileExtension(filePath));
        }
        else if (value is { } directoryPath
            && (property.IsKey(SonarProperties.PullRequestCacheBasePath)
                || property.IsKey(SonarProperties.VsCoverageXmlReportsPaths)
                || property.IsKey(SonarProperties.VsTestReportsPaths)
                || property.IsKey(SonarProperties.PluginCacheDirectory)
                || property.IsKey(SonarProperties.ProjectBaseDir)
                || property.IsKey(SonarProperties.UserHome)
                || property.IsKey(SonarProperties.WorkingDirectory)))
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
        $"dotnetenterprise.s4net.params.{SanitizeKeyRegex.Replace(property, "_").ToLowerInvariant()}";

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
            return Path.IsPathRooted(directoryPath) ? "rooted" : "relative";
        }
        catch (ArgumentException)
        {
            return "invalid";
        }
    }
}
