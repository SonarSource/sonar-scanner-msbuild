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

namespace SonarScanner.MSBuild.Common;

public static class TimeoutProvider
{
    // The default HTTP timeout is 100 seconds. https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout#remarks
    public static readonly TimeSpan DefaultHttpTimeout = TimeSpan.FromSeconds(100);

    public static TimeSpan HttpTimeout(IAnalysisPropertyProvider propertiesProvider, ILogger logger) =>
        TimeSpanFor(propertiesProvider, logger, SonarProperties.HttpTimeout, defaultValue: () => TimeSpanFor(propertiesProvider, logger, SonarProperties.ConnectTimeout, () => DefaultHttpTimeout));

    private static TimeSpan TimeSpanFor(IAnalysisPropertyProvider provider, ILogger logger, string property, Func<TimeSpan> defaultValue)
    {
        if (provider.TryGetValue(property, out var timeout))
        {
            if (int.TryParse(timeout, out var timeoutSeconds) && timeoutSeconds > 0)
            {
                return TimeSpan.FromSeconds(timeoutSeconds);
            }
            else
            {
                logger.LogWarning(Resources.WARN_InvalidTimeoutValue, property, timeout, defaultValue().TotalSeconds);
            }
        }

        return defaultValue();
    }
}
