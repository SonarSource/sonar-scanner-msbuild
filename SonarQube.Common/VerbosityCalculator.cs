//-----------------------------------------------------------------------
// <copyright file="VerbosityCalculator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;

namespace SonarQube.Common
{
    /// <summary>
    /// The integration assemblies use the SonarQube log specific settings to determine verbosity. These settings are:
    /// sonar.log.level is a flag enum with the values INFO, DEBUG and TRACE - and a valid value is INFO|DEBUG
    /// sonar.verbosity can be true or false (case insensitive) and will override sonar.log.level by setting it to INFO|DEBUG or to INFO 
    /// The calculator ignores invalid values.
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
                throw new ArgumentNullException("properties");
            }

            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            string sonarVerboseValue;
            properties.TryGetValue(SonarProperties.Verbose, out sonarVerboseValue);

            string sonarLogLevelValue;
            properties.TryGetValue(SonarProperties.LogLevel, out sonarLogLevelValue);

            return ComputeVerbosity(sonarVerboseValue, sonarLogLevelValue, logger);
        }

        private static LoggerVerbosity ComputeVerbosity(string sonarVerboseValue, string sonarLogValue, ILogger logger)
        {
            if (!String.IsNullOrWhiteSpace(sonarVerboseValue))
            {
                if (sonarVerboseValue.Equals("true", StringComparison.Ordinal))
                {
                    logger.LogDebug(Resources.MSG_SonarVerboseWasSpecified, sonarVerboseValue, LoggerVerbosity.Debug);
                    return LoggerVerbosity.Debug;
                }
                else if (sonarVerboseValue.Equals("false", StringComparison.Ordinal))
                {
                    logger.LogDebug(Resources.MSG_SonarVerboseWasSpecified, sonarVerboseValue, LoggerVerbosity.Debug);
                    return LoggerVerbosity.Info;
                }
                else
                {
                    logger.LogWarning(Resources.WARN_SonarVerboseNotBool, sonarVerboseValue);
                }
            }

            if (!String.IsNullOrWhiteSpace(sonarLogValue))
            {
                if (sonarLogValue.Split('|').Any(s => s.Equals(SonarLogDebugValue, StringComparison.Ordinal)))
                {
                    logger.LogDebug(Resources.MSG_SonarLogLevelWasSpecified, sonarLogValue);
                    return LoggerVerbosity.Debug;
                }
            }

            return DefaultLoggingVerbosity;
        }
    }
}