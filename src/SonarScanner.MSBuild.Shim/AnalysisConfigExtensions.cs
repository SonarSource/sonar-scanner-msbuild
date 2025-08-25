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

namespace SonarScanner.MSBuild.Shim;

public static class AnalysisConfigExtensions
{
    public const string VSBootstrapperPropertyKey = "sonar.visualstudio.enable";

    /// <summary>
    /// Returns the analysis properties specified through the call.
    /// </summary>
    public static AnalysisProperties ToAnalysisProperties(this AnalysisConfig config, ILogger logger)
    {
        var properties = new AnalysisProperties();

        properties.AddRange(
            config.AnalysisSettings(includeServerSettings: false, logger)
                .GetAllProperties()
                .Where(p => !p.ContainsSensitiveData()));

        // There are some properties we want to override regardless of what the user sets
        AddOrSetProperty(VSBootstrapperPropertyKey, "false", properties, logger);

        if (!properties.Exists(x => x.Id == SonarProperties.HostUrl))
        {
            // The default value for SonarProperties.HostUrl changed in version
            // https://github.com/SonarSource/sonar-scanner-msbuild/releases/tag/7.0.0.95646, but the embedded Scanner-Cli
            // isn't updated with this new default yet (the default is probably changed in version
            // https://github.com/SonarSource/sonar-scanner-cli/releases/tag/6.0.0.4432 of the CLI).
            // As a workaround, we set SonarProperties.HostUrl to the new default in case the parameter isn't already set.
            AddOrSetProperty(SonarProperties.HostUrl, config.SonarQubeHostUrl, properties, logger);
        }

        return properties;
    }

    private static void AddOrSetProperty(string key, string value, AnalysisProperties properties, ILogger logger)
    {
        Property.TryGetProperty(key, properties, out Property property);
        if (property is null)
        {
            logger.LogDebug(Resources.MSG_SettingAnalysisProperty, key, value);
            property = new(key, value);
            properties.Add(property);
        }
        else
        {
            if (string.Equals(property.Value, value, StringComparison.InvariantCulture))
            {
                logger.LogDebug(Resources.MSG_MandatorySettingIsCorrectlySpecified, key, value);
            }
            else
            {
                logger.LogWarning(Resources.WARN_OverridingAnalysisProperty, key, value);
                property.Value = value;
            }
        }
    }
}
