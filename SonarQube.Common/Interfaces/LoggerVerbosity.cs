//-----------------------------------------------------------------------
// <copyright file="LoggerVerbosity.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;

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
}