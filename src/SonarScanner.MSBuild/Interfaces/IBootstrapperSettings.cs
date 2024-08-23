/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

using System.Collections.Generic;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild;

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
