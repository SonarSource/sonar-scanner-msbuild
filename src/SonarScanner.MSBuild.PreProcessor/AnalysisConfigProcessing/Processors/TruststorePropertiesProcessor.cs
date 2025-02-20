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

namespace SonarScanner.MSBuild.PreProcessor.AnalysisConfigProcessing.Processors;

/// <summary>
/// Map property name to another property name and/or value to pass them through the SONAR_SCANNER_OPTS
/// environment variable to Scanner CLI 5.
/// </summary>
public class TruststorePropertiesProcessor(
    ProcessedArgs localSettings,
    IDictionary<string, string> serverProperties,
    IFileWrapper fileWrapper,
    ILogger logger,
    IOperatingSystemProvider operatingSystemProvider)
    : AnalysisConfigProcessorBase(localSettings, serverProperties)
{
    private const string JavaxNetSslTrustStore = "javax.net.ssl.trustStore";
    private const string JavaxNetSslTrustStoreType = "javax.net.ssl.trustStoreType";
    private const string JavaxNetSslTrustStorePassword = "javax.net.ssl.trustStorePassword";

    public override void Update(AnalysisConfig config)
    {
        if ((Uri.TryCreate(LocalSettings.ServerInfo.ServerUrl, UriKind.Absolute, out var uri) && uri.Scheme != Uri.UriSchemeHttps)
            || LocalSettings.ServerInfo.ServerUrl is SonarPropertiesDefault.SonarcloudUrl)
        {
            return;
        }

        var truststorePath = PropertyValueOrDefault(SonarProperties.TruststorePath, LocalSettings.TruststorePath);
        var truststorePassword = PropertyValueOrDefault(SonarProperties.TruststorePassword, LocalSettings.TruststorePassword);

        if (truststorePath is null)
        {
            if (operatingSystemProvider.IsUnix())
            {
                truststorePath = FindJavaTruststorePath();
                truststorePassword ??= TruststorePasswordFromEnvironment();
            }
            else
            {
                MapProperty(config, JavaxNetSslTrustStoreType, "Windows-ROOT");
            }
        }

        MapProperty(config, JavaxNetSslTrustStore, truststorePath, ConvertToJavaPath, EnsureSurroundedByQuotes);
        config.LocalSettings.RemoveAll(x => x.Id is SonarProperties.TruststorePath or SonarProperties.TruststorePassword);

        if (truststorePassword is not null)
        {
            MapProperty(config, JavaxNetSslTrustStorePassword, truststorePassword, EnsureSurroundedByQuotes);
            config.LocalSettings.Add(new Property(SonarProperties.TruststorePassword, truststorePassword));
        }
    }

    private static string TruststorePasswordFromEnvironment()
    {
        var sonarScannerOpts = Environment.GetEnvironmentVariable("SONAR_SCANNER_OPTS");
        if (sonarScannerOpts is null)
        {
            return null;
        }

        return sonarScannerOpts.Split(' ').FirstOrDefault(x => x.StartsWith($"-D{JavaxNetSslTrustStorePassword}=")) is { } truststorePassword
            ? truststorePassword.Substring($"-D{JavaxNetSslTrustStorePassword}=".Length)
            : null;
    }

    private string FindJavaTruststorePath()
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (javaHome is null)
        {
            logger.LogDebug(Resources.MSG_JavaHomeNotSet);
            return null;
        }

        // https://docs.oracle.com/en/java/javase/17/security/java-cryptography-architecture-jca-reference-guide.html#GUID-AB51DEFD-5238-4F96-967F-082F6D34FBEA
        var javaTruststorePath = Path.Combine(javaHome, "lib", "security", "cacerts");
        if (fileWrapper.Exists(javaTruststorePath))
        {
            return javaTruststorePath;
        }
        logger.LogDebug(Resources.MSG_JavaHomeCacertsNotFound, javaTruststorePath);
        return null;
    }

    private string PropertyValueOrDefault(string propertyName, string defaultValue) =>
        LocalSettings.TryGetSetting(propertyName, out var localValue)
            ? localValue
            : defaultValue;

    private static void MapProperty(AnalysisConfig config, string id, string value, params Func<string, string>[] valueTransformations)
    {
        value = valueTransformations.Aggregate(value, (current, fn) => fn(current));
        if (value is not null)
        {
            config.ScannerOptsSettings.Add(new Property(id, value));
        }
    }

    private static string ConvertToJavaPath(string path) =>
        path?.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    // We need to make sure that the value is surrounded by quotes in the case it
    // contains spaces.
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
