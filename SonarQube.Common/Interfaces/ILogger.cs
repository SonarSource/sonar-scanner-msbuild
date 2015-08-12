//-----------------------------------------------------------------------
// <copyright file="ILogger.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarQube.Common
{
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
    }
}
