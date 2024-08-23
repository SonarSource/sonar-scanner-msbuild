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
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor;

public interface IAnalyzerProvider
{
    /// <summary>
    /// Sets up a Roslyn analyzer to run as part of the build
    /// i.e. creates the Roslyn ruleset and provisions the analyzer's assemblies
    /// and rule parameter files
    /// </summary>
    /// <param name="projectKey">Identifier for the project being analyzed</param>
    /// <returns>The settings required to configure the build for Roslyn a analyzer</returns>
    AnalyzerSettings SetupAnalyzer(BuildSettings teamBuildSettings, IAnalysisPropertyProvider sonarProperties, IEnumerable<SonarRule> rules, string language);
}
