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
using System.Linq;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// The integration assemblies use the SonarQube log specific settings to determine verbosity. These settings are:
/// sonar.log.level is a flag enum with the values INFO, DEBUG and TRACE - and a valid value is INFO|DEBUG
/// sonar.verbosity can be true or false and will override sonar.log.level by setting it to INFO|DEBUG or to INFO
/// All values are case-sensitive. The calculator ignores invalid values.
/// </summary>
public static class VerbosityCalculator
{
    /// <summary>
    /// The initial verbosity of loggers
    /// </summary>
    /// <remarks>
    /// Each of the executables (bootstrapper, pre/post processor) have to parse their command line and config files before
    /// being able to deduce the correct verbosity. In doing that it makes sense to set the logger to be more verbose before
    /// a value can be set so as not to miss out on messages.
    /// </remarks>
    public const LoggerVerbosity InitialLoggingVerbosity = LoggerVerbosity.Debug;

    /// <summary>
    /// The verbosity of loggers if no settings have been specified
    /// </summary>
    public const LoggerVerbosity DefaultLoggingVerbosity = LoggerVerbosity.Info;

    private const string SonarLogDebugValue = "DEBUG";

    /// <summary>
    /// Computes verbosity based on the available properties.
    /// </summary>
    /// <remarks>If no verbosity setting is present, the default verbosity (info) is used</remarks>
    public static LoggerVerbosity ComputeVerbosity(IAnalysisPropertyProvider properties, ILogger logger)
    {
        if (properties == null)
        {
            throw new ArgumentNullException(nameof(properties));
        }

        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        properties.TryGetValue(SonarProperties.Verbose, out var sonarVerboseValue);

        properties.TryGetValue(SonarProperties.LogLevel, out var sonarLogLevelValue);

        return ComputeVerbosity(sonarVerboseValue, sonarLogLevelValue, logger);
    }

    private static LoggerVerbosity ComputeVerbosity(string sonarVerboseValue, string sonarLogValue, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(sonarVerboseValue))
        {
            if (sonarVerboseValue.Equals("true", StringComparison.Ordinal))
            {
                logger.LogDebug(Resources.MSG_SonarVerboseWasSpecified, sonarVerboseValue, LoggerVerbosity.Debug);
                return LoggerVerbosity.Debug;
            }
            else if (sonarVerboseValue.Equals("false", StringComparison.Ordinal))
            {
                logger.LogDebug(Resources.MSG_SonarVerboseWasSpecified, sonarVerboseValue, LoggerVerbosity.Info);
                return LoggerVerbosity.Info;
            }
            else
            {
                logger.LogWarning(Resources.WARN_SonarVerboseNotBool, sonarVerboseValue);
            }
        }

        if (!string.IsNullOrWhiteSpace(sonarLogValue) && sonarLogValue.Split('|').Any(s => s.Equals(SonarLogDebugValue, StringComparison.Ordinal)))
        {
            logger.LogDebug(Resources.MSG_SonarLogLevelWasSpecified, sonarLogValue);
            return LoggerVerbosity.Debug;
        }

        return DefaultLoggingVerbosity;
    }
}
