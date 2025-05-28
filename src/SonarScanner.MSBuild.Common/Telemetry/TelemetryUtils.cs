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

namespace SonarScanner.MSBuild.Common;

public static class TelemetryUtils
{
    private static string[] telemetryProperties = [SonarProperties.ScanAllAnalysis];

    public static void AddTelemetry(ILogger logger, AggregatePropertiesProvider aggregatedProperties)
    {
        foreach (var propertyWithProvider in aggregatedProperties.GetAllPropertiesWithProvider().Where(x => !x.Key.ContainsSensitiveData() && telemetryProperties.Contains(x.Key.Id)))
        {
            logger.AddTelemetryMessage($"dotnetenterprise.s4net.params.{ToTelemetryId(propertyWithProvider.Key.Id)}.value", propertyWithProvider.Key.Value);
            logger.AddTelemetryMessage($"dotnetenterprise.s4net.params.{ToTelemetryId(propertyWithProvider.Key.Id)}.source", propertyWithProvider.Value.ProviderType.ToString());
        }
    }

    public static void AddCIEnvironmentTelemetry(ILogger logger)
    {
        if (CIPlatformDetector.Detect() is var ciPlatform && ciPlatform is not CIPlatform.None)
        {
            logger.AddTelemetryMessage("dotnetenterprise.s4net.ci_platform", ciPlatform.ToString());
        }
    }

    private static string ToTelemetryId(string property) =>
        property.ToLower().Replace('.', '_');
}
