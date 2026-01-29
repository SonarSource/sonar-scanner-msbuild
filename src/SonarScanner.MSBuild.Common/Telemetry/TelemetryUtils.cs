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
    private static readonly Regex SanitizeKeyRegex = new("[^a-zA-Z0-9]", RegexOptions.None, RegexConstants.DefaultTimeout);

    public static string SanitizeKey(string key) =>
        SanitizeKeyRegex.Replace(key, "_");

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

    internal static string ToTelemetryId(string property) =>
        $"dotnetenterprise.s4net.params.{SanitizeKey(property).ToLowerInvariant()}";

    private static IEnumerable<KeyValuePair<string, string>> SelectManyTelemetryProperties(KeyValuePair<Property, IAnalysisPropertyProvider> argument)
    {
        var property = argument.Key;
        var value = argument.Key.Value;
        var provider = argument.Value;
        if (property.ContainsSensitiveData()
            // Further senstive parameters
            || property.IsKey(SonarProperties.Organization)
            || property.IsKey(SonarProperties.ProjectKey)
            || property.IsKey("sonar.scanner.proxyUser")
            || property.IsKey("sonar.scanner.proxyPassword")
            || property.IsKey("sonar.scanner.keystorePassword")
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
        else if (IsSourceOnlyWhitelisted(property))
        {
            // Report source only, not the value
            // See https://docs.google.com/spreadsheets/d/1L682GZWwVw5xUZPaFbYlJYN1m9whBu-uo1S-ZkpPq9A for the full list of whitelisted properties
            return MessagePair(provider, property, null);
        }
        else
        {
            // Default: Unknown properties are not reported
            return [];
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

    private static bool IsSourceOnlyWhitelisted(Property property)
    {
        var id = property.Id;
        // Properties from SonarProperties class
        if (property.IsKey(SonarProperties.CacheBaseUrl)
            || property.IsKey(SonarProperties.SkipJreProvisioning)
            || property.IsKey(SonarProperties.EngineJarPath)
            || property.IsKey(SonarProperties.UseSonarScannerCLI)
            || property.IsKey(SonarProperties.ConnectTimeout)
            || property.IsKey(SonarProperties.SocketTimeout)
            || property.IsKey(SonarProperties.ResponseTimeout)
            || property.IsKey(SonarProperties.ScanAllAnalysis)
            || property.IsKey(SonarProperties.ProjectBranch)
            || property.IsKey(SonarProperties.ProjectName)
            || property.IsKey(SonarProperties.ProjectVersion)
            || property.IsKey(SonarProperties.PullRequestBase)
            || property.IsKey(SonarProperties.Verbose)
            || property.IsKey(SonarProperties.LogLevel)
            || property.IsKey(SonarProperties.HttpTimeout)
            || property.IsKey(SonarProperties.Sources)
            || property.IsKey(SonarProperties.Tests)
            || property.IsKey(SonarProperties.JavaxNetSslTrustStore))
        {
            return true;
        }

        // Additional known properties (string literals)
        // https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/analysis-parameters/parameters-not-settable-in-ui
        if (property.IsKey("sonar.projectDescription")
            || property.IsKey("sonar.links.homepage")
            || property.IsKey("sonar.links.ci")
            || property.IsKey("sonar.links.issue")
            || property.IsKey("sonar.links.scm")
            || property.IsKey("sonar.externalIssuesReportPaths")
            || property.IsKey("sonar.sarifReportPaths")
            || property.IsKey("sonar.scm.exclusions.disabled")
            || property.IsKey("sonar.filesize.limit")
            || property.IsKey("sonar.log.level")
            || property.IsKey("sonar.scanner.metadataFilePath")
            || property.IsKey("sonar.qualitygate.wait")
            || property.IsKey("sonar.qualitygate.timeout")
            || property.IsKey("sonar.branch.name")
            || property.IsKey("sonar.pullrequest.key")
            || property.IsKey("sonar.pullrequest.branch")
            || property.IsKey("sonar.newCode.referenceBranch")
            || property.IsKey("sonar.scanner.keepReport")
            || property.IsKey("sonar.plugins.download.timeout")
            || property.IsKey("sonar.scanner.proxyHost")
            || property.IsKey("sonar.scanner.proxyPort")
            || property.IsKey("sonar.scanner.keystorePath")
            || property.IsKey("sonar.scm.revision")
            || property.IsKey("sonar.buildString")
            || property.IsKey("sonar.scanner.javaOpts")
            // https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/analysis-scope/narrowing-the-focus
            || property.IsKey("sonar.exclusions")
            || property.IsKey("sonar.test.exclusions")
            || property.IsKey("sonar.global.exclusions")
            || property.IsKey("sonar.coverage.exclusions")
            || property.IsKey("sonar.cpd.exclusions")
            || property.IsKey("sonar.inclusions")
            || property.IsKey("sonar.test.inclusions")
            // https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/scm-integration
            || property.IsKey("sonar.scm.provider")
            || property.IsKey("sonar.scm.forceReloadAll")
            || property.IsKey("sonar.scm.disabled")
            // Deprecated parameters
            || property.IsKey("sonar.ws.timeout")
            || property.IsKey("sonar.projectDate")
            || property.IsKey("sonar.scanner.dumpToFile")
            // Undocumented parameters
            || property.IsKey("sonar.branch.autoconfig.disabled"))
        {
            return true;
        }

        // Pattern-based properties
        // https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/analysis-parameters/parameters-not-settable-in-ui
        // sonar.analysis.* (e.g., sonar.analysis.yourKey)
        if (id.StartsWith("sonar.analysis.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/analysis-parameters/parameters-not-settable-in-ui
        // sonar.cpd.{language}.minimumTokens and sonar.cpd.{language}.minimumLines
        if (id.StartsWith("sonar.cpd.", StringComparison.OrdinalIgnoreCase)
            && (id.EndsWith(".minimumTokens", StringComparison.OrdinalIgnoreCase)
                || id.EndsWith(".minimumLines", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Cobertura-related properties (e.g., sonar.cs.cobertura, sonar.cs.cobertura.reportPaths) - not supported yet but users are trying
        if (id.EndsWith(".cobertura", StringComparison.OrdinalIgnoreCase)
            || id.IndexOf(".cobertura.", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        // https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/analysis-parameters/parameters-not-settable-in-ui
        // SCA (Software Composition Analysis) properties (e.g., sonar.sca.enabled, sonar.sca.exclusions)
        if (id.StartsWith("sonar.sca.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
