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

using SonarScanner.MSBuild.PreProcessor.JreResolution;

namespace SonarScanner.MSBuild.PreProcessor.AnalysisConfigProcessing.Processors;

/// <summary>
/// Map property name to another property name and/or value to pass them through the SONAR_SCANNER_OPTS
/// environment variable to Scanner CLI 5.
/// </summary>
public class TruststorePropertiesProcessor(
    ProcessedArgs localSettings,
    IDictionary<string, string> serverProperties,
    IProcessRunner processRunner,
    IRuntime runtime)
    : AnalysisConfigProcessorBase(localSettings, serverProperties)
{
    public override void Update(AnalysisConfig config)
    {
        if (Uri.TryCreate(LocalSettings.ServerInfo.ServerUrl, UriKind.Absolute, out var uri) && uri.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        var truststorePath = PropertyValueOrDefault(SonarProperties.TruststorePath, LocalSettings.TruststorePath);

        if (truststorePath is null)
        {
            if (runtime.OperatingSystem.IsUnix())
            {
                var truststoreResolver = new LocalJreTruststoreResolver(runtime.File, runtime.Directory, processRunner, runtime.Logger);
                truststorePath = truststoreResolver.UnixTruststorePath(LocalSettings);
            }
            else
            {
                MapProperty(config, SonarProperties.JavaxNetSslTrustStoreType, "Windows-ROOT");
            }
        }

        MapProperty(config, SonarProperties.JavaxNetSslTrustStore, truststorePath, ConvertToJavaPath, EnsureSurroundedByQuotes);
        config.LocalSettings.RemoveAll(x => x.Id is SonarProperties.TruststorePath or SonarProperties.TruststorePassword);

        config.HasBeginStepCommandLineTruststorePassword = LocalSettings.TryGetSetting(SonarProperties.TruststorePassword, out var truststorePassword)
            && !SonarPropertiesDefault.TruststorePasswords.Contains(truststorePassword);
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
            || runtime.OperatingSystem.IsUnix()
            || (str.StartsWith("\"") && str.EndsWith("\"")))
        {
            return str;
        }

        return $@"""{str}""";
    }
}
