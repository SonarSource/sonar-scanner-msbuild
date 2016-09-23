//-----------------------------------------------------------------------
// <copyright file="IBootstrapperSettings.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;

namespace SonarQube.Bootstrapper
{
    public enum AnalysisPhase
    {
        Unspecified = 0,
        PreProcessing,
        PostProcessing
    }

    /// <summary>
    /// Returns the settings required by the bootstrapper
    /// </summary>
    public interface IBootstrapperSettings
    {
        string SonarQubeUrl { get; }

        /// <summary>
        /// Temporary analysis directory, usually .sonarqube
        /// </summary>
        string TempDirectory { get; }

        AnalysisPhase Phase { get; }

        /// <summary>
        /// The command line arguments to pass to the child process
        /// </summary>
        IEnumerable<string> ChildCmdLineArgs { get; }

        /// <summary>
        /// The level of detail that should be logged
        /// </summary>
        /// <remarks>Should be in sync with the SQ components</remarks>
        LoggerVerbosity LoggingVerbosity { get; }

        /// <summary>
        /// Path of the directory where scanner binaries are located
        /// </summary>
        string ScannerBinaryDirPath { get; }
    }
}
