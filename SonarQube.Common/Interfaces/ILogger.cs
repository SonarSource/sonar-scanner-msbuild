//-----------------------------------------------------------------------
// <copyright file="ILogger.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarQube.Common
{
    /// <summary>
    /// Level of detail for the log messages.
    /// </summary>
    /// <remarks>
    /// Does not cover warnings and errors.
    /// The levels are in step with the SonarQube verbosity levels (http://docs.sonarqube.org/display/SONAR/Server+Log+Management):
    /// Info, Debug (for advanced logs), Trace (for advanced logs and logs that might have a perf impact)
    /// </remarks>
    public enum LoggerVerbosity
    {
        /// <summary>
        /// Important messages that always get logged
        /// </summary>
        Info = 0,

        /// <summary>
        /// Advanced information messages that help in debugging scenarios
        /// </summary>
        Debug = 1
    }

    /// <summary>
    /// Simple logging interface
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Log a message with the Debug verbosity
        /// </summary>
        void LogDebug(string message, params object[] args);

        /// <summary>
        /// Log a message with the Info verbosity
        /// </summary>
        void LogInfo(string message, params object[] args);
        
        void LogWarning(string message, params object[] args);
        
        void LogError(string message, params object[] args);

        /// <summary>
        /// Gets or sets the level of detail to show in the log
        /// </summary>
        LoggerVerbosity Verbosity { get; set; }

        /// <summary>
        /// Gets or sets whether log entries are prefixed with timestamps
        /// </summary>
        bool IncludeTimestamp { get; set; }
    }
}
